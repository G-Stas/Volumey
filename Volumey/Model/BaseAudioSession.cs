using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors.Core;
using Volumey.Controls;
using Volumey.Helper;
using Volumey.ViewModel.Settings;

namespace Volumey.Model
{
	public abstract class BaseAudioSession : IManagedAudioSession, INotifyPropertyChanged, IDisposable
	{
		protected AudioSessionStateNotificationMediator AudioSessionStateNotificationMediator { get; private set; }

		protected int _volume;

		public virtual int Volume
		{
			get => this._volume;
			set
			{
				this._volume = value;
				OnPropertyChanged();
			}
		}

		protected bool _isMuted;

		public virtual bool IsMuted
		{
			get => this._isMuted;
			set
			{
				this._isMuted = value;
				OnPropertyChanged();
			}
		}

		public abstract string Name { get; set; }

		private readonly string _id;
		public string Id => this._id;

		private ImageSource iconSource;
		public virtual ImageSource IconSource
		{
			get => iconSource;
			set
			{
				iconSource = value;
				OnPropertyChanged();
			}
		}

		private Icon _icon;
		public virtual Icon Icon
		{
			get => _icon;
			set
			{
				_icon = value;
				OnPropertyChanged();
			}
		}

		public virtual ICommand MuteCommand { get; set; }

		public BaseAudioSession(int volume, bool isMuted, string id, Icon icon, AudioSessionStateNotificationMediator audioSessionStateNotificationMediator = null)
		{
			this._volume = volume;
			this._isMuted = isMuted;
			this._id = id;
			this._icon = icon;
			this.iconSource = icon?.GetAsImageSource();
			this.AudioSessionStateNotificationMediator = audioSessionStateNotificationMediator;
			this.MuteCommand = new ActionCommand(() => this.IsMuted = !this.IsMuted);
		}

		internal void SetStateNotificationMediator(AudioSessionStateNotificationMediator mediator)
		{
			this.AudioSessionStateNotificationMediator = mediator;
		}

		internal void ResetStateNotificationMediator()
		{
			this.AudioSessionStateNotificationMediator = null;
		}

		public abstract bool SetVolumeHotkeys(HotKey volUp, HotKey volDown);
		public abstract void ResetVolumeHotkeys();
		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		public virtual void Dispose()
		{
			if(this._icon != null)
			{
				NativeMethods.DestroyIcon(this._icon.Handle);
				this._icon.Dispose();	
			}
			this.AudioSessionStateNotificationMediator?.NotifyOfDisposing(this);
		}
	}
}