namespace HexEditor.Constants
{
    /// <summary>
    /// Константы для UI
    /// </summary>
    public static class UiConstants
    {
        // Таймеры и throttling
        public const int SPD_UPDATE_TIMER_MS = 50;
        public const int STATUS_UPDATE_TIMER_MS = 500;
        public const int MENU_UPDATE_THROTTLE_MS = 50;
        public const int DPI_REFRESH_DELAY_MS = 500;

        // Логирование
        public const int MAX_LOG_ENTRIES = 500;

        // Hex Editor
        public const int DEFAULT_HEX_FONT_SIZE = 12;
        public const int DEFAULT_BYTES_PER_LINE = 16;
        public const int MOUSE_WHEEL_SCROLL_LINES = 3;

        // Placeholder значения
        public const string PLACEHOLDER_VALUE = "—";
        public const string PLACEHOLDER_UNKNOWN = "Unknown";

        // Arduino
        public const int ARDUINO_SCAN_TIMEOUT_SECONDS = 5;
        public const int ARDUINO_DEFAULT_BAUD_RATE = 115200;
        public const int ARDUINO_READ_CHUNK_SIZE = 32;
        public const int ARDUINO_WRITE_DELAY_MS = 10;

        // Имена устройств
        public const int ARDUINO_NAME_MAX_LENGTH = 16;

        // Сообщения
        public const string MSG_NO_DATA_TO_WRITE = "Нет данных для записи. Сначала загрузите или откройте файл.";
        public const string MSG_DEVICE_NOT_CONNECTED = "Устройство не подключено или SPD не готов.";
        public const string MSG_SELECT_DEVICE_FIRST = "Выберите устройство перед подключением.";
        public const string MSG_CRC_ALREADY_CORRECT = "CRC уже корректен.";
        public const string MSG_CRC_FIXED_SUCCESS = "CRC успешно исправлен.";

        // Форматы строк
        public const string FORMAT_HEX_OFFSET = "0x{0:X8}";
        public const string FORMAT_DEC_OFFSET = "{0}";
        public const string FORMAT_BYTE_HEX = "0x{0:X2}";
        public const string FORMAT_COM_PORT = "COM: {0}";

        // Размеры окон и панелей
        public const int MIN_WINDOW_WIDTH = 1511;
        public const int MIN_WINDOW_HEIGHT = 840;
        public const int LEFT_PANEL_WIDTH = 300;
        public const int LEFT_PANEL_MIN_WIDTH = 300;
        public const int LEFT_PANEL_MAX_WIDTH = 300;

        // Цвета статусов
        public static class StatusColors
        {
            public const string CRC_OK_COLOR = "#4CAF50";      // Green
            public const string CRC_BAD_COLOR = "#F44336";     // Red
            public const string DDR4_COLOR = "#FFC107";        // Yellow/Amber
            public const string DDR5_COLOR = "#FFC107";        // Yellow/Amber
            public const string MUTED_COLOR = "#888888";       // Gray
        }
    }
}

