using System.Runtime.InteropServices;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace Volumey.UITests.Infrastructure;

/// <summary>
/// A key combination as the tests type and press it. The tests use Ctrl+Alt+Shift with F13–F24 by default: no keyboard has those keys,
/// so nothing else on the machine reacts to them.
/// </summary>
internal sealed record Combo(VirtualKeyShort Key, bool Ctrl = true, bool Alt = true, bool Shift = true)
{
	public static Combo F(int n) => new((VirtualKeyShort)((int)VirtualKeyShort.F13 + n - 13));

	/// <summary>The key's name in settings.json (WPF's <c>Key</c> enum)</summary>
	public string JsonKey => Key switch
	{
		>= VirtualKeyShort.F1 and <= VirtualKeyShort.F24 => "F" + ((int)Key - (int)VirtualKeyShort.F1 + 1),
		_ => throw new NotSupportedException($"No settings name for {Key}")
	};

	/// <summary>The modifiers in settings.json (WPF's <c>ModifierKeys</c> flags: Alt, Control, Shift)</summary>
	public string JsonModifiers => string.Join(", ", new[] { Alt ? "Alt" : null, Ctrl ? "Control" : null, Shift ? "Shift" : null }.Where(m => m != null));

	private uint WinModifiers => (Alt ? 1u : 0) | (Ctrl ? 2u : 0) | (Shift ? 4u : 0);

	private VirtualKeyShort[] Modifiers => new[] { Ctrl ? VirtualKeyShort.CONTROL : 0, Alt ? VirtualKeyShort.ALT : 0, Shift ? VirtualKeyShort.SHIFT : 0 }
		.Where(k => k != 0).ToArray();

	/// <summary>
	/// Presses and releases the combination (SendInput): a hotkey box records it, a registered hotkey fires
	/// </summary>
	public void Press()
	{
		foreach(var m in Modifiers)
			Keyboard.Press(m);
		Keyboard.Press(Key);
		Wait.UntilInputIsProcessed();
		Keyboard.Release(Key);
		foreach(var m in Modifiers.Reverse())
			Keyboard.Release(m);
		Wait.UntilInputIsProcessed();
	}

	/// <summary>
	/// True while another app (Volumey) holds the combination with RegisterHotKey: then Windows refuses it to this process.
	/// Only meaningful while Volumey is in blocking mode; nothing is pressed.
	/// </summary>
	public bool IsHeldByAnotherApp()
	{
		const int id = 0xB00F;
		if(RegisterHotKey(IntPtr.Zero, id, WinModifiers | 0x4000 /*MOD_NOREPEAT*/, (uint)Key))
		{
			UnregisterHotKey(IntPtr.Zero, id);
			return false;
		}
		return Marshal.GetLastWin32Error() == 1409; //ERROR_HOTKEY_ALREADY_REGISTERED
	}

	/// <summary>
	/// Holds the combination in this process, so Volumey can't register it (blocking mode); dispose to release it.
	/// RegisterHotKey binds to the calling thread, so take and release it on the same thread.
	/// </summary>
	public IDisposable HoldInThisProcess()
	{
		const int id = 0xB010;
		if(!RegisterHotKey(IntPtr.Zero, id, WinModifiers | 0x4000, (uint)Key))
			throw new InvalidOperationException($"Couldn't hold {this}: error {Marshal.GetLastWin32Error()}");
		return new Release(() => UnregisterHotKey(IntPtr.Zero, id));
	}

	public override string ToString() => $"{(Ctrl ? "Ctrl+" : "")}{(Alt ? "Alt+" : "")}{(Shift ? "Shift+" : "")}{Key}";

	private sealed class Release(Action release) : IDisposable
	{
		public void Dispose() => release();
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
