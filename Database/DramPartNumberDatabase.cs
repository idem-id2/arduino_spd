using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HexEditor.Database
{
    /// <summary>
    /// Модель записи в базе данных DRAM part numbers
    /// </summary>
    public class DramPartNumberEntry : INotifyPropertyChanged
    {
        private string _partNumber = string.Empty;
        private string _manufacturer = string.Empty;
        private int? _dieDensityGb;
        private int? _deviceWidth;
        private int? _dieCount;
        private bool? _isMultiLoadStack;
        private string? _dieInfo;
        private string? _package;
        private string? _notes;

        [JsonPropertyName("partNumber")]
        public string PartNumber
        {
            get => _partNumber;
            set { _partNumber = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("manufacturer")]
        public string Manufacturer
        {
            get => _manufacturer;
            set { _manufacturer = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("dieDensityGb")]
        public int? DieDensityGb
        {
            get => _dieDensityGb;
            set { _dieDensityGb = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("deviceWidth")]
        public int? DeviceWidth
        {
            get => _deviceWidth;
            set { _deviceWidth = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("dieCount")]
        public int? DieCount
        {
            get => _dieCount;
            set { _dieCount = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("isMultiLoadStack")]
        public bool? IsMultiLoadStack
        {
            get => _isMultiLoadStack;
            set { _isMultiLoadStack = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("dieInfo")]
        public string? DieInfo
        {
            get => _dieInfo;
            set { _dieInfo = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("package")]
        public string? Package
        {
            get => _package;
            set { _package = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("notes")]
        public string? Notes
        {
            get => _notes;
            set { _notes = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Сервис для работы с базой данных DRAM part numbers
    /// </summary>
    public static class DramPartNumberDatabase
    {
        private static readonly string DatabasePath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory,
            "Database",
            "dram_part_numbers.json");

        private static List<DramPartNumberEntry>? _cachedEntries;

        /// <summary>
        /// Загружает базу данных из файла
        /// </summary>
        public static List<DramPartNumberEntry> LoadDatabase()
        {
            if (_cachedEntries != null)
                return _cachedEntries;

            EnsureDatabaseDirectoryExists();

            if (!File.Exists(DatabasePath))
            {
                _cachedEntries = new List<DramPartNumberEntry>();
                return _cachedEntries;
            }

            try
            {
                string json = File.ReadAllText(DatabasePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                _cachedEntries = JsonSerializer.Deserialize<List<DramPartNumberEntry>>(json, options) 
                    ?? new List<DramPartNumberEntry>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки базы DRAM part numbers: {ex.Message}");
                _cachedEntries = new List<DramPartNumberEntry>();
            }

            return _cachedEntries;
        }

        /// <summary>
        /// Сохраняет базу данных в файл
        /// </summary>
        public static void SaveDatabase(List<DramPartNumberEntry> entries)
        {
            try
            {
                EnsureDatabaseDirectoryExists();
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                string json = JsonSerializer.Serialize(entries, options);
                File.WriteAllText(DatabasePath, json);
                _cachedEntries = entries;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения базы DRAM part numbers: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Очищает кэш (для перезагрузки из файла)
        /// </summary>
        public static void ClearCache()
        {
            _cachedEntries = null;
        }

        /// <summary>
        /// Получает путь к файлу базы данных
        /// </summary>
        public static string GetDatabasePath() => DatabasePath;

        private static void EnsureDatabaseDirectoryExists()
        {
            string? directory = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}

