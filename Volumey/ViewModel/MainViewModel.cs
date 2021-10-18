using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Interop;
using log4net;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.DataProvider;
using Volumey.Helper;
using Volumey.Model;
using Volumey.ViewModel.Settings;

namespace Volumey.ViewModel
{
	public sealed class MainViewModel : INotifyPropertyChanged
	{
		public ICommand ClosingCommand { get; }
		public ICommand TrayMixerCommand { get; }
		public ICommand TraySettingsCommand { get; }
		public ICommand TrayExitCommand { get; }
		public ICommand TrayClickCommand { get; }
		public ICommand SoundControlPanelCommand { get; }
		public ICommand SoundSettingsCommand { get; }
		
		private bool windowIsVisible;
		public bool WindowIsVisible
		{
			get => windowIsVisible;
			set
			{
				windowIsVisible = value;
				OnPropertyChanged();
			}
		}

        public event Action<bool, bool> OpenCommandEvent;

		public MainViewModel()
		{
			this.WindowIsVisible = !Startup.StartMinimized;
			this.ClosingCommand = new ActionCommand(OnWindowClosing);
			this.TrayMixerCommand = new ActionCommand(() => OpenCommand(isSettingsPage: false, reverseDisplaying: false));
			this.TraySettingsCommand = new ActionCommand(() => OpenCommand(isSettingsPage: true, reverseDisplaying: false));
			this.TrayClickCommand = new ActionCommand(() => OpenCommand(isSettingsPage: false, reverseDisplaying: true));
			this.TrayExitCommand = new ActionCommand(OnExit);
			this.SoundControlPanelCommand = new ActionCommand(SystemSoundUtilities.StartSoundControlPanel);
			this.SoundSettingsCommand = new ActionCommand(SystemSoundUtilities.StartSoundSettings);
			App.Current.SessionEnding += (sender, args) => OnExit();

			HotkeysControl.OpenHotkeyPressed += OnOpenHotkeyPressed;
			ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;
			DeviceProvider.GetInstance().DeviceFormatChanged += OnDeviceFormatChanged;
		}

		private bool restartInvoked = false;

		private void OnDeviceFormatChanged(OutputDeviceModel sender)
		{
			//Prevent a situation when device format structure changes several times when its format is changed in system thus invoking several restarts
			if(restartInvoked)
				return;
			//After the device format is changed every stream of its sessions become invalid and returns 0x88890004/AUDCLNT_E_DEVICE_INVALIDATED when used
			//so you have to somehow update this data about sessions.
			//The only working way of handling this event I have found is restarting the app.
			//Releasing COM interfaces related to the device and retrieving them again to handle format change as recommended in documentation does nothing.
			//TODO find a better way to handle device format change
			restartInvoked = true;
			RestartApp();	
		}

		private void RestartApp()
		{
			var pathToExe = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
			if(this.WindowIsVisible)
				Process.Start(pathToExe, Startup.RestartArg);
			else
				Process.Start(pathToExe, $"{Startup.RestartArg} {Startup.MinimizedArg}");
			App.Current.Dispatcher.Invoke(OnExit);
		}
		
		private async void OnExit()
		{
			await SettingsProvider.SaveSettings().ConfigureAwait(true);
			LogManager.Shutdown();
			HotkeysControl.Dispose();
			App.Current.Shutdown();
		}

		/// <summary>
		/// Changes window visibility if requested and invokes event which is used by the view to open the requested page 
		/// </summary>
		/// <param name="reverseDisplaying">Flag to control whether to close the window if it's displayed or not</param>
		private void OpenCommand(bool isSettingsPage, bool reverseDisplaying = false)
		{
			if(reverseDisplaying)
			{
				if(!windowIsVisible)
				{
					//bring the window to the foreground and change page
					this.OpenCommandEvent?.Invoke(isSettingsPage, WindowIsVisible);
					this.WindowIsVisible = true;
				}
				else
					this.WindowIsVisible = false;
			}
			else
			{
				this.OpenCommandEvent?.Invoke(isSettingsPage, WindowIsVisible);
				if (!WindowIsVisible)
					WindowIsVisible = true;
			}
		}

		private void OnThreadFilterMessage(ref MSG msg, ref bool handled)
		{
			//"The operating system communicates with your application window by passing messages to it.
			// A message is simply a numeric code that designates a particular event."
			//When a user attempts to launch the second instance of the app
			//it sends a window message of type WM_SHOWME to all top-level windows in the system.
			//This event handler checks every window message for this type of message
			//to bring the window of the first instance to top or make it visible
			//if it was minimized to tray

			if(msg.message == Startup.WM_SHOWME)
				this.OpenCommand(false);
		}
		
		private void OnOpenHotkeyPressed()
		{
			this.OpenCommand(false);
		}
		
		private void OnWindowClosing(object e)
		{
            if (e is CancelEventArgs args)
            {
                args.Cancel = true;
                this.WindowIsVisible = false;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}