using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HexEditor.Database;

namespace HexEditor.SpdDecoder
{
    /// <summary>
    /// UserControl для расшифровки Part Numbers модулей памяти
    /// </summary>
    public partial class PartNumberDecoder : UserControl
    {
        public PartNumberDecoder()
        {
            InitializeComponent();
        }

        private void PartNumberTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Автоматическая расшифровка при вводе (с небольшой задержкой)
        }

        private void PartNumberTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DecodeButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void DecodeButton_Click(object sender, RoutedEventArgs e)
        {
            string partNumber = PartNumberTextBox?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(partNumber))
            {
                ShowNoResults("Please enter a part number to decode.");
                return;
            }

            var result = DecodePartNumber(partNumber);
            DisplayResults(result);
        }

        private PartNumberDecodeResult DecodePartNumber(string partNumber)
        {
            var result = new PartNumberDecodeResult
            {
                OriginalPartNumber = partNumber,
                Manufacturer = "Unknown",
                DecodedFields = new Dictionary<string, string>()
            };

            // Определяем производителя по префиксу
            string upperPart = partNumber.ToUpperInvariant();

            if (upperPart.StartsWith("M") && (upperPart.StartsWith("M3") || upperPart.StartsWith("M4")))
            {
                // Samsung format: M386AAG40BM3, M378A2K43EB1-CWE, etc.
                result.Manufacturer = "Samsung";
                DecodeSamsungPartNumber(partNumber, result);
            }
            else if (upperPart.StartsWith("MT") || upperPart.StartsWith("MTA") || upperPart.StartsWith("M8"))
            {
                // Micron format: MT40A512M16LY-075, MTA18ASF2G72PDZ-2G3B1, etc.
                result.Manufacturer = "Micron";
                DecodeMicronPartNumber(partNumber, result);
            }
            else if (upperPart.StartsWith("HMA") || upperPart.StartsWith("H5") || upperPart.StartsWith("HMC"))
            {
                // Hynix/SK Hynix format: HMA81GU6DJR8N-XN, H5ANAG4NCJR-XNC, etc.
                result.Manufacturer = "SK Hynix";
                DecodeHynixPartNumber(partNumber, result);
            }
            else if (upperPart.StartsWith("K") && upperPart.Length > 2 && char.IsLetter(upperPart[1]))
            {
                // Kingston format: KVR24N17S8/8, KF552C40-16, etc.
                result.Manufacturer = "Kingston";
                DecodeKingstonPartNumber(partNumber, result);
            }
            else if (upperPart.StartsWith("CMK") || upperPart.StartsWith("CML") || upperPart.StartsWith("CMR") || upperPart.StartsWith("CM"))
            {
                // Corsair format: CMK16GX4M2B3200C16, etc.
                result.Manufacturer = "Corsair";
                DecodeCorsairPartNumber(partNumber, result);
            }
            else if (upperPart.StartsWith("F") && char.IsDigit(upperPart[1]))
            {
                // G.Skill format: F4-3200C16D-16GVK, etc.
                result.Manufacturer = "G.Skill";
                DecodeGskillPartNumber(partNumber, result);
            }
            else if (upperPart.StartsWith("CT"))
            {
                // Crucial format: CT16G4DFD832A, etc.
                result.Manufacturer = "Crucial";
                DecodeCrucialPartNumber(partNumber, result);
            }
            else if (upperPart.StartsWith("T-") || upperPart.StartsWith("T"))
            {
                // Team Group format: T-FORCE-VULCAN-Z-16GB-3200, etc.
                result.Manufacturer = "Team Group";
                DecodeTeamGroupPartNumber(partNumber, result);
            }
            else if (upperPart.StartsWith("AX"))
            {
                // ADATA XPG format: AX4U320038G16A-DT60, etc.
                result.Manufacturer = "ADATA";
                DecodeAdataPartNumber(partNumber, result);
            }
            else
            {
                result.DecodedFields["Note"] = "Unknown manufacturer format. Trying generic decoding...";
                DecodeGenericPartNumber(partNumber, result);
            }

            return result;
        }

        private void DecodeSamsungPartNumber(string partNumber, PartNumberDecodeResult result)
        {
            // Format variations:
            // M386AAG40BM3-CWE (AA = config code, two letters)
            // M393A8G40CB4-CWE (A8 = config code, letter + digit)
            // M = Memory Module
            // 386/393 = Series/Family (3xx = DDR4, 4xx = DDR5)
            // AA/A8 = Configuration code (capacity/organization) - can be letters or letter+digit
            // G = Voltage code (G = 1.2V for DDR4, H = 1.1V for DDR5)
            // 40 = Speed code (40 = 3200 MHz or 2933 MHz)
            // B/C = Form factor/type code
            // M3/B4 = Revision/variant

            // Try pattern with config code that can contain digits: M393A8G40CB4-CWE
            // Pattern: M + series(digits) + config(letter + optional digit) + voltage(letter) + speed(digits) + formFactor(letter) + revision(letter+digit) + optional suffix
            var match = Regex.Match(partNumber, @"^M(\d+)([A-Z]\d?)([A-Z])(\d+)([A-Z])([A-Z]\d+)(?:-([A-Z0-9]+))?$", RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                // Try pattern with config code that's only letters: M386AAG40BM3-CWE
                match = Regex.Match(partNumber, @"^M(\d+)([A-Z]+)([A-Z])(\d+)([A-Z])([A-Z0-9]+)(?:-([A-Z0-9]+))?$", RegexOptions.IgnoreCase);
            }

            if (match.Success)
            {
                result.DecodedFields["Format"] = "Samsung Memory Module";

                // Series/Family
                string series = match.Groups[1].Value;
                result.DecodedFields["Series"] = series;
                if (series.StartsWith("3"))
                {
                    result.DecodedFields["Memory Type"] = "DDR4";
                }
                else if (series.StartsWith("4"))
                {
                    result.DecodedFields["Memory Type"] = "DDR5";
                }

                // Загружаем базу данных один раз для всех операций
                var data = PartNumberDecoderDatabase.LoadDatabase();

                // Configuration code (AA, A2, etc.)
                string configCode = match.Groups[2].Value;
                result.DecodedFields["Configuration Code"] = configCode;

                // Попытаемся найти описание конфигурации в базе данных
                if (data.Manufacturers?.TryGetValue("Samsung", out var samsungInfo) == true &&
                    samsungInfo.ConfigCodeExamples?.TryGetValue(configCode, out var configDesc) == true)
                {
                    result.DecodedFields["Configuration Description"] = configDesc;
                }
                else
                {
                    result.DecodedFields["Configuration Note"] = "Capacity and organization code (varies by model)";
                }

                // Voltage code
                string voltageCode = match.Groups[3].Value;
                string voltageDesc = voltageCode.ToUpperInvariant() switch
                {
                    "G" => "1.2V (DDR4 standard)",
                    "H" => "1.1V (DDR5 standard)",
                    "K" => "1.2V (DDR4 standard, alternative code)",
                    _ => $"Unknown voltage code ({voltageCode})"
                };
                result.DecodedFields["Voltage Code"] = $"{voltageCode} = {voltageDesc}";

                // Speed code decoding - используем базу данных и реальные данные
                string speedCode = match.Groups[4].Value;
                string memoryType = series.StartsWith("3") ? "DDR4" : (series.StartsWith("4") ? "DDR5" : "Unknown");

                if (int.TryParse(speedCode, out int speedNum))
                {
                    // Используем только документацию и эвристические правила (без примеров из дампов)
                    int? exactMhz = null;
                    string? speedBinCode = null;
                    string? speedWarning = null;

                    if (memoryType == "DDR4")
                    {
                        // Получаем информацию о Samsung из базы данных (data уже загружена выше)
                        // Используем новое имя переменной, чтобы избежать конфликта с samsungInfo выше
                        var samsungInfoForSpeed = data.Manufacturers?.TryGetValue("Samsung", out var info) == true ? info : null;

                        // Проверяем известные соответствия из документации (speedCodeMapping)
                        if (samsungInfoForSpeed?.Patterns != null && samsungInfoForSpeed.Patterns.Count > 0)
                        {
                            var pattern = samsungInfoForSpeed.Patterns[0];
                            if (pattern.SpeedCodeMapping?.KnownMappings != null)
                            {
                                // Проверяем известные соответствия
                                if (speedNum == 40 && pattern.SpeedCodeMapping.KnownMappings.TryGetValue("40", out var mapping40))
                                {
                                    exactMhz = mapping40.Mhz;
                                    speedBinCode = mapping40.Bin;
                                    speedWarning = mapping40.Note;
                                }
                                else if (speedNum == 40 && pattern.SpeedCodeMapping.KnownMappings.TryGetValue("40_alt", out var mapping40Alt))
                                {
                                    // Альтернативное значение для кода 40
                                    exactMhz = mapping40Alt.Mhz;
                                    speedBinCode = mapping40Alt.Bin;
                                    speedWarning = mapping40Alt.Note;
                                }
                                else if (speedNum == 43 && pattern.SpeedCodeMapping.KnownMappings.TryGetValue("43", out var mapping43))
                                {
                                    exactMhz = mapping43.Mhz;
                                    speedBinCode = mapping43.Bin;
                                    speedWarning = mapping43.Note;
                                }
                            }

                            // Если не нашли в известных соответствиях, используем формулу
                            if (!exactMhz.HasValue && pattern.SpeedCodeMapping?.Formula != null)
                            {
                                // Формула: speedNum * 80 (приблизительно)
                                int approximateMhz = speedNum * 80;
                                exactMhz = approximateMhz;
                                speedBinCode = PartNumberDecoderDatabase.GetSpeedCode(approximateMhz, "DDR4");
                                speedWarning = pattern.SpeedCodeMapping.Warning ?? "⚠ Approximate calculation. Real speed may vary. Check SPD data for exact value.";
                            }
                        }
                        else
                        {
                            // Fallback: используем приблизительную формулу
                            int approximateMhz = speedNum * 80;
                            exactMhz = approximateMhz;
                            speedBinCode = PartNumberDecoderDatabase.GetSpeedCode(approximateMhz, "DDR4");
                            speedWarning = "⚠ Approximate calculation (speedNum * 80). Real speed may vary. Check SPD data for exact value.";
                        }
                    }

                    string speedDesc = $"Speed Code {speedCode}";
                    if (exactMhz.HasValue)
                    {
                        speedDesc += $" = {exactMhz.Value} MHz";
                        if (!string.IsNullOrEmpty(speedBinCode))
                        {
                            speedDesc += $" ({speedBinCode})";
                        }
                        if (memoryType == "DDR4")
                        {
                            speedDesc += $" (PC4-{exactMhz.Value}{speedBinCode ?? ""})";
                        }
                        else if (memoryType == "DDR5")
                        {
                            speedDesc += $" (PC5-{exactMhz.Value})";
                        }

                        result.DecodedFields["Speed"] = speedDesc;

                        // Добавляем предупреждение, если есть
                        if (!string.IsNullOrEmpty(speedWarning))
                        {
                            result.DecodedFields["Speed Warning"] = speedWarning;
                        }
                    }
                    else
                    {
                        speedDesc += " (unknown speed)";
                        result.DecodedFields["Speed"] = speedDesc;
                        result.DecodedFields["Speed Note"] = "Speed code not found in database. Unable to determine exact frequency.";
                    }
                }
                else
                {
                    result.DecodedFields["Speed Code"] = speedCode;
                }

                // Form factor code
                string formFactorCode = match.Groups[5].Value;
                result.DecodedFields["Form Factor Code"] = formFactorCode;

                // Расшифровка form factor кода
                string formFactorDesc = formFactorCode.ToUpperInvariant() switch
                {
                    "B" => "LRDIMM (Load-Reduced DIMM) - Server module with data buffer",
                    "K" => "UDIMM (Unbuffered DIMM) - Desktop module",
                    "C" => "RDIMM (Registered DIMM) - Server module with register",
                    "E" => "UDIMM (Unbuffered DIMM) - Desktop module",
                    "M" => "RDIMM variant",
                    _ => $"Unknown form factor code ({formFactorCode})"
                };
                result.DecodedFields["Form Factor"] = formFactorDesc;

                // Revision/Variant
                string revision = match.Groups[6].Value;
                result.DecodedFields["Revision/Variant"] = revision;

                // Suffix (batch/revision code) - детальная расшифровка
                if (match.Groups[7].Success)
                {
                    string suffix = match.Groups[7].Value;
                    result.DecodedFields["Suffix"] = suffix;

                    // Расшифровка трехбуквенного суффикса (например, CWE)
                    if (suffix.Length == 3)
                    {
                        char revChar = suffix[0];
                        char tempChar = suffix[1];
                        char finishChar = suffix[2];

                        string revDesc = revChar switch
                        {
                            'C' => "3rd revision",
                            'D' => "4th revision",
                            'E' => "5th revision",
                            _ => $"Revision {revChar}"
                        };

                        string tempDesc = tempChar switch
                        {
                            'W' => "Commercial grade (0°C to 95°C)",
                            'C' => "Commercial grade (0°C to 85°C)",
                            'I' => "Industrial grade (-40°C to 85°C)",
                            _ => $"Temperature code {tempChar}"
                        };

                        string finishDesc = finishChar switch
                        {
                            'E' => "Halogen Free",
                            'F' => "Standard finish",
                            'G' => "Other finish",
                            _ => $"Lead finish {finishChar}"
                        };

                        result.DecodedFields["Suffix Breakdown"] = $"{revChar} = {revDesc}, {tempChar} = {tempDesc}, {finishChar} = {finishDesc}";
                    }
                    else
                    {
                        result.DecodedFields["Suffix Note"] = "Manufacturing batch, revision, or location code";
                    }
                }
            }
            else
            {
                // Fallback: try simpler pattern M386AAG40BM3 (without suffix)
                match = Regex.Match(partNumber, @"^M(\d+)([A-Z]+)([A-Z])(\d+)([A-Z])([A-Z0-9]+)$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    // Same decoding as above, but without suffix
                    result.DecodedFields["Format"] = "Samsung Memory Module";
                    result.DecodedFields["Series"] = match.Groups[1].Value;
                    result.DecodedFields["Configuration Code"] = match.Groups[2].Value;

                    string voltageCode = match.Groups[3].Value;
                    string voltageDesc = voltageCode.ToUpperInvariant() switch
                    {
                        "G" => "1.2V (DDR4 standard)",
                        "H" => "1.1V (DDR5 standard)",
                        _ => $"Unknown voltage code ({voltageCode})"
                    };
                    result.DecodedFields["Voltage Code"] = $"{voltageCode} = {voltageDesc}";

                    string speedCode = match.Groups[4].Value;
                    if (int.TryParse(speedCode, out int speedNum))
                    {
                        int mhz = speedNum * 80;
                        result.DecodedFields["Speed"] = $"Speed Code {speedCode} = {mhz} MHz";
                    }

                    result.DecodedFields["Form Factor Code"] = match.Groups[5].Value;
                    result.DecodedFields["Revision/Variant"] = match.Groups[6].Value;
                }
                else
                {
                    // Fallback: minimal info
                    result.DecodedFields["Format"] = "Samsung (simplified)";
                    result.DecodedFields["Raw"] = partNumber;
                    result.DecodedFields["Note"] = "Could not fully decode. This may be a custom or non-standard format.";
                }
            }
        }

        private void DecodeMicronPartNumber(string partNumber, PartNumberDecodeResult result)
        {
            // Format variations:
            // 144ASQ16G72LSZ-2S9E1 (Server module format)
            // MT40A512M16LY-075 (Standard format)
            // MTA18ASF2G72PDZ-2G3B1 (MTA format)

            // Try server module format: 144ASQ16G72LSZ-2S9E1
            var serverMatch = Regex.Match(partNumber, @"^(\d+)([A-Z]+)(\d+)([A-Z])(\d+)([A-Z]+)(?:-([A-Z0-9]+))?$", RegexOptions.IgnoreCase);
            if (serverMatch.Success)
            {
                result.DecodedFields["Format"] = "Micron Server Module Format";

                string packageCode = serverMatch.Groups[1].Value;
                string familyCode = serverMatch.Groups[2].Value;
                string densityCode = serverMatch.Groups[3].Value;
                string generationCode = serverMatch.Groups[4].Value;
                string busWidthCode = serverMatch.Groups[5].Value;
                string featuresCode = serverMatch.Groups[6].Value;
                string suffix = serverMatch.Groups[7].Success ? serverMatch.Groups[7].Value : "";

                // Package
                if (packageCode == "144")
                {
                    result.DecodedFields["Package"] = "144-pin FBGA package";
                }
                else
                {
                    result.DecodedFields["Package"] = $"{packageCode}-pin package";
                }

                // Product Family
                if (familyCode == "ASQ")
                {
                    result.DecodedFields["Product Family"] = "Server RDIMM (ASQ = Server/Enterprise)";
                }
                else
                {
                    result.DecodedFields["Product Family"] = $"Product Family: {familyCode}";
                }

                // Density
                if (int.TryParse(densityCode, out int density))
                {
                    result.DecodedFields["Density"] = $"{density}Gb per component";
                    result.DecodedFields["Density Description"] = $"Each DRAM component has {density}Gb capacity";
                }

                // Generation
                if (generationCode == "G")
                {
                    result.DecodedFields["Memory Type"] = "DDR4";
                    result.DecodedFields["Generation"] = "DDR4 (G = DDR4 generation)";
                }

                // Bus Width
                if (int.TryParse(busWidthCode, out int busWidth))
                {
                    if (busWidth == 72)
                    {
                        result.DecodedFields["Bus Width"] = "72-bit module (64 data + 8 ECC bits)";
                        result.DecodedFields["ECC"] = "Yes (8-bit ECC support)";
                    }
                    else
                    {
                        result.DecodedFields["Bus Width"] = $"{busWidth}-bit module";
                    }
                }

                // Features
                if (featuresCode.Length >= 1)
                {
                    char halogenCode = featuresCode[0];
                    if (halogenCode == 'L')
                    {
                        result.DecodedFields["Halogen"] = "Low Halogen (environmentally friendly, RoHS compliant)";
                    }
                }

                if (featuresCode.Length >= 2)
                {
                    string packageDetail = featuresCode.Substring(1);
                    if (packageDetail == "SZ")
                    {
                        result.DecodedFields["Package Details"] = "1.0mm pitch FBGA package";
                    }
                    else
                    {
                        result.DecodedFields["Package Details"] = $"Package: {packageDetail}";
                    }
                }

                // Suffix decoding: -2S9E1
                if (!string.IsNullOrEmpty(suffix) && suffix.Length >= 4)
                {
                    // Format: [DensityCode][PowerCode][SpeedCode][TempCode][Revision]
                    char densitySuffix = suffix[0];
                    char powerCode = suffix.Length > 1 ? suffix[1] : ' ';
                    char speedCode = suffix.Length > 2 ? suffix[2] : ' ';
                    char tempCode = suffix.Length > 3 ? suffix[3] : ' ';
                    char revisionCode = suffix.Length > 4 ? suffix[4] : ' ';

                    // Density from suffix
                    if (densitySuffix == '2')
                    {
                        result.DecodedFields["Suffix Density"] = "16Gb density per die (from suffix)";
                    }

                    // Power
                    if (powerCode == 'S')
                    {
                        result.DecodedFields["Power"] = "Standard Power (1.2V DDR4)";
                    }

                    // Speed
                    if (char.IsDigit(speedCode))
                    {
                        int speedNum = speedCode - '0';
                        int mhz = speedNum switch
                        {
                            9 => 2933,
                            8 => 2666,
                            7 => 2400,
                            _ => speedNum * 333 // Approximate
                        };
                        result.DecodedFields["Speed"] = $"DDR4-{mhz} (PC4-{mhz * 8})";
                        result.DecodedFields["Speed Code"] = $"Speed code {speedCode} = {mhz} MHz";
                    }

                    // Temperature
                    string tempDesc = tempCode switch
                    {
                        'E' => "Extended temperature range (0°C to 95°C)",
                        'C' => "Commercial temperature range (0°C to 85°C)",
                        'I' => "Industrial temperature range (-40°C to 95°C)",
                        _ => $"Temperature code: {tempCode}"
                    };
                    result.DecodedFields["Temperature Range"] = tempDesc;

                    // Revision
                    if (char.IsDigit(revisionCode))
                    {
                        result.DecodedFields["Revision"] = $"Revision {revisionCode}";
                    }
                }

                // Additional notes
                result.DecodedFields["Application"] = "Server/Enterprise memory modules";
                result.DecodedFields["Note"] = "Micron server modules typically support ECC, extended temperature ranges, and optimized timing for server platforms.";
            }
            else
            {
                // Other Micron formats (MT, MTA, etc.)
                result.DecodedFields["Format"] = "Micron";
                result.DecodedFields["Raw"] = partNumber;
                result.DecodedFields["Note"] = "Micron part number decoding - format analysis needed. Supported formats: Server module (144ASQ16G72LSZ-2S9E1), Standard (MT), MTA.";
            }
        }

        private void DecodeHynixPartNumber(string partNumber, PartNumberDecodeResult result)
        {
            // Format variations:
            // HMAA8GR7CJR4N-XN (HMA format - Module part number)
            // H5ANAG4NCJR-XNC (H5 format - DRAM component part number)
            // HMC format (other variants)

            string upperPart = partNumber.ToUpperInvariant();

            // Try HMA format: HMAA8GR7CJR4N-XN or HMA81GU6DJR8N-XN
            // Structure analysis based on examples:
            // HMA81GU6DJR8N-XN = 8GB UDIMM DDR4-3200AA, 1 rank, D-die
            // HMAA8GR7CJR4N-XN = ? capacity, RDIMM?, DDR4, R7 speed, J-die, 4 ranks?
            // 
            // Pattern: HMA + [Capacity][Generation][Speed/Org][Die][Ranks][Voltage] + suffix
            // HMA = Hynix Memory Module
            // Capacity codes: A8, 81, etc.
            // Generation: G = DDR4, H = DDR5
            // Speed/Org: U6, R7, etc. (U = UDIMM?, R = RDIMM?, number = speed code)
            // Die: D = D-die, J = J-die, C = C-die, M = M-die
            // Ranks: R8 = 8 ranks?, R4 = 4 ranks?, R1 = 1 rank?
            // Voltage: N = 1.2V DDR4
            // Suffix: -XN = temperature + lead finish

            var hmaMatch = Regex.Match(partNumber, @"^HMA([A-Z0-9]+)(?:-([A-Z0-9]+))?$", RegexOptions.IgnoreCase);
            if (hmaMatch.Success)
            {
                result.DecodedFields["Format"] = "SK Hynix HMA Format (Module Part Number)";

                string code = hmaMatch.Groups[1].Value;
                string suffix = hmaMatch.Groups[2].Success ? hmaMatch.Groups[2].Value : "";

                // Generation code (usually G for DDR4, H for DDR5)
                if (code.Contains("G"))
                {
                    result.DecodedFields["Memory Type"] = "DDR4";
                    result.DecodedFields["Generation"] = "DDR4 (G = DDR4 generation)";
                }
                else if (code.Contains("H"))
                {
                    result.DecodedFields["Memory Type"] = "DDR5";
                    result.DecodedFields["Generation"] = "DDR5 (H = DDR5 generation)";
                }

                // Voltage code (usually N for 1.2V DDR4, appears before suffix)
                if (code.EndsWith("N") && !string.IsNullOrEmpty(suffix))
                {
                    result.DecodedFields["Voltage Code"] = "N = 1.2V (DDR4 standard)";
                    result.DecodedFields["Voltage"] = "1.2V (DDR4 standard)";
                }

                // Extract architecture and density codes
                // Pattern: AA8, 81, etc.
                var archDensityMatch = Regex.Match(code, @"^([A-Z]{1,2})(\d+)", RegexOptions.IgnoreCase);
                if (archDensityMatch.Success)
                {
                    string archCode = archDensityMatch.Groups[1].Value;
                    string density1Code = archDensityMatch.Groups[2].Value;

                    result.DecodedFields["Architecture Code"] = archCode;
                    result.DecodedFields["Density Code 1"] = density1Code;

                    // Architecture
                    if (archCode == "AA")
                    {
                        result.DecodedFields["Architecture"] = "Monolithic (AA = Monolithic architecture)";
                    }
                    else if (archCode == "A")
                    {
                        result.DecodedFields["Architecture"] = "Monolithic (A = Monolithic architecture)";
                    }

                    // Density per chip (first density code)
                    if (int.TryParse(density1Code, out int chipDensity))
                    {
                        result.DecodedFields["Chip Density"] = $"{chipDensity}GB per chip";
                    }
                }

                // Extract second density code (after die revision, before voltage N)
                // Pattern: ...JR4N where 4 = 32Gb density
                var density2Match = Regex.Match(code, @"[JCDM]R(\d+)N", RegexOptions.IgnoreCase);
                if (density2Match.Success && density2Match.Groups.Count > 1)
                {
                    string density2Code = density2Match.Groups[1].Value;
                    result.DecodedFields["Density Code 2"] = density2Code;

                    // Density per die in Gb
                    if (int.TryParse(density2Code, out int dieDensity))
                    {
                        string densityDesc = dieDensity switch
                        {
                            4 => "32Gb (4GB) per die",
                            8 => "8Gb (1GB) per die",
                            16 => "16Gb (2GB) per die",
                            _ => $"{dieDensity}Gb per die"
                        };
                        result.DecodedFields["Die Density"] = densityDesc;
                        result.DecodedFields["Die Density Note"] = "This is the actual DRAM die density. Critical for SPD programming!";
                    }
                }

                // Form factor and prefetch code (R7, U6, etc.)
                // Pattern: After generation code G, we look for U or R followed by digit
                // R = RDIMM?, U = UDIMM?, number = Prefetch (7 = 8n, 6 = 8n)
                var formFactorMatch = Regex.Match(code, @"G([UR])(\d+)", RegexOptions.IgnoreCase);
                if (formFactorMatch.Success)
                {
                    string formFactorCode = formFactorMatch.Groups[1].Value;
                    string prefetchCode = formFactorMatch.Groups[2].Value;

                    // Form factor
                    if (formFactorCode == "U")
                    {
                        result.DecodedFields["Form Factor"] = "UDIMM (Unbuffered DIMM)";
                        result.DecodedFields["Form Factor Code"] = "U";
                    }
                    else if (formFactorCode == "R")
                    {
                        result.DecodedFields["Form Factor"] = "RDIMM (Registered DIMM) or LRDIMM";
                        result.DecodedFields["Form Factor Code"] = "R";
                    }

                    // Prefetch code
                    if (int.TryParse(prefetchCode, out int prefetch))
                    {
                        result.DecodedFields["Prefetch Code"] = prefetchCode;
                        result.DecodedFields["Prefetch"] = "8n (standard DDR4 prefetch)";
                    }
                }

                // Interface code (C, etc.) - appears after prefetch
                var interfaceMatch = Regex.Match(code, @"[UR]\d+([A-Z])", RegexOptions.IgnoreCase);
                if (interfaceMatch.Success)
                {
                    string interfaceCode = interfaceMatch.Groups[1].Value;
                    result.DecodedFields["Interface Code"] = interfaceCode;

                    if (interfaceCode == "C")
                    {
                        result.DecodedFields["Interface"] = "x4 or x8 organization";
                        result.DecodedFields["Interface Note"] = "C code indicates x4/x8 interface. Verify from SPD data for exact organization.";
                    }
                }

                // Die revision (J, C, D, M, etc.) - usually appears after speed code
                if (code.Contains("J"))
                {
                    result.DecodedFields["Die Revision"] = "J-die (SK Hynix J-die revision)";
                }
                else if (code.Contains("C"))
                {
                    result.DecodedFields["Die Revision"] = "C-die (SK Hynix C-die revision)";
                }
                else if (code.Contains("D"))
                {
                    result.DecodedFields["Die Revision"] = "D-die (SK Hynix D-die revision)";
                }
                else if (code.Contains("M"))
                {
                    result.DecodedFields["Die Revision"] = "M-die (SK Hynix M-die revision)";
                }

                // Ranks (R8, R4, R1, etc.) - appears after die code
                // IMPORTANT: For UDIMM modules, "R8" in HMA81GU6DJR8N-XN does NOT mean 8 ranks!
                // For 8GB UDIMM with 8 chips × 8Gb = Single Rank (1 rank)
                // R8 might be revision code or other identifier, not ranks count
                string formFactor = result.DecodedFields.GetValueOrDefault("Form Factor", "");
                bool isUDIMM = formFactor.Contains("UDIMM", StringComparison.OrdinalIgnoreCase);
                
                // For UDIMM, determine ranks based on capacity and chip count
                // 8GB UDIMM with 8 chips × 8Gb = typically Single Rank
                // 16GB UDIMM with 8 chips × 16Gb = typically Single Rank
                // 16GB UDIMM with 16 chips × 8Gb = typically Dual Rank
                string capacityCode = result.DecodedFields.GetValueOrDefault("Density Code 2", "");
                string dieDensity = result.DecodedFields.GetValueOrDefault("Die Density", "");
                
                int ranks = 1; // Default: Single Rank for UDIMM
                
                // Try to extract from pattern, but validate for UDIMM
                var ranksMatch = Regex.Match(code, @"[JCDM]R(\d+)", RegexOptions.IgnoreCase);
                if (ranksMatch.Success && ranksMatch.Groups.Count > 1)
                {
                    string ranksCode = ranksMatch.Groups[1].Value;
                    if (int.TryParse(ranksCode, out int extractedRanks) && extractedRanks >= 1 && extractedRanks <= 8)
                    {
                        // For UDIMM, R8 likely means revision, not 8 ranks
                        // Only use if it makes sense (1-2 ranks for typical UDIMM)
                        if (isUDIMM && extractedRanks <= 2)
                        {
                            ranks = extractedRanks;
                        }
                        else if (!isUDIMM)
                        {
                            // For RDIMM/LRDIMM, R8 could mean 8 ranks
                            ranks = extractedRanks;
                        }
                    }
                }
                
                // Smart detection for UDIMM based on capacity
                if (isUDIMM && ranks == 1)
                {
                    // 8GB UDIMM = typically Single Rank
                    // 16GB UDIMM = could be Single or Dual Rank depending on chip density
                    if (capacityCode == "81" || capacityCode == "8")
                    {
                        ranks = 1; // 8GB = Single Rank
                    }
                    else if (capacityCode == "A8" || capacityCode == "16")
                    {
                        // 16GB could be Single (16Gb chips) or Dual (8Gb chips)
                        if (dieDensity.Contains("16Gb") || dieDensity.Contains("32Gb"))
                            ranks = 1; // Single Rank with high-density chips
                        else
                            ranks = 2; // Dual Rank with 8Gb chips
                    }
                }
                
                result.DecodedFields["Ranks"] = $"{ranks} Rank{(ranks > 1 ? "s" : "")} ({(ranks == 1 ? "Single" : ranks == 2 ? "Dual" : ranks == 4 ? "Quad" : "Multi")} Rank)";
                if (isUDIMM && code.Contains("R8"))
                {
                    result.DecodedFields["Ranks Note"] = "Note: 'R8' in part number may indicate revision, not ranks. For 8GB UDIMM, typically Single Rank.";
                }

                // Suffix decoding (-XN)
                // Format: [Revision/Process][Speed Grade]
                // -XN = Revision X, Speed Grade N (DDR4-3200)
                // -VN = Revision V, Speed Grade N (DDR4-2933)
                // -UN = Revision U, Speed Grade N (DDR4-2666)
                if (!string.IsNullOrEmpty(suffix) && suffix.Length >= 2)
                {
                    char revisionCode = suffix[0];
                    char speedGradeCode = suffix[1];

                    // Revision/Process
                    result.DecodedFields["Revision/Process"] = $"Revision {revisionCode}";

                    // Speed Grade (second character in suffix)
                    string speedDesc = speedGradeCode switch
                    {
                        'N' => "DDR4-3200 (PC4-25600)",
                        'V' => "DDR4-2933 (PC4-23400)",
                        'U' => "DDR4-2666 (PC4-21300)",
                        _ => $"Speed Grade: {speedGradeCode}"
                    };
                    result.DecodedFields["Speed Grade"] = speedDesc;

                    // Extract MHz from speed grade
                    int speedMhz = speedGradeCode switch
                    {
                        'N' => 3200,
                        'V' => 2933,
                        'U' => 2666,
                        _ => 0
                    };

                    if (speedMhz > 0)
                    {
                        result.DecodedFields["Speed"] = $"{speedMhz} MHz ({speedDesc})";
                        string speedBin = PartNumberDecoderDatabase.GetSpeedCode(speedMhz, "DDR4") ?? "";
                        if (!string.IsNullOrEmpty(speedBin))
                        {
                            result.DecodedFields["Speed Bin"] = speedBin;
                        }
                    }

                    result.DecodedFields["Suffix"] = suffix;
                    result.DecodedFields["Suffix Breakdown"] = $"{revisionCode} = Revision/Process, {speedGradeCode} = {speedDesc}";
                }

                result.DecodedFields["Note"] = "SK Hynix HMA format uses compact encoding. Some parameters are estimated based on common patterns. Verify from SPD data for exact values.";
            }
            else
            {
                // Try H5 format: H5ANAG4NCJR-XNC
                var h5Match = Regex.Match(partNumber, @"^H5([A-Z0-9]+)(?:-([A-Z0-9]+))?$", RegexOptions.IgnoreCase);
                if (h5Match.Success)
                {
                    result.DecodedFields["Format"] = "SK Hynix H5 Format (DRAM Component Part Number)";
                    result.DecodedFields["Note"] = "H5 format is for DRAM component part numbers, not module part numbers. Module part numbers use HMA format.";
                }
                else
                {
                    result.DecodedFields["Format"] = "SK Hynix";
                    result.DecodedFields["Raw"] = partNumber;
                    result.DecodedFields["Note"] = "SK Hynix part number format not fully recognized. Supported formats: HMA (module), H5 (component).";
                }
            }
        }

        private void DecodeKingstonPartNumber(string partNumber, PartNumberDecodeResult result)
        {
            // Format variations:
            // HP32D4R2D4HCI-64 (HP Series Server format)
            // KVR24N17S8/8 (ValueRAM format)
            // KF552C40-16 (Fury format)

            string upperPart = partNumber.ToUpperInvariant();

            // Try HP Series format: HP32D4R2D4HCI-64
            var hpMatch = Regex.Match(partNumber, @"^HP(\d+)([A-Z])(\d+)([A-Z])(\d+)([A-Z])(\d+)([A-Z]+)([A-Z])(?:-(\d+))?$", RegexOptions.IgnoreCase);
            if (hpMatch.Success)
            {
                result.DecodedFields["Format"] = "Kingston HP Series (Server/Enterprise)";

                string speedCode = hpMatch.Groups[1].Value;
                string d4_1 = hpMatch.Groups[2].Value;
                string voltageCode = hpMatch.Groups[3].Value;
                string typeCode = hpMatch.Groups[4].Value;
                string ranksCode = hpMatch.Groups[5].Value;
                string d4_2 = hpMatch.Groups[6].Value;
                string orgCode = hpMatch.Groups[7].Value;
                string controllerCode = hpMatch.Groups[8].Value;
                string interfaceCode = hpMatch.Groups[9].Value;
                string capacityCode = hpMatch.Groups[10].Success ? hpMatch.Groups[10].Value : "";

                // Series
                result.DecodedFields["Series"] = "HP";
                result.DecodedFields["Series Description"] = "HP Series - Server memory for HP ProLiant and similar servers";

                // Speed
                if (int.TryParse(speedCode, out int speedNum))
                {
                    // HP32 typically means 3200 MHz
                    int mhz = speedNum * 100; // 32 = 3200 MHz
                    result.DecodedFields["Speed Code"] = speedCode;
                    result.DecodedFields["Speed"] = $"Speed Code {speedCode} = {mhz} MHz (PC4-{mhz})";
                }

                // Generation
                if (d4_1 == "D" && voltageCode == "4")
                {
                    result.DecodedFields["Memory Type"] = "DDR4";
                    result.DecodedFields["Voltage"] = "1.2V (DDR4 standard)";
                }

                // Type
                if (typeCode == "R")
                {
                    result.DecodedFields["Form Factor"] = "RDIMM (Registered DIMM)";
                }

                // Ranks
                if (int.TryParse(ranksCode, out int ranks))
                {
                    result.DecodedFields["Ranks"] = $"{ranks} Rank{(ranks > 1 ? "s" : "")} ({(ranks == 1 ? "Single" : ranks == 2 ? "Dual" : "Multi")} Rank)";
                }

                // Organization
                if (orgCode == "4")
                {
                    result.DecodedFields["Organization"] = "x4 (server architecture)";
                }

                // Controller
                if (controllerCode == "HC")
                {
                    result.DecodedFields["Controller"] = "Hybrid Controller (possibly LRDIMM variant)";
                }

                // Capacity
                if (!string.IsNullOrEmpty(capacityCode) && int.TryParse(capacityCode, out int capacity))
                {
                    result.DecodedFields["Capacity"] = $"{capacity} GB";
                    result.DecodedFields["Capacity Note"] = $"High-density module. Likely uses {(capacity >= 64 ? "32Gb" : "16Gb")} chips per component.";
                }

                // Additional notes
                result.DecodedFields["Application"] = "HP ProLiant servers and compatible platforms";
                result.DecodedFields["Note"] = "HP series modules may have HP-specific timing and register settings for optimal compatibility.";
            }
            else
            {
                // Other Kingston formats (KVR, KF, etc.)
                result.DecodedFields["Format"] = "Kingston";
                result.DecodedFields["Raw"] = partNumber;
                result.DecodedFields["Note"] = "Kingston part number decoding - format analysis needed. Supported formats: HP series (HP32D4R2D4HCI-64), ValueRAM (KVR), Fury (KF).";
            }
        }

        private void DecodeCorsairPartNumber(string partNumber, PartNumberDecodeResult result)
        {
            // Format: CMK16GX4M2B3200C16 or CMK16GX4M2B3200C16W
            // CM = Corsair Memory
            // K = Vengeance LPX series (L = RGB, R = Pro)
            // 16 = Capacity in GB
            // GX4 = DDR4 (GX5 = DDR5)
            // M = Multi-kit (S = Single)
            // 2 = Kit size (2 modules)
            // B = DIMM (S = SO-DIMM)
            // 3200 = Speed in MHz
            // C16 = CAS Latency 16
            // W = Optional variant (W = White, B = Black, etc.)

            var match = Regex.Match(partNumber, @"^CM([A-Z])(\d+)GX(\d+)M(\d+)([A-Z])(\d+)([A-Z])(\d+)(?:([A-Z]))?$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                result.DecodedFields["Format"] = "Corsair Memory Module";

                string seriesCode = match.Groups[1].Value;
                string capacityCode = match.Groups[2].Value;
                string generationCode = match.Groups[3].Value;
                string kitTypeCode = match.Groups[4].Value;
                string kitSizeCode = match.Groups[5].Value;
                string speedCode = match.Groups[6].Value;
                string casCode = match.Groups[7].Value;
                string casLatency = match.Groups[8].Value;
                string variantCode = match.Groups[9].Success ? match.Groups[9].Value : "";

                // Series
                string seriesName = seriesCode.ToUpperInvariant() switch
                {
                    "K" => "Vengeance LPX",
                    "L" => "Vengeance RGB",
                    "R" => "Vengeance Pro",
                    _ => $"Series {seriesCode}"
                };
                result.DecodedFields["Series"] = seriesName;

                // Capacity
                if (int.TryParse(capacityCode, out int capacity))
                {
                    result.DecodedFields["Capacity"] = $"{capacity} GB";
                    result.DecodedFields["Capacity Note"] = $"Per-module capacity. Kit contains {kitSizeCode} modules = {capacity * int.Parse(kitSizeCode)} GB total.";
                }

                // Generation
                if (generationCode == "4")
                {
                    result.DecodedFields["Memory Type"] = "DDR4";
                }
                else if (generationCode == "5")
                {
                    result.DecodedFields["Memory Type"] = "DDR5";
                }

                // Kit information
                result.DecodedFields["Kit Type"] = kitTypeCode == "M" ? "Multi-module kit" : "Single module";
                if (int.TryParse(kitSizeCode, out int kitSize))
                {
                    result.DecodedFields["Kit Size"] = $"{kitSize} modules";
                }

                // Form factor
                string formFactor = kitTypeCode.ToUpperInvariant() switch
                {
                    "B" => "DIMM",
                    "S" => "SO-DIMM",
                    _ => "Unknown"
                };
                result.DecodedFields["Form Factor"] = formFactor;

                // Speed
                if (int.TryParse(speedCode, out int speed))
                {
                    result.DecodedFields["Speed"] = $"{speed} MHz";
                    if (generationCode == "4")
                    {
                        result.DecodedFields["Speed Label"] = $"PC4-{speed}";
                    }
                    else if (generationCode == "5")
                    {
                        result.DecodedFields["Speed Label"] = $"PC5-{speed}";
                    }
                }

                // CAS Latency
                if (int.TryParse(casLatency, out int cas))
                {
                    result.DecodedFields["CAS Latency"] = $"CL{cas}";
                }

                // Variant
                if (!string.IsNullOrEmpty(variantCode))
                {
                    string variantName = variantCode.ToUpperInvariant() switch
                    {
                        "W" => "White",
                        "B" => "Black",
                        "R" => "Red",
                        _ => variantCode
                    };
                    result.DecodedFields["Variant"] = variantName;
                }

                result.DecodedFields["Application"] = "Gaming/Enthusiast";
                result.DecodedFields["Note"] = "Corsair modules typically include XMP profiles for overclocking.";
            }
            else
            {
                result.DecodedFields["Format"] = "Corsair";
                result.DecodedFields["Raw"] = partNumber;
                result.DecodedFields["Note"] = "Corsair part number format not fully recognized. Supported format: CMK16GX4M2B3200C16";
            }
        }

        private void DecodeGskillPartNumber(string partNumber, PartNumberDecodeResult result)
        {
            // Format: F4-3200C16D-16GVK
            // F = G.Skill
            // 4 = DDR4 (5 = DDR5)
            // 3200 = Speed in MHz
            // C16 = CAS Latency 16
            // D = Dual channel kit (S = Single)
            // 16 = Total kit capacity in GB
            // G = Gaming series (R = Ripjaws, T = Trident)
            // VK = Variant code

            var match = Regex.Match(partNumber, @"^F(\d+)-(\d+)([A-Z])(\d+)([A-Z])-(\d+)([A-Z]+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                result.DecodedFields["Format"] = "G.Skill Memory Module";

                string generationCode = match.Groups[1].Value;
                string speedCode = match.Groups[2].Value;
                string casPrefix = match.Groups[3].Value;
                string casLatency = match.Groups[4].Value;
                string kitTypeCode = match.Groups[5].Value;
                string capacityCode = match.Groups[6].Value;
                string seriesVariant = match.Groups[7].Value;

                // Generation
                if (generationCode == "4")
                {
                    result.DecodedFields["Memory Type"] = "DDR4";
                }
                else if (generationCode == "5")
                {
                    result.DecodedFields["Memory Type"] = "DDR5";
                }

                // Speed
                if (int.TryParse(speedCode, out int speed))
                {
                    result.DecodedFields["Speed"] = $"{speed} MHz";
                }

                // CAS Latency
                if (int.TryParse(casLatency, out int cas))
                {
                    result.DecodedFields["CAS Latency"] = $"CL{cas}";
                }

                // Kit type
                result.DecodedFields["Kit Type"] = kitTypeCode == "D" ? "Dual channel kit" : "Single module";

                // Capacity
                if (int.TryParse(capacityCode, out int totalCapacity))
                {
                    int modulesPerKit = kitTypeCode == "D" ? 2 : 1;
                    int perModule = totalCapacity / modulesPerKit;
                    result.DecodedFields["Capacity"] = $"{perModule} GB per module ({totalCapacity} GB total kit)";
                }

                // Series
                if (seriesVariant.Length > 0)
                {
                    char seriesChar = seriesVariant[0];
                    string seriesName = seriesChar switch
                    {
                        'G' => "Gaming series",
                        'R' => "Ripjaws series",
                        'T' => "Trident Z series",
                        'F' => "Flare X series (AMD optimized)",
                        _ => $"Series {seriesChar}"
                    };
                    result.DecodedFields["Series"] = seriesName;
                }

                result.DecodedFields["Application"] = "Gaming/Overclocking";
                result.DecodedFields["Note"] = "G.Skill specializes in high-performance, overclocking-oriented memory modules.";
            }
            else
            {
                result.DecodedFields["Format"] = "G.Skill";
                result.DecodedFields["Raw"] = partNumber;
                result.DecodedFields["Note"] = "G.Skill part number format not fully recognized.";
            }
        }

        private void DecodeCrucialPartNumber(string partNumber, PartNumberDecodeResult result)
        {
            // Format: CT16G4DFD832A
            // CT = Crucial Technology
            // 16 = Capacity in GB
            // G4 = DDR4 (G5 = DDR5)
            // DF = Desktop form factor (SF = SO-DIMM)
            // D = Additional code
            // 832 = Speed code (encoded, 832 ≈ 3200 MHz)
            // A = Variant

            var match = Regex.Match(partNumber, @"^CT(\d+)G(\d+)([A-Z]+)(\d+)([A-Z])$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                result.DecodedFields["Format"] = "Crucial Technology Memory Module";

                string capacityCode = match.Groups[1].Value;
                string generationCode = match.Groups[2].Value;
                string formFactorCode = match.Groups[3].Value;
                string speedCode = match.Groups[4].Value;
                string variantCode = match.Groups[5].Value;

                // Capacity
                if (int.TryParse(capacityCode, out int capacity))
                {
                    result.DecodedFields["Capacity"] = $"{capacity} GB";
                }

                // Generation
                if (generationCode == "4")
                {
                    result.DecodedFields["Memory Type"] = "DDR4";
                }
                else if (generationCode == "5")
                {
                    result.DecodedFields["Memory Type"] = "DDR5";
                }

                // Form factor
                if (formFactorCode.Contains("DF"))
                {
                    result.DecodedFields["Form Factor"] = "DIMM (Desktop)";
                }
                else if (formFactorCode.Contains("SF"))
                {
                    result.DecodedFields["Form Factor"] = "SO-DIMM (Laptop)";
                }

                // Speed (encoded, approximate)
                if (int.TryParse(speedCode, out int speedEncoded))
                {
                    // Crucial speed codes are encoded differently
                    // 832 might mean 3200 MHz (approximate)
                    int approximateSpeed = speedEncoded / 4 * 15; // Rough approximation
                    result.DecodedFields["Speed"] = $"~{approximateSpeed} MHz (encoded code: {speedCode})";
                    result.DecodedFields["Speed Note"] = "Crucial speed codes use encoding. Verify from SPD data for exact speed.";
                }

                result.DecodedFields["Application"] = "Desktop/Laptop";
                result.DecodedFields["Note"] = "Crucial is Micron's consumer brand. Known for reliability and compatibility.";
            }
            else
            {
                result.DecodedFields["Format"] = "Crucial";
                result.DecodedFields["Raw"] = partNumber;
                result.DecodedFields["Note"] = "Crucial part number format not fully recognized.";
            }
        }

        private void DecodeTeamGroupPartNumber(string partNumber, PartNumberDecodeResult result)
        {
            // Format variations: T-FORCE-VULCAN-Z-16GB-3200, T-FORCE-DELTA-RGB-16GB-3200
            result.DecodedFields["Format"] = "Team Group";
            result.DecodedFields["Raw"] = partNumber;
            result.DecodedFields["Note"] = "Team Group part number decoding - format analysis needed. Common formats include explicit capacity and speed.";
        }

        private void DecodeAdataPartNumber(string partNumber, PartNumberDecodeResult result)
        {
            // Format: AX4U320038G16A-DT60
            // AX = ADATA XPG
            // 4 = DDR4 (5 = DDR5)
            // U = Series code
            // 3200 = Speed in MHz
            // 8 = Capacity in GB (or 16, 32, etc.)
            // G = Additional code
            // 16 = CAS Latency
            // A = Variant
            // -DT60 = Additional suffix

            var match = Regex.Match(partNumber, @"^AX(\d+)([A-Z]+)(\d+)(\d+)([A-Z]+)(\d+)([A-Z])(?:-([A-Z0-9]+))?$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                result.DecodedFields["Format"] = "ADATA XPG Memory Module";

                string generationCode = match.Groups[1].Value;
                string seriesCode = match.Groups[2].Value;
                string speedCode = match.Groups[3].Value;
                string capacityCode = match.Groups[4].Value;
                string casCode = match.Groups[5].Value;
                string casLatency = match.Groups[6].Value;
                string variantCode = match.Groups[7].Value;
                string suffix = match.Groups[8].Success ? match.Groups[8].Value : "";

                // Generation
                if (generationCode == "4")
                {
                    result.DecodedFields["Memory Type"] = "DDR4";
                }
                else if (generationCode == "5")
                {
                    result.DecodedFields["Memory Type"] = "DDR5";
                }

                // Speed
                if (int.TryParse(speedCode, out int speed))
                {
                    result.DecodedFields["Speed"] = $"{speed} MHz";
                }

                // Capacity
                if (int.TryParse(capacityCode, out int capacity))
                {
                    result.DecodedFields["Capacity"] = $"{capacity} GB";
                }

                // CAS Latency
                if (int.TryParse(casLatency, out int cas))
                {
                    result.DecodedFields["CAS Latency"] = $"CL{cas}";
                }

                result.DecodedFields["Series"] = "XPG (Gaming)";
                result.DecodedFields["Application"] = "Gaming/Enthusiast";
            }
            else
            {
                result.DecodedFields["Format"] = "ADATA";
                result.DecodedFields["Raw"] = partNumber;
                result.DecodedFields["Note"] = "ADATA part number format not fully recognized.";
            }
        }

        private void DecodeGenericPartNumber(string partNumber, PartNumberDecodeResult result)
        {
            result.DecodedFields["Format"] = "Generic/Unknown";
            result.DecodedFields["Raw"] = partNumber;
            result.DecodedFields["Note"] = "Generic part number format. Manufacturer-specific decoding not available.";
        }

        private void DisplayResults(PartNumberDecodeResult result)
        {
            ResultsContainer.Children.Clear();
            NoResultsText.Visibility = Visibility.Collapsed;

            // Title with Copy button
            var titleGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 12)
            };
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            var titleBlock = new TextBlock
            {
                Text = "Decoded Information",
                Style = (Style)TryFindResource("CardTitleStyle"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleBlock, 0);
            titleGrid.Children.Add(titleBlock);

            var copyButton = new Button
            {
                Content = "Copy All",
                Style = (Style)TryFindResource("CommandButtonStyle"),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            copyButton.Click += (s, e) => CopyAllResultsToClipboard(result);
            Grid.SetColumn(copyButton, 1);
            titleGrid.Children.Add(copyButton);

            ResultsContainer.Children.Add(titleGrid);

            // Manufacturer
            var manufacturerBox = new TextBox
            {
                Text = $"Manufacturer: {result.Manufacturer}",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)TryFindResource("PrimaryTextBrush"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(0)
            };
            ResultsContainer.Children.Add(manufacturerBox);

            // Visual breakdown for Samsung part numbers (as table)
            if (result.Manufacturer == "Samsung" && result.DecodedFields.ContainsKey("Format"))
            {
                var breakdownData = CreateBreakdownTable(result.OriginalPartNumber, result.DecodedFields);
                if (breakdownData != null && breakdownData.Count > 0)
                {
                    var breakdownTitleGrid = new Grid
                    {
                        Margin = new Thickness(0, 0, 0, 12)
                    };
                    breakdownTitleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    breakdownTitleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

                    var breakdownTitle = new TextBlock
                    {
                        Text = "Part Number Breakdown",
                        Style = (Style)TryFindResource("CardTitleStyle"),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(breakdownTitle, 0);
                    breakdownTitleGrid.Children.Add(breakdownTitle);

                    var copyBreakdownButton = new Button
                    {
                        Content = "Copy Table",
                        Style = (Style)TryFindResource("CommandButtonStyle"),
                        Padding = new Thickness(12, 6, 12, 6),
                        Margin = new Thickness(12, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Cursor = Cursors.Hand
                    };
                    copyBreakdownButton.Click += (s, e) => CopyBreakdownTableToClipboard(breakdownData);
                    Grid.SetColumn(copyBreakdownButton, 1);
                    breakdownTitleGrid.Children.Add(copyBreakdownButton);

                    ResultsContainer.Children.Add(breakdownTitleGrid);

                    // Create table
                    var table = new Grid
                    {
                        Margin = new Thickness(0, 0, 0, 12)
                    };
                    table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                    table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                    table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    // Header row
                    int row = 0;
                    table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    var header1 = new TextBlock
                    {
                        Text = "Position",
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Foreground = (Brush)TryFindResource("PrimaryTextBrush"),
                        Margin = new Thickness(0, 0, 12, 8)
                    };
                    Grid.SetRow(header1, row);
                    Grid.SetColumn(header1, 0);
                    table.Children.Add(header1);

                    var header2 = new TextBlock
                    {
                        Text = "Value",
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        FontFamily = new FontFamily("Consolas"),
                        Foreground = (Brush)TryFindResource("PrimaryTextBrush"),
                        Margin = new Thickness(0, 0, 12, 8)
                    };
                    Grid.SetRow(header2, row);
                    Grid.SetColumn(header2, 1);
                    table.Children.Add(header2);

                    var header3 = new TextBlock
                    {
                        Text = "Description",
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Foreground = (Brush)TryFindResource("PrimaryTextBrush"),
                        Margin = new Thickness(0, 0, 0, 8)
                    };
                    Grid.SetRow(header3, row);
                    Grid.SetColumn(header3, 2);
                    table.Children.Add(header3);

                    // Data rows
                    foreach (var item in breakdownData)
                    {
                        row++;
                        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        var posBox = new TextBox
                        {
                            Text = item.Position,
                            FontSize = 11,
                            Foreground = (Brush)TryFindResource("SecondaryTextBrush"),
                            Background = Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            IsReadOnly = true,
                            Margin = new Thickness(0, 0, 12, 4),
                            Padding = new Thickness(0),
                            VerticalAlignment = VerticalAlignment.Top,
                            VerticalContentAlignment = VerticalAlignment.Top
                        };
                        Grid.SetRow(posBox, row);
                        Grid.SetColumn(posBox, 0);
                        table.Children.Add(posBox);

                        var valueBox = new TextBox
                        {
                            Text = item.Value,
                            FontSize = 11,
                            FontFamily = new FontFamily("Consolas"),
                            FontWeight = FontWeights.SemiBold,
                            Foreground = (Brush)TryFindResource("PrimaryTextBrush"),
                            Background = Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            IsReadOnly = true,
                            Margin = new Thickness(0, 0, 12, 4),
                            Padding = new Thickness(0),
                            VerticalAlignment = VerticalAlignment.Top,
                            VerticalContentAlignment = VerticalAlignment.Top
                        };
                        Grid.SetRow(valueBox, row);
                        Grid.SetColumn(valueBox, 1);
                        table.Children.Add(valueBox);

                        var descBox = new TextBox
                        {
                            Text = item.Description,
                            FontSize = 11,
                            Foreground = (Brush)TryFindResource("PrimaryTextBrush"),
                            Background = Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            IsReadOnly = true,
                            TextWrapping = TextWrapping.Wrap,
                            AcceptsReturn = false,
                            Margin = new Thickness(0, 0, 0, 4),
                            Padding = new Thickness(0),
                            VerticalAlignment = VerticalAlignment.Top,
                            VerticalContentAlignment = VerticalAlignment.Top
                        };
                        Grid.SetRow(descBox, row);
                        Grid.SetColumn(descBox, 2);
                        table.Children.Add(descBox);
                    }

                    ResultsContainer.Children.Add(table);

                    // Separator after breakdown
                    var separatorBreakdown = new Separator
                    {
                        Margin = new Thickness(0, 0, 0, 12),
                        Background = (Brush)TryFindResource("SeparatorBrush")
                    };
                    ResultsContainer.Children.Add(separatorBreakdown);
                }
            }

            // Decoded fields
            foreach (var kvp in result.DecodedFields)
            {
                var fieldGrid = new Grid
                {
                    Margin = new Thickness(0, 0, 0, 6)
                };
                fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
                fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var labelBox = new TextBox
                {
                    Text = kvp.Key + ":",
                    FontSize = 13,
                    FontWeight = FontWeights.Medium,
                    Foreground = (Brush)TryFindResource("SecondaryTextBrush"),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    IsReadOnly = true,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, 12, 0),
                    Padding = new Thickness(0),
                    VerticalContentAlignment = VerticalAlignment.Top
                };
                Grid.SetColumn(labelBox, 0);
                fieldGrid.Children.Add(labelBox);

                var valueBox = new TextBox
                {
                    Text = kvp.Value,
                    FontSize = 13,
                    Foreground = (Brush)TryFindResource("PrimaryTextBrush"),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = false,
                    VerticalAlignment = VerticalAlignment.Top,
                    Padding = new Thickness(0),
                    VerticalContentAlignment = VerticalAlignment.Top
                };
                Grid.SetColumn(valueBox, 1);
                fieldGrid.Children.Add(valueBox);

                ResultsContainer.Children.Add(fieldGrid);
            }

            // Separator
            var separator = new Separator
            {
                Margin = new Thickness(0, 12, 0, 12),
                Background = (Brush)TryFindResource("SeparatorBrush")
            };
            ResultsContainer.Children.Add(separator);

            // SPD Bytes for Programming section
            var spdBytes = CalculateSpdBytes(result);
            if (spdBytes != null && spdBytes.Count > 0)
            {
                var spdTitleBlock = new TextBlock
                {
                    Text = "SPD Bytes for Programming",
                    Style = (Style)TryFindResource("CardTitleStyle"),
                    Margin = new Thickness(0, 0, 0, 12)
                };
                ResultsContainer.Children.Add(spdTitleBlock);

                var spdNoteBlock = new TextBlock
                {
                    Text = "⚠ These values are calculated from the part number and may need adjustment based on actual SPD data.",
                    FontSize = 11,
                    FontStyle = FontStyles.Italic,
                    Foreground = (Brush)TryFindResource("MutedTextBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 12)
                };
                ResultsContainer.Children.Add(spdNoteBlock);

                // Display SPD bytes in a formatted table
                var spdTable = new Grid
                {
                    Margin = new Thickness(0, 0, 0, 12)
                };
                spdTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                spdTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
                spdTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                int row = 0;
                foreach (var kvp in spdBytes.OrderBy(x => x.Key))
                {
                    spdTable.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    // Byte offset
                    var offsetBlock = new TextBlock
                    {
                        Text = $"0x{kvp.Key:X2}:",
                        FontSize = 12,
                        FontFamily = new FontFamily("Consolas"),
                        Foreground = (Brush)TryFindResource("SecondaryTextBrush"),
                        Margin = new Thickness(0, 0, 8, 4)
                    };
                    Grid.SetRow(offsetBlock, row);
                    Grid.SetColumn(offsetBlock, 0);
                    spdTable.Children.Add(offsetBlock);

                    // Byte value
                    var valueBlock = new TextBlock
                    {
                        Text = $"0x{kvp.Value.Value:X2}",
                        FontSize = 12,
                        FontFamily = new FontFamily("Consolas"),
                        Foreground = (Brush)TryFindResource("PrimaryTextBrush"),
                        Margin = new Thickness(0, 0, 8, 4)
                    };
                    Grid.SetRow(valueBlock, row);
                    Grid.SetColumn(valueBlock, 1);
                    spdTable.Children.Add(valueBlock);

                    // Description
                    var descBlock = new TextBlock
                    {
                        Text = kvp.Value.Description,
                        FontSize = 11,
                        Foreground = (Brush)TryFindResource("MutedTextBrush"),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    Grid.SetRow(descBlock, row);
                    Grid.SetColumn(descBlock, 2);
                    spdTable.Children.Add(descBlock);

                    row++;
                }

                ResultsContainer.Children.Add(spdTable);

                // Separator
                var separator2 = new Separator
                {
                    Margin = new Thickness(0, 12, 0, 12),
                    Background = (Brush)TryFindResource("SeparatorBrush")
                };
                ResultsContainer.Children.Add(separator2);
            }

            // Original part number
            var originalBox = new TextBox
            {
                Text = $"Original Part Number: {result.OriginalPartNumber}",
                FontSize = 12,
                FontFamily = new FontFamily("Consolas"),
                Foreground = (Brush)TryFindResource("MutedTextBrush"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                Margin = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(0)
            };
            ResultsContainer.Children.Add(originalBox);
        }

        private void CopyAllResultsToClipboard(PartNumberDecodeResult result)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Part Number Decoder - Decoded Information");
                sb.AppendLine("=".PadRight(60, '='));
                sb.AppendLine();
                sb.AppendLine($"Manufacturer: {result.Manufacturer}");
                sb.AppendLine($"Original Part Number: {result.OriginalPartNumber}");
                sb.AppendLine();

                // Breakdown table
                if (result.Manufacturer == "Samsung" && result.DecodedFields.ContainsKey("Format"))
                {
                    var breakdownData = CreateBreakdownTable(result.OriginalPartNumber, result.DecodedFields);
                    if (breakdownData != null && breakdownData.Count > 0)
                    {
                        sb.AppendLine("Part Number Breakdown:");
                        sb.AppendLine("-".PadRight(60, '-'));
                        sb.AppendLine($"{"Position",-12} {"Value",-15} {"Description"}");
                        sb.AppendLine("-".PadRight(60, '-'));
                        foreach (var item in breakdownData)
                        {
                            sb.AppendLine($"{item.Position,-12} {item.Value,-15} {item.Description}");
                        }
                        sb.AppendLine();
                    }
                }

                // Decoded fields
                sb.AppendLine("Decoded Fields:");
                sb.AppendLine("-".PadRight(60, '-'));
                foreach (var kvp in result.DecodedFields)
                {
                    sb.AppendLine($"{kvp.Key}: {kvp.Value}");
                }
                sb.AppendLine();

                // SPD Bytes
                var spdBytes = CalculateSpdBytes(result);
                if (spdBytes != null && spdBytes.Count > 0)
                {
                    sb.AppendLine("SPD Bytes for Programming:");
                    sb.AppendLine("-".PadRight(60, '-'));
                    sb.AppendLine($"{"Offset",-10} {"Value",-10} {"Description"}");
                    sb.AppendLine("-".PadRight(60, '-'));
                    foreach (var kvp in spdBytes.OrderBy(x => x.Key))
                    {
                        string offset = $"0x{kvp.Key:X2}";
                        string value = $"0x{kvp.Value.Value:X2}";
                        sb.AppendLine($"{offset,-10} {value,-10} {kvp.Value.Description}");
                    }
                }

                Clipboard.SetText(sb.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error copying to clipboard: {ex.Message}");
            }
        }

        private void CopyBreakdownTableToClipboard(List<(string Position, string Value, string Description)>? breakdownData)
        {
            if (breakdownData == null || breakdownData.Count == 0)
                return;

            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Part Number Breakdown:");
                sb.AppendLine($"{"Position",-12} {"Value",-15} {"Description"}");
                sb.AppendLine("-".PadRight(60, '-'));
                foreach (var item in breakdownData)
                {
                    sb.AppendLine($"{item.Position,-12} {item.Value,-15} {item.Description}");
                }

                Clipboard.SetText(sb.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error copying breakdown table to clipboard: {ex.Message}");
            }
        }

        private void ShowNoResults(string message)
        {
            ResultsContainer.Children.Clear();
            NoResultsText.Text = message;
            NoResultsText.Visibility = Visibility.Visible;
        }

        private Dictionary<int, (byte Value, string Description)>? CalculateSpdBytes(PartNumberDecodeResult result)
        {
            if (!result.DecodedFields.ContainsKey("Memory Type"))
                return null;

            string memoryType = result.DecodedFields.GetValueOrDefault("Memory Type", "");
            if (memoryType != "DDR4")
                return null; // DDR5 support can be added later

            // Support Samsung and SK Hynix
            bool isSamsung = result.Manufacturer == "Samsung";
            bool isHynix = result.Manufacturer == "SK Hynix";

            if (!isSamsung && !isHynix)
                return null;

            var spdBytes = new Dictionary<int, (byte Value, string Description)>();

            // Byte 0: SPD Revision (typically 0x0D for DDR4)
            spdBytes[0] = (0x0D, "SPD Revision (0x0D = DDR4)");

            // Byte 1: SPD Revision (typically 0x0C)
            spdBytes[1] = (0x0C, "SPD Revision continuation");

            // Byte 2: DRAM Device Type (0x0C = DDR4 SDRAM)
            spdBytes[2] = (0x0C, "DRAM Device Type (0x0C = DDR4 SDRAM)");

            // Byte 3: Module Type
            // DDR4 Module Type encoding (bits 3-0):
            // 0x00 = Extended DIMM, 0x01 = RDIMM, 0x02 = UDIMM, 0x03 = SO-DIMM, 0x04 = LRDIMM
            byte moduleType = 0x02; // Default: UDIMM
            string formFactor = result.DecodedFields.GetValueOrDefault("Form Factor", "");
            if (formFactor.Contains("LRDIMM", StringComparison.OrdinalIgnoreCase))
                moduleType = 0x04; // LRDIMM
            else if (formFactor.Contains("RDIMM", StringComparison.OrdinalIgnoreCase))
                moduleType = 0x01; // RDIMM
            else if (formFactor.Contains("SO-DIMM", StringComparison.OrdinalIgnoreCase) || 
                     formFactor.Contains("SODIMM", StringComparison.OrdinalIgnoreCase))
                moduleType = 0x03; // SO-DIMM
            else if (formFactor.Contains("UDIMM", StringComparison.OrdinalIgnoreCase))
                moduleType = 0x02; // UDIMM (standard desktop memory)
            spdBytes[3] = (moduleType, $"Module Type (0x{moduleType:X2} = {GetModuleTypeName(moduleType)})");

            // Byte 4: SDRAM Density and Banks
            // This is complex and depends on capacity - we'll use a default for now
            // Byte 4 bits 3-0: Density code, bits 5-4: Banks code
            // For common configurations, we'll estimate based on config code or die density
            string configCode = result.DecodedFields.GetValueOrDefault("Configuration Code", "");
            string dieDensity = result.DecodedFields.GetValueOrDefault("Die Density", "");
            byte byte4 = CalculateDensityByte(configCode, formFactor, dieDensity, isHynix);
            spdBytes[4] = (byte4, $"SDRAM Density and Banks (calculated from {(isHynix ? "die density" : "config code")} {(!string.IsNullOrEmpty(dieDensity) ? dieDensity : configCode)})");

            // Byte 5: SDRAM Addressing (row/column addresses)
            // This also depends on capacity - default for common sizes
            byte byte5 = CalculateAddressingByte(configCode);
            spdBytes[5] = (byte5, "SDRAM Addressing (row/column addresses)");

            // Byte 6: Package Type
            // Bit 7: 0=Monolithic, 1=Stacked
            // Bits 6-4: Die count (if stacked)
            // Bits 1-0: Signal loading
            byte byte6 = 0x00; // Default: Monolithic
            if (formFactor.Contains("LRDIMM", StringComparison.OrdinalIgnoreCase))
            {
                // LRDIMM often uses stacked packages
                byte6 = 0x80; // Stacked, 1 die (default)
            }
            spdBytes[6] = (byte6, "Package Type (bit 7: 0=Monolithic, 1=Stacked)");

            // Byte 12: Module Memory Bus Width & Ranks
            byte byte12 = CalculateModuleBusWidthByte(result.DecodedFields);
            spdBytes[12] = (byte12, "Module Memory Bus Width & Ranks (bits 5-3: ranks, bits 2-0: device width)");

            // Byte 13: Module Memory Bus Width Extension & ECC
            byte byte13 = 0x00; // Default: no extension, ECC depends on module
            if (formFactor.Contains("RDIMM", StringComparison.OrdinalIgnoreCase) ||
                formFactor.Contains("LRDIMM", StringComparison.OrdinalIgnoreCase))
            {
                byte13 |= 0x08; // ECC typically present on server modules
            }
            spdBytes[13] = (byte13, "Module Memory Bus Width Extension & ECC (bit 3: ECC)");

            // Byte 17-18: Fine Timebase (FTB) and Medium Timebase (MTB)
            int speedMhz = ExtractSpeedMhz(result.DecodedFields);
            if (speedMhz > 0)
            {
                // Byte 17: Fine Timebase Dividend (FTB Dividend)
                // Byte 18: Fine Timebase Divisor (FTB Divisor) or tCK MTB
                // For DDR4-3200: tCK = 1000/3200 = 0.3125 ns
                // MTB = 0.125 ns, so tCK_MTB = 0.3125 / 0.125 = 2.5 ≈ 3
                // FTB = 1 ps, but typically not used for standard speeds
                // For standard speeds, we use MTB only
                int tckMtb = (int)Math.Round(8000.0 / speedMhz);
                if (tckMtb > 0 && tckMtb <= 255)
                {
                    // Byte 17: Medium Timebase (MTB) Dividend = 1 (0x01)
                    spdBytes[17] = (0x01, "Medium Timebase (MTB) Dividend = 1");
                    // Byte 18: tCK Medium Timebase (MTB) = tCK / MTB
                    spdBytes[18] = ((byte)tckMtb, $"tCK Medium Timebase (MTB) = {tckMtb} (for {speedMhz} MHz, tCK = {1000.0 / speedMhz:F3} ns)");
                }
            }

            // Byte 117-118: Manufacturer ID
            if (isHynix)
            {
                // SK Hynix = 0xAD
                spdBytes[117] = (0xAD, "Manufacturer ID (0xAD = SK Hynix)");
                spdBytes[118] = (0x00, "Manufacturer ID continuation (SK Hynix)");
            }
            else if (isSamsung)
            {
                // Samsung = 0xCE
                spdBytes[117] = (0xCE, "Manufacturer ID (0xCE = Samsung)");
                spdBytes[118] = (0x00, "Manufacturer ID continuation (Samsung)");
            }

            // Bytes 329-348: Part Number (ASCII, 20 bytes for DDR4)
            // DDR4 uses bytes 329-348 (0x149-0x15C) for part number
            string partNumber = result.OriginalPartNumber;
            int partNumberStart = 329;
            for (int i = 0; i < 20 && i < partNumber.Length; i++)
            {
                byte asciiByte = (byte)partNumber[i];
                spdBytes[partNumberStart + i] = (asciiByte, $"Part Number byte {i + 1} (ASCII: '{(char)asciiByte}')");
            }
            // Fill remaining bytes with 0x00
            for (int i = partNumber.Length; i < 20; i++)
            {
                spdBytes[partNumberStart + i] = (0x00, $"Part Number byte {i + 1} (padding)");
            }

            // Byte 128: Module Height
            spdBytes[128] = (0x00, "Module Height (0x00 = 15mm, standard DIMM)");

            // Byte 129: Module Thickness
            spdBytes[129] = (0x00, "Module Thickness (0x00 = standard)");

            return spdBytes;
        }

        private byte CalculateDensityByte(string configCode, string formFactor, string dieDensity, bool isHynix)
        {
            // This is a simplified calculation - real density depends on exact capacity
            // Common density codes for DDR4:
            // 0x0B = 8Gb, 0x0C = 16Gb, 0x0D = 32Gb
            // Banks: bits 5-4, typically 0x00 = 4 bank groups, 4 banks per group

            // For SK Hynix, use die density if available
            if (isHynix && !string.IsNullOrEmpty(dieDensity))
            {
                // Parse die density from strings like "32Gb (4GB) per die" or "16Gb per die"
                if (dieDensity.Contains("32Gb", StringComparison.OrdinalIgnoreCase))
                    return 0x0D; // 32Gb, 4 bank groups, 4 banks
                else if (dieDensity.Contains("16Gb", StringComparison.OrdinalIgnoreCase))
                    return 0x0C; // 16Gb, 4 bank groups, 4 banks
                else if (dieDensity.Contains("8Gb", StringComparison.OrdinalIgnoreCase))
                    return 0x0B; // 8Gb, 4 bank groups, 4 banks
            }

            // Estimate based on config code patterns (for Samsung and others)
            if (configCode.StartsWith("A", StringComparison.OrdinalIgnoreCase))
            {
                // A2, A8, AA often indicate higher capacity
                if (configCode.Contains("A8") || configCode == "AA")
                    return 0x0C; // 16Gb, 4 bank groups, 4 banks
                else if (configCode == "A2")
                    return 0x0B; // 8Gb, 4 bank groups, 4 banks
            }

            return 0x0B; // Default: 8Gb
        }

        private byte CalculateAddressingByte(string configCode)
        {
            // Byte 5: Row/Column addressing
            // Common values:
            // 0x08 = x8 organization (common for UDIMM)
            // 0x0A = x4 organization (common for RDIMM/LRDIMM)

            if (configCode == "AA" || configCode.Contains("A8"))
                return 0x0A; // x4 organization (common for high-capacity modules)
            else if (configCode == "A2")
                return 0x08; // x8 organization (common for desktop modules)

            return 0x08; // Default: x8
        }

        private byte CalculateModuleBusWidthByte(Dictionary<string, string> decodedFields)
        {
            byte byte12 = 0x00;

            // Bits 5-3: Number of ranks (0-7, actual ranks = value + 1)
            int ranks = 1; // Default: 1 rank

            // Try to extract from "Ranks" field first (e.g., "8 Ranks", "4 Ranks")
            string ranksField = decodedFields.GetValueOrDefault("Ranks", "");
            if (!string.IsNullOrEmpty(ranksField))
            {
                var ranksMatch = System.Text.RegularExpressions.Regex.Match(ranksField, @"(\d+)\s*Rank");
                if (ranksMatch.Success && int.TryParse(ranksMatch.Groups[1].Value, out int extractedRanks))
                {
                    ranks = extractedRanks;
                }
            }

            // Fallback to configuration description
            if (ranks == 1)
            {
                string configDesc = decodedFields.GetValueOrDefault("Configuration Description", "");
                if (configDesc.Contains("2R", StringComparison.OrdinalIgnoreCase) ||
                    configDesc.Contains("2 ranks", StringComparison.OrdinalIgnoreCase))
                    ranks = 2;
                else if (configDesc.Contains("4R", StringComparison.OrdinalIgnoreCase) ||
                         configDesc.Contains("4 ranks", StringComparison.OrdinalIgnoreCase) ||
                         configDesc.Contains("4DR", StringComparison.OrdinalIgnoreCase))
                    ranks = 4;
                else if (configDesc.Contains("8R", StringComparison.OrdinalIgnoreCase) ||
                         configDesc.Contains("8 ranks", StringComparison.OrdinalIgnoreCase))
                    ranks = 8;
            }

            // Limit ranks to valid range (1-8, encoded as 0-7)
            if (ranks < 1) ranks = 1;
            if (ranks > 8) ranks = 8;

            byte12 |= (byte)(((ranks - 1) & 0x7) << 3);

            // Bits 2-0: Device width
            // 0 = x4, 1 = x8, 2 = x16, 3 = x32
            int deviceWidth = 1; // Default: x8
            string interfaceField = decodedFields.GetValueOrDefault("Interface", "");
            string configDesc2 = decodedFields.GetValueOrDefault("Configuration Description", "");

            if (interfaceField.Contains("x4", StringComparison.OrdinalIgnoreCase) ||
                configDesc2.Contains("x4", StringComparison.OrdinalIgnoreCase))
                deviceWidth = 0;
            else if (interfaceField.Contains("x8", StringComparison.OrdinalIgnoreCase) ||
                     configDesc2.Contains("x8", StringComparison.OrdinalIgnoreCase))
                deviceWidth = 1;
            else if (interfaceField.Contains("x16", StringComparison.OrdinalIgnoreCase) ||
                     configDesc2.Contains("x16", StringComparison.OrdinalIgnoreCase))
                deviceWidth = 2;

            byte12 |= (byte)(deviceWidth & 0x7);

            return byte12;
        }

        private int ExtractSpeedMhz(Dictionary<string, string> decodedFields)
        {
            string speed = decodedFields.GetValueOrDefault("Speed", "");
            if (string.IsNullOrEmpty(speed))
                return 0;

            // Extract MHz from strings like "Speed Code 40 = 3200 MHz"
            var match = System.Text.RegularExpressions.Regex.Match(speed, @"(\d+)\s*MHz");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int mhz))
                return mhz;

            return 0;
        }

        private string GetModuleTypeName(byte moduleType)
        {
            return moduleType switch
            {
                0x00 => "Extended DIMM",
                0x01 => "RDIMM",
                0x02 => "UDIMM",
                0x03 => "SO-DIMM",
                0x04 => "LRDIMM",
                0x05 => "Mini-RDIMM",
                0x06 => "Mini-UDIMM",
                _ => "Unknown"
            };
        }

        private List<(string Position, string Value, string Description)>? CreateBreakdownTable(string partNumber, Dictionary<string, string> decodedFields)
        {
            // Try pattern with config code that can contain digits: M393A8G40CB4-CWE
            var match = Regex.Match(partNumber, @"^M(\d+)([A-Z]\d?)([A-Z])(\d+)([A-Z])([A-Z]\d+)(?:-([A-Z0-9]+))?$", RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                // Try pattern with config code that's only letters: M386AAG40BM3-CWE
                match = Regex.Match(partNumber, @"^M(\d+)([A-Z]+)([A-Z])(\d+)([A-Z])([A-Z0-9]+)(?:-([A-Z0-9]+))?$", RegexOptions.IgnoreCase);
            }

            if (!match.Success)
                return null;

            string series = match.Groups[1].Value;
            string configCode = match.Groups[2].Value;
            string voltageCode = match.Groups[3].Value;
            string speedCode = match.Groups[4].Value;
            string formFactorCode = match.Groups[5].Value;
            string revision = match.Groups[6].Value;
            string suffix = match.Groups[7].Success ? match.Groups[7].Value : "";

            var breakdown = new List<(string Position, string Value, string Description)>();

            // Determine positions based on format
            // Format 1: M386AAG40BM3 (config code is letters only, like AA)
            // Format 2: M393A8G40CB4 (config code is letter+digit, like A8)
            bool isConfigWithDigit = configCode.Length == 2 && char.IsDigit(configCode[1]);

            int pos = 1;
            breakdown.Add((pos++.ToString(), "M", "DRAM Component (Memory Module)"));

            // Series (3 digits typically)
            string seriesPos = pos.ToString();
            pos += series.Length;
            breakdown.Add(($"{seriesPos}-{pos - 1}", series, $"Samsung Memory Series ({series} = {(series.StartsWith("3") ? "DDR4" : series.StartsWith("4") ? "DDR5" : "Unknown")})"));

            // Config code - показываем как единый код для лучшей читаемости
            string configPos = pos.ToString();
            pos += configCode.Length;
            if (isConfigWithDigit)
            {
                // Format: A4, A8 (letter + digit) - показываем как единый код
                breakdown.Add(($"{configPos}-{pos - 1}", configCode, GetDensityOrganizationDescription(configCode, decodedFields)));
            }
            else
            {
                // Format: AA (letters only)
                breakdown.Add(($"{configPos}-{pos - 1}", configCode, GetDensityOrganizationDescription(configCode, decodedFields)));
            }

            // Voltage
            breakdown.Add((pos++.ToString(), voltageCode, GetVoltageDescription(voltageCode)));

            // Speed code
            string speedPos = pos.ToString();
            pos += speedCode.Length;
            breakdown.Add(($"{speedPos}-{pos - 1}", speedCode, GetSpeedDescription(speedCode, decodedFields)));

            // Form factor
            breakdown.Add((pos++.ToString(), formFactorCode, GetProductFamilyDescription(formFactorCode)));

            // Revision (can be letter+digit like B4, B2, or letter+digit like M3, or just letters)
            // M3, EB1, B4 - these are typically single revision codes, not separate package+revision
            // Only split if it's a known pattern like B2, B4 where B might indicate package type
            bool shouldSplitRevision = revision.Length == 2 &&
                                       char.IsDigit(revision[1]) &&
                                       (revision[0] == 'B' || revision[0] == 'E');

            if (shouldSplitRevision)
            {
                // Known patterns: B2, B4, E1, etc. - first char might indicate package/power
                string packageDesc = revision[0].ToString().ToUpperInvariant() switch
                {
                    "B" => "Package Type (FBGA) / Power Level",
                    "E" => "Package Type / Power Level",
                    _ => "Package/Power Code"
                };
                breakdown.Add((pos++.ToString(), revision[0].ToString(), packageDesc));
                breakdown.Add((pos++.ToString(), revision[1].ToString(), $"Revision {revision[1]}"));
            }
            else
            {
                // M3, EB1, etc. - treat as single revision code
                string revPos = pos.ToString();
                pos += revision.Length;
                string revDesc = revision.Length == 2 && char.IsDigit(revision[1])
                    ? $"Revision/Variant ({revision})"
                    : "Package (FBGA) / Revision";
                breakdown.Add(($"{revPos}-{pos - 1}", revision, revDesc));
            }

            // Suffix components
            if (!string.IsNullOrEmpty(suffix) && suffix.Length >= 3)
            {
                pos++; // Skip dash
                breakdown.Add((pos++.ToString(), suffix[0].ToString(), "Revision (suffix)"));
                breakdown.Add((pos++.ToString(), suffix[1].ToString(), GetTemperatureDescription(suffix[1])));
                breakdown.Add((pos++.ToString(), suffix[2].ToString(), GetLeadFinishDescription(suffix[2])));
            }

            return breakdown;
        }

        private string GetVoltageDescription(string voltageCode)
        {
            return voltageCode.ToUpperInvariant() switch
            {
                "G" => "Voltage (1.2V DDR4 standard)",
                "H" => "Voltage (1.1V DDR5 standard)",
                "K" => "Voltage (1.2V DDR4 standard)",
                _ => $"Voltage code ({voltageCode})"
            };
        }

        private string GetSpeedDescription(string speedCode, Dictionary<string, string> decodedFields)
        {
            string speed = decodedFields.GetValueOrDefault("Speed", "");
            if (!string.IsNullOrEmpty(speed))
            {
                // Extract MHz from speed string
                var match = Regex.Match(speed, @"(\d+)\s*MHz");
                if (match.Success)
                {
                    return $"Speed Code {speedCode} = {match.Groups[1].Value} MHz (Generation: DDR4)";
                }
            }
            return $"Speed Code {speedCode} (Generation: DDR4)";
        }

        private string GetProductFamilyDescription(string formFactorCode)
        {
            return formFactorCode.ToUpperInvariant() switch
            {
                "B" => "Form Factor: LRDIMM (Load-Reduced DIMM) ← ВАЖНО!",
                "K" => "Form Factor: UDIMM (Unbuffered DIMM) ← ВАЖНО!",
                "C" => "Form Factor: RDIMM (Registered DIMM) ← ВАЖНО!",
                "M" => "Form Factor: RDIMM (Registered DIMM) ← ВАЖНО!",
                "E" => "Form Factor: UDIMM (Unbuffered DIMM) ← ВАЖНО!",
                _ => $"Form Factor: Unknown code ({formFactorCode})"
            };
        }

        private string GetDensityOrganizationDescription(string configCode, Dictionary<string, string> decodedFields)
        {
            string configDesc = decodedFields.GetValueOrDefault("Configuration Description", "");
            string result = "";

            if (!string.IsNullOrEmpty(configDesc))
            {
                // Extract density/organization info from description
                if (configDesc.Contains("128GB") || configDesc.Contains("4DRx4"))
                    result = "Density/Organization (128GB, 4DRx4)";
                else if (configDesc.Contains("64GB") || configDesc.Contains("2Rx4"))
                    result = "Density/Organization (64GB, 2Rx4)";
                else if (configDesc.Contains("16GB") || configDesc.Contains("2Rx8"))
                    result = "Density/Organization (16GB, 2Rx8)";
                else if (configDesc.Contains("8GB") || configDesc.Contains("1Rx8"))
                    result = "Density/Organization (8GB, 1Rx8)";
            }

            // Fallback based on config code
            if (string.IsNullOrEmpty(result))
            {
                if (configCode == "AA")
                    result = "Configuration: Density/Organization (128GB, 4DRx4)";
                else if (configCode == "A8")
                    result = "Configuration: Density/Organization (64GB, 2Rx4)";
                else if (configCode == "A4")
                    result = "Configuration: Density/Organization (32GB, 2Rx4)";
                else if (configCode == "A2")
                    result = "Configuration: Density/Organization (16GB, 2Rx8)";
                else
                    result = "Configuration: Density/Organization (varies by model)";
            }

            // Add important marker
            return result + " ← ВАЖНО!";
        }

        private string GetTemperatureDescription(char tempChar)
        {
            return tempChar switch
            {
                'W' => "Temperature (Commercial: 0°C to 95°C)",
                'C' => "Temperature (Commercial: 0°C to 85°C)",
                'I' => "Temperature (Industrial: -40°C to 85°C)",
                'V' => "Temperature (Commercial: 0°C to 85°C)",
                _ => $"Temperature ({tempChar})"
            };
        }

        private string GetLeadFinishDescription(char finishChar)
        {
            return finishChar switch
            {
                'E' => "Lead Finish (Halogen Free)",
                'F' => "Lead Finish (Standard)",
                'G' => "Lead Finish (Other)",
                _ => $"Lead Finish ({finishChar})"
            };
        }

        private class PartNumberDecodeResult
        {
            public string OriginalPartNumber { get; set; } = "";
            public string Manufacturer { get; set; } = "";
            public Dictionary<string, string> DecodedFields { get; set; } = new();
        }
    }
}

