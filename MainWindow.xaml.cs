namespace iHaveControl
{
    using NAudio.CoreAudioApi;
    using NAudio.CoreAudioApi.Interfaces;


    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Documents;
    using System.Windows.Forms;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Navigation;
    using System.Windows.Shapes;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IMMNotificationClient
    {
        private HookProc _hookProc;
        private IntPtr _hHook;
        private MMDeviceEnumerator _enumerator = new();
        private MMDevice? _device;
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            _hookProc = new HookProc(MyCallbackFunction);
            _enumerator.RegisterEndpointNotificationCallback(this);
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) { }

        public void OnDeviceAdded(string pwstrDeviceId)
            => UpdateDeviceList();

        public void OnDeviceRemoved(string deviceId)
            => UpdateDeviceList();

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
            => UpdateDeviceList();

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            using Process process = Process.GetCurrentProcess();
            using ProcessModule module = process.MainModule!;

            _hHook = Win32.SetWindowsHookEx(HookType.WH_KEYBOARD_LL, _hookProc,
                Win32.GetModuleHandle(null), 0);

            UpdateDeviceList();
        }

        private void UpdateDeviceList()
        {
            var deviceDetailList = _enumerator
                .EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active)
                .Select(x => new DeviceDetail(x.ID, $"[{x.DataFlow}] {x.FriendlyName}"))
                .ToList();

            Dispatcher.Invoke(() =>
            {
                cbDevice.ItemsSource = deviceDetailList;
                cbDevice.DisplayMemberPath = "DisplayText";
            });
        }

        int MyCallbackFunction(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == 0 /* HC_ACTION */)
            {
                var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                var keyPressed = KeyInterop.KeyFromVirtualKey((int)kbd.vkCode);

                switch (keyPressed)
                {
                    case Key.VolumeUp:
                        if (_device != null
                            && wParam == (IntPtr)0x0100 /* WM_KEYDOWN */)
                            _device.AudioEndpointVolume.VolumeStepUp();
                        return 1;
                    case Key.VolumeDown:
                        if (_device != null
                            && wParam == (IntPtr)0x0100 /* WM_KEYDOWN */)
                            _device.AudioEndpointVolume.VolumeStepDown();
                        return 1;
                }
            }
            return Win32.CallNextHookEx(_hHook, nCode, wParam, lParam);
        }

        private void cbDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDevice.SelectedItem is not DeviceDetail dd)
                return;

            _device?.Dispose();
            _device = _enumerator.GetDevice(dd.Id);
        }

        public record DeviceDetail(string Id, string DisplayText);
    }
}
