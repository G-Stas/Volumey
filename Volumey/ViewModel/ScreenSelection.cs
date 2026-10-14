#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using log4net;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.Model;

namespace Volumey.ViewModel;

/// <summary>
/// The screen something is shown on (the main window, the OSD): the connected screens, the screen remembered in the settings
/// and the fallback to the primary screen while the remembered one isn't connected
/// </summary>
internal sealed class ScreenSelection
{
	public ObservableCollection<ScreenInfo> All { get; } = new ObservableCollection<ScreenInfo>();

	public ScreenInfo Selected { get; private set; }

	private readonly IScreenInfoProvider provider;
	private readonly Func<string?> rememberedName;
	private readonly Action<string?> remember;
	private readonly Func<Dispatcher?> uiDispatcher;
	private readonly Action<ScreenInfo> shown;
	private readonly ILog logger;

	/// <summary>
	/// Raised on the UI thread before the list of screens is replaced
	/// </summary>
	public event Action? ScreensChanging;

	/// <param name="rememberedName">Reads the remembered screen's name from the settings</param>
	/// <param name="remember">Writes the name of a screen the user picked to the settings</param>
	/// <param name="uiDispatcher">The UI thread that owns the window which follows the selection</param>
	/// <param name="shown">Called on the UI thread with the screen that is used now</param>
	public ScreenSelection(IScreenInfoProvider provider, Func<string?> rememberedName, Action<string?> remember,
		Func<Dispatcher?> uiDispatcher, Action<ScreenInfo> shown, ILog logger)
	{
		this.provider = provider;
		this.rememberedName = rememberedName;
		this.remember = remember;
		this.uiDispatcher = uiDispatcher;
		this.shown = shown;
		this.logger = logger;
	}

	/// <summary>
	/// Reads the screens and uses the remembered one
	/// </summary>
	public void Load()
	{
		foreach(var screen in provider.GetAllScreensInfo())
			All.Add(screen);
		Show(ScreenInfo.Find(All, rememberedName()));
	}

	/// <summary>
	/// The user's choice: used and remembered
	/// </summary>
	/// <returns>False if the screen isn't listed (the combo box writes back an empty value while the list is replaced)</returns>
	public bool Select(ScreenInfo screen)
	{
		if(!All.Contains(screen))
			return false;
		Show(screen);
		remember(screen.Name);
		return true;
	}

	/// <summary>
	/// Uses the screen without remembering it, so a fallback screen doesn't replace the user's choice
	/// </summary>
	private void Show(ScreenInfo screen)
	{
		Selected = screen;
		shown(screen);
	}

	/// <summary>
	/// After the window was displayed and when the screens change: reads the screens on the thread pool, then updates the list
	/// and picks the remembered screen again (or the primary one while it's not connected) on the UI thread
	/// </summary>
	public void Refresh()
	{
		Task.Run(() =>
		{
			var screens = provider.GetAllScreensInfo().ToList();

			//on the UI thread: it owns the window that follows the selection (the OSD window throws if its area is set from another thread)
			void Apply()
			{
				if(screens.Count != All.Count || screens.Except(All).Any())
				{
					ScreensChanging?.Invoke();
					All.Clear();
					foreach(var screen in screens)
						All.Add(screen);
				}
				//also updates the selection in the UI, which is lost when the list is replaced
				Show(ScreenInfo.Find(screens, rememberedName()));
			}

			var dispatcher = uiDispatcher();
			if(dispatcher == null || dispatcher.CheckAccess())
				Apply();
			else
				//without waiting: the pool thread isn't held while the UI thread is busy (e.g. laying out the mixer that just opened)
				dispatcher.InvokeAsync(Apply).Task.Forget(logger, "Failed to check the screen for availability");
		}).ContinueWith(task => logger.Error("Failed to check the screen for availability", task.Exception?.Flatten()),
			TaskContinuationOptions.OnlyOnFaulted);
	}
}
