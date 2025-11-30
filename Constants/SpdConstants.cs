namespace HexEditor.Constants
{
    /// <summary>
    /// Константы для работы с SPD данными
    /// </summary>
    public static class SpdConstants
    {
        // Размеры SPD дампов
        public const int DDR3_SPD_SIZE = 256;
        public const int DDR4_SPD_SIZE = 512;
        public const int DDR5_SPD_SIZE = 1024;

        // Размеры блоков RSWP
        public const int DDR3_RSWP_BLOCK_SIZE = 128;
        public const int DDR4_RSWP_BLOCK_SIZE = 128;
        public const int DDR5_RSWP_BLOCK_SIZE = 64;

        // Количество блоков RSWP
        public const int DDR3_RSWP_BLOCK_COUNT = 2;
        public const int DDR4_RSWP_BLOCK_COUNT = 4;
        public const int DDR5_RSWP_BLOCK_COUNT = 16;

        // Смещения для производителей
        public const int DDR4_MODULE_MANUFACTURER_OFFSET = 320;
        public const int DDR4_DRAM_MANUFACTURER_OFFSET = 350;
        public const int DDR5_MODULE_MANUFACTURER_OFFSET = 512;
        public const int DDR5_DRAM_MANUFACTURER_OFFSET = 552;

        // Смещения для Part Number
        public const int DDR4_PART_NUMBER_OFFSET = 329;
        public const int DDR4_PART_NUMBER_LENGTH = 20;

        // Смещения для Serial Number
        public const int DDR4_SERIAL_NUMBER_OFFSET = 325;
        public const int DDR4_SERIAL_NUMBER_LENGTH = 4;

        // Смещения для CRC (DDR4)
        public const int DDR4_CRC_BLOCK0_DATA_START = 0;
        public const int DDR4_CRC_BLOCK0_DATA_LENGTH = 126;
        public const int DDR4_CRC_BLOCK0_OFFSET = 126;

        public const int DDR4_CRC_BLOCK1_DATA_START = 128;
        public const int DDR4_CRC_BLOCK1_DATA_LENGTH = 126;
        public const int DDR4_CRC_BLOCK1_OFFSET = 254;

        // Key bytes для определения типа памяти
        public const int MEMORY_TYPE_BYTE_OFFSET = 2;
        public const byte MEMORY_TYPE_DDR4 = 0x0C;
        public const byte MEMORY_TYPE_DDR5 = 0x12;

        // Минимальные размеры для валидации
        public const int MIN_VALID_SPD_SIZE = 256;
        public const int MIN_DDR4_SIZE = 256;
        public const int MIN_DDR5_SIZE = 512;

        // Размеры страниц
        public const int DDR4_PAGE_SIZE = 256;
        public const int DDR5_PAGE_SIZE = 128;

        // Timing offsets (DDR4)
        public const int DDR4_TCK_MTB_OFFSET = 18;
        public const int DDR4_TCK_FTB_OFFSET = 125;
        public const int DDR4_TAA_MTB_OFFSET = 24;
        public const int DDR4_TAA_FTB_OFFSET = 123;

        // Timebases (DDR4)
        public const double DDR4_MEDIUM_TIMEBASE_PS = 125.0;
        public const double DDR4_FINE_TIMEBASE_PS = 1.0;
    }
}

