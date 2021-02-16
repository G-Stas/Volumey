using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Volumey.DataProvider;
using Volumey.Model;

namespace Volumey.ViewModel
{
    public class DeviceProviderViewModel : INotifyPropertyChanged
    {
        private OutputDeviceModel selectedDevice;
        public OutputDeviceModel SelectedDevice
        {
            get => selectedDevice;
            set
            {
                selectedDevice = value;
                OnPropertyChanged();
            }
        }
        
        private OutputDeviceModel defaultDevice;
        public OutputDeviceModel DefaultDevice
        {
            get => defaultDevice;
            set
            {
                defaultDevice = value;
                OnPropertyChanged();
            }
        }
        
        public IDeviceProvider DeviceProvider { get; }

        private Dispatcher dispatcher => App.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        
        public DeviceProviderViewModel()
        {
            this.DeviceProvider = DataProvider.DeviceProvider.GetInstance();
            this.DeviceProvider.DeviceDisabled += OnDeviceDisabled;
            this.DeviceProvider.DefaultDeviceChanged += OnDefaultDeviceChanged;
            
            if(!this.DeviceProvider.NoOutputDevices)
                this.SelectedDevice = this.DefaultDevice = this.DeviceProvider.DefaultDevice;

            if(App.Current != null)
                App.Current.Exit += (s, a) => this.DeviceProvider.Dispose();
        }

        private void OnDefaultDeviceChanged(OutputDeviceModel newDevice)
        {
            //update default device property and selected device property if default device was the selected device in app
            dispatcher.Invoke(() =>
            {
                if(this.SelectedDevice == this.defaultDevice)
                {
                    this.SelectedDevice = this.DefaultDevice = newDevice;
                    return;
                }
                this.DefaultDevice = newDevice;
            });
        }

        private void OnDeviceDisabled(OutputDeviceModel device)
        {
            dispatcher.Invoke(() =>
            {
                if(this.SelectedDevice == device)
                {
                    if(device != defaultDevice)
                        this.SelectedDevice = DefaultDevice;
                    
                    //set both properties to null
                    //if defaultDevice was the selected device in application
                    //and OnDeviceProviderPropertyChanged event handler will set reference of a new default device object to both properties
                    else
                        this.SelectedDevice = this.DefaultDevice = null;
                }
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}