using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HexEditor.Database;

namespace HexEditor.SpdDecoder
{
    internal sealed class Ddr4SpdDecoder : BaseSpdDecoder
    {
        private static readonly Dictionary<string, int> RawCardLayers = new()
        {
            { "A1", 8 },
            { "B1", 12 },
            { "B2", 12 },
            { "B3", 12 },
            { "B4", 12 },
        };

        private static readonly Dictionary<byte, string> LocationNames = new()
        {
            { 0x01, "Ichon, Korea" },
            { 0x02, "Cheongju, Korea" },
            { 0x03, "Pampanga, Philippines (PSPC)" },
            { 0x04, "Onyang, Korea" },
            { 0x05, "Keelung, Taiwan" },
            { 0x0F, "Xi'an, China (MXA)" },
            { 0x10, "Suzhou, China (SESS)" },
        };

        private static readonly IReadOnlyDictionary<(byte Type, byte Revision), RegisterModelEntry> RegisterModels =
            RegisterModelDatabase.LoadEntries();

        // Known Package Types: (DieCount, SignalLoading, IsMonolithic) -> Package Description
        // SignalLoading: 0 = Not Specified/Monolithic, 1 = Multi Load Stack, 2 = Single Load Stack (3DS)
        // According to JEDEC DDR4 SPD spec, all standard packages use 78-ball FBGA
        // Reference: JEDEC DDR4 SPD Specification and decode-dimms (i2c-tools)
        // Byte 6: Bit 7 = Monolithic (0) / Stack (1), Bits 6-4 = DieCount (0-7, +1 = 1-8), Bits 1-0 = SignalLoading
        private static readonly Dictionary<(int DieCount, int SignalLoading, bool IsMonolithic), string> KnownPackageTypes = new()
        {
            // Monolithic packages (most common - 78-ball FBGA)
            // Bit 7 = 0, DieCount = 1, SignalLoading = 0
            { (1, 0, true), "Standard Monolithic 78-ball FBGA" },
            
            // Single Load Stack (3DS) packages - all use 78-ball FBGA
            // Bit 7 = 1, SignalLoading = 2 (Single Load Stack)
            // DieCount can be 1-8 for 3DS packages (1 is rare but valid)
            { (1, 2, false), "1-High 3DS 78-ball FBGA" },
            { (2, 2, false), "2-High 3DS 78-ball FBGA" },
            { (3, 2, false), "3-High 3DS 78-ball FBGA" },
            { (4, 2, false), "4-High 3DS 78-ball FBGA" },
            { (5, 2, false), "5-High 3DS 78-ball FBGA" },
            { (6, 2, false), "6-High 3DS 78-ball FBGA" },
            { (7, 2, false), "7-High 3DS 78-ball FBGA" },
            { (8, 2, false), "8-High 3DS 78-ball FBGA" },
            
            // Multi Load Stack packages (typically non-standard, 78-ball FBGA)
            // Bit 7 = 1, SignalLoading = 1 (Multi Load Stack)
            // These are typically non-standard configurations
            // DieCount can be 1-8 (1 is rare but valid)
            { (1, 1, false), "Non-Standard Multi Load Stack 78-ball FBGA" },
            { (2, 1, false), "Non-Standard Multi Load Stack 78-ball FBGA" },
            { (3, 1, false), "Non-Standard Multi Load Stack 78-ball FBGA" },
            { (4, 1, false), "Non-Standard Multi Load Stack 78-ball FBGA" },
            { (5, 1, false), "Non-Standard Multi Load Stack 78-ball FBGA" },
            { (6, 1, false), "Non-Standard Multi Load Stack 78-ball FBGA" },
            { (7, 1, false), "Non-Standard Multi Load Stack 78-ball FBGA" },
            { (8, 1, false), "Non-Standard Multi Load Stack 78-ball FBGA" },
            
            // Edge cases: Stack packages with SignalLoading = 0 (Not Specified)
            // These are rare but may occur in some SPD dumps
            { (1, 0, false), "Non-Standard Stack 78-ball FBGA" },
            { (2, 0, false), "Non-Standard Stack 78-ball FBGA" },
            { (3, 0, false), "Non-Standard Stack 78-ball FBGA" },
            { (4, 0, false), "Non-Standard Stack 78-ball FBGA" },
            { (5, 0, false), "Non-Standard Stack 78-ball FBGA" },
            { (6, 0, false), "Non-Standard Stack 78-ball FBGA" },
            { (7, 0, false), "Non-Standard Stack 78-ball FBGA" },
            { (8, 0, false), "Non-Standard Stack 78-ball FBGA" },
        };


        private static readonly Regex DramPartPattern = new(@"^[A-Z0-9]{4,}[-A-Z0-9()/:]*$", RegexOptions.Compiled);

        private static readonly string[] DramPrefixes = { "K4", "H5", "MT", "D9", "ED", "MTA", "M8" };

        // Die type patterns from part numbers (Samsung, Hynix, Micron)
        private static readonly Dictionary<string, (string DieType, string? Process)> DieTypePatterns = new()
        {
            // Samsung patterns: K4AAG045WC-BCWE -> C-die
            { "K4AAG045WC", ("C-die", null) },
            { "K4AAG045WB", ("B-die", "Armstrong / 17 nm") },
            { "K4AAG045WM", ("M-die", "Pascal / 18 nm") },
            { "K4A8G085WE", ("E-die", "Kevlar / 16 nm") },
            { "K4A8G085WB", ("B-die", null) },
            { "K4ABG045WB", ("B-die", "Armstrong / 17 nm") },
            
            // Hynix patterns: H5ANAG4NCJR-XNC -> C-die
            { "H5ANAG4NC", ("C-die", "Rigel / 16 nm") },
            { "H5AN8G8ND", ("D-die", "Davinci / 17 nm") },
            
            // Micron patterns: MT40A8G4CLU -> E-die
            { "MT40A8G4CLU", ("E-die", "Z11B / 19 nm") },
        };

        private static readonly Dictionary<int, string> SpeedBinCodes = new()
        {
            { 1600, "P" },
            { 1866, "R" },
            { 2133, "S" },
            { 2400, "T" },
            { 2666, "V" },
            { 2933, "Y" },
            { 3200, "AA" },
        };

        private static readonly string[] RawCardNameTable =
        {
            "A", "B", "C", "D", "E", "F", "G", "H", "J", "K", "L", "M", "N", "P", "R", "T",
            "U", "V", "W", "Y", "AA", "AB", "AC", "AD", "AE", "AF", "AG", "AH", "AJ", "AK", "AL",
            "AM", "AN", "AP", "AR", "AT", "AU", "AV", "AW", "AY", "BA", "BB", "BC", "BD", "BE",
            "BF", "BG", "BH", "BJ", "BK", "BL", "BM", "BN", "BP", "BR", "BT", "BU", "BV", "BW",
            "BY", "CA", "CB", "ZZ"
        };

        private double? _cachedTckNs;

        public Ddr4SpdDecoder(byte[] data) : base(data)
        {
        }

        public override void Populate(
            List<SpdInfoPanel.InfoItem> moduleInfo,
            List<SpdInfoPanel.InfoItem> dramInfo,
            List<SpdInfoPanel.TimingRow> timingRows)
        {
            if (Data.Length < 352)
            {
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "DDR4",
                    Value = "SPD dump is too short for DDR4 decoding."
                });
                return;
            }

            PopulateModuleInfo(moduleInfo);
            PopulateDramInfo(dramInfo);
            PopulateTimings(timingRows);
        }

        public bool FixCrc(byte[] buffer)
        {
            if (buffer == null || buffer.Length < 256)
            {
                return false;
            }

            bool changed = false;
            changed |= FixCrcBlock(buffer, dataStart: 0, storedOffset: 126);
            changed |= FixCrcBlock(buffer, dataStart: 128, storedOffset: 254);
            return changed;
        }

        private bool FixCrcBlock(byte[] buffer, int dataStart, int storedOffset)
        {
            if (buffer.Length < storedOffset + 2)
            {
                return false;
            }

            ushort calculated = ComputeDdr4Crc(dataStart, 126);
            ushort stored = (ushort)((buffer[storedOffset + 1] << 8) | buffer[storedOffset]);

            if (stored == calculated)
            {
                return false;
            }

            buffer[storedOffset] = (byte)(calculated & 0xFF);         // LSB
            buffer[storedOffset + 1] = (byte)((calculated >> 8) & 0xFF); // MSB
            return true;
        }

        /// <summary>
        /// Populates lists with empty structure so all fields are visible at startup
        /// </summary>
        public static void PopulateEmpty(
            ICollection<SpdInfoPanel.InfoItem> moduleInfo,
            ICollection<SpdInfoPanel.InfoItem> dramInfo,
            ICollection<SpdInfoPanel.TimingRow> timingRows)
        {
            // MEMORY MODULE fields
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Manufacturer, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.PartNumber, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.SerialNumber, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.SpecificPart, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.JedecDimmLabel, Value = "—", IsHighlighted = true });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Architecture, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.SpeedGrade, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Capacity, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Organization, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ThermalSensor, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ModuleHeight, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ModuleThickness, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.RegisterBufferManufacturer, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.RegisterModel, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.RevisionRawCard, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.AddressMapping, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ManufacturingDate, Value = "—" });
            moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ManufacturingLocation, Value = "—" });

            // DRAM COMPONENTS fields
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Manufacturer, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.DramPartNumber, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Package, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.DieDensityCount, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Composition, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.InputClockFrequency, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Addressing, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.MinimumTimingDelays, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ReadLatenciesSupported, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.SupplyVoltage, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.SpdRevision, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.XmpCertified, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.XmpExtreme, Value = "—" });
            dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.XmpRevision, Value = "—" });

            // TIMING TABLE - add one empty row
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

        private void PopulateModuleInfo(List<SpdInfoPanel.InfoItem> moduleInfo)
        {
            try
            {
                // Производитель модуля: байты 320-321 (JEDEC Manufacturer ID)
                // Байт 320: Continuation Code LSB (биты 6-0, бит 7 - parity)
                // Байт 321: Manufacturer Code MSB (биты 6-0, бит 7 - parity)
                moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Manufacturer, Value = GetManufacturerName(Data[320], Data[321]), ByteOffset = 320, ByteLength = 2 });
                
                // Part Number модуля: байты 329-348 (20 байт, ASCII строка)
                moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.PartNumber, Value = GetPartNumber(329, 348), ByteOffset = 329, ByteLength = 20 });
                
                // Серийный номер модуля: байты 325-328 (4 байта, hex или ASCII)
                moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.SerialNumber, Value = GetSerialNumber(325, 328), ByteOffset = 325, ByteLength = 4 });
                
                // Specific Part Number: байты 353-383 (31 байт, Module Manufacturer Specific Data)
                // Примечание: Байт 352 (0x160) - это DRAM Stepping, не входит в manufacturer specific data
                string? specificPart = FindSpecificPartNumber();
                moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.SpecificPart, Value = string.IsNullOrWhiteSpace(specificPart) ? "—" : specificPart, ByteOffset = 353, ByteLength = 31 });
                
                string jedecLabel = GetJedecLabel();
                // JEDEC DIMM Label - составное поле, формируется из нескольких байтов
                // Используется в: FormatCapacityLabel (через GetModuleCapacityBytes), FormatRankDescriptor, BuildSpeedCode, BuildModuleSection, GetSpdRevisionSuffix
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.JedecDimmLabel,
                    Value = jedecLabel,
                    IsHighlighted = true,
                    ByteRanges = new List<(long offset, int length)>
                    {
                        (1, 1),    // Байт 1: SPD revision (major/minor) для суффикса - используется в GetSpdRevisionSuffix
                        (3, 4),    // Байты 3-6: Module type (байт 3), SDRAM Density/Banks (байты 4-5) и Die count/Stack info (байт 6)
                        (12, 2),   // Байты 12-13: Bus width & ranks (байт 12) + Bus width extension / ECC (байт 13)
                        (18, 1),   // Байт 18: tCK Medium Timebase (MTB) - используется в BuildSpeedCode
                        (125, 1),  // Байт 125: tCK Fine Timebase (FTB, signed) - используется в BuildSpeedCode
                        (128, 1),  // Байт 128: Raw card ordinal extension (биты 7-5) и height (биты 4-0) - используется в BuildModuleSection через GetRawCardOrdinal
                        (130, 1)   // Байт 130: Raw card name (биты 4-0) и revision (биты 6-5) - используется в BuildModuleSection
                    }
                });
                
                // Архитектура модуля: байт 3 (Module Type, биты 3-0)
                // 0x01=RDIMM, 0x02=UDIMM, 0x03=SO-DIMM, 0x04=LRDIMM, и т.д.
                moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Architecture, Value = GetModuleType(), ByteOffset = 3, ByteLength = 1 });
                string speedGrade = GetSpeedGrade();
                // Speed Grade: байты 18 и 125 (tCK = MTB + FTB)
                // Байт 18: tCK Medium Timebase (MTB, unsigned)
                // Байт 125: tCK Fine Timebase (FTB, signed)
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.SpeedGrade,
                    Value = speedGrade,
                    ByteRanges = new List<(long offset, int length)>
                    {
                        (18, 1),   // Байт 18: tCK MTB
                        (125, 1)   // Байт 125: tCK FTB
                    }
                });
                
                // Емкость модуля: байты 4-5 и 12-13
                // Байты 4-5: SDRAM Density (биты 3-0 байта 4) + Banks (биты 5-4 байта 4) + Die count/Stack info (байт 5)
                // Байты 12-13: Module Memory Bus Width & ranks (байт 12) + extension/ECC (байт 13)
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.Capacity,
                    Value = GetCapacity(),
                    ByteRanges = new List<(long offset, int length)>
                    {
                        (4, 2),   // Байты 4-5: SDRAM Density/Banks + Die count/stack info
                        (12, 2)   // Байты 12-13: Module Memory Bus Width & ranks + extension/ECC
                    }
                });
                
                // Организация модуля: байты 12-13, 6, 4
                // Байты 12-13: Ranks (биты 5-3 байта 12), bus width (биты 2-0 байта 12), ECC (бит 3 байта 13)
                // Байт 6: Die count (биты 6-4) и stack info (бит 7, биты 1-0)
                // Байт 4: Density (биты 3-0) и banks (биты 5-4) для RX дескриптора
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.Organization,
                    Value = GetOrganization(),
                    ByteRanges = new List<(long offset, int length)>
                    {
                        (12, 2),  // Байты 12-13: Ranks, bus width, ECC
                        (6, 1),   // Байт 6: Die count / stack info
                        (4, 1)    // Байт 4: Density / banks (для RX дескриптора)
                    }
                });
                
                // Thermal Sensor: байт 14, бит 7 (0=отсутствует, 1=присутствует)
                moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ThermalSensor, Value = HasThermalSensor() ? "Present" : "Not present", ByteOffset = 14, ByteLength = 1 });
                
                // Высота модуля: байт 128, биты 4-0 (height index, 0-31, высота = index + 15 мм)
                moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ModuleHeight, Value = GetModuleHeightInfo(), ByteOffset = 128, ByteLength = 1 });
                
                // Толщина модуля: байт 129
                // Бит 7: не используется
                // Биты 6-4: Raw card ordinal extension (если используется)
                // Биты 3-0: Front thickness code
                // Байт 129 также содержит back thickness code в старших битах (но это не стандарт JEDEC)
                moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ModuleThickness, Value = GetModuleThicknessInfo(), ByteOffset = 129, ByteLength = 1 });

                // Register Manufacturer / Model
                var registerInfo = GetRegisterInfo();
                bool isUdim = IsUdim();
                bool isLrdimm = IsLrdimm();
                string registerLabel = isLrdimm ? SpdInfoPanel.FieldLabels.RegisterBufferManufacturer : SpdInfoPanel.FieldLabels.RegisterManufacturer;
                string registerManufacturer = "—";
                if (!string.IsNullOrEmpty(registerInfo.Manufacturer))
                {
                    registerManufacturer = registerInfo.Manufacturer!;
                }
                else if (Data.Length > 132)
                {
                    registerManufacturer = GetManufacturerName(Data[131], Data[132]);
                }

                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = registerLabel,
                    Value = registerManufacturer,
                    ByteOffset = Data.Length > 132 ? 131 : (long?)null,
                    ByteLength = Data.Length > 132 ? 2 : (int?)null
                });

                // Register Model: байты 133-134
                // Байт 133: Register Revision
                // Байт 134: Register Type
                // * Примечание: Register Model реконструируется из базы данных по type+revision (байты 133-134),
                // или ищется как ASCII строка в vendor-specific областях, или генерируется как fallback.
                // Для нерегистрируемых модулей отображаем "N/A", но всё равно подсвечиваем байты.
                string registerModelValue = string.IsNullOrEmpty(registerInfo.Model)
                    ? (isUdim ? SpdInfoPanel.FieldLabels.RegisterModelNA : "—")
                    : registerInfo.Model!;

                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.RegisterModel,
                    Value = registerModelValue,
                    ByteOffset = Data.Length > 134 ? 133 : (long?)null,
                    ByteLength = Data.Length > 134 ? 2 : (int?)null
                });

                // Revision / Raw Card: байты 130, 128, 142-143
                // Байт 130: Raw card name (биты 4-0) и revision (биты 6-5), extension bit (бит 7)
                // Байт 128: Raw card ordinal extension (биты 7-5) и height (биты 4-0)
                // Байты 142-143: Raw card revision code (16-bit значение)
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.RevisionRawCard,
                    Value = GetRawCardInfo(),
                    ByteRanges = new List<(long offset, int length)>
                    {
                        (130, 1),  // Байт 130: Raw card name (биты 4-0) и revision (биты 6-5)
                        (128, 1),  // Байт 128: Raw card ordinal extension (биты 7-5) и height (биты 4-0)
                        (142, 2)   // Байты 142-143: Raw card revision code
                    }
                });
                
                // Address Mapping: байт 136, бит 0 (только для RDIMM/LRDIMM)
                // Бит 0: 0=Standard, 1=Mirrored
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.AddressMapping,
                    Value = GetAddressMappingInfo(),
                    ByteRanges = new List<(long offset, int length)> { (136, 1) }
                });
                
                // Manufacturing Date: байты 323-324 (BCD формат)
                // Байт 323: Year (BCD, 00-99 = 2000-2099)
                // Байт 324: Week (BCD, 01-52)
                moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ManufacturingDate, Value = GetManufacturingDateString(323, 324), ByteOffset = 323, ByteLength = 2 });
                
                // Manufacturing Location: байт 322 (Location Code, 0x01-0x10)
                moduleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ManufacturingLocation, Value = GetManufacturingLocationDescription(), ByteOffset = 322, ByteLength = 1 });

                // CRC: байты 126-127 (Block 0) и 254-255 (Block 1)
                // Block 0: байты 0-125, CRC хранится в байтах 126-127 (little-endian)
                // Block 1: байты 128-253, CRC хранится в байтах 254-255 (little-endian)
                var crcInfo = GetDdr4CrcInfo();
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.Crc,
                    Value = crcInfo.OverallStatus,
                    ByteRanges = crcInfo.AllRanges
                });
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.CrcBlock0,
                    Value = crcInfo.Block0Value,
                    ByteRanges = crcInfo.Block0Ranges
                });
                moduleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = SpdInfoPanel.FieldLabels.CrcBlock1,
                    Value = crcInfo.Block1Value,
                    ByteRanges = crcInfo.Block1Ranges
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in PopulateModuleInfo: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private void PopulateDramInfo(List<SpdInfoPanel.InfoItem> dramInfo)
        {
            try
            {
                // Производитель DRAM: байты 350-351 (JEDEC Manufacturer ID)
                // Байт 350: Continuation Code LSB (биты 6-0, бит 7 - parity)
                // Байт 351: Manufacturer Code MSB (биты 6-0, бит 7 - parity)
                dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Manufacturer, Value = GetManufacturerName(Data[350], Data[351]), ByteOffset = 350, ByteLength = 2 });

                // DRAM Part Number: реконструируется из базы данных по параметрам SPD
                // * Примечание: JEDEC не хранит DRAM part number в SPD, поэтому он реконструируется
                // из базы данных на основе: manufacturer, die density, device width, die count, package type
                string dramPart = GetDramPartNumber();
                dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.DramPartNumber, Value = string.IsNullOrWhiteSpace(dramPart) || dramPart == "—" ? "—" : dramPart });

                // Package Type: байт 6
                // Бит 7: 0=Monolithic, 1=Stacked
                // Биты 6-4: Die Count (0-7, значение + 1 = реальное количество dies)
                // Биты 1-0: Signal Loading (0=Not Specified/Monolithic, 1=Multi Load Stack, 2=Single Load Stack/3DS)
                dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Package, Value = GetPackage(), ByteOffset = 6, ByteLength = 1 });
                // Die Density / Count: реконструируется из байтов 4-5, 6, 12
                // * Примечание: Die type (например "B-die") реконструируется из part number, если он найден в базе данных
                // Байты 4-5: SDRAM Density (биты 3-0 байта 4) и Banks (биты 5-4 байта 4)
                // Байт 6: Die Count (биты 6-4) и Stack info (бит 7, биты 1-0)
                // Байт 12: Device Width (биты 2-0)
                dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.DieDensityCount, Value = GetDieDensity() });
                
                // Composition: реконструируется из байтов 4-5, 12
                // Байты 4-5: Total Capacity Per Die (Mb)
                // Байт 12: Device Width (биты 2-0)
                dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Composition, Value = GetComposition() });
                
                // Input Clock Frequency: байты 18 и 125 (tCK = MTB + FTB)
                // Байт 18: tCK Medium Timebase (MTB)
                // Байт 125: tCK Fine Timebase (FTB, signed)
                string inputClock = GetClockFrequency();
                dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.InputClockFrequency, Value = inputClock });
                
                // Addressing: байты 4-5
                // Байт 4: Bank Groups (биты 7-6) и Banks Per Group (биты 5-4)
                // Байт 5: Rows (биты 6-3) и Columns (биты 2-0)
                dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Addressing, Value = GetAddressingInfo() });
                
                // Minimum Timing Delays: байты 18, 24-26, 27-29, 120-123, 125
                // tCK: байты 18 (MTB) и 125 (FTB)
                // tAA: байты 24 (MTB) и 123 (FTB)
                // tRCD: байты 25 (MTB) и 122 (FTB)
                // tRP: байты 26 (MTB) и 121 (FTB)
                // tRAS: байты 28 (LSB) и 27 биты 3-0 (MSB)
                // tRC: байты 29 (LSB), 27 биты 7-4 (MSB) и 120 (FTB)
                string minTiming = GetMinTiming();
                dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.MinimumTimingDelays, Value = minTiming });
                
                // Read Latencies Supported: байты 20-23 (32-bit bitmask)
                // Байты 20-23: Supported CAS Latencies bitmask (CL7-CL38)
                dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ReadLatenciesSupported, Value = GetReadLatencies() });
                
                // Supply Voltage: байт 11, бит 0
                // Бит 0: 0=не поддерживается 1.20V, 1=поддерживается 1.20V
                dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.SupplyVoltage, Value = GetSupplyVoltage() });
                
                // SPD Revision: байт 1
                // Биты 7-4: Major revision (encoding level)
                // Биты 3-0: Minor revision (additions level)
                dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.SpdRevision, Value = GetSpdRevision(), ByteOffset = 1, ByteLength = 1 });
                dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.XmpCertified, Value = GetXmpCertified() });
                dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.XmpExtreme, Value = GetXmpExtreme() });
                dramInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.XmpRevision, Value = GetXmpRevision() });

                foreach (var profile in GetXmpProfiles())
                {
                    dramInfo.Add(new SpdInfoPanel.InfoItem
                    {
                        Label = profile.Label,
                        Value = FormatXmpSummary(profile)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in PopulateDramInfo: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private void PopulateTimings(List<SpdInfoPanel.TimingRow> timingRows)
        {
            double tck = GetTckNs();
            double taa = GetTimingNs(24, 123);
            double trcd = GetTimingNs(25, 122);
            double trp = GetTimingNs(26, 121);
            double tras = GetTimingNsFromComposite(28, 27, 3, 4);
            double trc = GetTimingNsFromComposite(29, 27, 7, 4, ftbIndex: 120);
            double tfaw = GetTimingNsFromComposite(37, 36, 3, 4);
            double trrdS = GetTimingNs(38, 119);
            double trrdL = GetTimingNs(39, 118);
            double twr = GetTimingNsFromComposite(42, 41, 3, 4);
            double twtrS = GetTimingNsFromComposite(44, 43, 3, 4);

            var row = new SpdInfoPanel.TimingRow
            {
                Frequency = GetFrequencyLabel(),
                CAS = FormatTimingCell(taa, tck),
                RCD = FormatTimingCell(trcd, tck),
                RP = FormatTimingCell(trp, tck),
                RAS = FormatTimingCell(tras, tck),
                RC = FormatTimingCell(trc, tck),
                FAW = FormatTimingCell(tfaw, tck),
                RRDS = FormatTimingCell(trrdS, tck),
                RRDL = FormatTimingCell(trrdL, tck),
                WR = FormatTimingCell(twr, tck),
                WTRS = FormatTimingCell(twtrS, tck)
            };

            timingRows.Add(row);
            AddXmpTimingRows(timingRows);
        }

        private void AddXmpTimingRows(List<SpdInfoPanel.TimingRow> timingRows)
        {
            foreach (var profile in GetXmpProfiles())
            {
                var row = new SpdInfoPanel.TimingRow
                {
                    Frequency = profile.DataRate > 0
                        ? $"{profile.Label} ({profile.DataRate} MT/s)"
                        : profile.Label,
                    CAS = FormatTimingCell(profile.TaaNs, profile.TckNs),
                    RCD = FormatTimingCell(profile.TrcdNs, profile.TckNs),
                    RP = FormatTimingCell(profile.TrpNs, profile.TckNs),
                    RAS = FormatTimingCell(profile.TrasNs, profile.TckNs),
                    RC = FormatTimingCell(profile.TrcNs, profile.TckNs),
                    FAW = FormatTimingCell(profile.TfawNs, profile.TckNs),
                    RRDS = FormatTimingCell(profile.TrrdShortNs, profile.TckNs),
                    RRDL = FormatTimingCell(profile.TrrdLongNs, profile.TckNs),
                    WR = FormatTimingCell(profile.TwrNs, profile.TckNs),
                    WTRS = FormatTimingCell(profile.TwtrsNs, profile.TckNs)
                };

                timingRows.Add(row);
            }
        }

        #region DDR4-specific helpers
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
                string speedCode = BuildSpeedCode(dataRate);
                string moduleSection = BuildModuleSection();
                string revisionSuffix = GetSpdRevisionSuffix();

                // Ensure speedCode is not empty
                if (string.IsNullOrEmpty(speedCode))
                {
                    speedCode = dataRate.ToString(CultureInfo.InvariantCulture);
                }

                var descriptorParts = new List<string>();
                if (!string.IsNullOrEmpty(capacity))
                {
                    descriptorParts.Add(capacity);
                }

                if (!string.IsNullOrEmpty(organization))
                {
                    descriptorParts.Add(organization);
                }

                string label = descriptorParts.Count > 0
                    ? $"{string.Join(' ', descriptorParts)} PC4-{speedCode}"
                    : $"PC4-{speedCode}";

                if (!string.IsNullOrEmpty(moduleSection))
                {
                    label += $"-{moduleSection}";
                }

                if (!string.IsNullOrEmpty(revisionSuffix))
                {
                    label += $"-{revisionSuffix}";
                }

                // Ensure we always return a non-empty string
                return string.IsNullOrEmpty(label) ? "—" : label;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetJedecLabel: {ex.Message}");
                return "—";
            }
        }

        private string GetModuleType()
        {
            if (Data.Length < 4)
                return "—";

            byte moduleType = Data[3];
            byte baseType = (byte)(moduleType & 0x0F);

            string map = baseType switch
            {
                0x00 => "Extended DIMM",
                0x01 => "RDIMM",
                0x02 => "UDIMM",
                0x03 => "SO-DIMM",
                0x04 => "LRDIMM",
                0x05 => "Mini-RDIMM",
                0x06 => "Mini-UDIMM",
                0x08 => "72b-SO-RDIMM",
                0x09 => "72b-SO-UDIMM",
                0x0C => "16b-SO-DIMM",
                0x0D => "32b-SO-DIMM",
                _ => $"Unknown (0x{baseType:X2})"
            };

            return $"DDR4 SDRAM {map}";
        }

        private string GetSpeedGrade()
        {
            try
            {
                int dataRate = RoundDataRate(GetTckNs());
                if (dataRate == 0)
                    return "—";

                string suffix = GetSpeedBinSuffix(dataRate);
                string downbin = IsDownbin() ? " downbin" : string.Empty;
                
                return string.IsNullOrEmpty(suffix)
                    ? $"DDR4-{dataRate}{downbin}"
                    : $"DDR4-{dataRate}{suffix}{downbin}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetSpeedGrade: {ex.Message}");
                return "—";
            }
        }

        private bool IsDownbin()
        {
            // Check byte 18 (0x12) bit 7 for downbin indication
            // According to JEDEC DDR4 SPD spec, bit 7 of byte 18 indicates downbin
            if (Data.Length <= 18)
                return false;
            
            return (Data[18] & 0x80) != 0;
        }

        private string GetCapacity()
        {
            long bytes = GetModuleCapacityBytes();
            if (bytes <= 0)
                return "—";

            int ranks = GetRankCount();
            int deviceWidth = GetDeviceWidthBits();
            int primaryBusWidth = GetPrimaryBusWidthBits();
            bool hasEcc = HasEcc();

            if (ranks == 0 || deviceWidth == 0 || primaryBusWidth == 0)
                return FormatDataSize((ulong)bytes);

            // Calculate components per rank
            int primaryComponentsPerRank = primaryBusWidth / deviceWidth;
            int eccComponentsPerRank = hasEcc ? 8 / deviceWidth : 0;

            // Handle Multi Load Stack
            int totalPrimaryComponents;
            int totalEccComponents;
            
            if (IsMultiLoadStack())
            {
                int dieCount = GetDieCount();
                if (dieCount > 0)
                {
                    int effectiveRanks = (int)Math.Ceiling(ranks / (double)dieCount);
                    totalPrimaryComponents = primaryComponentsPerRank * Math.Max(1, effectiveRanks);
                    totalEccComponents = eccComponentsPerRank * Math.Max(1, effectiveRanks);
                }
                else
                {
                    totalPrimaryComponents = primaryComponentsPerRank * ranks;
                    totalEccComponents = eccComponentsPerRank * ranks;
                }
            }
            else
            {
                totalPrimaryComponents = primaryComponentsPerRank * ranks;
                totalEccComponents = eccComponentsPerRank * ranks;
            }

            // Format components text
            string componentsText;
            if (hasEcc && totalEccComponents > 0)
            {
                componentsText = $"{totalPrimaryComponents} + {totalEccComponents} ECC";
            }
            else
            {
                componentsText = $"{totalPrimaryComponents}";
            }

            return $"{FormatDataSize((ulong)bytes)} ({componentsText} components)";
        }

        private string FormatCapacityLabel(long bytes)
        {
            string text = FormatDataSize((ulong)bytes);
            return text == "—"
                ? string.Empty
                : text.Replace(" ", string.Empty, StringComparison.Ordinal);
        }

        private string FormatRankDescriptor()
        {
            int ranks = GetRankCount();
            int deviceWidth = GetDeviceWidthBits();
            if (ranks <= 0 || deviceWidth <= 0)
                return string.Empty;

            int signalLoading = GetSignalLoading();

            if (!IsMonolithic())
            {
                int dieCount = GetDieCount();

                if (signalLoading == 2) // Single Load Stack (3DS)
                {
                    int diePerRank = dieCount / ranks;
                    if (diePerRank > 0)
                    {
                        string diePrefix = diePerRank switch
                        {
                            2 => "2S",
                            3 => "3S",
                            4 => "4S",
                            _ => $"{diePerRank}S"
                        };
                        return $"{diePrefix}{ranks}Rx{deviceWidth}";
                    }
                }
                else if (signalLoading == 1) // Multi Load Stack
                {
                    int labelDieCount = Math.Max(1, (dieCount * ranks) / 2);
                    string diePrefix = $"{labelDieCount}D";
                    return $"{diePrefix}Rx{deviceWidth}";
                }
            }

            return $"{ranks}Rx{deviceWidth}";
        }

        private string BuildSpeedCode(int dataRate)
        {
            string suffix = GetSpeedBinSuffix(dataRate);
            return string.IsNullOrEmpty(suffix)
                ? dataRate.ToString(CultureInfo.InvariantCulture)
                : $"{dataRate}{suffix}";
        }

        private string GetSpeedBinSuffix(int dataRate)
        {
            return SpeedBinCodes.TryGetValue(dataRate, out var value) ? value : string.Empty;
        }

        private string BuildModuleSection()
        {
            string prefix = GetModuleTypePrefix();
            string rawCard = GetRawCardCode();

            if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(rawCard))
            {
                return string.Empty;
            }

            return $"{prefix}{rawCard}";
        }

        private string GetModuleTypePrefix()
        {
            if (Data.Length < 4)
                return string.Empty;

            return (Data[3] & 0x0F) switch
            {
                0x00 => "X",
                0x01 => "R",
                0x02 => "U",
                0x03 => "S",
                0x04 => "L",
                0x05 => "R",
                0x06 => "U",
                0x08 => "S",
                0x09 => "S",
                0x0C => "S",
                0x0D => "S",
                _ => string.Empty
            };
        }

        private string GetSpdRevisionSuffix()
        {
            if (Data.Length <= 1)
                return string.Empty;

            int major = (Data[1] >> 4) & 0x0F;
            int minor = Data[1] & 0x0F;

            if (major == 0 && minor == 0)
                return string.Empty;

            return $"{major}{minor}";
        }

        private string GetOrganization()
        {
            int ranks = GetRankCount();
            if (ranks == 0)
                return "—";

            int busWidth = GetPrimaryBusWidthBits() + (HasEcc() ? 8 : 0);
            int perDieMb = GetTotalCapacityPerDieMb();
            if (perDieMb <= 0)
                return "—";

            return $"{perDieMb}M x{busWidth} ({ranks} rank{(ranks > 1 ? "s" : string.Empty)})";
        }

        private string GetRevision() => "0000h / —";

        private string GetPackage()
        {
            if (Data.Length < 7)
                return "—";

            bool isMonolithic = IsMonolithic();
            int dieCount = GetDieCount();
            int signalLoading = GetSignalLoading();
            
            // Try to find exact match in known package types
            if (KnownPackageTypes.TryGetValue((dieCount, signalLoading, isMonolithic), out string? knownPackage))
            {
                return knownPackage;
            }
            
            // Fallback to generic description
            string packageType = GetPackageTypeName(dieCount, signalLoading, isMonolithic);
            string ballCount = GetBallCount(dieCount, signalLoading, isMonolithic);
            string standardPrefix = IsStandardPackage() ? "Standard" : "Non-Standard";
            
            if (isMonolithic)
            {
                return $"{standardPrefix} Monolithic {ballCount}";
            }
            
            if (signalLoading == 2) // Single Load Stack (3DS)
            {
                return $"{dieCount}-High 3DS {ballCount}";
            }
            
            // For Multi Load Stack, simplify to "Non-Standard 78-ball FBGA" (like Thaiphoon Burner)
            if (signalLoading == 1)
            {
                return $"Non-Standard {ballCount}";
            }
            
            return $"{standardPrefix} {packageType} {ballCount}";
        }

        private string GetPackageTypeName(int dieCount, int signalLoading, bool isMonolithic)
        {
            if (isMonolithic)
                return "Monolithic";
            
            return signalLoading switch
            {
                1 => "Multi Load Stack",
                2 => "Single Load Stack",
                _ => "Stack"
            };
        }

        private string GetBallCount(int dieCount, int signalLoading, bool isMonolithic)
        {
            // Most common DDR4 packages use 78-ball FBGA
            // Some high-density or special packages may use 96-ball or 68-ball
            
            // For monolithic and standard packages, typically 78-ball
            if (isMonolithic)
            {
                return "78-ball FBGA";
            }
            
            // For 3DS (Single Load Stack), typically 78-ball
            if (signalLoading == 2)
            {
                return "78-ball FBGA";
            }
            
            // For Multi Load Stack, check if it's a known configuration
            if (signalLoading == 1)
            {
                // Multi Load Stack packages are typically 78-ball, but can vary
                // Some high-density configurations may use 96-ball
                return "78-ball FBGA";
            }
            
            // Default to 78-ball FBGA (most common)
            return "78-ball FBGA";
        }

        private bool IsStandardPackage()
        {
            if (Data.Length <= 6)
                return true;

            // Multi Load Stack packages are often non-standard
            // Example: Samsung M386AAG40BM3-CWE (Multi Load Stack, 2 die) -> Non-Standard
            if (!IsMonolithic())
            {
                int signalLoading = GetSignalLoading();
                if (signalLoading == 1) // Multi Load Stack
                {
                    // Multi Load Stack is typically non-standard
                    return false;
                }
            }

            // Default to standard for monolithic and Single Load Stack
            return true;
        }

        private string GetDramPartNumber()
        {
            // JEDEC не хранит DRAM part number в SPD, поэтому сразу реконструируем его по параметрам
            // Thaiphoon Burner делает то же самое – использует базу соответствий (плотность, ширина, тип корпуса и т.д.)
            return ReconstructDramPartNumberFromSpdParams();
        }

        /// <summary>
        /// Реконструирует DRAM part number на основе параметров из SPD
        /// Thaiphoon Burner использует аналогичный подход - базу данных соответствий
        /// </summary>
        private string ReconstructDramPartNumberFromSpdParams()
        {
            // Получаем параметры из SPD для поиска в базе данных
            string manufacturer = GetManufacturerName(Data[350], Data[351]);
            int dieDensityMb = GetTotalCapacityPerDieMb();
            int deviceWidth = GetDeviceWidthBits();
            int dieCount = GetDieCount();
            bool isMonolithic = IsMonolithic();
            int signalLoading = GetSignalLoading();
            bool isMultiLoadStack = !isMonolithic && signalLoading == 1;
            bool isSingleLoadStack = !isMonolithic && signalLoading == 2;
            int rows = GetRowCount();
            int cols = GetColumnCount();
            int bankGroups = GetBankGroupCount();
            int banksPerGroup = GetBanksPerGroup();

            // Ищем во внешней базе данных (JSON файл)
            var externalEntries = DramPartNumberDatabase.LoadDatabase();
            foreach (var entry in externalEntries)
            {
                if (MatchesSpdParams(entry, manufacturer, dieDensityMb, deviceWidth, dieCount, isMultiLoadStack))
                {
                    return entry.PartNumber;
                }
            }

            // Если не нашли в базе, возвращаем "—"
            return "—";
        }

        /// <summary>
        /// Проверяет, соответствует ли запись из базы данных параметрам SPD
        /// </summary>
        private static bool MatchesSpdParams(
            DramPartNumberEntry entry,
            string manufacturer,
            int dieDensityMb,
            int deviceWidth,
            int dieCount,
            bool isMultiLoadStack)
        {
            // Проверяем соответствие производителя
            if (!string.Equals(entry.Manufacturer, manufacturer, StringComparison.OrdinalIgnoreCase))
                return false;

            // Проверяем соответствие die density
            // Для Multi Load Stack: dieDensityMb из SPD = общая плотность устройства (dieCount * плотность одного die)
            // Для Monolithic/3DS: dieDensityMb из SPD = плотность одного die
            int baseDieDensityGb = dieDensityMb / 1024;
            if (entry.DieDensityGb.HasValue)
            {
                if (isMultiLoadStack && entry.DieCount.HasValue && entry.DieCount.Value > 1)
                {
                    // Для Multi Load Stack: общая плотность = dieCount * плотность одного die
                    int expectedTotalGb = entry.DieDensityGb.Value * entry.DieCount.Value;
                    if (baseDieDensityGb != expectedTotalGb)
                        return false;
                }
                else
                {
                    // Для Monolithic/3DS: сравниваем плотность одного die
                    if (entry.DieDensityGb.Value != baseDieDensityGb)
                        return false;
                }
            }

            // Проверяем соответствие device width
            if (entry.DeviceWidth.HasValue && entry.DeviceWidth.Value != deviceWidth)
                return false;

            // Проверяем соответствие die count
            if (entry.DieCount.HasValue && entry.DieCount.Value != dieCount)
                return false;

            // Проверяем соответствие package type
            if (entry.IsMultiLoadStack.HasValue && entry.IsMultiLoadStack.Value != isMultiLoadStack)
                return false;

            return true;
        }


        private string GetDieDensity()
        {
            // According to JEDEC standard (src/SpdReaderWriterCore/SPD/DDR4.cs):
            // DieDensity = (2^Rows) * (2^Columns) * BankAddress * BankGroup * DeviceWidth
            // This always gives the density per single die, regardless of package type
            // This is the correct JEDEC-compliant way to calculate Die Density
            int rows = GetRowCount();
            int cols = GetColumnCount();
            int bankGroups = GetBankGroupCount();
            int banksPerGroup = GetBanksPerGroup();
            int deviceWidth = GetDeviceWidthBits();

            if (rows <= 0 || cols <= 0 || bankGroups <= 0 || banksPerGroup <= 0 || deviceWidth <= 0)
                return "—";

            // Calculate DieDensity in bits using JEDEC formula
            ulong dieDensityBits = (1UL << rows) * (1UL << cols) * (ulong)banksPerGroup * (ulong)bankGroups * (ulong)deviceWidth;
            
            // Convert to megabits (divide by 1024*1024)
            int perDieMb = (int)(dieDensityBits / (1024UL * 1024UL));

            if (perDieMb <= 0)
                return "—";

            double perDieGb = perDieMb / 1024.0;
            string dieSizeText = Math.Abs(perDieGb - Math.Round(perDieGb)) < 0.01
                ? $"{perDieGb.ToString("F0", CultureInfo.InvariantCulture)} Gb"
                : $"{perDieGb.ToString("F2", CultureInfo.InvariantCulture)} Gb";
            
            int dieCount = GetDieCount();
            string dieCountText = dieCount == 1 ? "1 die" : $"{dieCount} dies";
            
            // Try to determine die type from part number
            string dieTypeInfo = GetDieTypeInfo();
            if (!string.IsNullOrEmpty(dieTypeInfo))
            {
                return $"{dieSizeText} {dieTypeInfo} / {dieCountText}";
            }
            
            return $"{dieSizeText} / {dieCountText}";
        }

        private string GetDieTypeInfo()
        {
            string dramPart = GetDramPartNumber();
            if (string.IsNullOrWhiteSpace(dramPart) || dramPart == "—")
                return string.Empty;

            // Try exact match first
            foreach (var pattern in DieTypePatterns)
            {
                if (dramPart.StartsWith(pattern.Key, StringComparison.OrdinalIgnoreCase))
                {
                    var (dieType, process) = pattern.Value;
                    if (!string.IsNullOrEmpty(process))
                    {
                        return $"{dieType} ({process})";
                    }
                    return dieType;
                }
            }

            // Try partial match for Samsung (K4AAG045W*)
            if (dramPart.StartsWith("K4AAG045W", StringComparison.OrdinalIgnoreCase))
            {
                if (dramPart.Length >= 11)
                {
                    char dieCode = dramPart[10];
                    return dieCode switch
                    {
                        'C' => "C-die",
                        'B' => "B-die (Armstrong / 17 nm)",
                        'M' => "M-die (Pascal / 18 nm)",
                        _ => string.Empty
                    };
                }
            }

            // Try partial match for Samsung (K4A8G085W*)
            if (dramPart.StartsWith("K4A8G085W", StringComparison.OrdinalIgnoreCase))
            {
                if (dramPart.Length >= 11)
                {
                    char dieCode = dramPart[10];
                    return dieCode switch
                    {
                        'E' => "E-die (Kevlar / 16 nm)",
                        'B' => "B-die",
                        _ => string.Empty
                    };
                }
            }

            return string.Empty;
        }

        private int GetRowCount()
        {
            if (Data.Length <= 5)
                return 0;
            return ((Data[5] >> 3) & 0x7) + 12;
        }

        private int GetColumnCount()
        {
            if (Data.Length <= 5)
                return 0;
            return (Data[5] & 0x7) + 9;
        }

        private string GetComposition()
        {
            int bankGroups = GetBankGroupCount();
            int banksPerGroup = GetBanksPerGroup();
            int deviceWidth = GetDeviceWidthBits();

            if (bankGroups <= 0 || banksPerGroup <= 0 || deviceWidth <= 0)
                return "—";

            // According to JEDEC standard:
            // For Composition, JEDEC-SPD describes capacity per SDRAM device (entire stack), not per die
            // TotalCapacityPerDie from byte 4 represents the capacity of the entire SDRAM device
            // For Multi Load Stack, this is the total capacity across all dies in the device
            // For Monolithic and Single Load Stack (3DS), this is the capacity per die
            // Note: Thaiphoon Burner incorrectly uses calculated DieDensity instead of TotalCapacityPerDie
            int totalCapacityPerDieMb = GetTotalCapacityPerDieMb();
            
            if (totalCapacityPerDieMb <= 0)
                return "—";

            // For Composition, we use TotalCapacityPerDie as-is for all package types
            // This represents the capacity of one SDRAM device (package) as per JEDEC standard
            int deviceCapacityMb = totalCapacityPerDieMb;

            int totalBanks = bankGroups * banksPerGroup;
            
            // Calculate capacity per bank: deviceCapacityMb / totalBanks
            // Format per JEDEC standard: "16384Mb x4 (1024Mb x4 x 16 banks)"
            int perBankMb = deviceCapacityMb / totalBanks;
            
            string composition = $"{deviceCapacityMb}Mb x{deviceWidth} ({perBankMb}Mb x{deviceWidth} x {totalBanks} banks)";
            
            // Add per-die composition for Multi Load Stack and 3DS
            bool isMonolithic = IsMonolithic();
            int signalLoading = GetSignalLoading();
            int dieCount = GetDieCount();
            bool isMultiLoadStack = !isMonolithic && signalLoading == 1;
            bool isSingleLoadStack = !isMonolithic && signalLoading == 2;
            
            if ((isMultiLoadStack || isSingleLoadStack) && dieCount > 1)
            {
                // Calculate per-die capacity
                int perDieMb;
                if (isMultiLoadStack)
                {
                    // For Multi Load Stack, divide device capacity by dieCount
                    perDieMb = deviceCapacityMb / dieCount;
                }
                else
                {
                    // For Single Load Stack (3DS), use calculated DieDensity from addressing
                    int rows = GetRowCount();
                    int cols = GetColumnCount();
                    if (rows > 0 && cols > 0)
                    {
                        ulong dieDensityBits = (1UL << rows) * (1UL << cols) * (ulong)banksPerGroup * (ulong)bankGroups * (ulong)deviceWidth;
                        perDieMb = (int)(dieDensityBits / (1024UL * 1024UL));
                    }
                    else
                    {
                        perDieMb = deviceCapacityMb / dieCount;
                    }
                }
                
                if (perDieMb > 0)
                {
                    int perDiePerBankMb = perDieMb / totalBanks;
                    composition += $" / Per die: {perDieMb}Mb x{deviceWidth} ({perDiePerBankMb}Mb x{deviceWidth} x {totalBanks} banks)";
                }
            }
            
            return composition;
        }

        private string GetClockFrequency()
        {
            try
            {
                double tck = GetTckNs();
                if (tck <= 0)
                    return "—";

                int dataRate = RoundDataRate(tck);
                double freqMHz = dataRate / 2.0;
                double rounded = Math.Round(freqMHz, 0, MidpointRounding.ToZero);
                // Format like Thaiphoon: "1600 MHz (0.625 ns)" with dot
                return $"{rounded.ToString("F0", CultureInfo.InvariantCulture)} MHz ({tck.ToString("F3", CultureInfo.InvariantCulture)} ns)";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetClockFrequency: {ex.Message}");
                return "—";
            }
        }

        private string GetMinTiming()
        {
            try
            {
                double tck = GetTckNs();
                double taa = GetTimingNs(24, 123);
                double trcd = GetTimingNs(25, 122);
                double trp = GetTimingNs(26, 121);
                // tRAS: Byte 28 (LSB) | (Byte 27, bits 3-0 << 8)
                // tRC: Byte 29 (LSB) | (Byte 27, bits 7-4 << 8) | Fine offset byte 120
                // According to src/SpdReaderWriterCore/SPD/DDR4.cs:
                // tRAS: RawData[28] | (Data.SubByte(RawData[27], 3, 4) << 8) - SubByte(27, 3, 4) reads bits 3-0
                // tRC: RawData[29] | (Data.SubByte(RawData[27], 7, 4) << 8) - SubByte(27, 7, 4) reads bits 7-4
                double tras = GetTrasNs();
                double trc = GetTrcNs();

                if (tck <= 0 || taa <= 0 || trcd <= 0 || trp <= 0 || tras <= 0 || trc <= 0)
                    return "—";

                double clCycles = Math.Ceiling(taa / tck);
                double trcdCycles = Math.Ceiling(trcd / tck);
                double trpCycles = Math.Ceiling(trp / tck);
                double trasCycles = Math.Ceiling(tras / tck);
                double trcCycles = Math.Ceiling(trc / tck);

                return $"{clCycles:0}-{trcdCycles:0}-{trpCycles:0}-{trasCycles:0}-{trcCycles:0}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetMinTiming: {ex.Message}");
                return "—";
            }
        }

        private double GetTrasNs()
        {
            // According to JEDEC standard (src/SpdReaderWriterCore/SPD/DDR4.cs, line 520-528):
            // tRASmin: Byte 28 (LSB) | (Byte 27, bits 3-0 << 8)
            // Data.SubByte(RawData[27], 3, 4) reads bits 3-0 (lower nibble) from byte 27
            if (Data.Length <= 28)
                return 0;

            byte byte27 = Data[27];
            byte byte28 = Data[28];
            
            // Read bits 3-0 (lower nibble) from byte 27: (byte27 >> 0) & 0xF
            int highBits = (byte27 >> 0) & 0xF;
            int combinedValue = byte28 | (highBits << 8);
            
            // Convert to nanoseconds: Medium value * MediumTimebasePs / 1000.0
            // tRASmin uses only Medium timebase (no Fine offset)
            double mtb = combinedValue * MediumTimebasePs / 1000.0;
            return mtb;
        }

        private double GetTrcNs()
        {
            // According to JEDEC standard (src/SpdReaderWriterCore/SPD/DDR4.cs, line 534-543):
            // tRCmin: Byte 29 (LSB) | (Byte 27, bits 7-4 << 8) | Fine offset byte 120
            // Data.SubByte(RawData[27], 7, 4) reads bits 7-4 (upper nibble) from byte 27
            if (Data.Length <= 29)
                return 0;

            byte byte27 = Data[27];
            byte byte29 = Data[29];
            
            // Read bits 7-4 (upper nibble) from byte 27: (byte27 >> 4) & 0xF
            int highBits = (byte27 >> 4) & 0xF;
            int combinedValue = byte29 | (highBits << 8);
            
            // Convert Medium value to nanoseconds: Medium * MediumTimebasePs / 1000.0
            double mtb = combinedValue * MediumTimebasePs / 1000.0;
            
            // Add Fine offset if available (byte 120, signed byte)
            if (Data.Length > 120)
            {
                sbyte fineOffset = (sbyte)Data[120];
                double ftb = fineOffset * FineTimebasePs / 1000.0;
                return mtb + ftb;
            }
            
            return mtb;
        }

        private string GetReadLatencies()
        {
            if (Data.Length < 24)
                return "—";

            uint bitmask = (uint)(Data[20] | (Data[21] << 8) | (Data[22] << 16) | (Data[23] << 24));
            bool highRange = (Data[23] & 0x80) != 0;
            var latencies = new List<int>();

            int clBase = 7;
            int highRangeOffset = highRange ? 32 : 0;

            for (int i = 0; i < 32; i++)
            {
                if (((bitmask >> i) & 1) != 0)
                {
                    latencies.Add(i + clBase + highRangeOffset);
                }
            }

            if (latencies.Count == 0)
                return "—";

            // Sort in descending order (like Thaiphoon Burner)
            latencies.Sort((a, b) => b.CompareTo(a));
            
            // Format: "24T, 22T, 21T, 20T, 19T, 18T, 17T..." (like Thaiphoon Burner)
            return string.Join(", ", latencies.Select(l => $"{l}T"));
        }

        private string GetSupplyVoltage()
        {
            if (Data.Length < 12)
                return "—";

            bool operable = (Data[11] & 0x01) != 0;
            // Format like Thaiphoon: "1.20 V" with dot
            return operable ? "1.20 V" : "—";
        }

        private string GetXmpCertified()
        {
            return HasXmpHeader() ? "Programmed" : "Not programmed";
        }

        private string GetXmpExtreme() => "Not programmed";

        private string GetSpdRevision()
        {
            if (Data.Length < 2)
                return "—";

            byte encoding = (byte)((Data[1] >> 4) & 0x0F);
            byte additions = (byte)(Data[1] & 0x0F);
            string version = $"{encoding}.{additions}";
            
            // Add release date for known SPD revisions (like Thaiphoon Burner: "1.2 / August 2016")
            string releaseDate = GetSpdRevisionDate(encoding, additions);
            if (!string.IsNullOrEmpty(releaseDate))
            {
                return $"{version} / {releaseDate}";
            }
            
            return version;
        }

        private static string GetSpdRevisionDate(byte encoding, byte additions)
        {
            // SPD Revision release dates (from JEDEC specifications)
            return (encoding, additions) switch
            {
                (1, 0) => "September 2014",
                (1, 1) => "September 2015",
                (1, 2) => "August 2016",
                (1, 3) => "November 2017",
                (1, 4) => "November 2020",
                _ => string.Empty
            };
        }

        private string GetXmpRevision() => "Undefined";

        private string GetFrequencyLabel()
        {
            double tck = GetTckNs();
            int dataRate = RoundDataRate(tck);
            return dataRate == 0 ? "—" : $"{dataRate} MT/s";
        }

        private double GetTckNs() => _cachedTckNs ??= GetTimingNs(18, 125);

        private string FormatTimingCell(double timingNs, double tck)
        {
            if (tck <= 0 || timingNs <= 0)
                return "—";
            double cycles = timingNs / tck;
            return $"{Math.Round(cycles, 1):F1}";
        }

        private int RoundDataRate(double tck)
        {
            if (tck <= 0)
                return 0;
            double dataRate = 2000.0 / tck; // Convert tCK (ns) to data rate (MT/s)
            return (int)Math.Round(dataRate / 100.0) * 100; // Round to nearest 100 MT/s
        }

        private int GetRankCount()
        {
            if (Data.Length <= 12)
                return 0;
            return ((Data[12] >> 3) & 0x7) + 1;
        }

        private int GetDeviceWidthBits()
        {
            if (Data.Length <= 12)
                return 0;
            return 4 << (Data[12] & 0x7);
        }

        private int GetPrimaryBusWidthBits()
        {
            if (Data.Length <= 13)
                return 0;
            return 8 << (Data[13] & 0x7);
        }

        private bool HasEcc() => Data.Length > 13 && ((Data[13] >> 3) & 0x1) != 0;

        private int GetTotalBusWidthBits() => GetPrimaryBusWidthBits() + (HasEcc() ? 8 : 0);

        private int GetDieCount()
        {
            if (Data.Length <= 6)
                return 1;
            // Bits 6-4: DieCount (value + 1)
            // SubByte(6, 6, 3) reads 3 bits starting at position 6: bits 6, 5, 4
            // To get bits 6-4, we shift right by 4 and mask with 0x7
            return ((Data[6] >> 4) & 0x7) + 1;
        }

        private bool IsMonolithic()
        {
            if (Data.Length <= 6)
                return true;
            // Bit 7: 0 = Monolithic, 1 = Stacked
            return (Data[6] & 0x80) == 0;
        }

        private int GetSignalLoading()
        {
            if (Data.Length <= 6)
                return 0; // Not Specified / Monolithic
            // Bits 1-0: SignalLoading
            // 0 = Not Specified / Monolithic
            // 1 = Multi Load Stack
            // 2 = Single Load Stack
            return Data[6] & 0x03;
        }

        private bool IsSingleLoadStack()
        {
            return !IsMonolithic() && GetSignalLoading() == 2;
        }

        private bool IsMultiLoadStack()
        {
            return !IsMonolithic() && GetSignalLoading() == 1;
        }

        private int GetTotalCapacityPerDieMb()
        {
            if (Data.Length <= 4)
                return 0;

            // Bits 3-0: Capacity per die (4 bits)
            // Bit 3: 0 = Standard density (256Mb-32Gb), 1 = 3DS density (12Gb-24Gb)
            int capacityField = Data[4] & 0x0F;
            bool is3Ds = (Data[4] & 0x08) != 0;
            
            // Formula from JEDEC standard matches src/SpdReaderWriterCore/SPD/DDR4.cs
            return !is3Ds
                ? (2 << (capacityField + 7))  // 256Mb-32Gb range
                : (3 << (capacityField + 4));  // 12Gb-24Gb range (3DS)
        }

        private long GetModuleCapacityBytes()
        {
            int perDieMb = GetTotalCapacityPerDieMb();
            if (perDieMb == 0)
                return 0;

            int deviceWidth = GetDeviceWidthBits();
            int busWidth = GetPrimaryBusWidthBits();
            int ranks = GetRankCount();

            if (deviceWidth == 0 || busWidth == 0 || ranks == 0)
                return 0;

            // For Single Load Stack (3DS), multiply by dieCount
            // For Monolithic or Multi Load Stack, dieCount is effectively 1
            int effectiveDieCount = IsSingleLoadStack() ? GetDieCount() : 1;

            // Formula from JEDEC standard (TotalModuleCapacityProgrammed):
            // TotalCapacityPerDie (in Mb) / 8 * (BusWidth / DeviceWidth) * Ranks * DieCount * 1024 * 1024
            // perDieMb is in megabits, divide by 8 to get megabytes
            long perDieMB = perDieMb / 8;
            
            // Calculate ratio and multiply by ranks and dieCount
            long moduleMB = perDieMB * (busWidth / deviceWidth) * ranks * effectiveDieCount;
            
            // Convert megabytes to bytes
            return moduleMB * 1024L * 1024L;
        }

        private int GetBankGroupCount()
        {
            if (Data.Length <= 4)
                return 4;

            int code = (Data[4] >> 6) & 0x3;
            return code == 0 ? 4 : code * 2;
        }

        private int GetBanksPerGroup()
        {
            if (Data.Length <= 4)
                return 4;

            int code = (Data[4] >> 4) & 0x3;
            return 1 << (code + 2);
        }

        private int GetComponentsPerRank()
        {
            int totalBus = GetTotalBusWidthBits();
            int deviceWidth = GetDeviceWidthBits();
            if (totalBus == 0 || deviceWidth == 0)
                return 0;
            return totalBus / deviceWidth;
        }

        private int GetTotalComponents()
        {
            int perRank = GetComponentsPerRank();
            int ranks = GetRankCount();
            if (perRank == 0 || ranks == 0)
                return 0;

            if (IsMultiLoadStack())
            {
                int dieCount = GetDieCount();
                if (dieCount > 0)
                {
                    int effectiveRanks = (int)Math.Ceiling(ranks / (double)dieCount);
                    return perRank * Math.Max(1, effectiveRanks);
                }
            }

            return perRank * ranks;
        }

        private (string? Manufacturer, string? Model) GetRegisterInfo()
        {
            if (!IsRegisteredModule() || Data.Length <= 134)
            {
                return default;
            }

            byte revision = Data[133];
            byte type = Data[134];

            if (RegisterModelDatabase.TryGetModel(type, revision, out var entry) && entry != null)
            {
                return (entry.Manufacturer, entry.Model);
            }

            string manufacturer = GetManufacturerName(Data[131], Data[132]);
            string? model = GetRegisterModelFromAscii();

            if (string.IsNullOrEmpty(model))
            {
                model = $"Type 0x{type:X2}, rev 0x{revision:X2}";
                System.Diagnostics.Debug.WriteLine(
                    $"[SPD] Unknown register model: type=0x{type:X2}, rev=0x{revision:X2}, manufacturer={manufacturer}. " +
                    $"Добавьте запись в register_models.json");
            }

            return (manufacturer, model);
        }

        private string? GetRegisterModelFromAscii()
        {
            foreach (var (_, text) in EnumerateAsciiStrings(6, 256))
            {
                string cleaned = text.Trim();
                if (cleaned.Length > 48 || cleaned.Length < 3)
                    continue;

                if (cleaned.Contains("RCD", StringComparison.OrdinalIgnoreCase) ||
                    cleaned.StartsWith("M88", StringComparison.OrdinalIgnoreCase) ||
                    cleaned.StartsWith("4RCD", StringComparison.OrdinalIgnoreCase) ||
                    cleaned.StartsWith("4DB", StringComparison.OrdinalIgnoreCase) ||
                    cleaned.StartsWith("iDDR4", StringComparison.OrdinalIgnoreCase) ||
                    cleaned.StartsWith("RC0", StringComparison.OrdinalIgnoreCase) ||
                    cleaned.StartsWith("NT5", StringComparison.OrdinalIgnoreCase))
                {
                    return cleaned;
                }
            }

            return null;
        }

        private bool IsRegisteredModule()
        {
            if (Data.Length <= 3)
                return false;

            int type = Data[3] & 0x0F;
            return type is 0x01 or 0x04 or 0x05 or 0x08;
        }

        private bool IsLrdimm()
        {
            if (Data.Length <= 3)
                return false;

            int type = Data[3] & 0x0F;
            return type == 0x04; // LRDIMM
        }

        private bool IsUdim()
        {
            if (Data.Length <= 3)
                return false;

            int type = Data[3] & 0x0F;
            return type is 0x02 or 0x06 or 0x09;
        }

        private string GetManufacturingLocationDescription()
        {
            if (Data.Length <= 322)
                return "—";

            byte code = Data[322];
            if (code == 0)
                return "—";

            return LocationNames.TryGetValue(code, out var name)
                ? name
                : $"Unknown: {code:X2}h";
        }

        private bool HasThermalSensor()
        {
            return Data.Length > 14 && (Data[14] & 0x80) != 0;
        }

        private string GetModuleHeightInfo()
        {
            if (Data.Length <= 128)
                return "—";

            int heightIndex = Data[128] & 0x1F;
            int min = heightIndex + 15;
            int max = min + 1;

            return $"{min}-{max} mm";
        }

        private string GetModuleThicknessInfo()
        {
            if (Data.Length <= 129)
                return "—";

            int backCode = (Data[129] >> 4) & 0x0F;
            int frontCode = Data[129] & 0x0F;

            return $"Front {FormatThicknessRange(frontCode)} / Back {FormatThicknessRange(backCode)}";
        }

        private static string FormatThicknessRange(int code)
        {
            double min = 1.0 + 0.25 * code;
            double max = min + 0.25;
            return $"{min:F2}-{max:F2} mm";
        }

        private string GetRawCardInfo()
        {
            if (Data.Length <= 130)
                return "—";

            // Get revision code (bytes 142-143 for DDR4)
            ushort revisionCode = 0x0000;
            if (Data.Length > 143)
            {
                revisionCode = (ushort)(Data[142] | (Data[143] << 8));
            }

            byte raw = Data[130];
            bool extension = (raw & 0x80) != 0;
            int revision = (raw >> 5) & 0x03;
            int code = raw & 0x1F;

            string codeText;

            if (code == 0x1F)
            {
                codeText = "ZZ";
            }
            else if (extension)
            {
                // Extension bit means we need to use RawCardNameTable
                int extendedCode = code + 32;
                if (extendedCode < RawCardNameTable.Length)
                {
                    string baseName = RawCardNameTable[extendedCode];
                    int ordinal = GetRawCardOrdinal();
                    codeText = ordinal > 0 ? $"{baseName}{ordinal}" : baseName;
                }
                else
                {
                    // Fallback to letter if table doesn't have entry
                    codeText = ((char)('A' + code)).ToString(CultureInfo.InvariantCulture);
                }
            }
            else
            {
                codeText = ((char)('A' + code)).ToString(CultureInfo.InvariantCulture);
            }

            // Format like Thaiphoon: "0000h / D3" or "FF00h / B4 (12 layers)" or "E1 / B1 (12 layers)"
            // Read actual revision code from bytes 142-143
            string revisionHex = $"{revisionCode:X4}h";
            string rawCardPart;

            if (RawCardLayers.TryGetValue(codeText, out int layers))
            {
                rawCardPart = $"{codeText} ({layers} layers)";
            }
            else
            {
                rawCardPart = revision > 0 ? $"{codeText}{revision}" : codeText;
            }

            return $"{revisionHex} / {rawCardPart}";
        }

        private string GetRawCardCode()
        {
            if (Data.Length <= 130)
                return string.Empty;

            int cardValue = Data[130] & 0x1F;
            bool extension = (Data[130] & 0x80) != 0;

            if (cardValue == 0x1F)
                return "ZZ";

            int index = cardValue + (extension ? 32 : 0);
            if (index < 0 || index >= RawCardNameTable.Length)
                return string.Empty;

            string baseName = RawCardNameTable[index];
            int ordinal = GetRawCardOrdinal();
            return ordinal > 0 ? $"{baseName}{ordinal}" : baseName;
        }

        private int GetRawCardOrdinal()
        {
            if (Data.Length <= 128)
                return 0;

            int extensionValue = (Data[128] >> 5) & 0x07;
            if (extensionValue > 0)
            {
                return extensionValue + 3;
            }

            if (Data.Length > 130)
            {
                return (Data[130] >> 5) & 0x07;
            }

            return 0;
        }

        private string GetAddressMappingInfo()
        {
            if (!IsRegisteredModule())
                return "Not applicable";

            if (Data.Length <= 136)
                return "Unknown";

            bool mirrored = (Data[136] & 0x1) != 0;
            return mirrored ? "Mirrored" : "Standard";
        }

        public (bool IsValid, string OverallStatus, List<(long offset, int length)> AllRanges,
                 string Block0Value, List<(long offset, int length)> Block0Ranges,
                 string Block1Value, List<(long offset, int length)> Block1Ranges)
            GetDdr4CrcInfo()
        {
            var allRanges = new List<(long offset, int length)>();
            var block0Ranges = new List<(long offset, int length)>();
            var block1Ranges = new List<(long offset, int length)>();

            var block0 = BuildCrcInfo(0, 126, 126, block0Ranges);
            var block1 = BuildCrcInfo(128, 126, 254, block1Ranges);

            allRanges.AddRange(block0Ranges);
            allRanges.AddRange(block1Ranges);

            string overall = (block0.IsOk && block1.IsOk) ? "OK" : "BAD";

            return (
                IsValid: block0.IsOk && block1.IsOk,
                OverallStatus: overall,
                AllRanges: allRanges,
                Block0Value: block0.Value,
                Block0Ranges: block0Ranges,
                Block1Value: block1.Value,
                Block1Ranges: block1Ranges
            );
        }

        private (string Value, bool IsOk) BuildCrcInfo(
            int dataStart,
            int dataLength,
            int storedOffset,
            List<(long offset, int length)> ranges)
        {
            if (Data.Length < dataStart + dataLength)
            {
                int available = Math.Max(0, Data.Length - dataStart);
                return ($"data incomplete ({available}/{dataLength} bytes)", false);
            }

            ushort calculated = ComputeDdr4Crc(dataStart, dataLength);

            if (Data.Length >= storedOffset + 2)
            {
                ushort storedValue = (ushort)((Data[storedOffset + 1] << 8) | Data[storedOffset]);
                bool match = storedValue == calculated;
                ranges.Add((storedOffset, 2));
                return ($"calc 0x{calculated:X4} - {(match ? "OK" : "BAD")}", match);
            }

            if (Data.Length == storedOffset + 1)
            {
                ranges.Add((storedOffset, 1));
                return ($"calc 0x{calculated:X4} - BAD (stored incomplete)", false);
            }

            return ($"calc 0x{calculated:X4} - BAD (stored missing)", false);
        }

        private ushort ComputeDdr4Crc(int start, int length)
        {
            const ushort polynomial = 0x1021;
            ushort crc = 0;

            for (int i = start; i < start + length; i++)
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

        private string GetAddressingInfo()
        {
            if (Data.Length <= 5)
                return "—";

            int rows = ((Data[5] >> 3) & 0x7) + 12;
            int columns = (Data[5] & 0x7) + 9;
            int bankGroups = GetBankGroupCount();
            int banksPerGroup = GetBanksPerGroup();

            return $"{rows} rows × {columns} cols, {bankGroups} BG × {banksPerGroup} banks";
        }

        private IReadOnlyList<XmpProfile> GetXmpProfiles()
        {
            var profiles = new List<XmpProfile>();
            if (!HasXmpHeader() || !HasByte(0x182))
            {
                return profiles;
            }

            byte enableBits = Data[0x182];

            for (int index = 0; index < 2; index++)
            {
                if ((enableBits & (1 << index)) == 0)
                {
                    continue;
                }

                int profileOffset = index * 63;
                int requiredIndex = 0x1AF + profileOffset;
                if (!HasByte(requiredIndex))
                {
                    continue;
                }

                double tck = GetTimingNs(0x18C + profileOffset, 0x1AF + profileOffset);
                if (tck <= 0)
                {
                    continue;
                }

                var profile = new XmpProfile
                {
                    Label = index == 0 ? "XMP Profile 1" : "XMP Profile 2",
                    TckNs = tck,
                    TaaNs = GetTimingNs(0x191 + profileOffset, 0x1AE + profileOffset),
                    TrcdNs = GetTimingNs(0x192 + profileOffset, 0x1AD + profileOffset),
                    TrpNs = GetTimingNs(0x193 + profileOffset, 0x1AC + profileOffset),
                    TrasNs = GetTimingNsFromComposite(0x195 + profileOffset, 0x194 + profileOffset, 7, 4),
                    TrcNs = GetTimingNsFromComposite(0x196 + profileOffset, 0x194 + profileOffset, 3, 4, ftbIndex: 0x1AB + profileOffset),
                    TfawNs = GetTimingNsFromComposite(0x19E + profileOffset, 0x19D + profileOffset, 3, 4),
                    TrrdShortNs = GetTimingNs(0x19F + profileOffset, 0x1AA + profileOffset),
                    TrrdLongNs = GetTimingNs(0x1A0 + profileOffset, 0x1A9 + profileOffset),
                    Voltage = DecodeXmpVoltage(0x189 + profileOffset),
                    DataRate = RoundDataRate(tck),
                    FrequencyMHz = tck > 0 ? 1000.0 / tck : 0
                };

                profiles.Add(profile);
            }

            return profiles;
        }

        private string FormatXmpSummary(XmpProfile profile)
        {
            if (profile.TckNs <= 0)
                return "—";

            double cl = TimingToCycles(profile.TaaNs, profile.TckNs);
            double trcd = TimingToCycles(profile.TrcdNs, profile.TckNs);
            double trp = TimingToCycles(profile.TrpNs, profile.TckNs);
            double tras = TimingToCycles(profile.TrasNs, profile.TckNs);
            string timings = $"{cl:0}-{trcd:0}-{trp:0}-{tras:0}";

            string freqText = profile.DataRate > 0
                ? $"{profile.DataRate} MT/s ({profile.FrequencyMHz:F0} MHz)"
                : $"{profile.FrequencyMHz:F0} MHz";

            string voltageText = profile.Voltage > 0 ? $"{profile.Voltage:F2} V" : "—";
            return $"{freqText} {timings} @ {voltageText}";
        }

        private double DecodeXmpVoltage(int offset)
        {
            if (!HasByte(offset))
                return 0;

            byte value = Data[offset];
            double integer = (value >> 7) & 0x1;
            double fraction = (value & 0x7F) / 100.0;
            return integer + fraction;
        }

        private bool HasXmpHeader()
        {
            if (Data.Length <= 386)
            {
                return false;
            }

            bool isXmp = Data[384] == (byte)'X' &&
                         Data[385] == (byte)'M' &&
                         Data[386] == (byte)'P';

            bool legacyOrder = Data[384] == 0x50 &&
                               Data[385] == 0x4D &&
                               Data[386] == 0x58;

            return isXmp || legacyOrder;
        }

        private bool HasByte(int index) => index >= 0 && index < Data.Length;

        private static double TimingToCycles(double timingNs, double tckNs)
        {
            if (timingNs <= 0 || tckNs <= 0)
                return 0;
            return Math.Ceiling(timingNs / tckNs);
        }

        private IEnumerable<(int Offset, string Text)> EnumerateAsciiStrings(int minLength, int startOffset = 0)
        {
            var builder = new StringBuilder();
            int currentStart = -1;
            int limit = Data.Length;

            for (int i = Math.Max(0, startOffset); i < limit; i++)
            {
                byte value = Data[i];
                if (value >= 0x20 && value <= 0x7E)
                {
                    if (builder.Length == 0)
                    {
                        currentStart = i;
                    }

                    builder.Append((char)value);
                }
                else
                {
                    if (builder.Length >= minLength && currentStart >= 0)
                    {
                        yield return (currentStart, builder.ToString());
                    }

                    builder.Clear();
                    currentStart = -1;
                }
            }

            if (builder.Length >= minLength && currentStart >= 0)
            {
                yield return (currentStart, builder.ToString());
            }
        }

        private static string? NormalizeAsciiToken(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            string trimmed = text.Trim();
            int breakIndex = trimmed.IndexOfAny(new[] { ' ', '\t', '\r', '\n', ',', ';', ')', '(' });
            if (breakIndex > 0)
            {
                trimmed = trimmed[..breakIndex];
            }

            trimmed = trimmed.Trim('-', '.', '+');

            if (trimmed.Length < 4 || trimmed.Length > 40)
                return null;

            return trimmed;
        }

        private sealed class XmpProfile
        {
            public string Label { get; init; } = string.Empty;
            public double TckNs { get; init; }
            public double TaaNs { get; init; }
            public double TrcdNs { get; init; }
            public double TrpNs { get; init; }
            public double TrasNs { get; init; }
            public double TrcNs { get; init; }
            public double TfawNs { get; init; }
            public double TrrdShortNs { get; init; }
            public double TrrdLongNs { get; init; }
            public double TwrNs { get; init; }
            public double TwtrsNs { get; init; }
            public double Voltage { get; init; }
            public int DataRate { get; init; }
            public double FrequencyMHz { get; init; }
        }
        #endregion
    }
}

