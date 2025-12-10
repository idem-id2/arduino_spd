using System;
using System.Collections.Generic;
using System.Linq;
using HexEditor.Database;

namespace HexEditor.SpdDecoder
{
    /// <summary>
    /// Редактор SPD данных для DDR5 по стандарту JEDEC JESD400-5C
    /// </summary>
    internal sealed class Ddr5SpdEditor : BaseSpdEditor
    {
        public Ddr5SpdEditor() : base()
        {
        }

        public override List<EditField> GetEditFields()
        {
            var fields = new List<EditField>();

            if (Data == null || Data.Length < 512)
                return fields;

            // Module Manufacturer (bytes 512-513)
            if (Data.Length > 513)
            {
                ushort manufacturerId = (ushort)(Data[512] | (Data[513] << 8));
                string manufacturerIdHex = manufacturerId.ToString("X4");

                // Загружаем список производителей
                var manufacturerItems = new List<ComboBoxItem>();
                var manufacturers = ManufacturerDatabase.GetManufacturerComboBoxItems();
                
                foreach (var (displayText, idHex) in manufacturers)
                {
                    manufacturerItems.Add(new ComboBoxItem
                    {
                        Content = displayText,
                        Tag = idHex
                    });
                }

                fields.Add(new EditField
                {
                    Id = "ModuleManufacturer",
                    Label = "Module Manufacturer",
                    Value = manufacturerIdHex,
                    Type = EditFieldType.ComboBox,
                    ComboBoxItems = manufacturerItems,
                    ToolTip = "Bytes 512-513: JEDEC Manufacturer ID",
                    Category = "MemoryModule"
                });
            }

            // Module Part Number (bytes 521-550, 30 bytes ASCII)
            if (Data.Length > 550)
            {
                var partNumber = new System.Text.StringBuilder();
                for (int i = 521; i <= 550 && i < Data.Length; i++)
                {
                    if (Data[i] == 0) break;
                    if (Data[i] >= 32 && Data[i] <= 126)
                        partNumber.Append((char)Data[i]);
                }

                fields.Add(new EditField
                {
                    Id = "ModulePartNumber",
                    Label = "Module Part Number",
                    Value = partNumber.ToString(),
                    Type = EditFieldType.TextBox,
                    MaxLength = 30,
                    ToolTip = "Bytes 521-550: Module Part Number (30 bytes ASCII)",
                    Category = "MemoryModule"
                });
            }

            // Serial Number (bytes 517-520, 4 bytes)
            if (Data.Length > 520)
            {
                var serial = new System.Text.StringBuilder();
                for (int i = 517; i <= 520 && i < Data.Length; i++)
                {
                    serial.Append($"{Data[i]:X2}");
                }

                fields.Add(new EditField
                {
                    Id = "ModuleSerialNumber",
                    Label = "Serial Number",
                    Value = serial.ToString(),
                    Type = EditFieldType.TextBox,
                    MaxLength = 8,
                    ToolTip = "Bytes 517-520: Module Serial Number (4 bytes hex)",
                    Category = "MemoryModule"
                });
            }

            // Manufacturing Date (bytes 515-516, BCD)
            if (Data.Length > 516)
            {
                byte yearBcd = Data[515];
                byte weekBcd = Data[516];

                fields.Add(new EditField
                {
                    Id = "ManufacturingYear",
                    Label = "Manufacturing Year",
                    Value = yearBcd.ToString("X2"),
                    Type = EditFieldType.TextBox,
                    MaxLength = 2,
                    ToolTip = "Byte 515: Year (BCD, 00-99 = 2000-2099)",
                    Category = "MemoryModule"
                });

                fields.Add(new EditField
                {
                    Id = "ManufacturingWeek",
                    Label = "Manufacturing Week",
                    Value = weekBcd.ToString("X2"),
                    Type = EditFieldType.TextBox,
                    MaxLength = 2,
                    ToolTip = "Byte 516: Week (BCD, 01-52)",
                    Category = "MemoryModule"
                });
            }

            // DRAM Manufacturer (bytes 552-553)
            if (Data.Length > 553)
            {
                ushort dramManufacturerId = (ushort)(Data[552] | (Data[553] << 8));
                string dramManufacturerIdHex = dramManufacturerId.ToString("X4");

                var dramManufacturerItems = new List<ComboBoxItem>();
                var manufacturers = ManufacturerDatabase.GetManufacturerComboBoxItems();
                
                foreach (var (displayText, idHex) in manufacturers)
                {
                    dramManufacturerItems.Add(new ComboBoxItem
                    {
                        Content = displayText,
                        Tag = idHex
                    });
                }

                fields.Add(new EditField
                {
                    Id = "DramManufacturer",
                    Label = "DRAM Manufacturer",
                    Value = dramManufacturerIdHex,
                    Type = EditFieldType.ComboBox,
                    ComboBoxItems = dramManufacturerItems,
                    ToolTip = "Bytes 552-553: JEDEC DRAM Manufacturer ID",
                    Category = "DramComponents"
                });
            }

            // Timing Parameters
            if (Data.Length > 235)
            {
                // tCK (bytes 20, 235)
                fields.Add(new EditField
                {
                    Id = "TimingTckMtb",
                    Label = "tCK (Clock Period) MTB",
                    Value = Data[20].ToString(),
                    Type = EditFieldType.TextBox,
                    ToolTip = "Byte 20: tCK Medium Timebase",
                    Category = "Timing"
                });

                fields.Add(new EditField
                {
                    Id = "TimingTckFtb",
                    Label = "tCK (Clock Period) FTB",
                    Value = ((sbyte)Data[235]).ToString(),
                    Type = EditFieldType.TextBox,
                    ToolTip = "Byte 235: tCK Fine Timebase (signed)",
                    Category = "Timing"
                });

                // tAA (bytes 20, 235) - same as tCK for simplest config
                fields.Add(new EditField
                {
                    Id = "TimingTaaMtb",
                    Label = "CAS Latency (tAA) MTB",
                    Value = Data[20].ToString(),
                    Type = EditFieldType.TextBox,
                    ToolTip = "Byte 20: tAA Medium Timebase (same as tCK in DDR5)",
                    Category = "Timing"
                });

                // tRCD (bytes 21, 236)
                if (Data.Length > 236)
                {
                    fields.Add(new EditField
                    {
                        Id = "TimingTrcdMtb",
                        Label = "tRCD MTB",
                        Value = Data[21].ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 21: tRCD Medium Timebase",
                        Category = "Timing"
                    });

                    fields.Add(new EditField
                    {
                        Id = "TimingTrcdFtb",
                        Label = "tRCD FTB",
                        Value = ((sbyte)Data[236]).ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 236: tRCD Fine Timebase (signed)",
                        Category = "Timing"
                    });
                }

                // tRP (bytes 25, 240)
                if (Data.Length > 240)
                {
                    fields.Add(new EditField
                    {
                        Id = "TimingTrpMtb",
                        Label = "tRP MTB",
                        Value = Data[25].ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 25: tRP Medium Timebase",
                        Category = "Timing"
                    });

                    fields.Add(new EditField
                    {
                        Id = "TimingTrpFtb",
                        Label = "tRP FTB",
                        Value = ((sbyte)Data[240]).ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 240: tRP Fine Timebase (signed)",
                        Category = "Timing"
                    });
                }

                // tRAS (bytes 26, 241)
                if (Data.Length > 241)
                {
                    fields.Add(new EditField
                    {
                        Id = "TimingTrasMtb",
                        Label = "tRAS MTB",
                        Value = Data[26].ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 26: tRAS Medium Timebase",
                        Category = "Timing"
                    });

                    fields.Add(new EditField
                    {
                        Id = "TimingTrasFtb",
                        Label = "tRAS FTB",
                        Value = ((sbyte)Data[241]).ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 241: tRAS Fine Timebase (signed)",
                        Category = "Timing"
                    });
                }
            }

            // Module Configuration
            if (Data.Length > 13)
            {
                // Package Ranks (byte 12, bits 5-3)
                int ranks = ((Data[12] >> 3) & 0x7) + 1;
                fields.Add(new EditField
                {
                    Id = "ModuleRanks",
                    Label = "Number of Ranks",
                    Value = ranks.ToString(),
                    Type = EditFieldType.TextBox,
                    ToolTip = "Byte 12, bits 5-3: Package ranks per channel (1-8)",
                    Category = "ModuleConfig"
                });

                // Device Width (byte 12, bits 2-0)
                int widthCode = Data[12] & 0x7;
                int deviceWidth = widthCode switch { 0 => 4, 1 => 8, 2 => 16, 3 => 32, _ => 0 };
                
                fields.Add(new EditField
                {
                    Id = "DeviceWidth",
                    Label = "Device Width (bits)",
                    Value = deviceWidth.ToString(),
                    Type = EditFieldType.TextBox,
                    ToolTip = "Byte 12, bits 2-0: I/O width (4, 8, 16, 32)",
                    Category = "ModuleConfig"
                });

                // Primary Bus Width (byte 13, bits 2-0)
                int busWidthCode = Data[13] & 0x7;
                int busWidth = busWidthCode switch { 0 => 32, 1 => 64, _ => 0 };
                
                fields.Add(new EditField
                {
                    Id = "PrimaryBusWidth",
                    Label = "Primary Bus Width (bits)",
                    Value = busWidth.ToString(),
                    Type = EditFieldType.TextBox,
                    ToolTip = "Byte 13, bits 2-0: Primary bus width per channel (32, 64)",
                    Category = "ModuleConfig"
                });

                // Thermal Sensor (byte 14, bit 7)
                bool hasThermalSensor = (Data[14] & 0x80) != 0;
                fields.Add(new EditField
                {
                    Id = "ThermalSensor",
                    Label = "Thermal Sensor",
                    Value = hasThermalSensor ? "True" : "False",
                    Type = EditFieldType.CheckBox,
                    ToolTip = "Byte 14, bit 7: On-die thermal sensor",
                    Category = "ModuleConfig"
                });
            }

            return fields;
        }

        public override List<SpdEditPanel.ByteChange> ApplyChanges(Dictionary<string, string> fieldValues)
        {
            if (Data == null)
                return new List<SpdEditPanel.ByteChange>();

            var changes = new List<SpdEditPanel.ByteChange>();

            // Module Manufacturer (bytes 512-513)
            if (fieldValues.TryGetValue("ModuleManufacturer", out string? moduleManufacturerText) &&
                Data.Length > 513 &&
                !string.IsNullOrWhiteSpace(moduleManufacturerText))
            {
                string hexString = moduleManufacturerText.Trim();
                if (hexString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    hexString = hexString.Substring(2);

                if (ushort.TryParse(hexString, System.Globalization.NumberStyles.HexNumber, null, out ushort manufacturerId))
                {
                    byte newByte512 = (byte)(manufacturerId & 0xFF);
                    byte newByte513 = (byte)(manufacturerId >> 8);
                    
                    if (Data[512] != newByte512)
                    {
                        Data[512] = newByte512;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 512, NewData = new[] { newByte512 } });
                    }
                    if (Data[513] != newByte513)
                    {
                        Data[513] = newByte513;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 513, NewData = new[] { newByte513 } });
                    }
                }
            }

            // DRAM Manufacturer (bytes 552-553)
            if (fieldValues.TryGetValue("DramManufacturer", out string? dramManufacturerText) &&
                Data.Length > 553 &&
                !string.IsNullOrWhiteSpace(dramManufacturerText))
            {
                string hexString = dramManufacturerText.Trim();
                if (hexString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    hexString = hexString.Substring(2);

                if (ushort.TryParse(hexString, System.Globalization.NumberStyles.HexNumber, null, out ushort dramManufacturerId))
                {
                    byte newByte552 = (byte)(dramManufacturerId & 0xFF);
                    byte newByte553 = (byte)(dramManufacturerId >> 8);
                    
                    if (Data[552] != newByte552)
                    {
                        Data[552] = newByte552;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 552, NewData = new[] { newByte552 } });
                    }
                    if (Data[553] != newByte553)
                    {
                        Data[553] = newByte553;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 553, NewData = new[] { newByte553 } });
                    }
                }
            }

            // Timing Parameters
            if (Data.Length > 235)
            {
                // tCK (bytes 20, 235)
                if (fieldValues.TryGetValue("TimingTckMtb", out string? tckMtbText) &&
                    byte.TryParse(tckMtbText, out byte tckMtb) && Data[20] != tckMtb)
                {
                    Data[20] = tckMtb;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = 20, NewData = new[] { tckMtb } });
                }

                if (fieldValues.TryGetValue("TimingTckFtb", out string? tckFtbText) &&
                    sbyte.TryParse(tckFtbText, out sbyte tckFtb) && Data[235] != (byte)tckFtb)
                {
                    Data[235] = (byte)tckFtb;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = 235, NewData = new[] { (byte)tckFtb } });
                }

                // tRCD (bytes 21, 236)
                if (Data.Length > 236)
                {
                    if (fieldValues.TryGetValue("TimingTrcdMtb", out string? trcdMtbText) &&
                        byte.TryParse(trcdMtbText, out byte trcdMtb) && Data[21] != trcdMtb)
                    {
                        Data[21] = trcdMtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 21, NewData = new[] { trcdMtb } });
                    }

                    if (fieldValues.TryGetValue("TimingTrcdFtb", out string? trcdFtbText) &&
                        sbyte.TryParse(trcdFtbText, out sbyte trcdFtb) && Data[236] != (byte)trcdFtb)
                    {
                        Data[236] = (byte)trcdFtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 236, NewData = new[] { (byte)trcdFtb } });
                    }
                }

                // tRP (bytes 25, 240)
                if (Data.Length > 240)
                {
                    if (fieldValues.TryGetValue("TimingTrpMtb", out string? trpMtbText) &&
                        byte.TryParse(trpMtbText, out byte trpMtb) && Data[25] != trpMtb)
                    {
                        Data[25] = trpMtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 25, NewData = new[] { trpMtb } });
                    }

                    if (fieldValues.TryGetValue("TimingTrpFtb", out string? trpFtbText) &&
                        sbyte.TryParse(trpFtbText, out sbyte trpFtb) && Data[240] != (byte)trpFtb)
                    {
                        Data[240] = (byte)trpFtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 240, NewData = new[] { (byte)trpFtb } });
                    }
                }

                // tRAS (bytes 26, 241)
                if (Data.Length > 241)
                {
                    if (fieldValues.TryGetValue("TimingTrasMtb", out string? trasMtbText) &&
                        byte.TryParse(trasMtbText, out byte trasMtb) && Data[26] != trasMtb)
                    {
                        Data[26] = trasMtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 26, NewData = new[] { trasMtb } });
                    }

                    if (fieldValues.TryGetValue("TimingTrasFtb", out string? trasFtbText) &&
                        sbyte.TryParse(trasFtbText, out sbyte trasFtb) && Data[241] != (byte)trasFtb)
                    {
                        Data[241] = (byte)trasFtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 241, NewData = new[] { (byte)trasFtb } });
                    }
                }
            }

            // Module Configuration
            if (Data.Length > 13)
            {
                // Ranks (byte 12, bits 5-3)
                if (fieldValues.TryGetValue("ModuleRanks", out string? ranksText) &&
                    int.TryParse(ranksText, out int ranks) && ranks >= 1 && ranks <= 8)
                {
                    byte byte12 = Data[12];
                    byte newByte12 = (byte)((byte12 & 0xC7) | (((ranks - 1) & 0x7) << 3));
                    
                    if (Data[12] != newByte12)
                    {
                        Data[12] = newByte12;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 12, NewData = new[] { newByte12 } });
                    }
                }

                // Device Width (byte 12, bits 2-0)
                if (fieldValues.TryGetValue("DeviceWidth", out string? deviceWidthText) &&
                    int.TryParse(deviceWidthText, out int deviceWidth))
                {
                    int code = deviceWidth switch
                    {
                        4 => 0,
                        8 => 1,
                        16 => 2,
                        32 => 3,
                        _ => -1
                    };

                    if (code >= 0)
                    {
                        byte byte12 = Data[12];
                        byte newByte12 = (byte)((byte12 & 0xF8) | (code & 0x7));
                        
                        if (Data[12] != newByte12)
                        {
                            Data[12] = newByte12;
                            changes.Add(new SpdEditPanel.ByteChange { Offset = 12, NewData = new[] { newByte12 } });
                        }
                    }
                }

                // Primary Bus Width (byte 13, bits 2-0)
                if (fieldValues.TryGetValue("PrimaryBusWidth", out string? busWidthText) &&
                    int.TryParse(busWidthText, out int busWidth))
                {
                    int code = busWidth switch
                    {
                        32 => 0,
                        64 => 1,
                        _ => -1
                    };

                    if (code >= 0)
                    {
                        byte byte13 = Data[13];
                        byte newByte13 = (byte)((byte13 & 0xF8) | (code & 0x7));
                        
                        if (Data[13] != newByte13)
                        {
                            Data[13] = newByte13;
                            changes.Add(new SpdEditPanel.ByteChange { Offset = 13, NewData = new[] { newByte13 } });
                        }
                    }
                }

                // Thermal Sensor (byte 14, bit 7)
                if (fieldValues.TryGetValue("ThermalSensor", out string? thermalText))
                {
                    bool hasThermal = thermalText == "True" || thermalText == "true";
                    byte byte14 = Data[14];
                    byte newByte14 = hasThermal ? (byte)(byte14 | 0x80) : (byte)(byte14 & 0x7F);
                    
                    if (Data[14] != newByte14)
                    {
                        Data[14] = newByte14;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 14, NewData = new[] { newByte14 } });
                    }
                }
            }

            return changes;
        }

        public override Dictionary<string, string> ValidateFields(Dictionary<string, string> fieldValues)
        {
            var errors = new Dictionary<string, string>();

            // Validate BCD fields
            if (fieldValues.TryGetValue("ManufacturingYear", out string? year) &&
                !string.IsNullOrWhiteSpace(year) && !TryParseBcd(year, out _))
            {
                errors["ManufacturingYear"] = "Invalid BCD format (00-99)";
            }

            if (fieldValues.TryGetValue("ManufacturingWeek", out string? week) &&
                !string.IsNullOrWhiteSpace(week) && !TryParseBcd(week, out _))
            {
                errors["ManufacturingWeek"] = "Invalid BCD format (01-52)";
            }

            // Validate numeric ranges
            if (fieldValues.TryGetValue("ModuleRanks", out string? ranks) &&
                (!int.TryParse(ranks, out int ranksVal) || ranksVal < 1 || ranksVal > 8))
            {
                errors["ModuleRanks"] = "Ranks must be 1-8";
            }

            return errors;
        }
    }
}
