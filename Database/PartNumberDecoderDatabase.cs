using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HexEditor.Database
{
    /// <summary>
    /// База данных для декодирования Part Numbers модулей памяти
    /// </summary>
    public static class PartNumberDecoderDatabase
    {
        private static PartNumberDecoderData? _cachedData;

        public static PartNumberDecoderData LoadDatabase()
        {
            if (_cachedData != null)
                return _cachedData;

            try
            {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "part_number_decoder.json");
                if (!File.Exists(jsonPath))
                {
                    // Fallback to embedded or default data
                    return _cachedData = new PartNumberDecoderData();
                }

                string jsonContent = File.ReadAllText(jsonPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                _cachedData = JsonSerializer.Deserialize<PartNumberDecoderData>(jsonContent, options)
                    ?? new PartNumberDecoderData();

                return _cachedData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading PartNumberDecoder database: {ex.Message}");
                return _cachedData = new PartNumberDecoderData();
            }
        }

        public static string? GetSpeedCode(int mhz, string memoryType = "DDR4")
        {
            var data = LoadDatabase();
            if (data.SpeedCodes?.TryGetValue(memoryType, out var codes) == true)
            {
                return codes.TryGetValue(mhz.ToString(), out var code) ? code : null;
            }
            return null;
        }

        public static int? GetSpeedFromCode(string code, string memoryType = "DDR4")
        {
            var data = LoadDatabase();
            if (data.SpeedCodes?.TryGetValue(memoryType, out var codes) == true)
            {
                var entry = codes.FirstOrDefault(kvp => kvp.Value == code);
                if (entry.Key != null && int.TryParse(entry.Key, out int mhz))
                {
                    return mhz;
                }
            }
            return null;
        }

        public static ManufacturerDecoderInfo? GetManufacturerInfo(string manufacturer)
        {
            var data = LoadDatabase();
            return data.Manufacturers?.TryGetValue(manufacturer, out var info) == true ? info : null;
        }
    }

    public class PartNumberDecoderData
    {
        public Dictionary<string, Dictionary<string, string>>? SpeedCodes { get; set; }
        public Dictionary<string, ManufacturerDecoderInfo>? Manufacturers { get; set; }
        public List<PartNumberExample>? Examples { get; set; }
    }

    public class ManufacturerDecoderInfo
    {
        public List<PartNumberPattern>? Patterns { get; set; }
        public Dictionary<string, string>? VoltageCodes { get; set; }
        public Dictionary<string, string>? SeriesMapping { get; set; }
        public Dictionary<string, string>? ConfigCodeExamples { get; set; }
        public Dictionary<string, object>? SuffixDecoding { get; set; }
    }

    public class PartNumberPattern
    {
        public string? Regex { get; set; }
        public string? Description { get; set; }
        public string? Note { get; set; }
        public Dictionary<string, FieldInfo>? Fields { get; set; }
        public SpeedCodeMapping? SpeedCodeMapping { get; set; }
    }

    public class FieldInfo
    {
        public int? Index { get; set; }
        public string? Description { get; set; }
    }

    public class SpeedCodeMapping
    {
        public Dictionary<string, SpeedMappingDetail>? KnownMappings { get; set; }
        public string? Formula { get; set; }
        public string? FormulaNote { get; set; }
        public string? Warning { get; set; }
        public Dictionary<string, object>? Heuristics { get; set; }
        public Dictionary<string, object>? CrossReference { get; set; }
    }

    public class SpeedMappingDetail
    {
        public int Mhz { get; set; }
        public string? Bin { get; set; }
        public string? Note { get; set; }
    }

    public class PartNumberExample
    {
        public string? PartNumber { get; set; }
        public string? Manufacturer { get; set; }
        public Dictionary<string, object>? Decoded { get; set; }
        public Dictionary<string, object>? SpdData { get; set; }
        public string? Source { get; set; }
        public string? Note { get; set; }
    }
}

