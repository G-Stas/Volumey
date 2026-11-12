#nullable enable
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using log4net;

namespace Volumey.ViewModel.Settings;

/// <summary>
/// The plumbing shared by the hotkey settings pages: the hotkey service, the error message and the logger
/// </summary>
public abstract partial class HotkeyViewModel : ObservableObject
{
	[ObservableProperty]
	public partial string ErrorMessage { get; private set; }

	protected ErrorMessageType CurrentErrorType { get; private set; } = ErrorMessageType.None;

	protected readonly IHotkeyService hotkeys;

	private ILog? _logger;
	protected ILog Logger => _logger ??= LogManager.GetLogger(GetType());

	protected HotkeyViewModel(IHotkeyService hotkeys)
	{
		this.hotkeys = hotkeys;
		ErrorDictionary.LanguageChanged += () => ShowError(CurrentErrorType);
	}

	protected void SetErrorMessage(ErrorMessageType type)
	{
		errorIsRefusal = false;
		ShowError(type);
	}

	private void ShowError(ErrorMessageType type)
	{
		CurrentErrorType = type;
		ErrorMessage = type == ErrorMessageType.None ? string.Empty : ErrorDictionary.Instance[type];
	}

	private bool errorIsRefusal;

	/// <summary>
	/// Shows <paramref name="error"/> while the system refuses the hotkey (see <see cref="RefusalError"/>)
	/// </summary>
	protected void ShowRefusal(bool refused, ErrorMessageType error)
	{
		(var type, errorIsRefusal) = RefusalError.Next(CurrentErrorType, errorIsRefusal, refused, error);
		if(type != CurrentErrorType)
			ShowError(type);
	}

	/// <summary>
	/// Shows the error of a failed validation
	/// </summary>
	/// <returns>True if there's no error</returns>
	protected bool IsValid(ErrorMessageType validationResult)
	{
		if(validationResult == ErrorMessageType.None)
			return true;
		SetErrorMessage(validationResult);
		return false;
	}

	/// <summary>
	/// Registers the hotkeys loaded from the settings now, or once the hotkey manager is set (the main window sets it on startup)
	/// </summary>
	protected void RegisterWhenHotkeysActive(Action registerLoadedHotkeys)
	{
		if(hotkeys.IsActive)
			registerLoadedHotkeys();
		else
			hotkeys.Activated += registerLoadedHotkeys;
	}
}
