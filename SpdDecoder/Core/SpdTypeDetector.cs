using System;

namespace HexEditor.SpdDecoder
{
    /// <summary>
    /// Фабрика для определения типа SPD и создания соответствующих редакторов/декодеров
    /// </summary>
    internal static class SpdTypeDetector
    {
        /// <summary>
        /// Определяет тип памяти по байту 2 SPD
        /// </summary>
        public static SpdDecoderMemoryType DetectMemoryType(byte[] data)
        {
            if (data == null || data.Length < 3)
                return SpdDecoderMemoryType.Unknown;

            byte memoryType = data[2];
            return memoryType switch
            {
                0x0C => SpdDecoderMemoryType.Ddr4,
                0x12 => SpdDecoderMemoryType.Ddr5,
                _ => SpdDecoderMemoryType.Unknown
            };
        }

        /// <summary>
        /// Создает редактор SPD на основе типа памяти
        /// </summary>
        public static ISpdEditor? CreateEditor(byte[] data, ForcedMemoryType forcedType = ForcedMemoryType.Auto)
        {
            if (data == null || data.Length < 256)
                return null;

            var memoryType = GetMemoryType(data, forcedType);
            return memoryType switch
            {
                SpdDecoderMemoryType.Ddr4 => new Ddr4SpdEditor(),
                SpdDecoderMemoryType.Ddr5 => new Ddr5SpdEditor(),
                _ => null
            };
        }
        
        /// <summary>
        /// Получает тип памяти с учетом принудительного выбора
        /// </summary>
        private static SpdDecoderMemoryType GetMemoryType(byte[] data, ForcedMemoryType forcedType)
        {
            if (forcedType != ForcedMemoryType.Auto)
            {
                return forcedType switch
                {
                    ForcedMemoryType.Ddr4 => SpdDecoderMemoryType.Ddr4,
                    ForcedMemoryType.Ddr5 => SpdDecoderMemoryType.Ddr5,
                    _ => DetectMemoryType(data)
                };
            }
            return DetectMemoryType(data);
        }

        /// <summary>
        /// Создает декодер SPD на основе типа памяти
        /// </summary>
        public static ISpdDecoder? CreateDecoder(byte[] data, ForcedMemoryType forcedType = ForcedMemoryType.Auto)
        {
            if (data == null || data.Length < 256)
                return null;

            var memoryType = GetMemoryType(data, forcedType);
            return memoryType switch
            {
                SpdDecoderMemoryType.Ddr4 => new Ddr4SpdDecoder(data),
                SpdDecoderMemoryType.Ddr5 => new Ddr5SpdDecoder(data),
                _ => null
            };
        }
    }

    /// <summary>
    /// Тип памяти SPD (для декодера)
    /// </summary>
    internal enum SpdDecoderMemoryType
    {
        Unknown,
        Ddr4,
        Ddr5
    }
    
    /// <summary>
    /// Принудительный выбор типа памяти для поврежденных дампов
    /// </summary>
    public enum ForcedMemoryType
    {
        Auto,   // Автоматическое определение
        Ddr4,   // Принудительно DDR4
        Ddr5    // Принудительно DDR5
    }
}

