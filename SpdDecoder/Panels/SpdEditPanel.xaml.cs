using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HexEditor.SpdDecoder
{
    /// <summary>
    /// UserControl для редактирования SPD данных по стандарту JEDEC
    /// </summary>
    public partial class SpdEditPanel : UserControl
    {
        private byte[]? _currentSpdData;
        private ISpdEditor? _currentEditor;
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
            
            // Подписываемся на событие Loaded для проверки доступности элементов
            Loaded += OnSpdEditPanelLoaded;
            
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

        private void OnSpdEditPanelLoaded(object sender, RoutedEventArgs e)
        {
            // Элементы доступны после загрузки
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

            UpdateStaticFields();
        }

        /// <summary>
        /// Очищает панель редактирования
        /// </summary>
        public void Clear()
        {
            _currentSpdData = null;
            _currentEditor = null;
            
            // Скрываем все секции и поля
            HideAllSections();
            HideAllFields();
        }

        /// <summary>
        /// Обновляет статические поля на основе данных редактора
        /// </summary>
        private void UpdateStaticFields()
        {
            if (_currentEditor == null)
            {
                HideAllSections();
                HideAllFields();
                return;
            }

            var fields = _currentEditor.GetEditFields();
            if (fields == null || fields.Count == 0)
            {
                HideAllSections();
                HideAllFields();
                return;
            }

            // Группируем поля по категориям
            var fieldsByCategory = fields
                .GroupBy(f => f.Category ?? "Common")
                .OrderBy(g => GetCategoryOrder(g.Key))
                .ToDictionary(g => g.Key, g => g.ToList());

            // Показываем/скрываем секции и заполняем поля
            UpdateCategorySection("MemoryModule", FindName("MemoryModuleSection") as Border, fieldsByCategory);
            UpdateCategorySection("DensityDie", FindName("DensityDieSection") as Border, fieldsByCategory);
            UpdateCategorySection("DramComponents", FindName("DramComponentsSection") as Border, fieldsByCategory);
            UpdateCategorySection("Timing", FindName("TimingSection") as Border, fieldsByCategory);
            UpdateCategorySection("ModuleConfig", FindName("ModuleConfigSection") as Border, fieldsByCategory);
            UpdateCategorySection("XMP", null, fieldsByCategory); // XMP пока не реализовано

            // Заполняем все поля значениями
            // Обрабатываем ModuleYear и ModuleWeek вместе (они в одной строке)
            bool skipNext = false;
            for (int i = 0; i < fields.Count; i++)
            {
                if (skipNext)
                {
                    skipNext = false;
                    continue;
                }

                var field = fields[i];
                
                // Специальная обработка для ModuleYear + ModuleWeek
                if (field.Id == "ModuleYear" && i + 1 < fields.Count && fields[i + 1].Id == "ModuleWeek")
                {
                    UpdateField(field);
                    UpdateField(fields[i + 1], skipZebra: true); // Пропускаем zebra для ModuleWeek, так как он в том же Grid
                    skipNext = true;
                }
                else
                {
                    UpdateField(field);
                }
            }

            // Применяем zebra-паттерн после обновления всех полей
            ApplyZebraPatternToAllVisibleFields();
        }

        /// <summary>
        /// Обновляет секцию категории (показывает/скрывает)
        /// </summary>
        private void UpdateCategorySection(string category, Border? sectionBorder, Dictionary<string, List<EditField>> fieldsByCategory)
        {
            if (sectionBorder == null)
                return;

            if (fieldsByCategory.TryGetValue(category, out var categoryFields) && categoryFields.Count > 0)
            {
                sectionBorder.Visibility = Visibility.Visible;
            }
            else
            {
                sectionBorder.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Обновляет конкретное поле
        /// </summary>
        private void UpdateField(EditField field, bool skipZebra = false)
        {
            // Специальная обработка для ModuleWeek - он использует тот же Grid что и ModuleYear
            if (field.Id == "ModuleWeek")
            {
                // Обновляем только значение, без показа Grid (Grid уже показан для ModuleYear)
                UpdateComboBoxField(field);
                return;
            }

            // Используем FindName для поиска элементов в визуальном дереве
            // В WPF FindName ищет элементы по всему визуальному дереву UserControl
            var fieldGrid = FindName($"EditField_{field.Id}") as Grid;
            if (fieldGrid == null)
            {
                return;
            }

            // Показываем поле
            fieldGrid.Visibility = Visibility.Visible;

            // Обновляем label
            var labelControl = FindName($"EditField_{field.Id}_Label") as TextBox;
            if (labelControl != null && !string.IsNullOrEmpty(field.Label))
            {
                labelControl.Text = field.Label;
            }

            // Обновляем value в зависимости от типа
            switch (field.Type)
            {
                case EditFieldType.TextBox:
                    UpdateTextBoxField(field);
                    break;
                case EditFieldType.ComboBox:
                    UpdateComboBoxField(field);
                    break;
                case EditFieldType.CheckBox:
                    UpdateCheckBoxField(field);
                    break;
            }
        }

        /// <summary>
        /// Применяет zebra-паттерн (чередование фона) ко всем видимым полям во всех секциях
        /// </summary>
        private void ApplyZebraPatternToAllVisibleFields()
        {
            var zebraEven = GetResource<Brush>("ZebraEvenBrush") ?? new SolidColorBrush(Colors.White);
            var zebraOdd = GetResource<Brush>("ZebraOddBrush") ?? new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));

            // Применяем zebra-паттерн для каждой секции отдельно
            ApplyZebraPatternToSection(FindName("MemoryModuleSection") as Border, zebraEven, zebraOdd);
            ApplyZebraPatternToSection(FindName("DensityDieSection") as Border, zebraEven, zebraOdd);
            ApplyZebraPatternToSection(FindName("DramComponentsSection") as Border, zebraEven, zebraOdd);
            ApplyZebraPatternToSection(FindName("TimingSection") as Border, zebraEven, zebraOdd);
            ApplyZebraPatternToSection(FindName("ModuleConfigSection") as Border, zebraEven, zebraOdd);
        }

        /// <summary>
        /// Применяет zebra-паттерн к полям в секции
        /// </summary>
        private void ApplyZebraPatternToSection(Border? section, Brush zebraEven, Brush zebraOdd)
        {
            if (section == null || section.Visibility != Visibility.Visible)
                return;

            var stackPanel = section.Child as StackPanel;
            if (stackPanel == null)
                return;

            int visibleIndex = 0;
            foreach (var child in stackPanel.Children)
            {
                // Пропускаем заголовок (TextBlock)
                if (child is TextBlock)
                    continue;

                if (child is Grid fieldGrid)
                {
                    if (fieldGrid.Visibility == Visibility.Visible)
                    {
                        fieldGrid.Background = visibleIndex % 2 == 0 ? zebraEven : zebraOdd;
                        visibleIndex++;
                    }
                }
            }
        }

        /// <summary>
        /// Обновляет TextBox поле
        /// </summary>
        private void UpdateTextBoxField(EditField field)
        {
            var valueControl = FindName($"EditField_{field.Id}_Value") as TextBox;
            if (valueControl == null)
                return;

            // Отключаем обработчик событий временно, чтобы не вызывать ApplyChanges
            valueControl.TextChanged -= OnTextBoxTextChanged;
            
            // Для Part Number заменяем пробелы на видимый символ (middle dot) для лучшей видимости
            string displayText = field.Id == "ModulePartNumber" 
                ? field.Value?.Replace(' ', '·') ?? "" 
                : field.Value ?? "";
            
            valueControl.Text = displayText;
            valueControl.IsReadOnly = field.IsReadOnly;
            valueControl.ToolTip = field.ToolTip;
            
            if (field.MaxLength.HasValue)
            {
                valueControl.MaxLength = field.MaxLength.Value;
            }

            // Включаем обработчик событий обратно
            if (!field.IsReadOnly)
            {
                valueControl.TextChanged += OnTextBoxTextChanged;
                
                // Для Part Number добавляем обработчики для автоматической замены пробелов на middle dot при вводе
                if (field.Id == "ModulePartNumber")
                {
                    valueControl.PreviewTextInput -= OnPartNumberPreviewTextInput;
                    valueControl.PreviewTextInput += OnPartNumberPreviewTextInput;
                    
                    // Обрабатываем вставку (Ctrl+V)
                    valueControl.PreviewKeyDown -= OnPartNumberPreviewKeyDown;
                    valueControl.PreviewKeyDown += OnPartNumberPreviewKeyDown;
                }
            }
        }
        
        /// <summary>
        /// Обработчик для Part Number: заменяет пробелы на middle dot при вводе и вставляет символ в позицию курсора
        /// Валидирует символы по JEDEC стандарту (ASCII печатные символы 32-126) и ограничивает длину
        /// </summary>
        private void OnPartNumberPreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null)
                return;
            
            // Валидация: разрешены только ASCII печатные символы (32-126) по JEDEC стандарту
            string validInput = "";
            foreach (char c in e.Text)
            {
                // Разрешаем только ASCII печатные символы (32-126)
                if (c >= 32 && c <= 126)
                {
                    // Заменяем пробелы на middle dot для отображения
                    validInput += (c == ' ') ? '·' : c;
                }
            }
            
            if (validInput.Length == 0)
            {
                // Нет валидных символов - блокируем ввод
                e.Handled = true;
                return;
            }
            
            // Проверяем максимальную длину (20 для DDR4, 30 для DDR5)
            int maxLength = textBox.MaxLength > 0 ? textBox.MaxLength : 20;
            int selectionStart = textBox.SelectionStart;
            int selectionLength = textBox.SelectionLength;
            int currentLength = textBox.Text.Length - selectionLength;
            
            // Ограничиваем длину вставляемого текста
            int availableLength = maxLength - currentLength;
            if (availableLength <= 0)
            {
                e.Handled = true;
                return; // Достигнута максимальная длина
            }
            
            if (validInput.Length > availableLength)
            {
                validInput = validInput.Substring(0, availableLength);
            }
            
            e.Handled = true;
            
            // Вставляем текст в позицию курсора, удаляя выделенный текст (стандартное поведение)
            string newText = textBox.Text.Substring(0, selectionStart) + 
                           validInput + 
                           textBox.Text.Substring(selectionStart + selectionLength);
            
            textBox.Text = newText;
            // Устанавливаем курсор после вставленного текста
            textBox.SelectionStart = selectionStart + validInput.Length;
            textBox.SelectionLength = 0;
        }
        
        /// <summary>
        /// Обработчик для Part Number: обрабатывает вставку (Ctrl+V) с заменой пробелов на middle dot
        /// Валидирует символы по JEDEC стандарту и ограничивает длину
        /// </summary>
        private void OnPartNumberPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Обрабатываем вставку через Ctrl+V
            if (e.Key == System.Windows.Input.Key.V && 
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                var textBox = sender as TextBox;
                if (textBox != null)
                {
                    // Получаем текст из буфера обмена
                    if (Clipboard.ContainsText())
                    {
                        string clipboardText = Clipboard.GetText();
                        
                        // Валидация: разрешены только ASCII печатные символы (32-126) по JEDEC стандарту
                        var validChars = new System.Text.StringBuilder();
                        foreach (char c in clipboardText)
                        {
                            // Разрешаем только ASCII печатные символы (32-126)
                            if (c >= 32 && c <= 126)
                            {
                                // Заменяем пробелы на middle dot для отображения
                                validChars.Append((c == ' ') ? '·' : c);
                            }
                        }
                        
                        string processedText = validChars.ToString();
                        
                        if (processedText.Length > 0)
                        {
                            e.Handled = true;
                            
                            // Проверяем максимальную длину (20 для DDR4, 30 для DDR5)
                            int maxLength = textBox.MaxLength > 0 ? textBox.MaxLength : 20;
                            int selectionStart = textBox.SelectionStart;
                            int selectionLength = textBox.SelectionLength;
                            int currentLength = textBox.Text.Length - selectionLength;
                            
                            // Ограничиваем длину вставляемого текста
                            int availableLength = maxLength - currentLength;
                            if (availableLength > 0 && processedText.Length > availableLength)
                            {
                                processedText = processedText.Substring(0, availableLength);
                            }
                            else if (availableLength <= 0)
                            {
                                return; // Достигнута максимальная длина
                            }
                            
                            // Вставляем текст в позицию курсора, удаляя выделенный текст
                            string newText = textBox.Text.Substring(0, selectionStart) + 
                                           processedText + 
                                           textBox.Text.Substring(selectionStart + selectionLength);
                            
                            textBox.Text = newText;
                            // Устанавливаем курсор после вставленного текста
                            textBox.SelectionStart = selectionStart + processedText.Length;
                            textBox.SelectionLength = 0;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Обновляет ComboBox поле
        /// </summary>
        private void UpdateComboBoxField(EditField field)
        {
            var valueControl = FindName($"EditField_{field.Id}_Value") as ComboBox;
            if (valueControl == null)
                return;

            // Отключаем обработчик событий временно
            valueControl.SelectionChanged -= OnComboBoxSelectionChanged;

            // Очищаем и заполняем ComboBox
            valueControl.Items.Clear();
            if (field.ComboBoxItems != null)
            {
                foreach (var item in field.ComboBoxItems)
                {
                    var comboItem = new System.Windows.Controls.ComboBoxItem
                    {
                        Content = item.Content,
                        Tag = item.Tag
                    };
                    valueControl.Items.Add(comboItem);

                    if (string.Equals(item.Tag, field.Value, StringComparison.Ordinal))
                    {
                        valueControl.SelectedItem = comboItem;
                    }
                }
            }

            valueControl.IsEnabled = !field.IsReadOnly;
            valueControl.ToolTip = field.ToolTip;

            // Включаем обработчик событий обратно
            if (!field.IsReadOnly)
            {
                valueControl.SelectionChanged += OnComboBoxSelectionChanged;
            }
        }

        /// <summary>
        /// Обновляет CheckBox поле
        /// </summary>
        private void UpdateCheckBoxField(EditField field)
        {
            var valueControl = FindName($"EditField_{field.Id}_Value") as CheckBox;
            if (valueControl == null)
                return;

            // Отключаем обработчики событий временно
            valueControl.Checked -= OnCheckBoxChecked;
            valueControl.Unchecked -= OnCheckBoxUnchecked;

            bool isChecked = field.Value == "True" || field.Value == "true";
            valueControl.IsChecked = isChecked;
            valueControl.Content = isChecked ? "Enabled" : "Disabled";
            valueControl.IsEnabled = !field.IsReadOnly;
            valueControl.ToolTip = field.ToolTip;

            // Включаем обработчики событий обратно
            if (!field.IsReadOnly)
            {
                valueControl.Checked += OnCheckBoxChecked;
                valueControl.Unchecked += OnCheckBoxUnchecked;
            }
        }

        /// <summary>
        /// Обработчик изменения текста в TextBox
        /// </summary>
        private void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            // Для Part Number автоматически заменяем пробелы на middle dot
            if (sender is TextBox textBox && textBox.Name == "EditField_ModulePartNumber_Value")
            {
                if (textBox.Text.Contains(' '))
                {
                    int selectionStart = textBox.SelectionStart;
                    int selectionLength = textBox.SelectionLength;
                    string newText = textBox.Text.Replace(' ', '·');
                    textBox.Text = newText;
                    // Восстанавливаем позицию курсора с учётом заменённых символов
                    textBox.SelectionStart = Math.Min(selectionStart, newText.Length);
                    textBox.SelectionLength = selectionLength;
                }
            }
            
            _applyTimer.Stop();
            _applyTimer.Start();
        }

        /// <summary>
        /// Обработчик изменения выбора в ComboBox
        /// </summary>
        private void OnComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyChangesImmediately();
        }

        /// <summary>
        /// Обработчик изменения CheckBox (Checked)
        /// </summary>
        private void OnCheckBoxChecked(object sender, RoutedEventArgs e)
        {
            ApplyChangesImmediately();
        }

        /// <summary>
        /// Обработчик изменения CheckBox (Unchecked)
        /// </summary>
        private void OnCheckBoxUnchecked(object sender, RoutedEventArgs e)
        {
            ApplyChangesImmediately();
        }

        /// <summary>
        /// Скрывает все секции
        /// </summary>
        private void HideAllSections()
        {
            var memoryModuleSection = FindName("MemoryModuleSection") as Border;
            if (memoryModuleSection != null) memoryModuleSection.Visibility = Visibility.Collapsed;
            
            var densityDieSection = FindName("DensityDieSection") as Border;
            if (densityDieSection != null) densityDieSection.Visibility = Visibility.Collapsed;
            
            var dramComponentsSection = FindName("DramComponentsSection") as Border;
            if (dramComponentsSection != null) dramComponentsSection.Visibility = Visibility.Collapsed;
            
            var timingSection = FindName("TimingSection") as Border;
            if (timingSection != null) timingSection.Visibility = Visibility.Collapsed;
            
            var moduleConfigSection = FindName("ModuleConfigSection") as Border;
            if (moduleConfigSection != null) moduleConfigSection.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Скрывает все поля
        /// </summary>
        private void HideAllFields()
        {
            // Получаем все Grid элементы, которые начинаются с "EditField_"
            var allFields = new[]
            {
                "ModuleManufacturer", "ModulePartNumber", "ModuleSerialNumber", "ModuleYear",
                "ModuleLocation", "ModuleType", "SpdRevisionMajor", "SpdRevisionMinor", "MemoryType",
                "Density", "PackageMonolithic", "PackageDieCount", "Banks", "BankGroups",
                "ColumnAddresses", "RowAddresses",
                "DramManufacturer",
                "TimingTckMtb", "TimingTckFtb", "TimingTaaMtb", "TimingTaaFtb",
                "TimingTrcdMtb", "TimingTrcdFtb", "TimingTrpMtb", "TimingTrpFtb",
                "TimingTras", "TimingTrc", "TimingTrcFtb", "TimingTfaw",
                "TimingTrrdSMtb", "TimingTrrdSFtb", "TimingTrrdLMtb", "TimingTrrdLFtb",
                "TimingCcdlMtb", "TimingCcdlFtb", "TimingTwr", "TimingTwtrs",
                "ModuleRanks", "DeviceWidth", "PrimaryBusWidth", "HasEcc",
                "RankMix", "ThermalSensor", "SupplyVoltageOperable"
            };

            foreach (var fieldId in allFields)
            {
                var fieldGrid = FindName($"EditField_{fieldId}") as Grid;
                if (fieldGrid != null)
                {
                    fieldGrid.Visibility = Visibility.Collapsed;
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



        /// <summary>
        /// Собирает значения из всех видимых статических полей
        /// </summary>
        private Dictionary<string, string> CollectFieldValues()
        {
            var values = new Dictionary<string, string>();

            if (_currentEditor == null)
                return values;

            var fields = _currentEditor.GetEditFields();
            if (fields == null)
                return values;

            // Собираем значения из статических элементов
            foreach (var field in fields)
            {
                // Специальная обработка для ModuleWeek - он использует тот же Grid что и ModuleYear
                if (field.Id == "ModuleWeek")
                {
                    // ModuleWeek не имеет своего Grid, но имеет свой ComboBox в Grid ModuleYear
                    string? weekValue = GetControlValue(field.Id, field.Type);
                    if (weekValue != null)
                    {
                        values[field.Id] = weekValue;
                    }
                    continue;
                }

                var fieldGrid = FindName($"EditField_{field.Id}") as Grid;
                if (fieldGrid == null || fieldGrid.Visibility != Visibility.Visible)
                    continue;

                string? fieldValue = GetControlValue(field.Id, field.Type);
                // Сохраняем значение, даже если оно пустое (для некоторых полей пустое значение валидно)
                // Редактор сам решит, нужно ли применять пустое значение
                if (fieldValue != null)
                {
                    values[field.Id] = fieldValue;
                }
            }

            return values;
        }

        /// <summary>
        /// Получает значение из статического контрола по ID поля
        /// </summary>
        private string? GetControlValue(string fieldId, EditFieldType fieldType)
        {
            var valueControl = FindName($"EditField_{fieldId}_Value");
            
            string? result = valueControl switch
            {
                TextBox textBox => textBox.Text,
                ComboBox comboBox => (comboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() ?? "",
                CheckBox checkBox => checkBox.IsChecked == true ? "True" : "False",
                _ => null
            };
            
            // Для Part Number преобразуем видимый символ (middle dot) обратно в пробел
            if (fieldId == "ModulePartNumber" && result != null)
            {
                result = result.Replace('·', ' ');
            }
            
            return result;
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

