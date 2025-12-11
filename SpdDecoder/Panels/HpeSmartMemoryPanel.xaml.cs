using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HexEditor.SpdDecoder
{
    /// <summary>
    /// UserControl для декодирования и редактирования HPE SmartMemory Encryption ID
    /// </summary>
    public partial class HpeSmartMemoryPanel : UserControl
    {
        private byte[]? _currentSpdData;
        private uint? _currentSecureId;

        public HpeSmartMemoryPanel()
        {
            InitializeComponent();
            Log("HPE SmartMemory panel initialized");
        }

        /// <summary>
        /// Очистка панели
        /// </summary>
        public void Clear()
        {
            _currentSpdData = null;
            _currentSecureId = null;
            
            HptMarkerValue.Text = "—";
            SecureIdValue.Text = "—";
            EncryptionIdValue.Text = "—";
            MatchValue.Text = "—";
            MatchValue.Foreground = TryFindResource("PrimaryTextBrush") as System.Windows.Media.Brush;
            
            Log("Panel cleared");
        }

        /// <summary>
        /// Обновление SPD данных
        /// </summary>
        /// <param name="data">SPD данные</param>
        public void UpdateSpdData(byte[]? data)
        {
            _currentSpdData = data;
            
            if (data == null || data.Length < 0x188)
            {
                Clear();
                return;
            }

            // Проверка маркера HPT
            bool hasHpt = HpeEncryptionIdCalculator.CheckHptMarker(data);
            HptMarkerValue.Text = hasHpt ? "Found (HPT)" : "Not found";
            HptMarkerValue.Foreground = hasHpt 
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green)
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);

            // Чтение Secure ID
            _currentSecureId = HpeEncryptionIdCalculator.ReadSecureId(data);
            if (_currentSecureId.HasValue)
            {
                SecureIdValue.Text = $"0x{_currentSecureId.Value:X8}";
                Log($"Secure ID read from SPD: 0x{_currentSecureId.Value:X8}");
            }
            else
            {
                SecureIdValue.Text = "Not found";
                Log("Secure ID not found in SPD");
            }

            // Автоматический расчет, если есть Secure ID
            if (_currentSecureId.HasValue)
            {
                TryAutoCalculate();
            }
        }

        /// <summary>
        /// Попытка автоматического расчета с текущими значениями
        /// </summary>
        private void TryAutoCalculate()
        {
            if (_currentSpdData == null || !_currentSecureId.HasValue)
            {
                return;
            }

            if (TryGetSensorRegisters(out ushort reg6, out ushort reg7))
            {
                CalculateEncryptionId(reg6, reg7);
            }
        }

        /// <summary>
        /// Получение значений Sensor Registers из полей ввода
        /// </summary>
        private bool TryGetSensorRegisters(out ushort reg6, out ushort reg7)
        {
            reg6 = 0;
            reg7 = 0;

            try
            {
                string reg6Str = SensorReg6Value.Text.Trim();
                string reg7Str = SensorReg7Value.Text.Trim();

                if (string.IsNullOrEmpty(reg6Str) || string.IsNullOrEmpty(reg7Str))
                {
                    return false;
                }

                reg6 = Convert.ToUInt16(reg6Str, 16);
                reg7 = Convert.ToUInt16(reg7Str, 16);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Расчет Encryption ID
        /// </summary>
        private void CalculateEncryptionId(ushort sensorReg6, ushort sensorReg7)
        {
            if (_currentSpdData == null)
            {
                Log("ERROR: SPD data not loaded");
                return;
            }

            try
            {
                uint encryptionId = HpeEncryptionIdCalculator.CalculateEncryptionId(
                    _currentSpdData, sensorReg6, sensorReg7);

                EncryptionIdValue.Text = $"0x{encryptionId:X8}";
                Log($"Calculated Encryption ID: 0x{encryptionId:X8}");
                Log($"Sensor Register 6: 0x{sensorReg6:X4}");
                Log($"Sensor Register 7: 0x{sensorReg7:X4}");

                // Проверка совпадения с Secure ID
                if (_currentSecureId.HasValue)
                {
                    bool match = encryptionId == _currentSecureId.Value;
                    MatchValue.Text = match ? "MATCH ✓" : "NO MATCH ✗";
                    MatchValue.Foreground = match
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green)
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);

                    if (match)
                    {
                        Log("✓ MATCH! Encryption ID equals Secure ID");
                    }
                    else
                    {
                        uint diff = encryptionId ^ _currentSecureId.Value;
                        Log($"✗ NO MATCH. Difference: 0x{diff:X8}");
                    }
                }
                else
                {
                    MatchValue.Text = "Secure ID not available";
                    MatchValue.Foreground = TryFindResource("PrimaryTextBrush") as System.Windows.Media.Brush;
                }
            }
            catch (Exception ex)
            {
                Log($"ERROR: Calculation failed: {ex.Message}");
                EncryptionIdValue.Text = "ERROR";
                MatchValue.Text = "ERROR";
            }
        }

        #region Event Handlers

        private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                string[] parts = tag.Split(',');
                if (parts.Length == 2)
                {
                    SensorReg6Value.Text = parts[0].Trim();
                    SensorReg7Value.Text = parts[1].Trim();
                    Log($"Preset selected: {item.Content}");
                    TryAutoCalculate();
                }
            }
        }

        private void SensorReg_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем только HEX символы
            e.Handled = !IsHexChar(e.Text);
        }

        private void SensorReg_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Обработка Ctrl+V для вставки HEX
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (sender is TextBox textBox)
                {
                    e.Handled = true;
                    string clipboardText = Clipboard.GetText();
                    string hexOnly = FilterHexString(clipboardText);
                    if (!string.IsNullOrEmpty(hexOnly))
                    {
                        int maxLength = 4; // 4 hex символа для 16-битного значения
                        if (hexOnly.Length > maxLength)
                        {
                            hexOnly = hexOnly.Substring(0, maxLength);
                        }
                        textBox.Text = hexOnly.ToUpper();
                        textBox.CaretIndex = textBox.Text.Length;
                        TryAutoCalculate();
                    }
                }
            }
            else if (e.Key == Key.Enter)
            {
                // При нажатии Enter пересчитываем
                TryAutoCalculate();
            }
        }

        private void SensorReg6Value_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Автоматическое обновление при изменении текста (с задержкой)
            if (sender is TextBox textBox)
            {
                // Ограничиваем длину до 4 символов
                if (textBox.Text.Length > 4)
                {
                    textBox.Text = textBox.Text.Substring(0, 4);
                    textBox.CaretIndex = textBox.Text.Length;
                }
                
                // Преобразуем в верхний регистр
                if (textBox.Text != textBox.Text.ToUpper())
                {
                    int caretPos = textBox.CaretIndex;
                    textBox.Text = textBox.Text.ToUpper();
                    textBox.CaretIndex = Math.Min(caretPos, textBox.Text.Length);
                }
            }
        }

        private void SensorReg7Value_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Аналогично SensorReg6Value_TextChanged
            if (sender is TextBox textBox)
            {
                if (textBox.Text.Length > 4)
                {
                    textBox.Text = textBox.Text.Substring(0, 4);
                    textBox.CaretIndex = textBox.Text.Length;
                }
                
                if (textBox.Text != textBox.Text.ToUpper())
                {
                    int caretPos = textBox.CaretIndex;
                    textBox.Text = textBox.Text.ToUpper();
                    textBox.CaretIndex = Math.Min(caretPos, textBox.Text.Length);
                }
            }
        }

        private void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSensorRegisters(out ushort reg6, out ushort reg7))
            {
                Log("ERROR: Invalid Sensor Register values");
                MessageBox.Show("Please enter valid HEX values for Sensor Registers", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            CalculateEncryptionId(reg6, reg7);
        }

        private void AutoDetectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSpdData == null || !_currentSecureId.HasValue)
            {
                Log("ERROR: SPD data or Secure ID not available");
                MessageBox.Show("Please load SPD data first", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Log("Auto-detecting Sensor Registers...");
            var result = HpeEncryptionIdCalculator.AutoDetectSensorRegisters(
                _currentSpdData, _currentSecureId.Value);

            if (result.HasValue)
            {
                SensorReg6Value.Text = $"{result.Value.reg6:X4}";
                SensorReg7Value.Text = $"{result.Value.reg7:X4}";
                Log($"✓ Found: Reg6=0x{result.Value.reg6:X4}, Reg7=0x{result.Value.reg7:X4}");
                
                // Обновляем выбранный preset, если есть совпадение
                UpdatePresetSelection(result.Value.reg6, result.Value.reg7);
                
                CalculateEncryptionId(result.Value.reg6, result.Value.reg7);
            }
            else
            {
                Log("✗ No match found among known presets");
                MessageBox.Show("No matching Sensor Registers found among known presets", 
                    "Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
            Log("Log cleared");
        }

        #endregion

        #region Helper Methods

        private bool IsHexChar(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (char c in text)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')))
                {
                    return false;
                }
            }
            return true;
        }

        private string FilterHexString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            foreach (char c in input)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private void UpdatePresetSelection(ushort reg6, ushort reg7)
        {
            string reg6Str = $"{reg6:X4}";
            string reg7Str = $"{reg7:X4}";
            string tag = $"{reg6Str},{reg7Str}";

            foreach (ComboBoxItem item in PresetComboBox.Items)
            {
                if (item.Tag is string itemTag && itemTag == tag)
                {
                    PresetComboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogTextBox.AppendText($"[{timestamp}] {message}\n");
            LogTextBox.ScrollToEnd();
        }

        #endregion
    }
}

