/*
 * Arduino SPD Scanner
 * Сканирует I2C шину и читает регистры микросхемы SPD (Serial Presence Detect)
 * 
 * Подключение:
 * - SDA -> A4 (Uno/Nano) или 20 (Mega)
 * - SCL -> A5 (Uno/Nano) или 21 (Mega)
 * - SPD EEPROM обычно на адресах 0x50-0x57
 * 
 * SPD EEPROM:
 * - Базовая область: 0x00-0xFF (256 байт)
 * - Расширенная область: 0x100-0x1FF (256 байт, требует переключения страниц)
 */

#include <Wire.h>

// Настройки
const int I2C_FREQ = 100000;  // 100 kHz (стандартная скорость для SPD)
const int SCAN_START = 0x08;
const int SCAN_END = 0x77;
const int SPD_BASE_ADDR = 0x50;  // Базовый адрес SPD (обычно 0x50-0x57 для разных слотов)

// Буфер для данных SPD
byte spd_data[512];
bool spd_found = false;
int spd_address = 0;

void setup() {
  Serial.begin(115200);
  while (!Serial) {
    ; // Ждем подключения Serial
  }
  
  Wire.begin();
  Wire.setClock(I2C_FREQ);
  
  Serial.println("========================================");
  Serial.println("Arduino SPD Scanner");
  Serial.println("========================================\n");
  
  delay(1000);
}

void loop() {
  // Сканирование I2C шины
  scanI2CBus();
  
  // Если найден SPD, читаем его
  if (spd_found) {
    Serial.println("\n========================================");
    Serial.println("Чтение SPD данных");
    Serial.println("========================================\n");
    
    readSPDBase(spd_address);
    readSPDExtended(spd_address);
    
    // Выводим данные
    printSPDData();
    
    // Анализ SPD
    analyzeSPD();
    
    // Сохранение данных в формате для Python скриптов
    saveSPDToFile();
  }
  
  Serial.println("\n========================================");
  Serial.println("Сканирование завершено");
  Serial.println("========================================\n");
  
  delay(5000);  // Пауза перед следующим сканированием
}

// Сканирование I2C шины
void scanI2CBus() {
  Serial.println("Сканирование I2C шины...");
  Serial.println("Адрес | Найдено устройство");
  Serial.println("------|-------------------");
  
  spd_found = false;
  int device_count = 0;
  
  for (byte address = SCAN_START; address <= SCAN_END; address++) {
    Wire.beginTransmission(address);
    byte error = Wire.endTransmission();
    
    if (error == 0) {
      Serial.print("0x");
      if (address < 16) Serial.print("0");
      Serial.print(address, HEX);
      Serial.print("  | ");
      
      // Проверяем, является ли это SPD устройством
      if (address >= 0x50 && address <= 0x57) {
        Serial.print("SPD EEPROM (DIMM ");
        Serial.print(address - 0x50);
        Serial.print(")");
        if (!spd_found) {
          spd_found = true;
          spd_address = address;
        }
      } else {
        Serial.print("Устройство");
      }
      
      Serial.println();
      device_count++;
    } else if (error == 4) {
      Serial.print("0x");
      if (address < 16) Serial.print("0");
      Serial.print(address, HEX);
      Serial.println("  | Ошибка (неизвестная)");
    }
  }
  
  Serial.print("\nНайдено устройств: ");
  Serial.println(device_count);
  
  if (spd_found) {
    Serial.print("SPD найден на адресе: 0x");
    Serial.println(spd_address, HEX);
  } else {
    Serial.println("SPD не найден!");
  }
}

// Чтение базовой области SPD (0x00-0xFF)
void readSPDBase(int address) {
  Serial.print("Чтение базовой области SPD (0x00-0xFF)... ");
  
  for (int i = 0; i < 256; i++) {
    Wire.beginTransmission(address);
    Wire.write(i);  // Адрес регистра
    byte error = Wire.endTransmission();
    
    if (error == 0) {
      Wire.requestFrom(address, (byte)1);
      if (Wire.available()) {
        spd_data[i] = Wire.read();
      } else {
        spd_data[i] = 0xFF;  // Ошибка чтения
      }
    } else {
      spd_data[i] = 0xFF;  // Ошибка записи адреса
    }
    
    delayMicroseconds(100);  // Небольшая задержка для стабильности
  }
  
  Serial.println("Готово");
}

// Чтение расширенной области SPD (0x100-0x1FF)
// Требует переключения страниц через регистр 0x7F
void readSPDExtended(int address) {
  Serial.print("Чтение расширенной области SPD (0x100-0x1FF)... ");
  
  // Читаем расширенную область по страницам
  for (int page = 0; page < 2; page++) {
    // Переключаем страницу через регистр 0x7F
    Wire.beginTransmission(address);
    Wire.write(0x7F);  // Регистр выбора страницы
    Wire.write(page);  // Номер страницы (0 или 1)
    Wire.endTransmission();
    delay(5);  // Задержка для переключения страницы
    
    // Читаем данные страницы (128 байт)
    int base_offset = 0x80 + (page * 128);
    for (int i = 0; i < 128; i++) {
      int reg_addr = 0x80 + i;
      Wire.beginTransmission(address);
      Wire.write(reg_addr);
      byte error = Wire.endTransmission();
      
      if (error == 0) {
        Wire.requestFrom(address, (byte)1);
        if (Wire.available()) {
          spd_data[base_offset + i] = Wire.read();
        } else {
          spd_data[base_offset + i] = 0xFF;
        }
      } else {
        spd_data[base_offset + i] = 0xFF;
      }
      
      delayMicroseconds(100);
    }
  }
  
  // Альтернативный метод: чтение напрямую через адресацию 0x100+
  // Некоторые EEPROM поддерживают 16-битную адресацию
  for (int i = 0; i < 256; i++) {
    int addr = 0x100 + i;
    Wire.beginTransmission(address);
    Wire.write((addr >> 8) & 0xFF);  // Старший байт адреса
    Wire.write(addr & 0xFF);          // Младший байт адреса
    byte error = Wire.endTransmission();
    
    if (error == 0) {
      Wire.requestFrom(address, (byte)1);
      if (Wire.available()) {
        byte value = Wire.read();
        // Сохраняем только если предыдущее значение было 0xFF (не прочитано)
        if (spd_data[addr] == 0xFF || spd_data[addr] == 0x00) {
          spd_data[addr] = value;
        }
      }
    }
    
    delayMicroseconds(100);
  }
  
  Serial.println("Готово");
}

// Вывод данных SPD в hex формате
void printSPDData() {
  Serial.println("\n========================================");
  Serial.println("Данные SPD (Hex dump)");
  Serial.println("========================================\n");
  
  // Базовая область (0x00-0xFF)
  Serial.println("Базовая область (0x00-0xFF):");
  printHexDump(0, 256);
  
  // Расширенная область (0x100-0x1FF)
  Serial.println("\nРасширенная область (0x100-0x1FF):");
  printHexDump(256, 256);
}

// Вывод hex dump
void printHexDump(int start, int length) {
  for (int i = 0; i < length; i += 16) {
    int addr = start + i;
    Serial.print("0x");
    if (addr < 0x100) Serial.print("0");
    if (addr < 0x10) Serial.print("0");
    Serial.print(addr, HEX);
    Serial.print(": ");
    
    // Hex значения
    for (int j = 0; j < 16; j++) {
      if (i + j < length) {
        byte b = spd_data[start + i + j];
        if (b < 0x10) Serial.print("0");
        Serial.print(b, HEX);
        Serial.print(" ");
      } else {
        Serial.print("   ");
      }
    }
    
    // ASCII значения
    Serial.print(" | ");
    for (int j = 0; j < 16; j++) {
      if (i + j < length) {
        byte b = spd_data[start + i + j];
        if (b >= 32 && b < 127) {
          Serial.print((char)b);
        } else {
          Serial.print(".");
        }
      }
    }
    
    Serial.println();
  }
}

// Анализ SPD данных
void analyzeSPD() {
  Serial.println("\n========================================");
  Serial.println("Анализ SPD данных");
  Serial.println("========================================\n");
  
  // Байт 0: Размер SPD
  Serial.print("Размер SPD: ");
  byte spd_size = spd_data[0];
  if (spd_size == 0) {
    Serial.println("Не определен");
  } else {
    Serial.print(spd_size * 128);
    Serial.println(" байт");
  }
  
  // Байт 1: Тип памяти
  Serial.print("Тип памяти: ");
  byte mem_type = spd_data[1];
  switch (mem_type) {
    case 0x0C: Serial.println("DDR4 SDRAM"); break;
    case 0x0B: Serial.println("DDR3 SDRAM"); break;
    case 0x08: Serial.println("DDR2 SDRAM"); break;
    default: 
      Serial.print("Неизвестный (0x");
      Serial.print(mem_type, HEX);
      Serial.println(")");
  }
  
  // Байт 2: Ревизия SPD
  Serial.print("Ревизия SPD: ");
  Serial.print((spd_data[2] >> 4) & 0x0F);
  Serial.print(".");
  Serial.println(spd_data[2] & 0x0F);
  
  // Байты 117-127: Производитель
  Serial.print("Производитель: ");
  for (int i = 117; i < 127; i++) {
    if (spd_data[i] != 0 && spd_data[i] != 0xFF) {
      Serial.print((char)spd_data[i]);
    }
  }
  Serial.println();
  
  // Байты 128-145: Part Number
  Serial.print("Part Number: ");
  for (int i = 128; i < 145; i++) {
    if (spd_data[i] != 0 && spd_data[i] != 0xFF) {
      Serial.print((char)spd_data[i]);
    }
  }
  Serial.println();
  
  // Байты 146-177: Serial Number
  Serial.print("Serial Number: ");
  for (int i = 146; i < 177; i++) {
    if (spd_data[i] != 0 && spd_data[i] != 0xFF) {
      Serial.print((char)spd_data[i]);
    }
  }
  Serial.println();
  
  // Байты 184-187: Secure ID (для HPE)
  Serial.print("Secure ID (0x184-0x187): ");
  Serial.print("0x");
  for (int i = 187; i >= 184; i--) {
    if (spd_data[i] < 0x10) Serial.print("0");
    Serial.print(spd_data[i], HEX);
  }
  Serial.println();
  
  // Байты 180-182: Маркер HPT
  Serial.print("Маркер (0x180-0x182): ");
  if (spd_data[0x180] == 0x48 && spd_data[0x181] == 0x50 && spd_data[0x182] == 0x54) {
    Serial.println("HPT (найден)");
  } else {
    Serial.print("0x");
    for (int i = 0x180; i <= 0x182; i++) {
      if (spd_data[i] < 0x10) Serial.print("0");
      Serial.print(spd_data[i], HEX);
      Serial.print(" ");
    }
    Serial.println();
  }
  
  // Вывод диапазонов для расчета Secure ID
  Serial.println("\nДиапазоны для расчета Secure ID:");
  Serial.println("SPD 0x7E-0xFF:");
  printHexRange(0x7E, 0xFF - 0x7E + 1);
  
  Serial.println("\nSPD 0x140-0x17F:");
  printHexRange(0x140, 0x17F - 0x140 + 1);
}

// Вывод диапазона в hex
void printHexRange(int start, int length) {
  for (int i = 0; i < length; i += 16) {
    int addr = start + i;
    Serial.print("0x");
    if (addr < 0x100) Serial.print("0");
    if (addr < 0x10) Serial.print("0");
    Serial.print(addr, HEX);
    Serial.print(": ");
    
    for (int j = 0; j < 16 && (i + j) < length; j++) {
      byte b = spd_data[start + i + j];
      if (b < 0x10) Serial.print("0");
      Serial.print(b, HEX);
      Serial.print(" ");
    }
    Serial.println();
  }
}

// Сохранение данных SPD в формате для Python скриптов
void saveSPDToFile() {
  Serial.println("\n========================================");
  Serial.println("Данные SPD для Python скриптов");
  Serial.println("========================================\n");
  
  Serial.println("// Скопируйте эти данные в файл .bin");
  Serial.println("// Или используйте для расчета Secure ID\n");
  
  // Вывод в формате массива байтов
  Serial.print("SPD_DATA = bytes([");
  for (int i = 0; i < 512; i++) {
    if (i > 0 && i % 16 == 0) {
      Serial.println();
      Serial.print("    ");
    }
    Serial.print("0x");
    if (spd_data[i] < 0x10) Serial.print("0");
    Serial.print(spd_data[i], HEX);
    if (i < 511) Serial.print(", ");
  }
  Serial.println("])");
  
  Serial.println("\n// Или в hex формате:");
  Serial.print("SPD_HEX = \"");
  for (int i = 0; i < 512; i++) {
    if (spd_data[i] < 0x10) Serial.print("0");
    Serial.print(spd_data[i], HEX);
  }
  Serial.println("\"");
  
  // Вывод критических диапазонов
  Serial.println("\n// Диапазон 0x7E-0xFF (для расчета Secure ID):");
  Serial.print("SPD_7E_FF = bytes([");
  for (int i = 0x7E; i <= 0xFF; i++) {
    if (i > 0x7E && (i - 0x7E) % 16 == 0) {
      Serial.println();
      Serial.print("    ");
    }
    Serial.print("0x");
    if (spd_data[i] < 0x10) Serial.print("0");
    Serial.print(spd_data[i], HEX);
    if (i < 0xFF) Serial.print(", ");
  }
  Serial.println("])");
  
  Serial.println("\n// Диапазон 0x140-0x17F (для расчета Secure ID):");
  Serial.print("SPD_140_17F = bytes([");
  for (int i = 0x140; i <= 0x17F; i++) {
    if (i > 0x140 && (i - 0x140) % 16 == 0) {
      Serial.println();
      Serial.print("    ");
    }
    Serial.print("0x");
    if (spd_data[i] < 0x10) Serial.print("0");
    Serial.print(spd_data[i], HEX);
    if (i < 0x17F) Serial.print(", ");
  }
  Serial.println("])");
  
  // Вывод Secure ID
  Serial.println("\n// Secure ID:");
  Serial.print("SECURE_ID = 0x");
  for (int i = 187; i >= 184; i--) {
    if (spd_data[i] < 0x10) Serial.print("0");
    Serial.print(spd_data[i], HEX);
  }
  Serial.println();
}

// Сохранение данных SPD в формате для Python скриптов
void saveSPDToFile() {
  Serial.println("\n========================================");
  Serial.println("Данные SPD для Python скриптов");
  Serial.println("========================================\n");
  
  Serial.println("// Скопируйте эти данные в файл .bin");
  Serial.println("// Или используйте для расчета Secure ID\n");
  
  // Вывод в формате массива байтов
  Serial.print("SPD_DATA = bytes([");
  for (int i = 0; i < 512; i++) {
    if (i > 0 && i % 16 == 0) {
      Serial.println();
      Serial.print("    ");
    }
    Serial.print("0x");
    if (spd_data[i] < 0x10) Serial.print("0");
    Serial.print(spd_data[i], HEX);
    if (i < 511) Serial.print(", ");
  }
  Serial.println("])");
  
  Serial.println("\n// Или в hex формате:");
  Serial.print("SPD_HEX = \"");
  for (int i = 0; i < 512; i++) {
    if (spd_data[i] < 0x10) Serial.print("0");
    Serial.print(spd_data[i], HEX);
  }
  Serial.println("\"");
  
  // Вывод критических диапазонов
  Serial.println("\n// Диапазон 0x7E-0xFF (для расчета Secure ID):");
  Serial.print("SPD_7E_FF = bytes([");
  for (int i = 0x7E; i <= 0xFF; i++) {
    if (i > 0x7E && (i - 0x7E) % 16 == 0) {
      Serial.println();
      Serial.print("    ");
    }
    Serial.print("0x");
    if (spd_data[i] < 0x10) Serial.print("0");
    Serial.print(spd_data[i], HEX);
    if (i < 0xFF) Serial.print(", ");
  }
  Serial.println("])");
  
  Serial.println("\n// Диапазон 0x140-0x17F (для расчета Secure ID):");
  Serial.print("SPD_140_17F = bytes([");
  for (int i = 0x140; i <= 0x17F; i++) {
    if (i > 0x140 && (i - 0x140) % 16 == 0) {
      Serial.println();
      Serial.print("    ");
    }
    Serial.print("0x");
    if (spd_data[i] < 0x10) Serial.print("0");
    Serial.print(spd_data[i], HEX);
    if (i < 0x17F) Serial.print(", ");
  }
  Serial.println("])");
  
  // Вывод Secure ID
  Serial.println("\n// Secure ID:");
  Serial.print("SECURE_ID = 0x");
  for (int i = 187; i >= 184; i--) {
    if (spd_data[i] < 0x10) Serial.print("0");
    Serial.print(spd_data[i], HEX);
  }
  Serial.println();
}

