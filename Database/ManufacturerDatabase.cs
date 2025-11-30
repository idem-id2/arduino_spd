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
    /// Модель записи о производителе
    /// </summary>
    public class ManufacturerEntry : INotifyPropertyChanged
    {
        private ushort _id;
        private string _idHex;
        private string _name = string.Empty;
        private string? _notes;
        private int _rowNumber;

        public ManufacturerEntry()
        {
            _idHex = $"{_id:X4}";
        }

        [JsonIgnore]
        public int RowNumber
        {
            get => _rowNumber;
            set { _rowNumber = value; OnPropertyChanged(); }
        }

        [JsonIgnore]
        public ushort Id
        {
            get => _id;
            set 
            { 
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged();
                    
                    // Строго форматируем в 4 символа (XXXX)
                    string formatted = $"{value:X4}";
                    if (_idHex != formatted)
                    {
                        _idHex = formatted;
                        OnPropertyChanged(nameof(IdHex));
                    }
                }
            }
        }

        [JsonIgnore]
        public string IdHex
        {
            get => _idHex;
            set
            {
                if (_idHex != value)
                {
                    _idHex = value;
                    OnPropertyChanged();
                    
                    // Пытаемся распарсить и обновить ID
                    if (ushort.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out ushort parsedId))
                    {
                        // Обновляем ID, что триггернет форматирование в XXXX
                        Id = parsedId;
                        
                        // Если введенное значение было валидным (например "A"), но не в формате XXXX ("000A"),
                        // сеттер Id обновит _idHex. 
                    }
                }
            }
        }

        private bool IsHexMatch(string hexStr, ushort val)
        {
            if (string.IsNullOrWhiteSpace(hexStr)) return false;
            if (ushort.TryParse(hexStr, System.Globalization.NumberStyles.HexNumber, null, out ushort parsed))
            {
                return parsed == val;
            }
            return false;
        }

        [JsonIgnore]
        public byte ContinuationCode => (byte)((_id >> 8) & 0x7F);

        [JsonIgnore]
        public byte ManufacturerCode => (byte)(_id & 0x7F);

        [JsonIgnore]
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        [JsonIgnore]
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
    /// Модель для десериализации JSON файла
    /// </summary>
    internal class ManufacturersJsonRoot
    {
        [JsonPropertyName("manufacturers")]
        public Dictionary<string, string>? Manufacturers { get; set; }
    }

    /// <summary>
    /// Сервис для работы с базой данных производителей
    /// </summary>
    public static class ManufacturerDatabase
    {
        private static readonly string DatabasePath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory,
            "Database",
            "manufacturers.json");

        private static List<ManufacturerEntry>? _cachedEntries;
        private static Dictionary<ushort, string>? _cachedMap;
        private static List<(string DisplayText, string IdHex)>? _cachedComboBoxItems;

        /// <summary>
        /// Загружает базу данных из файла
        /// </summary>
        public static List<ManufacturerEntry> LoadDatabase()
        {
            if (_cachedEntries != null)
                return _cachedEntries;

            EnsureDatabaseDirectoryExists();

            if (!File.Exists(DatabasePath))
            {
                _cachedEntries = new List<ManufacturerEntry>();
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

                var root = JsonSerializer.Deserialize<ManufacturersJsonRoot>(json, options);
                
                _cachedEntries = new List<ManufacturerEntry>();
                
                if (root?.Manufacturers != null)
                {
                    foreach (var kvp in root.Manufacturers)
                    {
                        if (ushort.TryParse(kvp.Key, System.Globalization.NumberStyles.HexNumber, null, out ushort id))
                        {
                            // Store ID as-is from JSON (with parity bits, as per JEDEC standard)
                            _cachedEntries.Add(new ManufacturerEntry
                            {
                                Id = id,
                                Name = kvp.Value ?? string.Empty
                            });
                        }
                    }
                }

                // Сортировка по ID удалена по требованию (показывать "как есть")
                // _cachedEntries = _cachedEntries.OrderBy(e => e.Id).ToList();
                _cachedMap = null; // Reset map cache
                _cachedComboBoxItems = null; // Reset ComboBox items cache
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки базы manufacturers: {ex.Message}");
                _cachedEntries = new List<ManufacturerEntry>();
            }

            return _cachedEntries;
        }

        /// <summary>
        /// Сохраняет базу данных в файл
        /// </summary>
        public static void SaveDatabase(List<ManufacturerEntry> entries)
        {
            try
            {
                EnsureDatabaseDirectoryExists();
                
                var manufacturers = new Dictionary<string, string>();
                foreach (var entry in entries.Where(e => !string.IsNullOrWhiteSpace(e.Name)))
                {
                    string idHex = entry.IdHex;
                    manufacturers[idHex] = entry.Name;
                }

                var root = new ManufacturersJsonRoot
                {
                    Manufacturers = manufacturers
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                
                string json = JsonSerializer.Serialize(root, options);
                File.WriteAllText(DatabasePath, json);
                // Сортировка удалена, сохраняем текущий порядок списка
                _cachedEntries = entries.ToList(); 
                _cachedMap = null; // Reset map cache
                _cachedComboBoxItems = null; // Reset ComboBox items cache
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения базы manufacturers: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Получает словарь для быстрого поиска по ID
        /// </summary>
        public static Dictionary<ushort, string> GetManufacturerMap()
        {
            if (_cachedMap != null)
                return _cachedMap;

            var entries = LoadDatabase();
            _cachedMap = new Dictionary<ushort, string>();
            
            foreach (var entry in entries)
            {
                _cachedMap[entry.Id] = entry.Name;
            }

            return _cachedMap;
        }

        /// <summary>
        /// Получает имя производителя по ID
        /// </summary>
        public static string GetManufacturerName(byte continuationCode, byte manufacturerCode)
        {
            // Use ID with parity bits as per JEDEC standard (as stored in manufacturers.json)
            ushort id = (ushort)((continuationCode << 8) | manufacturerCode);
            
            var map = GetManufacturerMap();
            
            if (map.TryGetValue(id, out var name))
            {
                return name;
            }

            return $"JEDEC ID 0x{id:X4}";
        }

        /// <summary>
        /// Очищает кэш (для перезагрузки из файла)
        /// </summary>
        public static void ClearCache()
        {
            _cachedEntries = null;
            _cachedMap = null;
            _cachedComboBoxItems = null;
        }

        /// <summary>
        /// Получает путь к файлу базы данных
        /// </summary>
        public static string GetDatabasePath() => DatabasePath;

        /// <summary>
        /// Получает список производителей для ComboBox (с кэшированием)
        /// </summary>
        public static List<(string DisplayText, string IdHex)> GetManufacturerComboBoxItems()
        {
            if (_cachedComboBoxItems != null)
                return _cachedComboBoxItems;

            var entries = LoadDatabase();
            var items = new List<(string, string)>(entries.Count);
            
            foreach (var entry in entries.OrderBy(e => e.Name))
            {
                string displayText = $"{entry.Name} (0x{entry.IdHex})";
                items.Add((displayText, entry.IdHex));
            }
            
            _cachedComboBoxItems = items;
            return _cachedComboBoxItems;
        }

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

