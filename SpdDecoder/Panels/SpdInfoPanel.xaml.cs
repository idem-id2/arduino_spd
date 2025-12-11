using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HexEditor.SpdDecoder
{
    /// <summary>
    /// UserControl для отображения декодированной информации SPD
    /// </summary>
    public partial class SpdInfoPanel : UserControl
    {
        public static class FieldLabels
        {
            // Module Info Labels
            public const string Manufacturer = "Manufacturer";
            public const string PartNumber = "Part Number";
            public const string SerialNumber = "Serial Number";
            public const string SpecificPart = "Specific Part";
            public const string JedecDimmLabel = "JEDEC DIMM Label";
            public const string Architecture = "Architecture";
            public const string SpeedGrade = "Speed Grade";
            public const string Capacity = "Capacity";
            public const string Organization = "Organization";
            public const string ThermalSensor = "Thermal Sensor";
            public const string ModuleHeight = "Module Height";
            public const string ModuleThickness = "Module Thickness";
            public const string RegisterBufferManufacturer = "Register & Buffer Manufacturer";
            public const string RegisterManufacturer = "Register Manufacturer";
            public const string RegisterModel = "*Register Model";
            public const string RevisionRawCard = "Revision / Raw Card";
            public const string AddressMapping = "Address Mapping";
            public const string ManufacturingDate = "Manufacturing Date";
            public const string ManufacturingLocation = "Manufacturing Location";
            public const string Crc = "CRC";
            public const string CrcBlock0 = "Block 0 (0x00-0x7D)";
            public const string CrcBlock1 = "Block 1 (0x80-0xFD)";
            public const string RegisterModelNA = "N/A";

            // DRAM Info Labels
            public const string DramPartNumber = "*Part Number";
            public const string Package = "Package";
            public const string DieDensityCount = "*Die Density / Count";
            public const string Composition = "Composition";
            public const string InputClockFrequency = "Input Clock Frequency";
            public const string Addressing = "Addressing";
            public const string MinimumTimingDelays = "Minimum Timing Delays";
            public const string ReadLatenciesSupported = "Read Latencies Supported";
            public const string SupplyVoltage = "Supply Voltage";
            public const string SpdRevision = "SPD Revision";
            public const string XmpCertified = "XMP Certified";
            public const string XmpExtreme = "XMP Extreme";
            public const string XmpRevision = "XMP Revision";
        }

        // TODO: Timing table - реализовать позже
        // private readonly ObservableCollection<TimingRow> _timingRows = new();
        private List<InfoItem> _lastModuleInfo = new();
        private List<InfoItem> _lastDramInfo = new();
        
        // Кэш последних данных для избежания избыточных пересчетов
        // Используем ReadOnlyMemory для избежания копирования данных
        private ReadOnlyMemory<byte> _lastData;
        private int _lastDataHash;
        private ForcedMemoryType _lastForcedType = ForcedMemoryType.Auto;
        
        // Кэшированные ресурсы для оптимизации
        private Brush? _cachedSurfaceBrush;
        private Brush? _cachedSurfaceBorderBrush;
        private Brush? _cachedPrimaryTextBrush;
        private const double RowMinHeight = 28;
        private static readonly Thickness RowMargin = new Thickness(0, 4, 0, 4);
        
        // Кэшированные объекты для оптимизации (избегаем создания в циклах)
        private static readonly Thickness CachedBorderThickness = new Thickness(1);
        private static readonly Thickness BorderMargin = new Thickness(0, 0, 0, 12);
        private static readonly Thickness BorderPadding = new Thickness(12);
        private static readonly Thickness TitleMargin = new Thickness(0, 0, 0, 12);
        private static readonly Thickness RowGridMargin = new Thickness(0);
        private static readonly GridLength LabelColumnWidth = new GridLength(200);
        private static readonly GridLength ValueColumnWidth = new GridLength(1, GridUnitType.Star);

        private Brush? _cachedZebraEvenBrush;
        private Brush? _cachedZebraOddBrush;
        private Style? _cachedLabelStyle;
        private Style? _cachedValueStyle;
        private Style? _cachedSectionBlockStyle;
        private SolidColorBrush? _cachedHighlightForeground;
        
        // Кешированные кисти для CRC фона (избегаем создания новых объектов)
        private SolidColorBrush? _cachedCrcOkBrush;
        private SolidColorBrush? _cachedCrcBadBrush;
        
        // Простая задержка для очистки подсветки (позволяет контекстному меню открыться)
        private System.Windows.Threading.DispatcherTimer? _clearTimer;
        
        // Флаги состояния
        private bool _isScrolling;
        private bool _isContextMenuOpen;

        /// <summary>
        /// Событие для выделения диапазона байтов в HEX редакторе
        /// </summary>
        public event Action<IReadOnlyList<(long offset, int length)>>? HighlightBytes;

        /// <summary>
        /// Событие для снятия выделения диапазона байтов
        /// </summary>
        public event Action? ClearHighlight;

        public SpdInfoPanel()
        {
            InitializeComponent();
            CacheResources();
            ResetDisplay();
            
            // Инициализируем таймеры один раз
            InitializeTimers();
            
            PreviewMouseWheel += OnPreviewMouseWheel;
            Unloaded += OnUnloaded;
        }
        
        /// <summary>
        /// Инициализирует таймеры один раз (простота и производительность)
        /// </summary>
        private void InitializeTimers()
        {
            // Таймер для очистки подсветки и отслеживания прокрутки
            _clearTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _clearTimer.Tick += (_, _) =>
            {
                if (_isScrolling)
                {
                    _isScrolling = false;
                }
                else if (!_isContextMenuOpen)
                {
                    ClearHighlight?.Invoke();
                }
                _clearTimer.Stop();
            };
        }
        
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _clearTimer?.Stop();
        }
        
        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            _isScrolling = true;
            _clearTimer?.Stop();
            _clearTimer?.Start();
        }
        
        private void CacheResources()
        {
            _cachedSurfaceBrush = TryFindResource("SurfaceBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF4));
            _cachedSurfaceBorderBrush = TryFindResource("SurfaceBorderBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));
            _cachedPrimaryTextBrush = TryFindResource("PrimaryTextBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F));
            _cachedZebraEvenBrush = TryFindResource("ZebraEvenBrush") as Brush ?? new SolidColorBrush(Colors.White);
            _cachedZebraOddBrush = TryFindResource("ZebraOddBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
            _cachedLabelStyle = TryFindResource("SpdInfoLabelStyle") as Style;
            _cachedValueStyle = TryFindResource("SpdInfoValueStyle") as Style;
            _cachedSectionBlockStyle = TryFindResource("SectionBlockStyle") as Style;
            _cachedHighlightForeground = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));
        }

        public void Clear()
        {
            ResetDisplay();
        }

        public void UpdateSpdData(byte[] data, ForcedMemoryType forcedType = ForcedMemoryType.Auto)
        {
            if (data == null || data.Length < 256)
            {
                ResetDisplay();
                _lastData = default;
                _lastDataHash = 0;
                return;
            }

            // Быстрая проверка: если данные и forcedType не изменились, не пересчитываем
            // Учитываем forcedType, так как он влияет на результат парсинга
            int newHash = ComputeDataHash(data);
            if (!_lastData.IsEmpty && 
                _lastForcedType == forcedType &&
                _lastDataHash == newHash && 
                _lastData.Length == data.Length &&
                _lastData.Span.SequenceEqual(data))
            {
                return; // Данные не изменились, ничего не делаем
            }

            try
            {
                var parser = new SpdParser(data);
                parser.Parse(forcedType);

                _lastModuleInfo = parser.ModuleInfo;
                _lastDramInfo = parser.DramInfo;
                
                BuildDynamicUI();
                // TODO: Timing table - реализовать позже
                // UpdateTimingRows(parser.TimingRows);
                
                // Сохраняем данные, hash и forcedType для следующей проверки
                // Используем копию данных для кэша (необходимо, так как исходный массив может измениться)
                _lastData = new ReadOnlyMemory<byte>((byte[])data.Clone());
                _lastDataHash = newHash;
                _lastForcedType = forcedType;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing SPD: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }

                ResetDisplay();
                _lastData = default;
                _lastDataHash = 0;
            }
        }
        
        /// <summary>
        /// Вычисление hash для всех данных (оптимизированная версия с использованием FNV-1a)
        /// Hash вычисляется для всех данных, чтобы избежать ложных совпадений
        /// </summary>
        private static int ComputeDataHash(byte[] data)
        {
            if (data == null || data.Length == 0) return 0;
            
            // Используем FNV-1a hash для лучшего распределения
            // Это быстрее чем полное сравнение, но все еще надежно
            const uint FNV_OFFSET_BASIS = 2166136261u;
            const uint FNV_PRIME = 16777619u;
            
            uint hash = FNV_OFFSET_BASIS;
            for (int i = 0; i < data.Length; i++)
            {
                hash ^= data[i];
                hash *= FNV_PRIME;
            }
            
            return unchecked((int)hash);
        }

        public class InfoItem
        {
            public string Label { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public bool IsHighlighted { get; set; }
            public long? ByteOffset { get; set; }
            public int? ByteLength { get; set; }
            public List<(long offset, int length)> ByteRanges { get; set; } = new();
        }

        public class TimingRow
        {
            public string Frequency { get; set; } = string.Empty;
            public string CAS { get; set; } = string.Empty;
            public string RCD { get; set; } = string.Empty;
            public string RP { get; set; } = string.Empty;
            public string RAS { get; set; } = string.Empty;
            public string RC { get; set; } = string.Empty;
            public string FAW { get; set; } = string.Empty;
            public string RRDS { get; set; } = string.Empty;
            public string RRDL { get; set; } = string.Empty;
            public string WR { get; set; } = string.Empty;
            public string WTRS { get; set; } = string.Empty;
        }

        private void CopyAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sb = new StringBuilder();

                sb.AppendLine("=== MEMORY MODULE ===");
                foreach (var item in _lastModuleInfo)
                {
                    sb.AppendLine($"{item.Label}: {item.Value}");
                }
                sb.AppendLine();

                sb.AppendLine("=== DRAM COMPONENTS ===");
                foreach (var item in _lastDramInfo)
                {
                    sb.AppendLine($"{item.Label}: {item.Value}");
                }
                sb.AppendLine();

                // TODO: Timing table - реализовать позже
                // if (_timingRows.Count > 0)
                // {
                //     sb.AppendLine("=== TIMING TABLE ===");
                //     sb.AppendLine("Frequency\tCAS\tRCD\tRP\tRAS\tRC\tFAW\tRRDS\tRRDL\tWR\tWTRS");
                //     foreach (var row in _timingRows)
                //     {
                //         sb.AppendLine($"{row.Frequency}\t{row.CAS}\t{row.RCD}\t{row.RP}\t{row.RAS}\t{row.RC}\t{row.FAW}\t{row.RRDS}\t{row.RRDL}\t{row.WR}\t{row.WTRS}");
                //     }
                // }

                string text = sb.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    Clipboard.SetText(text);
                    MessageBox.Show("All SPD data has been copied to clipboard.", "Copy Successful",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copying data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetDisplay()
        {
            _lastModuleInfo = new List<InfoItem>();
            _lastDramInfo = new List<InfoItem>();
            BuildDynamicUI();
            // TODO: Timing table - реализовать позже
            // UpdateTimingRows(CreateDefaultTimings());
        }

        private void BuildDynamicUI()
        {
            // Заполняем статические поля Module Info
            UpdateModuleInfoFields();

            // Заполняем статические поля DRAM Info
            UpdateDramInfoFields();
        }

        private void UpdateModuleInfoFields()
        {
            if (_lastModuleInfo == null || _lastModuleInfo.Count == 0)
            {
                // Очищаем все поля
                ClearModuleInfoFields();
                return;
            }

            // Создаем словарь для быстрого поиска по Label
            var moduleInfoDict = _lastModuleInfo.ToDictionary(item => item.Label, item => item);

            // Заполняем каждое поле
            UpdateField("ModuleInfo_Manufacturer", moduleInfoDict, FieldLabels.Manufacturer);
            UpdateField("ModuleInfo_PartNumber", moduleInfoDict, FieldLabels.PartNumber);
            UpdateField("ModuleInfo_SerialNumber", moduleInfoDict, FieldLabels.SerialNumber);
            UpdateField("ModuleInfo_SpecificPart", moduleInfoDict, FieldLabels.SpecificPart);
            UpdateField("ModuleInfo_JedecDimmLabel", moduleInfoDict, FieldLabels.JedecDimmLabel, isHighlighted: true);
            UpdateField("ModuleInfo_Architecture", moduleInfoDict, FieldLabels.Architecture);
            UpdateField("ModuleInfo_SpeedGrade", moduleInfoDict, FieldLabels.SpeedGrade);
            UpdateField("ModuleInfo_Capacity", moduleInfoDict, FieldLabels.Capacity);
            UpdateField("ModuleInfo_Organization", moduleInfoDict, FieldLabels.Organization);
            UpdateField("ModuleInfo_ThermalSensor", moduleInfoDict, FieldLabels.ThermalSensor);
            UpdateField("ModuleInfo_ModuleHeight", moduleInfoDict, FieldLabels.ModuleHeight);
            UpdateField("ModuleInfo_ModuleThickness", moduleInfoDict, FieldLabels.ModuleThickness);
            UpdateField("ModuleInfo_RegisterBufferManufacturer", moduleInfoDict, FieldLabels.RegisterBufferManufacturer);
            UpdateField("ModuleInfo_RegisterModel", moduleInfoDict, FieldLabels.RegisterModel);
            UpdateField("ModuleInfo_RevisionRawCard", moduleInfoDict, FieldLabels.RevisionRawCard);
            UpdateField("ModuleInfo_AddressMapping", moduleInfoDict, FieldLabels.AddressMapping);
            UpdateField("ModuleInfo_ManufacturingDate", moduleInfoDict, FieldLabels.ManufacturingDate);
            UpdateField("ModuleInfo_ManufacturingLocation", moduleInfoDict, FieldLabels.ManufacturingLocation);
            UpdateField("ModuleInfo_Crc", moduleInfoDict, FieldLabels.Crc, updateCrcBackground: true);
            UpdateField("ModuleInfo_CrcBlock0", moduleInfoDict, FieldLabels.CrcBlock0);
            UpdateField("ModuleInfo_CrcBlock1", moduleInfoDict, FieldLabels.CrcBlock1);
        }

        private void UpdateDramInfoFields()
        {
            if (_lastDramInfo == null || _lastDramInfo.Count == 0)
            {
                // Очищаем все поля
                ClearDramInfoFields();
                return;
            }

            // Создаем словарь для быстрого поиска по Label
            var dramInfoDict = _lastDramInfo.ToDictionary(item => item.Label, item => item);

            // Заполняем каждое поле
            UpdateField("DramInfo_Manufacturer", dramInfoDict, FieldLabels.Manufacturer);
            UpdateField("DramInfo_DramPartNumber", dramInfoDict, FieldLabels.DramPartNumber);
            UpdateField("DramInfo_Package", dramInfoDict, FieldLabels.Package);
            UpdateField("DramInfo_DieDensityCount", dramInfoDict, FieldLabels.DieDensityCount);
            UpdateField("DramInfo_Composition", dramInfoDict, FieldLabels.Composition);
            UpdateField("DramInfo_InputClockFrequency", dramInfoDict, FieldLabels.InputClockFrequency);
            UpdateField("DramInfo_Addressing", dramInfoDict, FieldLabels.Addressing);
            UpdateField("DramInfo_MinimumTimingDelays", dramInfoDict, FieldLabels.MinimumTimingDelays);
            UpdateField("DramInfo_ReadLatenciesSupported", dramInfoDict, FieldLabels.ReadLatenciesSupported);
            UpdateField("DramInfo_SupplyVoltage", dramInfoDict, FieldLabels.SupplyVoltage);
            UpdateField("DramInfo_SpdRevision", dramInfoDict, FieldLabels.SpdRevision);
            UpdateField("DramInfo_XmpCertified", dramInfoDict, FieldLabels.XmpCertified);
            UpdateField("DramInfo_XmpExtreme", dramInfoDict, FieldLabels.XmpExtreme);
            UpdateField("DramInfo_XmpRevision", dramInfoDict, FieldLabels.XmpRevision);
        }

        private void UpdateField(string fieldNamePrefix, Dictionary<string, InfoItem> infoDict, string label, bool isHighlighted = false, bool updateCrcBackground = false)
        {
            var valueControl = FindName($"{fieldNamePrefix}_Value") as TextBox;
            if (valueControl == null) return;

            var labelControl = FindName($"{fieldNamePrefix}_Label") as TextBox;
            
            // Находим родительский Grid для изменения фона (для CRC)
            var gridControl = valueControl.Parent as Grid;
            
            if (infoDict.TryGetValue(label, out var item))
            {
                string valueText = string.IsNullOrWhiteSpace(item.Value) ? "—" : item.Value;
                valueControl.Text = valueText;
                valueControl.FontWeight = (isHighlighted || item.IsHighlighted) ? FontWeights.SemiBold : FontWeights.Normal;
                valueControl.Foreground = (isHighlighted || item.IsHighlighted) 
                    ? (_cachedHighlightForeground ?? _cachedPrimaryTextBrush) 
                    : _cachedPrimaryTextBrush;

                // Обновляем цвет фона для CRC строки (используем кешированные кисти)
                if (updateCrcBackground && gridControl != null)
                {
                    if (valueText.Contains("OK", StringComparison.OrdinalIgnoreCase))
                    {
                        // Светло-зелёный для OK (кешируем кисть для избежания аллокаций)
                        if (_cachedCrcOkBrush == null)
                        {
                            _cachedCrcOkBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x90, 0xEE, 0x90));
                            _cachedCrcOkBrush.Freeze(); // Замораживаем для потокобезопасности и производительности
                        }
                        gridControl.Background = _cachedCrcOkBrush;
                    }
                    else if (valueText.Contains("BAD", StringComparison.OrdinalIgnoreCase))
                    {
                        // Светло-красный для BAD (кешируем кисть для избежания аллокаций)
                        if (_cachedCrcBadBrush == null)
                        {
                            _cachedCrcBadBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0x6B, 0x6B));
                            _cachedCrcBadBrush.Freeze(); // Замораживаем для потокобезопасности и производительности
                        }
                        gridControl.Background = _cachedCrcBadBrush;
                    }
                    else
                    {
                        // Обычный цвет (zebra)
                        gridControl.Background = _cachedZebraEvenBrush ?? new SolidColorBrush(Colors.White);
                    }
                }

                // Сохраняем данные для контекстного меню и подсветки
                // Важно: получаем ranges независимо от того, определено ли значение
                var ranges = GetHighlightRanges(item);
                var rowData = new RowData(item.Label, item.Value, ranges);
                valueControl.Tag = rowData;
                
                // Устанавливаем обработчики событий для подсветки на Value
                // Подсветка работает, если есть ranges, даже если значение не определено (Value = "—")
                valueControl.MouseEnter -= OnValueControlMouseEnter;
                valueControl.MouseLeave -= OnValueControlMouseLeave;
                valueControl.PreviewMouseRightButtonDown -= OnValueControlPreviewMouseRightButtonDown;
                valueControl.ContextMenuOpening -= OnContextMenuOpening;
                
                if (ranges != null)
                {
                    // Если есть ranges, устанавливаем обработчики для подсветки
                    valueControl.MouseEnter += OnValueControlMouseEnter;
                    valueControl.MouseLeave += OnValueControlMouseLeave;
                    valueControl.PreviewMouseRightButtonDown += OnValueControlPreviewMouseRightButtonDown;
                }
                // Контекстное меню всегда доступно
                valueControl.ContextMenuOpening += OnContextMenuOpening;
                
                // Устанавливаем обработчики событий для подсветки на Label тоже
                if (labelControl != null)
                {
                    labelControl.Tag = rowData;
                    
                    labelControl.MouseEnter -= OnValueControlMouseEnter;
                    labelControl.MouseLeave -= OnValueControlMouseLeave;
                    labelControl.PreviewMouseRightButtonDown -= OnValueControlPreviewMouseRightButtonDown;
                    labelControl.ContextMenuOpening -= OnContextMenuOpening;
                    
                    if (ranges != null)
                    {
                        // Если есть ranges, устанавливаем обработчики для подсветки
                        labelControl.MouseEnter += OnValueControlMouseEnter;
                        labelControl.MouseLeave += OnValueControlMouseLeave;
                        labelControl.PreviewMouseRightButtonDown += OnValueControlPreviewMouseRightButtonDown;
                    }
                    // Контекстное меню всегда доступно
                    labelControl.ContextMenuOpening += OnContextMenuOpening;
                }
            }
            else
            {
                valueControl.Text = "—";
                valueControl.FontWeight = FontWeights.Normal;
                valueControl.Foreground = _cachedPrimaryTextBrush;
                valueControl.Tag = null;
                
                // Сбрасываем фон для CRC, если поле пустое
                if (updateCrcBackground && gridControl != null)
                {
                    gridControl.Background = _cachedZebraEvenBrush ?? new SolidColorBrush(Colors.White);
                }
                
                // Удаляем обработчики с Value
                valueControl.MouseEnter -= OnValueControlMouseEnter;
                valueControl.MouseLeave -= OnValueControlMouseLeave;
                valueControl.PreviewMouseRightButtonDown -= OnValueControlPreviewMouseRightButtonDown;
                valueControl.ContextMenuOpening -= OnContextMenuOpening;
                
                // Удаляем обработчики с Label
                if (labelControl != null)
                {
                    labelControl.Tag = null;
                    labelControl.MouseEnter -= OnValueControlMouseEnter;
                    labelControl.MouseLeave -= OnValueControlMouseLeave;
                    labelControl.PreviewMouseRightButtonDown -= OnValueControlPreviewMouseRightButtonDown;
                    labelControl.ContextMenuOpening -= OnContextMenuOpening;
                }
            }
        }

        private void ClearModuleInfoFields()
        {
            var fieldNames = new[]
            {
                "ModuleInfo_Manufacturer", "ModuleInfo_PartNumber", "ModuleInfo_SerialNumber",
                "ModuleInfo_SpecificPart", "ModuleInfo_JedecDimmLabel", "ModuleInfo_Architecture",
                "ModuleInfo_SpeedGrade", "ModuleInfo_Capacity", "ModuleInfo_Organization",
                "ModuleInfo_ThermalSensor", "ModuleInfo_ModuleHeight", "ModuleInfo_ModuleThickness",
                "ModuleInfo_RegisterBufferManufacturer", "ModuleInfo_RegisterModel",
                "ModuleInfo_RevisionRawCard", "ModuleInfo_AddressMapping", "ModuleInfo_ManufacturingDate",
                "ModuleInfo_ManufacturingLocation", "ModuleInfo_Crc", "ModuleInfo_CrcBlock0", "ModuleInfo_CrcBlock1"
            };

            foreach (var fieldName in fieldNames)
            {
                var valueControl = FindName($"{fieldName}_Value") as TextBox;
                if (valueControl != null)
                {
                    valueControl.Text = "—";
                    valueControl.FontWeight = FontWeights.Normal;
                    valueControl.Foreground = _cachedPrimaryTextBrush;
                    valueControl.Tag = null;
                    
                    // Сбрасываем фон для CRC строки
                    if (fieldName == "ModuleInfo_Crc")
                    {
                        var gridControl = valueControl.Parent as Grid;
                        if (gridControl != null)
                        {
                            gridControl.Background = _cachedZebraEvenBrush ?? new SolidColorBrush(Colors.White);
                        }
                    }
                    
                    valueControl.MouseEnter -= OnValueControlMouseEnter;
                    valueControl.MouseLeave -= OnValueControlMouseLeave;
                    valueControl.PreviewMouseRightButtonDown -= OnValueControlPreviewMouseRightButtonDown;
                    valueControl.ContextMenuOpening -= OnContextMenuOpening;
                }
            }
        }

        private void ClearDramInfoFields()
        {
            var fieldNames = new[]
            {
                "DramInfo_Manufacturer", "DramInfo_DramPartNumber", "DramInfo_Package",
                "DramInfo_DieDensityCount", "DramInfo_Composition", "DramInfo_InputClockFrequency",
                "DramInfo_Addressing", "DramInfo_MinimumTimingDelays", "DramInfo_ReadLatenciesSupported",
                "DramInfo_SupplyVoltage", "DramInfo_SpdRevision", "DramInfo_XmpCertified",
                "DramInfo_XmpExtreme", "DramInfo_XmpRevision"
            };

            foreach (var fieldName in fieldNames)
            {
                var valueControl = FindName($"{fieldName}_Value") as TextBox;
                var labelControl = FindName($"{fieldName}_Label") as TextBox;
                
                if (valueControl != null)
                {
                    valueControl.Text = "—";
                    valueControl.FontWeight = FontWeights.Normal;
                    valueControl.Foreground = _cachedPrimaryTextBrush;
                    valueControl.Tag = null;
                    
                    // Удаляем обработчики с Value
                    valueControl.MouseEnter -= OnValueControlMouseEnter;
                    valueControl.MouseLeave -= OnValueControlMouseLeave;
                    valueControl.PreviewMouseRightButtonDown -= OnValueControlPreviewMouseRightButtonDown;
                    valueControl.ContextMenuOpening -= OnContextMenuOpening;
                }
                
                // Удаляем обработчики с Label
                if (labelControl != null)
                {
                    labelControl.Tag = null;
                    labelControl.MouseEnter -= OnValueControlMouseEnter;
                    labelControl.MouseLeave -= OnValueControlMouseLeave;
                    labelControl.PreviewMouseRightButtonDown -= OnValueControlPreviewMouseRightButtonDown;
                    labelControl.ContextMenuOpening -= OnContextMenuOpening;
                }
            }
        }

        private void OnValueControlMouseEnter(object sender, MouseEventArgs e)
        {
            // Быстрая проверка: если прокрутка активна, не обрабатываем событие
            if (_isScrolling) return;
            
            // Получаем данные из Tag (быстрая операция, без аллокаций)
            var rowData = (sender as FrameworkElement)?.Tag as RowData;
            if (rowData?.Ranges == null) return;
            
            // Останавливаем таймер очистки для предотвращения мерцания
            _clearTimer?.Stop();
            
            // Вызываем подсветку сразу для мгновенной реакции
            // Throttling уже есть в MainWindow (10ms), поэтому дополнительная задержка не нужна
            HighlightBytes?.Invoke(rowData.Ranges);
        }

        private void OnValueControlMouseLeave(object sender, MouseEventArgs e)
        {
            if (_isScrolling || _isContextMenuOpen) return;
            
            _clearTimer?.Stop();
            _clearTimer?.Start();
        }

        private void OnValueControlPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isContextMenuOpen = true;
        }

        // TODO: Timing table - реализовать позже
        // private void UpdateTimingRows(IEnumerable<TimingRow> rows)
        // {
        //     _timingRows.Clear();
        //     foreach (var row in rows)
        //     {
        //         _timingRows.Add(row);
        //     }
        // }




        /// <summary>
        /// Получает диапазоны байтов для подсветки из InfoItem.
        /// Важно: работает независимо от того, определено ли значение (Value).
        /// Если есть ByteOffset и ByteLength, подсветка будет работать даже если Value = "—".
        /// </summary>
        private IReadOnlyList<(long offset, int length)>? GetHighlightRanges(InfoItem item)
        {
            if (item.ByteRanges != null && item.ByteRanges.Count > 0)
            {
                return item.ByteRanges;
            }

            // Проверяем ByteOffset и ByteLength независимо от значения
            // Это позволяет подсветке работать даже для неопределенных значений
            if (item.ByteOffset.HasValue && item.ByteLength.HasValue)
            {
                return new List<(long offset, int length)>
                {
                    (item.ByteOffset.Value, item.ByteLength.Value)
                };
            }

            return null;
        }

        // Старые обработчики OnRowGrid* удалены - теперь используем OnValueControl* для статических полей
        
        /// <summary>
        /// Вспомогательный класс для хранения данных строки в Tag (оптимизация производительности)
        /// </summary>
        private sealed class RowData
        {
            public string Label { get; }
            public string Value { get; }
            public IReadOnlyList<(long offset, int length)>? Ranges { get; }
            
            public RowData(string label, string value, IReadOnlyList<(long offset, int length)>? ranges)
            {
                Label = label;
                Value = value;
                Ranges = ranges;
            }
        }
        
        /// <summary>
        /// Обработчик открытия контекстного меню - создает меню динамически (lazy initialization)
        /// Это критично для производительности: вместо создания 150+ меню при построении UI,
        /// создаем меню только при необходимости (при открытии)
        /// </summary>
        private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null)
            {
                e.Handled = true;
                return;
            }
            
            var rowData = element.Tag as RowData;
            if (rowData == null)
            {
                // Fallback для старых элементов с tuple в Tag
                if (element.Tag is (string label, string value))
                {
                    element.ContextMenu = CreateContextMenu(label, value);
                    return;
                }
                
                e.Handled = true;
                return;
            }
            
            // Создаем меню только при открытии
            element.ContextMenu = CreateContextMenu(rowData.Label, rowData.Value);
        }
        
        /// <summary>
        /// Создает контекстное меню для копирования названия поля и значения
        /// </summary>
        private ContextMenu CreateContextMenu(string label, string value)
        {
            var contextMenu = new ContextMenu();
            
            // Пункт для копирования названия поля
            var copyLabelMenuItem = new MenuItem
            {
                Header = "Copy Label"
            };
            copyLabelMenuItem.Click += (s, e) => CopyToClipboard(label);
            contextMenu.Items.Add(copyLabelMenuItem);
            
            // Пункт для копирования значения
            var copyValueMenuItem = new MenuItem
            {
                Header = "Copy Value"
            };
            var valueToCopy = string.IsNullOrWhiteSpace(value) ? "—" : value;
            copyValueMenuItem.Click += (s, e) => CopyToClipboard(valueToCopy);
            contextMenu.Items.Add(copyValueMenuItem);
            
            // Пункт для копирования "Label: Value"
            var copyBothMenuItem = new MenuItem
            {
                Header = "Copy Label: Value"
            };
            var bothText = $"{label}: {valueToCopy}";
            copyBothMenuItem.Click += (s, e) => CopyToClipboard(bothText);
            contextMenu.Items.Add(copyBothMenuItem);
            
            contextMenu.Opened += (s, e) =>
            {
                _isContextMenuOpen = true;
                _clearTimer?.Stop();
            };
            contextMenu.Closed += (s, e) =>
            {
                _isContextMenuOpen = false;
                _clearTimer?.Stop();
                _clearTimer?.Start();
            };
            
            return contextMenu;
        }
        
        
        /// <summary>
        /// Копирует текст в буфер обмена
        /// </summary>
        private void CopyToClipboard(string text)
        {
            try
            {
                if (!string.IsNullOrEmpty(text))
                {
                    Clipboard.SetText(text);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error copying to clipboard: {ex.Message}");
            }
        }

    }
}

