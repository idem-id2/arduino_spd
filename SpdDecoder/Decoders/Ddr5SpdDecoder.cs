using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HexEditor.Database;
using HexEditor.Constants;

namespace HexEditor.SpdDecoder
{
    /// <summary>
    /// Декодер SPD для DDR5 по стандарту JEDEC JESD400-5C
    /// </summary>
    internal sealed class Ddr5SpdDecoder : BaseSpdDecoder
    {
        // DDR5 SPD Size: 1024 bytes (8 pages × 128 bytes)
        private const int DDR5_SPD_MIN_SIZE = 512;
        
        // DDR5 Module Type codes (Byte 3, bits 3-0)
        private static readonly Dictionary<byte, string> ModuleTypeNames = new()
        {
            { 0x01, "RDIMM" },
            { 0x02, "UDIMM" },
            { 0x03, "SO-DIMM" },
            { 0x04, "LRDIMM" },
            { 0x05, "Mini-RDIMM" },
            { 0x06, "Mini-UDIMM" },
            { 0x08, "72b-SO-RDIMM" },
            { 0x09, "72b-SO-UDIMM" },
            { 0x0A, "SO-UDIMM (16b non-ECC)" },
            { 0x0B, "SO-DIMM (32b ECC)" },
            { 0x0C, "SO-RDIMM (16b non-ECC)" },
            { 0x0D, "SO-RDIMM (32b ECC)" },
            { 0x0E, "SO-UDIMM (32b ECC)" },
            { 0x0F, "SO-RDIMM (64b ECC)" },
        };

        // DDR5 Speed Grades (MT/s)
        private static readonly Dictionary<int, string> SpeedGrades = new()
        {
            { 3200, "DDR5-3200" },
            { 3600, "DDR5-3600" },
            { 4000, "DDR5-4000" },
            { 4400, "DDR5-4400" },
            { 4800, "DDR5-4800" },
            { 5200, "DDR5-5200" },
            { 5600, "DDR5-5600" },
            { 6000, "DDR5-6000" },
            { 6400, "DDR5-6400" },
            { 6800, "DDR5-6800" },
            { 7200, "DDR5-7200" },
        };

        private double? _cachedTckNs;

        public Ddr5SpdDecoder(byte[] data) : base(data)
        {
        }

        public override void Populate(
            List<SpdInfoPanel.InfoItem> moduleInfo,
            List<SpdInfoPanel.InfoItem> dramInfo,
            List<SpdInfoPanel.TimingRow> timingRows)
        {
            if (Data.Length < DDR5_SPD_MIN_SIZE)
            {
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "DDR5",
                    Value = $"SPD dump is too short for DDR5 decoding. Minimum {DDR5_SPD_MIN_SIZE} bytes required."
                });
                return;
            }

            PopulateModuleInfo(moduleInfo);
            PopulateDramInfo(dramInfo);
            PopulateTimings(timingRows);
        }

        #region Module Info

        private void PopulateModuleInfo(List<SpdInfoPanel.InfoItem> moduleInfo)
        {
            try
            {
                // Module Manufacturer: bytes 512-513 (JEDEC Manufacturer ID)
                // JEDEC Standard: Byte 512 = Manufacturer Code (LSB), Byte 513 = Continuation Code (MSB)
                // Format: (continuationCode << 8) | manufacturerCode (same as DDR4)
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.Manufacturer,
                    Value = GetManufacturerName(Data[513], Data[512]),
                    ByteOffset = 512,
                    ByteLength = 2
                });

                // Module Part Number: bytes 521-550 (30 bytes ASCII)
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.PartNumber,
                    Value = GetPartNumber(521, 550),
                    ByteOffset = 521,
                    ByteLength = 30
                });

                // Module Serial Number: bytes 517-520 (4 bytes)
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.SerialNumber,
                    Value = GetSerialNumber(517, 520),
                    ByteOffset = 517,
                    ByteLength = 4
                });

                // Manufacturing Date: bytes 515-516 (BCD Year/Week)
                // Byte 515: Year (BCD, 00-99 = 2000-2099)
                // Byte 516: Week (BCD, 01-52)
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.ManufacturingDate,
                    Value = GetManufacturingDateString(515, 516),
                    ByteOffset = 515,
                    ByteLength = 2
                });

                // Manufacturing Location: byte 514
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.ManufacturingLocation,
                    Value = GetManufacturingLocationCode(),
                    ByteOffset = 514,
                    ByteLength = 1
                });

                // JEDEC DIMM Label
                string jedecLabel = GetJedecLabel();
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.JedecDimmLabel,
                    Value = jedecLabel,
                    IsHighlighted = true,
                    ByteRanges = new List<(long offset, int length)>
                    {
                        (1, 1),    // SPD Revision
                        (3, 1),    // Module Type
                        (4, 2),    // Density and addressing
                        (12, 2),   // Organization
                        (20, 1),   // tCK
                        (235, 1)   // tCK Fine
                    }
                });

                // Module Type: byte 3, bits 3-0
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.Architecture,
                    Value = GetModuleType(),
                    ByteOffset = 3,
                    ByteLength = 1
                });

                // Speed Grade: bytes 20, 235 (tCK MTB + FTB)
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.SpeedGrade,
                    Value = GetSpeedGrade(),
                    ByteRanges = new List<(long offset, int length)>
                    {
                        (20, 1),   // tCK MTB
                        (235, 1)   // tCK FTB
                    }
                });

                // Module Capacity: bytes 4, 12-13, 235 (channel)
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.Capacity,
                    Value = GetCapacity(),
                    ByteRanges = new List<(long offset, int length)>
                    {
                        (4, 2),    // Density and banks
                        (12, 2),   // Width and ranks
                        (235, 1)   // Channel info
                    }
                });

                // Organization: ranks, width, channels
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.Organization,
                    Value = GetOrganization(),
                    ByteRanges = new List<(long offset, int length)>
                    {
                        (12, 2),   // Width and ranks
                        (235, 1)   // Channel info
                    }
                });

                // Thermal Sensor: byte 14, bit 7
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.ThermalSensor,
                    Value = HasThermalSensor() ? "Present" : "Not present",
                    ByteOffset = 14,
                    ByteLength = 1
                });

                // Module Height: byte 229, bits 4-0
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.ModuleHeight,
                    Value = GetModuleHeight(),
                    ByteOffset = 229,
                    ByteLength = 1
                });

                // Module Thickness: byte 230
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.ModuleThickness,
                    Value = GetModuleThickness(),
                    ByteOffset = 230,
                    ByteLength = 1
                });

                // SPD Revision: byte 1
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.SpdRevision,
                    Value = GetSpdRevision(),
                    ByteOffset = 1,
                    ByteLength = 1
                });

                // CRC: bytes 510-511 (0x1FE-0x1FF) для bytes 0-509
                // DDR5 JEDEC JESD400-5C: Single CRC-16 for entire base SPD (0-509)
                var crcInfo = GetDdr5CrcInfo();
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.Crc,
                    Value = crcInfo.Value,
                    ByteRanges = crcInfo.Ranges
                });

                // ========== SPD HUB DEVICE ==========
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "─────── SPD Hub Device ───────",
                    Value = string.Empty,
                    IsHighlighted = false
                });

                // SPD Hub Device Type: bytes 0-1 (device identification)
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "Hub Device Type",
                    Value = GetSpdHubDeviceType(),
                    ByteOffset = 0,
                    ByteLength = 2
                });

                // SPD Hub Manufacturer (often undefined in SPD data)
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "Hub Manufacturer",
                    Value = GetSpdHubManufacturer(),
                    ByteOffset = 0,
                    ByteLength = 1
                });

                // SPD Hub Model (from manufacturer specific data or detection)
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "Hub Model",
                    Value = GetSpdHubModel(),
                    ByteOffset = 0,
                    ByteLength = 1
                });

                // Temperature Sensor status
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "Temperature Sensor",
                    Value = GetTemperatureSensorStatus(),
                    ByteOffset = 14,
                    ByteLength = 1
                });

                // Write Protection status (would need to read from SPD Hub registers)
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "Write Protection",
                    Value = GetWriteProtectionStatus(),
                    ByteOffset = 0,
                    ByteLength = 1
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in PopulateModuleInfo: {ex.Message}");
            }
        }

        #endregion

        #region SPD Hub Device Info

        /// <summary>
        /// Get SPD Hub Device Type (SPD5118, SPD5XXXX, etc.)
        /// DDR5 uses SPD5118 or compatible hub devices
        /// </summary>
        private string GetSpdHubDeviceType()
        {
            if (Data.Length < 2)
                return "—";

            // Byte 0: SPD Bytes Used/Total
            // For DDR5: Should be 0x12 or similar
            byte deviceType = Data[0];
            
            // Byte 2: DRAM Device Type (это правильный байт для определения DDR5!)
            byte memoryType = Data.Length > 2 ? Data[2] : (byte)0;

            // DDR5 modules typically use SPD5118 or compatible
            // The actual device type is often detected from manufacturer specific bytes
            // or from the SPD Hub's own identification registers
            
            if (memoryType == 0x12)  // DDR5 (byte 2, not byte 0!)
            {
                // Try to detect specific SPD5118 variant
                return "SPD5118 (or compatible)";
            }
            else if (deviceType == 0x12)  // Some modules may use byte 0
            {
                return "SPD5118 (or compatible)";
            }

            return $"Unknown (byte 0=0x{deviceType:X2}, byte 2=0x{memoryType:X2})";
        }

        /// <summary>
        /// Get SPD Hub Manufacturer
        /// Often not directly encoded in SPD data, detected from other sources
        /// </summary>
        private string GetSpdHubManufacturer()
        {
            // SPD Hub manufacturer is typically not stored in standard SPD bytes
            // It would need to be read from SPD Hub device registers (I2C address 0x36/0x37)
            // Common manufacturers: Montage, Renesas, IDT
            
            // Check if we can infer from module manufacturer or other clues
            if (Data.Length > 513)
            {
                // Check module manufacturer
                byte moduleMfgLsb = Data[512];
                byte moduleMfgMsb = Data[513];
                
                // Some manufacturers use specific SPD Hub vendors
                // This is heuristic and may not always be accurate
                if (moduleMfgLsb == 0xCE && moduleMfgMsb == 0x80)  // Samsung
                {
                    return "IDT/Renesas (typical for Samsung)";
                }
                else if (moduleMfgLsb == 0x98 && moduleMfgMsb == 0x80)  // Kingston
                {
                    return "Montage (typical for Kingston)";
                }
            }

            return "Undefined (requires Hub register read)";
        }

        /// <summary>
        /// Get SPD Hub Model (SPD5118-Y1B000NCG, M88SPD5118A5-T, etc.)
        /// </summary>
        private string GetSpdHubModel()
        {
            // SPD Hub model is not stored in standard SPD data
            // It requires reading from the SPD Hub's own identification registers
            // Common models:
            // - IDT/Renesas: SPD5118-Y1B000NCG, SPD5118-Y0B000NCG
            // - Montage: M88SPD5118A5-T, M88SPD5118B3
            // - Samsung: S2FPD01

            // We can make educated guesses based on module data
            if (Data.Length > 513)
            {
                byte moduleMfgLsb = Data[512];
                byte moduleMfgMsb = Data[513];
                
                if (moduleMfgLsb == 0xCE && moduleMfgMsb == 0x80)  // Samsung
                {
                    // Samsung often uses IDT/Renesas or their own
                    return "SPD5118 or S2FPD01 (detection required)";
                }
                else if (moduleMfgLsb == 0x98 && moduleMfgMsb == 0x80)  // Kingston
                {
                    return "M88SPD5118A5-T (typical)";
                }
                else if (moduleMfgLsb == 0x2C && moduleMfgMsb == 0x80)  // Micron
                {
                    return "SPD5118 (Renesas)";
                }
            }

            return "SPD5118 variant (requires Hub register read)";
        }

        /// <summary>
        /// Get Temperature Sensor status
        /// </summary>
        private string GetTemperatureSensorStatus()
        {
            // Byte 14, bit 7: Thermal sensor
            bool hasSensor = HasThermalSensor();
            
            if (!hasSensor)
            {
                return "Not Incorporated / N/A";
            }

            // If sensor is present, status would need to be read from SPD Hub registers
            // We can only confirm its presence from SPD data
            return "Incorporated (status requires Hub read)";
        }

        /// <summary>
        /// Get Write Protection status
        /// </summary>
        private string GetWriteProtectionStatus()
        {
            // Write protection status is managed by SPD Hub device
            // It's not stored in SPD data itself, but in SPD Hub control registers
            // Typical states:
            // - All blocks unprotected
            // - Partial protection (blocks 0-3 protected, 4+ writable)
            // - All blocks protected
            
            // We cannot determine this from SPD data alone
            // Would need to query SPD Hub register at I2C address 0x36 or 0x37
            
            return "Unknown (requires Hub register read)";
        }

        #endregion

        #region DRAM Info

        private void PopulateDramInfo(List<SpdInfoPanel.InfoItem> dramInfo)
        {
            try
            {
                // DRAM Manufacturer: bytes 552-553 (JEDEC Manufacturer ID)
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.Manufacturer,
                    // JEDEC Standard: Byte 552 = Manufacturer Code (LSB), Byte 553 = Continuation Code (MSB)
                    // Format: (continuationCode << 8) | manufacturerCode (same as DDR4)
                    Value = GetManufacturerName(Data[553], Data[552]),
                    ByteOffset = 552,
                    ByteLength = 2
                });

                // Package Type: byte 6
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.Package,
                    Value = GetPackageType(),
                    ByteOffset = 6,
                    ByteLength = 1
                });

                // Die Density: bytes 4-5
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.DieDensityCount,
                    Value = GetDieDensity(),
                    ByteOffset = 4,
                    ByteLength = 2
                });

                // Addressing: bytes 4-5 (банки, ряды, столбцы)
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.Addressing,
                    Value = GetAddressing(),
                    ByteOffset = 4,
                    ByteLength = 2
                });

                // Input Clock Frequency
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.InputClockFrequency,
                    Value = GetClockFrequency()
                });

                // Minimum Timing Delays
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.MinimumTimingDelays,
                    Value = GetMinimumTimings()
                });

                // Read Latencies: bytes 22-31 (80 bits для CL22-CL101)
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.ReadLatenciesSupported,
                    Value = GetReadLatencies(),
                    ByteOffset = 22,
                    ByteLength = 10
                });

                // Supply Voltage: byte 11
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.SupplyVoltage,
                    Value = GetSupplyVoltage(),
                    ByteOffset = 11,
                    ByteLength = 1
                });

                // ========== EXTENDED TIMINGS (JESD400-5C) ==========
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "─────── Extended Timings ───────",
                    Value = string.Empty,
                    IsHighlighted = false
                });

                // tRFC1 (Refresh Cycle Time - Normal): bytes 43-44 (MTB only, 16-bit)
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "tRFC1 (Refresh Normal)",
                    Value = GetExtendedTiming16bit(43, 44, "ns"),
                    ByteOffset = 43,
                    ByteLength = 2
                });

                // tRFC2 (Refresh Cycle Time - Fine Granularity): bytes 45-46 (MTB only, 16-bit)
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "tRFC2 (Refresh Fine)",
                    Value = GetExtendedTiming16bit(45, 46, "ns"),
                    ByteOffset = 45,
                    ByteLength = 2
                });

                // tRFCsb (Refresh Cycle Time - Same Bank): bytes 47-48 (MTB only, 16-bit)
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "tRFCsb (Refresh Same Bank)",
                    Value = GetExtendedTiming16bit(47, 48, "ns"),
                    ByteOffset = 47,
                    ByteLength = 2
                });

                // tFAW (Four Activate Window): bytes 36-37 (MTB), byte 242 (FTB)
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "tFAW (Four Activate Window)",
                    Value = GetExtendedTiming16bitWithFtb(36, 37, 242, "ns"),
                    ByteRanges = new List<(long offset, int length)>
                    {
                        (36, 2),   // MTB
                        (242, 1)   // FTB
                    }
                });

                // tRTP (Read to Precharge): byte 38 (MTB), byte 243 (FTB)
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "tRTP (Read to Precharge)",
                    Value = GetTimingNsFormatted(38, 243),
                    ByteRanges = new List<(long offset, int length)>
                    {
                        (38, 1),   // MTB
                        (243, 1)   // FTB
                    }
                });

                // tWR (Write Recovery Time): byte 42 (MTB), byte 244 (FTB)
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "tWR (Write Recovery Time)",
                    Value = GetTimingNsFormatted(42, 244),
                    ByteRanges = new List<(long offset, int length)>
                    {
                        (42, 1),   // MTB
                        (244, 1)   // FTB
                    }
                });

                // CAS Write Latencies: bytes 32-35 (32 bits для CWL22-CWL53)
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "CAS Write Latencies",
                    Value = GetCasWriteLatencies(),
                    ByteOffset = 32,
                    ByteLength = 4
                });

                // Refresh Management: byte 9
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "Refresh Management",
                    Value = GetRefreshManagement(),
                    ByteOffset = 9,
                    ByteLength = 1
                });

                // PMIC Information (if available): bytes 551, 554-560
                if (Data.Length > 560)
                {
                    dramInfo.Add(new SpdInfoPanel.InfoItem
                    {
                        Label = "PMIC Manufacturer",
                        Value = GetPmicManufacturer(),
                        ByteOffset = 554,
                        ByteLength = 2
                    });

                    dramInfo.Add(new SpdInfoPanel.InfoItem
                    {
                        Label = "PMIC Revision",
                        Value = GetPmicRevision(),
                        ByteOffset = 556,
                        ByteLength = 1
                    });
                }

                // TODO: XMP 3.0 / EXPO profiles (bytes 384+)
                dramInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "XMP 3.0 / EXPO",
                    Value = "Not yet implemented"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in PopulateDramInfo: {ex.Message}");
            }
        }

        #endregion

        #region Timings

        private void PopulateTimings(List<SpdInfoPanel.TimingRow> timingRows)
        {
            try
            {
                double tck = GetTckNs();
                if (tck <= 0)
                    return;

                // Main JEDEC timing profile - DDR5 uses 16-bit values in picoseconds
                // JEDEC JESD400-5C: All timing parameters are 16-bit (LSB, MSB) in ps
                double taa = GetTiming16bitNs(30, 31);      // tAA (CAS Latency) - bytes 30-31
                double trcd = GetTiming16bitNs(32, 33);     // tRCD - bytes 32-33
                double trp = GetTiming16bitNs(34, 35);      // tRP - bytes 34-35
                double tras = GetTiming16bitNs(36, 37);     // tRAS - bytes 36-37
                
                // tRC - bytes 38-39 (can also be calculated as tRAS + tRP)
                double trc = GetTiming16bitNs(38, 39);

                // Extended timings (JESD400-5C)
                // tFAW (Four Activate Window): bytes 36-37 (16-bit MTB) + byte 242 (FTB)
                double tfaw = GetExtendedTiming16bitWithFtbNs(36, 37, 242);
                
                // tWR (Write Recovery Time): byte 42 (MTB) + byte 244 (FTB)
                double twr = GetTimingNs(42, 244);

                var row = new SpdInfoPanel.TimingRow
                {
                    Frequency = GetFrequencyLabel(),
                    CAS = FormatTimingCell(taa, tck),
                    RCD = FormatTimingCell(trcd, tck),
                    RP = FormatTimingCell(trp, tck),
                    RAS = FormatTimingCell(tras, tck),
                    RC = FormatTimingCell(trc, tck),
                    FAW = FormatTimingCell(tfaw, tck),
                    RRDS = "—",                              // tRRD_S (Same Bank Group) - not in basic SPD
                    RRDL = "—",                              // tRRD_L (Different Bank Group) - not in basic SPD
                    WR = FormatTimingCell(twr, tck),
                    WTRS = "—"                               // tWTR_S - not in basic SPD
                };

                timingRows.Add(row);
                
                // TODO: Добавить XMP 3.0 / EXPO профили
                // XMP 3.0 profiles start at byte 384+ (if present)
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in PopulateTimings: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods - Module Info

        private string GetModuleType()
        {
            if (Data.Length < 4)
                return "—";

            byte moduleType = Data[3];
            byte baseType = (byte)(moduleType & 0x0F);

            if (ModuleTypeNames.TryGetValue(baseType, out string? typeName))
            {
                return $"DDR5 SDRAM {typeName}";
            }

            return $"DDR5 SDRAM Unknown (0x{baseType:X2})";
        }

        private string GetSpeedGrade()
        {
            double tck = GetTckNs();
            if (tck <= 0)
                return "—";

            int dataRate = RoundDataRate(tck);
            
            if (SpeedGrades.TryGetValue(dataRate, out string? grade))
            {
                return grade;
            }

            return $"DDR5-{dataRate}";
        }

        private string GetJedecLabel()
        {
            try
            {
                long capacityBytes = GetModuleCapacityBytes();
                int dataRate = RoundDataRate(GetTckNs());

                if (capacityBytes <= 0 || dataRate == 0)
                    return "—";

                string capacity = FormatCapacityLabel(capacityBytes);
                string organization = FormatRankDescriptor();
                
                // DDR5 JEDEC Label format: "{Capacity} {Org} PC5-{Number}{SpeedBin}-{Arch}-{Height}-{Attr}"
                // Example: "16GB 1Rx8 PC5-5600B-UA0-1010-XT"
                // PC5 number in simplified format (data rate instead of bandwidth)
                // Simplified: PC5-5600 (data rate)
                // Full format: PC5-44800 (5600 × 8 = bandwidth in MB/s / 1000)
                int pc5Number = dataRate;

                // Build full JEDEC label with suffixes
                var label = new StringBuilder();
                label.Append($"{capacity} {organization} PC5-{pc5Number}");
                
                // Speed Bin suffix (B, C, etc.) - byte 7, bits 7-5
                string speedBin = GetSpeedBinSuffix();
                if (!string.IsNullOrEmpty(speedBin))
                    label.Append(speedBin);
                
                // Architecture code (UA0, UC0, RA1, etc.)
                string archCode = GetArchitectureCode();
                if (!string.IsNullOrEmpty(archCode))
                    label.Append($"-{archCode}");
                
                // Height/Thickness code (1010, 1111, etc.) - bytes 229-230
                string heightCode = GetHeightThicknessCode();
                if (!string.IsNullOrEmpty(heightCode))
                    label.Append($"-{heightCode}");
                
                // Module attributes (XT, NT, etc.)
                string attributes = GetModuleAttributes();
                if (!string.IsNullOrEmpty(attributes))
                    label.Append($"-{attributes}");

                return label.ToString();
            }
            catch
            {
                return "—";
            }
        }

        private string FormatCapacityLabel(long bytes)
        {
            string text = FormatDataSize((ulong)bytes);
            return text == "—" ? string.Empty : text.Replace(" ", string.Empty);
        }

        private string FormatRankDescriptor()
        {
            int ranks = GetRankCount();
            int deviceWidth = GetDeviceWidth();
            
            if (ranks <= 0 || deviceWidth <= 0)
                return string.Empty;

            return $"{ranks}Rx{deviceWidth}";
        }

        private string GetCapacity()
        {
            long bytes = GetModuleCapacityBytes();
            if (bytes <= 0)
                return "—";

            int channels = GetChannelCount();
            string channelText = channels > 1 ? $" ({channels} channels)" : "";

            return $"{FormatDataSize((ulong)bytes)}{channelText}";
        }

        private string GetOrganization()
        {
            int ranks = GetRankCount();
            int deviceWidth = GetDeviceWidth();
            int busWidth = GetBusWidth();  // Already includes 2 sub-channels
            int channels = GetChannelCount();

            if (ranks == 0 || deviceWidth == 0 || busWidth == 0)
                return "—";

            // DDR5: busWidth already accounts for 2 sub-channels (e.g., 64-bit = 32-bit × 2)
            string channelText = channels > 1 ? $", {channels} channels" : "";
            return $"{ranks} rank{(ranks > 1 ? "s" : "")} × {busWidth}-bit{channelText}, x{deviceWidth} devices";
        }

        private bool HasThermalSensor()
        {
            return Data.Length > 14 && (Data[14] & 0x80) != 0;
        }

        private string GetModuleHeight()
        {
            if (Data.Length <= 229)
                return "—";

            int heightCode = Data[229] & 0x1F;  // Bits 4-0
            int heightMm = heightCode + 15;     // Height = code + 15 mm
            
            return $"{heightMm} mm";
        }

        private string GetModuleThickness()
        {
            if (Data.Length <= 230)
                return "—";

            int frontCode = Data[230] & 0x0F;   // Bits 3-0
            int backCode = (Data[230] >> 4) & 0x0F;  // Bits 7-4

            return $"Front {FormatThickness(frontCode)} / Back {FormatThickness(backCode)}";
        }

        private static string FormatThickness(int code)
        {
            // DDR5: каждый код = 0.2 mm, начиная с 1.0 mm
            double thickness = 1.0 + (code * 0.2);
            return $"{thickness:F1} mm";
        }

        private string GetManufacturingLocationCode()
        {
            if (Data.Length <= 514)
                return "—";

            byte code = Data[514];
            if (code == 0)
                return "—";

            return $"0x{code:X2}";
        }

        private string GetSpdRevision()
        {
            if (Data.Length < 2)
                return "—";

            byte encoding = (byte)((Data[1] >> 4) & 0x0F);  // Bits 7-4
            byte additions = (byte)(Data[1] & 0x0F);         // Bits 3-0

            return $"{encoding}.{additions}";
        }

        /// <summary>
        /// Get Speed Bin suffix (B, C, etc.) from byte 7
        /// JEDEC: Byte 7, bits 7-5 encode speed bin
        /// </summary>
        private string GetSpeedBinSuffix()
        {
            if (Data.Length <= 7)
                return string.Empty;

            // Byte 7, bits 7-5: Speed bin
            int binCode = (Data[7] >> 5) & 0x7;
            
            return binCode switch
            {
                0 => "B",  // Base/JEDEC bin
                1 => "C",  // Custom bin
                2 => "A",  // Alternative bin
                _ => string.Empty  // Unknown or not specified
            };
        }

        /// <summary>
        /// Get Architecture code (UA0, UC0, RA1, etc.)
        /// Format: [Module Type][Revision][Channel]
        /// </summary>
        private string GetArchitectureCode()
        {
            if (Data.Length <= 13)
                return string.Empty;

            // Module Type (byte 3, bits 3-0)
            byte moduleType = Data[3];
            byte baseType = (byte)(moduleType & 0x0F);
            
            char typeCode = baseType switch
            {
                0x01 => 'R',  // RDIMM
                0x02 => 'U',  // UDIMM
                0x03 => 'S',  // SO-DIMM
                0x04 => 'L',  // LRDIMM
                0x08 => 'T',  // 72b-SO-RDIMM
                0x09 => 'V',  // 72b-SO-UDIMM
                0x0A => 'C',  // SO-UDIMM (16b non-ECC)
                _ => 'U'      // Default to Unbuffered
            };

            // Design Revision (byte 234, bits 7-4) - если доступно
            char revisionCode = 'A';  // Default
            if (Data.Length > 234)
            {
                int revision = (Data[234] >> 4) & 0x0F;
                if (revision < 26)
                    revisionCode = (char)('A' + revision);
            }

            // Channel indicator (always 0 for single channel modules)
            char channelCode = '0';
            int channels = GetChannelCount();
            if (channels > 1)
                channelCode = (char)('0' + (channels - 1));

            return $"{typeCode}{revisionCode}{channelCode}";
        }

        /// <summary>
        /// Get Height/Thickness code (1010, 1111, etc.)
        /// Format: [Front Thickness][Back Thickness][Height][Reserved]
        /// Each digit is hex (0-F)
        /// </summary>
        private string GetHeightThicknessCode()
        {
            if (Data.Length <= 230)
                return string.Empty;

            // Byte 229: Module Height (bits 4-0)
            int heightCode = Data[229] & 0x1F;
            
            // Byte 230: Module Thickness (front/back)
            int frontCode = Data[230] & 0x0F;    // Bits 3-0
            int backCode = (Data[230] >> 4) & 0x0F;  // Bits 7-4

            // Format: [Front][Back][Height MSB][Height LSB]
            // Example: 1010 = Front=1, Back=0, Height=10 (hex)
            return $"{frontCode:X}{backCode:X}{heightCode >> 4:X}{heightCode & 0xF:X}";
        }

        /// <summary>
        /// Get Module Attributes suffix (XT, NT, etc.)
        /// X = Extended attributes
        /// T = Thermal sensor present
        /// N = Normal (no special attributes)
        /// </summary>
        private string GetModuleAttributes()
        {
            var attrs = new StringBuilder();

            // Check for extended features (byte 7, bit 4)
            bool hasExtended = Data.Length > 7 && (Data[7] & 0x10) != 0;
            if (hasExtended)
                attrs.Append('X');
            else
                attrs.Append('N');

            // Check thermal sensor (byte 14, bit 7)
            bool hasThermal = HasThermalSensor();
            if (hasThermal)
                attrs.Append('T');

            return attrs.Length > 0 ? attrs.ToString() : string.Empty;
        }

        #endregion

        #region Helper Methods - DRAM Info

        private string GetPackageType()
        {
            if (Data.Length < 7)
                return "—";

            // DDR5 Package: byte 6
            // Bits 7-5: Die count (0-7, actual = value + 1)
            // Bits 4-2: Package type
            // Bits 1-0: Signal loading
            
            int dieCount = ((Data[6] >> 5) & 0x7) + 1;
            int packageType = (Data[6] >> 2) & 0x7;
            int signalLoading = Data[6] & 0x3;

            string packageDesc = packageType switch
            {
                0 => "Monolithic",
                1 => "Multi Load Stack (DDP)",
                2 => "Single Load Stack (3DS)",
                _ => $"Unknown (0x{packageType:X})"
            };

            return $"{packageDesc}, {dieCount} die{(dieCount > 1 ? "s" : "")}";
        }

        private string GetDieDensity()
        {
            // DDR5: Byte 4, bits 3-0: Density per die
            // Reference: src_old_code DDR5.cs - DensityPackage property uses lookup table
            if (Data.Length <= 4)
                return "—";

            int densityCode = Data[4] & 0x0F;  // Bits 3-0 (NOT 4-0!)
            
            // DDR5 density lookup table (from reference decoder)
            int[] densityList = { 0, 4, 8, 12, 16, 24, 32, 48, 64 };
            int densityGb = (densityCode < densityList.Length) ? densityList[densityCode] : 0;
            
            int dieCount = GetDieCount();
            string dieCountText = dieCount == 1 ? "1 die" : $"{dieCount} dies";

            System.Diagnostics.Debug.WriteLine($"DDR5 GetDieDensity: byte 4=0x{Data[4]:X2}, densityCode={densityCode}, densityGb={densityGb}, dieCount={dieCount}");

            return $"{densityGb} Gb / {dieCountText}";
        }

        private string GetAddressing()
        {
            if (Data.Length <= 5)
                return "—";

            // DDR5: Byte 4, bits 7-5: Bank groups
            int bankGroupCode = (Data[4] >> 5) & 0x7;
            int bankGroups = bankGroupCode switch
            {
                0 => 2,   // 2 bank groups (BG0-BG1)
                1 => 4,   // 4 bank groups (BG0-BG3)
                2 => 8,   // 8 bank groups (BG0-BG7)
                _ => 0
            };

            // DDR5: All configurations have 4 banks per group (BA0-BA3)
            int banksPerGroup = 4;

            // Byte 5: Addressing
            int rowBits = ((Data[5] >> 4) & 0x7) + 16;  // Bits 6-4: Rows (16-19)
            int colBits = (Data[5] & 0x7) + 10;         // Bits 2-0: Columns (10-12)

            return $"{rowBits} rows × {colBits} cols, {bankGroups} BG × {banksPerGroup} banks";
        }

        private string GetClockFrequency()
        {
            double tck = GetTckNs();
            if (tck <= 0)
                return "—";

            int dataRate = RoundDataRate(tck);
            double freqMHz = dataRate / 2.0;
            
            return $"{freqMHz:F0} MHz ({tck:F3} ns)";
        }

        private string GetMinimumTimings()
        {
            try
            {
                double tck = GetTckNs();
                
                // DDR5 JEDEC JESD400-5C: All timing parameters are 16-bit (LSB, MSB) in ps
                double taa = GetTiming16bitNs(30, 31);   // tAA - bytes 30-31
                double trcd = GetTiming16bitNs(32, 33);  // tRCD - bytes 32-33
                double trp = GetTiming16bitNs(34, 35);   // tRP - bytes 34-35
                double tras = GetTiming16bitNs(36, 37);  // tRAS - bytes 36-37
                double trc = GetTiming16bitNs(38, 39);   // tRC - bytes 38-39

                if (tck <= 0)
                    return "—";

                int cl = (int)Math.Ceiling(taa / tck);
                int rcd = (int)Math.Ceiling(trcd / tck);
                int rp = (int)Math.Ceiling(trp / tck);
                int ras = (int)Math.Ceiling(tras / tck);
                int rc = (int)Math.Ceiling(trc / tck);

                return $"{cl}-{rcd}-{rp}-{ras}-{rc}";
            }
            catch
            {
                return "—";
            }
        }

        private string GetReadLatencies()
        {
            if (Data.Length < 32)
                return "—";

            // DDR5: Bytes 22-31 (10 bytes, 80 bits) для CAS Latencies
            // CL range: 22-101 (80 possible values)
            var latencies = new List<int>();

            for (int byteIndex = 0; byteIndex < 10; byteIndex++)
            {
                if (22 + byteIndex >= Data.Length)
                    break;

                byte mask = Data[22 + byteIndex];
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((mask & (1 << bit)) != 0)
                    {
                        int cl = 22 + (byteIndex * 8) + bit;
                        latencies.Add(cl);
                    }
                }
            }

            if (latencies.Count == 0)
                return "—";

            // Sort descending
            latencies.Sort((a, b) => b.CompareTo(a));

            return string.Join(", ", latencies.Select(l => $"{l}T"));
        }

        private string GetSupplyVoltage()
        {
            if (Data.Length < 12)
                return "—";

            // DDR5: Byte 11
            // Bits 2-0: Vdd/Vddq
            // Bits 5-3: Vpp
            
            int vddCode = Data[11] & 0x7;
            int vppCode = (Data[11] >> 3) & 0x7;

            string vdd = vddCode switch
            {
                0 => "1.1 V (nominal)",
                1 => "1.1 V (operable)",
                2 => "1.1 V (endurant)",
                _ => $"Unknown (0x{vddCode:X})"
            };

            string vpp = vppCode switch
            {
                0 => "1.8 V",
                _ => $"Unknown (0x{vppCode:X})"
            };

            return $"Vdd/Vddq: {vdd}, Vpp: {vpp}";
        }

        /// <summary>
        /// Get Extended Timing (16-bit MTB only) - for tRFC values
        /// JESD400-5C: bytes stored as LSB, MSB
        /// </summary>
        private string GetExtendedTiming16bit(int lsbIndex, int msbIndex, string unit = "ns")
        {
            if (Data.Length <= msbIndex)
                return "—";

            byte lsb = Data[lsbIndex];
            byte msb = Data[msbIndex];
            
            // 16-bit value: (MSB << 8) | LSB
            int mtbValue = (msb << 8) | lsb;
            
            if (mtbValue == 0)
                return "—";

            // Convert to nanoseconds: MTB * 125 ps = MTB * 0.125 ns
            double ns = mtbValue * MediumTimebasePs / 1000.0;
            
            return $"{ns:F2} {unit}";
        }

        /// <summary>
        /// Get Extended Timing (16-bit MTB + 8-bit FTB) as double value in nanoseconds
        /// JESD400-5C: For tFAW and similar
        /// </summary>
        private double GetExtendedTiming16bitWithFtbNs(int lsbIndex, int msbIndex, int ftbIndex)
        {
            if (Data.Length <= msbIndex)
                return 0;

            byte lsb = Data[lsbIndex];
            byte msb = Data[msbIndex];
            
            // 16-bit MTB value
            int mtbValue = (msb << 8) | lsb;
            
            if (mtbValue == 0)
                return 0;

            // MTB part
            double mtbNs = mtbValue * MediumTimebasePs / 1000.0;
            
            // FTB part (signed)
            double ftbNs = 0;
            if (Data.Length > ftbIndex)
            {
                sbyte ftbValue = (sbyte)Data[ftbIndex];
                ftbNs = ftbValue * FineTimebasePs / 1000.0;
            }
            
            return mtbNs + ftbNs;
        }

        /// <summary>
        /// Get Extended Timing (16-bit MTB + 8-bit FTB) as formatted string
        /// JESD400-5C: For tFAW and similar
        /// </summary>
        private string GetExtendedTiming16bitWithFtb(int lsbIndex, int msbIndex, int ftbIndex, string unit = "ns")
        {
            double totalNs = GetExtendedTiming16bitWithFtbNs(lsbIndex, msbIndex, ftbIndex);
            
            if (totalNs <= 0)
                return "—";
            
            return $"{totalNs:F3} {unit}";
        }

        /// <summary>
        /// Get timing formatted with MTB and FTB
        /// </summary>
        private string GetTimingNsFormatted(int mtbIndex, int ftbIndex)
        {
            double ns = GetTimingNs(mtbIndex, ftbIndex);
            if (ns <= 0)
                return "—";
            
            return $"{ns:F3} ns";
        }

        /// <summary>
        /// Get CAS Write Latencies (CWL)
        /// JESD400-5C: Bytes 32-35 (32 bits) for CWL22-CWL53
        /// </summary>
        private string GetCasWriteLatencies()
        {
            if (Data.Length < 36)
                return "—";

            var latencies = new List<int>();

            // Bytes 32-35: 32 bits for CWL 22-53
            for (int byteIndex = 0; byteIndex < 4; byteIndex++)
            {
                if (32 + byteIndex >= Data.Length)
                    break;

                byte mask = Data[32 + byteIndex];
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((mask & (1 << bit)) != 0)
                    {
                        int cwl = 22 + (byteIndex * 8) + bit;
                        if (cwl <= 53)  // CWL range is 22-53 for DDR5
                            latencies.Add(cwl);
                    }
                }
            }

            if (latencies.Count == 0)
                return "—";

            // Sort descending
            latencies.Sort((a, b) => b.CompareTo(a));

            // Show top 8 values to keep it concise
            var topLatencies = latencies.Take(8);
            
            return string.Join(", ", topLatencies.Select(l => $"{l}T"));
        }

        /// <summary>
        /// Get Refresh Management information
        /// JESD400-5C: Byte 9
        /// </summary>
        private string GetRefreshManagement()
        {
            if (Data.Length <= 9)
                return "—";

            byte refreshByte = Data[9];
            
            // Bits 2-0: Refresh Rate
            int refreshRate = refreshByte & 0x7;
            
            // Bit 3: RAAIMT (Refresh All Accumulated Invoke Management Timing)
            bool raaimt = (refreshByte & 0x08) != 0;
            
            // Bits 6-4: Refresh Options
            int refreshOptions = (refreshByte >> 4) & 0x7;

            string rate = refreshRate switch
            {
                0 => "Normal (7.8 µs @ 85°C)",
                1 => "2x (3.9 µs)",
                2 => "4x (1.95 µs)",
                _ => $"Reserved (0x{refreshRate:X})"
            };

            string options = refreshOptions switch
            {
                0 => "Normal",
                1 => "Extended Temperature",
                2 => "Fine Granularity",
                _ => $"Reserved (0x{refreshOptions:X})"
            };

            return $"{rate}, {options}" + (raaimt ? ", RAAIMT" : "");
        }

        /// <summary>
        /// Get PMIC Manufacturer
        /// JESD400-5C: Bytes 554-555 (JEDEC Manufacturer ID)
        /// </summary>
        private string GetPmicManufacturer()
        {
            if (Data.Length <= 555)
                return "—";

            byte pmicMfgLsb = Data[554];
            byte pmicMfgMsb = Data[555];

            // Check if PMIC info is programmed (non-zero)
            if (pmicMfgLsb == 0 && pmicMfgMsb == 0)
                return "Not programmed";

            return GetManufacturerName(pmicMfgLsb, pmicMfgMsb);
        }

        /// <summary>
        /// Get PMIC Revision
        /// JESD400-5C: Byte 556
        /// </summary>
        private string GetPmicRevision()
        {
            if (Data.Length <= 556)
                return "—";

            byte revision = Data[556];
            
            if (revision == 0)
                return "Not programmed";

            return $"0x{revision:X2}";
        }

        #endregion

        #region Helper Methods - Calculations

        private double GetTckNs()
        {
            if (_cachedTckNs.HasValue)
                return _cachedTckNs.Value;

            // DDR5 JEDEC JESD400-5C:
            // Bytes 20-21: tCKAVGmin (Least Significant Byte, Most Significant Byte)
            // 16-bit value in PICOSECONDS (ps), LSB first
            // Formula: tCK_ns = ((Byte21 << 8) | Byte20) / 1000.0
            
            if (Data.Length <= 21)
                return 0;
            
            byte lsb = Data[20];  // Least Significant Byte
            byte msb = Data[21];  // Most Significant Byte
            
            int tck_ps = (msb << 8) | lsb;  // 16-bit value in picoseconds
            double tck = tck_ps / 1000.0;   // Convert ps to ns
            
            System.Diagnostics.Debug.WriteLine($"DDR5 GetTckNs: byte20=0x{lsb:X2}, byte21=0x{msb:X2}, tck={tck_ps} ps = {tck:.6f} ns");
            
            // Sanity check: tCK должен быть в разумном диапазоне (0.2 ns - 1.0 ns для DDR5)
            // DDR5-7200+ → 0.278 ns (278 ps)
            // DDR5-3200 → 0.625 ns (625 ps)
            if (tck < 0.2 || tck > 1.0)
            {
                System.Diagnostics.Debug.WriteLine($"Warning: tCK out of normal range: {tck:F6} ns ({tck_ps} ps)");
                // Don't cache invalid values
                return tck;
            }
            
            _cachedTckNs = tck;
            return _cachedTckNs.Value;
        }

        private int RoundDataRate(double tck)
        {
            if (tck <= 0)
                return 0;

            double dataRate = 2000.0 / tck;  // Convert tCK (ns) to MT/s
            
            // DDR5 обычно кратен 400 MT/s
            return (int)Math.Round(dataRate / 400.0) * 400;
        }

        private string GetFrequencyLabel()
        {
            double tck = GetTckNs();
            int dataRate = RoundDataRate(tck);
            return dataRate == 0 ? "—" : $"{dataRate} MT/s";
        }

        private string FormatTimingCell(double timingNs, double tck)
        {
            if (tck <= 0 || timingNs <= 0)
                return "—";

            double cycles = timingNs / tck;
            return $"{Math.Round(cycles, 1):F1}";
        }

        private int GetRankCount()
        {
            if (Data.Length <= 234)
                return 0;

            // DDR5: Byte 234 (0xEA), bits 5-3: Package ranks per channel
            // Reference: src_old_code DDR5.cs
            int rankCode = (Data[234] >> 3) & 0x7;
            int ranks = rankCode + 1;  // 0-7 → 1-8 ranks
            
            // Sanity check: ranks должны быть 1, 2, 4, или 8
            if (ranks > 8)
                return 0;
                
            System.Diagnostics.Debug.WriteLine($"DDR5 GetRankCount: byte 234=0x{Data[234]:X2}, rankCode={rankCode}, ranks={ranks}");
                
            return ranks;
        }

        private int GetDeviceWidth()
        {
            if (Data.Length <= 6)
                return 0;

            // DDR5: Byte 6 (0x006), bits 7-5: First SDRAM I/O width
            // Reference: src_old_code DDR5.cs - IoWidth property
            int code = (Data[6] >> 5) & 0x7;  // Bits 7-5
            
            int width = code switch
            {
                0 => 4,   // x4
                1 => 8,   // x8
                2 => 16,  // x16
                3 => 32,  // x32
                _ => 0
            };
            
            System.Diagnostics.Debug.WriteLine($"DDR5 GetDeviceWidth: byte 6=0x{Data[6]:X2}, code={code}, width=x{width}");
            
            // Debug: если получили 0, значит неизвестный код
            if (width == 0 && code != 0)
            {
                System.Diagnostics.Debug.WriteLine($"Unknown device width code: 0x{code:X} in byte 6");
            }
            
            return width;
        }

        private int GetBusWidth()
        {
            if (Data.Length <= 235)
                return 0;

            // DDR5 JEDEC JESD400-5C: Byte 235 (0xEB), bits 2-0: Primary bus width PER SUB-CHANNEL
            // IMPORTANT: DDR5 has 2 sub-channels (A and B) per channel!
            // For capacity calculation: Total bus width = sub-channel width × 2
            // Reference: JESD400-5C Page 79: "two 40-bit sub-channels"
            
            int code = (Data[235] >> 0) & 0x7;  // Bits 2-0
            
            int subChannelWidth = (1 << (code + 3)) & 0xF8;  // Width per sub-channel
            
            // DDR5 always has 2 sub-channels (A and B)
            int totalWidth = subChannelWidth * 2;
            
            System.Diagnostics.Debug.WriteLine($"DDR5 GetBusWidth: byte 235=0x{Data[235]:X2}, code={code}, subCh={subChannelWidth}, total={totalWidth}");
            
            return totalWidth;
        }

        private int GetChannelCount()
        {
            if (Data.Length <= 235)
                return 1;

            // DDR5: Byte 235 (0xEB), bits 7-6: Channel count
            // Reference: src_old_code DDR5.cs
            int code = (Data[235] >> 6) & 0x3;  // Bits 7-6
            int channels = (int)Math.Pow(2, code);  // 0→1, 1→2, 2→4, 3→8
            
            System.Diagnostics.Debug.WriteLine($"DDR5 GetChannelCount: byte 235=0x{Data[235]:X2}, code={code}, channels={channels}");
            
            return channels;
        }

        private int GetDieCount()
        {
            if (Data.Length <= 4)
                return 1;

            // DDR5: Byte 4, bits 7-4: Die per package count
            // Reference: src_old_code DDR5.cs - DensityPackage property
            int dieCode = (Data[4] >> 4) & 0x0F;  // Bits 7-4
            int dieCount = (dieCode == 0) ? 1 : dieCode;  // 0=1 die, 1-15=1-15 dies
            
            System.Diagnostics.Debug.WriteLine($"DDR5 GetDieCount: byte 4=0x{Data[4]:X2}, dieCode={dieCode}, dieCount={dieCount}");
            
            return dieCount;
        }

        private long GetModuleCapacityBytes()
        {
            if (Data.Length <= 235)
                return 0;

            // DDR5: Byte 4, bits 3-0: Density per die (Gb)
            // Use lookup table, not formula!
            int densityCode = Data[4] & 0x0F;  // Bits 3-0 (NOT 4-0!)
            int[] densityList = { 0, 4, 8, 12, 16, 24, 32, 48, 64 };
            int densityGb = (densityCode < densityList.Length) ? densityList[densityCode] : 0;

            int deviceWidth = GetDeviceWidth();
            int busWidth = GetBusWidth();
            int ranks = GetRankCount();
            int channels = GetChannelCount();
            int dieCount = GetDieCount();

            // Debug output for diagnosing capacity calculation issues
            if (deviceWidth == 0 || busWidth == 0 || ranks == 0)
            {
                System.Diagnostics.Debug.WriteLine($"DDR5 Capacity Calculation Failed:");
                System.Diagnostics.Debug.WriteLine($"  Byte 4 = 0x{Data[4]:X2}, densityCode = {densityCode}, densityGb = {densityGb}");
                System.Diagnostics.Debug.WriteLine($"  Byte 12 = 0x{Data[12]:X2}, deviceWidth = {deviceWidth}, ranks = {ranks}");
                System.Diagnostics.Debug.WriteLine($"  Byte 13 = 0x{Data[13]:X2}, busWidth = {busWidth}");
                System.Diagnostics.Debug.WriteLine($"  Byte 6 = 0x{Data[6]:X2}, dieCount = {dieCount}");
                System.Diagnostics.Debug.WriteLine($"  Byte 235 = 0x{(Data.Length > 235 ? Data[235] : 0):X2}, channels = {channels}");
                return 0;
            }

            // DDR5 Formula:
            // Capacity = (DensityPerDie × DieCount / 8) × (BusWidth / DeviceWidth) × Ranks × Channels
            // DensityPerDie in Gb, divide by 8 to get GB
            long capacityGB = (long)densityGb * dieCount / 8 * (busWidth / deviceWidth) * ranks * channels;
            
            System.Diagnostics.Debug.WriteLine($"DDR5 Capacity: {capacityGB} GB (density={densityGb}Gb, dies={dieCount}, width={deviceWidth}, bus={busWidth}, ranks={ranks}, channels={channels})");
            
            return capacityGB * 1024L * 1024L * 1024L;  // GB to bytes
        }

        /// <summary>
        /// Get Extended Timing 16-bit value (for calculations)
        /// Returns timing in nanoseconds
        /// </summary>
        private double GetExtendedTiming16bitValue(int lsbIndex, int msbIndex, int ftbIndex)
        {
            if (Data.Length <= msbIndex)
                return 0;

            byte lsb = Data[lsbIndex];
            byte msb = Data[msbIndex];
            
            int mtbValue = (msb << 8) | lsb;
            if (mtbValue == 0)
                return 0;

            double mtbNs = mtbValue * MediumTimebasePs / 1000.0;
            
            double ftbNs = 0;
            if (Data.Length > ftbIndex)
            {
                sbyte ftbValue = (sbyte)Data[ftbIndex];
                ftbNs = ftbValue * FineTimebasePs / 1000.0;
            }
            
            return mtbNs + ftbNs;
        }

        #endregion

        #region Timing Helpers

        /// <summary>
        /// Read 16-bit timing value (LSB, MSB) in picoseconds and convert to nanoseconds
        /// JEDEC JESD400-5C: DDR5 uses 16-bit direct picosecond values
        /// </summary>
        private double GetTiming16bitNs(int lsbIndex, int msbIndex)
        {
            if (Data.Length <= msbIndex)
                return 0;

            byte lsb = Data[lsbIndex];
            byte msb = Data[msbIndex];
            
            int value_ps = (msb << 8) | lsb;  // 16-bit value in picoseconds
            double value_ns = value_ps / 1000.0;  // Convert ps to ns
            
            return value_ns;
        }

        /// <summary>
        /// Legacy 8-bit timing method for compatibility (if needed)
        /// DDR5 primarily uses 16-bit values, but some parameters might still use 8-bit MTB
        /// </summary>
        private new double GetTimingNs(int mtbIndex, int ftbIndex)
        {
            if (Data.Length <= mtbIndex)
                return 0;

            // 8-bit MTB/FTB formula (legacy, rarely used in DDR5)
            byte mtbValue = Data[mtbIndex];
            sbyte ftbValue = Data.Length > ftbIndex ? (sbyte)Data[ftbIndex] : (sbyte)0;

            double mtb = mtbValue * MediumTimebasePs / 1000.0;  // ps → ns
            double ftb = ftbValue * FineTimebasePs / 1000.0;     // ps → ns

            return mtb + ftb;
        }

        #endregion

        #region CRC Verification and Fix

        /// <summary>
        /// Get DDR5 CRC information
        /// JEDEC JESD400-5C: Bytes 510-511 contain CRC-16 for bytes 0-509
        /// </summary>
        public (string Value, bool IsValid, List<(long offset, int length)> Ranges) GetDdr5CrcInfo()
        {
            var ranges = new List<(long offset, int length)>();
            
            // DDR5 has single CRC for bytes 0-509, stored in bytes 510-511
            const int dataStart = 0;
            const int dataLength = 510;  // bytes 0-509
            const int crcOffset = 510;   // CRC stored at 510-511

            if (Data.Length < dataLength)
            {
                int available = Math.Max(0, Data.Length);
                return ($"data incomplete ({available}/{dataLength} bytes)", false, ranges);
            }

            // Calculate CRC-16 using JEDEC algorithm
            ushort calculated = ComputeDdr5Crc(dataStart, dataLength);

            if (Data.Length >= crcOffset + 2)
            {
                // Read stored CRC (LSB first, MSB second)
                ushort storedValue = (ushort)((Data[crcOffset + 1] << 8) | Data[crcOffset]);
                bool match = storedValue == calculated;
                
                ranges.Add((crcOffset, 2));
                
                string status = match ? "OK" : "BAD";
                return ($"calc 0x{calculated:X4}, stored 0x{storedValue:X4} - {status}", match, ranges);
            }

            if (Data.Length == crcOffset + 1)
            {
                ranges.Add((crcOffset, 1));
                return ($"calc 0x{calculated:X4} - BAD (stored incomplete)", false, ranges);
            }

            return ($"calc 0x{calculated:X4} - BAD (stored missing)", false, ranges);
        }

        /// <summary>
        /// Compute CRC-16 for DDR5 SPD
        /// JEDEC JESD400-5C: CRC-16 with polynomial 0x1021
        /// Same algorithm as DDR4 (CRC-16-CCITT)
        /// </summary>
        private ushort ComputeDdr5Crc(int start, int length)
        {
            const ushort polynomial = 0x1021;  // CRC-16-CCITT polynomial
            ushort crc = 0;

            for (int i = start; i < start + length && i < Data.Length; i++)
            {
                crc ^= (ushort)(Data[i] << 8);
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 0x8000) != 0
                        ? (ushort)((crc << 1) ^ polynomial)
                        : (ushort)(crc << 1);
                }
            }

            return crc;
        }

        /// <summary>
        /// Fix CRC for DDR5 SPD data
        /// JEDEC JESD400-5C: Single CRC for bytes 0-509, stored at bytes 510-511
        /// </summary>
        /// <param name="buffer">SPD data buffer (will be modified if CRC is incorrect)</param>
        /// <returns>True if CRC was corrected, false if it was already correct</returns>
        public bool FixCrc(byte[] buffer)
        {
            if (buffer == null || buffer.Length < 512)
            {
                return false;
            }

            const int dataStart = 0;
            const int dataLength = 510;  // bytes 0-509
            const int crcOffset = 510;   // CRC stored at 510-511

            // Calculate correct CRC
            ushort calculated = ComputeCrc16(buffer, dataStart, dataLength);

            // Read stored CRC (LSB first, MSB second)
            ushort stored = (ushort)((buffer[crcOffset + 1] << 8) | buffer[crcOffset]);

            if (stored == calculated)
            {
                // CRC is already correct
                return false;
            }

            // Fix CRC
            buffer[crcOffset] = (byte)(calculated & 0xFF);         // LSB
            buffer[crcOffset + 1] = (byte)((calculated >> 8) & 0xFF); // MSB

            System.Diagnostics.Debug.WriteLine($"DDR5 CRC fixed: was 0x{stored:X4}, now 0x{calculated:X4}");
            
            return true;
        }

        /// <summary>
        /// Compute CRC-16 for arbitrary buffer (static version for FixCrc)
        /// </summary>
        private static ushort ComputeCrc16(byte[] buffer, int start, int length)
        {
            const ushort polynomial = 0x1021;
            ushort crc = 0;

            for (int i = start; i < start + length && i < buffer.Length; i++)
            {
                crc ^= (ushort)(buffer[i] << 8);
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 0x8000) != 0
                        ? (ushort)((crc << 1) ^ polynomial)
                        : (ushort)(crc << 1);
                }
            }

            return crc;
        }

        #endregion

        /// <summary>
        /// Populate empty structure for initial display
        /// </summary>
        public static void PopulateEmpty(
            ICollection<SpdInfoPanel.InfoItem> moduleInfo,
            ICollection<SpdInfoPanel.InfoItem> dramInfo,
            ICollection<SpdInfoPanel.TimingRow> timingRows)
        {
            // MODULE INFO
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Manufacturer, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.PartNumber, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.SerialNumber, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.JedecDimmLabel, Value = "—", IsHighlighted = true });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Architecture, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.SpeedGrade, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Capacity, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Organization, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ThermalSensor, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ModuleHeight, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ModuleThickness, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ManufacturingDate, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ManufacturingLocation, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.SpdRevision, Value = "—" });

            // DRAM INFO
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Manufacturer, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Package, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.DieDensityCount, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Addressing, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.InputClockFrequency, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.MinimumTimingDelays, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ReadLatenciesSupported, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.SupplyVoltage, Value = "—" });

            // TIMING TABLE
            timingRows.Add(new SpdInfoPanel.TimingRow
            {
                Frequency = "—",
                CAS = "—",
                RCD = "—",
                RP = "—",
                RAS = "—",
                RC = "—",
                FAW = "—",
                RRDS = "—",
                RRDL = "—",
                WR = "—",
                WTRS = "—"
            });
        }
    }
}
