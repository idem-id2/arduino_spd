using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using HexEditor.Constants;
using HexEditor.Arduino.Services;
using HexEditor.ViewModels;

namespace HexEditor.Arduino.ViewModels
{
    /// <summary>
    /// ViewModel для управления подключением к Arduino
    /// </summary>
    internal class ArduinoConnectionViewModel : ViewModelBase
    {
        private readonly ArduinoService _arduinoService;

        private ArduinoDeviceInfo? _selectedDevice;
        private bool _isScanning;
        private bool _isConnecting;
        private bool _isConnected;
        private bool _isSpdReady;
        private SpdMemoryType _memoryType;

        // Device details
        private string _detailPort = UiConstants.PLACEHOLDER_VALUE;
        private string _detailFirmware = UiConstants.PLACEHOLDER_VALUE;
        private string _detailName = UiConstants.PLACEHOLDER_VALUE;
        private string _detailClock = UiConstants.PLACEHOLDER_VALUE;
        private string _detailRswp = UiConstants.PLACEHOLDER_VALUE;

        // Status badge
        private string _connectionStatusText = "Автономный";
        private Brush _connectionStatusBackground = Brushes.Transparent;
        private Brush _connectionStatusForeground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));

        public ArduinoConnectionViewModel(ArduinoService arduinoService)
        {
            _arduinoService = arduinoService ?? throw new ArgumentNullException(nameof(arduinoService));

            // Подписка на события сервиса
            _arduinoService.ConnectionStateChanged += OnConnectionStateChanged;
            _arduinoService.ConnectionInfoChanged += OnConnectionInfoChanged;
            _arduinoService.SpdStateChanged += OnSpdStateChanged;
            _arduinoService.MemoryTypeChanged += OnMemoryTypeChanged;
            _arduinoService.StateChanged += OnServiceStateChanged;

            // Команды
            ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning && !IsConnecting);
            ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => CanConnect);
            DisconnectCommand = new RelayCommand(Disconnect, () => IsConnected);

            // Начальное состояние
            UpdateConnectionStatus();
        }

        #region Properties

        public ObservableCollection<ArduinoDeviceInfo> Devices => _arduinoService.Devices;

        public ArduinoDeviceInfo? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetProperty(ref _selectedDevice, value))
                {
                    _arduinoService.SetSelectedDevice(value);
                    OnPropertyChanged(nameof(CanConnect));
                    OnPropertyChanged(nameof(ToggleButtonContent));
                }
            }
        }

        public bool IsScanning
        {
            get => _isScanning;
            private set
            {
                if (SetProperty(ref _isScanning, value))
                {
                    OnPropertyChanged(nameof(CanConnect));
                }
            }
        }

        public bool IsConnecting
        {
            get => _isConnecting;
            private set
            {
                if (SetProperty(ref _isConnecting, value))
                {
                    OnPropertyChanged(nameof(CanConnect));
                }
            }
        }

        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    OnPropertyChanged(nameof(CanConnect));
                    OnPropertyChanged(nameof(ToggleButtonContent));
                    UpdateConnectionStatus();
                }
            }
        }

        public bool IsSpdReady
        {
            get => _isSpdReady;
            private set => SetProperty(ref _isSpdReady, value);
        }

        public SpdMemoryType MemoryType
        {
            get => _memoryType;
            private set => SetProperty(ref _memoryType, value);
        }

        public bool CanConnect => SelectedDevice != null && !IsScanning && !IsConnecting;

        public string ToggleButtonContent => IsConnected ? "Отключить" : "Подключить";

        // Device Details
        public string DetailPort
        {
            get => _detailPort;
            private set => SetProperty(ref _detailPort, value);
        }

        public string DetailFirmware
        {
            get => _detailFirmware;
            private set => SetProperty(ref _detailFirmware, value);
        }

        public string DetailName
        {
            get => _detailName;
            private set => SetProperty(ref _detailName, value);
        }

        public string DetailClock
        {
            get => _detailClock;
            private set => SetProperty(ref _detailClock, value);
        }

        public string DetailRswp
        {
            get => _detailRswp;
            private set => SetProperty(ref _detailRswp, value);
        }

        // Connection Status Badge
        public string ConnectionStatusText
        {
            get => _connectionStatusText;
            private set => SetProperty(ref _connectionStatusText, value);
        }

        public Brush ConnectionStatusBackground
        {
            get => _connectionStatusBackground;
            private set => SetProperty(ref _connectionStatusBackground, value);
        }

        public Brush ConnectionStatusForeground
        {
            get => _connectionStatusForeground;
            private set => SetProperty(ref _connectionStatusForeground, value);
        }

        #endregion

        #region Commands

        public ICommand ScanCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }

        private async Task ScanAsync()
        {
            IsScanning = true;
            try
            {
                await _arduinoService.ScanAsync();
                if (Devices.Count > 0)
                {
                    SelectedDevice = Devices[0];
                }
            }
            finally
            {
                IsScanning = false;
            }
        }

        private async Task ConnectAsync()
        {
            if (IsConnected)
            {
                Disconnect();
            }
            else
            {
                IsConnecting = true;
                try
                {
                    await _arduinoService.ConnectAsync();
                }
                finally
                {
                    IsConnecting = false;
                }
            }
        }

        private void Disconnect()
        {
            _arduinoService.Disconnect();
        }

        #endregion

        #region Event Handlers

        private void OnConnectionStateChanged(object? sender, bool isConnected)
        {
            IsConnected = isConnected;
            if (!isConnected)
            {
                ResetDeviceDetails();
            }
        }

        private void OnConnectionInfoChanged(object? sender, ArduinoConnectionInfo info)
        {
            if (ReferenceEquals(info, ArduinoConnectionInfo.Empty))
            {
                ResetDeviceDetails();
            }
            else
            {
                SetDeviceDetails(info.Port, info.FirmwareVersion, info.Name, info.I2CClock, info.Ddr4Rswp);
            }
        }

        private void OnSpdStateChanged(object? sender, bool isSpdReady)
        {
            IsSpdReady = isSpdReady;
        }

        private void OnMemoryTypeChanged(object? sender, SpdMemoryType memoryType)
        {
            MemoryType = memoryType;
        }

        private void OnServiceStateChanged(object? sender, EventArgs e)
        {
            // Обновляем состояние команд
            ((RelayCommand)DisconnectCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)ConnectCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)ScanCommand).RaiseCanExecuteChanged();
        }

        #endregion

        #region Private Methods

        private void ResetDeviceDetails()
        {
            DetailPort = UiConstants.PLACEHOLDER_VALUE;
            DetailFirmware = UiConstants.PLACEHOLDER_VALUE;
            DetailName = UiConstants.PLACEHOLDER_VALUE;
            DetailClock = UiConstants.PLACEHOLDER_VALUE;
            DetailRswp = UiConstants.PLACEHOLDER_VALUE;
            UpdateConnectionStatus();
        }

        private void SetDeviceDetails(string port, string firmware, string name, string clock, string ddr4Rswp)
        {
            DetailPort = string.IsNullOrWhiteSpace(port) ? UiConstants.PLACEHOLDER_VALUE : port;
            DetailFirmware = string.IsNullOrWhiteSpace(firmware) ? UiConstants.PLACEHOLDER_VALUE : firmware;
            DetailName = string.IsNullOrWhiteSpace(name) ? UiConstants.PLACEHOLDER_VALUE : name;
            DetailClock = string.IsNullOrWhiteSpace(clock) ? UiConstants.PLACEHOLDER_VALUE : clock;
            DetailRswp = string.IsNullOrWhiteSpace(ddr4Rswp) ? UiConstants.PLACEHOLDER_VALUE : ddr4Rswp;
            UpdateConnectionStatus();
        }

        private void UpdateConnectionStatus()
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(DetailPort) || DetailPort == UiConstants.PLACEHOLDER_VALUE)
            {
                ConnectionStatusBackground = Brushes.Transparent;
                ConnectionStatusForeground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));
                ConnectionStatusText = "Автономный";
            }
            else
            {
                ConnectionStatusBackground = Brushes.White;
                ConnectionStatusForeground = Brushes.Black;
                string formattedPort = FormatConnectionPort(DetailPort);
                ConnectionStatusText = $"Arduino {formattedPort}";
            }
        }

        private static string FormatConnectionPort(string port)
        {
            if (string.IsNullOrWhiteSpace(port))
                return UiConstants.PLACEHOLDER_VALUE;

            if (port.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            {
                var suffix = port.Substring(3).TrimStart(':').Trim();
                return string.IsNullOrEmpty(suffix) ? port : suffix;
            }

            return port;
        }

        #endregion

        public void Dispose()
        {
            _arduinoService.ConnectionStateChanged -= OnConnectionStateChanged;
            _arduinoService.ConnectionInfoChanged -= OnConnectionInfoChanged;
            _arduinoService.SpdStateChanged -= OnSpdStateChanged;
            _arduinoService.MemoryTypeChanged -= OnMemoryTypeChanged;
            _arduinoService.StateChanged -= OnServiceStateChanged;
        }
    }
}

