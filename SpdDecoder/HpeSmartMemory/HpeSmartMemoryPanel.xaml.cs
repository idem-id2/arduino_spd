using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;

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
        
        // Адреса для Secure ID
        private const int SPD_SECURE_ID_START = 0x184;
        private const int SPD_SECURE_ID_END = 0x188;
        
        // Текущие SPD данные
        private byte[]? _currentSpdData;
        
        // Текущие значения Sensor Registers
        private ushort? _currentSensorReg6;
        private ushort? _currentSensorReg7;
        
        // Текущие значения Encryption ID
        private uint? _currentSecureIdFromSpd;
        private uint? _currentCalculatedEncryptionId;
        
        /// <summary>
        /// Структура для передачи изменений байтов
        /// </summary>
        public class ByteChange
        {
            public int Offset { get; set; }
            public byte[] NewData { get; set; } = Array.Empty<byte>();
        }
        
        /// <summary>
        /// Событие, возникающее при применении изменений
        /// </summary>
        public event Action<List<ByteChange>>? ChangesApplied;

        public HpeSmartMemoryPanel()
        {
            InitializeComponent();
            
            // Инициализация ComboBox - выбор первого элемента (пустое значение)
            if (PresetComboBox != null && PresetComboBox.Items.Count > 0)
            {
                PresetComboBox.SelectedIndex = 0;
            }
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
            
            // Очистка Encryption ID
            if (FindName("SecureIdFromSpdValue") is TextBox secureIdFromSpdValue)
            {
                secureIdFromSpdValue.Text = "—";
            }
            if (FindName("CalculatedEncryptionIdValue") is TextBox calculatedEncryptionIdValue)
            {
                calculatedEncryptionIdValue.Text = "—";
            }
            if (FindName("SecureIdMatchValue") is TextBox secureIdMatchValue)
            {
                secureIdMatchValue.Text = "—";
            }
            if (FindName("CalculatedIdMatchValue") is TextBox calculatedIdMatchValue)
            {
                calculatedIdMatchValue.Text = "—";
            }
            
            _currentSpdData = null;
            _currentSensorReg6 = null;
            _currentSensorReg7 = null;
            _currentSecureIdFromSpd = null;
            _currentCalculatedEncryptionId = null;
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

            // Сохраняем текущие данные
            _currentSpdData = new byte[data.Length];
            Array.Copy(data, _currentSpdData, data.Length);
            
            // Обновляем Sensor Registers только если они переданы явно
            // Если не переданы, сохраняем текущие значения (установленные через пресет)
            if (sensorReg6.HasValue)
            {
                _currentSensorReg6 = sensorReg6;
            }
            if (sensorReg7.HasValue)
            {
                _currentSensorReg7 = sensorReg7;
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
            
            // 4. Обновление Sensor Registers (используем сохраненные значения, если они есть)
            UpdateSensorRegisters(isArduinoMode, _currentSensorReg6, _currentSensorReg7);
            
            // 5. Обновление Encryption ID блока
            UpdateEncryptionIdBlock(data, secureId);
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
        /// Обработчик изменения выбора пресета
        /// </summary>
        private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetComboBox?.SelectedItem == null)
            {
                return;
            }

            // Получаем Tag из выбранного элемента
            string? tagValue = null;
            
            if (PresetComboBox.SelectedItem is ComboBoxItem item)
            {
                tagValue = item.Tag?.ToString();
            }
            else
            {
                // Fallback: пытаемся получить через Content или другие способы
                var selectedItem = PresetComboBox.SelectedItem;
                var tagProperty = selectedItem.GetType().GetProperty("Tag");
                if (tagProperty != null)
                {
                    tagValue = tagProperty.GetValue(selectedItem)?.ToString();
                }
            }

            if (string.IsNullOrEmpty(tagValue))
            {
                // Пустое значение - очищаем регистры
                UpdateSensorRegistersFromPreset(null, null);
                return;
            }

            // Парсим Tag в формате "Reg6,Reg7" (например, "1C85,2221")
            string[] parts = tagValue.Split(',');
            if (parts.Length == 2)
            {
                try
                {
                    // Конвертируем HEX строки в ushort
                    ushort reg6 = ushort.Parse(parts[0].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    ushort reg7 = ushort.Parse(parts[1].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    
                    UpdateSensorRegistersFromPreset(reg6, reg7);
                }
                catch (Exception ex)
                {
                    // В случае ошибки парсинга просто очищаем
                    System.Diagnostics.Debug.WriteLine($"Error parsing preset values: {ex.Message}");
                    UpdateSensorRegistersFromPreset(null, null);
                }
            }
            else
            {
                UpdateSensorRegistersFromPreset(null, null);
            }
        }

        /// <summary>
        /// Обновление Sensor Registers из выбранного пресета
        /// </summary>
        private void UpdateSensorRegistersFromPreset(ushort? reg6, ushort? reg7)
        {
            // Сохраняем значения для использования в расчете
            _currentSensorReg6 = reg6;
            _currentSensorReg7 = reg7;
            
            if (FindName("SensorReg6Value") is TextBox reg6Value)
            {
                if (reg6.HasValue)
                {
                    reg6Value.Text = $"0x{reg6.Value:X4}";
                }
                else
                {
                    reg6Value.Text = "—";
                }
            }
            
            if (FindName("SensorReg7Value") is TextBox reg7Value)
            {
                if (reg7.HasValue)
                {
                    reg7Value.Text = $"0x{reg7.Value:X4}";
                }
                else
                {
                    reg7Value.Text = "—";
                }
            }
        }
        
        /// <summary>
        /// Обновление блока Encryption ID
        /// </summary>
        private void UpdateEncryptionIdBlock(byte[] spdData, uint? secureIdFromSpd)
        {
            _currentSecureIdFromSpd = secureIdFromSpd;
            
            // Отображаем Secure ID из SPD
            if (FindName("SecureIdFromSpdValue") is TextBox secureIdFromSpdValue)
            {
                if (secureIdFromSpd.HasValue)
                {
                    secureIdFromSpdValue.Text = $"0x{secureIdFromSpd.Value:X8}";
                }
                else
                {
                    secureIdFromSpdValue.Text = "—";
                }
            }
            
            // Если есть рассчитанный Encryption ID, проверяем совпадение
            if (_currentCalculatedEncryptionId.HasValue && secureIdFromSpd.HasValue)
            {
                bool isMatch = _currentCalculatedEncryptionId.Value == secureIdFromSpd.Value;
                UpdateMatchIndicators(isMatch);
            }
            else
            {
                // Очищаем индикаторы совпадения
                if (FindName("SecureIdMatchValue") is TextBox secureIdMatchValue)
                {
                    secureIdMatchValue.Text = "—";
                }
                if (FindName("CalculatedIdMatchValue") is TextBox calculatedIdMatchValue)
                {
                    calculatedIdMatchValue.Text = "—";
                }
            }
        }
        
        /// <summary>
        /// Обновление индикаторов совпадения
        /// </summary>
        private void UpdateMatchIndicators(bool isMatch)
        {
            var matchText = isMatch ? "YES" : "NO";
            var matchColor = isMatch 
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green)
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            
            if (FindName("SecureIdMatchValue") is TextBox secureIdMatchValue)
            {
                secureIdMatchValue.Text = matchText;
                secureIdMatchValue.Foreground = matchColor;
            }
            
            if (FindName("CalculatedIdMatchValue") is TextBox calculatedIdMatchValue)
            {
                calculatedIdMatchValue.Text = matchText;
                calculatedIdMatchValue.Foreground = matchColor;
            }
        }
        
        /// <summary>
        /// Обработчик кнопки "Рассчитать"
        /// </summary>
        private void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSpdData == null)
            {
                MessageBox.Show("SPD данные не загружены.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (!_currentSensorReg6.HasValue || !_currentSensorReg7.HasValue)
            {
                MessageBox.Show("Необходимо выбрать модель термодатчика (Sensor Registers).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                // Рассчитываем Encryption ID
                uint calculatedId = HpeEncryptionIdCalculator.CalculateEncryptionId(
                    _currentSpdData, 
                    _currentSensorReg6.Value, 
                    _currentSensorReg7.Value);
                
                _currentCalculatedEncryptionId = calculatedId;
                
                // Отображаем рассчитанный Encryption ID
                if (FindName("CalculatedEncryptionIdValue") is TextBox calculatedEncryptionIdValue)
                {
                    calculatedEncryptionIdValue.Text = $"0x{calculatedId:X8}";
                }
                
                // Сравниваем с Secure ID из SPD
                if (_currentSecureIdFromSpd.HasValue)
                {
                    bool isMatch = calculatedId == _currentSecureIdFromSpd.Value;
                    UpdateMatchIndicators(isMatch);
                }
                else
                {
                    // Secure ID отсутствует в SPD
                    if (FindName("SecureIdMatchValue") is TextBox secureIdMatchValue)
                    {
                        secureIdMatchValue.Text = "—";
                    }
                    if (FindName("CalculatedIdMatchValue") is TextBox calculatedIdMatchValue)
                    {
                        calculatedIdMatchValue.Text = "—";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при расчете Encryption ID: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Обработчик кнопки "Прочитать регистры"
        /// </summary>
        private void ReadRegistersButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Реализовать чтение регистров термодатчика через Arduino
            MessageBox.Show(
                "Функция чтения регистров термодатчика через Arduino пока не реализована.\n\n" +
                "Эта функция будет доступна в будущих версиях.",
                "Информация",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        
        /// <summary>
        /// Обработчик кнопки "Подобрать автономно"
        /// </summary>
        private void AutoDetectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSpdData == null)
            {
                MessageBox.Show("SPD данные не загружены.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Читаем Secure ID из SPD
            uint? secureIdFromSpd = HpeEncryptionIdCalculator.ReadSecureId(_currentSpdData);
            if (!secureIdFromSpd.HasValue)
            {
                MessageBox.Show(
                    "Secure ID не найден в SPD данных.\n\n" +
                    "Автоматическое определение требует наличия валидного Secure ID.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                // Выполняем автоматическое определение
                var result = HpeEncryptionIdCalculator.AutoDetectSensorRegisters(
                    _currentSpdData,
                    secureIdFromSpd.Value,
                    (presetName, reg6, reg7, encryptionId, secureId, isMatch) =>
                    {
                        // Логирование процесса (можно добавить в лог, если нужно)
                        System.Diagnostics.Debug.WriteLine(
                            $"[AutoDetect] {presetName}: Reg6=0x{reg6:X4}, Reg7=0x{reg7:X4}, " +
                            $"EncryptionID=0x{encryptionId:X8}, SecureID=0x{secureId:X8}, Match={isMatch}");
                    });
                
                if (result != null)
                {
                    // Найдено совпадение!
                    // Обновляем значения Sensor Registers
                    _currentSensorReg6 = result.Reg6;
                    _currentSensorReg7 = result.Reg7;
                    
                    // Обновляем ComboBox - выбираем соответствующий пресет
                    if (PresetComboBox != null)
                    {
                        foreach (var item in PresetComboBox.Items)
                        {
                            if (item is ComboBoxItem comboItem)
                            {
                                string? tagValue = comboItem.Tag?.ToString();
                                if (!string.IsNullOrEmpty(tagValue))
                                {
                                    string[] parts = tagValue.Split(',');
                                    if (parts.Length == 2)
                                    {
                                        try
                                        {
                                            ushort reg6 = ushort.Parse(parts[0].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                                            ushort reg7 = ushort.Parse(parts[1].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                                            
                                            if (reg6 == result.Reg6 && reg7 == result.Reg7)
                                            {
                                                PresetComboBox.SelectedItem = comboItem;
                                                break;
                                            }
                                        }
                                        catch
                                        {
                                            // Пропускаем некорректные значения
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    // Обновляем отображение Sensor Registers
                    UpdateSensorRegistersFromPreset(result.Reg6, result.Reg7);
                    
                    // Автоматически рассчитываем Encryption ID с найденными значениями
                    _currentCalculatedEncryptionId = result.CalculatedEncryptionId;
                    if (FindName("CalculatedEncryptionIdValue") is TextBox calculatedEncryptionIdValue)
                    {
                        calculatedEncryptionIdValue.Text = $"0x{result.CalculatedEncryptionId:X8}";
                    }
                    
                    // Обновляем индикаторы совпадения
                    UpdateMatchIndicators(true);
                    
                    MessageBox.Show(
                        $"Автоматическое определение выполнено успешно!\n\n" +
                        $"Модель: {result.PresetName}\n" +
                        $"Sensor Register 6: 0x{result.Reg6:X4}\n" +
                        $"Sensor Register 7: 0x{result.Reg7:X4}\n" +
                        $"Encryption ID: 0x{result.CalculatedEncryptionId:X8}\n\n" +
                        $"Значения совпадают с Secure ID из SPD.",
                        "Успех",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    // Совпадение не найдено
                    MessageBox.Show(
                        $"Автоматическое определение не дало результатов.\n\n" +
                        $"Проверены все известные модели термодатчиков, но ни одна не соответствует Secure ID из SPD.\n\n" +
                        $"Secure ID из SPD: 0x{secureIdFromSpd.Value:X8}\n\n" +
                        $"Возможные причины:\n" +
                        $"• Модель термодатчика не входит в список известных\n" +
                        $"• Secure ID в SPD данных некорректен\n" +
                        $"• SPD данные повреждены",
                        "Результат",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при автоматическом определении Sensor Registers:\n\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Обработчик кнопки "Fix Secure ID"
        /// </summary>
        private void FixSecureIdButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSpdData == null)
            {
                MessageBox.Show("SPD данные не загружены.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (!_currentCalculatedEncryptionId.HasValue)
            {
                MessageBox.Show("Необходимо сначала рассчитать Encryption ID.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Подтверждение действия
            var result = MessageBox.Show(
                $"Вы уверены, что хотите записать рассчитанный Encryption ID (0x{_currentCalculatedEncryptionId.Value:X8}) в Secure ID SPD?\n\n" +
                $"Это изменит байты по адресам 0x{SPD_SECURE_ID_START:X3}-0x{SPD_SECURE_ID_END - 1:X3}.",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
            
            try
            {
                // Конвертируем uint в байты (little-endian)
                byte[] secureIdBytes = BitConverter.GetBytes(_currentCalculatedEncryptionId.Value);
                
                // Создаем список изменений
                var changes = new List<ByteChange>
                {
                    new ByteChange
                    {
                        Offset = SPD_SECURE_ID_START,
                        NewData = secureIdBytes
                    }
                };
                
                // Вызываем событие для применения изменений
                ChangesApplied?.Invoke(changes);
                
                // Обновляем отображение Secure ID из SPD
                _currentSecureIdFromSpd = _currentCalculatedEncryptionId.Value;
                if (FindName("SecureIdFromSpdValue") is TextBox secureIdFromSpdValue)
                {
                    secureIdFromSpdValue.Text = $"0x{_currentCalculatedEncryptionId.Value:X8}";
                }
                
                // Обновляем индикаторы совпадения
                UpdateMatchIndicators(true);
                
                MessageBox.Show("Secure ID успешно обновлен в SPD данных.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при записи Secure ID: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
