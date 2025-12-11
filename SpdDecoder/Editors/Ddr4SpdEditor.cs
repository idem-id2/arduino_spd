using System;
using System.Collections.Generic;
using System.Linq;
using HexEditor.Database;

namespace HexEditor.SpdDecoder
{
    /// <summary>
    /// Редактор SPD данных для DDR4
    /// </summary>
    internal sealed class Ddr4SpdEditor : BaseSpdEditor
    {
        public Ddr4SpdEditor() : base()
        {
        }

        public override List<EditField> GetEditFields()
        {
            var fields = CreateCommonFields();

            if (Data == null || Data.Length < 256)
                return fields;

            // DRAM Manufacturer (bytes 350-351)
            if (Data.Length > 351)
            {
                ushort dramManufacturerId = (ushort)((Data[350] << 8) | Data[351]);
                string manufacturerIdHex = dramManufacturerId.ToString("X4");
                
                // Загружаем список производителей для ComboBox
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
                
                // Добавляем текущий ID, если его нет в списке
                if (!manufacturers.Any(m => m.IdHex.Equals(manufacturerIdHex, StringComparison.OrdinalIgnoreCase)))
                {
                    string currentName = ManufacturerDatabase.GetManufacturerName(Data[350], Data[351]);
                    manufacturerItems.Insert(0, new ComboBoxItem
                    {
                        Content = $"{currentName} (0x{manufacturerIdHex})",
                        Tag = manufacturerIdHex
                    });
                }
                
                fields.Add(new EditField
                {
                    Id = "DramManufacturer",
                    Label = "DRAM Manufacturer",
                    Value = manufacturerIdHex,
                    Type = EditFieldType.ComboBox,
                    ComboBoxItems = manufacturerItems,
                    ToolTip = "Bytes 350-351: JEDEC Manufacturer ID",
                    Category = "DramComponents"
                });
            }

            // Density (byte 4, bits 3-0)
            if (Data.Length > 4)
            {
                byte densityCode = (byte)(Data[4] & 0x0F);
                bool is3Ds = (Data[4] & 0x08) != 0;
                
                // Создаем список для ComboBox
                var densityItems = new List<ComboBoxItem>();
                
                // Standard densities (bit 3 = 0)
                densityItems.Add(new ComboBoxItem { Content = "256 Mb", Tag = "0" });
                densityItems.Add(new ComboBoxItem { Content = "512 Mb", Tag = "1" });
                densityItems.Add(new ComboBoxItem { Content = "1 Gb", Tag = "2" });
                densityItems.Add(new ComboBoxItem { Content = "2 Gb", Tag = "3" });
                densityItems.Add(new ComboBoxItem { Content = "4 Gb", Tag = "4" });
                densityItems.Add(new ComboBoxItem { Content = "8 Gb", Tag = "5" });
                densityItems.Add(new ComboBoxItem { Content = "16 Gb", Tag = "6" });
                densityItems.Add(new ComboBoxItem { Content = "32 Gb", Tag = "7" });
                
                // 3DS densities (bit 3 = 1)
                densityItems.Add(new ComboBoxItem { Content = "12 Gb (3DS)", Tag = "8" });
                densityItems.Add(new ComboBoxItem { Content = "24 Gb (3DS)", Tag = "9" });

                fields.Add(new EditField
                {
                    Id = "Density",
                    Label = "Die Density",
                    Value = densityCode.ToString(),
                    Type = EditFieldType.ComboBox,
                    ComboBoxItems = densityItems,
                    ToolTip = "Byte 4, bits 3-0: Density per die (0-7=Standard, 8-9=3DS)",
                    Category = "DensityDie"
                });
            }

            // Package Type (byte 6)
            if (Data.Length > 6)
            {
                byte packageByte = Data[6];
                bool isMonolithic = (packageByte & 0x80) == 0;
                int dieCount = ((packageByte >> 4) & 0x07);

                var packageTypeItems = new List<ComboBoxItem>();
                packageTypeItems.Add(new ComboBoxItem { Content = "Monolithic", Tag = "True" });
                packageTypeItems.Add(new ComboBoxItem { Content = "Stacked", Tag = "False" });

                fields.Add(new EditField
                {
                    Id = "PackageMonolithic",
                    Label = "Package Type",
                    Value = isMonolithic ? "True" : "False",
                    Type = EditFieldType.ComboBox,
                    ComboBoxItems = packageTypeItems,
                    ToolTip = "Byte 6, bit 7: 0=Monolithic, 1=Stacked",
                    Category = "DensityDie"
                });

                // Die Count через ComboBox
                var dieCountItems = new List<ComboBoxItem>();
                for (int i = 0; i <= 7; i++)
                {
                    int actualCount = i + 1; // value+1 = реальное количество
                    dieCountItems.Add(new ComboBoxItem
                    {
                        Content = $"{actualCount} die{(actualCount > 1 ? "s" : "")}",
                        Tag = i.ToString()
                    });
                }

                fields.Add(new EditField
                {
                    Id = "PackageDieCount",
                    Label = "Die Count",
                    Value = dieCount.ToString(),
                    Type = EditFieldType.ComboBox,
                    ComboBoxItems = dieCountItems,
                    ToolTip = "Byte 6, bits 6-4: Die Count (0-7, value+1 = actual count)",
                    Category = "DensityDie"
                });
            }

            // Banks, Bank Groups, Column Addresses, Row Addresses (bytes 4-5)
            if (Data.Length > 5)
            {
                // Banks Per Group (byte 4, bits 5-4)
                int banksCode = (Data[4] >> 4) & 0x3;
                int[] banksMap = { 4, 8, -1, -1 };
                int banks = banksMap[banksCode];
                
                var banksItems = new List<ComboBoxItem>();
                banksItems.Add(new ComboBoxItem { Content = "4 banks", Tag = "0" });
                banksItems.Add(new ComboBoxItem { Content = "8 banks", Tag = "1" });

                fields.Add(new EditField
                {
                    Id = "Banks",
                    Label = "Banks",
                    Value = banksCode.ToString(),
                    Type = EditFieldType.ComboBox,
                    ComboBoxItems = banksItems,
                    ToolTip = "Byte 4, bits 5-4: Banks per group (0=4, 1=8)",
                    Category = "DensityDie"
                });

                // Bank Groups (byte 4, bits 7-6)
                int bankGroupsCode = (Data[4] >> 6) & 0x3;
                int[] bankGroupsMap = { 0, 2, 4, -1 };
                int bankGroups = bankGroupsMap[bankGroupsCode];
                
                var bankGroupsItems = new List<ComboBoxItem>();
                bankGroupsItems.Add(new ComboBoxItem { Content = "0 groups", Tag = "0" });
                bankGroupsItems.Add(new ComboBoxItem { Content = "2 groups", Tag = "1" });
                bankGroupsItems.Add(new ComboBoxItem { Content = "4 groups", Tag = "2" });

                fields.Add(new EditField
                {
                    Id = "BankGroups",
                    Label = "Bank Groups",
                    Value = bankGroupsCode.ToString(),
                    Type = EditFieldType.ComboBox,
                    ComboBoxItems = bankGroupsItems,
                    ToolTip = "Byte 4, bits 7-6: Bank groups (0=0, 1=2, 2=4)",
                    Category = "DensityDie"
                });

                // Column Addresses (byte 5, bits 2-0)
                int columnCode = Data[5] & 0x7;
                int[] columnMap = { 9, 10, 11, 12, -1, -1, -1, -1 };
                int columns = columnMap[columnCode];
                
                var columnItems = new List<ComboBoxItem>();
                columnItems.Add(new ComboBoxItem { Content = "9 bits", Tag = "0" });
                columnItems.Add(new ComboBoxItem { Content = "10 bits", Tag = "1" });
                columnItems.Add(new ComboBoxItem { Content = "11 bits", Tag = "2" });
                columnItems.Add(new ComboBoxItem { Content = "12 bits", Tag = "3" });

                fields.Add(new EditField
                {
                    Id = "ColumnAddresses",
                    Label = "Column Addresses",
                    Value = columnCode.ToString(),
                    Type = EditFieldType.ComboBox,
                    ComboBoxItems = columnItems,
                    ToolTip = "Byte 5, bits 2-0: Column addresses (0=9, 1=10, 2=11, 3=12)",
                    Category = "DensityDie"
                });

                // Row Addresses (byte 5, bits 6-3)
                int rowCode = (Data[5] >> 3) & 0x7;
                int[] rowMap = { 12, 13, 14, 15, 16, 17, 18, -1 };
                int rows = rowMap[rowCode];
                
                var rowItems = new List<ComboBoxItem>();
                rowItems.Add(new ComboBoxItem { Content = "12 bits", Tag = "0" });
                rowItems.Add(new ComboBoxItem { Content = "13 bits", Tag = "1" });
                rowItems.Add(new ComboBoxItem { Content = "14 bits", Tag = "2" });
                rowItems.Add(new ComboBoxItem { Content = "15 bits", Tag = "3" });
                rowItems.Add(new ComboBoxItem { Content = "16 bits", Tag = "4" });
                rowItems.Add(new ComboBoxItem { Content = "17 bits", Tag = "5" });
                rowItems.Add(new ComboBoxItem { Content = "18 bits", Tag = "6" });

                fields.Add(new EditField
                {
                    Id = "RowAddresses",
                    Label = "Row Addresses",
                    Value = rowCode.ToString(),
                    Type = EditFieldType.ComboBox,
                    ComboBoxItems = rowItems,
                    ToolTip = "Byte 5, bits 6-3: Row addresses (0=12, 1=13, 2=14, 3=15, 4=16, 5=17, 6=18)",
                    Category = "DensityDie"
                });
            }

            // Timing parameters
            if (Data.Length > 125)
            {
                // tCK (bytes 18, 125)
                fields.Add(new EditField
                {
                    Id = "TimingTckMtb",
                    Label = "tCK (Clock Period) MTB",
                    Value = Data[18].ToString(),
                    Type = EditFieldType.TextBox,
                    ToolTip = "Byte 18: tCK Medium Timebase",
                    Category = "Timing"
                });

                fields.Add(new EditField
                {
                    Id = "TimingTckFtb",
                    Label = "tCK (Clock Period) FTB",
                    Value = ((sbyte)Data[125]).ToString(),
                    Type = EditFieldType.TextBox,
                    ToolTip = "Byte 125: tCK Fine Timebase (signed)",
                    Category = "Timing"
                });

                // tAA (bytes 24, 123)
                if (Data.Length > 123)
                {
                    fields.Add(new EditField
                    {
                        Id = "TimingTaaMtb",
                        Label = "CAS Latency (tAA) MTB",
                        Value = Data[24].ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 24: tAA Medium Timebase",
                        Category = "Timing"
                    });

                    fields.Add(new EditField
                    {
                        Id = "TimingTaaFtb",
                        Label = "CAS Latency (tAA) FTB",
                        Value = ((sbyte)Data[123]).ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 123: tAA Fine Timebase (signed)",
                        Category = "Timing"
                    });
                }

                // tRCD (bytes 25, 122)
                if (Data.Length > 122)
                {
                    fields.Add(new EditField
                    {
                        Id = "TimingTrcdMtb",
                        Label = "tRCD MTB",
                        Value = Data[25].ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 25: tRCD Medium Timebase",
                        Category = "Timing"
                    });

                    fields.Add(new EditField
                    {
                        Id = "TimingTrcdFtb",
                        Label = "tRCD FTB",
                        Value = ((sbyte)Data[122]).ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 122: tRCD Fine Timebase (signed)",
                        Category = "Timing"
                    });
                }

                // tRP (bytes 26, 121)
                if (Data.Length > 121)
                {
                    fields.Add(new EditField
                    {
                        Id = "TimingTrpMtb",
                        Label = "tRP MTB",
                        Value = Data[26].ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 26: tRP Medium Timebase",
                        Category = "Timing"
                    });

                    fields.Add(new EditField
                    {
                        Id = "TimingTrpFtb",
                        Label = "tRP FTB",
                        Value = ((sbyte)Data[121]).ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 121: tRP Fine Timebase (signed)",
                        Category = "Timing"
                    });
                }

                // tRAS (bytes 28, 27 bits 3-0)
                if (Data.Length > 28)
                {
                    byte byte27 = Data[27];
                    byte byte28 = Data[28];
                    int trasValue = byte28 | ((byte27 & 0x0F) << 8);

                    fields.Add(new EditField
                    {
                        Id = "TimingTras",
                        Label = "tRAS (composite)",
                        Value = trasValue.ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 28: tRAS LSB, Byte 27 bits 3-0: tRAS MSB",
                        Category = "Timing"
                    });
                }

                // tRC (bytes 29, 27 bits 7-4, FTB byte 120)
                if (Data.Length > 120)
                {
                    byte byte27 = Data[27];
                    byte byte29 = Data[29];
                    int trcValue = byte29 | (((byte27 >> 4) & 0x0F) << 8);

                    fields.Add(new EditField
                    {
                        Id = "TimingTrc",
                        Label = "tRC (composite)",
                        Value = trcValue.ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 29: tRC LSB, Byte 27 bits 7-4: tRC MSB",
                        Category = "Timing"
                    });

                    fields.Add(new EditField
                    {
                        Id = "TimingTrcFtb",
                        Label = "tRC FTB",
                        Value = ((sbyte)Data[120]).ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 120: tRC Fine Timebase (signed)",
                        Category = "Timing"
                    });
                }

                // tFAW (bytes 37, 36 bits 3-0)
                if (Data.Length > 37)
                {
                    byte byte36 = Data[36];
                    byte byte37 = Data[37];
                    int tfawValue = byte37 | ((byte36 & 0x0F) << 8);

                    fields.Add(new EditField
                    {
                        Id = "TimingTfaw",
                        Label = "tFAW (composite)",
                        Value = tfawValue.ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 37: tFAW LSB, Byte 36 bits 3-0: tFAW MSB",
                        Category = "Timing"
                    });
                }

                // tRRD_S (bytes 38, 119)
                if (Data.Length > 119)
                {
                    fields.Add(new EditField
                    {
                        Id = "TimingTrrdSMtb",
                        Label = "tRRD_S MTB",
                        Value = Data[38].ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 38: tRRD_S Medium Timebase",
                        Category = "Timing"
                    });

                    fields.Add(new EditField
                    {
                        Id = "TimingTrrdSFtb",
                        Label = "tRRD_S FTB",
                        Value = ((sbyte)Data[119]).ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 119: tRRD_S Fine Timebase (signed)",
                        Category = "Timing"
                    });
                }

                // tRRD_L (bytes 39, 118)
                if (Data.Length > 118)
                {
                    fields.Add(new EditField
                    {
                        Id = "TimingTrrdLMtb",
                        Label = "tRRD_L MTB",
                        Value = Data[39].ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 39: tRRD_L Medium Timebase",
                        Category = "Timing"
                    });

                    fields.Add(new EditField
                    {
                        Id = "TimingTrrdLFtb",
                        Label = "tRRD_L FTB",
                        Value = ((sbyte)Data[118]).ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 118: tRRD_L Fine Timebase (signed)",
                        Category = "Timing"
                    });
                }

                // tCCDL (bytes 40, 117)
                if (Data.Length > 117)
                {
                    fields.Add(new EditField
                    {
                        Id = "TimingCcdlMtb",
                        Label = "tCCDL MTB",
                        Value = Data[40].ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 40: tCCDL Medium Timebase",
                        Category = "Timing"
                    });

                    fields.Add(new EditField
                    {
                        Id = "TimingCcdlFtb",
                        Label = "tCCDL FTB",
                        Value = ((sbyte)Data[117]).ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 117: tCCDL Fine Timebase (signed)",
                        Category = "Timing"
                    });
                }

                // tWR (bytes 42, 41 bits 3-0)
                if (Data.Length > 42)
                {
                    byte byte41 = Data[41];
                    byte byte42 = Data[42];
                    int twrValue = byte42 | ((byte41 & 0x0F) << 8);

                    fields.Add(new EditField
                    {
                        Id = "TimingTwr",
                        Label = "tWR (composite)",
                        Value = twrValue.ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 42: tWR LSB, Byte 41 bits 3-0: tWR MSB",
                        Category = "Timing"
                    });
                }

                // tWTR_S (bytes 44, 43 bits 3-0)
                if (Data.Length > 44)
                {
                    byte byte43 = Data[43];
                    byte byte44 = Data[44];
                    int twtrsValue = byte44 | ((byte43 & 0x0F) << 8);

                    fields.Add(new EditField
                    {
                        Id = "TimingTwtrs",
                        Label = "tWTR_S (composite)",
                        Value = twtrsValue.ToString(),
                        Type = EditFieldType.TextBox,
                        ToolTip = "Byte 44: tWTR_S LSB, Byte 43 bits 3-0: tWTR_S MSB",
                        Category = "Timing"
                    });
                }
            }

            // Module Configuration fields
            if (Data.Length > 13)
            {
                // Ranks (byte 12, bits 5-3)
                int ranks = ((Data[12] >> 3) & 0x7) + 1;
                fields.Add(new EditField
                {
                    Id = "ModuleRanks",
                    Label = "Number of Ranks",
                    Value = ranks.ToString(),
                    Type = EditFieldType.TextBox,
                    MaxLength = 1,
                    ToolTip = "Byte 12, bits 5-3: Number of ranks (1-8, value+1)",
                    Category = "ModuleConfig"
                });

                // Device Width (byte 12, bits 2-0)
                int deviceWidth = 4 << (Data[12] & 0x7);
                fields.Add(new EditField
                {
                    Id = "DeviceWidth",
                    Label = "Device Width (bits)",
                    Value = deviceWidth.ToString(),
                    Type = EditFieldType.TextBox,
                    ToolTip = "Byte 12, bits 2-0: Device width (4, 8, 16, 32 bits)",
                    Category = "ModuleConfig"
                });

                // Primary Bus Width (byte 13, bits 2-0)
                int primaryBusWidth = 8 << (Data[13] & 0x7);
                fields.Add(new EditField
                {
                    Id = "PrimaryBusWidth",
                    Label = "Primary Bus Width (bits)",
                    Value = primaryBusWidth.ToString(),
                    Type = EditFieldType.TextBox,
                    ToolTip = "Byte 13, bits 2-0: Primary bus width (8, 16, 32, 64 bits)",
                    Category = "ModuleConfig"
                });

                // ECC (byte 13, bit 3)
                bool hasEcc = ((Data[13] >> 3) & 0x1) != 0;
                fields.Add(new EditField
                {
                    Id = "HasEcc",
                    Label = "ECC Support",
                    Value = hasEcc ? "True" : "False",
                    Type = EditFieldType.CheckBox,
                    ToolTip = "Byte 13, bit 3: ECC support (0=No, 1=Yes)",
                    Category = "ModuleConfig"
                });

                // Rank Mix (byte 12, bit 6)
                bool rankMix = ((Data[12] >> 6) & 0x1) != 0;
                var rankMixItems = new List<ComboBoxItem>();
                rankMixItems.Add(new ComboBoxItem { Content = "Symmetrical", Tag = "False" });
                rankMixItems.Add(new ComboBoxItem { Content = "Asymmetrical", Tag = "True" });

                fields.Add(new EditField
                {
                    Id = "RankMix",
                    Label = "Rank Mix",
                    Value = rankMix ? "True" : "False",
                    Type = EditFieldType.ComboBox,
                    ComboBoxItems = rankMixItems,
                    ToolTip = "Byte 12, bit 6: Rank mix (0=Symmetrical, 1=Asymmetrical)",
                    Category = "ModuleConfig"
                });
            }

            // Thermal Sensor (byte 14, bit 7)
            if (Data.Length > 14)
            {
                bool hasThermalSensor = (Data[14] & 0x80) != 0;
                fields.Add(new EditField
                {
                    Id = "ThermalSensor",
                    Label = "Thermal Sensor",
                    Value = hasThermalSensor ? "True" : "False",
                    Type = EditFieldType.CheckBox,
                    ToolTip = "Byte 14, bit 7: Thermal sensor present (0=No, 1=Yes)",
                    Category = "ModuleConfig"
                });
            }

            // Supply Voltage (byte 11)
            if (Data.Length > 11)
            {
                bool operable = (Data[11] & 0x01) != 0;
                fields.Add(new EditField
                {
                    Id = "SupplyVoltageOperable",
                    Label = "Supply Voltage Operable",
                    Value = operable ? "True" : "False",
                    Type = EditFieldType.CheckBox,
                    ToolTip = "Byte 11, bit 0: Supply voltage operable (0=No, 1=Yes)",
                    Category = "ModuleConfig"
                });
            }

            // XMP Profiles (bytes 384+)
            if (HasXmpHeader())
            {
                AddXmpProfileFields(fields, profileIndex: 1, baseOffset: 0x189);
                AddXmpProfileFields(fields, profileIndex: 2, baseOffset: 0x1C8);
            }

            return fields;
        }

        /// <summary>
        /// Проверяет наличие XMP заголовка
        /// </summary>
        private bool HasXmpHeader()
        {
            if (Data == null || Data.Length <= 386)
                return false;

            bool isXmp = Data[384] == (byte)'X' &&
                         Data[385] == (byte)'M' &&
                         Data[386] == (byte)'P';

            bool legacyOrder = Data[384] == 0x50 &&
                               Data[385] == 0x4D &&
                               Data[386] == 0x58;

            return isXmp || legacyOrder;
        }

        /// <summary>
        /// Добавляет поля редактирования для XMP профиля
        /// </summary>
        private void AddXmpProfileFields(List<EditField> fields, int profileIndex, int baseOffset)
        {
            if (Data == null || Data.Length < baseOffset + 47)
                return;

            // Проверяем, включен ли профиль (байт 0x182, bit 0 для Profile 1, bit 1 для Profile 2)
            bool isEnabled = Data.Length > 0x182 && (Data[0x182] & (1 << (profileIndex - 1))) != 0;
            if (!isEnabled)
                return;

            string profilePrefix = $"XMP{profileIndex}_";
            string category = "XMP";

            // Voltage (offset 0)
            uint voltage = DecodeXmpVoltage(baseOffset);
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}Voltage",
                Label = $"XMP Profile {profileIndex} - Voltage (V)",
                Value = voltage.ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset}: Voltage in hundredths (0-227 = 0.00-2.27V)",
                Category = category
            });

            // SDRAM Cycle Time Ticks (offset 3)
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}SDRAMCycleTicks",
                Label = $"XMP Profile {profileIndex} - SDRAM Cycle Time (MTB ticks)",
                Value = Data[baseOffset + 3].ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 3}: SDRAM Cycle Time in MTB ticks",
                Category = category
            });

            // SDRAM Cycle Time Fine Correction (offset 38)
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}SDRAMCycleTimeFC",
                Label = $"XMP Profile {profileIndex} - SDRAM Cycle Time FC (ps)",
                Value = ((sbyte)Data[baseOffset + 38]).ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 38}: Fine Correction in picoseconds (-127 to +127)",
                Category = category
            });

            // CL Supported (offsets 4-6) - три байта для CL 7-30
            // Добавляем как отдельные поля для каждого байта
            for (int i = 0; i < 3; i++)
            {
                fields.Add(new EditField
                {
                    Id = $"{profilePrefix}CLSupported{i}",
                    Label = $"XMP Profile {profileIndex} - CL Supported Byte {i}",
                    Value = Data[baseOffset + 4 + i].ToString("X2"),
                    Type = EditFieldType.TextBox,
                    MaxLength = 2,
                    ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 4 + i}: CL Supported bits (hex)",
                    Category = category
                });
            }

            // CL Ticks (offset 8)
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}CLTicks",
                Label = $"XMP Profile {profileIndex} - CAS Latency (MTB ticks)",
                Value = Data[baseOffset + 8].ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 8}: CAS Latency in MTB ticks",
                Category = category
            });

            // CL Fine Correction (offset 37)
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}CLFC",
                Label = $"XMP Profile {profileIndex} - CAS Latency FC (ps)",
                Value = ((sbyte)Data[baseOffset + 37]).ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 37}: CL Fine Correction in picoseconds (-127 to +127)",
                Category = category
            });

            // tRCD Ticks (offset 9)
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}RCDTicks",
                Label = $"XMP Profile {profileIndex} - tRCD (MTB ticks)",
                Value = Data[baseOffset + 9].ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 9}: tRCD in MTB ticks",
                Category = category
            });

            // tRCD Fine Correction (offset 36)
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}RCDFC",
                Label = $"XMP Profile {profileIndex} - tRCD FC (ps)",
                Value = ((sbyte)Data[baseOffset + 36]).ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 36}: tRCD Fine Correction in picoseconds (-127 to +127)",
                Category = category
            });

            // tRP Ticks (offset 10)
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}RPTicks",
                Label = $"XMP Profile {profileIndex} - tRP (MTB ticks)",
                Value = Data[baseOffset + 10].ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 10}: tRP in MTB ticks",
                Category = category
            });

            // tRP Fine Correction (offset 35)
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}RPFC",
                Label = $"XMP Profile {profileIndex} - tRP FC (ps)",
                Value = ((sbyte)Data[baseOffset + 35]).ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 35}: tRP Fine Correction in picoseconds (-127 to +127)",
                Category = category
            });

            // tRAS (composite: offset 12 + bits 3-0 of offset 11)
            int rasTicks = Data[baseOffset + 12] | ((Data[baseOffset + 11] & 0x0F) << 8);
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}RASTicks",
                Label = $"XMP Profile {profileIndex} - tRAS (MTB ticks)",
                Value = rasTicks.ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 12} + {baseOffset + 11} bits 3-0: tRAS in MTB ticks (0-4095)",
                Category = category
            });

            // tRC (composite: offset 13 + bits 7-4 of offset 11)
            int rcTicks = Data[baseOffset + 13] | (((Data[baseOffset + 11] >> 4) & 0x0F) << 8);
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}RCTicks",
                Label = $"XMP Profile {profileIndex} - tRC (MTB ticks)",
                Value = rcTicks.ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 13} + {baseOffset + 11} bits 7-4: tRC in MTB ticks (0-4095)",
                Category = category
            });

            // tRC Fine Correction (offset 34)
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}RCFC",
                Label = $"XMP Profile {profileIndex} - tRC FC (ps)",
                Value = ((sbyte)Data[baseOffset + 34]).ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 34}: tRC Fine Correction in picoseconds (-127 to +127)",
                Category = category
            });

            // tRFC1 (offsets 14-15, little-endian ushort)
            ushort rfc1Ticks = (ushort)(Data[baseOffset + 14] | (Data[baseOffset + 15] << 8));
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}RFC1Ticks",
                Label = $"XMP Profile {profileIndex} - tRFC1 (MTB ticks)",
                Value = rfc1Ticks.ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 14}-{baseOffset + 15}: tRFC1 in MTB ticks (0-65535)",
                Category = category
            });

            // tRFC2 (offsets 16-17, little-endian ushort)
            ushort rfc2Ticks = (ushort)(Data[baseOffset + 16] | (Data[baseOffset + 17] << 8));
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}RFC2Ticks",
                Label = $"XMP Profile {profileIndex} - tRFC2 (MTB ticks)",
                Value = rfc2Ticks.ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 16}-{baseOffset + 17}: tRFC2 in MTB ticks (0-65535)",
                Category = category
            });

            // tRFC4 (offsets 18-19, little-endian ushort)
            ushort rfc4Ticks = (ushort)(Data[baseOffset + 18] | (Data[baseOffset + 19] << 8));
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}RFC4Ticks",
                Label = $"XMP Profile {profileIndex} - tRFC4 (MTB ticks)",
                Value = rfc4Ticks.ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 18}-{baseOffset + 19}: tRFC4 in MTB ticks (0-65535)",
                Category = category
            });

            // tFAW (composite: offset 21 + bits 3-0 of offset 20)
            int fawTicks = Data[baseOffset + 21] | ((Data[baseOffset + 20] & 0x0F) << 8);
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}FAWTicks",
                Label = $"XMP Profile {profileIndex} - tFAW (MTB ticks)",
                Value = fawTicks.ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 21} + {baseOffset + 20} bits 3-0: tFAW in MTB ticks (0-4095)",
                Category = category
            });

            // tRRDS Ticks (offset 22)
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}RRDSTicks",
                Label = $"XMP Profile {profileIndex} - tRRDS (MTB ticks)",
                Value = Data[baseOffset + 22].ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 22}: tRRDS in MTB ticks",
                Category = category
            });

            // tRRDS Fine Correction (offset 33)
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}RRDSFC",
                Label = $"XMP Profile {profileIndex} - tRRDS FC (ps)",
                Value = ((sbyte)Data[baseOffset + 33]).ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 33}: tRRDS Fine Correction in picoseconds (-127 to +127)",
                Category = category
            });

            // tRRDL Ticks (offset 23)
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}RRDLTicks",
                Label = $"XMP Profile {profileIndex} - tRRDL (MTB ticks)",
                Value = Data[baseOffset + 23].ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 23}: tRRDL in MTB ticks",
                Category = category
            });

            // tRRDL Fine Correction (offset 32)
            fields.Add(new EditField
            {
                Id = $"{profilePrefix}RRDLFC",
                Label = $"XMP Profile {profileIndex} - tRRDL FC (ps)",
                Value = ((sbyte)Data[baseOffset + 32]).ToString(),
                Type = EditFieldType.Numeric,
                ToolTip = $"XMP Profile {profileIndex}, offset {baseOffset + 32}: tRRDL Fine Correction in picoseconds (-127 to +127)",
                Category = category
            });
        }

        /// <summary>
        /// Декодирует напряжение XMP из байта
        /// </summary>
        private uint DecodeXmpVoltage(int offset)
        {
            if (Data == null || Data.Length <= offset)
                return 0;

            byte value = Data[offset];
            uint integer = (uint)((value >> 7) & 0x1);
            uint fraction = (uint)(value & 0x7F);
            return integer * 100 + fraction; // Возвращаем в сотых долях вольта (например, 135 = 1.35V)
        }

        public override List<SpdEditPanel.ByteChange> ApplyChanges(Dictionary<string, string> fieldValues)
        {
            if (Data == null)
                return new List<SpdEditPanel.ByteChange>();

            var changes = ApplyCommonChanges(fieldValues);

            // DRAM Manufacturer (bytes 350-351)
            if (fieldValues.TryGetValue("DramManufacturer", out string? dramManufacturerText) &&
                Data.Length > 351 &&
                !string.IsNullOrWhiteSpace(dramManufacturerText))
            {
                // dramManufacturerText из ComboBox - hex строка (например, "80AD" или "0x80AD")
                string hexString = dramManufacturerText.Trim();
                if (hexString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    hexString = hexString.Substring(2);
                }
                
                if (ushort.TryParse(hexString, System.Globalization.NumberStyles.HexNumber, null, out ushort dramManufacturerId))
                {
                    // Сохраняем байты с parity битами (как в JEDEC стандарте)
                    byte newByte350 = (byte)(dramManufacturerId >> 8);
                    byte newByte351 = (byte)(dramManufacturerId & 0xFF);
                    if (Data[350] != newByte350)
                    {
                        Data[350] = newByte350;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 350, NewData = new[] { newByte350 } });
                    }
                    if (Data[351] != newByte351)
                    {
                        Data[351] = newByte351;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 351, NewData = new[] { newByte351 } });
                    }
                }
            }

            // Density, Banks, Bank Groups (byte 4)
            if (Data.Length > 4)
            {
                byte oldByte4 = Data[4];
                byte newByte4 = oldByte4;

                // Density (bits 3-0)
                if (fieldValues.TryGetValue("Density", out string? densityText) &&
                    byte.TryParse(densityText, out byte densityCode) && densityCode <= 9)
                {
                    newByte4 = (byte)((newByte4 & 0xF0) | (densityCode & 0x0F));
                }

                // Banks (bits 5-4)
                if (fieldValues.TryGetValue("Banks", out string? banksText) &&
                    byte.TryParse(banksText, out byte banksCode) && banksCode <= 1)
                {
                    newByte4 = (byte)((newByte4 & 0xCF) | ((banksCode & 0x3) << 4));
                }

                // Bank Groups (bits 7-6)
                if (fieldValues.TryGetValue("BankGroups", out string? bankGroupsText) &&
                    byte.TryParse(bankGroupsText, out byte bankGroupsCode) && bankGroupsCode <= 2)
                {
                    newByte4 = (byte)((newByte4 & 0x3F) | ((bankGroupsCode & 0x3) << 6));
                }

                if (oldByte4 != newByte4)
                {
                    Data[4] = newByte4;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = 4, NewData = new[] { newByte4 } });
                }
            }

            // Column Addresses, Row Addresses (byte 5)
            if (Data.Length > 5)
            {
                byte oldByte5 = Data[5];
                byte newByte5 = oldByte5;

                // Column Addresses (bits 2-0)
                if (fieldValues.TryGetValue("ColumnAddresses", out string? columnText) &&
                    byte.TryParse(columnText, out byte columnCode) && columnCode <= 3)
                {
                    newByte5 = (byte)((newByte5 & 0xF8) | (columnCode & 0x7));
                }

                // Row Addresses (bits 6-3)
                if (fieldValues.TryGetValue("RowAddresses", out string? rowText) &&
                    byte.TryParse(rowText, out byte rowCode) && rowCode <= 6)
                {
                    newByte5 = (byte)((newByte5 & 0x87) | ((rowCode & 0x7) << 3));
                }

                if (oldByte5 != newByte5)
                {
                    Data[5] = newByte5;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = 5, NewData = new[] { newByte5 } });
                }
            }

            // Package Type (byte 6)
            if (Data.Length > 6)
            {
                byte oldPackageByte = Data[6];
                byte packageByte = oldPackageByte;
                bool isMonolithic = fieldValues.TryGetValue("PackageMonolithic", out string? monolithicText) &&
                                    (monolithicText == "True" || monolithicText == "true");

                if (fieldValues.TryGetValue("PackageDieCount", out string? dieCountText) &&
                    int.TryParse(dieCountText, out int dieCount) && dieCount >= 0 && dieCount <= 7)
                {
                    packageByte = (byte)(packageByte & 0x8F); // Clear bits 6-4
                    packageByte |= (byte)((dieCount & 0x07) << 4);
                }

                if (isMonolithic)
                    packageByte &= 0x7F; // Clear bit 7
                else
                    packageByte |= 0x80; // Set bit 7

                if (oldPackageByte != packageByte)
                {
                    Data[6] = packageByte;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = 6, NewData = new[] { packageByte } });
                }
            }

            // Timing parameters
            if (Data.Length > 125)
            {
                // tCK (bytes 18, 125)
                if (fieldValues.TryGetValue("TimingTckMtb", out string? tckMtbText) &&
                    byte.TryParse(tckMtbText, out byte tckMtb) && Data[18] != tckMtb)
                {
                    Data[18] = tckMtb;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = 18, NewData = new[] { tckMtb } });
                }
                if (fieldValues.TryGetValue("TimingTckFtb", out string? tckFtbText) &&
                    sbyte.TryParse(tckFtbText, out sbyte tckFtb) && Data[125] != (byte)tckFtb)
                {
                    Data[125] = (byte)tckFtb;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = 125, NewData = new[] { (byte)tckFtb } });
                }

                // tAA (bytes 24, 123)
                if (Data.Length > 123)
                {
                    if (fieldValues.TryGetValue("TimingTaaMtb", out string? taaMtbText) &&
                        byte.TryParse(taaMtbText, out byte taaMtb) && Data[24] != taaMtb)
                    {
                        Data[24] = taaMtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 24, NewData = new[] { taaMtb } });
                    }
                    if (fieldValues.TryGetValue("TimingTaaFtb", out string? taaFtbText) &&
                        sbyte.TryParse(taaFtbText, out sbyte taaFtb) && Data[123] != (byte)taaFtb)
                    {
                        Data[123] = (byte)taaFtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 123, NewData = new[] { (byte)taaFtb } });
                    }
                }

                // tRCD (bytes 25, 122)
                if (Data.Length > 122)
                {
                    if (fieldValues.TryGetValue("TimingTrcdMtb", out string? trcdMtbText) &&
                        byte.TryParse(trcdMtbText, out byte trcdMtb) && Data[25] != trcdMtb)
                    {
                        Data[25] = trcdMtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 25, NewData = new[] { trcdMtb } });
                    }
                    if (fieldValues.TryGetValue("TimingTrcdFtb", out string? trcdFtbText) &&
                        sbyte.TryParse(trcdFtbText, out sbyte trcdFtb) && Data[122] != (byte)trcdFtb)
                    {
                        Data[122] = (byte)trcdFtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 122, NewData = new[] { (byte)trcdFtb } });
                    }
                }

                // tRP (bytes 26, 121)
                if (Data.Length > 121)
                {
                    if (fieldValues.TryGetValue("TimingTrpMtb", out string? trpMtbText) &&
                        byte.TryParse(trpMtbText, out byte trpMtb) && Data[26] != trpMtb)
                    {
                        Data[26] = trpMtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 26, NewData = new[] { trpMtb } });
                    }
                    if (fieldValues.TryGetValue("TimingTrpFtb", out string? trpFtbText) &&
                        sbyte.TryParse(trpFtbText, out sbyte trpFtb) && Data[121] != (byte)trpFtb)
                    {
                        Data[121] = (byte)trpFtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 121, NewData = new[] { (byte)trpFtb } });
                    }
                }

                // tRAS (bytes 28, 27 bits 3-0)
                if (Data.Length > 28 &&
                    fieldValues.TryGetValue("TimingTras", out string? trasText) &&
                    int.TryParse(trasText, out int trasValue))
                {
                    byte newByte28 = (byte)(trasValue & 0xFF);
                    byte byte27 = Data[27];
                    byte newByte27 = (byte)((byte27 & 0xF0) | ((trasValue >> 8) & 0x0F));

                    if (Data[28] != newByte28)
                    {
                        Data[28] = newByte28;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 28, NewData = new[] { newByte28 } });
                    }
                    if (Data[27] != newByte27)
                    {
                        Data[27] = newByte27;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 27, NewData = new[] { newByte27 } });
                    }
                }

                // tRC (bytes 29, 27 bits 7-4, FTB byte 120)
                if (Data.Length > 120 &&
                    fieldValues.TryGetValue("TimingTrc", out string? trcText) &&
                    int.TryParse(trcText, out int trcValue))
                {
                    byte newByte29 = (byte)(trcValue & 0xFF);
                    byte byte27 = Data[27];
                    byte newByte27 = (byte)((byte27 & 0x0F) | (((trcValue >> 8) & 0x0F) << 4));

                    if (Data[29] != newByte29)
                    {
                        Data[29] = newByte29;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 29, NewData = new[] { newByte29 } });
                    }
                    if (Data[27] != newByte27)
                    {
                        Data[27] = newByte27;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 27, NewData = new[] { newByte27 } });
                    }
                }
                if (Data.Length > 120 &&
                    fieldValues.TryGetValue("TimingTrcFtb", out string? trcFtbText) &&
                    sbyte.TryParse(trcFtbText, out sbyte trcFtb) && Data[120] != (byte)trcFtb)
                {
                    Data[120] = (byte)trcFtb;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = 120, NewData = new[] { (byte)trcFtb } });
                }

                // tFAW (bytes 37, 36 bits 3-0)
                if (Data.Length > 37 &&
                    fieldValues.TryGetValue("TimingTfaw", out string? tfawText) &&
                    int.TryParse(tfawText, out int tfawValue))
                {
                    byte newByte37 = (byte)(tfawValue & 0xFF);
                    byte byte36 = Data[36];
                    byte newByte36 = (byte)((byte36 & 0xF0) | ((tfawValue >> 8) & 0x0F));

                    if (Data[37] != newByte37)
                    {
                        Data[37] = newByte37;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 37, NewData = new[] { newByte37 } });
                    }
                    if (Data[36] != newByte36)
                    {
                        Data[36] = newByte36;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 36, NewData = new[] { newByte36 } });
                    }
                }

                // tRRD_S (bytes 38, 119)
                if (Data.Length > 119)
                {
                    if (fieldValues.TryGetValue("TimingTrrdSMtb", out string? trrdSMtbText) &&
                        byte.TryParse(trrdSMtbText, out byte trrdSMtb) && Data[38] != trrdSMtb)
                    {
                        Data[38] = trrdSMtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 38, NewData = new[] { trrdSMtb } });
                    }
                    if (fieldValues.TryGetValue("TimingTrrdSFtb", out string? trrdSFtbText) &&
                        sbyte.TryParse(trrdSFtbText, out sbyte trrdSFtb) && Data[119] != (byte)trrdSFtb)
                    {
                        Data[119] = (byte)trrdSFtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 119, NewData = new[] { (byte)trrdSFtb } });
                    }
                }

                // tRRD_L (bytes 39, 118)
                if (Data.Length > 118)
                {
                    if (fieldValues.TryGetValue("TimingTrrdLMtb", out string? trrdLMtbText) &&
                        byte.TryParse(trrdLMtbText, out byte trrdLMtb) && Data[39] != trrdLMtb)
                    {
                        Data[39] = trrdLMtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 39, NewData = new[] { trrdLMtb } });
                    }
                    if (fieldValues.TryGetValue("TimingTrrdLFtb", out string? trrdLFtbText) &&
                        sbyte.TryParse(trrdLFtbText, out sbyte trrdLFtb) && Data[118] != (byte)trrdLFtb)
                    {
                        Data[118] = (byte)trrdLFtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 118, NewData = new[] { (byte)trrdLFtb } });
                    }
                }

                // tCCDL (bytes 40, 117)
                if (Data.Length > 117)
                {
                    if (fieldValues.TryGetValue("TimingCcdlMtb", out string? ccdlMtbText) &&
                        byte.TryParse(ccdlMtbText, out byte ccdlMtb) && Data[40] != ccdlMtb)
                    {
                        Data[40] = ccdlMtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 40, NewData = new[] { ccdlMtb } });
                    }
                    if (fieldValues.TryGetValue("TimingCcdlFtb", out string? ccdlFtbText) &&
                        sbyte.TryParse(ccdlFtbText, out sbyte ccdlFtb) && Data[117] != (byte)ccdlFtb)
                    {
                        Data[117] = (byte)ccdlFtb;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 117, NewData = new[] { (byte)ccdlFtb } });
                    }
                }

                // tWR (bytes 42, 41 bits 3-0)
                if (Data.Length > 42 &&
                    fieldValues.TryGetValue("TimingTwr", out string? twrText) &&
                    int.TryParse(twrText, out int twrValue))
                {
                    byte newByte42 = (byte)(twrValue & 0xFF);
                    byte byte41 = Data[41];
                    byte newByte41 = (byte)((byte41 & 0xF0) | ((twrValue >> 8) & 0x0F));

                    if (Data[42] != newByte42)
                    {
                        Data[42] = newByte42;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 42, NewData = new[] { newByte42 } });
                    }
                    if (Data[41] != newByte41)
                    {
                        Data[41] = newByte41;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 41, NewData = new[] { newByte41 } });
                    }
                }

                // tWTR_S (bytes 44, 43 bits 3-0)
                if (Data.Length > 44 &&
                    fieldValues.TryGetValue("TimingTwtrs", out string? twtrsText) &&
                    int.TryParse(twtrsText, out int twtrsValue))
                {
                    byte newByte44 = (byte)(twtrsValue & 0xFF);
                    byte byte43 = Data[43];
                    byte newByte43 = (byte)((byte43 & 0xF0) | ((twtrsValue >> 8) & 0x0F));

                    if (Data[44] != newByte44)
                    {
                        Data[44] = newByte44;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 44, NewData = new[] { newByte44 } });
                    }
                    if (Data[43] != newByte43)
                    {
                        Data[43] = newByte43;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 43, NewData = new[] { newByte43 } });
                    }
                }
            }

            // Module Configuration fields
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
                    // Valid values: 4, 8, 16, 32
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
                    // Valid values: 8, 16, 32, 64
                    int code = busWidth switch
                    {
                        8 => 0,
                        16 => 1,
                        32 => 2,
                        64 => 3,
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

                // ECC (byte 13, bit 3)
                if (fieldValues.TryGetValue("HasEcc", out string? eccText))
                {
                    bool hasEcc = eccText == "True" || eccText == "true";
                    byte byte13 = Data[13];
                    byte newByte13 = hasEcc
                        ? (byte)(byte13 | 0x08)
                        : (byte)(byte13 & 0xF7);
                    if (Data[13] != newByte13)
                    {
                        Data[13] = newByte13;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 13, NewData = new[] { newByte13 } });
                    }
                }

                // Rank Mix (byte 12, bit 6)
                if (fieldValues.TryGetValue("RankMix", out string? rankMixText))
                {
                    bool rankMix = rankMixText == "True" || rankMixText == "true";
                    byte byte12 = Data[12];
                    byte newByte12 = rankMix
                        ? (byte)(byte12 | 0x40)
                        : (byte)(byte12 & 0xBF);
                    if (Data[12] != newByte12)
                    {
                        Data[12] = newByte12;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 12, NewData = new[] { newByte12 } });
                    }
                }
            }

            // Thermal Sensor (byte 14, bit 7)
            if (Data.Length > 14 &&
                fieldValues.TryGetValue("ThermalSensor", out string? thermalText))
            {
                bool hasThermal = thermalText == "True" || thermalText == "true";
                byte byte14 = Data[14];
                byte newByte14 = hasThermal
                    ? (byte)(byte14 | 0x80)
                    : (byte)(byte14 & 0x7F);
                if (Data[14] != newByte14)
                {
                    Data[14] = newByte14;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = 14, NewData = new[] { newByte14 } });
                }
            }

            // Supply Voltage (byte 11, bit 0)
            // Применяем только если значение явно указано (не пустая строка)
            if (Data.Length > 11 &&
                fieldValues.TryGetValue("SupplyVoltageOperable", out string? voltageText) &&
                !string.IsNullOrWhiteSpace(voltageText))
            {
                bool operable = voltageText == "True" || voltageText == "true";
                byte byte11 = Data[11];
                byte newByte11 = operable
                    ? (byte)(byte11 | 0x01)
                    : (byte)(byte11 & 0xFE);
                if (Data[11] != newByte11)
                {
                    Data[11] = newByte11;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = 11, NewData = new[] { newByte11 } });
                }
            }

            // XMP Profiles
            if (HasXmpHeader())
            {
                ApplyXmpProfileChanges(changes, fieldValues, profileIndex: 1, baseOffset: 0x189);
                ApplyXmpProfileChanges(changes, fieldValues, profileIndex: 2, baseOffset: 0x1C8);
            }

            return changes;
        }

        /// <summary>
        /// Применяет изменения для XMP профиля
        /// </summary>
        private void ApplyXmpProfileChanges(List<SpdEditPanel.ByteChange> changes, Dictionary<string, string> fieldValues, int profileIndex, int baseOffset)
        {
            if (Data == null || Data.Length < baseOffset + 47)
                return;

            string profilePrefix = $"XMP{profileIndex}_";

            // Voltage (offset 0)
            if (fieldValues.TryGetValue($"{profilePrefix}Voltage", out string? voltageText) &&
                uint.TryParse(voltageText, out uint voltage) && voltage <= 227)
            {
                bool ones = voltage >= 100;
                uint hundredths = voltage >= 100 ? voltage - 100 : voltage & 0x7F;
                byte newVoltage = (byte)((ones ? 0x80u : 0x00u) | hundredths);
                if (Data[baseOffset] != newVoltage)
                {
                    Data[baseOffset] = newVoltage;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset, NewData = new[] { newVoltage } });
                }
            }

            // SDRAM Cycle Time Ticks (offset 3)
            if (fieldValues.TryGetValue($"{profilePrefix}SDRAMCycleTicks", out string? cycleTicksText) &&
                byte.TryParse(cycleTicksText, out byte cycleTicks) && Data[baseOffset + 3] != cycleTicks)
            {
                Data[baseOffset + 3] = cycleTicks;
                changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 3, NewData = new[] { cycleTicks } });
            }

            // SDRAM Cycle Time Fine Correction (offset 38)
            if (fieldValues.TryGetValue($"{profilePrefix}SDRAMCycleTimeFC", out string? cycleFcText) &&
                sbyte.TryParse(cycleFcText, out sbyte cycleFc))
            {
                byte newCycleFc = (byte)cycleFc;
                if (Data[baseOffset + 38] != newCycleFc)
                {
                    Data[baseOffset + 38] = newCycleFc;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 38, NewData = new[] { newCycleFc } });
                }
            }

            // CL Supported (offsets 4-6)
            for (int i = 0; i < 3; i++)
            {
                if (fieldValues.TryGetValue($"{profilePrefix}CLSupported{i}", out string? clSupportedText))
                {
                    string hexString = clSupportedText.Trim();
                    if (hexString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        hexString = hexString.Substring(2);
                    
                    if (byte.TryParse(hexString, System.Globalization.NumberStyles.HexNumber, null, out byte clSupported) &&
                        Data[baseOffset + 4 + i] != clSupported)
                    {
                        Data[baseOffset + 4 + i] = clSupported;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 4 + i, NewData = new[] { clSupported } });
                    }
                }
            }

            // CL Ticks (offset 8)
            if (fieldValues.TryGetValue($"{profilePrefix}CLTicks", out string? clTicksText) &&
                byte.TryParse(clTicksText, out byte clTicks) && Data[baseOffset + 8] != clTicks)
            {
                Data[baseOffset + 8] = clTicks;
                changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 8, NewData = new[] { clTicks } });
            }

            // CL Fine Correction (offset 37)
            if (fieldValues.TryGetValue($"{profilePrefix}CLFC", out string? clFcText) &&
                sbyte.TryParse(clFcText, out sbyte clFc))
            {
                byte newClFc = (byte)clFc;
                if (Data[baseOffset + 37] != newClFc)
                {
                    Data[baseOffset + 37] = newClFc;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 37, NewData = new[] { newClFc } });
                }
            }

            // tRCD Ticks (offset 9)
            if (fieldValues.TryGetValue($"{profilePrefix}RCDTicks", out string? rcdTicksText) &&
                byte.TryParse(rcdTicksText, out byte rcdTicks) && Data[baseOffset + 9] != rcdTicks)
            {
                Data[baseOffset + 9] = rcdTicks;
                changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 9, NewData = new[] { rcdTicks } });
            }

            // tRCD Fine Correction (offset 36)
            if (fieldValues.TryGetValue($"{profilePrefix}RCDFC", out string? rcdFcText) &&
                sbyte.TryParse(rcdFcText, out sbyte rcdFc))
            {
                byte newRcdFc = (byte)rcdFc;
                if (Data[baseOffset + 36] != newRcdFc)
                {
                    Data[baseOffset + 36] = newRcdFc;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 36, NewData = new[] { newRcdFc } });
                }
            }

            // tRP Ticks (offset 10)
            if (fieldValues.TryGetValue($"{profilePrefix}RPTicks", out string? rpTicksText) &&
                byte.TryParse(rpTicksText, out byte rpTicks) && Data[baseOffset + 10] != rpTicks)
            {
                Data[baseOffset + 10] = rpTicks;
                changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 10, NewData = new[] { rpTicks } });
            }

            // tRP Fine Correction (offset 35)
            if (fieldValues.TryGetValue($"{profilePrefix}RPFC", out string? rpFcText) &&
                sbyte.TryParse(rpFcText, out sbyte rpFc))
            {
                byte newRpFc = (byte)rpFc;
                if (Data[baseOffset + 35] != newRpFc)
                {
                    Data[baseOffset + 35] = newRpFc;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 35, NewData = new[] { newRpFc } });
                }
            }

            // tRAS (composite: offset 12 + bits 3-0 of offset 11)
            if (fieldValues.TryGetValue($"{profilePrefix}RASTicks", out string? rasTicksText) &&
                int.TryParse(rasTicksText, out int rasTicks) && rasTicks >= 0 && rasTicks <= 4095)
            {
                byte newRasTicks = (byte)(rasTicks & 0xFF);
                byte byte11 = Data[baseOffset + 11];
                byte newByte11 = (byte)((byte11 & 0xF0) | ((rasTicks >> 8) & 0x0F));

                if (Data[baseOffset + 12] != newRasTicks)
                {
                    Data[baseOffset + 12] = newRasTicks;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 12, NewData = new[] { newRasTicks } });
                }
                if (Data[baseOffset + 11] != newByte11)
                {
                    Data[baseOffset + 11] = newByte11;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 11, NewData = new[] { newByte11 } });
                }
            }

            // tRC (composite: offset 13 + bits 7-4 of offset 11)
            if (fieldValues.TryGetValue($"{profilePrefix}RCTicks", out string? rcTicksText) &&
                int.TryParse(rcTicksText, out int rcTicks) && rcTicks >= 0 && rcTicks <= 4095)
            {
                byte newRcTicks = (byte)(rcTicks & 0xFF);
                byte byte11 = Data[baseOffset + 11];
                byte newByte11 = (byte)((byte11 & 0x0F) | (((rcTicks >> 8) & 0x0F) << 4));

                if (Data[baseOffset + 13] != newRcTicks)
                {
                    Data[baseOffset + 13] = newRcTicks;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 13, NewData = new[] { newRcTicks } });
                }
                if (Data[baseOffset + 11] != newByte11)
                {
                    Data[baseOffset + 11] = newByte11;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 11, NewData = new[] { newByte11 } });
                }
            }

            // tRC Fine Correction (offset 34)
            if (fieldValues.TryGetValue($"{profilePrefix}RCFC", out string? rcFcText) &&
                sbyte.TryParse(rcFcText, out sbyte rcFc))
            {
                byte newRcFc = (byte)rcFc;
                if (Data[baseOffset + 34] != newRcFc)
                {
                    Data[baseOffset + 34] = newRcFc;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 34, NewData = new[] { newRcFc } });
                }
            }

            // tRFC1 (offsets 14-15, little-endian ushort)
            if (fieldValues.TryGetValue($"{profilePrefix}RFC1Ticks", out string? rfc1Text) &&
                ushort.TryParse(rfc1Text, out ushort rfc1Ticks))
            {
                byte rfc1Lsb = (byte)(rfc1Ticks & 0xFF);
                byte rfc1Msb = (byte)((rfc1Ticks >> 8) & 0xFF);

                if (Data[baseOffset + 14] != rfc1Lsb)
                {
                    Data[baseOffset + 14] = rfc1Lsb;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 14, NewData = new[] { rfc1Lsb } });
                }
                if (Data[baseOffset + 15] != rfc1Msb)
                {
                    Data[baseOffset + 15] = rfc1Msb;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 15, NewData = new[] { rfc1Msb } });
                }
            }

            // tRFC2 (offsets 16-17, little-endian ushort)
            if (fieldValues.TryGetValue($"{profilePrefix}RFC2Ticks", out string? rfc2Text) &&
                ushort.TryParse(rfc2Text, out ushort rfc2Ticks))
            {
                byte rfc2Lsb = (byte)(rfc2Ticks & 0xFF);
                byte rfc2Msb = (byte)((rfc2Ticks >> 8) & 0xFF);

                if (Data[baseOffset + 16] != rfc2Lsb)
                {
                    Data[baseOffset + 16] = rfc2Lsb;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 16, NewData = new[] { rfc2Lsb } });
                }
                if (Data[baseOffset + 17] != rfc2Msb)
                {
                    Data[baseOffset + 17] = rfc2Msb;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 17, NewData = new[] { rfc2Msb } });
                }
            }

            // tRFC4 (offsets 18-19, little-endian ushort)
            if (fieldValues.TryGetValue($"{profilePrefix}RFC4Ticks", out string? rfc4Text) &&
                ushort.TryParse(rfc4Text, out ushort rfc4Ticks))
            {
                byte rfc4Lsb = (byte)(rfc4Ticks & 0xFF);
                byte rfc4Msb = (byte)((rfc4Ticks >> 8) & 0xFF);

                if (Data[baseOffset + 18] != rfc4Lsb)
                {
                    Data[baseOffset + 18] = rfc4Lsb;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 18, NewData = new[] { rfc4Lsb } });
                }
                if (Data[baseOffset + 19] != rfc4Msb)
                {
                    Data[baseOffset + 19] = rfc4Msb;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 19, NewData = new[] { rfc4Msb } });
                }
            }

            // tFAW (composite: offset 21 + bits 3-0 of offset 20)
            if (fieldValues.TryGetValue($"{profilePrefix}FAWTicks", out string? fawTicksText) &&
                int.TryParse(fawTicksText, out int fawTicks) && fawTicks >= 0 && fawTicks <= 4095)
            {
                byte newFawTicks = (byte)(fawTicks & 0xFF);
                byte byte20 = Data[baseOffset + 20];
                byte newByte20 = (byte)((byte20 & 0xF0) | ((fawTicks >> 8) & 0x0F));

                if (Data[baseOffset + 21] != newFawTicks)
                {
                    Data[baseOffset + 21] = newFawTicks;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 21, NewData = new[] { newFawTicks } });
                }
                if (Data[baseOffset + 20] != newByte20)
                {
                    Data[baseOffset + 20] = newByte20;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 20, NewData = new[] { newByte20 } });
                }
            }

            // tRRDS Ticks (offset 22)
            if (fieldValues.TryGetValue($"{profilePrefix}RRDSTicks", out string? rrdsTicksText) &&
                byte.TryParse(rrdsTicksText, out byte rrdsTicks) && Data[baseOffset + 22] != rrdsTicks)
            {
                Data[baseOffset + 22] = rrdsTicks;
                changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 22, NewData = new[] { rrdsTicks } });
            }

            // tRRDS Fine Correction (offset 33)
            if (fieldValues.TryGetValue($"{profilePrefix}RRDSFC", out string? rrdsFcText) &&
                sbyte.TryParse(rrdsFcText, out sbyte rrdsFc))
            {
                byte newRrdsFc = (byte)rrdsFc;
                if (Data[baseOffset + 33] != newRrdsFc)
                {
                    Data[baseOffset + 33] = newRrdsFc;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 33, NewData = new[] { newRrdsFc } });
                }
            }

            // tRRDL Ticks (offset 23)
            if (fieldValues.TryGetValue($"{profilePrefix}RRDLTicks", out string? rrdlTicksText) &&
                byte.TryParse(rrdlTicksText, out byte rrdlTicks) && Data[baseOffset + 23] != rrdlTicks)
            {
                Data[baseOffset + 23] = rrdlTicks;
                changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 23, NewData = new[] { rrdlTicks } });
            }

            // tRRDL Fine Correction (offset 32)
            if (fieldValues.TryGetValue($"{profilePrefix}RRDLFC", out string? rrdlFcText) &&
                sbyte.TryParse(rrdlFcText, out sbyte rrdlFc))
            {
                byte newRrdlFc = (byte)rrdlFc;
                if (Data[baseOffset + 32] != newRrdlFc)
                {
                    Data[baseOffset + 32] = newRrdlFc;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = baseOffset + 32, NewData = new[] { newRrdlFc } });
                }
            }
        }

        public override Dictionary<string, string> ValidateFields(Dictionary<string, string> fieldValues)
        {
            var errors = new Dictionary<string, string>();

            // Validate hex fields
            // Manufacturer fields are ComboBox, no validation needed (value comes from selected item)

            // Validate BCD fields
            if (fieldValues.TryGetValue("ModuleYear", out string? year) &&
                !string.IsNullOrWhiteSpace(year) && !TryParseBcd(year, out _))
            {
                errors["ModuleYear"] = "Invalid BCD format (00-99)";
            }

            if (fieldValues.TryGetValue("ModuleWeek", out string? week) &&
                !string.IsNullOrWhiteSpace(week) && !TryParseBcd(week, out _))
            {
                errors["ModuleWeek"] = "Invalid BCD format (01-52)";
            }

            // Validate numeric fields
            if (fieldValues.TryGetValue("Density", out string? density) &&
                !string.IsNullOrWhiteSpace(density))
            {
                if (!byte.TryParse(density, out byte densityCode) || densityCode > 9)
                {
                    errors["Density"] = "Density code must be 0-9 (0-7=Standard, 8-9=3DS)";
                }
            }

            if (fieldValues.TryGetValue("PackageDieCount", out string? dieCount) &&
                !string.IsNullOrWhiteSpace(dieCount) &&
                (!int.TryParse(dieCount, out int count) || count < 0 || count > 7))
            {
                errors["PackageDieCount"] = "Die count must be 0-7";
            }

            // Validate XMP fields
            ValidateXmpFields(fieldValues, errors, profileIndex: 1);
            ValidateXmpFields(fieldValues, errors, profileIndex: 2);

            return errors;
        }

        /// <summary>
        /// Валидирует поля XMP профиля
        /// </summary>
        private void ValidateXmpFields(Dictionary<string, string> fieldValues, Dictionary<string, string> errors, int profileIndex)
        {
            string profilePrefix = $"XMP{profileIndex}_";

            // Voltage (0-227 = 0.00-2.27V)
            if (fieldValues.TryGetValue($"{profilePrefix}Voltage", out string? voltageText) &&
                !string.IsNullOrWhiteSpace(voltageText))
            {
                if (!uint.TryParse(voltageText, out uint voltage) || voltage > 227)
                {
                    errors[$"{profilePrefix}Voltage"] = "Voltage must be 0-227 (0.00-2.27V)";
                }
            }

            // SDRAM Cycle Time Ticks (0-255)
            if (fieldValues.TryGetValue($"{profilePrefix}SDRAMCycleTicks", out string? cycleTicksText) &&
                !string.IsNullOrWhiteSpace(cycleTicksText))
            {
                if (!byte.TryParse(cycleTicksText, out _))
                {
                    errors[$"{profilePrefix}SDRAMCycleTicks"] = "Must be 0-255";
                }
            }

            // Fine Correction values (-127 to +127)
            string[] fcFields = { "SDRAMCycleTimeFC", "CLFC", "RCDFC", "RPFC", "RCFC", "RRDSFC", "RRDLFC" };
            foreach (string fcField in fcFields)
            {
                if (fieldValues.TryGetValue($"{profilePrefix}{fcField}", out string? fcText) &&
                    !string.IsNullOrWhiteSpace(fcText))
                {
                    if (!sbyte.TryParse(fcText, out sbyte fc) || fc < -127 || fc > 127)
                    {
                        errors[$"{profilePrefix}{fcField}"] = "Fine Correction must be -127 to +127";
                    }
                }
            }

            // CL Supported (hex bytes)
            for (int i = 0; i < 3; i++)
            {
                if (fieldValues.TryGetValue($"{profilePrefix}CLSupported{i}", out string? clSupportedText) &&
                    !string.IsNullOrWhiteSpace(clSupportedText))
                {
                    string hexString = clSupportedText.Trim();
                    if (hexString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        hexString = hexString.Substring(2);
                    
                    if (!byte.TryParse(hexString, System.Globalization.NumberStyles.HexNumber, null, out _))
                    {
                        errors[$"{profilePrefix}CLSupported{i}"] = "Must be valid hex byte (00-FF)";
                    }
                }
            }

            // Timing ticks (0-255 for byte, 0-4095 for composite, 0-65535 for ushort)
            if (fieldValues.TryGetValue($"{profilePrefix}CLTicks", out string? clTicksText) &&
                !string.IsNullOrWhiteSpace(clTicksText) && !byte.TryParse(clTicksText, out _))
            {
                errors[$"{profilePrefix}CLTicks"] = "Must be 0-255";
            }

            if (fieldValues.TryGetValue($"{profilePrefix}RCDTicks", out string? rcdTicksText) &&
                !string.IsNullOrWhiteSpace(rcdTicksText) && !byte.TryParse(rcdTicksText, out _))
            {
                errors[$"{profilePrefix}RCDTicks"] = "Must be 0-255";
            }

            if (fieldValues.TryGetValue($"{profilePrefix}RPTicks", out string? rpTicksText) &&
                !string.IsNullOrWhiteSpace(rpTicksText) && !byte.TryParse(rpTicksText, out _))
            {
                errors[$"{profilePrefix}RPTicks"] = "Must be 0-255";
            }

            if (fieldValues.TryGetValue($"{profilePrefix}RASTicks", out string? rasTicksText) &&
                !string.IsNullOrWhiteSpace(rasTicksText))
            {
                if (!int.TryParse(rasTicksText, out int rasTicks) || rasTicks < 0 || rasTicks > 4095)
                {
                    errors[$"{profilePrefix}RASTicks"] = "Must be 0-4095";
                }
            }

            if (fieldValues.TryGetValue($"{profilePrefix}RCTicks", out string? rcTicksText) &&
                !string.IsNullOrWhiteSpace(rcTicksText))
            {
                if (!int.TryParse(rcTicksText, out int rcTicks) || rcTicks < 0 || rcTicks > 4095)
                {
                    errors[$"{profilePrefix}RCTicks"] = "Must be 0-4095";
                }
            }

            if (fieldValues.TryGetValue($"{profilePrefix}RFC1Ticks", out string? rfc1Text) &&
                !string.IsNullOrWhiteSpace(rfc1Text))
            {
                if (!ushort.TryParse(rfc1Text, out ushort rfc1) || rfc1 > 65535)
                {
                    errors[$"{profilePrefix}RFC1Ticks"] = "Must be 0-65535";
                }
            }

            if (fieldValues.TryGetValue($"{profilePrefix}RFC2Ticks", out string? rfc2Text) &&
                !string.IsNullOrWhiteSpace(rfc2Text))
            {
                if (!ushort.TryParse(rfc2Text, out ushort rfc2) || rfc2 > 65535)
                {
                    errors[$"{profilePrefix}RFC2Ticks"] = "Must be 0-65535";
                }
            }

            if (fieldValues.TryGetValue($"{profilePrefix}RFC4Ticks", out string? rfc4Text) &&
                !string.IsNullOrWhiteSpace(rfc4Text))
            {
                if (!ushort.TryParse(rfc4Text, out ushort rfc4) || rfc4 > 65535)
                {
                    errors[$"{profilePrefix}RFC4Ticks"] = "Must be 0-65535";
                }
            }

            if (fieldValues.TryGetValue($"{profilePrefix}FAWTicks", out string? fawTicksText) &&
                !string.IsNullOrWhiteSpace(fawTicksText))
            {
                if (!int.TryParse(fawTicksText, out int fawTicks) || fawTicks < 0 || fawTicks > 4095)
                {
                    errors[$"{profilePrefix}FAWTicks"] = "Must be 0-4095";
                }
            }

            if (fieldValues.TryGetValue($"{profilePrefix}RRDSTicks", out string? rrdsTicksText) &&
                !string.IsNullOrWhiteSpace(rrdsTicksText) && !byte.TryParse(rrdsTicksText, out _))
            {
                errors[$"{profilePrefix}RRDSTicks"] = "Must be 0-255";
            }

            if (fieldValues.TryGetValue($"{profilePrefix}RRDLTicks", out string? rrdlTicksText) &&
                !string.IsNullOrWhiteSpace(rrdlTicksText) && !byte.TryParse(rrdlTicksText, out _))
            {
                errors[$"{profilePrefix}RRDLTicks"] = "Must be 0-255";
            }
        }
    }
}

