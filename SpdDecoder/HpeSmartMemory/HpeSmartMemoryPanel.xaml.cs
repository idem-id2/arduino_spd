using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace HexEditor.SpdDecoder.HpeSmartMemory
{
    /// <summary>
    /// UserControl для декодирования и редактирования HPE SmartMemory Encryption ID
    /// </summary>
    public partial class HpeSmartMemoryPanel : UserControl
    {
        // Адреса для HPE Part Number
        private const int HPE_PART_NUMBER_START = 0x192;
        private const int HPE_PART_NUMBER_END = 0x19A;
        private const int HPE_PART_NUMBER_LENGTH = 9;

        public HpeSmartMemoryPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Очистка панели
        /// </summary>
        public void Clear()
        {
            if (HptMarkerValue != null) HptMarkerValue.Text = "—";
            if (SecureIdValue != null) SecureIdValue.Text = "—";
            if (FindName("HpePartNumberValue") is TextBox hpePartNumber)
            {
                hpePartNumber.Text = "—";
            }
            
            // Очистка Sensor Registers
            if (FindName("ModeValue") is TextBox modeValue)
            {
                modeValue.Text = "—";
            }
            if (FindName("SensorReg6Value") is TextBox reg6Value)
            {
                reg6Value.Text = "—";
            }
            if (FindName("SensorReg7Value") is TextBox reg7Value)
            {
                reg7Value.Text = "—";
            }
        }

        /// <summary>
        /// Обновление SPD данных
        /// </summary>
        /// <param name="data">SPD данные</param>
        /// <param name="isArduinoMode">Режим работы: true - Arduino, false - Автономный</param>
        /// <param name="sensorReg6">Sensor Register 6 (опционально)</param>
        /// <param name="sensorReg7">Sensor Register 7 (опционально)</param>
        public void UpdateSpdData(byte[]? data, bool isArduinoMode = false, ushort? sensorReg6 = null, ushort? sensorReg7 = null)
        {
            if (data == null || data.Length < HPE_PART_NUMBER_END + 1)
            {
                Clear();
                return;
            }

            // 1. Проверка маркера HPT
            bool hasHpt = HpeEncryptionIdCalculator.CheckHptMarker(data);
            HptMarkerValue.Text = hasHpt ? "YES" : "NO";
            HptMarkerValue.Foreground = hasHpt
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green)
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);

            // 2. Проверка Secure ID
            uint? secureId = HpeEncryptionIdCalculator.ReadSecureId(data);
            SecureIdValue.Text = secureId.HasValue ? "YES" : "NO";
            SecureIdValue.Foreground = secureId.HasValue
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green)
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);

            // 3. Проверка HPE Part Number (0x192-0x19A, 9 байт)
            string hpePartNumber = ReadHpePartNumber(data);
            if (FindName("HpePartNumberValue") is TextBox hpePartNumberBox)
            {
                hpePartNumberBox.Text = !string.IsNullOrEmpty(hpePartNumber) ? hpePartNumber : "—";
            }
            
            // 4. Обновление Sensor Registers
            UpdateSensorRegisters(isArduinoMode, sensorReg6, sensorReg7);
        }

        /// <summary>
        /// Обновление блока Sensor Registers
        /// </summary>
        /// <param name="isArduinoMode">Режим работы: true - Arduino, false - Автономный</param>
        /// <param name="sensorReg6">Sensor Register 6 (опционально)</param>
        /// <param name="sensorReg7">Sensor Register 7 (опционально)</param>
        private void UpdateSensorRegisters(bool isArduinoMode, ushort? sensorReg6, ushort? sensorReg7)
        {
            // Режим работы
            if (FindName("ModeValue") is TextBox modeValue)
            {
                modeValue.Text = isArduinoMode ? "Arduino" : "Автономный";
            }
            
            // Sensor Register 6
            if (FindName("SensorReg6Value") is TextBox reg6Value)
            {
                if (sensorReg6.HasValue)
                {
                    reg6Value.Text = $"0x{sensorReg6.Value:X4}";
                }
                else
                {
                    reg6Value.Text = "—";
                }
            }
            
            // Sensor Register 7
            if (FindName("SensorReg7Value") is TextBox reg7Value)
            {
                if (sensorReg7.HasValue)
                {
                    reg7Value.Text = $"0x{sensorReg7.Value:X4}";
                }
                else
                {
                    reg7Value.Text = "—";
                }
            }
        }

        /// <summary>
        /// Чтение HPE Part Number из SPD данных
        /// </summary>
        /// <param name="spdData">SPD данные</param>
        /// <returns>HPE Part Number в ASCII формате или пустая строка</returns>
        private string ReadHpePartNumber(byte[] spdData)
        {
            if (spdData == null || spdData.Length < HPE_PART_NUMBER_END + 1)
            {
                return string.Empty;
            }

            try
            {
                // Читаем 9 байт начиная с адреса 0x192
                byte[] partNumberBytes = new byte[HPE_PART_NUMBER_LENGTH];
                Array.Copy(spdData, HPE_PART_NUMBER_START, partNumberBytes, 0, HPE_PART_NUMBER_LENGTH);

                // Проверяем, что все байты являются ASCII печатными символами (32-126)
                bool isValid = true;
                foreach (byte b in partNumberBytes)
                {
                    if (b < 32 || b > 126)
                    {
                        isValid = false;
                        break;
                    }
                }

                if (isValid)
                {
                    // Конвертируем в ASCII строку
                    string partNumber = Encoding.ASCII.GetString(partNumberBytes);
                    // Убираем trailing spaces
                    return partNumber.TrimEnd();
                }
                else
                {
                    return string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
