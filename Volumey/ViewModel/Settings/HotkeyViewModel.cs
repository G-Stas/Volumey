using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Volumey.ViewModel.Settings
{
	public abstract class HotkeyViewModel : INotifyPropertyChanged
	{
		private string errorMessage;
		public string ErrorMessage
		{
			get => errorMessage;
			private set
			{
				this.errorMessage = value;
				OnPropertyChanged();
			}
		}
		
		private ErrorMessageType currentErrType = ErrorMessageType.None;
		protected ErrorMessageType CurrentErrorType => currentErrType;

		public HotkeyViewModel()
		{
			ErrorDictionary.LanguageChanged += () => this.SetErrorMessage(this.CurrentErrorType);
		}
		
		protected void SetErrorMessage(ErrorMessageType type)
		{
			this.currentErrType = type;
			this.ErrorMessage = type == ErrorMessageType.None ? string.Empty : ErrorDictionary.Instance[type];
		}
		
		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}