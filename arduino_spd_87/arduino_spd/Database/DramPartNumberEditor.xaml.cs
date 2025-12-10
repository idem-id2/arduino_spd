using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace HexEditor.Database
{
    /// <summary>
    /// UserControl для редактирования базы данных DRAM part numbers
    /// </summary>
    public partial class DramPartNumberEditor : UserControl
    {
        private ObservableCollection<DramPartNumberEntry> _entries = new();

        /// <summary>
        /// Инициализирует новый экземпляр редактора базы данных
        /// </summary>
        public DramPartNumberEditor()
        {
            InitializeComponent();
            DatabasePathText.Text = $"База данных: {DramPartNumberDatabase.GetDatabasePath()}";
            LoadDatabase();
        }

        private void LoadDatabase()
        {
            try
            {
                var entries = DramPartNumberDatabase.LoadDatabase();
                _entries = new ObservableCollection<DramPartNumberEntry>(entries);
                EntriesDataGrid.ItemsSource = _entries;
                UpdateStatus("База данных загружена");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Ошибка загрузки: {ex.Message}");
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var newEntry = new DramPartNumberEntry
            {
                PartNumber = "NEW_PART_NUMBER",
                Manufacturer = "Unknown"
            };
            _entries.Add(newEntry);
            EntriesDataGrid.SelectedItem = newEntry;
            EntriesDataGrid.ScrollIntoView(newEntry);
            UpdateStatus("Добавлена новая запись");
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (EntriesDataGrid.SelectedItem is DramPartNumberEntry selected)
            {
                var result = MessageBox.Show(
                    $"Удалить запись '{selected.PartNumber}'?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _entries.Remove(selected);
                    UpdateStatus($"Запись '{selected.PartNumber}' удалена");
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Валидация перед сохранением
                var invalidEntries = _entries.Where(e => 
                    string.IsNullOrWhiteSpace(e.PartNumber) || 
                    string.IsNullOrWhiteSpace(e.Manufacturer)).ToList();

                if (invalidEntries.Any())
                {
                    MessageBox.Show(
                        $"Найдены записи с пустыми обязательными полями (Part Number, Manufacturer).\n" +
                        $"Исправьте их перед сохранением.",
                        "Ошибка валидации",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                DramPartNumberDatabase.SaveDatabase(_entries.ToList());
                UpdateStatus("База данных сохранена");
                
                // Уведомляем декодер о необходимости перезагрузки
                DramPartNumberDatabase.ClearCache();
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
            DeleteButton.IsEnabled = EntriesDataGrid.SelectedItem != null;
        }

        private void EntriesDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Обработка конвертации строк в int? для nullable полей
            if (e.EditingElement is System.Windows.Controls.TextBox textBox)
            {
                var column = e.Column as System.Windows.Controls.DataGridTextColumn;
                if (column != null)
                {
                    var binding = column.Binding as System.Windows.Data.Binding;
                    if (binding != null && e.Row.Item is DramPartNumberEntry entry)
                    {
                        string propertyName = binding.Path.Path;
                        string text = textBox.Text?.Trim() ?? string.Empty;

                        // Если поле пустое, устанавливаем null
                        if (string.IsNullOrEmpty(text))
                        {
                            switch (propertyName)
                            {
                                case "DieDensityGb":
                                    entry.DieDensityGb = null;
                                    break;
                                case "DeviceWidth":
                                    entry.DeviceWidth = null;
                                    break;
                                case "DieCount":
                                    entry.DieCount = null;
                                    break;
                            }
                        }
                        else
                        {
                            // Пытаемся распарсить int
                            if (int.TryParse(text, out int value))
                            {
                                switch (propertyName)
                                {
                                    case "DieDensityGb":
                                        entry.DieDensityGb = value;
                                        break;
                                    case "DeviceWidth":
                                        entry.DeviceWidth = value;
                                        break;
                                    case "DieCount":
                                        entry.DieCount = value;
                                        break;
                                }
                            }
                        }
                    }
                }
            }

            UpdateStatus("Изменения не сохранены (нажмите 'Сохранить')");
        }

        private void UpdateStatus(string message)
        {
            StatusTextBlock.Text = message;
        }
    }
}

