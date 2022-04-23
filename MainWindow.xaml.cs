namespace iHaveControl
{
    using NAudio.CoreAudioApi;
    using NAudio.CoreAudioApi.Interfaces;


    using System;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text.Json;
    using System.Windows;
    using System.Windows.Forms;
    using System.Windows.Input;

    using MessageBox = System.Windows.MessageBox;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IMMNotificationClient
    {
        private readonly string _configPath;
        private HookProc _hookProc;
        private IntPtr _hHook;
        private MMDeviceEnumerator _enumerator = new();
        private MMDevice? _device;
        private NotifyIcon _trayIcon;
        private ToolStripMenuItem _inputDeviceMenu = new("Input Device");
        private ToolStripMenuItem _outputDeviceMenu = new("Output Device");
        private Config _config = new();

        private bool IsDeviceAvailable
            => _device is not null && _device.State == DeviceState.Active;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            _hookProc = new HookProc(LowLevelKeyboardHookProc);
            _enumerator.RegisterEndpointNotificationCallback(this);

            _trayIcon = new NotifyIcon
            {
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add(_inputDeviceMenu);
            contextMenu.Items.Add(_outputDeviceMenu);
            contextMenu.Items.Add(new ToolStripMenuItem("Exit", null, Exit));
            _trayIcon.ContextMenuStrip = contextMenu;
            _configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "iHaveControl",
                "config.json");
        }

        private void LoadConfig()
        {
            if (File.Exists(_configPath))
            {
                _config = JsonSerializer.Deserialize<Config>(File.ReadAllText(_configPath))!;
                try { _device = _enumerator.GetDevice(_config.DeviceId); } catch { /* Just shutup */ }
            }
        }

        private void SaveConfig()
        {
            if (!Directory.Exists(Path.GetDirectoryName(_configPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);

            File.WriteAllText(_configPath, JsonSerializer.Serialize(_config, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));
        }

        private void Exit(object? sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) { }

        public void OnDeviceAdded(string pwstrDeviceId)
            => SomethingChanged();

        public void OnDeviceRemoved(string deviceId)
            => SomethingChanged();

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
            => SomethingChanged();

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
            => SomethingChanged();

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            using Process process = Process.GetCurrentProcess();
            using ProcessModule module = process.MainModule!;

            _hHook = Win32.SetWindowsHookEx(HookType.WH_KEYBOARD_LL, _hookProc,
                Win32.GetModuleHandle(null), 0);

            Visibility = Visibility.Hidden;
            LoadConfig();
            UpdateDeviceList();
            UpdateSelectItem();
            UpdateVolume();
        }

        private void SomethingChanged()
        {
            Dispatcher.Invoke(() =>
            {
                UpdateDeviceList();
                UpdateSelectItem();
                UpdateVolume();
            });
        }
        private void UpdateDeviceList()
        {
            var deviceList = _enumerator
                .EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active)
                .ToList();

            _inputDeviceMenu.DropDownItems.Clear();
            _inputDeviceMenu.DropDownItems.AddRange(deviceList
                .Where(x => x.DataFlow == DataFlow.Capture)
                .Select(x => new ToolStripMenuItem(x.FriendlyName, null, ContextMenuSelectDevice)
                {
                    Tag = x.ID
                })
                .ToArray());

            _outputDeviceMenu.DropDownItems.Clear();
            _outputDeviceMenu.DropDownItems.AddRange(deviceList
                .Where(x => x.DataFlow == DataFlow.Render)
                .Select(x => new ToolStripMenuItem(x.FriendlyName, null, ContextMenuSelectDevice)
                {
                    Tag = x.ID
                })
                .ToArray());
        }

        private void UpdateVolume()
        {
            var vol = _device is null
                ? "0"
                : Math.Clamp(Math.Round(_device!.AudioEndpointVolume.MasterVolumeLevelScalar * 100), 0, 100)
                    .ToString();

            var ico = MakeIcon(vol, IsDeviceAvailable);
            _trayIcon.Icon?.Dispose();
            _trayIcon.Icon = ico;

            _trayIcon.Text = _device is null
                ? "Nothing"
                : $"[{(_device!.DataFlow == DataFlow.Capture ? "Input" : "Output")}] {_device.FriendlyName}";
        }

        private void UpdateSelectItem()
        {
            if (_config.DeviceId != null)
            {
                for (var i = 0; i < _outputDeviceMenu.DropDownItems.Count; i++)
                {
                    var item = (ToolStripMenuItem)_outputDeviceMenu.DropDownItems[i];
                    item.Checked = (string)item.Tag == _config.DeviceId;
                }

                for (var i = 0; i < _inputDeviceMenu.DropDownItems.Count; i++)
                {
                    var item = (ToolStripMenuItem)_inputDeviceMenu.DropDownItems[i];
                    item.Checked = (string)item.Tag == _config.DeviceId;
                }
            }
        }

        private int LowLevelKeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == 0
                && _device != null
                && IsDeviceAvailable
                && wParam == (IntPtr)0x0100 /* WM_KEYDOWN */)
            {
                var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                var keyPressed = KeyInterop.KeyFromVirtualKey((int)kbd.vkCode);

                switch (keyPressed)
                {
                    case Key.VolumeUp:
                        _device.AudioEndpointVolume.VolumeStepUp();
                        UpdateVolume();
                        return 1;
                    case Key.VolumeDown:
                        _device.AudioEndpointVolume.VolumeStepDown();
                        UpdateVolume();
                        return 1;
                }
            }
            return Win32.CallNextHookEx(_hHook, nCode, wParam, lParam);
        }


        private void ContextMenuSelectDevice(object? sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem item)
                return;

            try
            {
                _device = _enumerator.GetDevice((string)item.Tag);
                _config.DeviceId = _device.ID;
                UpdateSelectItem();
                SaveConfig();
            }
            catch (Exception)
            {
                MessageBox.Show($"Cannot get device: {item.Text}\nID: {item.Tag}",
                    Title, MessageBoxButton.OK, MessageBoxImage.Warning);

                _device = null;
            }

            UpdateVolume();
        }

        private Icon MakeIcon(string text, bool deviceExists = false)
        {
            var size = 16;
            using var square = new Bitmap(size, size);
            using var g = Graphics.FromImage(square);
            using var f = new Font("Arial", size / 2, System.Drawing.FontStyle.Bold);
            var textSize = g.MeasureString(text, f);

            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.DrawString(text, f,
                new SolidBrush(deviceExists ? Color.White : Color.Red),
                new PointF((size / 2) - (textSize.Width / 2), 0));
            g.Flush();

            return System.Drawing.Icon.FromHandle(square.GetHicon());
        }
    }
}
