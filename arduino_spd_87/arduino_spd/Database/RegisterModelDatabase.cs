using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HexEditor.Database
{
    /// <summary>
    /// Модель записи о регистровом драйвере (RCD/DB)
    /// </summary>
    public sealed class RegisterModelEntry
    {
        [JsonPropertyName("type")]
        public byte Type { get; set; }

        [JsonPropertyName("revision")]
        public byte Revision { get; set; }

        [JsonPropertyName("manufacturer")]
        public string Manufacturer { get; set; } = string.Empty;

        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// База данных известных Register Model (по type+revision из SPD)
    /// </summary>
    public static class RegisterModelDatabase
    {
        private static readonly string DatabasePath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory,
            "Database",
            "register_models.json");

        private static IReadOnlyDictionary<(byte Type, byte Revision), RegisterModelEntry>? _cachedEntries;

        /// <summary>
        /// Возвращает таблицу известных моделей регистров
        /// </summary>
        public static IReadOnlyDictionary<(byte Type, byte Revision), RegisterModelEntry> LoadEntries()
        {
            if (_cachedEntries != null)
                return _cachedEntries;

            EnsureDatabaseExists();

            try
            {
                string json = File.ReadAllText(DatabasePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                var entries = JsonSerializer.Deserialize<List<RegisterModelEntry>>(json, options) ?? new List<RegisterModelEntry>();
                _cachedEntries = entries.ToDictionary(
                    entry => (entry.Type, entry.Revision),
                    entry => entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки register_models.json: {ex.Message}");
                _cachedEntries = new Dictionary<(byte, byte), RegisterModelEntry>();
            }

            return _cachedEntries;
        }

        /// <summary>
        /// Пытается найти модель в базе
        /// </summary>
        public static bool TryGetModel(byte type, byte revision, out RegisterModelEntry? entry)
        {
            var map = LoadEntries();
            return map.TryGetValue((type, revision), out entry);
        }

        /// <summary>
        /// Сбрасывает кэш (при обновлении файла)
        /// </summary>
        public static void ClearCache() => _cachedEntries = null;

        private static void EnsureDatabaseDirectoryExists()
        {
            string? directory = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void EnsureDatabaseExists()
        {
            if (File.Exists(DatabasePath))
                return;

            try
            {
                EnsureDatabaseDirectoryExists();
                var defaultEntries = new List<RegisterModelEntry>
                {
                    new RegisterModelEntry
                    {
                        Type = 0x32,
                        Revision = 0x86,
                        Manufacturer = "Montage Technology",
                        Model = "M88DR4RCD02-PH1",
                        Notes = "Встречается в RDIMM Micron/Samsung (type=0x32, rev=0x86)."
                    },
                    new RegisterModelEntry
                    {
                        Type = 0xB3,
                        Revision = 0x80,
                        Manufacturer = "IDT (Renesas)",
                        Model = "4RCD0232KC1ATG8",
                        Notes = "Встречается в LRDIMM Samsung (type=0xB3, rev=0x80)."
                    }
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(defaultEntries, options);
                File.WriteAllText(DatabasePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка создания register_models.json: {ex.Message}");
            }
        }
    }
}

