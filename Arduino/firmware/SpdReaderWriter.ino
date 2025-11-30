/*
    Arduino SPD Reader/Writer - Программатор SPD для DDR4/DDR5
   ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
   Для оверклокеров и энтузиастов компьютерного железа

   Репозиторий: https://github.com/1a2m3/SPD-Reader-Writer
   Поддержка:   https://forums.evga.com/FindPost/3053544
   Донаты:      https://paypal.me/mik4rt3m

   ============================================================================
   МОДИФИЦИРОВАННАЯ ВЕРСИЯ - Исправление для Samsung S34TS04A (build 2)
   ============================================================================
   Изменения:
   - Строка 34: Обновлена версия прошивки до 20251127 build 2
   - Строки 1435-1450: Исправлена функция ddr4Detect() с двойным методом
   - Строки 1452-1469: Добавлена функция ddr4DetectBySpdByte()
   - Строка 1076: КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ - кэширование isDdr4 ДО включения HV
   
   Проблема 1: Samsung S34TS04A медленно отвечает на команды SPA/RPA
   Решение 1: Добавлена задержка 10ms + резервный метод через SPD байт
   
   Проблема 2: При включенном HV (9V) ddr4Detect() работает еще медленнее
   Решение 2: Определяем DDR4 ДО включения HV и кэшируем результат
   
   Результат: RSWP блоки 1-3 теперь работают на S34TS04A ✅
   ============================================================================
*/

#include <Wire.h>
#include <EEPROM.h>

// ===================== НАСТРОЙКИ ПЛАТЫ =====================

// Настройки связи
#define PORT        Serial  // Порт связи (Serial или SerialUSB для нативных USB Arduino)
#define BAUD_RATE   115200  // Скорость порта (должна совпадать с программой на ПК)

// Пины управления
#define HV_EN        9      // Пин управления высоким напряжением (9V)
#define HV_FB        6      // Пин обратной связи высокого напряжения
#define SA1_EN      A1      // Пин управления SA1

// ===========================================================

#define FW_VER 20251127  // Версия прошивки (ГГГГММДД) - build 3

// Маски поддержки RSWP для разных типов памяти
#define DDR5 _BV(5)  // Режим Offline
#define DDR4 _BV(4)  // Управление VHV
#define DDR3 _BV(3)  // Управление VHV+SA1

// Регистры SPD5 Hub
#define MR0  0x00  // Тип устройства; старший байт
#define MR1  0x01  // Тип устройства; младший байт
#define MR6  0x06  // Время восстановления после записи
#define MR11 0x0B  // Конфигурация Legacy режима I2C
#define MR12 0x0C  // Защита записи NVM блоков [7:0]
#define MR13 0x0D  // Защита записи NVM блоков [15:8]
#define MR14 0x0E  // Конфигурация интерфейса Host и Local
#define MR18 0x12  // Конфигурация устройства
#define MR20 0x14  // Команда очистки статуса ошибок MR52
#define MR48 0x30  // Статус устройства
#define MR52 0x34  // Статус ошибок Hub, термодатчика и NVM

#define PMIC 0b1001 << 3  // Идентификатор локального устройства PMIC

// Типы устройств SPD5 Hub
#define SPD5_NO 0x5108  // SPD5 Hub без термодатчика
#define SPD5_TS 0x5118  // SPD5 Hub с термодатчиком

// Команды страниц DDR4 EEPROM
#define SPA0 0x6C  // Установить адрес страницы 0
#define SPA1 0x6E  // Установить адрес страницы 1
#define RPA  0x6D  // Прочитать адрес текущей страницы

// Команды RSWP (обратимая защита записи)
#define RPS0 0x63  // Прочитать статус SWP0      (байты 0-127)   (DDR4/DDR3/DDR2)
#define RPS1 0x69  // Прочитать статус SWP1      (байты 128-255) (DDR4)
#define RPS2 0x6B  // Прочитать статус SWP2      (байты 256-383) (DDR4)
#define RPS3 0x61  // Прочитать статус SWP3      (байты 384-511) (DDR4)

#define SWP0 0x62  // Установить RSWP для блока 0  (байты 0-127)   (DDR4/DDR3/DDR2)
#define SWP1 0x68  // Установить RSWP для блока 1  (байты 128-255) (DDR4)
#define SWP2 0x6A  // Установить RSWP для блока 2  (байты 256-383) (DDR4)
#define SWP3 0x60  // Установить RSWP для блока 3  (байты 384-511) (DDR4)

#define CWP  0x66  // Очистить RSWP (все блоки)  (DDR4/DDR3/DDR2)

// Команды PSWP (постоянная защита записи)
#define PWPB 0b0110  // Код управления PSWP (биты 7-4) (DDR3/DDR2)

// Данные EEPROM
#define DNC 0x00  // Байт "не важно"

// Команды устройства (от ПК к Arduino)
#define READBYTE     'r'  // Чтение байта
#define WRITEBYTE    'w'  // Запись байта
#define WRITEPAGE    'g'  // Запись страницы
#define SCANBUS      's'  // Сканирование шины I2C
#define I2CCLOCK     'c'  // Управление частотой I2C
#define PROBEADDRESS 'a'  // Проверка адреса
#define PINCONTROL   'p'  // Управление пинами
#define PINRESET     'd'  // Сброс пинов в состояние по умолчанию
#define RSWP         'b'  // Управление обратимой защитой записи
#define PSWP         'l'  // Управление постоянной защитой записи
#define OVERWRITE    'o'  // Тест защиты записи по смещению
#define NAME         'n'  // Управление именем устройства
#define VERSION      'v'  // Получить версию прошивки
#define TEST         't'  // Тест связи
#define RSWPREPORT   'f'  // Возможности RSWP
#define DDR4DETECT   '4'  // Тест наличия DDR4
#define DDR5DETECT   '5'  // Тест наличия DDR5
#define SPD5HUBREG   'h'  // Доступ к регистрам SPD5 Hub
#define SIZE         'z'  // Получить размер EEPROM
#define FACTORYRESET '-'  // Сброс настроек устройства

// Параметры команд
#define ENABLE  0x01
#define SET     0x01
#define DISABLE 0x00
#define RESET   0x00
#define GET     '?'  // Суффикс для получения текущего состояния

// Параметры пинов устройства
#define HV_SWITCH  0  // Переключение VHV на пине SA0
#define SA1_SWITCH 1  // Переключение состояния SA1

// Ответы устройства
#define RESPONSE '&'
#define ALERT    '@'
#define UNKNOWN  '?'

// Шаблоны
#define A1_MASK 0b11001100  // Маска ответа ScanBus() когда SA1 высокий: 82-83, 86-87

// Оповещения устройства
#define SLAVEINC '+'
#define SLAVEDEC '-'
#define CLOCKINC '/'
#define CLOCKDEC '\\'

// Настройки имени устройства
#define NAMELENGTH 16
char deviceName[NAMELENGTH];

// Настройки устройства
#define DEVICESETTINGS 0x20  // Адрес EEPROM для хранения настроек
#define CLOCKMODE      0     // Позиция бита для настроек частоты I2C
#define FASTMODE       true
#define STDMODE        false

// Частоты шины I2C
int32_t clock[] = { 100000, 400000 };

// Глобальные переменные
uint32_t i2cClock = clock[0];  // Начальная частота I2C
uint8_t eepromPageAddress;     // Начальный адрес страницы EEPROM
uint8_t slaveCountCurrent;     // Текущее количество устройств на шине I2C
uint8_t slaveCountLast;        // Предыдущее количество устройств на шине I2C
bool i2cClockCurrent;          // Текущий режим частоты I2C
bool i2cClockLast;             // Предыдущий режим частоты I2C
bool cmdExecuting;             // Указывает на выполнение команды
uint8_t responseBuffer[32];    // Буфер тела ответа
uint8_t responseLength;        // Длина и индекс тела ответа

// Структура данных пина
typedef struct {
  const int name;
  bool defaultState;
  uint8_t mode;
} pinData;

// Массив конфигурационных пинов
pinData ConfigPin[] = {
  { HV_EN, false, OUTPUT }, // Управление HV
  { HV_FB, NULL, INPUT },   // Обратная связь HV
  { SA1_EN, true, OUTPUT }, // Управление SA1
};

size_t pinCount = sizeof(ConfigPin) / sizeof(ConfigPin[0]);

void setup() {

  // Настройка конфигурационных пинов
  for (uint8_t i = 0; i < pinCount; i++) {
    pinMode(ConfigPin[i].name, ConfigPin[i].mode);
  }

  resetPinsInternal();

  // Инициализация шины I2C в режиме мастера
  Wire.begin();

  // Установка таймаута I2C на 0.01 сек
  Wire.setWireTimeout(10000, true);

  // Настройка частоты I2C
  Wire.setClock(clock[getI2cClockMode()]);
  i2cClockCurrent = clock[getI2cClockMode()];
  i2cClockLast = i2cClockCurrent;

  // Сканирование шины I2C
  slaveCountCurrent = getQuantity();
  slaveCountLast = slaveCountCurrent;

  // Сброс адреса страницы EEPROM
  setPageAddress(0);

  // Запуск последовательной передачи данных
  PORT.begin(BAUD_RATE);
  PORT.setTimeout(100);  // Таймаут ввода в мс

  // Ожидание подключения последовательного порта или инициализации
  while (!PORT) {}

  // Проверка оборудования
  #ifndef __AVR__
  PORT.write(UNKNOWN);
  while (true) {}
  #endif

  // Отправка положительного ответа когда устройство готово
  Respond(true);
  OutputResponse();
}

void loop() {

  resetPinsInternal();

  // Ожидание входных данных
  if (PORT.available()) {
    parseCommand();
  }

  // Мониторинг шины I2C
  i2cMonitor();
}

// Обработка входных команд и данных
void parseCommand() {

  if (!PORT.available()) {
    cmdExecuting = false;
    return;
  }

  cmdExecuting = true;

  switch ((char)PORT.read()) {

    // Чтение байта
    case READBYTE:
      cmdRead();
      break;

    // Запись байта
    case WRITEBYTE:
      cmdWrite();
      break;

    // Запись страницы
    case WRITEPAGE:
      cmdWritePage();
      break;

    // Сканирование шины I2C для адресов
    case SCANBUS:
      cmdScanBus();
      break;

    // Проверка наличия адреса I2C
    case PROBEADDRESS:
      cmdProbeBusAddress();
      break;

    // Настройки шины I2C
    case I2CCLOCK:
      cmdI2CClock();
      break;

    // Управление цифровыми пинами
    case PINCONTROL:
      cmdPinControl();
      break;

    case PINRESET:
      cmdPinReset();
      break;

    // Управление RSWP
    case RSWP:
      cmdRSWP();
      break;

    // Управление PSWP
    case PSWP:
      cmdPSWP();
      break;

    case OVERWRITE:
      cmdOverWrite();
      break;

    // Получить версию прошивки
    case VERSION:
      cmdVersion();
      break;

    // Тест связи с устройством
    case TEST:
      cmdTest();
      break;

    // Отчет о поддерживаемых возможностях RSWP
    case RSWPREPORT:
      cmdRswpRespond();
      break;

    // Тест определения DDR4
    case DDR4DETECT:
      cmdDdr4Detect();
      break;

    // Тест определения DDR5
    case DDR5DETECT:
      cmdDdr5Detect();
      break;

    // Регистр DDR5 Hub
    case SPD5HUBREG:
      cmdSpd5Hub();
      break;

    case SIZE:
      cmdSize();
      break;

    // Управление именем устройства
    case NAME:
      cmdName();
      break;

    // Восстановление заводских настроек
    case FACTORYRESET:
      cmdFactoryReset();
      break;
  }

  // Вывод ответа
  OutputResponse();

  cmdExecuting = false;
}


/*  -=  Обработчики ответов  =-  */

// Поместить один байт в ответ
void Respond(uint8_t inputData) {
  responseBuffer[responseLength] = inputData;
  responseLength++;
}

// Поместить массив байтов в ответ
void Respond(uint8_t* inputData, size_t length) {
  for (uint8_t i = 0; i < length; i++) {
    Respond(inputData[i]);
  }
}

// Поместить строку в ответ
void Respond(String inputData) {
  for (uint8_t i = 0; i < inputData.length(); i++) {
    Respond(inputData[i]);
  }
}

// Вывод заголовка ответа, размера, содержимого и контрольной суммы
void OutputResponse() {
  if (responseLength > 0) {

    // Расчет контрольной суммы
    uint8_t checkSum = 0;
    for (uint8_t i = 0; i < responseLength; i++) {
      checkSum += responseBuffer[i];
    }

    PORT.write(RESPONSE);
    PORT.write(responseLength);
    PORT.write(responseBuffer, responseLength);
    PORT.write(checkSum);

    // Ожидание завершения вывода
    PORT.flush();

    // Сброс индекса буфера данных ответа
    responseLength = 0;

    // Очистка массива ответа
    memset(responseBuffer, 0x00, sizeof(responseBuffer));
  }
}


/*  -=  Обработчики команд  =-  */

void cmdRead() {
  // Входной буфер
  uint8_t buffer[4] = { 0 };
  PORT.readBytes(buffer, sizeof(buffer));

  // Адрес EEPROM
  uint8_t address = buffer[0];
  // Адрес смещения
  uint16_t offset = buffer[1] << 8 | buffer[2];
  // Количество байт
  uint8_t length  = buffer[3];

  // Выходной буфер
  uint8_t data[length];
  // Заполнение буфера данных
  readByte(address, offset, length, data);

  Respond(data, sizeof(data));
}

void cmdWrite() {
  // Входной буфер
  uint8_t buffer[4] = { 0 };
  PORT.readBytes(buffer, sizeof(buffer));

  // Адрес EEPROM
  uint8_t address = buffer[0];
  // Адрес смещения
  uint16_t offset = buffer[1] << 8 | buffer[2];
  // Значение входного байта
  uint8_t data    = buffer[3];

  Respond(writeByte(address, offset, data));
}

void cmdWritePage() {
  // Входной буфер
  uint8_t buffer[4] = { 0 };
  PORT.readBytes(buffer, sizeof(buffer));

  // Адрес EEPROM
  uint8_t address = buffer[0];
  // Адрес смещения
  uint16_t offset = buffer[1] << 8 | buffer[2];
  // Количество байт
  uint8_t length  = buffer[3];

  // Проверка входной длины
  if (length == 0) {
    Respond(0);
    return;
  }

  // Буфер входных данных
  uint8_t data[length];
  PORT.readBytes(data, sizeof(data));

  if (length > 16) {
    Respond(false);
    return;
  }

  Respond(writePage(address, offset, length, data));
}

void cmdScanBus() {
  Respond(scanBus());
}

void cmdTest() {
  Respond(true);
}

void cmdRswpRespond() {
  Respond(rswpSupportTest());
}

void cmdDdr4Detect() {
  // Входной буфер
  uint8_t buffer[1];
  PORT.readBytes(buffer, sizeof(buffer));

  uint8_t address = buffer[0];  // Адрес I2C
  Respond(ddr4Detect(address));
}

void cmdDdr5Detect() {
  // Входной буфер
  uint8_t buffer[1];
  PORT.readBytes(buffer, sizeof(buffer));

  uint8_t address = buffer[0];  // Адрес I2C
  Respond(ddr5Detect(address));
}

void cmdSpd5Hub() {
  // Входной буфер
  uint8_t buffer[3];
  PORT.readBytes(buffer, sizeof(buffer));

  uint8_t address = buffer[0];  // Адрес I2C
  uint8_t memReg  = buffer[1];  // Регистр
  uint8_t command = buffer[2];  // Команда

  if (!ddr5Detect(address)) {
    Respond(false);
  }

  // Запись в регистр
  if (command == SET) {
    // Буфер данных
    uint8_t data[1];  // Значение байта
    PORT.readBytes(data, sizeof(data));
    Respond(writeReg(address, memReg, data[0]));
  }
  // Чтение из регистра
  else if (command == GET) {
    Respond(readReg(address, memReg));
  }
  // Нераспознанная команда
  else {
    Respond(false);
  }
}

void cmdSize() {
  // Входной буфер
  uint8_t buffer[1];
  PORT.readBytes(buffer, sizeof(buffer));

  uint8_t address = buffer[0];  // Адрес I2C

  if (!validateAddress(address) || !probeBusAddress(address)) {
    Respond(false);
    return;
  }

  uint16_t size = 0;  // 0

  bool ddr5 = ddr5Detect(address);

  if (ddr5) {
    size = 1024;  // 3
  }
  else {
    if (getQuantity() == 1) {
      if (ddr4Detect()) {
        size = 512;  // 2
      }
      else {
        size = 256;  // 1
      }
    }
    else if (getQuantity() > 1) {
      if (!ddr4Detect()) {
        size = 256;  // 1
      }
    }
  }

  if (!size) {
    // Чтение байта 0x02
    uint8_t keyByte[1] = { 0 };
    readByte(address, 0x02, 1, keyByte);
    if (0x0C <= keyByte[0] && keyByte[0] <= 0x11) {
      // DDR4, DDR4E, LPDDR3, LPDDR4, и LPDDR4X
      size = 512;  // 2
    }
  }

  // Возврат позиции бита, соответствующей индексу массива SpdReaderWriterDll.Spd.DataLength.Length
  for (uint8_t i = 0; i <= 3; i++) {
    if(bitRead(highByte(size), i)) {
      Respond(i + 1);
      return;
    }
  }

  Respond(0);
}

void cmdVersion() {

  uint8_t verLength = sizeof(FW_VER);
  uint8_t data[verLength];

  for (int8_t i = verLength; i > 0; i--) {
    data[i - 1] = FW_VER >> (8 * (i - 1));
  }

  Respond(data, verLength);
}

void cmdName() {
  // Буфер данных для байта команды
  uint8_t buffer[1] = { 0 };
  PORT.readBytes(buffer, sizeof(buffer));

  // Получить имя
  if (buffer[0] == GET) {
    Respond(getName());
  }
  // Установить имя
  else if (buffer[0] > 0 && buffer[0] <= NAMELENGTH) {
    // Подготовка буфера имени
    char name[buffer[0] + 1];
    // Чтение имени и помещение в буфер
    PORT.readBytes(name, buffer[0]);
    // Установка последнего байта на \0 где заканчивается строка
    name[buffer[0]] = 0;

    Respond(setName(name));
  }
  // Неверная команда
  else {
    Respond(false);
  }
}

void cmdProbeBusAddress() {
  // Буфер данных для адреса
  uint8_t buffer[1] = { 0 };
  PORT.readBytes(buffer, sizeof(buffer));

  uint8_t address = buffer[0];  // Адрес I2C
  Respond(probeBusAddress(address));
}

void cmdI2CClock() {
  // Буфер данных для режима частоты
  uint8_t buffer[1] = { 0 };
  PORT.readBytes(buffer, sizeof(buffer));

  // Установить частоту I2C
  if (buffer[0] == FASTMODE || buffer[0] == STDMODE) {
    setI2cClockMode(buffer[0]);
    Respond(getI2cClockMode() == buffer[0]);
  }
  // Получить текущую частоту I2C
  else if (buffer[0] == GET) {
    Respond(getI2cClockMode());
  }
  // Нераспознанная команда
  else {
    Respond(false);
  }
}

void cmdFactoryReset() {
  Respond(factoryReset());
}

void cmdRSWP() {
  // Буфер данных
  uint8_t buffer[3] = { 0 };
  PORT.readBytes(buffer, sizeof(buffer));

  // Адрес I2C
  uint8_t address = buffer[0];
  // Номер блока
  uint8_t block   = buffer[1];
  // Состояние блока
  char state      = buffer[2];

  // Включить RSWP
  if (state == ENABLE) {
    Respond(setRswp(address, block));
  }
  // Очистить RSWP (все блоки)
  else if (state == DISABLE) {
    Respond(clearRswp(address));
  }
  // Получить статус RSWP
  else if (state == GET) {
    Respond(getRswp(address, block));
  }
  // Нераспознанная команда RSWP
  else {
    Respond(false);
  }
}

void cmdPSWP() {
  // Буфер данных
  uint8_t buffer[2] = { 0 };
  PORT.readBytes(buffer, sizeof(buffer));

  // Адрес EEPROM
  uint8_t address = buffer[0];
  // Состояние PSWP
  char state      = buffer[1];

  // Включить PSWP
  if (state == ENABLE) {
    Respond(setPswp(address));
  }
  // Прочитать PSWP
  else if (state == GET) {
    Respond(getPswp(address));
  }
  // Неизвестное состояние
  else {
    Respond(false);
  }
}

void cmdOverWrite() {
  // Буфер данных
  uint8_t buffer[3] = { 0 };
  PORT.readBytes(buffer, sizeof(buffer));

  // Адрес EEPROM
  uint8_t address = buffer[0];
  // Адрес смещения
  uint16_t offset = buffer[1] << 8 | buffer[2];

  uint8_t data[1];
  Respond(readByte(address, offset, 1, data) && writeByte(address, offset, data[0]));
}

void cmdPinControl() {
  // Буфер данных
  uint8_t buffer[2] = { 0 };
  PORT.readBytes(buffer, sizeof(buffer));

  // Номер пина
  uint8_t pin = buffer[0];
  // Состояние пина
  char state  = buffer[1];

  // Управление SA1
  if (pin == HV_SWITCH) {
    // Переключить состояние SA1
    if (state == ENABLE || state == DISABLE) {
      Respond(setConfigPin(SA1_EN, state));
    }
    // Получить состояние SA1
    else if (state == GET) {
      Respond(getConfigPin(SA1_EN));
    }
    // Неизвестное состояние
    else {
      Respond(false);
    }
  }
  // Управление VHV 9V
  else if (pin == SA1_SWITCH) {
    // Переключить состояние HV
    if (state == ENABLE || state == DISABLE) {
      Respond(setHighVoltage(state));
    }
    // Получить состояние HV
    else if (state == GET) {
      Respond(getHighVoltage());
    }
    // Неизвестное состояние
    else {
      Respond(0);
    }
  }
  // Неизвестный пин
  else {
    Respond(false);
  }
}

void cmdPinReset() {
  Respond(resetPins());
}

/*  -=  Функции чтения/записи  =-  */

// Чтение байтов из EEPROM в буфер данных
bool readByte(uint8_t address, uint16_t offset, uint8_t length, uint8_t* data) {

  uint16_t _offset = offset;

  if (ddr5Detect(address)) {
    _offset |= 0x80;
  }

  adjustPageAddress(address, offset);

  Wire.beginTransmission(address);
  Wire.write((uint8_t)(_offset));

  if (Wire.endTransmission(false) != 0) {
    return false;
  }

  Wire.requestFrom(address, length);

  while (Wire.available() < length) {}

  // Заполнение буфера данных
  for (uint8_t i = 0; i < length; i++) {
    while (!Wire.available()) {}
    data[i] = Wire.read();
    Wire.flush();
  }

  return true;
}

// Запись одного байта в EEPROM
bool writeByte(uint8_t address, uint16_t offset, uint8_t data) {

  uint8_t input[1] = { data };
  return writePage(address, offset, 1, input);
}

// Запись страницы (несколько байтов) в EEPROM
bool writePage(uint8_t address, uint16_t offset, uint8_t length, uint8_t* data) {

  // Проверка смещения и длины чтобы избежать перекрытия границ страницы или блока
  if ((offset % 16 + length) > 16) {
    return false;
  }

  // Проверка защищен ли блок от записи
  if (ddr5Detect(address) && ddr5GetOfflineMode()) {
    uint8_t block = offset / 64;
    if (getRswp(address, block)) {
      return false;
    }
  }

  uint16_t _offset = offset;

  adjustPageAddress(address, offset);

  if (ddr5Detect(address)) {
    _offset |= 0x80;

    // Ожидание завершения записи
    while (bitRead(readReg(address, MR48), 3)) {}
  }

  Wire.beginTransmission(address);
  Wire.write((uint8_t)(_offset));
  Wire.write(data, length);
  uint8_t status = Wire.endTransmission();

  delay(10);

  return status == 0;
}

// Чтение данных из регистра SPD5 Hub
uint8_t readReg(uint8_t address, uint8_t memReg) {

  if (bitRead(memReg, 7)) {
    return false;
  }

  Wire.beginTransmission(address);
  Wire.write(memReg & 0x7F);
  uint8_t status = Wire.endTransmission(false);

  if (status != 0) {
    return false;
  }

  Wire.requestFrom(address, (uint8_t)1);

  // Заполнение буфера данных
  while (!Wire.available()) {}

  uint8_t output = Wire.read();
  Wire.flush();

  return output;
}

// Запись данных в регистр SPD5 Hub
bool writeReg(uint8_t address, uint8_t memReg, uint8_t value) {

  if (!ddr5Detect(address) || bitRead(memReg, 7)) {
    return false;
  }

  if (!(MR11 <= memReg && memReg <= MR13)) {
    return false;
  }

  Wire.beginTransmission(address);
  Wire.write(memReg & 0x7F);
  Wire.write(value);

  // Запись в регистры MR12/MR13 должна сопровождаться операцией Stop для обновления SPD Hub
  uint8_t status = Wire.endTransmission(memReg == MR12 || memReg == MR13);

  // SPD5 Hub не требует задержки для переключения между страницами
  delay(memReg == MR11 ? 0 : 10);

  return status == 0;
}


/*  -=  Функции RSWP  =-  */

// Установка обратимой защиты записи для указанного блока
bool setRswp(uint8_t address, uint8_t block) {

  if (block > 15) {
    return false;
  }

  // DDR5 RSWP
  if (ddr5Detect(address)) {
    // Выбор регистра
    uint8_t memReg = MR12 + bitRead(block, 3);
    // Существующее значение RSWP
    uint8_t currentValue = readReg(address, memReg);
    // Обновленное значение RSWP
    uint8_t updatedValue = 1 << (block & 0b111);

    return writeReg(address, memReg, currentValue | updatedValue);
  }

  // DDR4 и старее RSWP
  uint8_t commands[] = { SWP0, SWP1, SWP2, SWP3 };
  uint8_t cmd = commands[(0 < block && block <= 3) ? block : 0];

  bool result = false;

  // ИСПРАВЛЕНИЕ: Определяем DDR4 ДО включения высокого напряжения
  // Когда HV включен, I2C команды работают медленнее и ddr4Detect() может давать false
  bool isDdr4 = ddr4Detect();

  if (setHighVoltage(true)) {
    if (block == 0) {
      setConfigPin(SA1_EN, false);  // Требуется для пре-DDR4
    }
    // Используем закэшированный результат isDdr4 вместо повторного вызова
    if (block > 0 && !isDdr4) {
      result = false;
    }
    else {
      result = probeDeviceTypeId(cmd);
    }
    resetPinsInternal();
  }

  return result;
}

// Чтение статуса обратимой защиты записи
bool getRswp(uint8_t address, uint8_t block) {

  if (ddr5Detect(address)) {
    if (block > 15) {
      return false;
    }

    return readReg(address, MR12 + bitRead(block, 3)) & (1 << (block & 0b111));
  }

  uint8_t commands[] = { RPS0, RPS1, RPS2, RPS3 };
  uint8_t cmd = (0 < block && block <= 3) ? commands[block] : commands[0];

  // Соответствие JEDEC EE1002(A), TSE2002av
  if (block == 0 && !ddr4Detect()) {
    setHighVoltage(true);
  }

  bool status = probeDeviceTypeId(cmd);  // true/ack = не защищен

  resetPinsInternal();

  return !status;  // true = защищен или rswp не поддерживается; false = не защищен
}

// Очистка обратимой защиты записи
bool clearRswp(uint8_t address) {

  if (ddr5Detect(address)) {

    uint8_t ddr5CwpCmd[2] = { 0 };

    if (!ddr5GetOfflineMode()) {
      return false;
    }

    Wire.beginTransmission(address);
    Wire.write(MR12);
    Wire.write(ddr5CwpCmd, sizeof(ddr5CwpCmd));
    uint8_t status = Wire.endTransmission();

    return status == 0 && readReg(address, MR12) == 0 && readReg(address, MR13) == 0;
  }

  if (!ddr4Detect(address)) {
    // Требуется для пре-DDR4
    setConfigPin(SA1_EN, true);
  }

  if (setHighVoltage(true)) {
    bool result = probeDeviceTypeId(CWP);
    resetPinsInternal();

    return result;
  }

  return false;
}

// Тест поддержки возможностей RSWP
uint8_t rswpSupportTest() {

  // Сброс конфигурационных пинов и состояния HV
  resetPinsInternal();

  // Сканирование шины I2C
  if (!scanBus()) {
    return 0;
  }

  // Значение поддерживаемой памяти
  uint8_t rswpSupport = 0;

  // Тест RSWP DDR5
  if (ddr5GetOfflineMode()) {
    rswpSupport |= DDR5;
  }

  // Тест RSWP VHV
  if (setHighVoltage(true)) {
    rswpSupport |= DDR4;

    // Тест RSWP SA1
    if ((setConfigPin(SA1_EN, true) && setConfigPin(SA1_EN, false))) {
      rswpSupport |= DDR3;
    }
  }

  resetPinsInternal();

  return rswpSupport;
}


/*  -=  Функции высокого напряжения (9V)  =-  */

// Управление источником HV (установить состояние на ON для включения, или OFF для выключения)
bool setHighVoltage(bool state) {

  digitalWrite(HV_EN, state);

  uint64_t timeout = millis() + 25;

  while (millis() < timeout) {
    if (getHighVoltage() == state) {
      return true;
    }
  }

  return false;
}

// Возврат статуса HV чтением HV_FB
bool getHighVoltage() {
  return getConfigPin(HV_FB);
}


/*  -=  Функции PSWP  =-  */

// Установка постоянной защиты записи на поддерживаемых EEPROM
bool setPswp(uint8_t address) {

  if (ddr4Detect(address) || ddr5Detect(address)) {
    return false;
  }

  // Сохранение битов адреса (SA0-SA2) и изменение битов 7-4 на '0110'
  uint8_t cmd = (address & 0b111) | (PWPB << 3);

  Wire.beginTransmission(cmd);
  // Запись 2 байтов DNC для установки LSB в 0
  Wire.write(DNC);
  Wire.write(DNC);
  int status = Wire.endTransmission();

  return status == 0;
}

// Чтение статуса постоянной защиты записи
bool getPswp(uint8_t address) {

  // Сохранение битов адреса (SA0-SA2) и изменение битов 7-4 на '0110'
  uint8_t cmd = (address & 0b111) | (PWPB << 3);

  Wire.beginTransmission(cmd);
  // Запись 1 байта DNC для установки LSB в 1
  Wire.write(DNC);
  int status = Wire.endTransmission();

  return status == 0;  // Возврат true если PSWP не установлен
}


/*  -=  Функции страниц EEPROM  =-  */

// Получение активного адреса страницы DDR4
uint8_t getPageAddress(bool lowLevel = false) {

  if (!lowLevel) {
    return eepromPageAddress;
  }

  int8_t status = -1;

  // Отправка условия старта
  TWCR = _BV(TWEN) | _BV(TWINT) | _BV(TWEA) | _BV(TWSTA);

  // Ожидание установки флага TWINT
  while (!(TWCR & (_BV(TWINT)))) {}

  // Ожидание старта
  while ((TWSR & 0xF8) != 0x08) {}

  // Загрузка команды RPA в регистр данных
  TWDR = RPA;

  // Передача адреса
  TWCR = _BV(TWEN) | _BV(TWEA) | _BV(TWINT);

  // Ожидание передачи адреса
  while (!(TWCR & (_BV(TWINT)))) {}

  // Проверка статуса (0x40 = ACK = страница 0; 0x48 = NACK = страница 1)
  status = (TWSR & 0xF8);

  // Запись 2xDNC после контрольного байта
  if (status == 0x40) {
    for (int i = 0; i < 2; i++) {
      TWDR = DNC;
      TWCR = _BV(TWEN) | _BV(TWEA) | _BV(TWINT);
      while (!(TWCR & (_BV(TWINT)))) {}
    }
  }

  // Отправка условия стоп
  TWCR = _BV(TWEN) | _BV(TWINT) | _BV(TWEA) | _BV(TWSTO);

  // Возврат результата
  switch (status) {
    case 0x40: return 0;
    case 0x48: return 1;
    default: return status;
  }
}

// Установка адреса страницы для доступа к нижним или верхним 256 байтам DDR4 SPD
void setPageAddress(uint8_t pageNumber) {
  probeDeviceTypeId((pageNumber == 0) ? SPA0 : SPA1);
  eepromPageAddress = pageNumber;
}

// Корректировка адреса страницы согласно указанному смещению байта
void adjustPageAddress(uint8_t address, uint16_t offset) {

  if (!validateAddress(address) || offset >= 1024) {
    return;
  }

  int8_t page;

  // Проверка наличия DDR5 и корректировка номера страницы и режима адресации
  if (ddr5Detect(address)) {
    page = offset >> 7;  // Страница DDR5

    if (readReg(address, MR11) == page) {
      return;
    }

    // Запись адреса страницы в MR11[2:0]
    Wire.beginTransmission(address);
    Wire.write(MR11);
    Wire.write(page);
    Wire.endTransmission(true);  // Требуется повторный старт для чтения DDR5 SPD5HUB EEPROM

    return;
  }

  // Предположение что присутствует DDR4
  if (offset < 512) {
    page = bitRead(offset, 8);  // Страница DDR4
    if (getPageAddress() != page) {
      setPageAddress(page);
      eepromPageAddress = page;
    }
  }
}


/*  -=  Функции настроек устройства  =-  */

// Назначение нового имени
bool setName(String name) {

  for (uint8_t i = 0; i < name.length(); i++) {
    EEPROM.update(i, name[i]);
  }
  EEPROM.update(name.length(), 0);

  return name == getName();
}

// Получение имени устройства
String getName() {

  char deviceNameChar[NAMELENGTH + 1];

  for (uint8_t i = 0; i < NAMELENGTH; i++) {
    deviceNameChar[i] = EEPROM.read(i);
  }
  // Установка последнего байта в ноль
  deviceNameChar[NAMELENGTH] = 0;

  return deviceNameChar;
}

// Чтение настроек устройства
bool getSettings(uint8_t name) {
  return bitRead(EEPROM.read(DEVICESETTINGS), name);
}

// Сохранение настроек устройства
bool saveSettings(uint8_t name, uint8_t value) {

  uint8_t currentSettings = EEPROM.read(DEVICESETTINGS);
  EEPROM.update(DEVICESETTINGS, bitWrite(currentSettings, name, value));

  return getSettings(name) == value;
}


/*  -=  Функции шины I2C  =-  */

// Установка режима частоты шины I2C
bool setI2cClockMode(bool mode) {
  saveSettings(CLOCKMODE, mode ? FASTMODE : STDMODE);
  Wire.setClock(clock[mode]);

  return getI2cClockMode() == mode;
}

// Получение сохраненного режима частоты I2C (true=быстрый режим, false=стандартный режим)
bool getI2cClockMode() {
  return getSettings(CLOCKMODE);
}

// Сканирование шины I2C в диапазоне 80-87
uint8_t scanBus() {
  return scanBus(80, 87);
}

// Сканирование шины I2C в указанном диапазоне
uint8_t scanBus(uint8_t startAddress, uint8_t endAddress) {

  uint8_t totalAddresses = endAddress - startAddress;

  if (totalAddresses > 7) {
    return 0;
  }

  uint8_t response = 0;

  for (uint8_t i = 0; i <= totalAddresses; i++) {
    if (probeBusAddress(i + startAddress)) {
      response |= 1 << i;
    }
  }

  return response;
}

// Получение количества устройств на шине I2C
uint8_t getQuantity() {
  return bitCount(scanBus());
}

// Получение количества битов в битовой маске
uint8_t bitCount(uint8_t bitMask) {
  if (bitMask == 0) {
    return 0;
  }

  uint8_t quantity = 0;

  for (uint8_t i = 0; i <= 7; i++) {
    if (bitRead(bitMask, i)) {
      quantity++;
    }
  }

  return quantity;
}

// Монитор I2C
void i2cMonitor() {

  if (cmdExecuting) {
    return;
  }

  bool i2cPause = false;

  // Мониторинг адреса Slave
  slaveCountCurrent = getQuantity();

  if (slaveCountCurrent != slaveCountLast) {
    uint8_t buffer[] = { ALERT, (uint8_t)(slaveCountCurrent < slaveCountLast ? SLAVEDEC : SLAVEINC) };
    PORT.write(buffer, sizeof(buffer));
    slaveCountLast = slaveCountCurrent;
    i2cPause = true;
  }

  // Мониторинг частоты I2C
  i2cClockCurrent = getI2cClockMode();

  if (i2cClockCurrent != i2cClockLast) {
    uint8_t buffer[] = { ALERT, (uint8_t)(i2cClockCurrent < i2cClockLast ? CLOCKDEC : CLOCKINC) };
    PORT.write(buffer, sizeof(buffer));
    i2cClockLast = i2cClockCurrent;
  }

  // Горячее подключение DDR5 не обнаруживается без этой паузы,
  // потому что PMIC за SPD5 Hub остается невидимым для probeBusAddress()
  if (i2cPause) {
    delay(10);
  }

  PORT.flush();
}

// Управление конфигурационными пинами
bool setConfigPin(uint8_t pin, bool state) {
  digitalWrite(pin, state);

  if (pin == SA1_EN) {

    uint64_t timeout = millis() + 10;

    while (millis() < timeout) {
      if (scanBus() & (state ? A1_MASK : ~A1_MASK)) {
        return true;
      }
    }

    return false;
  }

  return getConfigPin(pin) == state;
}

// Получение состояния конфигурационного пина
bool getConfigPin(uint8_t pin) {
  return pin == SA1_EN ? scanBus() & A1_MASK : digitalRead(pin);
}

// Сброс конфигурационных пинов с обратной связью
bool resetPins() {
  return setHighVoltage(false) && setConfigPin(SA1_EN, false);
}

// Сброс конфигурационных пинов без обратной связи
void resetPinsInternal() {
  for (uint8_t i = 0; i < pinCount; i++) {
    if (ConfigPin[i].mode == OUTPUT) {
      digitalWrite(ConfigPin[i].name, ConfigPin[i].defaultState);
    }
  }
}

// Получение режима offline DDR5
bool ddr5GetOfflineMode() {
  return ddr5Detect(80) && bitRead(readReg(80, MR48), 2);
}

// Тест присутствия адреса устройства на шине I2C
bool probeBusAddress(uint8_t address) {
  Wire.beginTransmission(address);
  return Wire.endTransmission(false) == 0;
}

// Тест возврата Device Select Code: ACK (true) или NACK (false)
bool probeDeviceTypeId(uint8_t deviceSelectCode) {

  uint8_t status = 0;

  // Проверка LSB DSC, если 0 (запись), нужно записать адрес DNC + данные DNC
  bool writeBit = !bitRead(deviceSelectCode, 0);

  // Библиотека Wire использует 7-битный адрес, поэтому мы отбрасываем LSB из DSC сдвигом вправо на 1
  uint8_t cmd = deviceSelectCode >> 1;

  Wire.beginTransmission(cmd);
  if (writeBit) {
    Wire.write(DNC);
    Wire.write(DNC);
  }
  status = Wire.endTransmission();

  if (writeBit) {
    return status == 0;
  }

  return Wire.requestFrom(cmd, (uint8_t)1) > 0;  // true когда получен ACK после контрольного байта
}

// Тест определения DDR4 (по адресу)
bool ddr4Detect(uint8_t address) {
  if (!address) {
    return ddr4Detect();
  }

  return probeBusAddress(address) && ddr4Detect() && !ddr5Detect(address);
}

// ============================================================================
// Тест определения DDR4 (общий) - ИСПРАВЛЕНО для совместимости с Samsung S34TS04A
// ============================================================================
// ПРОБЛЕМА: Samsung S34TS04A и подобные чипы имеют более медленное время отклика
//           на команды SPA (Set Page Address) и RPA (Read Page Address).
//           Это приводило к сбою ddr4Detect(), что не позволяло работать
//           блокам RSWP 1-3 (работал только блок 0).
//
// РЕШЕНИЕ: Двойной метод определения:
//          1. Сначала пробуем метод SPA/RPA с задержкой 10мс (для медленных чипов)
//          2. Если не сработало, используем резервный метод чтения SPD байта (стандарт JEDEC)
//
// ПОЧЕМУ ЭТО РАБОТАЕТ:
//          - FT34C04A: Быстрый отклик → проходит тест SPA/RPA сразу
//          - S34TS04A: Медленный отклик → задержка 10мс дает время на обработку
//          - Неизвестные чипы: Резервный метод через SPD байт всегда надежен
//
bool ddr4Detect() {
  // Метод 1: Тест SPA/RPA с задержкой для медленных чипов типа S34TS04A
  setPageAddress(0);
  delay(10);  // КРИТИЧНО: Ожидание обработки команды SPA медленными чипами (S34TS04A)
  
  // Проверка работает ли SPA/RPA
  if (getQuantity() > 0 && getPageAddress(true) == 0) {
    return true;  // Успех через метод SPA/RPA
  }
  
  // Метод 2: Резервный метод чтения SPD байта (более надежный)
  // Этот метод работает даже если timing SPA/RPA ненадежен
  return ddr4DetectBySpdByte();
}

// ============================================================================
// Альтернативное определение DDR4 чтением SPD байта 2 (Key Byte)
// ============================================================================
// Этот метод более надежен и не зависит от чипа, так как читает реальные
// данные SPD вместо использования команд EEPROM, зависящих от timing.
//
// Значения JEDEC SPD байта 2:
// - 0x0C = DDR4 SDRAM
// - 0x12 = DDR4E SDRAM
// - 0x0B = DDR3 SDRAM
// - 0x08 = DDR2 SDRAM
//
bool ddr4DetectBySpdByte() {
  uint8_t keyByte[1] = { 0 };
  
  // Попытка чтения SPD байта 2 со стандартного адреса 0x50
  if (readByte(0x50, 0x02, 1, keyByte)) {
    // Проверка на DDR4 или DDR4E
    return (keyByte[0] == 0x0C || keyByte[0] == 0x12);
  }
  
  return false;
}

// Тест определения DDR5 (по адресу)
bool ddr5Detect(uint8_t address) {

  if (!validateAddress(address) || !probeBusAddress(address)) {
    return false;
  }

  // Сброс страницы в 0 (фикс для SPD5118-Y1B000NCG)
  writeReg(address, MR11, 0);

  if (probeBusAddress((address & 0b111) | PMIC) && readReg(address, MR0) == highByte(SPD5_TS)) {
    uint8_t mr1 = readReg(address, MR1);
    return mr1 == lowByte(SPD5_TS) || mr1 == lowByte(SPD5_NO);
  }

  return false;
}

// Проверка адреса EEPROM
bool validateAddress(uint8_t address) {
  return address >> 3 == 0b1010;
}

// Восстановление настроек устройства по умолчанию
bool factoryReset() {
  for (uint8_t i = 0; i <= 32; i++) {
    EEPROM.update(i, 0);
  }
  for (uint8_t i = 0; i <= 32; i++) {
    if (EEPROM.read(i) != 0) {
      return false;
    }
  }
  return true;
}
