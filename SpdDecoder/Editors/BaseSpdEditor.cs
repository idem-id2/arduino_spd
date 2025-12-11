using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HexEditor.Database;

namespace HexEditor.SpdDecoder
{
    /// <summary>
    /// Базовый класс для редактирования SPD данных
    /// </summary>
    internal abstract class BaseSpdEditor : ISpdEditor
    {
        protected byte[]? Data;

        protected BaseSpdEditor()
        {
        }

        public virtual void LoadData(byte[] data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public abstract List<EditField> GetEditFields();
        public abstract List<SpdEditPanel.ByteChange> ApplyChanges(Dictionary<string, string> fieldValues);
        public abstract Dictionary<string, string> ValidateFields(Dictionary<string, string> fieldValues);

        /// <summary>
        /// Создает общие поля для всех типов SPD
        /// </summary>
        protected List<EditField> CreateCommonFields()
        {
            if (Data == null || Data.Length < 256)
                return new List<EditField>();

            var fields = new List<EditField>();

            // Module Manufacturer (bytes 320-321)
            if (Data.Length > 321)
            {
                ushort manufacturerId = (ushort)((Data[320] << 8) | Data[321]);
                string manufacturerIdHex = manufacturerId.ToString("X4");
                
                // Загружаем список производителей для ComboBox
                var manufacturerItems = new List<ComboBoxItem>();
                var manufacturers = ManufacturerDatabase.GetManufacturerComboBoxItems();
                foreach ((string displayText, string idHex) in manufacturers)
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
                    string currentName = ManufacturerDatabase.GetManufacturerName(Data[320], Data[321]);
                    manufacturerItems.Insert(0, new ComboBoxItem
                    {
                        Content = $"{currentName} (0x{manufacturerIdHex})",
                        Tag = manufacturerIdHex
                    });
                }
                
                fields.Add(new EditField
                {
                    Id = "ModuleManufacturer",
                    Label = "Manufacturer",
                    Value = manufacturerIdHex,
                    Type = EditFieldType.ComboBox,
                    ComboBoxItems = manufacturerItems,
                    ToolTip = "Bytes 320-321: JEDEC Manufacturer ID",
                    Category = "MemoryModule"
                });
            }

            // Module Part Number (bytes 329-348)
            // Всегда 20 байт, оставшие байты заполняются пробелами
            if (Data.Length > 348)
            {
                string partNumber = ReadAsciiString(329, 348);
                // Обрезаем завершающие пробелы для отображения в UI (чтобы можно было редактировать)
                // При сохранении пробелы будут автоматически дополнены до 20 байт
                partNumber = partNumber.TrimEnd();
                fields.Add(new EditField
                {
                    Id = "ModulePartNumber",
                    Label = "Part Number",
                    Value = partNumber,
                    Type = EditFieldType.TextBox,
                    MaxLength = 20,
                    ToolTip = "Bytes 329-348: Module Part Number (ASCII, 20 bytes, padded with spaces)",
                    Category = "MemoryModule"
                });
            }

            // Module Serial Number (bytes 325-328)
            // По стандарту JEDEC: только HEX данные (4 байта = 8 hex символов)
            if (Data.Length > 328)
            {
                string serial = ReadSerialNumber(325, 328);
                fields.Add(new EditField
                {
                    Id = "ModuleSerialNumber",
                    Label = "Serial Number",
                    Value = serial,
                    Type = EditFieldType.TextBox,
                    MaxLength = 8,
                    ToolTip = "Bytes 325-328: Serial Number (HEX only, 4 bytes = 8 hex characters, JEDEC standard)",
                    Category = "MemoryModule"
                });
            }

            // Manufacturing Date (bytes 323-324)
            if (Data.Length > 324)
            {
                byte yearByte = Data[323];
                byte weekByte = Data[324];
                int year = 0, week = 0;
                
                if (yearByte != 0 && yearByte != 0xFF)
                {
                    year = ((yearByte >> 4) & 0x0F) * 10 + (yearByte & 0x0F);
                }
                if (weekByte != 0 && weekByte != 0xFF)
                {
                    week = ((weekByte >> 4) & 0x0F) * 10 + (weekByte & 0x0F);
                }

                // Создаем списки для ComboBox
                var yearItems = new List<ComboBoxItem>();
                for (int y = 0; y <= 99; y++)
                {
                    int fullYear = 2000 + y;
                    yearItems.Add(new ComboBoxItem
                    {
                        Content = $"{fullYear} ({y:D2})",
                        Tag = y.ToString("D2")
                    });
                }

                var weekItems = new List<ComboBoxItem>();
                for (int w = 1; w <= 52; w++)
                {
                    weekItems.Add(new ComboBoxItem
                    {
                        Content = $"Week {w:D2}",
                        Tag = w.ToString("D2")
                    });
                }

                // Year field
                fields.Add(new EditField
                {
                    Id = "ModuleYear",
                    Label = "Manufacturing Date",
                    Value = year > 0 ? year.ToString("D2") : "",
                    Type = EditFieldType.ComboBox,
                    ComboBoxItems = yearItems,
                    ToolTip = "Byte 323: Year (BCD, 00-99 = 2000-2099)",
                    Category = "MemoryModule"
                });

                // Week field (будет объединен с Year в одну строку в UI)
                fields.Add(new EditField
                {
                    Id = "ModuleWeek",
                    Label = "", // Пустой label, так как будет в той же строке
                    Value = week > 0 ? week.ToString("D2") : "",
                    Type = EditFieldType.ComboBox,
                    ComboBoxItems = weekItems,
                    ToolTip = "Byte 324: Week (BCD, 01-52)",
                    Category = "MemoryModule"
                });
            }

            // Manufacturing Location (byte 322)
            if (Data.Length > 322)
            {
                fields.Add(new EditField
                {
                    Id = "ModuleLocation",
                    Label = "Manufacturing Location",
                    Value = $"0x{Data[322]:X2}",
                    Type = EditFieldType.TextBox,
                    MaxLength = 2,
                    ToolTip = "Byte 322: Location Code (hex, 0x01-0x10)",
                    Category = "MemoryModule"
                });
            }

            // Module Type (byte 3)
            if (Data.Length > 3)
            {
                byte moduleType = (byte)(Data[3] & 0x0F);
                var moduleTypeItems = new List<ComboBoxItem>
                {
                    new() { Content = "Extended DIMM", Tag = "0x00" },
                    new() { Content = "RDIMM", Tag = "0x01" },
                    new() { Content = "UDIMM", Tag = "0x02" },
                    new() { Content = "SO-DIMM", Tag = "0x03" },
                    new() { Content = "LRDIMM", Tag = "0x04" },
                    new() { Content = "Mini-RDIMM", Tag = "0x05" },
                    new() { Content = "Mini-UDIMM", Tag = "0x06" }
                };

                fields.Add(new EditField
                {
                    Id = "ModuleType",
                    Label = "Module Type",
                    Value = $"0x{moduleType:X2}",
                    Type = EditFieldType.ComboBox,
                    ComboBoxItems = moduleTypeItems,
                    ToolTip = "Byte 3: Module Type",
                    Category = "MemoryModule"
                });
            }

            // SPD Revision (byte 1)
            if (Data.Length > 1)
            {
                byte revision = Data[1];
                int major = (revision >> 4) & 0x0F;
                int minor = revision & 0x0F;

                fields.Add(new EditField
                {
                    Id = "SpdRevisionMajor",
                    Label = "SPD Revision (Major)",
                    Value = major.ToString("X"),
                    Type = EditFieldType.TextBox,
                    MaxLength = 1,
                    ToolTip = "Byte 1, bits 7-4: Major revision (0-F)",
                    Category = "MemoryModule"
                });

                fields.Add(new EditField
                {
                    Id = "SpdRevisionMinor",
                    Label = "SPD Revision (Minor)",
                    Value = minor.ToString("X"),
                    Type = EditFieldType.TextBox,
                    MaxLength = 1,
                    ToolTip = "Byte 1, bits 3-0: Minor revision (0-F)",
                    Category = "MemoryModule"
                });
            }

            // Memory Type (byte 2) - read-only
            if (Data.Length > 2)
            {
                byte memoryType = Data[2];
                var memoryTypeItems = new List<ComboBoxItem>
                {
                    new() { Content = "DDR4 SDRAM", Tag = "0x0C" },
                    new() { Content = "DDR5 SDRAM", Tag = "0x12" }
                };

                fields.Add(new EditField
                {
                    Id = "MemoryType",
                    Label = "Memory Type",
                    Value = $"0x{memoryType:X2}",
                    Type = EditFieldType.ComboBox,
                    IsReadOnly = true,
                    ComboBoxItems = memoryTypeItems,
                    ToolTip = "Byte 2: Memory Type (read-only)",
                    Category = "MemoryModule"
                });
            }

            return fields;
        }

        /// <summary>
        /// Применяет общие изменения
        /// </summary>
        protected List<SpdEditPanel.ByteChange> ApplyCommonChanges(Dictionary<string, string> fieldValues)
        {
            if (Data == null)
                return new List<SpdEditPanel.ByteChange>();

            var changes = new List<SpdEditPanel.ByteChange>();

            // Module Manufacturer (bytes 320-321)
            if (fieldValues.TryGetValue("ModuleManufacturer", out string? manufacturerText) &&
                !string.IsNullOrWhiteSpace(manufacturerText))
            {
                // manufacturerText из ComboBox - hex строка (например, "80AD" или "0x80AD")
                string hexString = manufacturerText.Trim();
                if (hexString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    hexString = hexString.Substring(2);
                }
                
                if (ushort.TryParse(hexString, System.Globalization.NumberStyles.HexNumber, null, out ushort manufacturerId))
                {
                    // Сохраняем байты с parity битами (как в JEDEC стандарте)
                    byte newByte320 = (byte)(manufacturerId >> 8);
                    byte newByte321 = (byte)(manufacturerId & 0xFF);
                    if (Data[320] != newByte320)
                    {
                        Data[320] = newByte320;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 320, NewData = new[] { newByte320 } });
                    }
                    if (Data[321] != newByte321)
                    {
                        Data[321] = newByte321;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 321, NewData = new[] { newByte321 } });
                    }
                }
            }

            // Module Part Number (bytes 329-348)
            if (fieldValues.TryGetValue("ModulePartNumber", out string? partNumber) && Data.Length > 348)
            {
                byte[] oldPartNumber = new byte[20];
                Array.Copy(Data, 329, oldPartNumber, 0, 20);
                WriteAsciiString(Data, 329, 348, partNumber);
                byte[] newPartNumber = new byte[20];
                Array.Copy(Data, 329, newPartNumber, 0, 20);
                if (!oldPartNumber.SequenceEqual(newPartNumber))
                {
                    changes.Add(new SpdEditPanel.ByteChange { Offset = 329, NewData = newPartNumber });
                }
            }

            // Module Serial Number (bytes 325-328)
            if (fieldValues.TryGetValue("ModuleSerialNumber", out string? serial) && Data.Length > 328)
            {
                byte[] oldSerial = new byte[4];
                Array.Copy(Data, 325, oldSerial, 0, 4);
                WriteSerialNumber(Data, 325, 328, serial);
                byte[] newSerial = new byte[4];
                Array.Copy(Data, 325, newSerial, 0, 4);
                if (!oldSerial.SequenceEqual(newSerial))
                {
                    changes.Add(new SpdEditPanel.ByteChange { Offset = 325, NewData = newSerial });
                }
            }

            // Manufacturing Date (bytes 323-324)
            // JEDEC Standard: Byte 323 = Year (BCD), Byte 324 = Week (BCD)
            // Применяем Year и Week независимо, так как они могут редактироваться отдельно
            if (fieldValues.TryGetValue("ModuleYear", out string? yearText))
            {
                if (TryParseBcd(yearText, out byte yearByte))
                {
                    // Byte 323 = Year (BCD, 00-99 = 2000-2099)
                    if (Data.Length > 323 && Data[323] != yearByte)
                    {
                        Data[323] = yearByte;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 323, NewData = new[] { yearByte } });
                    }
                }
            }

            if (fieldValues.TryGetValue("ModuleWeek", out string? weekText))
            {
                if (TryParseBcd(weekText, out byte weekByte))
                {
                    // Byte 324 = Week (BCD, 01-52)
                    if (Data.Length > 324 && Data[324] != weekByte)
                    {
                        Data[324] = weekByte;
                        changes.Add(new SpdEditPanel.ByteChange { Offset = 324, NewData = new[] { weekByte } });
                    }
                }
            }

            // Manufacturing Location (byte 322)
            if (fieldValues.TryGetValue("ModuleLocation", out string? locationText) &&
                TryParseHex(locationText, out byte locationByte))
            {
                if (Data[322] != locationByte)
                {
                    Data[322] = locationByte;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = 322, NewData = new[] { locationByte } });
                }
            }

            // Module Type (byte 3)
            if (fieldValues.TryGetValue("ModuleType", out string? moduleTypeText) &&
                TryParseHex(moduleTypeText, out byte moduleType))
            {
                byte newByte3 = (byte)((Data[3] & 0xF0) | (moduleType & 0x0F));
                if (Data[3] != newByte3)
                {
                    Data[3] = newByte3;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = 3, NewData = new[] { newByte3 } });
                }
            }

            // SPD Revision (byte 1)
            if (fieldValues.TryGetValue("SpdRevisionMajor", out string? majorText) &&
                fieldValues.TryGetValue("SpdRevisionMinor", out string? minorText) &&
                TryParseHex(majorText, out byte major) &&
                TryParseHex(minorText, out byte minor))
            {
                byte newByte1 = (byte)((major << 4) | (minor & 0x0F));
                if (Data[1] != newByte1)
                {
                    Data[1] = newByte1;
                    changes.Add(new SpdEditPanel.ByteChange { Offset = 1, NewData = new[] { newByte1 } });
                }
            }

            return changes;
        }

        protected string ReadAsciiString(int start, int end)
        {
            if (Data == null || start >= Data.Length || end >= Data.Length)
                return "";

            var sb = new StringBuilder();
            for (int i = start; i <= end && i < Data.Length; i++)
            {
                byte b = Data[i];
                if (b == 0)
                    break;
                if (b >= 32 && b <= 126)
                    sb.Append((char)b);
            }
            // НЕ обрезаем пробелы - они должны сохраняться (как в примере DDR4XMPEditor)
            return sb.ToString();
        }

        protected string ReadSerialNumber(int start, int end)
        {
            if (Data == null || start >= Data.Length || end >= Data.Length)
                return "";

            // По стандарту JEDEC: Serial Number всегда HEX (4 байта = 8 hex символов)
            // Читаем байты и преобразуем в HEX строку
            var hexSb = new StringBuilder();
            for (int i = start; i <= end && i < Data.Length; i++)
            {
                hexSb.Append($"{Data[i]:X2}");
            }
            return hexSb.ToString();
        }

        protected void WriteAsciiString(byte[] data, int start, int end, string text)
        {
            if (text == null)
                text = "";

            // Вычисляем максимальный размер (всегда заполняем весь диапазон)
            int maxSize = end - start + 1;
            
            // Преобразуем middle dot обратно в пробелы (если они были заменены в UI)
            text = text.Replace('·', ' ');
            
            // Ограничиваем длину строки, если она превышает максимум
            string str = text.Length > maxSize ? text.Substring(0, maxSize) : text;
            
            // Дополняем пробелами до нужной длины (всегда заполняем все 20 байт для Part Number)
            if (str.Length < maxSize)
            {
                str = str.PadRight(maxSize, ' ');
            }

            // Записываем строку байт за байтом (всегда заполняем весь диапазон пробелами)
            for (int i = 0; i < maxSize && (start + i) < data.Length && (start + i) <= end; i++)
            {
                if (i < str.Length)
                {
                    data[start + i] = (byte)str[i];
                }
                else
                {
                    // Дополняем пробелами, если строка короче
                    data[start + i] = (byte)' ';
                }
            }
        }

        protected void WriteSerialNumber(byte[] data, int start, int end, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            // По стандарту JEDEC: Serial Number всегда HEX (4 байта = 8 hex символов)
            // Убираем пробелы и префиксы (0x, 0X)
            text = text.Trim().Replace(" ", "").Replace("0x", "").Replace("0X", "");
            
            // Проверяем, что это валидная HEX строка
            if (!Regex.IsMatch(text, @"^[0-9A-Fa-f]+$") || text.Length % 2 != 0)
            {
                // Невалидный HEX - не записываем
                return;
            }

            // Записываем HEX байты
            int byteCount = Math.Min(text.Length / 2, end - start + 1);
            for (int i = 0; i < byteCount && (start + i) < data.Length; i++)
            {
                string hexByte = text.Substring(i * 2, 2);
                if (byte.TryParse(hexByte, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value))
                {
                    data[start + i] = value;
                }
            }
            
            // Заполняем оставшиеся байты нулями (если HEX строка короче)
            for (int i = byteCount; i <= (end - start) && (start + i) < data.Length; i++)
            {
                data[start + i] = 0x00;
            }
        }

        protected bool TryParseHex(string text, out byte result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(2);
            }

            return byte.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
        }

        protected bool TryParseHex(string text, out ushort result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(2);
            }

            return ushort.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
        }

        protected bool TryParseBcd(string text, out byte result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (int.TryParse(text, out int value) && value >= 0 && value <= 99)
            {
                int tens = value / 10;
                int ones = value % 10;
                result = (byte)((tens << 4) | ones);
                return true;
            }

            return false;
        }
    }
}

