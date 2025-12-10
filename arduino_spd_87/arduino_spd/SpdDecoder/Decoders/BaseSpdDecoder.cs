using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HexEditor.Database;

namespace HexEditor.SpdDecoder
{
    internal abstract class BaseSpdDecoder : ISpdDecoder
    {
        // JEP106 Manufacturer ID Codes (based on JEP106BM/JEP106BG standard)
        // Format: (ContinuationCode << 8) | ManufacturerCode
        // Note: In SPD, format is (continuationCode << 8) | manufacturerCode
        // Parity bits are masked out in GetManufacturerName, so keys are without parity
        // Continuation code 0x7F means next byte contains continuation code
        // All manufacturer data is now loaded from manufacturers.json database

        protected readonly byte[] Data;
        private readonly double _mediumTimebasePs;
        private readonly double _fineTimebasePs;

        protected double MediumTimebasePs => _mediumTimebasePs;
        protected double FineTimebasePs => _fineTimebasePs;

        protected BaseSpdDecoder(byte[] data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            (_mediumTimebasePs, _fineTimebasePs) = DecodeTimebases();
        }

        public abstract void Populate(
            System.Collections.Generic.List<SpdInfoPanel.InfoItem> moduleInfo,
            System.Collections.Generic.List<SpdInfoPanel.InfoItem> dramInfo,
            System.Collections.Generic.List<SpdInfoPanel.TimingRow> timingRows);

        protected string GetManufacturerName(byte continuationCodeLsb, byte manufacturerCodeMsb)
        {
            // Format for DDR4: Byte 320 = ContinuationCode (LSB), Byte 321 = ManufacturerCode (MSB)
            // Format: (continuationCode << 8) | manufacturerCode
            // Pass bytes as-is (with parity bits) to match JEDEC standard format in manufacturers.json
            return ManufacturerDatabase.GetManufacturerName(continuationCodeLsb, manufacturerCodeMsb);
        }

        protected string GetPartNumber(int start, int end)
        {
            if (start >= Data.Length || end >= Data.Length)
                return "—";

            var sb = new StringBuilder();
            for (int i = start; i <= end && i < Data.Length; i++)
            {
                byte b = Data[i];
                if (b == 0)
                    break;
                if (b >= 32 && b <= 126)
                    sb.Append((char)b);
            }
            return sb.ToString().Trim();
        }

        protected string GetSerialNumber(int start, int end)
        {
            if (start >= Data.Length || end >= Data.Length)
                return "—";

            // Check if serial number is ASCII string or hex
            bool isAscii = true;
            for (int i = start; i <= end && i < Data.Length; i++)
            {
                byte b = Data[i];
                // Check if byte is printable ASCII (0x20-0x7E) or null terminator
                if (b != 0 && (b < 0x20 || b > 0x7E))
                {
                    isAscii = false;
                    break;
                }
            }

            if (isAscii)
            {
                // Try to read as ASCII string
                var asciiBuilder = new StringBuilder();
                for (int i = start; i <= end && i < Data.Length; i++)
                {
                    byte b = Data[i];
                    if (b == 0)
                        break;
                    if (b >= 0x20 && b <= 0x7E)
                        asciiBuilder.Append((char)b);
                }

                string asciiResult = asciiBuilder.ToString().Trim();
                if (!string.IsNullOrEmpty(asciiResult))
                {
                    return asciiResult;
                }
            }

            // Return as hex string (without suffix); even all-zero serials are considered valid
            var hexBuilder = new StringBuilder();
            for (int i = start; i <= end && i < Data.Length; i++)
            {
                hexBuilder.Append($"{Data[i]:X2}");
            }
            return hexBuilder.ToString();
        }

        protected string? FindSpecificPartNumber()
        {
            // According to JEDEC DDR4 SPD spec:
            // Byte 352 (0x160): DRAM Stepping
            // Bytes 353-383 (0x161-0x17F): Module Manufacturer Specific Data
            // This is where vendors store additional part numbers like "W05R12035"
            
            if (Data.Length < 384)
                return null;

            // Read Module Manufacturer Specific Data (bytes 353-383, skipping DRAM Stepping at 352)
            var sb = new StringBuilder();
            bool foundStart = false;
            for (int i = 353; i < 384 && i < Data.Length; i++)
            {
                byte b = Data[i];
                
                // Stop on null terminator
                if (b == 0)
                {
                    if (foundStart)
                        break; // End of string
                    continue; // Skip leading nulls
                }
                
                if (b >= 32 && b <= 126)
                {
                    foundStart = true;
                    sb.Append((char)b); // !не трогать! выводим байты как есть по требованию пользователя
                }
                // Skip non-printable characters but continue reading
                // (some vendors may have padding bytes between characters)
            }

            string result = sb.ToString().Trim();
            
            // Return if we found a valid vendor-specific part number (8-20 chars)
            // Allow alphanumeric and common special characters like #, _, etc.
            if (result.Length >= 8 && result.Length <= 20)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(result, @"^[A-Z0-9\-_#]+$", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    // Exclude module part numbers and register models
                    if (!result.StartsWith("M3", StringComparison.OrdinalIgnoreCase) &&
                        !result.StartsWith("K4", StringComparison.OrdinalIgnoreCase) &&
                        !result.StartsWith("H5", StringComparison.OrdinalIgnoreCase) &&
                        !result.StartsWith("MT", StringComparison.OrdinalIgnoreCase) &&
                        !result.StartsWith("M8", StringComparison.OrdinalIgnoreCase) &&
                        !result.Contains("RCD", StringComparison.OrdinalIgnoreCase) &&
                        !result.Contains("4RCD", StringComparison.OrdinalIgnoreCase) &&
                        !result.Contains("4DB", StringComparison.OrdinalIgnoreCase))
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        protected (double mediumTimebasePs, double fineTimebasePs) DecodeTimebases()
        {
            if (Data.Length < 16)
                return (125.0, 1.0); // Default values

            // Byte 15 (0x0F): Timebases
            // Bits 3-2: Medium Timebase (MTB) - 0 = 125 ps, other = 0
            // Bits 1-0: Fine Timebase (FTB) - 0 = 1 ps, other = 0
            // According to JEDEC DDR4 SPD spec and src/SpdReaderWriterCore/SPD/DDR4.cs
            byte timebaseByte = Data[15];
            int mtbBits = (timebaseByte >> 2) & 0x03; // Bits 3-2
            int ftbBits = timebaseByte & 0x03; // Bits 1-0

            double mediumTimebasePs = mtbBits == 0 ? 125.0 : 0.0;
            double fineTimebasePs = ftbBits == 0 ? 1.0 : 0.0;

            return (mediumTimebasePs, fineTimebasePs);
        }

        protected int ReadTimebaseValue(int byteIndex)
        {
            if (byteIndex >= Data.Length)
                return 0;

            byte value = Data[byteIndex];
            int signedValue = (value & 0x80) != 0 ? value - 256 : value;
            return signedValue;
        }

        protected double GetTimebaseValue(int byteIndex)
        {
            // Medium timebase values are unsigned bytes (0-255)
            // According to JEDEC DDR4 SPD spec and src/SpdReaderWriterCore/SPD/DDR4.cs
            if (byteIndex >= Data.Length)
                return 0;
            
            byte value = Data[byteIndex];
            return value * MediumTimebasePs;
        }

        protected double GetFineTimebaseValue(int byteIndex)
        {
            if (byteIndex >= Data.Length)
                return 0;

            byte value = Data[byteIndex];
            int signedValue = (value & 0x80) != 0 ? value - 256 : value;
            return signedValue * FineTimebasePs;
        }

        protected double GetCombinedTimebaseValue(int mtbIndex, int ftbIndex)
        {
            // Medium and Fine timebase values are in picoseconds
            // Formula: (Medium * MTB + Fine * FTB) / 1000 to get nanoseconds
            // According to JEDEC DDR4 SPD spec and src/SpdReaderWriterCore/SPD/DDR4.cs
            double mtb = GetTimebaseValue(mtbIndex); // in picoseconds
            double ftb = ftbIndex < Data.Length ? GetFineTimebaseValue(ftbIndex) : 0; // in picoseconds
            return (mtb + ftb) / 1000.0; // Convert to nanoseconds
        }

        protected string FormatDataSize(ulong bytes)
        {
            const ulong KB = 1024;
            const ulong MB = KB * 1024;
            const ulong GB = MB * 1024;
            const ulong TB = GB * 1024UL;

            if (bytes >= TB)
            {
                double tb = (double)bytes / TB;
                if (tb >= 100)
                    return $"{tb:F0} TB";
                return $"{tb:F1} TB";
            }
            if (bytes >= GB)
            {
                return $"{bytes / GB} GB";
            }
            if (bytes >= MB)
            {
                return $"{bytes / MB} MB";
            }
            if (bytes >= KB)
            {
                return $"{bytes / KB} KB";
            }
            return $"{bytes} B";
        }

        protected (int year, int week) GetManufacturingDate(int yearByteIndex, int weekByteIndex)
        {
            if (yearByteIndex >= Data.Length || weekByteIndex >= Data.Length)
                return (0, 0);

            byte yearByte = Data[yearByteIndex];
            byte weekByte = Data[weekByteIndex];

            // Check for invalid/unset values
            if (yearByte == 0x00 || yearByte == 0xFF || weekByte == 0x00 || weekByte == 0xFF)
                return (0, 0);

            // Decode BCD (Binary-Coded Decimal)
            int year = ((yearByte >> 4) & 0x0F) * 10 + (yearByte & 0x0F);
            int week = ((weekByte >> 4) & 0x0F) * 10 + (weekByte & 0x0F);

            // Validate ranges
            if (year < 0 || year > 99 || week < 1 || week > 52)
                return (0, 0);

            return (year, week);
        }

        protected string GetManufacturingDateString(int yearByteIndex, int weekByteIndex)
        {
            var (year, week) = GetManufacturingDate(yearByteIndex, weekByteIndex);
            if (year == 0 || week == 0)
                return "—";

            int fullYear = 2000 + year;

            // Calculate date range for the week
            // JEDEC weeks are typically Monday-Friday (5 working days)
            // Week 1 starts on January 1st (or the Monday of the week containing January 1st)
            DateTime yearStart = new DateTime(fullYear, 1, 1);
            
            // Find the Monday of the first week (ISO 8601 week starts on Monday)
            int firstDayOfWeek = (int)yearStart.DayOfWeek;
            if (firstDayOfWeek == 0) firstDayOfWeek = 7; // Sunday = 7
            DateTime firstMonday = yearStart.AddDays(1 - firstDayOfWeek);
            
            // Calculate the Monday of the target week
            int daysOffset = (week - 1) * 7;
            DateTime weekStart = firstMonday.AddDays(daysOffset);
            
            // Week range is Monday to Friday (5 days)
            DateTime weekEnd = weekStart.AddDays(4); // Friday of the week

            string monthStart = weekStart.ToString("MMMM", CultureInfo.InvariantCulture);
            string monthEnd = weekEnd.ToString("MMMM", CultureInfo.InvariantCulture);
            int dayStart = weekStart.Day;
            int dayEnd = weekEnd.Day;

            if (monthStart == monthEnd)
            {
                return $"{monthStart} {dayStart}-{dayEnd} / Week {week}, {fullYear}";
            }
            else
            {
                return $"{monthStart} {dayStart} - {monthEnd} {dayEnd} / Week {week}, {fullYear}";
            }
        }

        protected double GetTimingNs(int mtbIndex, int ftbIndex)
        {
            return GetCombinedTimebaseValue(mtbIndex, ftbIndex);
        }

        protected double GetTimingNsFromComposite(
            int lsbIndex,
            int msbIndex,
            int msbBitStart,
            int msbBitLength,
            int lsbBitLength = 8,
            int? ftbIndex = null)
        {
            if (lsbIndex >= Data.Length || msbIndex >= Data.Length)
                return 0;

            byte lsb = Data[lsbIndex];
            byte msb = Data[msbIndex];

            // Формула из JEDEC: value = (MSB_fragment << LSB_width) | LSB_fragment,
            // где LSB_width задаётся количеством младших бит (обычно 8).
            int lsbMask = (1 << lsbBitLength) - 1;
            int msbShift = msbBitStart - msbBitLength + 1;
            if (msbShift < 0)
                return 0;

            int msbMask = (1 << msbBitLength) - 1;
            int lsbValue = lsb & lsbMask;
            int msbValue = (msb >> msbShift) & msbMask;
            int combinedValue = (msbValue << lsbBitLength) | lsbValue;

            double mtbNs = combinedValue * MediumTimebasePs / 1000.0;
            double ftbNs = ftbIndex.HasValue && ftbIndex.Value < Data.Length
                ? GetFineTimebaseValue(ftbIndex.Value) / 1000.0
                : 0.0;

            return mtbNs + ftbNs;
        }
    }
}
