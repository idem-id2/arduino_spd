using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HexEditor.SpdDecoder
{
    /// <summary>
    /// UserControl для редактирования SPD данных по стандарту JEDEC
    /// </summary>
    public partial class SpdEditPanel : UserControl
    {
        private byte[]? _currentSpdData;
        private ISpdEditor? _currentEditor;
        private readonly Dictionary<string, FrameworkElement> _dynamicFieldControls = new();
        private readonly System.Windows.Threading.DispatcherTimer _applyTimer;
        private bool _isApplyingChanges = false;
        private readonly Dictionary<string, string> _categoryTitles = new()
        {
            { "MemoryModule", "MEMORY MODULE" },
            { "DensityDie", "DENSITY & DIE" },
            { "DramComponents", "DRAM COMPONENTS" },
            { "Timing", "TIMING PARAMETERS" },
            { "ModuleConfig", "MODULE CONFIGURATION" },
            { "XMP", "XMP PROFILES" }
        };

        /// <summary>
        /// Структура для передачи изменений байтов
        /// </summary>
        public class ByteChange
        {
            public int Offset { get; set; }
            public byte[] NewData { get; set; } = Array.Empty<byte>();
        }

        /// <summary>
        /// Событие, возникающее при применении изменений
        /// </summary>
        public event Action<List<ByteChange>>? ChangesApplied;

        private const double RowMinHeight = 28;
        private static readonly Thickness RowMargin = new(0, 4, 0, 4);
        private static double TextControlMinHeight => Math.Max(18, RowMinHeight - RowMargin.Top - RowMargin.Bottom);
        private static double ComboBoxMinHeight => Math.Min(RowMinHeight - 2, TextControlMinHeight + 4);

        public SpdEditPanel()
        {
            InitializeComponent();
            
            // Таймер для debouncing изменений в TextBox
            _applyTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500) // 500ms задержка для TextBox
            };
            _applyTimer.Tick += (s, e) =>
            {
                _applyTimer.Stop();
                ApplyChangesImmediately();
            };
        }

        /// <summary>
        /// Вспомогательный метод для безопасного получения ресурсов из Application.Resources
        /// </summary>
        private T? GetResource<T>(string key) where T : class
        {
            return TryFindResource(key) as T 
                ?? Application.Current?.TryFindResource(key) as T;
        }

        /// <summary>
        /// Загружает SPD данные для редактирования
        /// </summary>
        public void LoadSpdData(byte[] data, ForcedMemoryType forcedType = ForcedMemoryType.Auto)
        {
            if (data == null || data.Length < 256)
            {
                Clear();
                return;
            }

            _currentSpdData = new byte[data.Length];
            Array.Copy(data, _currentSpdData, data.Length);

            // Создаем редактор на основе типа памяти (с учетом принудительного выбора)
            _currentEditor = SpdTypeDetector.CreateEditor(data, forcedType);
            if (_currentEditor == null)
            {
                Clear();
                return;
            }

            _currentEditor.LoadData(_currentSpdData);

            BuildDynamicUI();
        }

        /// <summary>
        /// Очищает панель редактирования
        /// </summary>
        public void Clear()
        {
            _currentSpdData = null;
            _currentEditor = null;
            _dynamicFieldControls.Clear();
            
            // Очищаем динамические контейнеры
            ClearDynamicContainers();
        }

        private void ClearDynamicContainers()
        {
            // Очищаем все динамические контейнеры
            if (DynamicFieldsContainer != null)
            {
                DynamicFieldsContainer.Children.Clear();
            }
            
            _dynamicFieldControls.Clear();
        }

        private void BuildDynamicUI()
        {
            if (_currentEditor == null)
                return;

            ClearDynamicContainers();

            var fields = _currentEditor.GetEditFields();
            if (fields == null || fields.Count == 0)
                return;

            // Группируем поля по категориям
            var fieldsByCategory = fields
                .GroupBy(f => f.Category ?? "Common")
                .OrderBy(g => GetCategoryOrder(g.Key))
                .ToList();

            foreach (var categoryGroup in fieldsByCategory)
            {
                string category = categoryGroup.Key;
                var categoryFields = categoryGroup.ToList();

                if (categoryFields.Count == 0)
                    continue;

                // Создаем секцию для категории
                var section = CreateCategorySection(category, categoryFields);
                if (DynamicFieldsContainer != null)
                {
                    DynamicFieldsContainer.Children.Add(section);
                }
            }
        }

        private int GetCategoryOrder(string category)
        {
            return category switch
            {
                "MemoryModule" => 1,
                "DensityDie" => 2,
                "DramComponents" => 3,
                "Timing" => 4,
                "ModuleConfig" => 5,
                "XMP" => 6,
                _ => 99
            };
        }

        private FrameworkElement CreateCategorySection(string category, List<EditField> fields)
        {
            var border = new Border();
            var sectionStyle = GetResource<Style>("SectionBlockStyle");
            if (sectionStyle != null)
            {
                border.Style = sectionStyle;
            }
            else
            {
                // Fallback если стиль не найден
                border.Background = GetResource<Brush>("ZebraEvenBrush") ?? new SolidColorBrush(Colors.White);
                border.BorderBrush = GetResource<Brush>("SurfaceBorderBrush") ?? new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));
                border.BorderThickness = new Thickness(1);
                border.Margin = new Thickness(0, 0, 0, 12);
                border.Padding = new Thickness(12);
            }

            var stackPanel = new StackPanel();

            // Заголовок секции
            string titleText = _categoryTitles.TryGetValue(category, out string? categoryTitle) ? categoryTitle : category;
            var title = new TextBlock
            {
                Text = titleText,
                Style = GetResource<Style>("CardTitleStyle")
            };
            if (title.Style == null)
            {
                // Fallback если стиль не найден
                title.FontWeight = FontWeights.SemiBold;
                title.FontSize = 14;
                title.Foreground = GetResource<Brush>("PrimaryTextBrush") ?? new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F));
                title.Margin = new Thickness(0, 0, 0, 12);
            }
            stackPanel.Children.Add(title);

            var zebraEven = GetResource<Brush>("ZebraEvenBrush") ?? new SolidColorBrush(Colors.White);
            var zebraOdd = GetResource<Brush>("ZebraOddBrush") ?? new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
            double controlMinHeight = TextControlMinHeight;
            double comboHeight = ComboBoxMinHeight;

            for (int rowIndex = 0; rowIndex < fields.Count; rowIndex++)
            {
                var field = fields[rowIndex];
                
                // Специальная обработка для Manufacturing Date (ModuleYear + ModuleWeek в одной строке)
                if (field.Id == "ModuleYear" && rowIndex + 1 < fields.Count && fields[rowIndex + 1].Id == "ModuleWeek")
                {
                    var weekField = fields[rowIndex + 1];
                    rowIndex++; // Пропускаем следующее поле, так как обработаем его здесь
                    
                    var rowGrid = new Grid
                    {
                        MinHeight = RowMinHeight,
                        Margin = new Thickness(0),
                        Background = (rowIndex - 1) % 2 == 0 ? zebraEven : zebraOdd
                    };
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) }); // Spacer
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    // Label для Manufacturing Date
                    var label = new TextBox
                    {
                        Text = field.Label, // "Manufacturing Date"
                        Style = GetResource<Style>("EditFieldLabelStyle"),
                        Margin = RowMargin
                    };
                    Grid.SetColumn(label, 0);
                    rowGrid.Children.Add(label);

                    // Year ComboBox
                    var yearControl = CreateFieldControl(field);
                    if (yearControl is FrameworkElement yearFe)
                    {
                        yearFe.Margin = RowMargin;
                        yearFe.VerticalAlignment = VerticalAlignment.Center;
                        yearFe.MinHeight = controlMinHeight;
                        if (yearFe is ComboBox yearCombo)
                        {
                            yearCombo.MinHeight = comboHeight;
                            yearCombo.MaxHeight = RowMinHeight;
                            yearCombo.VerticalContentAlignment = VerticalAlignment.Center;
                            yearCombo.Padding = new Thickness(4, 0, 4, 0);
                            // Автоматическое применение при изменении выбора
                            yearCombo.SelectionChanged += (s, e) => ApplyChangesImmediately();
                        }
                    }
                    Grid.SetColumn(yearControl, 1);
                    rowGrid.Children.Add(yearControl);
                    _dynamicFieldControls[field.Id] = yearControl;

                    // Week ComboBox
                    var weekControl = CreateFieldControl(weekField);
                    if (weekControl is FrameworkElement weekFe)
                    {
                        weekFe.Margin = RowMargin;
                        weekFe.VerticalAlignment = VerticalAlignment.Center;
                        weekFe.MinHeight = controlMinHeight;
                        if (weekFe is ComboBox weekCombo)
                        {
                            weekCombo.MinHeight = comboHeight;
                            weekCombo.MaxHeight = RowMinHeight;
                            weekCombo.VerticalContentAlignment = VerticalAlignment.Center;
                            weekCombo.Padding = new Thickness(4, 0, 4, 0);
                            // Автоматическое применение при изменении выбора
                            weekCombo.SelectionChanged += (s, e) => ApplyChangesImmediately();
                        }
                    }
                    Grid.SetColumn(weekControl, 3);
                    rowGrid.Children.Add(weekControl);
                    _dynamicFieldControls[weekField.Id] = weekControl;

                    stackPanel.Children.Add(rowGrid);
                    continue;
                }

                // Обычная обработка для остальных полей
                var normalRowGrid = new Grid
                {
                    MinHeight = RowMinHeight,
                    Margin = new Thickness(0),
                    Background = rowIndex % 2 == 0 ? zebraEven : zebraOdd
                };
                normalRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                normalRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Используем TextBox вместо TextBlock для возможности выделения текста
                var normalLabel = new TextBox
                {
                    Text = field.Label,
                    Style = GetResource<Style>("EditFieldLabelStyle"),
                    Margin = RowMargin
                };
                Grid.SetColumn(normalLabel, 0);
                normalRowGrid.Children.Add(normalLabel);

                var control = CreateFieldControl(field);
                if (control != null)
                {
                    if (control is FrameworkElement fe)
                    {
                        fe.Margin = RowMargin;
                        fe.VerticalAlignment = VerticalAlignment.Center;
                        fe.MinHeight = controlMinHeight;

                        if (fe is ComboBox comboBox)
                        {
                            comboBox.MinHeight = comboHeight;
                            comboBox.MaxHeight = RowMinHeight;
                            comboBox.VerticalContentAlignment = VerticalAlignment.Center;
                            comboBox.Padding = new Thickness(4, 0, 4, 0);
                        }
                    }
                    Grid.SetColumn(control, 1);
                    normalRowGrid.Children.Add(control);
                    _dynamicFieldControls[field.Id] = control;
                }

                stackPanel.Children.Add(normalRowGrid);
            }

            border.Child = stackPanel;
            return border;
        }

        private FrameworkElement? CreateFieldControl(EditField field)
        {
            return field.Type switch
            {
                EditFieldType.TextBox => CreateTextBox(field),
                EditFieldType.ComboBox => CreateComboBox(field),
                EditFieldType.CheckBox => CreateCheckBox(field),
                EditFieldType.Numeric => CreateTextBox(field),
                _ => null
            };
        }

        private FrameworkElement CreateTextBox(EditField field)
        {
            var textBox = new TextBox
            {
                Text = field.Value,
                Style = GetResource<Style>("EditFieldValueStyle"),
                ToolTip = field.ToolTip,
                IsReadOnly = field.IsReadOnly,
                MinHeight = TextControlMinHeight,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 0, 4, 0)
            };

            if (field.MaxLength.HasValue)
            {
                textBox.MaxLength = field.MaxLength.Value;
            }

            // Автоматическое применение с debouncing для TextBox
            if (!field.IsReadOnly)
            {
                textBox.TextChanged += (s, e) =>
                {
                    _applyTimer.Stop();
                    _applyTimer.Start();
                };
            }

            return textBox;
        }

        private FrameworkElement CreateComboBox(EditField field)
        {
            var comboBox = new ComboBox
            {
                Style = GetResource<Style>("EditFieldComboBoxStyle"),
                ToolTip = field.ToolTip,
                IsEnabled = !field.IsReadOnly,
                MinHeight = ComboBoxMinHeight,
                MaxHeight = RowMinHeight,
                Padding = new Thickness(4, 0, 4, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };

            if (field.ComboBoxItems != null)
            {
                foreach (var item in field.ComboBoxItems)
                {
                    var comboItem = new System.Windows.Controls.ComboBoxItem
                    {
                        Content = item.Content,
                        Tag = item.Tag
                    };
                    comboBox.Items.Add(comboItem);

                    if (string.Equals(item.Tag, field.Value, StringComparison.Ordinal))
                    {
                        comboBox.SelectedItem = comboItem;
                    }
                }
            }

            // Автоматическое применение при изменении выбора
            if (!field.IsReadOnly)
            {
                comboBox.SelectionChanged += (s, e) => ApplyChangesImmediately();
            }

            return comboBox;
        }

        private FrameworkElement CreateCheckBox(EditField field)
        {
            var checkBox = new CheckBox
            {
                Content = field.Value == "True" || field.Value == "true" ? "Enabled" : "Disabled",
                IsChecked = field.Value == "True" || field.Value == "true",
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = field.ToolTip,
                IsEnabled = !field.IsReadOnly,
                MinHeight = TextControlMinHeight
            };

            // Автоматическое применение при изменении CheckBox
            if (!field.IsReadOnly)
            {
                checkBox.Checked += (s, e) => ApplyChangesImmediately();
                checkBox.Unchecked += (s, e) => ApplyChangesImmediately();
            }

            return checkBox;
        }


        private void UpdateFieldsFromEditor()
        {
            if (_currentEditor == null)
                return;

            var fields = _currentEditor.GetEditFields();
            if (fields == null)
                return;

            // Обновляем значения в динамических элементах
            foreach (var field in fields)
            {
                if (_dynamicFieldControls.TryGetValue(field.Id, out var control))
                {
                    UpdateControlValue(control, field);
                }
            }
        }

        private void UpdateControlValue(FrameworkElement control, EditField field)
        {
            switch (control)
            {
                case TextBox textBox:
                    textBox.Text = field.Value;
                    textBox.IsReadOnly = field.IsReadOnly;
                    break;
                case ComboBox comboBox:
                    if (field.ComboBoxItems != null)
                    {
                        foreach (var item in comboBox.Items.OfType<System.Windows.Controls.ComboBoxItem>())
                        {
                            if (item.Tag?.ToString() == field.Value)
                            {
                                comboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                    comboBox.IsEnabled = !field.IsReadOnly;
                    break;
                case CheckBox checkBox:
                    checkBox.IsChecked = field.Value == "True" || field.Value == "true";
                    checkBox.IsEnabled = !field.IsReadOnly;
                    break;
            }
        }


        private Dictionary<string, string> CollectFieldValues()
        {
            var values = new Dictionary<string, string>();

            // Собираем значения из динамических элементов
            foreach (var kvp in _dynamicFieldControls)
            {
                string fieldId = kvp.Key;
                FrameworkElement control = kvp.Value;

                string? value = GetControlValue(control);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values[fieldId] = value;
                }
            }

            return values;
        }

        private string? GetControlValue(FrameworkElement control)
        {
            return control switch
            {
                TextBox textBox => textBox.Text,
                ComboBox comboBox => (comboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() ?? "",
                CheckBox checkBox => checkBox.IsChecked == true ? "True" : "False",
                _ => null
            };
        }

        /// <summary>
        /// Автоматически применяет изменения при редактировании полей
        /// </summary>
        private void ApplyChangesImmediately()
        {
            if (_isApplyingChanges || _currentEditor == null || _currentSpdData == null)
                return;

            try
            {
                _isApplyingChanges = true;

                // Собираем значения из UI
                var fieldValues = CollectFieldValues();

                // Валидация (тихо, без сообщений)
                var validationErrors = _currentEditor.ValidateFields(fieldValues);
                if (validationErrors.Count > 0)
                {
                    // Если есть ошибки валидации, не применяем изменения
                    return;
                }

                // Применяем изменения через редактор
                var changes = _currentEditor.ApplyChanges(fieldValues);

                // Обновляем текущие данные
                foreach (var change in changes)
                {
                    if (change.Offset >= 0 && change.Offset < _currentSpdData.Length && 
                        change.NewData != null && change.NewData.Length > 0)
                    {
                        for (int i = 0; i < change.NewData.Length && (change.Offset + i) < _currentSpdData.Length; i++)
                        {
                            _currentSpdData[change.Offset + i] = change.NewData[i];
                        }
                    }
                }

                // Перезагружаем данные в редактор для синхронизации
                _currentEditor.LoadData(_currentSpdData);

                if (changes.Count > 0)
                {
                    ChangesApplied?.Invoke(changes);
                }
            }
            catch (Exception ex)
            {
                // Тихая обработка ошибок при автоматическом применении
                System.Diagnostics.Debug.WriteLine($"Error applying changes automatically: {ex.Message}");
            }
            finally
            {
                _isApplyingChanges = false;
            }
        }
    }
}

