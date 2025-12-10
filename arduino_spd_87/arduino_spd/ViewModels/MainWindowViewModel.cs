using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using HexEditor.Constants;
using HexEditor.Arduino.Services;
using HexEditor.Arduino.ViewModels;

namespace HexEditor.ViewModels
{
    /// <summary>
    /// Главная ViewModel для MainWindow
    /// Координирует работу всех дочерних ViewModels
    /// </summary>
    internal class MainWindowViewModel : ViewModelBase
    {
        private readonly ArduinoService _arduinoService;

        // Status Badge properties
        private string _memoryTypeText = "DDR —";
        private Brush _memoryTypeBadgeBackground = Brushes.Transparent;
        private Brush _memoryTypeForeground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));

        private string _crcStatusText = "CRC —";
        private Brush _crcStatusBadgeBackground = Brushes.Transparent;
        private Brush _crcStatusForeground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));

        private bool _lastSpdIsDdr4;
        private bool _lastCrcValid = true;
        private bool _lastSpdHasData;
        private byte _lastMemoryTypeCode;

        public MainWindowViewModel(
            ArduinoService arduinoService,
            ArduinoConnectionViewModel arduinoViewModel,
            RswpViewModel rswpViewModel,
            LogViewModel logViewModel)
        {
            _arduinoService = arduinoService ?? throw new ArgumentNullException(nameof(arduinoService));
            ArduinoViewModel = arduinoViewModel ?? throw new ArgumentNullException(nameof(arduinoViewModel));
            RswpViewModel = rswpViewModel ?? throw new ArgumentNullException(nameof(rswpViewModel));
            LogViewModel = logViewModel ?? throw new ArgumentNullException(nameof(logViewModel));

            // Подписка на события
            _arduinoService.MemoryTypeChanged += OnMemoryTypeChanged;
            _arduinoService.SpdStateChanged += OnSpdStateChanged;

            // Команды
            FixCrcCommand = new RelayCommand(FixCrc, () => CanFixCrc);

            // Начальное состояние
            UpdateMemoryTypeBadge();
            UpdateCrcStatusBadge();
        }

        #region Properties

        public ArduinoConnectionViewModel ArduinoViewModel { get; }
        public RswpViewModel RswpViewModel { get; }
        public LogViewModel LogViewModel { get; }

        // Memory Type Badge
        public string MemoryTypeText
        {
            get => _memoryTypeText;
            private set => SetProperty(ref _memoryTypeText, value);
        }

        public Brush MemoryTypeBadgeBackground
        {
            get => _memoryTypeBadgeBackground;
            private set => SetProperty(ref _memoryTypeBadgeBackground, value);
        }

        public Brush MemoryTypeForeground
        {
            get => _memoryTypeForeground;
            private set => SetProperty(ref _memoryTypeForeground, value);
        }

        // CRC Status Badge
        public string CrcStatusText
        {
            get => _crcStatusText;
            private set => SetProperty(ref _crcStatusText, value);
        }

        public Brush CrcStatusBadgeBackground
        {
            get => _crcStatusBadgeBackground;
            private set => SetProperty(ref _crcStatusBadgeBackground, value);
        }

        public Brush CrcStatusForeground
        {
            get => _crcStatusForeground;
            private set => SetProperty(ref _crcStatusForeground, value);
        }

        public bool CanFixCrc => _lastSpdHasData && _lastSpdIsDdr4 && !_lastCrcValid;

        #endregion

        #region Commands

        public ICommand FixCrcCommand { get; }

        private void FixCrc()
        {
            // Эта логика останется в MainWindow, так как требует доступ к HexEditor
            // ViewModel только управляет состоянием кнопки
        }

        #endregion

        #region Public Methods

        public void UpdateCrcState(byte[] data)
        {
            _lastMemoryTypeCode = data.Length > 2 ? data[2] : (byte)0;
            _lastSpdIsDdr4 = _lastMemoryTypeCode == SpdConstants.MEMORY_TYPE_DDR4;
            bool isDdr5 = _lastMemoryTypeCode == SpdConstants.MEMORY_TYPE_DDR5;

            if (_lastSpdIsDdr4)
            {
                try
                {
                    var decoder = new SpdDecoder.Ddr4SpdDecoder(data);
                    var crcInfo = decoder.GetDdr4CrcInfo();
                    _lastCrcValid = crcInfo.IsValid;
                }
                catch
                {
                    _lastCrcValid = true;
                }
            }
            else
            {
                _lastCrcValid = true;
            }

            UpdateFixCrcButtonState();
            UpdateCrcStatusBadge();
            UpdateMemoryTypeBadge();
        }

        public void UpdateSpdHasData(bool hasData)
        {
            _lastSpdHasData = hasData;
            UpdateFixCrcButtonState();
            UpdateCrcStatusBadge();
            UpdateMemoryTypeBadge();
        }

        #endregion

        #region Event Handlers

        private void OnMemoryTypeChanged(object? sender, SpdMemoryType memoryType)
        {
            UpdateMemoryTypeBadge();
        }

        private void OnSpdStateChanged(object? sender, bool isSpdReady)
        {
            UpdateMemoryTypeBadge();
            UpdateCrcStatusBadge();
        }

        #endregion

        #region Private Methods

        private void UpdateFixCrcButtonState()
        {
            OnPropertyChanged(nameof(CanFixCrc));
            ((RelayCommand)FixCrcCommand).RaiseCanExecuteChanged();
        }

        private void UpdateCrcStatusBadge()
        {
            if (!_lastSpdHasData || !_lastSpdIsDdr4)
            {
                CrcStatusBadgeBackground = Brushes.Transparent;
                CrcStatusForeground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
                CrcStatusText = "CRC —";
                return;
            }

            if (_lastCrcValid)
            {
                CrcStatusBadgeBackground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(UiConstants.StatusColors.CRC_OK_COLOR)!);
                CrcStatusText = "CRC-OK";
                CrcStatusForeground = Brushes.White;
            }
            else
            {
                CrcStatusBadgeBackground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(UiConstants.StatusColors.CRC_BAD_COLOR)!);
                CrcStatusText = "! CRC-BAD";
                CrcStatusForeground = Brushes.White;
            }
        }

        private void UpdateMemoryTypeBadge()
        {
            if (!_lastSpdHasData)
            {
                MemoryTypeBadgeBackground = Brushes.Transparent;
                MemoryTypeForeground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));
                MemoryTypeText = "DDR —";
                return;
            }

            if (_lastMemoryTypeCode == SpdConstants.MEMORY_TYPE_DDR4)
            {
                MemoryTypeBadgeBackground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(UiConstants.StatusColors.DDR4_COLOR)!);
                MemoryTypeText = "DDR4";
                MemoryTypeForeground = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11));
            }
            else if (_lastMemoryTypeCode == SpdConstants.MEMORY_TYPE_DDR5)
            {
                MemoryTypeBadgeBackground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(UiConstants.StatusColors.DDR5_COLOR)!);
                MemoryTypeText = "DDR5";
                MemoryTypeForeground = Brushes.White;
            }
            else
            {
                MemoryTypeBadgeBackground = Brushes.Transparent;
                MemoryTypeText = "DDR ?";
                MemoryTypeForeground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));
            }
        }

        #endregion

        public void Dispose()
        {
            _arduinoService.MemoryTypeChanged -= OnMemoryTypeChanged;
            _arduinoService.SpdStateChanged -= OnSpdStateChanged;

            ArduinoViewModel.Dispose();
            RswpViewModel.Dispose();
            LogViewModel.Dispose();
        }
    }
}

