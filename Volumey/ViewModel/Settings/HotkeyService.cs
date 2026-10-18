#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using log4net;
using Volumey.Controls;
using Volumey.DataProvider;
using Volumey.Helper;

namespace Volumey.ViewModel.Settings;

/// <summary>
/// The app's hotkeys: holds the hotkey managers, counts the users of each hotkey so several features can share one,
/// raises the pressed hotkeys and validates new ones. One instance for the app (<see cref="Services.ServiceRegistration"/>).
/// Keyboard hotkeys go to the keyboard manager (blocking or shared, <see cref="SetHotkeyManager"/>), mouse hotkeys to a mouse
/// manager that only exists while a mouse hotkey is registered.
/// </summary>
public sealed class HotkeyService : IHotkeyService
{
	private static ILog? logger;
	private static ILog Logger => logger ??= LogManager.GetLogger(typeof(HotkeyService));

	/// <summary>
	/// Raised when the first hotkey manager is set and hotkeys can be registered
	/// </summary>
	public event Action? Activated;

	private Action<HotKey>? _hotkeyPressed;
	public event Action<HotKey> HotkeyPressed
	{
		add
		{
			//Prevent multiple subscribing of the same event handler
			_hotkeyPressed -= value;
			_hotkeyPressed += value;
		}
		remove => _hotkeyPressed -= value;
	}
	public event Action? OpenHotkeyPressed;
	/// <summary>
	/// Indicates whether a hotkey manager instance is set and hotkeys can be registered.
	/// </summary>
	public bool IsActive { get; private set; }

	public int VolumeStep { get; private set; } = 1;

	private HotKey? openMixer;
	private IHotkeyManager? hotkeyManager;
	private IHotkeyManager? mouseManager;
	private readonly Func<IHotkeyManager>? createMouseManager;

	/// <summary>
	/// Keeps track of hotkeys and amount of its users to prevent early unregistering of hotkeys.
	/// </summary>
	private readonly Dictionary<HotKey, int> HotkeysUserCount = new Dictionary<HotKey, int>();

	private void IncrementHotkeyUserCounter(HotKey hotkey)
	{
		if(HotkeysUserCount.ContainsKey(hotkey))
			HotkeysUserCount[hotkey]++;
		else
			HotkeysUserCount.Add(hotkey, 1);
	}

	/// <param name="hotkey"></param>
	/// <param name="lastUser">Flag to check if the call was for the last user and hotkey can be unregistered.</param>
	private void DecrementHotkeyUserCounter(HotKey hotkey, out bool lastUser)
	{
		if(HotkeysUserCount.TryGetValue(hotkey, out var userCount))
		{
			if(userCount == 1)
			{
				HotkeysUserCount.Remove(hotkey);
				lastUser = true;
				return;
			}
			HotkeysUserCount[hotkey] = --userCount;
			lastUser = false;
			return;
		}
		lastUser = true;
	}

	/// <summary>
	/// Registers both hotkeys, or neither: if the second one fails, the first is released again.
	/// </summary>
	public bool RegisterHotkeysPair(HotKey? hotkey1, HotKey? hotkey2) => RegisterHotkeysPair(hotkey1, hotkey2, out _);

	/// <inheritdoc cref="RegisterHotkeysPair(HotKey, HotKey)"/>
	/// <param name="failed">The hotkey that couldn't be registered (the settings show the error under its box), null on success</param>
	public bool RegisterHotkeysPair(HotKey? hotkey1, HotKey? hotkey2, out HotKey? failed)
	{
		failed = hotkey1;
		if(!RegisterHotkey(hotkey1))
			return false;
		failed = hotkey2;
		if(!RegisterHotkey(hotkey2))
		{
			//release only what this call took: the combination may be shared with another feature
			UnregisterHotkey(hotkey1);
			return false;
		}
		failed = null;
		return true;
	}

	public void UnregisterHotkeysPair(HotKey? hotkey1, HotKey? hotkey2)
	{
		UnregisterHotkey(hotkey1);
		UnregisterHotkey(hotkey2);
	}

	/// <summary>
	/// Registers the hotkeys that are set (nulls are skipped), or none: if one fails, the ones before it are released again.
	/// </summary>
	/// <param name="failed">The hotkey that couldn't be registered, null on success</param>
	public bool RegisterHotkeys(IReadOnlyList<HotKey?> hotkeys, out HotKey? failed)
	{
		var registered = new List<HotKey>();
		foreach(var hotkey in hotkeys)
		{
			if(hotkey == null)
				continue;
			if(!RegisterHotkey(hotkey))
			{
				//release only what this call took: a combination may be shared with another feature
				foreach(var taken in registered)
					UnregisterHotkey(taken);
				failed = hotkey;
				return false;
			}
			registered.Add(hotkey);
		}
		failed = null;
		return true;
	}

	public void UnregisterHotkeys(IReadOnlyList<HotKey?> hotkeys)
	{
		foreach(var hotkey in hotkeys)
			UnregisterHotkey(hotkey);
	}

	/// <summary>
	/// Registers the hotkey and counts this user. False, with nothing counted, if there's no manager yet or it refuses the hotkey
	/// (e.g. another app owns the combination in blocking mode). The managers accept a combination that is already registered,
	/// so several features can share one.
	/// </summary>
	public bool RegisterHotkey(HotKey? hotkey)
	{
		if(hotkeyManager == null || hotkey == null)
			return false;
		if(hotkey.Source == InputSource.Mouse && !IsUsableMouseHotkey(hotkey))
		{
			Logger.Warn($"Refused the mouse hotkey [{hotkey}]: an unknown button, or a click or the wheel without a modifier");
			return false;
		}
		var manager = ManagerFor(hotkey, create: true);
		if(manager == null)
			return false;
		try
		{
			manager.RegisterHotkey(hotkey);
		}
		catch(Exception e)
		{
			Logger.Warn($"Failed to register hotkey [{hotkey}]", e);
			ReleaseIdleMouseManager();
			return false;
		}
		IncrementHotkeyUserCounter(hotkey);
		return true;
	}

	/// <summary>A known button; clicks and the wheel only with a modifier (they are used all the time), side buttons also alone</summary>
	internal static bool IsUsableMouseHotkey(HotKey hotkey)
		=> hotkey.Button is { } button && MouseButtons.IsKnown(button)
			&& (hotkey.ModifierKeys != ModifierKeys.None || !MouseButtons.NeedsModifier(button));

	/// <param name="create">Starts the mouse manager for a mouse hotkey</param>
	/// <returns>The hotkey's manager; null if there's none (no keyboard manager yet, mouse hotkeys not available)</returns>
	private IHotkeyManager? ManagerFor(HotKey hotkey, bool create)
	{
		if(hotkey.Source != InputSource.Mouse)
			return hotkeyManager;
		if(mouseManager == null && create && createMouseManager != null)
		{
			try
			{
				mouseManager = createMouseManager();
				mouseManager.HotkeyPressed += OnHotkeyPressed;
			}
			catch(Exception e)
			{
				Logger.Error("Failed to start listening to the mouse", e);
				mouseManager = null;
			}
		}
		return mouseManager;
	}

	/// <summary>The mouse manager sees every mouse move: it's stopped once no mouse hotkey is left</summary>
	private void ReleaseIdleMouseManager()
	{
		if(mouseManager == null || mouseManager.RegisteredHotkeysCount > 0)
			return;
		mouseManager.HotkeyPressed -= OnHotkeyPressed;
		mouseManager.Dispose();
		mouseManager = null;
	}

	/// <summary>
	/// Releases one user of the hotkey; the last one unregisters it.
	/// </summary>
	public void UnregisterHotkey(HotKey? hotkey)
	{
		if(hotkeyManager == null || hotkey == null)
			return;
		DecrementHotkeyUserCounter(hotkey, out bool lastUser);
		if(!lastUser)
			return;
		var manager = ManagerFor(hotkey, create: false);
		if(manager == null)
			return;
		try
		{
			manager.UnregisterHotkey(hotkey);
		}
		catch(Exception e)
		{
			Logger.Warn($"Failed to unregister hotkey [{hotkey}]", e);
		}
		if(manager == mouseManager)
			ReleaseIdleMouseManager();
	}

	public bool RegisterOpenMixerHotkey(HotKey? hotkey)
	{
		if(!RegisterHotkey(hotkey))
			return false;
		openMixer = hotkey;
		return true;
	}

	public void UnregisterOpenMixerHotkey(HotKey? hotkey)
	{
		if(hotkey == null)
			return;
		if(hotkey.Equals(openMixer))
			openMixer = null;
		UnregisterHotkey(hotkey);
	}

	public ErrorMessageType HotkeyIsValid(HotKey? hotkey)
	{
		if(hotkey == null)
			return ErrorMessageType.OpenReg;
		if(hotkey.ModifierKeys == ModifierKeys.None && hotkey.Key == Key.F12)
			return ErrorMessageType.F12;
		if(SettingsProvider.HotkeyExists(hotkey))
			return ErrorMessageType.HotkeyExists;
		return ErrorMessageType.None;
	}

	public ErrorMessageType HotkeysAreValid(HotKey? up, HotKey? down)
	{
		if(up == null || down == null)
			return ErrorMessageType.VolumeReg;
		if((up.ModifierKeys == ModifierKeys.None && up.Key == Key.F12) ||
		   (down.ModifierKeys == ModifierKeys.None && down.Key == Key.F12))
			return ErrorMessageType.F12;
		if(up.Equals(down))
			return ErrorMessageType.Diff;
		if(SettingsProvider.HotkeysExist(up, down))
			return ErrorMessageType.HotkeyExists;
		return ErrorMessageType.None;
	}

	/// <summary>
	/// Checks a volume up/down pair hotkey by hotkey, so the settings can show an error under the box that has it.
	/// A missing hotkey is <see cref="ErrorMessageType.Missing"/>; two equal hotkeys are reported on the second one.
	/// </summary>
	public (ErrorMessageType up, ErrorMessageType down) CheckHotkeysPair(HotKey? up, HotKey? down)
	{
		ErrorMessageType Check(HotKey? hotkey) => hotkey == null ? ErrorMessageType.Missing : HotkeyIsValid(hotkey);

		var upError = Check(up);
		var downError = Check(down);
		if(downError == ErrorMessageType.None && down is not null && down.Equals(up))
			downError = ErrorMessageType.Diff;
		return (upError, downError);
	}

	/// <summary>
	/// Checks an app's or the foreground app's hotkeys box by box: each is optional, but at least one is set
	/// (<see cref="ErrorMessageType.Missing"/> under the first box when none is), and a hotkey set twice is <see cref="ErrorMessageType.Diff"/>
	/// on the later box. Each set hotkey is also checked with <see cref="HotkeyIsValid"/>.
	/// </summary>
	public (ErrorMessageType up, ErrorMessageType down, ErrorMessageType mute) CheckAppHotkeys(HotKey? up, HotKey? down, HotKey? mute)
	{
		if(up == null && down == null && mute == null)
			return (ErrorMessageType.Missing, ErrorMessageType.None, ErrorMessageType.None);
		ErrorMessageType Check(HotKey? hotkey, params HotKey?[] earlier)
		{
			if(hotkey == null)
				return ErrorMessageType.None;
			if(earlier.Any(other => hotkey.Equals(other)))
				return ErrorMessageType.Diff;
			return HotkeyIsValid(hotkey);
		}
		return (Check(up), Check(down, up), Check(mute, up, down));
	}

	public void SetVolumeStep(int newValue)
	{
		VolumeStep = newValue;
	}

	/// <summary>
	/// Sets the keyboard hotkey manager. On a later call the registered keyboard hotkeys move from the previous manager to the new one,
	/// and the previous manager is disposed. The user counts stay as they are. Mouse hotkeys stay with the mouse manager:
	/// the hotkey mode only decides how keys are taken.
	/// </summary>
	public void SetHotkeyManager(IHotkeyManager hm)
	{
		if(hotkeyManager == hm)
			return;
		var previous = hotkeyManager;
		hotkeyManager = hm;
		hotkeyManager.HotkeyPressed += OnHotkeyPressed;
		if(previous == null)
		{
			IsActive = true;
			Activated?.Invoke();
			return;
		}

		previous.HotkeyPressed -= OnHotkeyPressed;
		foreach(var hotkey in previous.GetRegisteredHotkeys() ?? Array.Empty<HotKey>())
		{
			try { previous.UnregisterHotkey(hotkey); }
			catch { }
			try { hm.RegisterHotkey(hotkey); }
			catch(Exception e)
			{
				//e.g. another app uses the combination when moving to the blocking manager
				Logger.Error($"Failed to move hotkey [{hotkey}] to the new hotkey manager", e);
			}
		}
		previous.Dispose();
	}

	/// <summary>
	/// Set while the user types a hotkey into a hotkey box, so the typed combination doesn't trigger a registered hotkey.
	/// The blocking manager doesn't need it (the box swallows WM_HOTKEY), the non-blocking one does.
	/// </summary>
	public bool IsEditingHotkey { get; set; }

	private readonly Func<HotKey, bool> isRegisteredElsewhere;

	public HotkeyService() : this(hotkey => HotkeyProbe.IsRegisteredElsewhere(Win32.Instance, hotkey), () => new MouseHotkeyManager()) { }

	/// <param name="isRegisteredElsewhere">Whether Windows refuses to register the combination because it's taken (tests pass a fake)</param>
	/// <param name="createMouseManager">Starts listening to the mouse for mouse hotkeys; null: mouse hotkeys are refused</param>
	internal HotkeyService(Func<HotKey, bool> isRegisteredElsewhere, Func<IHotkeyManager>? createMouseManager = null)
	{
		this.isRegisteredElsewhere = isRegisteredElsewhere;
		this.createMouseManager = createMouseManager;
	}

	/// <summary>
	/// Who holds a combination that Windows delivered as a hotkey instead of as keys (a hotkey box only sees its key released).
	/// </summary>
	public HotkeyOwner FindOwner(HotKey hotkey)
	{
		if(ManagerFor(hotkey, create: false)?.GetRegisteredHotkeys()?.Contains(hotkey) == true)
			return HotkeyOwner.Volumey;
		//only key combinations can be registered by other apps
		if(hotkey.Source == InputSource.Mouse)
			return HotkeyOwner.Nobody;
		return isRegisteredElsewhere(hotkey) ? HotkeyOwner.AnotherApp : HotkeyOwner.Nobody;
	}

	private void OnHotkeyPressed(HotKey hotkey)
	{
		if(IsEditingHotkey)
			return;
		if(openMixer != null && hotkey.Equals(openMixer))
			OpenHotkeyPressed?.Invoke();
		_hotkeyPressed?.Invoke(hotkey);
	}

	public void Dispose()
	{
		//called again when the container is disposed on exit
		hotkeyManager?.Dispose();
		hotkeyManager = null;
		mouseManager?.Dispose();
		mouseManager = null;
	}
}
