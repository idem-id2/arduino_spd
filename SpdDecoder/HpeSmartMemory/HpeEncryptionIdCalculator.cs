using System;
using System.Linq;
using System.Text;

namespace HexEditor.SpdDecoder.HpeSmartMemory
{
    /// <summary>
    /// Калькулятор Encryption ID для HP SmartMemory модулей DDR4
    /// 
    /// Алгоритм основан на анализе BIOS HP (функции sub_232F05C, sub_232F0BC, sub_233260C).
    /// Используется для расчета и проверки Encryption ID модулей памяти HP SmartMemory.
    /// </summary>
    public static class HpeEncryptionIdCalculator
    {
        #region Константы

        /// <summary>
        /// Секретный ключ для расчета Encryption ID
        /// Строка из кода BIOS: "Copyright HP.  All rights reserved." (35 байт)
        /// </summary>
        private static readonly byte[] COPYRIGHT_STRING = Encoding.ASCII.GetBytes("Copyright HP.  All rights reserved.").Take(35).ToArray();

        /// <summary>
        /// Размер буфера v69 в байтах
        /// </summary>
        private const int BUFFER_V69_SIZE = 244;

        /// <summary>
        /// Диапазоны SPD данных для расчета
        /// </summary>
        private const int SPD_RANGE_1_START = 0x7E;  // Начало первого диапазона
        private const int SPD_RANGE_1_END = 0xFF;    // Конец первого диапазона
        private const int SPD_RANGE_1_SIZE = 130;    // Размер первого диапазона (0x7E-0xFF)
        private const int SPD_RANGE_2_START = 0x140; // Начало второго диапазона
        private const int SPD_RANGE_2_END = 0x180;  // Конец второго диапазона
        private const int SPD_RANGE_2_SIZE = 64;     // Размер второго диапазона (0x140-0x17F)

        /// <summary>
        /// Адреса в SPD для чтения данных
        /// </summary>
        private const int SPD_HPT_MARKER_START = 0x181; // Начало маркера HPT
        private const int SPD_HPT_MARKER_END = 0x184;   // Конец маркера HPT
        private const int SPD_SECURE_ID_START = 0x184;  // Начало Secure ID
        private const int SPD_SECURE_ID_END = 0x188;    // Конец Secure ID

        /// <summary>
        /// Параметры CRC32
        /// </summary>
        private const uint CRC32_POLYNOMIAL = 0xD5828281;      // Полином для CRC32 (MakeHpId)
        private const uint CRC32_INITIAL_VALUE = 0xFFFFFFFF;   // Начальное значение CRC32

        #endregion

        #region Публичные методы

        /// <summary>
        /// Проверка наличия маркера HPT в SPD данных
        /// 
        /// Маркер HPT (HP Technology) находится в SPD по адресам 0x181-0x183.
        /// Наличие этого маркера указывает на то, что модуль является HP SmartMemory.
        /// </summary>
        /// <param name="spdData">SPD данные модуля памяти</param>
        /// <returns>True, если маркер найден, False в противном случае</returns>
        public static bool CheckHptMarker(byte[] spdData)
        {
            if (spdData == null || spdData.Length <= SPD_HPT_MARKER_END - 1)
            {
                return false;
            }

            var hptMarker = spdData.Skip(SPD_HPT_MARKER_START).Take(SPD_HPT_MARKER_END - SPD_HPT_MARKER_START).ToArray();
            
            // Проверяем полный маркер "HPT" или неполный "PT" (для совместимости)
            return hptMarker.SequenceEqual(Encoding.ASCII.GetBytes("HPT").Take(3)) ||
                   (hptMarker.Length >= 2 && hptMarker[0] == 0x50 && hptMarker[1] == 0x54);
        }

        /// <summary>
        /// Чтение Secure ID из SPD данных модуля памяти
        /// 
        /// Secure ID хранится в SPD по адресам 0x184-0x187 в формате little-endian.
        /// Это 32-битное значение, которое должно совпадать с рассчитанным Encryption ID.
        /// </summary>
        /// <param name="spdData">SPD данные модуля памяти</param>
        /// <returns>Secure ID (32-битное значение) или null, если данных недостаточно</returns>
        public static uint? ReadSecureId(byte[] spdData)
        {
            if (spdData == null || spdData.Length < SPD_SECURE_ID_END)
            {
                return null;
            }

            // Читаем 4 байта в формате little-endian (unsigned int)
            return BitConverter.ToUInt32(spdData, SPD_SECURE_ID_START);
        }

        /// <summary>
        /// Расчет Encryption ID на основе SPD данных и Sensor Registers
        /// </summary>
        /// <param name="spdData">SPD данные модуля памяти</param>
        /// <param name="sensorReg6">Sensor Register 6 (Manufacturer ID) - 16-битное значение</param>
        /// <param name="sensorReg7">Sensor Register 7 (Device ID/Revision) - 16-битное значение</param>
        /// <returns>Рассчитанный Encryption ID (32-битное значение)</returns>
        public static uint CalculateEncryptionId(byte[] spdData, ushort sensorReg6, ushort sensorReg7)
        {
            if (spdData == null)
            {
                throw new ArgumentNullException(nameof(spdData));
            }

            // Построение буфера v69
            var buffer = BuildBufferV69(spdData, sensorReg6, sensorReg7);

            // Расчет CRC32 (Encryption ID)
            return CalculateCrc32MakeHpId(buffer, BUFFER_V69_SIZE);
        }

        /// <summary>
        /// Автоматическое определение Sensor Registers путем перебора известных значений
        /// </summary>
        /// <param name="spdData">SPD данные модуля памяти</param>
        /// <param name="secureId">Secure ID из SPD</param>
        /// <returns>Кортеж (sensorReg6, sensorReg7) или null, если совпадение не найдено</returns>
        public static (ushort reg6, ushort reg7)? AutoDetectSensorRegisters(byte[] spdData, uint secureId)
        {
            if (spdData == null)
            {
                return null;
            }

            // Предустановленные значения Sensor Registers
            var presetValues = new Dictionary<string, (ushort reg6, ushort reg7)>
            {
                { "S34TS04A - Ablic", (0x1C85, 0x2221) },
                { "STTS2004 - STMicroelectronics", (0x104A, 0x2201) },
                { "MCP98244 - Microchip", (0x0054, 0x2201) },
                { "TSE2004GB2B0 - Renesas", (0x00F8, 0xEE25) }
            };

            // Перебор всех известных значений
            foreach (var (_, (reg6, reg7)) in presetValues)
            {
                try
                {
                    var encryptionId = CalculateEncryptionId(spdData, reg6, reg7);
                    if (encryptionId == secureId)
                    {
                        return (reg6, reg7);
                    }
                }
                catch
                {
                    // Игнорируем ошибки при переборе
                    continue;
                }
            }

            return null;
        }

        #endregion

        #region Приватные методы

        /// <summary>
        /// Построение буфера v69 (244 байта) для расчета Encryption ID
        /// 
        /// Структура буфера:
        /// [0-129]:     SPD данные (0x7E-0xFF) - 130 байт
        /// [130-193]:   SPD Extended (0x140-0x17F) - 64 байта
        /// [194]:       Разделитель 0x20 (пробел)
        /// [195-196]:   Sensor Register 6 (Low byte, High byte)
        /// [197]:       Разделитель 0x20 (пробел)
        /// [198-199]:   Sensor Register 7 (Low byte, High byte)
        /// [200]:       Разделитель 0x20 (пробел)
        /// [201-208]:   RCD данные - 8 байт (0, 0, 0x20, 0, 0, 0x20, 0, 0x20)
        /// [209-243]:   Secret Key - 35 байт
        /// </summary>
        private static byte[] BuildBufferV69(byte[] spdData, ushort sensorReg6, ushort sensorReg7)
        {
            var buffer = new byte[BUFFER_V69_SIZE];
            int offset = 0;

            // ШАГ 1: Копирование SPD данных
            if (spdData.Length >= SPD_RANGE_2_END)
            {
                // Полный SPD: копируем оба диапазона
                Array.Copy(spdData, SPD_RANGE_1_START, buffer, offset, SPD_RANGE_1_SIZE);
                offset += SPD_RANGE_1_SIZE;

                Array.Copy(spdData, SPD_RANGE_2_START, buffer, offset, SPD_RANGE_2_SIZE);
                offset += SPD_RANGE_2_SIZE;
            }
            else
            {
                // Неполный SPD: копируем доступные данные
                int spdSize = BUFFER_V69_SIZE - (1 + 2 + 1 + 2 + 1 + 8 + 35); // 194 байта
                int copySize = Math.Min(spdSize, spdData.Length);
                Array.Copy(spdData, 0, buffer, offset, copySize);
                offset += copySize;
            }

            // ШАГ 2: Разделитель перед Sensor Registers
            buffer[offset++] = 0x20; // Пробел (ASCII 0x20)

            // ШАГ 3: Sensor Register 6 (Manufacturer ID)
            // Порядок байтов: Low byte, High byte (из I2C интерфейса)
            buffer[offset++] = (byte)(sensorReg6 & 0xFF);        // Low byte
            buffer[offset++] = (byte)((sensorReg6 >> 8) & 0xFF); // High byte

            // Разделитель
            buffer[offset++] = 0x20;

            // ШАГ 4: Sensor Register 7 (Device ID/Revision)
            buffer[offset++] = (byte)(sensorReg7 & 0xFF);        // Low byte
            buffer[offset++] = (byte)((sensorReg7 >> 8) & 0xFF); // High byte

            // Разделитель
            buffer[offset++] = 0x20;

            // ШАГ 5: RCD данные (Register Clock Driver)
            // Формат: Vendor ID (2 байта), разделитель, Device ID (2 байта),
            //         разделитель, Revision (1 байт), разделитель
            // В текущей реализации все значения равны 0
            buffer[offset++] = 0;
            buffer[offset++] = 0;
            buffer[offset++] = 0x20;
            buffer[offset++] = 0;
            buffer[offset++] = 0;
            buffer[offset++] = 0x20;
            buffer[offset++] = 0;
            buffer[offset++] = 0x20;
            offset += 8;

            // ШАГ 6: Секретный ключ
            Array.Copy(COPYRIGHT_STRING, 0, buffer, offset, Math.Min(COPYRIGHT_STRING.Length, BUFFER_V69_SIZE - offset));

            return buffer;
        }

        /// <summary>
        /// Точная реализация CRC32 с полиномом 0xD5828281 (MakeHpId)
        /// 
        /// Алгоритм основан на декомпилированном коде BIOS (функция sub_232F05C).
        /// Используется для расчета Encryption ID из буфера данных.
        /// 
        /// Алгоритм:
        /// 1. Инициализация CRC значением 0xFFFFFFFF
        /// 2. Для каждого байта в буфере:
        ///    - XOR CRC с байтом, сдвинутым на 8 бит влево
        ///    - Выполнить 32 итерации:
        ///      * Если старший бит установлен: crc = (crc * 2) ^ POLYNOMIAL
        ///      * Иначе: crc = crc * 2
        /// </summary>
        /// <param name="buffer">Буфер данных</param>
        /// <param name="length">Длина данных для обработки (в байтах)</param>
        /// <returns>Рассчитанное значение CRC32 (32-битное беззнаковое число)</returns>
        private static uint CalculateCrc32MakeHpId(byte[] buffer, int length)
        {
            uint crc = CRC32_INITIAL_VALUE;

            // Обработка каждого байта в буфере
            for (int i = 0; i < length; i++)
            {
                // Явно конвертируем в uint для избежания проблем с типами
                uint byteVal = (uint)(buffer[i] & 0xFF);

                // XOR с байтом, сдвинутым на 8 бит влево
                crc ^= (byteVal << 8);
                crc &= 0xFFFFFFFF;

                // 32 итерации обработки CRC
                for (int j = 0; j < 32; j++)
                {
                    if ((crc & 0x80000000) != 0) // Старший бит установлен
                    {
                        // Полином применяется при установленном старшем бите
                        crc = ((crc * 2) ^ CRC32_POLYNOMIAL) & 0xFFFFFFFF;
                    }
                    else // Старший бит = 0
                    {
                        // Просто сдвиг влево
                        crc = (crc * 2) & 0xFFFFFFFF;
                    }
                }
            }

            return crc & 0xFFFFFFFF;
        }

        #endregion
    }
}


