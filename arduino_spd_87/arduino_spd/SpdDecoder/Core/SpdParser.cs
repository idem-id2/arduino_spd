using System;
using System.Collections.Generic;
using System.Text;

namespace HexEditor.SpdDecoder
{
    public class SpdParser
    {
        private readonly byte[] _data;

        public List<SpdInfoPanel.InfoItem> ModuleInfo { get; } = new();
        public List<SpdInfoPanel.InfoItem> DramInfo { get; } = new();
        public List<SpdInfoPanel.TimingRow> TimingRows { get; } = new();

        public SpdParser(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public void Parse(ForcedMemoryType forcedType = ForcedMemoryType.Auto)
        {
            ModuleInfo.Clear();
            DramInfo.Clear();
            TimingRows.Clear();

            if (_data.Length < 256)
            {
                ModuleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "SPD",
                    Value = "Недостаточно данных для анализа SPD."
                });
                return;
            }

            ISpdDecoder? decoder = SpdTypeDetector.CreateDecoder(_data, forcedType);

            if (decoder != null)
            {
                decoder.Populate(ModuleInfo, DramInfo, TimingRows);
            }
            else
            {
                byte memoryType = _data.Length > 2 ? _data[2] : (byte)0;
                PopulateGenericInfo(memoryType);
            }
        }

        private void PopulateGenericInfo(byte memoryType)
        {
            if (memoryType == 0x0B) // DDR3 fallback (упрощенный)
            {
                string manufacturer = GetManufacturerNameSafe(117, 118);
                string partNumber = ReadAsciiSafe(73, 90);
                string serialNumber = ReadSerialSafe(95, 98);
                string manufacturingDate = ReadDateSafe(93, 94);

                ModuleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Manufacturer, Value = manufacturer });
                ModuleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.PartNumber, Value = partNumber });
                ModuleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.SerialNumber, Value = serialNumber });
                ModuleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.ManufacturingDate, Value = manufacturingDate });
                ModuleInfo.Add(new SpdInfoPanel.InfoItem { Label = SpdInfoPanel.FieldLabels.Architecture, Value = "DDR3 SDRAM" });
            }
            else
            {
                ModuleInfo.Add(new SpdInfoPanel.InfoItem
                {
                    Label = "SPD",
                    Value = $"Тип памяти 0x{memoryType:X2} пока не поддерживается."
                });
            }
        }

        private string GetManufacturerNameSafe(int lsbOffset, int msbOffset)
        {
            byte lsb = lsbOffset < _data.Length ? _data[lsbOffset] : (byte)0;
            byte msb = msbOffset < _data.Length ? _data[msbOffset] : (byte)0;
            // Pass bytes as-is (with parity bits) to match JEDEC standard format
            return HexEditor.Database.ManufacturerDatabase.GetManufacturerName(lsb, msb);
        }

        private string ReadAsciiSafe(int start, int end)
        {
            if (start >= _data.Length || end >= _data.Length)
                return "—";

            var chars = new System.Text.StringBuilder();
            for (int i = start; i <= end && i < _data.Length; i++)
            {
                if (_data[i] == 0) break;
                chars.Append((char)_data[i]);
            }
            return chars.ToString().Trim();
        }

        private string ReadSerialSafe(int start, int end)
        {
            if (start >= _data.Length || end >= _data.Length)
                return "—";

            var sb = new System.Text.StringBuilder();
            for (int i = start; i <= end && i < _data.Length; i++)
            {
                sb.Append($"{_data[i]:X2}");
            }
            return sb.ToString();
        }

        private string ReadDateSafe(int yearOffset, int weekOffset)
        {
            if (yearOffset >= _data.Length || weekOffset >= _data.Length)
                return "—";

            byte year = _data[yearOffset];
            byte week = _data[weekOffset];

            if (year == 0 && week == 0)
                return "—";

            int actualYear = 2000 + year;
            return $"Week {week}, {actualYear}";
        }

    }
}
