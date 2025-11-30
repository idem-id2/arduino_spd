using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace HexEditor.Database
{

    /// <summary>
    /// UserControl для редактирования базы данных производителей
    /// </summary>
    public partial class ManufacturerEditor : UserControl
    {
        private ObservableCollection<ManufacturerEntry> _entries = new();

        /// <summary>
        /// Инициализирует новый экземпляр редактора базы данных
        /// </summary>
        public ManufacturerEditor()
        {
            InitializeComponent();
            DatabasePathText.Text = $"База данных: {ManufacturerDatabase.GetDatabasePath()}";
            LoadDatabase();
            UpdateMoveButtonsState();
        }

        private void LoadDatabase()
        {
            try
            {
                var entries = ManufacturerDatabase.LoadDatabase();
                _entries = new ObservableCollection<ManufacturerEntry>(entries);
                UpdateRowNumbers(); // БАГ #7: Обновляем номера строк
                EntriesDataGrid.ItemsSource = _entries;
                UpdateStatus($"База данных загружена ({_entries.Count} записей)");
                UpdateMoveButtonsState(); // БАГ #1: Обновляем состояние кнопок после загрузки
            }
            catch (Exception ex)
            {
                UpdateStatus($"Ошибка загрузки: {ex.Message}");
                UpdateMoveButtonsState(); // БАГ #1: Обновляем состояние и при ошибке
            }
        }

        private void UpdateRowNumbers()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                _entries[i].RowNumber = i + 1;
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            // Находим следующий доступный ID (или начинаем с 0x0001)
            ushort nextId = 1;
            if (_entries.Any())
            {
                var maxId = _entries.Max(e => e.Id);
                nextId = (ushort)(maxId + 1);
            }

            var newEntry = new ManufacturerEntry
            {
                Id = nextId,
                Name = "New Manufacturer"
            };
            _entries.Add(newEntry);
            UpdateRowNumbers(); // БАГ #7: Обновляем номера строк после добавления
            EntriesDataGrid.SelectedItem = newEntry;
            EntriesDataGrid.ScrollIntoView(newEntry);
            UpdateStatus("Добавлена новая запись");
            UpdateMoveButtonsState(); // БАГ #2: Обновляем состояние кнопок после добавления
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (EntriesDataGrid.SelectedItem is ManufacturerEntry selected)
            {
                var result = MessageBox.Show(
                    $"Удалить производителя '{selected.Name}' (ID: {selected.IdHex})?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _entries.Remove(selected);
                    UpdateRowNumbers(); // БАГ #7: Обновляем номера строк после удаления
                    UpdateStatus($"Производитель '{selected.Name}' удален");
                    UpdateMoveButtonsState(); // БАГ #3: Обновляем состояние кнопок после удаления
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Валидация перед сохранением
                var invalidEntries = _entries.Where(e => 
                    string.IsNullOrWhiteSpace(e.Name)).ToList();

                if (invalidEntries.Any())
                {
                    MessageBox.Show(
                        $"Найдены записи с пустым названием производителя.\n" +
                        $"Исправьте их перед сохранением.",
                        "Ошибка валидации",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Проверка на дубликаты ID
                var duplicateIds = _entries
                    .GroupBy(e => e.Id)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateIds.Any())
                {
                    MessageBox.Show(
                        $"Найдены записи с одинаковыми ID: {string.Join(", ", duplicateIds.Select(id => $"0x{id:X4}"))}\n" +
                        $"Исправьте их перед сохранением.",
                        "Ошибка валидации",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                
                // Проверка на корректность ID (валидный HEX, 4 символа)
                // Благодаря автоформатированию в модели, если значение валидное, оно будет XXXX.
                // Но если пользователь ввел некорректный HEX ("XYZ"), он остался в IdHex, а Id не обновился.
                var invalidHexIds = _entries
                    .Where(e => !IsHexInput(e.IdHex) || e.IdHex.Length != 4)
                    .ToList();
                    
                if (invalidHexIds.Any())
                {
                    MessageBox.Show(
                        $"Найдены записи с некорректным ID (должно быть 4 HEX символа):\n" +
                        $"{string.Join(", ", invalidHexIds.Select(e => $"'{e.IdHex}'"))}\n" +
                        $"Исправьте их перед сохранением.",
                        "Ошибка валидации",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                ManufacturerDatabase.SaveDatabase(_entries.ToList());
                UpdateStatus($"База данных сохранена ({_entries.Count} записей)");
                
                // Уведомляем декодер о необходимости перезагрузки
                ManufacturerDatabase.ClearCache();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка сохранения базы данных:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                UpdateStatus($"Ошибка сохранения: {ex.Message}");
            }
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Перезагрузить базу данных? Все несохранённые изменения будут потеряны.",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                LoadDatabase();
            }
        }

        private void EntriesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateMoveButtonsState();
        }

        private void UpdateMoveButtonsState()
        {
            bool hasSelection = EntriesDataGrid.SelectedItem != null;
            int selectedIndex = EntriesDataGrid.SelectedIndex;
            int count = _entries.Count;

            DeleteButton.IsEnabled = hasSelection;
            MoveUpButton.IsEnabled = hasSelection && selectedIndex > 0;
            MoveDownButton.IsEnabled = hasSelection && selectedIndex < count - 1 && selectedIndex >= 0;
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = EntriesDataGrid.SelectedIndex;
            if (selectedIndex <= 0)
                return;

            var item = _entries[selectedIndex];
            _entries.RemoveAt(selectedIndex);
            _entries.Insert(selectedIndex - 1, item);
            UpdateRowNumbers(); // БАГ #7: Обновляем номера строк после перемещения
            EntriesDataGrid.SelectedIndex = selectedIndex - 1;
            EntriesDataGrid.ScrollIntoView(item);
            
            UpdateStatus("Строка перемещена вверх (не забудьте сохранить!)");
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = EntriesDataGrid.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= _entries.Count - 1)
                return;

            var item = _entries[selectedIndex];
            _entries.RemoveAt(selectedIndex);
            _entries.Insert(selectedIndex + 1, item);
            UpdateRowNumbers(); // БАГ #7: Обновляем номера строк после перемещения
            EntriesDataGrid.SelectedIndex = selectedIndex + 1;
            EntriesDataGrid.ScrollIntoView(item);
            
            UpdateStatus("Строка перемещена вниз (не забудьте сохранить!)");
        }

        private void EntriesDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            // БАГ #6: Проверяем по Binding вместо Header (более надежно)
            if (e.Column is DataGridTextColumn column)
            {
                var binding = column.Binding as System.Windows.Data.Binding;
                string? path = binding?.Path.Path;
                
                // Разрешаем редактирование только для IdHex и Name
                if (path == "ContinuationCode" || path == "ManufacturerCode")
                {
                    e.Cancel = true;
                }
            }
        }

        private void UpdateStatus(string message)
        {
            StatusTextBlock.Text = message;
        }

        private void IdHexTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // Разрешаем только hex символы (0-9, A-F, a-f)
            e.Handled = !IsHexInput(e.Text);
        }

        private void IdHexTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!IsHexInput(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        private bool IsHexInput(string text)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(text, "^[0-9A-Fa-f]+$");
        }
    }
}

