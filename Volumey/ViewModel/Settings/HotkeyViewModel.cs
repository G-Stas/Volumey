using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using log4net;

namespace Volumey.ViewModel.Settings;

/// <summary>
/// The plumbing shared by the hotkey settings pages: the hotkey service, the error message and the logger
/// </summary>
public abstract class HotkeyViewModel : INotifyPropertyChanged
{
	private string errorMessage;
	public string ErrorMessage
	{
		get => errorMessage;
		private set
		{
			errorMessage = value;
			OnPropertyChanged();
		}
	}

	private ErrorMessageType currentErrType = ErrorMessageType.None;
	protected ErrorMessageType CurrentErrorType => currentErrType;

	protected readonly IHotkeyService hotkeys;

	private ILog _logger;
	protected ILog Logger => _logger ??= LogManager.GetLogger(GetType());

	protected HotkeyViewModel(IHotkeyService hotkeys)
	{
		this.hotkeys = hotkeys;
		ErrorDictionary.LanguageChanged += () => SetErrorMessage(CurrentErrorType);
	}

	protected void SetErrorMessage(ErrorMessageType type)
	{
		currentErrType = type;
		ErrorMessage = type == ErrorMessageType.None ? string.Empty : ErrorDictionary.Instance[type];
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

	public event PropertyChangedEventHandler PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
