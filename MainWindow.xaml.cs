using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Documents;
using HexEditor.Arduino.Services;
using HexEditor.Arduino.Hardware;
using HexEditor.Constants;
using HexEditor.SpdDecoder;
using HexEditor.SpdDecoder.HpeSmartMemory;
using ArduinoHardware = HexEditor.Arduino.Hardware.Arduino;

namespace HexEditor
{
    /// <summary>
    /// Класс для представления записи в логе с поддержкой цветов
    /// </summary>
    public class LogEntry
    {
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string FormattedText { get; set; } = string.Empty;
        public string FormattedTimestamp { get; set; } = string.Empty;
        
        public Brush LevelBrush
        {
            get
            {
                return Level switch
                {
                    "Error" => (Brush)Application.Current.FindResource("ErrorBrush") ?? Brushes.Red,
                    "Warn" => (Brush)Application.Current.FindResource("WarningBrush") ?? Brushes.Orange,
                    "Info" => (Brush)Application.Current.FindResource("InfoBrush") ?? Brushes.Blue,
                    "Debug" => (Brush)Application.Current.FindResource("MutedTextBrush") ?? Brushes.Gray,
                    _ => (Brush)Application.Current.FindResource("PrimaryTextBrush") ?? Brushes.Black
                };
            }
        }
    }

    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<LogEntry> _logEntries = new();
        private readonly ObservableCollection<string> _i2cAddresses = new();
        private readonly ArduinoService _arduinoService;
        private byte[]? _lastLoggedAddresses;
        private const int MaxLogEntries = 500;
        private const string PlaceholderValue = "—";
        private readonly List<long> _verificationMismatchOffsets = new(); // Список смещений отличающихся байтов после верификации
        private readonly System.Windows.Threading.DispatcherTimer _spdUpdateTimer;
        private readonly System.Windows.Threading.DispatcherTimer _spdEditUpdateTimer; // Таймер для debouncing UpdateSpdEditPanel
        private System.Windows.Threading.DispatcherTimer? _statusTimer;
        private System.Windows.Threading.DispatcherTimer? _dpiRefreshTimer; // Таймер для DPI refresh
        private bool _lastCanUndo;
        private bool _lastCanRedo;
        private bool _lastCanCopy;
        private bool _lastCanPaste;

        public MainWindow()
        {
            InitializeComponent();
            
            // Проверяем доступность ApplicationFontFamily после инициализации
            if (TryFindResource("ApplicationFontFamily") is FontFamily fontFamily)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: ApplicationFontFamily resource found: {fontFamily.Source}");
                System.Diagnostics.Debug.WriteLine($"MainWindow: FontFamily.FamilyNames: {string.Join(", ", fontFamily.FamilyNames.Values)}");
                
                // Проверяем доступность Bold шрифта
                try
                {
                    var boldTypeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                    if (boldTypeface.TryGetGlyphTypeface(out _))
                    {
                        System.Diagnostics.Debug.WriteLine($"MainWindow: ✓ Bold font (FontWeights.Bold) is available");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"MainWindow: ✗ Bold font GlyphTypeface creation failed");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MainWindow: ✗ Error testing Bold font: {ex.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: WARNING - ApplicationFontFamily resource NOT found!");
            }
            
            // Проверяем доступность ApplicationFontFamilyBold (явный Bold ресурс)
            if (TryFindResource("ApplicationFontFamilyBold") is FontFamily boldFontFamily)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: ✓ ApplicationFontFamilyBold resource found: {boldFontFamily.Source}");
                System.Diagnostics.Debug.WriteLine($"MainWindow: Bold FontFamily.FamilyNames: {string.Join(", ", boldFontFamily.FamilyNames.Values)}");
                
                // Проверяем, что Bold FontFamily действительно работает
                try
                {
                    var boldTypeface = new Typeface(boldFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                    if (boldTypeface.TryGetGlyphTypeface(out _))
                    {
                        System.Diagnostics.Debug.WriteLine($"MainWindow: ✓ ApplicationFontFamilyBold is working correctly");
                        System.Diagnostics.Debug.WriteLine($"MainWindow:   TextBlocks with CardTitleStyle will use JetBrainsMono-Bold.ttf");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"MainWindow: ✗ ApplicationFontFamilyBold GlyphTypeface creation failed");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MainWindow: ✗ Error testing ApplicationFontFamilyBold: {ex.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: WARNING - ApplicationFontFamilyBold resource NOT found!");
                System.Diagnostics.Debug.WriteLine("MainWindow:   Bold text will use ApplicationFontFamily (may be synthetic bold)");
            }
            
            // Устанавливаем версию в заголовке окна
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            if (version != null)
            {
                // Используем только Major.Minor.Build, игнорируем Revision если он 0
                if (version.Revision > 0)
                {
                    Title = $"SPD-RW-EDIT v{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
                }
                else
                {
                    Title = $"SPD-RW-EDIT v{version.Major}.{version.Minor}.{version.Build}";
                }
            }
            else
            {
                Title = "SPD-RW-EDIT v1.0.0";
            }
            
            _arduinoService = new ArduinoService();
            _arduinoService.LogGenerated += OnArduinoLogGenerated;
            _arduinoService.ConnectionStateChanged += OnArduinoConnectionStateChanged;
            _arduinoService.ConnectionInfoChanged += OnArduinoConnectionInfoChanged;
            _arduinoService.SpdStateChanged += OnArduinoSpdStateChanged;
            _arduinoService.MemoryTypeChanged += OnArduinoMemoryTypeChanged;
            _arduinoService.RswpStateChanged += OnArduinoRswpStateChanged;
            _arduinoService.StateChanged += OnArduinoServiceStateChanged;

            _spdUpdateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300) // Увеличено до 300ms для снижения нагрузки при редактировании
            };
            _spdUpdateTimer.Tick += OnSpdUpdateTimerTick;
            
            _spdEditUpdateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300) // Debouncing для UpdateSpdEditPanel
            };
            _spdEditUpdateTimer.Tick += (_, _) =>
            {
                _spdEditUpdateTimer.Stop();
                UpdateSpdEditPanel();
            };

            LogListBox.ItemsSource = _logEntries;
            LogTabListBox.ItemsSource = _logEntries;
            DevicesListBox.ItemsSource = _arduinoService.Devices;
            I2CAddressesListBox.ItemsSource = _i2cAddresses;
            ToggleConnectionButton.Content = "Подключить";
            ToggleConnectionButton.IsEnabled = false;
            ResetDeviceDetails();
            SetupDpiHandling();
            StartStatusTimer();
            EnsureProperInitialization();

            // Подписываемся на событие завершения загрузки файла
            HexEditor.FileLoadCompleted += OnHexEditorFileLoadCompleted;
            HexEditor.DataModified += OnHexEditorDataModified;
            
            // Подписываемся на события выделения байтов в SPD Info Panel
            if (SpdInfoPanel != null)
            {
                SpdInfoPanel.HighlightBytes += OnSpdInfoHighlightBytes;
                SpdInfoPanel.ClearHighlight += OnSpdInfoClearHighlight;
                // Подписываемся на прокрутку для оптимизации производительности
                SpdInfoPanel.PreviewMouseWheel += OnSpdInfoPanelMouseWheel;
            }
            
            // Подписываемся на события изменения данных в SPD Edit Panel
            if (SpdEditPanel != null)
            {
                SpdEditPanel.ChangesApplied += OnSpdEditChangesApplied;
            }
            
            // Подписываемся на события изменения данных в HPE SmartMemory Panel
            if (HpeSmartMemoryPanel != null)
            {
                HpeSmartMemoryPanel.ChangesApplied += OnHpeSmartMemoryChangesApplied;
            }

            HideProgress();
            UpdateCrcStatusBadge();
            UpdateMemoryTypeBadge();
            UpdateConnectionStatusBadge();
        }
        
        private List<(long offset, int length)>? _currentHighlightRanges;
        private System.Windows.Threading.DispatcherTimer? _highlightTimer;
        private List<(long offset, int length)>? _pendingHighlight;
        private System.Windows.Threading.DispatcherTimer? _clearHighlightTimer;
        private bool _isScrolling;
        private System.Windows.Threading.DispatcherTimer? _scrollEndTimer;
        
        // Сохраняем ссылки на обработчики для правильной отписки (предотвращение утечек памяти)
        private EventHandler? _highlightTimerTickHandler;
        private EventHandler? _clearHighlightTimerTickHandler;
        private EventHandler? _scrollEndTimerTickHandler;
        
        
        private bool _lastSpdIsDdr4;
        private bool _lastSpdIsDdr5;
        private bool _lastCrcValid = true;
        private bool _lastSpdHasData;
        private byte _lastMemoryTypeCode;
        private bool _isArduinoConnected;
        private string _lastConnectionPort = string.Empty;
        
        private void OnSpdInfoHighlightBytes(IReadOnlyList<(long offset, int length)> ranges)
        {
            // Игнорируем подсветку во время прокрутки для улучшения производительности
            if (_isScrolling)
            {
                return;
            }
            
            if (ranges == null || ranges.Count == 0)
            {
                return;
            }
            
            // Отменяем таймер очистки подсветки, если он активен
            // Это предотвращает мерцание при быстром переходе между строками
            _clearHighlightTimer?.Stop();
            
            // Простая проверка: если ranges не изменились, ничего не делаем
            if (_currentHighlightRanges != null && 
                _currentHighlightRanges.Count == ranges.Count &&
                RangesEqual(_currentHighlightRanges, ranges))
            {
                return;
            }
            
            // Сохраняем запрос на выделение для debouncing
            // Оптимизация: переиспользуем список если ranges уже List, иначе создаем новый
            if (ranges is List<(long offset, int length)> list)
            {
                _pendingHighlight = list;
            }
            else
            {
                // Создаем новый список только если ranges не List
                if (_pendingHighlight == null)
                {
                    _pendingHighlight = new List<(long offset, int length)>(ranges);
                }
                else
                {
                    // Переиспользуем существующий список
                    _pendingHighlight.Clear();
                    _pendingHighlight.AddRange(ranges);
                }
            }
            
            if (_highlightTimer == null)
            {
                _highlightTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(10) // Быстрая реакция на подсветку
                };
                _highlightTimerTickHandler = (_, _) => ApplyHighlight();
                _highlightTimer.Tick += _highlightTimerTickHandler;
            }
            
            _highlightTimer.Stop();
            _highlightTimer.Start();
        }
        
        /// <summary>
        /// Вычисляет hash для ranges (быстрая проверка равенства)
        /// </summary>
        private static int ComputeRangesHash(IReadOnlyList<(long offset, int length)> ranges)
        {
            if (ranges == null || ranges.Count == 0) return 0;
            
            // Простой hash на основе offset и length
            int hash = ranges.Count;
            for (int i = 0; i < ranges.Count && i < 4; i++) // Ограничиваем до 4 элементов для производительности
            {
                hash = unchecked(hash * 31 + (int)ranges[i].offset);
                hash = unchecked(hash * 31 + ranges[i].length);
            }
            return hash;
        }
        
        private static bool RangesEqual(IReadOnlyList<(long offset, int length)> a, IReadOnlyList<(long offset, int length)> b)
        {
            // Быстрая проверка на равенство ссылок
            if (ReferenceEquals(a, b)) return true;
            if (a.Count != b.Count) return false;
            
            // Для небольших списков обычный цикл достаточно быстр
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].offset != b[i].offset || a[i].length != b[i].length)
                    return false;
            }
            return true;
        }
        
        private void ApplyHighlight()
        {
            // Проверяем еще раз на случай, если прокрутка началась во время ожидания таймера
            if (_isScrolling || _pendingHighlight == null || _pendingHighlight.Count == 0)
            {
                return;
            }
            
            var ranges = _pendingHighlight;
            _pendingHighlight = null;
            
            // Удаляем предыдущее выделение, если есть
            if (_currentHighlightRanges != null && _currentHighlightRanges.Count > 0)
            {
                var offsetsToRemove = _currentHighlightRanges.Select(r => r.offset).ToList();
                HexEditor.RemoveBookmarkRanges(offsetsToRemove, Colors.Red);
            }
            
            // Применяем новое выделение
            var rangesToAdd = ranges.Select(r => (r.offset, (long)r.length)).ToList();
            HexEditor.AddBookmarkRanges(rangesToAdd, Colors.Red);
            
            _currentHighlightRanges = ranges;
        }
        
        private void OnSpdInfoClearHighlight()
        {
            // Игнорируем очистку подсветки во время прокрутки
            if (_isScrolling)
            {
                return;
            }
            
            _pendingHighlight = null;
            _highlightTimer?.Stop();
            
            // Используем debouncing для ClearHighlight, чтобы предотвратить мерцание
            // при быстром переходе между строками
            if (_clearHighlightTimer == null)
            {
                _clearHighlightTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100) // Увеличено с 50ms до 100ms
                };
                _clearHighlightTimerTickHandler = (_, _) => ApplyClearHighlight();
                _clearHighlightTimer.Tick += _clearHighlightTimerTickHandler;
            }
            
            _clearHighlightTimer.Stop();
            _clearHighlightTimer.Start();
        }
        
        private void ApplyClearHighlight()
        {
            _clearHighlightTimer?.Stop();
            
            // Оптимизация: используем пакетный метод для массового удаления (один InvalidateRender вместо множества)
            if (_currentHighlightRanges != null && _currentHighlightRanges.Count > 0)
            {
                var offsetsToRemove = _currentHighlightRanges.Select(r => r.offset).ToList();
                HexEditor.RemoveBookmarkRanges(offsetsToRemove, Colors.Red);
                _currentHighlightRanges = null;
            }
        }
        
        private void OnSpdInfoPanelMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Устанавливаем флаг прокрутки
            _isScrolling = true;
            
            // Отменяем все таймеры подсветки во время прокрутки
            _highlightTimer?.Stop();
            _clearHighlightTimer?.Stop();
            _pendingHighlight = null;
            
            // Создаем таймер для сброса флага прокрутки после окончания прокрутки
            if (_scrollEndTimer == null)
            {
                _scrollEndTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(150) // Сбрасываем флаг через 150ms после последнего события прокрутки
                };
                _scrollEndTimerTickHandler = (_, _) =>
                {
                    _isScrolling = false;
                    _scrollEndTimer?.Stop();
                };
                _scrollEndTimer.Tick += _scrollEndTimerTickHandler;
            }
            
            _scrollEndTimer.Stop();
            _scrollEndTimer.Start();
        }


        private void SetupDpiHandling()
        {
            this.DpiChanged += OnDpiChanged;
            SourceInitialized += OnSourceInitialized;
        }

        private void EnsureProperInitialization()
        {
            // Гарантируем правильную инициализацию после полной загрузки
            ContentRendered += (s, e) =>
            {
                HexEditor.ForceDpiRefresh();
                Debug.WriteLine("MainWindow fully rendered - scrollbar should be operational");
            };
        }

        private void StartStatusTimer()
        {
            _statusTimer = new System.Windows.Threading.DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromMilliseconds(500); // Увеличено с 100мс до 500мс для снижения нагрузки на CPU
            _statusTimer.Tick += (s, e) =>
            {
                UpdateUndoRedoMenuItems();
                UpdateEditMenuItems();
            };
            _statusTimer.Start();
            
            // Инициализируем начальные значения
            _lastCanUndo = HexEditor.CanUndo;
            _lastCanRedo = HexEditor.CanRedo;
            _lastCanCopy = HexEditor.CanCopy;
            _lastCanPaste = HexEditor.CanPaste;
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var source = PresentationSource.FromVisual(this) as HwndSource;
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_DPICHANGED = 0x02E0;
            const int WM_SETTINGCHANGE = 0x001A;

            if (msg == WM_DPICHANGED)
            {
                Debug.WriteLine("WM_DPICHANGED received - updating DPI automatically");
                Dispatcher.BeginInvoke(() => HexEditor.ForceDpiRefresh());
                handled = true;
            }
            else if (msg == WM_SETTINGCHANGE)
            {
                string? setting = Marshal.PtrToStringAuto(lParam);
                if (setting == "WindowMetrics" || setting == "LogPixels")
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        // Переиспользуем существующий таймер вместо создания нового (оптимизация памяти)
                        if (_dpiRefreshTimer == null)
                        {
                            _dpiRefreshTimer = new System.Windows.Threading.DispatcherTimer
                            {
                                Interval = TimeSpan.FromMilliseconds(500)
                            };
                            _dpiRefreshTimer.Tick += (s, e) =>
                            {
                                _dpiRefreshTimer?.Stop();
                                HexEditor.ForceDpiRefresh();
                            };
                        }
                        
                        _dpiRefreshTimer.Stop();
                        _dpiRefreshTimer.Start();
                    });
                }
            }

            return IntPtr.Zero;
        }

        private void OnDpiChanged(object sender, DpiChangedEventArgs e)
        {
            Debug.WriteLine($"MainWindow DPI changed: {e.NewDpi.PixelsPerDip}");
        }

        private void OnHexEditorFileLoadCompleted(object? sender, EventArgs e)
        {
            // Принудительное обновление скроллбара после загрузки файла
            Dispatcher.BeginInvoke(() =>
            {
                // Очищаем подсветки верификации при загрузке нового файла
                ClearVerificationHighlights();
                Debug.WriteLine("File load completed - scrollbar should be updated");
                ScheduleSpdInfoUpdate(immediate: true);
                UpdateSpdEditPanel();
                UpdateReadButtonState();
            });
        }

        private void OnHexEditorDataModified(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ScheduleSpdInfoUpdate();
                ScheduleSpdEditUpdate(); // Используем debouncing вместо немедленного обновления
                UpdateReadButtonState();
            });
        }
        
        /// <summary>
        /// Планирует обновление SpdEditPanel с debouncing для снижения нагрузки
        /// </summary>
        private void ScheduleSpdEditUpdate()
        {
            if (SpdEditPanel == null)
            {
                return;
            }
            
            _spdEditUpdateTimer.Stop();
            _spdEditUpdateTimer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // Останавливаем таймеры и отписываемся от событий (предотвращение утечек памяти)
                _spdUpdateTimer?.Stop();
                _spdEditUpdateTimer?.Stop();
                _statusTimer?.Stop();
                _dpiRefreshTimer?.Stop();
                
                if (_highlightTimer != null)
                {
                    _highlightTimer.Stop();
                    if (_highlightTimerTickHandler != null)
                    {
                        _highlightTimer.Tick -= _highlightTimerTickHandler;
                    }
                }
                
                if (_clearHighlightTimer != null)
                {
                    _clearHighlightTimer.Stop();
                    if (_clearHighlightTimerTickHandler != null)
                    {
                        _clearHighlightTimer.Tick -= _clearHighlightTimerTickHandler;
                    }
                }
                
                if (_scrollEndTimer != null)
                {
                    _scrollEndTimer.Stop();
                    if (_scrollEndTimerTickHandler != null)
                    {
                        _scrollEndTimer.Tick -= _scrollEndTimerTickHandler;
                    }
                }
                
                // Очищаем подсветки
                OnSpdInfoClearHighlight();
                
                // Отписываемся от событий ArduinoService
                if (_arduinoService != null)
                {
                    _arduinoService.LogGenerated -= OnArduinoLogGenerated;
                    _arduinoService.ConnectionStateChanged -= OnArduinoConnectionStateChanged;
                    _arduinoService.ConnectionInfoChanged -= OnArduinoConnectionInfoChanged;
                    _arduinoService.SpdStateChanged -= OnArduinoSpdStateChanged;
                    _arduinoService.MemoryTypeChanged -= OnArduinoMemoryTypeChanged;
                    _arduinoService.RswpStateChanged -= OnArduinoRswpStateChanged;
                    _arduinoService.StateChanged -= OnArduinoServiceStateChanged;
                }
                
                // Отписываемся от событий HexEditor
                if (HexEditor != null)
                {
                    HexEditor.FileLoadCompleted -= OnHexEditorFileLoadCompleted;
                    HexEditor.DataModified -= OnHexEditorDataModified;
                    HexEditor.DisposeResources();
                }
                
                // Отписываемся от событий SpdInfoPanel
                if (SpdInfoPanel != null)
                {
                    SpdInfoPanel.HighlightBytes -= OnSpdInfoHighlightBytes;
                    SpdInfoPanel.ClearHighlight -= OnSpdInfoClearHighlight;
                    SpdInfoPanel.PreviewMouseWheel -= OnSpdInfoPanelMouseWheel;
                }
                
                // Отписываемся от событий SpdEditPanel
                if (SpdEditPanel != null)
                {
                    SpdEditPanel.ChangesApplied -= OnSpdEditChangesApplied;
                }
                
                if (HpeSmartMemoryPanel != null)
                {
                    HpeSmartMemoryPanel.ChangesApplied -= OnHpeSmartMemoryChangesApplied;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disposing resources: {ex.Message}");
            }

            base.OnClosed(e);
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.F5 &&
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            {
                HexEditor.ForceDpiRefresh();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private void UpdateUndoRedoMenuItems()
        {
            // Обновляем только если состояние изменилось (оптимизация CPU)
            bool canUndo = HexEditor.CanUndo;
            bool canRedo = HexEditor.CanRedo;
            
            if (canUndo != _lastCanUndo)
            {
                UndoMenuItem.IsEnabled = canUndo;
                _lastCanUndo = canUndo;
            }
            
            if (canRedo != _lastCanRedo)
            {
                RedoMenuItem.IsEnabled = canRedo;
                _lastCanRedo = canRedo;
            }
        }

        private void UpdateEditMenuItems()
        {
            // Обновляем только если состояние изменилось (оптимизация CPU)
            bool canCopy = HexEditor.CanCopy;
            bool canPaste = HexEditor.CanPaste;
            
            if (canCopy != _lastCanCopy)
            {
                CopyMenuItem.IsEnabled = canCopy;
                _lastCanCopy = canCopy;
            }
            
            if (canPaste != _lastCanPaste)
            {
                PasteMenuItem.IsEnabled = canPaste;
                _lastCanPaste = canPaste;
            }
        }

        private void OnSpdUpdateTimerTick(object? sender, EventArgs e)
        {
            _spdUpdateTimer.Stop();
            UpdateSpdInfo();
        }

        private void ScheduleSpdInfoUpdate(bool immediate = false)
        {
            if (SpdInfoPanel == null)
            {
                return;
            }

            if (immediate)
            {
                _spdUpdateTimer.Stop();
                UpdateSpdInfo();
                return;
            }

            _spdUpdateTimer.Stop();
            _spdUpdateTimer.Start();
        }

        private void UpdateSpdInfo()
        {
            if (SpdInfoPanel == null) return;

            if (HexEditor.DocumentLength == 0)
            {
                SpdInfoPanel.Clear();
                HpeSmartMemoryPanel?.Clear();
                _lastSpdIsDdr4 = false;
                _lastSpdIsDdr5 = false;
                _lastCrcValid = true;
                _lastSpdHasData = false;
                _lastMemoryTypeCode = 0;
                UpdateFixCrcButtonState();
                UpdateCrcStatusBadge();
                UpdateMemoryTypeBadge();
                return;
            }

            try
            {
                // Read up to 1024 bytes for DDR5 support (DDR4 uses 512, DDR5 uses 1024)
                var data = HexEditor.ReadBytes(0, (int)Math.Min(HexEditor.DocumentLength, SpdConstants.DDR5_SPD_SIZE));
                _lastSpdHasData = data.Length >= 256;
                var forcedType = GetForcedMemoryType();
                SpdInfoPanel.UpdateSpdData(data, forcedType);
                
                // Update HPE SmartMemory panel
                bool isArduinoMode = _arduinoService.IsConnected;
                HpeSmartMemoryPanel?.UpdateSpdData(data, isArduinoMode);
                
                UpdateCrcState(data);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating SPD info: {ex.Message}");
            }
        }
        
        private ForcedMemoryType GetForcedMemoryType()
        {
            if (ForcedMemoryTypeComboBox?.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                return item.Tag?.ToString() switch
                {
                    "Ddr4" => ForcedMemoryType.Ddr4,
                    "Ddr5" => ForcedMemoryType.Ddr5,
                    _ => ForcedMemoryType.Auto
                };
            }
            return ForcedMemoryType.Auto;
        }
        
        private void ForcedMemoryTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Обновляем SPD Info и Edit при изменении выбора типа памяти
            ScheduleSpdInfoUpdate(true);
            UpdateSpdEditPanel();
        }

        private void UpdateCrcState(byte[] data)
        {
            _lastMemoryTypeCode = data.Length > 2 ? data[2] : (byte)0;
            _lastSpdIsDdr4 = _lastMemoryTypeCode == 0x0C;
            _lastSpdIsDdr5 = _lastMemoryTypeCode == 0x12;

            if (_lastSpdIsDdr4)
            {
                try
                {
                    var decoder = new Ddr4SpdDecoder(data);
                    var crcInfo = decoder.GetDdr4CrcInfo();
                    _lastCrcValid = crcInfo.IsValid;
                }
                catch
                {
                    _lastCrcValid = true;
                }
            }
            else if (_lastSpdIsDdr5)
            {
                try
                {
                    var decoder = new Ddr5SpdDecoder(data);
                    var crcInfo = decoder.GetDdr5CrcInfo();
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

        private void UpdateFixCrcButtonState()
        {
            if (FixCrcButton == null)
            {
                return;
            }

            bool hasData = _lastSpdHasData;
            // Enable Fix CRC button for both DDR4 and DDR5 when CRC is invalid
            FixCrcButton.IsEnabled = hasData && (_lastSpdIsDdr4 || _lastSpdIsDdr5) && !_lastCrcValid;
        }

        private void UpdateCrcStatusBadge()
        {
            if (CrcStatusBadge == null || CrcStatusText == null)
            {
                return;
            }

            // Show CRC status for both DDR4 and DDR5
            if (!_lastSpdHasData || (!_lastSpdIsDdr4 && !_lastSpdIsDdr5))
            {
                CrcStatusBadge.Background = Brushes.Transparent;
                CrcStatusBadge.BorderBrush = Brushes.Transparent;
                CrcStatusText.Text = "CRC —";
                CrcStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
                return;
            }

            CrcStatusBadge.BorderBrush = Brushes.Transparent;
            if (_lastCrcValid)
            {
                CrcStatusBadge.Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // Green
                CrcStatusText.Text = "CRC-OK";
                CrcStatusText.Foreground = Brushes.White;
            }
            else
            {
                CrcStatusBadge.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)); // Red
                CrcStatusText.Text = "! CRC-BAD";
                CrcStatusText.Foreground = Brushes.White;
            }
        }

        private void UpdateMemoryTypeBadge()
        {
            if (MemoryTypeBadge == null || MemoryTypeText == null)
            {
                return;
            }

            if (!_lastSpdHasData)
            {
                MemoryTypeBadge.Background = Brushes.Transparent;
                MemoryTypeBadge.BorderBrush = Brushes.Transparent;
                MemoryTypeText.Text = "DDR —";
                MemoryTypeText.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));
                return;
            }

            MemoryTypeBadge.BorderBrush = Brushes.Transparent;
            if (_lastMemoryTypeCode == 0x0C)
            {
                MemoryTypeBadge.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)); // Yellow
                MemoryTypeText.Text = "DDR4";
                MemoryTypeText.Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11)); // Black
            }
            else if (_lastMemoryTypeCode == 0x12)
            {
                MemoryTypeBadge.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)); // Yellow
                MemoryTypeText.Text = "DDR5";
                MemoryTypeText.Foreground = Brushes.White;
            }
            else
            {
                MemoryTypeBadge.Background = Brushes.Transparent;
                MemoryTypeText.Text = "DDR ?";
                MemoryTypeText.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));
            }
        }

        private void UpdateConnectionStatusBadge()
        {
            if (ConnectionStatusBadge == null || ConnectionStatusText == null)
            {
                return;
            }

            if (!_isArduinoConnected || string.IsNullOrWhiteSpace(_lastConnectionPort) || _lastConnectionPort == PlaceholderValue)
            {
                ConnectionStatusBadge.Background = Brushes.Transparent;
                ConnectionStatusBadge.BorderBrush = Brushes.Transparent;
                ConnectionStatusText.Text = "Автономный";
                ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));
                return;
            }

            // Зелёный фон для индикатора подключения
            ConnectionStatusBadge.Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // Зелёный цвет
            ConnectionStatusBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)); // Тёмно-зелёная граница
            ConnectionStatusText.Foreground = Brushes.White; // Белый текст на зелёном фоне

            string formattedPort = FormatConnectionPort(_lastConnectionPort);
            ConnectionStatusText.Text = $"Arduino {formattedPort}";
        }

        private static string FormatConnectionPort(string port)
        {
            if (string.IsNullOrWhiteSpace(port))
            {
                return "—";
            }

            if (port.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            {
                var suffix = port.Substring(3).TrimStart(':').Trim();
                return string.IsNullOrEmpty(suffix) ? port : suffix;
            }

            return port;
        }
        private void ResetDeviceDetails()
        {
            DetailPortText.Text = PlaceholderValue;
            DetailFirmwareText.Text = PlaceholderValue;
            DetailNameText.Text = PlaceholderValue;
            DetailClockText.Text = PlaceholderValue;
            DetailRswpText.Text = PlaceholderValue;
            UpdateRswpDisplay(Array.Empty<bool>());
            UpdateI2CAddresses(Array.Empty<byte>());
            UpdateReadButtonState();
        }

        private void SetDeviceDetails(string port, string firmware, string name, string clock, string ddr4Rswp)
        {
            DetailPortText.Text = string.IsNullOrWhiteSpace(port) ? PlaceholderValue : port;
            DetailFirmwareText.Text = string.IsNullOrWhiteSpace(firmware) ? PlaceholderValue : firmware;
            DetailNameText.Text = string.IsNullOrWhiteSpace(name) ? PlaceholderValue : name;
            DetailClockText.Text = string.IsNullOrWhiteSpace(clock) ? PlaceholderValue : clock;
            DetailRswpText.Text = string.IsNullOrWhiteSpace(ddr4Rswp) ? PlaceholderValue : ddr4Rswp;
        }

        private void UpdateToggleConnectionButtonState()
        {
            bool isConnected = _arduinoService.IsConnected;
            ToggleConnectionButton.Content = isConnected ? "Отключить" : "Подключить";

            if (isConnected)
            {
                ToggleConnectionButton.IsEnabled = !_arduinoService.IsConnecting;
            }
            else
            {
                bool canConnect =
                    _arduinoService.SelectedDevice != null &&
                    !_arduinoService.IsScanning &&
                    !_arduinoService.IsConnecting;
                ToggleConnectionButton.IsEnabled = canConnect;
            }

            SearchButton.IsEnabled = !_arduinoService.IsScanning && !_arduinoService.IsConnecting;

            UpdateReadButtonState();
        }

        private void UpdateReadButtonState()
        {
            bool canRead = _arduinoService.IsConnected &&
                          _arduinoService.IsSpdReady &&
                          !_arduinoService.IsReading;
            
            ReadButton.IsEnabled = canRead;
            
            bool canWrite = _arduinoService.IsConnected &&
                           _arduinoService.IsSpdReady &&
                           HexEditor.DocumentLength > 0;
            
            WriteButton.IsEnabled = canWrite;
            VerifyButton.IsEnabled = canWrite;
        }

        private void UpdateRswpButtonsState()
        {
            bool canOperate = _arduinoService.IsConnected &&
                              _arduinoService.IsSpdReady &&
                              _arduinoService.ActiveRswpBlockCount > 0;

            CheckRswpButton.IsEnabled = canOperate;
            ClearRswpButton.IsEnabled = canOperate;
            SetRswpButton.IsEnabled = canOperate;
        }

        private void UpdateRswpDisplay(bool[]? states)
        {
            var checkBoxes = new[]
            {
                RswpBlock0CheckBox, RswpBlock1CheckBox, RswpBlock2CheckBox, RswpBlock3CheckBox,
                RswpBlock4CheckBox, RswpBlock5CheckBox, RswpBlock6CheckBox, RswpBlock7CheckBox,
                RswpBlock8CheckBox, RswpBlock9CheckBox, RswpBlock10CheckBox, RswpBlock11CheckBox,
                RswpBlock12CheckBox, RswpBlock13CheckBox, RswpBlock14CheckBox, RswpBlock15CheckBox
            };

            var textBlocks = new[]
            {
                RswpBlock0Text, RswpBlock1Text, RswpBlock2Text, RswpBlock3Text,
                RswpBlock4Text, RswpBlock5Text, RswpBlock6Text, RswpBlock7Text,
                RswpBlock8Text, RswpBlock9Text, RswpBlock10Text, RswpBlock11Text,
                RswpBlock12Text, RswpBlock13Text, RswpBlock14Text, RswpBlock15Text
            };

            UpdateRswpMemoryTypeLabel();

            int blockCount = _arduinoService.ActiveRswpBlockCount;
            bool hasStates = states != null && states.Length > 0;
            bool canEdit = _arduinoService.IsConnected && _arduinoService.IsSpdReady;

            for (int i = 0; i < checkBoxes.Length; i++)
            {
                if (checkBoxes[i] is not CheckBox checkBox || textBlocks[i] is not TextBlock textBlock)
                {
                    continue;
                }

                bool isVisible = blockCount > 0 && i < blockCount;
                checkBox.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                textBlock.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

                if (!isVisible)
                {
                    checkBox.IsChecked = false;
                    checkBox.IsEnabled = false;
                    textBlock.Text = $"Block {i}: - {PlaceholderValue}";
                    textBlock.Foreground = new SolidColorBrush(Colors.Black);
                    continue;
                }

                if (hasStates && states != null && i < states.Length)
                {
                    bool isProtected = states[i];
                    checkBox.IsChecked = isProtected;
                    textBlock.Text = $"Block {i}: - {(isProtected ? "Защищен" : "Не защищен")}";
                    textBlock.Foreground = isProtected
                        ? new SolidColorBrush(Colors.Red)
                        : new SolidColorBrush(Colors.Green);
                }
                else
                {
                    checkBox.IsChecked = false;
                    textBlock.Text = $"Block {i}: - {PlaceholderValue}";
                    textBlock.Foreground = new SolidColorBrush(Colors.Black);
                }

                checkBox.IsEnabled = canEdit;
            }
        }

        private void UpdateRswpMemoryTypeLabel()
        {
            if (RswpMemoryTypeText == null)
            {
                return;
            }

            string typeText = _arduinoService.ActiveMemoryType switch
            {
                SpdMemoryType.Ddr4 => "DDR4",
                SpdMemoryType.Ddr5 => "DDR5",
                _ => PlaceholderValue
            };

            RswpMemoryTypeText.Text = $"Тип памяти: {typeText}";
        }

        private void UpdateI2CAddresses(byte[] addresses)
        {
            _i2cAddresses.Clear();
            
            if (addresses == null || addresses.Length == 0)
            {
                return;
            }

            foreach (byte address in addresses.OrderBy(a => a))
            {
                string description = GetI2CAddressDescription(address);
                _i2cAddresses.Add($"0x{address:X2} - {description}");
            }
        }

        private static string GetI2CAddressDescription(byte address)
        {
            return address switch
            {
                // Термодатчики (Temperature Sensors)
                0x18 => "термодатчик",
                0x19 => "термодатчик",
                0x1A => "термодатчик",
                0x1B => "термодатчик",
                0x1C => "термодатчик",
                0x1D => "термодатчик",
                0x1E => "термодатчик",
                0x1F => "термодатчик",
                // Регистры страниц EEPROM
                0x36 => "регистр страниц EEPROM SPA0",
                0x37 => "регистр страниц EEPROM SPA1",
                // EEPROM модули памяти (SPD)
                0x50 => "EEPROM",
                0x51 => "EEPROM",
                0x52 => "EEPROM",
                0x53 => "EEPROM",
                0x54 => "EEPROM",
                0x55 => "EEPROM",
                0x56 => "EEPROM",
                0x57 => "EEPROM",
                _ => "неизвестное устройство"
            };
        }

        private void OnArduinoLogGenerated(object? sender, ArduinoLogEventArgs e)
        {
            AppendLog(e.Level, e.Message);
        }

        private void OnArduinoConnectionStateChanged(object? sender, bool isConnected)
        {
            Dispatcher.Invoke(() =>
            {
                if (!isConnected)
                {
                    ResetDeviceDetails();
                    _lastConnectionPort = string.Empty;
                }
                // Не обновляем адреса здесь - это будет сделано в OnArduinoConnectionInfoChanged
                // чтобы избежать множественных вызовов

                _isArduinoConnected = isConnected;
                UpdateConnectionStatusBadge();
                UpdateToggleConnectionButtonState();
            });
        }

        private void OnArduinoConnectionInfoChanged(object? sender, ArduinoConnectionInfo info)
        {
            Dispatcher.Invoke(() =>
            {
                if (ReferenceEquals(info, ArduinoConnectionInfo.Empty))
                {
                    ResetDeviceDetails();
                    _lastConnectionPort = string.Empty;
                }
                else
                {
                    SetDeviceDetails(info.Port, info.FirmwareVersion, info.Name, info.I2CClock, info.Ddr4Rswp);
                    _lastConnectionPort = info.Port;
                    // Обновляем адреса сразу после получения информации о подключении
                    // device.Scan() уже был вызван в ConnectAsync, кэш установлен
                    UpdateI2CAddressesFromDevice();
                }

                UpdateConnectionStatusBadge();
            });

            // Автоматическая проверка имени при подключении
            if (!ReferenceEquals(info, ArduinoConnectionInfo.Empty) && string.IsNullOrWhiteSpace(info.Name))
            {
                AppendLog("Info", "Имя устройства не задано. Предлагается задать имя.");
                
                // Показываем диалог асинхронно, чтобы не блокировать UI
                _ = Dispatcher.BeginInvoke(new System.Action(async () =>
                {
                    var dialog = new InputDialog
                    {
                        Owner = this,
                        Title = "Задать имя устройства",
                        Prompt = $"Имя устройства не задано.\n\nВведите имя для устройства (максимум {ArduinoHardware.Command.NAMELENGTH} символов, только ASCII):",
                        DefaultValue = "",
                        MaxLength = ArduinoHardware.Command.NAMELENGTH,
                        AllowOnlyAscii = true
                    };

                    if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
                    {
                        bool success = await _arduinoService.SetDeviceNameAsync(dialog.ResponseText.Trim());
                        if (success)
                        {
                            AppendLog("Info", $"Имя устройства установлено: '{dialog.ResponseText.Trim()}'");
                        }
                    }
                }));
            }
        }

        private void UpdateI2CAddressesFromDevice()
        {
            try
            {
                var device = _arduinoService.GetActiveDevice();
                if (device == null)
                {
                    AppendLog("Debug", "UpdateI2CAddressesFromDevice: device is null");
                    UpdateI2CAddresses(Array.Empty<byte>());
                    return;
                }

                if (!device.IsConnected)
                {
                    AppendLog("Debug", "UpdateI2CAddressesFromDevice: device is not connected");
                    UpdateI2CAddresses(Array.Empty<byte>());
                    return;
                }

                // Используем результаты полного сканирования, если они доступны
                // Полное сканирование выполняется в ConnectAsync перед быстрым сканом
                byte[]? fullAddresses = _arduinoService.GetFullScanAddresses();
                if (fullAddresses != null && fullAddresses.Length > 0)
                {
                    // Используем результаты полного сканирования
                    UpdateI2CAddresses(fullAddresses);
                }
                else
                {
                    // Если полное сканирование еще не выполнено, используем быстрый скан
                    try
                    {
                        byte[] addresses = device.Scan();
                        UpdateI2CAddresses(addresses);
                    }
                    catch (Exception ex)
                    {
                        // Ошибка может быть из-за извлечения EEPROM - это нормально
                        // Логируем только если устройство действительно отключено
                        if (!device.IsConnected)
                        {
                            AppendLog("Error", $"Не удалось получить I2C адреса: {ex.Message}");
                        }
                        UpdateI2CAddresses(Array.Empty<byte>());
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog("Error", $"Ошибка при обновлении I2C адресов: {ex.Message}");
                UpdateI2CAddresses(Array.Empty<byte>());
            }
        }

        private void OnArduinoSpdStateChanged(object? sender, bool isSpdReady)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateReadButtonState();
                UpdateRswpButtonsState();
                // Обновляем адреса после изменения состояния SPD
                // При вставке SPD адреса уже должны быть в кэше
                if (isSpdReady)
                {
                    UpdateI2CAddressesFromDevice();
                }
                else
                {
                    UpdateI2CAddresses(Array.Empty<byte>());
                }
            });
        }

        private void OnArduinoRswpStateChanged(object? sender, bool[] states)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateRswpDisplay(states);
            });
        }

        private void OnArduinoMemoryTypeChanged(object? sender, SpdMemoryType type)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateRswpDisplay(Array.Empty<bool>());
                UpdateRswpButtonsState();
            });
        }

        private void OnArduinoServiceStateChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateToggleConnectionButtonState();
                UpdateReadButtonState();
                UpdateRswpButtonsState();
                // Обновляем адреса I2C после изменения состояния (например, после полного сканирования)
                if (_arduinoService.IsConnected && _arduinoService.IsSpdReady)
                {
                    UpdateI2CAddressesFromDevice();
                }
            });
        }


        #region Обработчики меню
        private void Undo_Click(object sender, RoutedEventArgs e) => HexEditor.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => HexEditor.Redo();
        private void Copy_Click(object sender, RoutedEventArgs e) => HexEditor.Copy();
        private void Paste_Click(object sender, RoutedEventArgs e) => HexEditor.Paste();
        private void SelectAll_Click(object sender, RoutedEventArgs e) => HexEditor.SelectAll();

        private async void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                // Очищаем подсветки верификации при открытии нового файла
                ClearVerificationHighlights();
                await HexEditor.OpenFileAsync(openFileDialog.FileName);
            }
        }

        private async void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog();
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Асинхронное сохранение, чтобы не блокировать UI
                    await HexEditor.SaveFileAsync(saveFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка сохранения",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void SaveFileAs_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog();
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Асинхронное сохранение, чтобы не блокировать UI
                    await HexEditor.SaveFileAsync(saveFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка сохранения",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

        private async void ScanArduinoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await StartDeviceSearchAsync();
        }

        private async void RenameDeviceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!_arduinoService.IsConnected)
            {
                AppendLog("Warn", "Устройство не подключено. Сначала подключите устройство.");
                MessageBox.Show(
                    "Устройство не подключено.\n\nСначала подключите устройство.",
                    "Устройство не подключено",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Получаем текущее имя устройства
                string currentName = await Task.Run(() =>
                {
                    var device = _arduinoService.GetActiveDevice();
                    if (_arduinoService.IsConnected && device != null)
                    {
                        return device.Name;
                    }
                    return string.Empty;
                });

                // Если имени нет или оно пустое, логируем и предлагаем задать
                if (string.IsNullOrWhiteSpace(currentName))
                {
                    AppendLog("Info", "Имя устройства не задано. Предлагается задать имя.");
                    
                    var dialog = new InputDialog
                    {
                        Owner = this,
                        Title = "Задать имя устройства",
                        Prompt = $"Введите имя для устройства (максимум {ArduinoHardware.Command.NAMELENGTH} символов, только ASCII):",
                        DefaultValue = "",
                        MaxLength = ArduinoHardware.Command.NAMELENGTH,
                        AllowOnlyAscii = true
                    };

                    if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
                    {
                        bool success = await _arduinoService.SetDeviceNameAsync(dialog.ResponseText.Trim());
                        if (success)
                        {
                            AppendLog("Info", $"Имя устройства установлено: '{dialog.ResponseText.Trim()}'");
                        }
                    }
                }
                else
                {
                    // Имя есть - предлагаем переименовать
                    var dialog = new InputDialog
                    {
                        Owner = this,
                        Title = "Переименовать устройство",
                        Prompt = $"Текущее имя: '{currentName}'\n\nВведите новое имя (максимум {ArduinoHardware.Command.NAMELENGTH} символов, только ASCII):",
                        DefaultValue = currentName,
                        MaxLength = ArduinoHardware.Command.NAMELENGTH,
                        AllowOnlyAscii = true
                    };

                    if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
                    {
                        string newName = dialog.ResponseText.Trim();
                        if (newName != currentName)
                        {
                            bool success = await _arduinoService.SetDeviceNameAsync(newName);
                            if (success)
                            {
                                AppendLog("Info", $"Устройство переименовано: '{currentName}' → '{newName}'");
                            }
                        }
                        else
                        {
                            AppendLog("Info", "Имя не изменилось.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog("Error", $"Ошибка при переименовании устройства: {ex.Message}");
                MessageBox.Show(
                    $"Ошибка при переименовании устройства:\n\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await StartDeviceSearchAsync();
        }

        private async Task StartDeviceSearchAsync()
        {
            SearchButton.IsEnabled = false;
            try
            {
                await _arduinoService.ScanAsync();
                DevicesListBox.SelectedIndex = _arduinoService.Devices.Count > 0 ? 0 : -1;
            }
            finally
            {
                SearchButton.IsEnabled = true;
                UpdateToggleConnectionButtonState();
            }
        }

        private void DevicesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = DevicesListBox.SelectedItem as ArduinoDeviceInfo;
            _arduinoService.SetSelectedDevice(selected);

            if (!_arduinoService.IsConnected)
            {
                ResetDeviceDetails();
            }

            UpdateToggleConnectionButtonState();
        }

        private async void ToggleConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_arduinoService.IsConnected)
            {
                _arduinoService.Disconnect();
            }
            else
            {
                await _arduinoService.ConnectAsync();
            }
        }

        private async void ReadButton_Click(object sender, RoutedEventArgs e)
        {
            ReadButton.IsEnabled = false;
            int startTick = Environment.TickCount;
            try
            {
                // Показываем индикатор прогресса (чтение идет по 32 байта за раз)
                ShowProgress("Чтение SPD...", true);
                var data = await _arduinoService.ReadSpdDumpAsync();
                int elapsedMs = Environment.TickCount - startTick;
                
                if (data != null && data.Length > 0)
                {
                    // Очищаем подсветки верификации при чтении новых данных
                    ClearVerificationHighlights();
                    HexEditor.LoadData(data);
                    // Явно сбрасываем выделение и каретку в начало после загрузки данных
                    HexEditor.ClearSelection();
                    HexEditor.SetCaretPosition(0);
                    ScheduleSpdInfoUpdate(immediate: true);
                    UpdateSpdEditPanel();
                    UpdateReadButtonState();
                    StatusText.Text = $"Прочитано {data.Length} байт за {elapsedMs} мс";
                    
                    // Автоматическое чтение Sensor Register после чтения SPD, если найдены адреса термодатчиков
                    await TryReadSensorRegistersAfterSpdRead();
                }
                else
                {
                    StatusText.Text = "Ошибка чтения";
                }
            }
            finally
            {
                HideProgress();
                UpdateReadButtonState();
            }
        }

        private async void WriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_arduinoService.IsConnected || !_arduinoService.IsSpdReady)
            {
                AppendLog("Warn", "Устройство не подключено или SPD не готов.");
                return;
            }

            if (HexEditor.DocumentLength == 0)
            {
                AppendLog("Warn", "Нет данных для записи. Сначала загрузите или откройте файл.");
                return;
            }

            WriteButton.IsEnabled = false;
            try
            {
                var data = HexEditor.ReadBytes(0, (int)HexEditor.DocumentLength);
                
                // Валидация размера данных в зависимости от типа памяти
                var memoryType = _arduinoService.ActiveMemoryType;
                int expectedSize = memoryType switch
                {
                    SpdMemoryType.Ddr4 => 512,
                    SpdMemoryType.Ddr5 => 1024,
                    _ => 256 // DDR3 и ниже
                };
                
                if (data.Length != expectedSize)
                {
                    AppendLog("Error", $"Неверный размер SPD дампа: ожидается {expectedSize} байт для {memoryType}, получено {data.Length} байт.");
                    MessageBox.Show(
                        $"Неверный размер SPD дампа!\n\n" +
                        $"Ожидается: {expectedSize} байт ({memoryType})\n" +
                        $"Получено: {data.Length} байт\n\n" +
                        $"Проверьте, что файл соответствует типу памяти устройства.",
                        "Ошибка валидации",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
                
                long writeRangeStart = 0;
                long writeRangeEnd = data.LongLength;
                if (HexEditor.TryGetModifiedRange(out var modifiedStart, out var modifiedEnd))
                {
                    long dataLengthLong = data.LongLength;
                    writeRangeStart = Math.Max(0, Math.Min(modifiedStart, dataLengthLong));
                    writeRangeEnd = Math.Max(writeRangeStart, Math.Min(modifiedEnd, dataLengthLong));
                }
                
                // Проверяем RSWP статус
                var rswpStates = await _arduinoService.CheckRswpAsync();
                if (rswpStates != null && rswpStates.Length > 0)
                {
                    // Определяем защищенные блоки
                    var protectedBlocks = new List<int>();
                    for (int i = 0; i < rswpStates.Length; i++)
                    {
                        if (rswpStates[i])
                        {
                            protectedBlocks.Add(i);
                        }
                    }

                    if (protectedBlocks.Count > 0)
                    {
                        // Определяем размер блока
                        int blockSize = _arduinoService.ActiveMemoryType switch
                        {
                            SpdMemoryType.Ddr5 => 128,
                            SpdMemoryType.Ddr4 => 128,
                            _ => 256
                        };

                        // Проверяем, попадают ли данные в защищенные блоки
                        var affectedBlocks = new List<int>();
                        foreach (var block in protectedBlocks)
                        {
                            long blockStart = (long)block * blockSize;
                            long blockEnd = blockStart + blockSize;

                            bool intersects = writeRangeStart < blockEnd && blockStart < writeRangeEnd;
                            if (intersects)
                            {
                                affectedBlocks.Add(block);
                            }
                        }

                        if (affectedBlocks.Count > 0)
                        {
                            // Данные попадают в защищенные блоки - показываем предупреждение
                            string blocksList = string.Join(", ", affectedBlocks);
                            string message = $"Обнаружены защищенные блоки RSWP: {blocksList}.\n\n" +
                                           $"Запись в эти блоки будет пропущена.\n\n" +
                                           $"Продолжить запись?";

                            var result = MessageBox.Show(
                                message,
                                "Предупреждение: Защищенные блоки RSWP",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning,
                                MessageBoxResult.No);

                            if (result != MessageBoxResult.Yes)
                            {
                                AppendLog("Info", "Запись отменена пользователем.");
                                return;
                            }
                        }
                        else
                        {
                            // Данные не попадают в защищенные блоки - просто логируем
                            AppendLog("Info", "Защищенные блоки RSWP обнаружены, но данные не попадают в защищенную зону.");
                        }
                    }
                }

                AppendLog("Info", $"Запись {data.Length} байт в устройство...");
                int writeStartTick = Environment.TickCount;
                ShowProgress("Запись SPD...", true);

                bool writeResult = await _arduinoService.WriteSpdDumpAsync(data, skipProtectedBlocks: true);
                int writeElapsedMs = Environment.TickCount - writeStartTick;
                
                if (writeResult)
                {
                    AppendLog("Info", $"Запись успешно завершена за {writeElapsedMs} мс. Выполняется верификация...");
                    int verifyStartTick = Environment.TickCount;
                    ShowProgress("Верификация...", true);
                    
                    // Всегда выполняем верификацию после записи
                    bool verificationPassed = await PerformVerificationAsync(data);
                    int verifyElapsedMs = Environment.TickCount - verifyStartTick;
                    
                    if (verificationPassed)
                    {
                        AppendLog("Info", $"Верификация успешна за {verifyElapsedMs} мс: данные записаны корректно.");
                        StatusText.Text = $"Записано {data.Length} байт за {writeElapsedMs} мс, верификация {verifyElapsedMs} мс";
                    }
                    else
                    {
                        // Верификация не пройдена - показываем предупреждение
                        var result = MessageBox.Show(
                            "Верификация не пройдена: записанные данные не совпадают с исходными.\n\n" +
                            "Возможные причины:\n" +
                            "- Защищенные блоки RSWP были пропущены\n" +
                            "- Ошибка при записи данных\n" +
                            "- Данные в устройстве были изменены\n\n" +
                            "Проверьте логи для деталей.",
                            "Предупреждение: Верификация не пройдена",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        StatusText.Text = $"Запись {writeElapsedMs} мс, верификация не пройдена ({verifyElapsedMs} мс)";
                    }
                }
                else
                {
                    AppendLog("Error", "Ошибка при записи данных.");
                    StatusText.Text = $"Ошибка записи (попытка заняла {writeElapsedMs} мс)";
                }
            }
            catch (Exception ex)
            {
                AppendLog("Error", $"Ошибка записи: {ex.Message}");
            }
            finally
            {
                HideProgress();
                WriteButton.IsEnabled = true;
                UpdateReadButtonState();
            }
        }

        private async Task<bool> PerformVerificationAsync(byte[] localData)
        {
            try
            {
                // Очищаем предыдущие подсветки верификации
                ClearVerificationHighlights();
                
                AppendLog("Info", $"Верификация {localData.Length} байт...");
                
                var deviceData = await _arduinoService.ReadSpdDumpAsync();
                if (deviceData == null || deviceData.Length == 0)
                {
                    AppendLog("Error", "Не удалось прочитать данные из устройства для верификации.");
                    return false;
                }

                // Верифицируем все данные независимо от состояния RSWP
                bool isMatch = true;
                var mismatches = new List<string>();
                var mismatchOffsets = new List<int>(); // Список смещений отличающихся байтов
                int checkedBytes = 0;
                
                for (int i = 0; i < localData.Length && i < deviceData.Length; i++)
                {
                    checkedBytes++;
                    if (localData[i] != deviceData[i])
                    {
                        isMatch = false;
                        mismatchOffsets.Add(i);
                        mismatches.Add($"0x{i:X4}: локально 0x{localData[i]:X2}, в устройстве 0x{deviceData[i]:X2}");
                        
                        // Ограничиваем количество несоответствий в логе
                        if (mismatches.Count >= 10)
                        {
                            mismatches.Add("... (и другие)");
                            // Не прерываем цикл, продолжаем собирать все смещения для подсветки
                        }
                    }
                }

                if (localData.Length != deviceData.Length)
                {
                    AppendLog("Warn", $"Размер данных не совпадает: локально {localData.Length} байт, в устройстве {deviceData.Length} байт");
                    isMatch = false;
                }

                // Подсвечиваем отличающиеся байты красным цветом (обычные закладки)
                Dispatcher.Invoke(() =>
                {
                    foreach (int offset in mismatchOffsets)
                    {
                        HexEditor.AddBookmark(offset, Colors.Red);
                        _verificationMismatchOffsets.Add(offset);
                    }
                });

                if (isMatch)
                {
                    AppendLog("Info", $"Верификация успешна: проверено {checkedBytes} байт, все совпадают.");
                    return true;
                }
                else
                {
                    foreach (var mismatch in mismatches)
                    {
                        AppendLog("Warn", $"Несоответствие: {mismatch}");
                    }
                    AppendLog("Warn", $"Верификация не пройдена: найдено {mismatchOffsets.Count} несоответствий из {checkedBytes} проверенных байт.");
                    AppendLog("Info", $"Отличающиеся байты подсвечены красным цветом в HEX редакторе.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                AppendLog("Error", $"Ошибка верификации: {ex.Message}");
                return false;
            }
        }

        private void ClearVerificationHighlights()
        {
            // Очищаем все предыдущие подсветки верификации (обычные закладки)
            if (_verificationMismatchOffsets.Count == 0)
                return;

            if (Dispatcher.CheckAccess())
            {
                // Уже в потоке UI
                foreach (long offset in _verificationMismatchOffsets)
                {
                    HexEditor.RemoveBookmark(offset, Colors.Red);
                }
                _verificationMismatchOffsets.Clear();
            }
            else
            {
                // Вызываем из другого потока
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    foreach (long offset in _verificationMismatchOffsets)
                    {
                        HexEditor.RemoveBookmark(offset, Colors.Red);
                    }
                    _verificationMismatchOffsets.Clear();
                }));
            }
        }

        private async void VerifyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_arduinoService.IsConnected || !_arduinoService.IsSpdReady)
            {
                AppendLog("Warn", "Устройство не подключено или SPD не готов.");
                return;
            }

            if (HexEditor.DocumentLength == 0)
            {
                AppendLog("Warn", "Нет данных для верификации. Сначала загрузите или откройте файл.");
                return;
            }

            VerifyButton.IsEnabled = false;
            try
            {
                var localData = HexEditor.ReadBytes(0, (int)HexEditor.DocumentLength);
                bool verificationPassed = await PerformVerificationAsync(localData);
                
                if (verificationPassed)
                {
                    AppendLog("Info", "Верификация успешна: данные совпадают.");
                }
                else
                {
                    // Верификация не пройдена - показываем предупреждение
                    var result = MessageBox.Show(
                        "Верификация не пройдена: данные в устройстве не совпадают с локальными данными.\n\n" +
                        "Возможные причины:\n" +
                        "- Данные в устройстве были изменены\n" +
                        "- Защищенные блоки RSWP не были записаны\n" +
                        "- Ошибка при предыдущей записи\n\n" +
                        "Проверьте логи для деталей.",
                        "Предупреждение: Верификация не пройдена",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                AppendLog("Error", $"Ошибка верификации: {ex.Message}");
            }
            finally
            {
                VerifyButton.IsEnabled = true;
            }
        }

        private void FixCrcButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_lastSpdIsDdr4 && !_lastSpdIsDdr5)
            {
                MessageBox.Show("Исправление CRC доступно только для DDR4 и DDR5.", "Fix CRC",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            long documentLength = HexEditor.DocumentLength;
            int minLength = _lastSpdIsDdr5 ? 512 : 256;
            
            if (documentLength < minLength)
            {
                MessageBox.Show($"Недостаточно данных для исправления CRC. Требуется минимум {minLength} байт.",
                    "Fix CRC", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int length = _lastSpdIsDdr5 
                ? (int)Math.Min(documentLength, SpdConstants.DDR5_SPD_SIZE) 
                : (int)Math.Min(documentLength, SpdConstants.DDR4_SPD_SIZE);
            
            var buffer = HexEditor.ReadBytes(0, length);

            try
            {
                bool changed = false;
                
                if (_lastSpdIsDdr4)
                {
                    // DDR4: Fix CRC for 2 blocks (0-125 @ 126-127, 128-253 @ 254-255)
                    var decoder = new Ddr4SpdDecoder(buffer);
                    changed = decoder.FixCrc(buffer);

                    if (changed)
                    {
                        if (length > 127)
                        {
                            HexEditor.ReplaceData(126, new[] { buffer[126], buffer[127] });
                        }
                        if (length > 255)
                        {
                            HexEditor.ReplaceData(254, new[] { buffer[254], buffer[255] });
                        }
                    }
                }
                else if (_lastSpdIsDdr5)
                {
                    // DDR5: Fix CRC for single block (0-509 @ 510-511)
                    var decoder = new Ddr5SpdDecoder(buffer);
                    changed = decoder.FixCrc(buffer);

                    if (changed)
                    {
                        if (length > 511)
                        {
                            HexEditor.ReplaceData(510, new[] { buffer[510], buffer[511] });
                        }
                    }
                }

                if (!changed)
                {
                    AppendLog("Info", "CRC уже корректен.");
                    MessageBox.Show("CRC уже корректен.", "Fix CRC",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string memType = _lastSpdIsDdr5 ? "DDR5" : "DDR4";
                AppendLog("Info", $"CRC {memType} успешно исправлен.");
                StatusText.Text = $"CRC {memType} успешно исправлен.";
                ScheduleSpdInfoUpdate(immediate: true);
            }
            catch (Exception ex)
            {
                AppendLog("Error", $"Не удалось исправить CRC: {ex.Message}");
                MessageBox.Show($"Не удалось исправить CRC:\n\n{ex.Message}",
                    "Fix CRC", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CheckRswpButton_Click(object sender, RoutedEventArgs e)
        {
            CheckRswpButton.IsEnabled = false;
            try
            {
                await _arduinoService.CheckRswpAsync();
            }
            finally
            {
                UpdateRswpButtonsState();
            }
        }

        private async void ClearRswpButton_Click(object sender, RoutedEventArgs e)
        {
            ClearRswpButton.IsEnabled = false;
            try
            {
                await _arduinoService.ClearRswpAsync();
            }
            finally
            {
                UpdateRswpButtonsState();
            }
        }

        private async void SetRswpButton_Click(object sender, RoutedEventArgs e)
        {
            var checkBoxes = new[]
            {
                RswpBlock0CheckBox, RswpBlock1CheckBox, RswpBlock2CheckBox, RswpBlock3CheckBox,
                RswpBlock4CheckBox, RswpBlock5CheckBox, RswpBlock6CheckBox, RswpBlock7CheckBox,
                RswpBlock8CheckBox, RswpBlock9CheckBox, RswpBlock10CheckBox, RswpBlock11CheckBox,
                RswpBlock12CheckBox, RswpBlock13CheckBox, RswpBlock14CheckBox, RswpBlock15CheckBox
            };

            int blockCount = _arduinoService.ActiveRswpBlockCount;
            if (blockCount == 0)
            {
                UpdateRswpButtonsState();
                return;
            }

            var blocksToSet = new List<byte>();
            for (byte block = 0; block < blockCount; block++)
            {
                if (checkBoxes[block]?.IsChecked == true)
                {
                    blocksToSet.Add(block);
                }
            }

            if (blocksToSet.Count == 0)
            {
                AppendLog("Info", "Выберите блоки для установки RSWP.");
                UpdateRswpButtonsState();
                return;
            }

            SetRswpButton.IsEnabled = false;
            try
            {
                // Устанавливаем блоки через специальный метод с логированием
                await _arduinoService.SetMultipleRswpAsync(blocksToSet.ToArray());
            }
            finally
            {
                UpdateRswpButtonsState();
            }
        }

        private void CopySelectedLogMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, какой ListBox является источником (через контекстное меню)
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var listBox = contextMenu?.PlacementTarget as ListBox;
            
            if (listBox?.SelectedItem is not LogEntry entry)
            {
                return;
            }

            try
            {
                Clipboard.SetText(entry.FormattedText);
            }
            catch (Exception ex)
            {
                AppendLog("Warn", $"Clipboard error: {ex.Message}");
            }
        }

        private void CopyAllLogsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CopyAllLogs();
        }

        private void CopyAllLogsButton_Click(object sender, RoutedEventArgs e)
        {
            CopyAllLogs();
        }

        private void CopyAllLogs()
        {
            if (_logEntries.Count == 0)
            {
                return;
            }

            try
            {
                Clipboard.SetText(string.Join(Environment.NewLine, _logEntries.Select(e => e.FormattedText)));
            }
            catch (Exception ex)
            {
                AppendLog("Warn", $"Clipboard error: {ex.Message}");
            }
        }

        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            _logEntries.Clear();
        }

        /// <summary>
        /// Получить ссылку на ArduinoService (для использования в других панелях)
        /// </summary>
        internal ArduinoService? GetArduinoService() => _arduinoService;

        private void LogTextBlock_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.DataContext is LogEntry entry)
            {
                textBlock.Inlines.Clear();
                
                // Для моноширинных шрифтов WPF добавляет визуальные пробелы между Run элементами
                // Используем один Run для всей строки, чтобы избежать пробелов
                // Применяем цвет уровня ко всей строке (компромисс для компактности)
                string fullText = $"[{entry.Level}] {entry.FormattedTimestamp}: {entry.Message}";
                textBlock.Inlines.Add(new Run(fullText) { Foreground = entry.LevelBrush });
            }
        }

        private void AppendLog(string level, string message)
        {
            // Записываем в файл
            Utils.FileLogger.WriteLog(level, message);

            Dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now;
                var entry = new LogEntry
                {
                    Level = level,
                    Message = message,
                    Timestamp = timestamp,
                    FormattedTimestamp = timestamp.ToString("dd.MM.yyyy HH:mm:ss"),
                    FormattedText = $"[{level}] {timestamp:dd.MM.yyyy HH:mm:ss}: {message}"
                };
                
                _logEntries.Add(entry);

                while (_logEntries.Count > MaxLogEntries)
                {
                    _logEntries.RemoveAt(0);
                }

                if (LogListBox.Items.Count > 0)
                {
                    LogListBox.ScrollIntoView(LogListBox.Items[^1]);
                }

                if (LogTabListBox != null && LogTabListBox.Items.Count > 0)
                {
                    LogTabListBox.ScrollIntoView(LogTabListBox.Items[^1]);
                }
            });
        }

        private void SpdInfoPanel_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void ShowProgress(string text, bool isIndeterminate = false)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressText.Text = text;
                ProgressPanel.IsEnabled = true;
                ProgressPanel.Opacity = 1;
                ProgressSeparator.Opacity = 1;
                StatusProgressBar.Opacity = 1;

                if (isIndeterminate)
                {
                    StatusProgressBar.IsIndeterminate = true;
                }
                else
                {
                    StatusProgressBar.IsIndeterminate = false;
                    StatusProgressBar.Value = 0;
                }
            });
        }

        private void HideProgress()
        {
            Dispatcher.Invoke(() =>
            {
                StatusProgressBar.IsIndeterminate = false;
                StatusProgressBar.Value = 0;
                ProgressText.Text = "—";
                ProgressPanel.IsEnabled = false;
                ProgressPanel.Opacity = 0.5;
                ProgressSeparator.Opacity = 0.5;
                StatusProgressBar.Opacity = 0.5;
            });
        }

        private void UpdateProgress(int value, int maximum = 100)
        {
            Dispatcher.Invoke(() =>
            {
                if (StatusProgressBar.IsIndeterminate)
                {
                    StatusProgressBar.IsIndeterminate = false;
                }
                StatusProgressBar.Maximum = maximum;
                StatusProgressBar.Value = value;
            });
        }

        private void UpdateSpdEditPanel()
        {
            if (SpdEditPanel == null)
                return;

            if (HexEditor.DocumentLength == 0)
            {
                SpdEditPanel.Clear();
                return;
            }

            try
            {
                var forcedType = GetForcedMemoryType();
                // Read up to 1024 bytes for DDR5 support (DDR4 uses 512, DDR5 uses 1024)
                var data = HexEditor.ReadBytes(0, (int)Math.Min(HexEditor.DocumentLength, SpdConstants.DDR5_SPD_SIZE));
                if (data.Length >= 256)
                {
                    SpdEditPanel.LoadSpdData(data, forcedType);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating SPD Edit Panel: {ex.Message}");
            }
        }

        private void OnSpdEditChangesApplied(List<SpdEditPanel.ByteChange> changes)
        {
            try
            {
                // Применяем каждое изменение через ReplaceData для сохранения истории undo/redo
                foreach (var change in changes)
                {
                    if (change.Offset >= 0 && change.Offset < HexEditor.DocumentLength && 
                        change.NewData != null && change.NewData.Length > 0)
                    {
                        HexEditor.ReplaceData(change.Offset, change.NewData);
                    }
                }
                
                // Обновляем панель информации
                ScheduleSpdInfoUpdate(immediate: true);
                
                AppendLog("Info", $"SPD data modified via Edit Panel: {changes.Count} byte range(s) changed.");
            }
            catch (Exception ex)
            {
                AppendLog("Error", $"Error applying SPD Edit changes: {ex.Message}");
            }
        }
        
        private void OnHpeSmartMemoryChangesApplied(List<HpeSmartMemoryPanel.ByteChange> changes)
        {
            try
            {
                // Применяем каждое изменение через ReplaceData для сохранения истории undo/redo
                foreach (var change in changes)
                {
                    if (change.Offset >= 0 && change.Offset < HexEditor.DocumentLength && 
                        change.NewData != null && change.NewData.Length > 0)
                    {
                        HexEditor.ReplaceData(change.Offset, change.NewData);
                    }
                }
                
                // Обновляем панель информации
                ScheduleSpdInfoUpdate(immediate: true);
                
                // Формируем детальное сообщение о изменениях
                if (changes.Count == 3 && 
                    changes[0].Offset == 0x18E && changes[0].NewData.Length == 2 &&
                    changes[1].Offset == 0x190 && changes[1].NewData.Length == 2 &&
                    changes[2].Offset == 0x19C && changes[2].NewData.Length == 100)
                {
                    // Это обнуление информации о работе в сервере
                    AppendLog("Info", "Информация о работе в сервере успешно обнулена (байты 0x18E-0x18F, 0x190-0x191, 0x19C-0x1FF).");
                }
                else
                {
                    AppendLog("Info", $"SPD data modified via HPE SmartMemory Panel: {changes.Count} byte range(s) changed.");
                }
            }
            catch (Exception ex)
            {
                AppendLog("Error", $"Error applying HPE SmartMemory changes: {ex.Message}");
            }
        }

        /// <summary>
        /// Попытка автоматического чтения Sensor Register после чтения SPD
        /// </summary>
        private Task TryReadSensorRegistersAfterSpdRead()
        {
            return Task.Run(() =>
            {
                if (!_arduinoService.IsConnected)
                {
                    return;
                }

                // Получаем адреса из полного сканирования I2C
                var fullScanAddresses = _arduinoService.GetFullScanAddresses();
                if (fullScanAddresses == null || fullScanAddresses.Length == 0)
                {
                    return;
                }

                // Типичные адреса термодатчиков для HPE SmartMemory
                // Термодатчики обычно находятся в диапазоне 0x18-0x1F (JEDEC JC-42.4 стандарт)
                // Наиболее распространенные адреса: 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F
                byte[] sensorAddresses = { 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F };
                
                // Ищем адрес термодатчика среди найденных устройств
                byte? foundSensorAddress = null;
                foreach (var sensorAddr in sensorAddresses)
                {
                    if (fullScanAddresses.Contains(sensorAddr))
                    {
                        foundSensorAddress = sensorAddr;
                        break;
                    }
                }

                if (!foundSensorAddress.HasValue)
                {
                    return;
                }

                try
                {
                    var device = _arduinoService.GetActiveDevice();
                    if (device == null)
                    {
                        return;
                    }

                    Dispatcher.Invoke(() => AppendLog("Info", $"Найден термодатчик на адресе 0x{foundSensorAddress.Value:X2}, читаем регистры..."));

                    // Читаем регистры 6 и 7 (16-битные значения по стандарту JC-42.4)
                    ushort? reg6 = device.ReadSensorRegister(foundSensorAddress.Value, 6);
                    ushort? reg7 = device.ReadSensorRegister(foundSensorAddress.Value, 7);

                    if (reg6.HasValue && reg7.HasValue)
                    {
                        ushort reg6Value = reg6.Value;
                        ushort reg7Value = reg7.Value;
                        
                        Dispatcher.Invoke(() =>
                        {
                            AppendLog("Info", $"Прочитаны регистры термодатчика: Reg6=0x{reg6Value:X4}, Reg7=0x{reg7Value:X4}");
                            
                            // Обновляем HPE SmartMemory Panel с прочитанными регистрами
                            var spdData = HexEditor.ReadBytes(0, (int)Math.Min(HexEditor.DocumentLength, 512));
                            HpeSmartMemoryPanel?.UpdateSpdData(
                                spdData,
                                isArduinoMode: true,
                                sensorReg6: reg6Value,
                                sensorReg7: reg7Value);
                        });
                    }
                    else
                    {
                        Dispatcher.Invoke(() => AppendLog("Warn", "Не удалось прочитать регистры термодатчика"));
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => AppendLog("Error", $"Ошибка при чтении регистров термодатчика: {ex.Message}"));
                }
            });
        }

    }

}