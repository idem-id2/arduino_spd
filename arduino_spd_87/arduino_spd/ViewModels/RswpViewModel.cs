using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using HexEditor.Constants;
using HexEditor.Arduino.Services;

namespace HexEditor.ViewModels
{
    /// <summary>
    /// ViewModel для блока RSWP
    /// </summary>
    internal class RswpBlockViewModel : ViewModelBase
    {
        private int _blockNumber;
        private bool _isProtected;
        private bool _isVisible;
        private bool _isEnabled;

        public int BlockNumber
        {
            get => _blockNumber;
            set => SetProperty(ref _blockNumber, value);
        }

        public bool IsProtected
        {
            get => _isProtected;
            set
            {
                if (SetProperty(ref _isProtected, value))
                {
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public string StatusText => IsProtected ? "Защищен" : "Не защищен";

        public Brush StatusColor => IsProtected
            ? new SolidColorBrush(Colors.Red)
            : new SolidColorBrush(Colors.Green);
    }

    /// <summary>
    /// ViewModel для управления RSWP (Reversible Software Write Protection)
    /// </summary>
    internal class RswpViewModel : ViewModelBase
    {
        private readonly ArduinoService _arduinoService;

        private string _memoryTypeText = $"Тип памяти: {UiConstants.PLACEHOLDER_VALUE}";
        private bool _canOperateRswp;

        public RswpViewModel(ArduinoService arduinoService)
        {
            _arduinoService = arduinoService ?? throw new ArgumentNullException(nameof(arduinoService));

            // Инициализация блоков
            Blocks = new ObservableCollection<RswpBlockViewModel>();
            for (int i = 0; i < 16; i++)
            {
                Blocks.Add(new RswpBlockViewModel
                {
                    BlockNumber = i,
                    IsVisible = false,
                    IsEnabled = false,
                    IsProtected = false
                });
            }

            // Подписка на события
            _arduinoService.MemoryTypeChanged += OnMemoryTypeChanged;
            _arduinoService.RswpStateChanged += OnRswpStateChanged;
            _arduinoService.SpdStateChanged += OnSpdStateChanged;
            _arduinoService.ConnectionStateChanged += OnConnectionStateChanged;

            // Команды
            CheckRswpCommand = new AsyncRelayCommand(CheckRswpAsync, () => CanOperateRswp);
            ClearRswpCommand = new AsyncRelayCommand(ClearRswpAsync, () => CanOperateRswp);
            SetRswpCommand = new AsyncRelayCommand(SetRswpAsync, () => CanOperateRswp);

            UpdateMemoryTypeLabel();
            UpdateRswpButtonsState();
        }

        #region Properties

        public ObservableCollection<RswpBlockViewModel> Blocks { get; }

        public string MemoryTypeText
        {
            get => _memoryTypeText;
            private set => SetProperty(ref _memoryTypeText, value);
        }

        public bool CanOperateRswp
        {
            get => _canOperateRswp;
            private set
            {
                if (SetProperty(ref _canOperateRswp, value))
                {
                    ((AsyncRelayCommand)CheckRswpCommand).RaiseCanExecuteChanged();
                    ((AsyncRelayCommand)ClearRswpCommand).RaiseCanExecuteChanged();
                    ((AsyncRelayCommand)SetRswpCommand).RaiseCanExecuteChanged();
                }
            }
        }

        #endregion

        #region Commands

        public ICommand CheckRswpCommand { get; }
        public ICommand ClearRswpCommand { get; }
        public ICommand SetRswpCommand { get; }

        private async Task CheckRswpAsync()
        {
            await _arduinoService.CheckRswpAsync();
        }

        private async Task ClearRswpAsync()
        {
            await _arduinoService.ClearRswpAsync();
        }

        private async Task SetRswpAsync()
        {
            var blocksToSet = Blocks
                .Where(b => b.IsVisible && b.IsProtected)
                .Select(b => (byte)b.BlockNumber)
                .ToArray();

            if (blocksToSet.Length == 0)
                return;

            // Устанавливаем блоки через специальный метод с логированием
            await _arduinoService.SetMultipleRswpAsync(blocksToSet);
        }

        #endregion

        #region Event Handlers

        private void OnMemoryTypeChanged(object? sender, SpdMemoryType memoryType)
        {
            UpdateMemoryTypeLabel();
            UpdateRswpDisplay(Array.Empty<bool>());
            UpdateRswpButtonsState();
        }

        private void OnRswpStateChanged(object? sender, bool[] states)
        {
            UpdateRswpDisplay(states);
        }

        private void OnSpdStateChanged(object? sender, bool isSpdReady)
        {
            UpdateRswpButtonsState();
        }

        private void OnConnectionStateChanged(object? sender, bool isConnected)
        {
            UpdateRswpButtonsState();
        }

        #endregion

        #region Private Methods

        private void UpdateMemoryTypeLabel()
        {
            string typeText = _arduinoService.ActiveMemoryType switch
            {
                SpdMemoryType.Ddr4 => "DDR4",
                SpdMemoryType.Ddr5 => "DDR5",
                _ => UiConstants.PLACEHOLDER_VALUE
            };

            MemoryTypeText = $"Тип памяти: {typeText}";
        }

        private void UpdateRswpDisplay(bool[] states)
        {
            int blockCount = _arduinoService.ActiveRswpBlockCount;
            bool hasStates = states != null && states.Length > 0;
            bool canEdit = _arduinoService.IsConnected && _arduinoService.IsSpdReady;

            for (int i = 0; i < Blocks.Count; i++)
            {
                var block = Blocks[i];
                bool isVisible = blockCount > 0 && i < blockCount;

                block.IsVisible = isVisible;
                block.IsEnabled = canEdit;

                if (!isVisible)
                {
                    block.IsProtected = false;
                }
                else if (hasStates && states != null && i < states.Length)
                {
                    block.IsProtected = states[i];
                }
                else
                {
                    block.IsProtected = false;
                }
            }
        }

        private void UpdateRswpButtonsState()
        {
            CanOperateRswp = _arduinoService.IsConnected &&
                            _arduinoService.IsSpdReady &&
                            _arduinoService.ActiveRswpBlockCount > 0;
        }

        #endregion

        public void Dispose()
        {
            _arduinoService.MemoryTypeChanged -= OnMemoryTypeChanged;
            _arduinoService.RswpStateChanged -= OnRswpStateChanged;
            _arduinoService.SpdStateChanged -= OnSpdStateChanged;
            _arduinoService.ConnectionStateChanged -= OnConnectionStateChanged;
        }
    }
}

