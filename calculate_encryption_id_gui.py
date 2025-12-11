#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
GUI приложение для расчета Encryption ID для HP SmartMemory модулей

Это однофайловое приложение для расчета и проверки Encryption ID модулей
памяти HP SmartMemory DDR4. Приложение позволяет:
- Загружать SPD файлы модулей памяти
- Автоматически определять тип датчика температуры
- Рассчитывать Encryption ID на основе SPD данных и Sensor Registers
- Сравнивать рассчитанный Encryption ID с Secure ID из SPD
- Просматривать детальную информацию о данных расчета

Автор: Разработано на основе анализа BIOS HP
Версия: 1.0
"""

import tkinter as tk
from tkinter import ttk, filedialog, scrolledtext
import struct
import os
from datetime import datetime

# ============================================================================
# КОНСТАНТЫ И УТИЛИТЫ ДЛЯ РАСЧЕТА ENCRYPTION ID
# ============================================================================

# Секретный ключ для расчета Encryption ID
# Используется строка из кода BIOS (функция sub_233260C)
# Строка: "Copyright HP.  All rights reserved." (35 байт)
COPYRIGHT_STRING = b"Copyright HP.  All rights reserved."[:35]

# Размеры буферов для расчета
BUFFER_V69_SIZE = 244  # Размер буфера v69 в байтах
SPD_RANGE_1_START = 0x7E  # Начало первого диапазона SPD данных
SPD_RANGE_1_END = 0xFF  # Конец первого диапазона SPD данных
SPD_RANGE_1_SIZE = 130  # Размер первого диапазона (0x7E-0xFF)
SPD_RANGE_2_START = 0x140  # Начало второго диапазона SPD данных
SPD_RANGE_2_END = 0x180  # Конец второго диапазона SPD данных
SPD_RANGE_2_SIZE = 64  # Размер второго диапазона (0x140-0x17F)

# Адреса в SPD для чтения данных
SPD_HPT_MARKER_START = 0x181  # Начало маркера HPT
SPD_HPT_MARKER_END = 0x184  # Конец маркера HPT
SPD_SECURE_ID_START = 0x184  # Начало Secure ID
SPD_SECURE_ID_END = 0x188  # Конец Secure ID

# Параметры CRC32
CRC32_POLYNOMIAL = 0xD5828281  # Полином для CRC32 (MakeHpId)
CRC32_INITIAL_VALUE = 0xFFFFFFFF  # Начальное значение CRC32


# ============================================================================
# ФУНКЦИИ РАСЧЕТА ENCRYPTION ID
# ============================================================================

def calculate_crc32_makehpid(buffer, length):
    """
    Точная реализация CRC32 с полиномом 0xD5828281 (MakeHpId)
    
    Алгоритм основан на декомпилированном коде BIOS (функция sub_232F05C).
    Используется для расчета Encryption ID из буфера данных.
    
    Алгоритм:
    1. Инициализация CRC значением 0xFFFFFFFF
    2. Для каждого байта в буфере:
       - XOR CRC с байтом, сдвинутым на 8 бит влево
       - Выполнить 32 итерации:
         * Если старший бит установлен: crc = (crc * 2) ^ POLYNOMIAL
         * Иначе: crc = crc * 2
    
    Args:
        buffer: Буфер данных (bytes или bytearray)
        length: Длина данных для обработки (в байтах)
    
    Returns:
        int: Рассчитанное значение CRC32 (32-битное беззнаковое число)
    
    Пример:
        >>> buffer = bytearray(244)
        >>> crc = calculate_crc32_makehpid(buffer, 244)
        >>> print(f"CRC32: 0x{crc:08X}")
    """
    crc = CRC32_INITIAL_VALUE
    
    # Обработка каждого байта в буфере
    for i in range(length):
        # Явно конвертируем в Python int для избежания проблем с типами
        byte_val = int(buffer[i]) & 0xFF
        
        # XOR с байтом, сдвинутым на 8 бит влево
        crc ^= (byte_val << 8)
        crc &= 0xFFFFFFFF
        
        # 32 итерации обработки CRC
        for _ in range(32):
            if crc & 0x80000000:  # Старший бит установлен (v2 < 0 в signed)
                # Полином применяется при установленном старшем бите
                crc = ((crc * 2) ^ CRC32_POLYNOMIAL) & 0xFFFFFFFF
            else:  # Старший бит = 0 (v2 >= 0 в signed)
                # Просто сдвиг влево
                crc = (crc * 2) & 0xFFFFFFFF
    
    return crc & 0xFFFFFFFF


def build_buffer_v69(spd_data, sensor_reg_6, sensor_reg_7, rcd_data, secret_key):
    """
    Построение буфера v69 (244 байта) для расчета Encryption ID
    
    Структура буфера основана на анализе кода BIOS (функция sub_232F0BC):
    
    Структура буфера (244 байта):
    [0-129]:     SPD данные (0x7E-0xFF) - 130 байт
    [130-193]:   SPD Extended (0x140-0x17F) - 64 байта
    [194]:       Разделитель 0x20 (пробел)
    [195-196]:   Sensor Register 6 (Low byte, High byte)
    [197]:       Разделитель 0x20 (пробел)
    [198-199]:   Sensor Register 7 (Low byte, High byte)
    [200]:       Разделитель 0x20 (пробел)
    [201-208]:   RCD данные - 8 байт (0, 0, 0x20, 0, 0, 0x20, 0, 0x20)
    [209-243]:   Secret Key - 35 байт
    
    Args:
        spd_data: SPD данные модуля памяти (bytes или bytearray)
        sensor_reg_6: Sensor Register 6 (Manufacturer ID) - 16-битное значение
        sensor_reg_7: Sensor Register 7 (Device ID/Revision) - 16-битное значение
        rcd_data: RCD данные (не используется, всегда None)
        secret_key: Секретный ключ (35 байт)
    
    Returns:
        bytearray: Буфер размером 244 байта для расчета CRC32
    
    Примечание:
        Порядок байтов для Sensor Registers: Low byte, High byte
        Это связано с порядком байтов из I2C интерфейса
    """
    buffer = bytearray(BUFFER_V69_SIZE)
    offset = 0
    
    # ========================================================================
    # ШАГ 1: Копирование SPD данных
    # ========================================================================
    # Копируем два диапазона SPD данных:
    # 1. SPD[0x7E-0xFF] (130 байт) -> buffer[0-129]
    # 2. SPD[0x140-0x17F] (64 байта) -> buffer[130-193]
    
    if len(spd_data) >= SPD_RANGE_2_END:
        # Полный SPD: копируем оба диапазона
        buffer[offset:offset+SPD_RANGE_1_SIZE] = spd_data[SPD_RANGE_1_START:SPD_RANGE_1_END+1]
        offset += SPD_RANGE_1_SIZE
        
        buffer[offset:offset+SPD_RANGE_2_SIZE] = spd_data[SPD_RANGE_2_START:SPD_RANGE_2_END]
        offset += SPD_RANGE_2_SIZE
    else:
        # Неполный SPD: копируем доступные данные
        spd_size = BUFFER_V69_SIZE - (1 + 2 + 1 + 2 + 1 + 8 + 35)  # 194 байта
        if len(spd_data) >= spd_size:
            buffer[offset:offset+spd_size] = spd_data[0:spd_size]
            offset += spd_size
        else:
            buffer[offset:offset+len(spd_data)] = spd_data
            offset += len(spd_data)
    
    # ========================================================================
    # ШАГ 2: Разделитель перед Sensor Registers
    # ========================================================================
    buffer[offset] = 0x20  # Пробел (ASCII 0x20)
    offset += 1
    
    # ========================================================================
    # ШАГ 3: Sensor Register 6 (Manufacturer ID)
    # ========================================================================
    # Порядок байтов: Low byte, High byte (из I2C интерфейса)
    buffer[offset] = sensor_reg_6 & 0xFF  # Low byte
    buffer[offset+1] = (sensor_reg_6 >> 8) & 0xFF  # High byte
    offset += 2
    
    # Разделитель
    buffer[offset] = 0x20
    offset += 1
    
    # ========================================================================
    # ШАГ 4: Sensor Register 7 (Device ID/Revision)
    # ========================================================================
    buffer[offset] = sensor_reg_7 & 0xFF  # Low byte
    buffer[offset+1] = (sensor_reg_7 >> 8) & 0xFF  # High byte
    offset += 2
    
    # Разделитель
    buffer[offset] = 0x20
    offset += 1
    
    # ========================================================================
    # ШАГ 5: RCD данные (Register Clock Driver)
    # ========================================================================
    # Формат: Vendor ID (2 байта), разделитель, Device ID (2 байта),
    #         разделитель, Revision (1 байт), разделитель
    # В текущей реализации все значения равны 0
    buffer[offset:offset+8] = [0, 0, 0x20, 0, 0, 0x20, 0, 0x20]
    offset += 8
    
    # ========================================================================
    # ШАГ 6: Секретный ключ
    # ========================================================================
    key_bytes = secret_key[:35]
    buffer[offset:offset+len(key_bytes)] = key_bytes
    
    return buffer


def read_secure_id(spd_data):
    """
    Чтение Secure ID из SPD данных модуля памяти
    
    Secure ID хранится в SPD по адресам 0x184-0x187 в формате little-endian.
    Это 32-битное значение, которое должно совпадать с рассчитанным Encryption ID.
    
    Args:
        spd_data: SPD данные модуля памяти (bytes или bytearray)
    
    Returns:
        int: Secure ID (32-битное значение) или None, если данных недостаточно
    
    Пример:
        >>> spd_data = bytearray(512)
        >>> secure_id = read_secure_id(spd_data)
        >>> if secure_id:
        ...     print(f"Secure ID: 0x{secure_id:08X}")
    """
    if len(spd_data) > SPD_SECURE_ID_START + 3:
        # Читаем 4 байта в формате little-endian (unsigned int)
        secure_id = struct.unpack('<I', spd_data[SPD_SECURE_ID_START:SPD_SECURE_ID_END])[0]
        return secure_id
    return None


def check_hpt_marker(spd_data):
    """
    Проверка наличия маркера HPT в SPD данных
    
    Маркер HPT (HP Technology) находится в SPD по адресам 0x181-0x183.
    Наличие этого маркера указывает на то, что модуль является HP SmartMemory.
    
    Args:
        spd_data: SPD данные модуля памяти (bytes или bytearray)
    
    Returns:
        bool: True, если маркер найден, False в противном случае
    
    Примечание:
        Проверяется полный маркер "HPT" или неполный "PT" (для совместимости)
    """
    if len(spd_data) > SPD_HPT_MARKER_END - 1:
        hpt_marker = spd_data[SPD_HPT_MARKER_START:SPD_HPT_MARKER_END]
        # Проверяем полный маркер "HPT" или неполный "PT"
        return hpt_marker == b'HPT' or (hpt_marker[0] == 0x50 and hpt_marker[1] == 0x54)
    return False


# ============================================================================
# КЛАСС ГЛАВНОГО ПРИЛОЖЕНИЯ
# ============================================================================

class EncryptionIDCalculator:
    """
    Главный класс GUI приложения для расчета Encryption ID
    
    Приложение предоставляет графический интерфейс для:
    - Загрузки SPD файлов модулей памяти
    - Выбора или автоматического определения Sensor Registers
    - Расчета Encryption ID
    - Сравнения с Secure ID из SPD
    - Просмотра детальной информации о расчете
    """
    
    def __init__(self, root):
        """
        Инициализация приложения
        
        Args:
            root: Корневое окно Tkinter
        """
        self.root = root
        self.root.title("Калькулятор Encryption ID для HP SmartMemory")
        self.root.geometry("800x700")
        
        # Данные SPD файла
        self.spd_data = None
        self.spd_file_path = None
        
        # Предустановленные значения Sensor Registers
        # Формат: "Название датчика (Reg6, Reg7)": ("Reg6_hex", "Reg7_hex")
        self.preset_values = {
            "S34TS04A - Ablic (1C85, 2221)": ("1C85", "2221"),
            "STTS2004 - STMicroelectronics (104A, 2201)": ("104A", "2201"),
            "MCP98244 - Microchip (0054, 2201)": ("0054", "2201"),
            "TSE2004GB2B0 - Renesas (00F8, EE25)": ("00F8", "EE25")
        }
        
        # Создание интерфейса
        self.create_widgets()
    
    # ========================================================================
    # СОЗДАНИЕ ИНТЕРФЕЙСА
    # ========================================================================
    
    def create_widgets(self):
        """
        Создание всех виджетов интерфейса
        
        Метод создает все элементы GUI:
        - Заголовок приложения
        - Фрейм для выбора файла SPD
        - Фрейм для Sensor Registers
        - Кнопки управления
        - Фрейм для отображения результатов
        - Фрейм для лога
        """
        self._create_title()
        self._create_file_selection_frame()
        self._create_sensor_registers_frame()
        self._create_calculate_button()
        self._create_results_frame()
        self._create_log_frame()
        
        # Инициализация лога
        self.log("Приложение запущено")
        self.log("Готово к расчету Encryption ID")
        
        # Применение начальных значений
        self.on_preset_selected(None)
    
    def _create_title(self):
        """Создание заголовка приложения"""
        title_label = tk.Label(
            self.root,
            text="Калькулятор Encryption ID для HP SmartMemory",
            font=("Arial", 16, "bold"),
            pady=10
        )
        title_label.pack()
    
    def _create_file_selection_frame(self):
        """Создание фрейма для выбора файла SPD"""
        file_frame = ttk.LabelFrame(self.root, text="Файл SPD", padding=10)
        file_frame.pack(fill=tk.X, padx=10, pady=5)
        
        # Метка с именем файла
        self.file_label = tk.Label(
            file_frame,
            text="Файл не выбран",
            anchor=tk.W,
            bg="#f0f0f0",
            relief=tk.SUNKEN,
            padx=5,
            pady=5
        )
        self.file_label.pack(fill=tk.X, side=tk.LEFT, expand=True)
        
        # Кнопка выбора файла
        browse_btn = ttk.Button(
            file_frame,
            text="Обзор...",
            command=self.browse_file
        )
        browse_btn.pack(side=tk.RIGHT, padx=(5, 0))
    
    def _create_sensor_registers_frame(self):
        """Создание фрейма для Sensor Registers"""
        sensor_frame = ttk.LabelFrame(self.root, text="Sensor Registers", padding=10)
        sensor_frame.pack(fill=tk.X, padx=10, pady=5)
        
        # Выбор предустановленного значения
        self._create_preset_selector(sensor_frame)
        
        # Поля ввода Sensor Register 6 и 7
        self._create_register_inputs(sensor_frame)
        
        # Кнопка автоматического определения
        self._create_auto_detect_button(sensor_frame)
    
    def _create_preset_selector(self, parent):
        """Создание селектора предустановленных значений"""
        preset_frame = tk.Frame(parent)
        preset_frame.pack(fill=tk.X, pady=5)
        
        tk.Label(preset_frame, text="Предустановка:", width=20, anchor=tk.W).pack(side=tk.LEFT)
        
        self.preset_combo = ttk.Combobox(
            preset_frame,
            values=list(self.preset_values.keys()),
            state="readonly",
            width=45
        )
        self.preset_combo.pack(side=tk.LEFT, padx=5)
        self.preset_combo.set("S34TS04A - Ablic (1C85, 2221)")
        self.preset_combo.bind("<<ComboboxSelected>>", self.on_preset_selected)
    
    def _create_register_inputs(self, parent):
        """Создание полей ввода для Sensor Registers"""
        # Sensor Register 6
        reg6_frame = tk.Frame(parent)
        reg6_frame.pack(fill=tk.X, pady=5)
        
        tk.Label(reg6_frame, text="Sensor Register 6:", width=20, anchor=tk.W).pack(side=tk.LEFT)
        self.reg6_entry = tk.Entry(reg6_frame, width=10)
        self.reg6_entry.pack(side=tk.LEFT, padx=5)
        tk.Label(reg6_frame, text="(High, Low)", font=("Arial", 9), fg="gray").pack(side=tk.LEFT, padx=5)
        
        # Sensor Register 7
        reg7_frame = tk.Frame(parent)
        reg7_frame.pack(fill=tk.X, pady=5)
        
        tk.Label(reg7_frame, text="Sensor Register 7:", width=20, anchor=tk.W).pack(side=tk.LEFT)
        self.reg7_entry = tk.Entry(reg7_frame, width=10)
        self.reg7_entry.pack(side=tk.LEFT, padx=5)
        tk.Label(reg7_frame, text="(High, Low)", font=("Arial", 9), fg="gray").pack(side=tk.LEFT, padx=5)
    
    def _create_auto_detect_button(self, parent):
        """Создание кнопки автоматического определения"""
        auto_detect_frame = tk.Frame(parent)
        auto_detect_frame.pack(fill=tk.X, pady=5)
        
        auto_detect_btn = ttk.Button(
            auto_detect_frame,
            text="Автоматически определить",
            command=self.auto_detect_sensor_registers,
            width=25
        )
        auto_detect_btn.pack(side=tk.LEFT, padx=5)
    
    def _create_calculate_button(self):
        """Создание кнопки расчета"""
        calc_frame = tk.Frame(self.root)
        calc_frame.pack(pady=10)
        
        calc_btn = ttk.Button(
            calc_frame,
            text="Рассчитать Encryption ID",
            command=self.calculate,
            width=25
        )
        calc_btn.pack()
    
    def _create_results_frame(self):
        """Создание фрейма для отображения результатов"""
        results_frame = ttk.LabelFrame(self.root, text="Результаты", padding=10)
        results_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=5)
        
        results_inner = tk.Frame(results_frame)
        results_inner.pack(fill=tk.BOTH, expand=True)
        
        # Secure ID
        self._create_result_label(results_inner, "Secure ID:", "secure_id_label")
        
        # Encryption ID
        self._create_result_label(results_inner, "Encryption ID:", "enc_id_label")
        
        # Совпадение
        self._create_result_label(results_inner, "Совпадение:", "match_label")
        
        # Кнопка показа таблицы данных
        table_btn = ttk.Button(
            results_inner,
            text="Показать таблицу данных",
            command=self.show_data_table
        )
        table_btn.pack(pady=5)
    
    def _create_result_label(self, parent, label_text, attr_name):
        """
        Создание метки для отображения результата
        
        Args:
            parent: Родительский виджет
            label_text: Текст метки
            attr_name: Имя атрибута для хранения виджета Label
        """
        frame = tk.Frame(parent)
        frame.pack(fill=tk.X, pady=5)
        
        tk.Label(frame, text=label_text, width=15, anchor=tk.W).pack(side=tk.LEFT)
        label = tk.Label(frame, text="Н/Д", font=("Courier", 10), anchor=tk.W)
        label.pack(side=tk.LEFT, padx=5)
        setattr(self, attr_name, label)
    
    def _create_log_frame(self):
        """Создание фрейма для лога"""
        log_frame = ttk.LabelFrame(self.root, text="Лог", padding=10)
        log_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=5)
        
        # Текстовое поле с прокруткой
        self.log_text = scrolledtext.ScrolledText(
            log_frame,
            height=10,
            wrap=tk.WORD,
            font=("Courier", 9)
        )
        self.log_text.pack(fill=tk.BOTH, expand=True)
        
        # Кнопка очистки лога
        clear_btn = ttk.Button(
            log_frame,
            text="Очистить лог",
            command=self.clear_log
        )
        clear_btn.pack(pady=5)
    
    # ========================================================================
    # ОБРАБОТЧИКИ СОБЫТИЙ
    # ========================================================================
    
    def on_preset_selected(self, event):
        """
        Обработчик выбора предустановленного значения Sensor Registers
        
        При выборе значения из списка предустановок автоматически заполняются
        поля ввода Sensor Register 6 и 7.
        
        Args:
            event: Событие выбора (может быть None при инициализации)
        """
        selected = self.preset_combo.get()
        if selected in self.preset_values:
            reg6, reg7 = self.preset_values[selected]
            
            # Обновление полей ввода
            self.reg6_entry.delete(0, tk.END)
            self.reg6_entry.insert(0, reg6)
            self.reg7_entry.delete(0, tk.END)
            self.reg7_entry.insert(0, reg7)
            
            # Логирование (только если log_text уже создан)
            if hasattr(self, 'log_text'):
                self.log(f"Выбрана предустановка: {selected}")
    
    def browse_file(self):
        """
        Открытие диалога выбора файла SPD
        
        Позволяет пользователю выбрать бинарный файл SPD для анализа.
        После выбора файл автоматически загружается.
        """
        filename = filedialog.askopenfilename(
            title="Выберите файл SPD",
            filetypes=[
                ("Бинарные файлы", "*.bin"),
                ("Все файлы", "*.*")
            ]
        )
        
        if filename:
            self.spd_file_path = filename
            self.file_label.config(text=os.path.basename(filename))
            self.load_spd_file(filename)
    
    # ========================================================================
    # РАБОТА С ФАЙЛАМИ
    # ========================================================================
    
    def load_spd_file(self, filename):
        """
        Загрузка и анализ SPD файла
        
        Загружает SPD файл, проверяет его структуру и извлекает информацию:
        - Размер файла
        - Наличие маркера HPT
        - Значение Secure ID
        
        Args:
            filename: Путь к файлу SPD
        """
        try:
            # Чтение файла
            with open(filename, 'rb') as f:
                self.spd_data = f.read()
            
            self.log(f"Файл загружен: {os.path.basename(filename)}")
            self.log(f"Размер SPD: {len(self.spd_data)} байт")
            
            # Проверка маркера HPT
            self._check_hpt_marker()
            
            # Чтение Secure ID
            self._read_and_log_secure_id()
            
        except Exception as e:
            self.log(f"[ERROR] Ошибка загрузки файла: {str(e)}")
            self.spd_data = None
    
    def _check_hpt_marker(self):
        """Проверка и логирование маркера HPT"""
        has_hpt = check_hpt_marker(self.spd_data)
        if has_hpt:
            self.log("[OK] Маркер HPT найден в SPD[0x181-0x183]")
        else:
            if len(self.spd_data) > SPD_HPT_MARKER_END - 1:
                hpt_marker = self.spd_data[SPD_HPT_MARKER_START:SPD_HPT_MARKER_END]
                self.log(f"[WARN] Маркер HPT не найден: {hpt_marker.hex()} ({hpt_marker})")
            else:
                self.log("[WARN] SPD слишком короткий для проверки маркера HPT")
    
    def _read_and_log_secure_id(self):
        """Чтение и логирование Secure ID"""
        secure_id = read_secure_id(self.spd_data)
        if secure_id:
            self.log(f"Secure ID из SPD[0x184-0x187]: 0x{secure_id:08X}")
        else:
            self.log("[ERROR] Secure ID не найден в SPD[0x184-0x187]")
    
    # ========================================================================
    # РАСЧЕТ ENCRYPTION ID
    # ========================================================================
    
    def calculate(self):
        """
        Расчет Encryption ID на основе SPD данных и Sensor Registers
        
        Выполняет полный цикл расчета:
        1. Проверка наличия SPD данных
        2. Валидация Sensor Registers
        3. Построение буфера v69
        4. Расчет CRC32 (Encryption ID)
        5. Сравнение с Secure ID из SPD
        6. Отображение результатов
        """
        # Проверка наличия SPD данных
        if not self.spd_data:
            self.log("[ERROR] Файл SPD не загружен")
            return
        
        # Получение и валидация Sensor Registers
        sensor_reg_6, sensor_reg_7 = self._get_sensor_registers()
        if sensor_reg_6 is None or sensor_reg_7 is None:
            return  # Ошибка уже залогирована
        
        # Логирование начала расчета
        self._log_calculation_start(sensor_reg_6, sensor_reg_7)
        
        # Чтение Secure ID
        secure_id = read_secure_id(self.spd_data)
        if not secure_id:
            self._handle_secure_id_error()
            return
        
        # Отображение Secure ID
        self.secure_id_label.config(text=f"0x{secure_id:08X}", fg="black")
        
        # Проверка размера SPD
        if len(self.spd_data) < SPD_SECURE_ID_END:
            self.log(f"[WARN] Размер SPD ({len(self.spd_data)} байт) меньше требуемого ({SPD_SECURE_ID_END} байта)")
        
        # Расчет Encryption ID
        try:
            encryption_id = self._perform_calculation(sensor_reg_6, sensor_reg_7)
            
            # Проверка совпадения
            self._check_match(encryption_id, secure_id)
            
            # Дополнительная информация
            self._log_additional_info()
            
        except Exception as e:
            self._handle_calculation_error(e)
        
        self.log("=" * 60)
    
    def _get_sensor_registers(self):
        """
        Получение и валидация Sensor Registers из полей ввода
        
        Returns:
            tuple: (sensor_reg_6, sensor_reg_7) или (None, None) при ошибке
        """
        try:
            reg6_str = self.reg6_entry.get().strip()
            reg7_str = self.reg7_entry.get().strip()
            
            if not reg6_str or not reg7_str:
                self.log("[ERROR] Sensor Registers не указаны")
                return None, None
            
            sensor_reg_6 = int(reg6_str, 16)
            sensor_reg_7 = int(reg7_str, 16)
            
            return sensor_reg_6, sensor_reg_7
            
        except ValueError as e:
            self.log(f"[ERROR] Неверный формат Sensor Register: {str(e)}")
            return None, None
    
    def _log_calculation_start(self, sensor_reg_6, sensor_reg_7):
        """Логирование начала расчета"""
        self.log("=" * 60)
        self.log(f"Расчет начат: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
        self.log(f"Sensor Register 6: 0x{sensor_reg_6:04X}")
        self.log(f"Sensor Register 7: 0x{sensor_reg_7:04X}")
    
    def _handle_secure_id_error(self):
        """Обработка ошибки чтения Secure ID"""
        self.log("[ERROR] Secure ID не найден в SPD")
        self.secure_id_label.config(text="Н/Д", fg="red")
        self.enc_id_label.config(text="Н/Д", fg="red")
        self.match_label.config(text="Н/Д", fg="red")
    
    def _perform_calculation(self, sensor_reg_6, sensor_reg_7):
        """
        Выполнение расчета Encryption ID
        
        Args:
            sensor_reg_6: Sensor Register 6
            sensor_reg_7: Sensor Register 7
        
        Returns:
            int: Рассчитанный Encryption ID
        """
        rcd_data = None
        buffer = build_buffer_v69(self.spd_data, sensor_reg_6, sensor_reg_7, rcd_data, COPYRIGHT_STRING)
        encryption_id = calculate_crc32_makehpid(buffer, BUFFER_V69_SIZE)
        
        # Отображение результата
        self.enc_id_label.config(text=f"0x{encryption_id:08X}", fg="black")
        
        return encryption_id
    
    def _check_match(self, encryption_id, secure_id):
        """
        Проверка совпадения Encryption ID и Secure ID
        
        Args:
            encryption_id: Рассчитанный Encryption ID
            secure_id: Secure ID из SPD
        """
        match = (encryption_id == secure_id)
        
        if match:
            self.match_label.config(text="ДА", fg="green")
            self.log(f"[OK] Encryption ID: 0x{encryption_id:08X}")
            self.log(f"[OK] Secure ID:     0x{secure_id:08X}")
            self.log("[OK] СОВПАДЕНИЕ! Encryption ID равен Secure ID")
        else:
            self.match_label.config(text="НЕТ", fg="red")
            diff = encryption_id ^ secure_id
            self.log(f"[FAIL] Encryption ID: 0x{encryption_id:08X}")
            self.log(f"[FAIL] Secure ID:     0x{secure_id:08X}")
            self.log(f"[FAIL] Разница:       0x{diff:08X}")
            self.log("[FAIL] Encryption ID НЕ совпадает с Secure ID")
    
    def _log_additional_info(self):
        """Логирование дополнительной информации о расчете"""
        self.log(f"Размер буфера: {BUFFER_V69_SIZE} байт")
        self.log(f"Секретный ключ: '{COPYRIGHT_STRING.decode('ascii', errors='replace')}'")
    
    def _handle_calculation_error(self, error):
        """Обработка ошибки расчета"""
        self.log(f"[ERROR] Ошибка расчета: {str(error)}")
        import traceback
        self.log(traceback.format_exc())
        self.enc_id_label.config(text="ОШИБКА", fg="red")
        self.match_label.config(text="ОШИБКА", fg="red")
    
    # ========================================================================
    # АВТОМАТИЧЕСКОЕ ОПРЕДЕЛЕНИЕ SENSOR REGISTERS
    # ========================================================================
    
    def auto_detect_sensor_registers(self):
        """
        Автоматическое определение Sensor Registers путем перебора известных значений
        
        Метод перебирает все известные предустановленные значения Sensor Registers
        и проверяет, какое из них дает совпадение Encryption ID с Secure ID.
        
        Процесс:
        1. Чтение Secure ID из SPD
        2. Перебор всех известных значений
        3. Для каждого значения: расчет Encryption ID
        4. При совпадении: обновление полей ввода и результатов
        """
        # Проверка наличия SPD данных
        if not self.spd_data:
            self.log("[ERROR] Файл SPD не загружен")
            return
        
        # Чтение Secure ID
        secure_id = read_secure_id(self.spd_data)
        if not secure_id:
            self.log("[ERROR] Secure ID не найден в SPD")
            return
        
        # Логирование начала процесса
        self.log("=" * 60)
        self.log("Автоматическое определение Sensor Registers")
        self.log(f"Secure ID из SPD: 0x{secure_id:08X}")
        self.log(f"Проверка {len(self.preset_values)} известных вариантов...")
        self.log("")
        
        found = False
        rcd_data = None
        
        # Перебор всех известных значений
        for preset_name, (reg6_str, reg7_str) in self.preset_values.items():
            try:
                # Конвертация строковых значений в числа
                sensor_reg_6 = int(reg6_str, 16)
                sensor_reg_7 = int(reg7_str, 16)
                
                # Построение буфера и расчет Encryption ID
                buffer = build_buffer_v69(self.spd_data, sensor_reg_6, sensor_reg_7, rcd_data, COPYRIGHT_STRING)
                encryption_id = calculate_crc32_makehpid(buffer, BUFFER_V69_SIZE)
                
                # Проверка совпадения
                if encryption_id == secure_id:
                    # Найдено совпадение!
                    found = True
                    self._handle_auto_detect_success(
                        preset_name, sensor_reg_6, sensor_reg_7,
                        reg6_str, reg7_str, encryption_id, secure_id
                    )
                    break
                else:
                    # Совпадение не найдено - логируем для отладки
                    self.log(f"[ ] {preset_name}: 0x{encryption_id:08X} != 0x{secure_id:08X}")
                    
            except Exception as e:
                self.log(f"[ERROR] Ошибка при проверке {preset_name}: {str(e)}")
                continue
        
        # Обработка случая, когда совпадение не найдено
        if not found:
            self._handle_auto_detect_failure(secure_id)
        
        self.log("=" * 60)
    
    def _handle_auto_detect_success(self, preset_name, sensor_reg_6, sensor_reg_7,
                                     reg6_str, reg7_str, encryption_id, secure_id):
        """
        Обработка успешного автоматического определения
        
        Args:
            preset_name: Название найденной предустановки
            sensor_reg_6: Значение Sensor Register 6
            sensor_reg_7: Значение Sensor Register 7
            reg6_str: Строковое представление Reg6
            reg7_str: Строковое представление Reg7
            encryption_id: Рассчитанный Encryption ID
            secure_id: Secure ID из SPD
        """
        # Логирование успеха
        self.log(f"[OK] Найдено совпадение!")
        self.log(f"    Вариант: {preset_name}")
        self.log(f"    Sensor Register 6: 0x{sensor_reg_6:04X}")
        self.log(f"    Sensor Register 7: 0x{sensor_reg_7:04X}")
        self.log(f"    Encryption ID: 0x{encryption_id:08X}")
        self.log(f"    Secure ID: 0x{secure_id:08X}")
        
        # Обновление полей ввода
        self.reg6_entry.delete(0, tk.END)
        self.reg6_entry.insert(0, reg6_str)
        self.reg7_entry.delete(0, tk.END)
        self.reg7_entry.insert(0, reg7_str)
        
        # Выбор соответствующего варианта в комбобоксе
        self.preset_combo.set(preset_name)
        
        # Обновление результатов
        self.secure_id_label.config(text=f"0x{secure_id:08X}", fg="black")
        self.enc_id_label.config(text=f"0x{encryption_id:08X}", fg="black")
        self.match_label.config(text="ДА", fg="green")
    
    def _handle_auto_detect_failure(self, secure_id):
        """
        Обработка неудачного автоматического определения
        
        Args:
            secure_id: Secure ID из SPD
        """
        self.log("[FAIL] Совпадение не найдено среди известных вариантов")
        self.log("      Возможно, используется неизвестный датчик температуры")
        self.secure_id_label.config(text=f"0x{secure_id:08X}", fg="black")
        self.enc_id_label.config(text="Н/Д", fg="red")
        self.match_label.config(text="НЕТ", fg="red")
    
    # ========================================================================
    # РАБОТА С ЛОГОМ
    # ========================================================================
    
    def log(self, message):
        """
        Добавление сообщения в лог с временной меткой
        
        Args:
            message: Текст сообщения для логирования
        """
        timestamp = datetime.now().strftime('%H:%M:%S')
        self.log_text.insert(tk.END, f"[{timestamp}] {message}\n")
        self.log_text.see(tk.END)  # Прокрутка к концу
    
    def clear_log(self):
        """Очистка лога"""
        self.log_text.delete(1.0, tk.END)
        self.log("Лог очищен")
    
    # ========================================================================
    # ОТОБРАЖЕНИЕ ТАБЛИЦЫ ДАННЫХ
    # ========================================================================
    
    def show_data_table(self):
        """
        Отображение детальной таблицы данных, используемых для расчета
        
        Создает новое окно с подробной информацией о:
        - Параметрах CRC32
        - SPD данных
        - Sensor Registers
        - RCD данных
        - Секретном ключе
        - Структуре буфера
        - Результатах расчета
        """
        # Проверка наличия SPD данных
        if not self.spd_data:
            self.log("[ERROR] Файл SPD не загружен")
            return
        
        # Получение Sensor Registers
        sensor_reg_6, sensor_reg_7 = self._get_sensor_registers()
        if sensor_reg_6 is None or sensor_reg_7 is None:
            return  # Ошибка уже залогирована
        
        # Создание окна с таблицей
        table_window = self._create_table_window()
        
        # Построение буфера для анализа
        rcd_data = None
        buffer = build_buffer_v69(self.spd_data, sensor_reg_6, sensor_reg_7, rcd_data, COPYRIGHT_STRING)
        
        # Формирование и отображение таблицы
        lines = self._build_data_table_lines(buffer, sensor_reg_6, sensor_reg_7)
        self._display_table_data(table_window, lines)
        
        self.log("Таблица данных открыта")
    
    def _create_table_window(self):
        """
        Создание окна для отображения таблицы данных
        
        Returns:
            tk.Toplevel: Окно с таблицей данных
        """
        table_window = tk.Toplevel(self.root)
        table_window.title("Таблица данных для расчета")
        table_window.geometry("900x600")
        
        # Текстовое поле с прокруткой
        text_frame = tk.Frame(table_window)
        text_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        text_widget = scrolledtext.ScrolledText(
            text_frame,
            wrap=tk.WORD,
            font=("Courier", 9)
        )
        text_widget.pack(fill=tk.BOTH, expand=True)
        
        # Сохранение ссылки на виджет для использования в _display_table_data
        table_window.text_widget = text_widget
        
        # Кнопка закрытия
        close_btn = ttk.Button(
            table_window,
            text="Закрыть",
            command=table_window.destroy
        )
        close_btn.pack(pady=5)
        
        return table_window
    
    def _build_data_table_lines(self, buffer, sensor_reg_6, sensor_reg_7):
        """
        Построение строк таблицы данных
        
        Args:
            buffer: Буфер данных для расчета
            sensor_reg_6: Sensor Register 6
            sensor_reg_7: Sensor Register 7
        
        Returns:
            list: Список строк для отображения в таблице
        """
        lines = []
        
        # Заголовок
        lines.append("=" * 80)
        lines.append("ТАБЛИЦА ДАННЫХ ДЛЯ РАСЧЕТА ENCRYPTION ID")
        lines.append("=" * 80)
        lines.append("")
        
        # 1. Параметры CRC32
        lines.extend(self._build_crc32_section(buffer))
        
        # 2. SPD данные
        lines.extend(self._build_spd_section())
        
        # 3. Sensor Registers
        lines.extend(self._build_sensor_registers_section(sensor_reg_6, sensor_reg_7))
        
        # 4. RCD данные
        lines.extend(self._build_rcd_section(buffer))
        
        # 5. Secret Key
        lines.extend(self._build_secret_key_section())
        
        # 6. Структура буфера
        lines.extend(self._build_buffer_structure_section(buffer))
        
        # 7. Первые и последние байты буфера
        lines.extend(self._build_buffer_bytes_section(buffer))
        
        # 8. Результат расчета
        lines.extend(self._build_calculation_result_section(buffer))
        
        # Завершение
        lines.append("")
        lines.append("=" * 80)
        
        return lines
    
    def _build_crc32_section(self, buffer):
        """Построение секции параметров CRC32"""
        lines = []
        lines.append("1. ПАРАМЕТРЫ CRC32 (MakeHpId):")
        lines.append("-" * 80)
        lines.append(f"  Полином:           0x{CRC32_POLYNOMIAL:08X}")
        lines.append(f"  Начальное значение: 0x{CRC32_INITIAL_VALUE:08X}")
        lines.append(f"  Размер данных:      {len(buffer)} байт")
        lines.append("")
        return lines
    
    def _build_spd_section(self):
        """Построение секции SPD данных (все байты полностью)"""
        lines = []
        lines.append("2. SPD ДАННЫЕ:")
        lines.append("-" * 80)
        lines.append(f"  Размер SPD:         {len(self.spd_data)} байт")
        
        # Диапазон 1: SPD[0x7E-0xFF] (130 байт)
        if len(self.spd_data) >= SPD_RANGE_1_END + 1:
            lines.append(f"  SPD[0x7E-0xFF]:     {SPD_RANGE_1_SIZE} байт (индексы 0-129 в буфере)")
            spd_range1 = self.spd_data[SPD_RANGE_1_START:SPD_RANGE_1_END+1]
            # Показываем все байты диапазона по 16 байт в строке
            for i in range(0, len(spd_range1), 16):
                chunk = spd_range1[i:min(i+16, len(spd_range1))]
                hex_str = chunk.hex().upper()
                # Выравниваем по 32 символа (16 байт * 2 символа)
                hex_str = hex_str.ljust(32)
                spd_offset = SPD_RANGE_1_START + i
                lines.append(f"    [0x{spd_offset:02X}-0x{spd_offset+len(chunk)-1:02X}]: {hex_str}")
        
        # Диапазон 2: SPD[0x140-0x17F] (64 байта)
        if len(self.spd_data) >= SPD_RANGE_2_END:
            lines.append(f"  SPD[0x140-0x17F]:   {SPD_RANGE_2_SIZE} байта (индексы 130-193 в буфере)")
            spd_range2 = self.spd_data[SPD_RANGE_2_START:SPD_RANGE_2_END]
            # Показываем все байты диапазона по 16 байт в строке
            for i in range(0, len(spd_range2), 16):
                chunk = spd_range2[i:min(i+16, len(spd_range2))]
                hex_str = chunk.hex().upper()
                # Выравниваем по 32 символа (16 байт * 2 символа)
                hex_str = hex_str.ljust(32)
                spd_offset = SPD_RANGE_2_START + i
                lines.append(f"    [0x{spd_offset:03X}-0x{spd_offset+len(chunk)-1:03X}]: {hex_str}")
        
        # Secure ID (не участвует в расчете, но показываем для справки)
        secure_id = read_secure_id(self.spd_data)
        if secure_id:
            lines.append(f"  Secure ID:          SPD[0x184-0x187] = 0x{secure_id:08X}")
        
        lines.append("")
        return lines
    
    def _build_sensor_registers_section(self, sensor_reg_6, sensor_reg_7):
        """Построение секции Sensor Registers"""
        lines = []
        lines.append("3. SENSOR REGISTERS:")
        lines.append("-" * 80)
        
        # Sensor Register 6
        lines.append(f"  Sensor Register 6:  0x{sensor_reg_6:04X}")
        lines.append(f"    High byte:        0x{(sensor_reg_6 >> 8) & 0xFF:02X}")
        lines.append(f"    Low byte:        0x{sensor_reg_6 & 0xFF:02X}")
        lines.append(f"    В буфере:         индекс 195-196")
        
        # Sensor Register 7
        lines.append(f"  Sensor Register 7:  0x{sensor_reg_7:04X}")
        lines.append(f"    High byte:        0x{(sensor_reg_7 >> 8) & 0xFF:02X}")
        lines.append(f"    Low byte:        0x{sensor_reg_7 & 0xFF:02X}")
        lines.append(f"    В буфере:         индекс 198-199")
        lines.append("")
        return lines
    
    def _build_rcd_section(self, buffer):
        """Построение секции RCD данных"""
        lines = []
        lines.append("4. RCD ДАННЫЕ:")
        lines.append("-" * 80)
        lines.append(f"  Формат:             Стандартный (8 байт с разделителями)")
        lines.append(f"  В буфере:           индекс 201-208")
        rcd_bytes = buffer[201:209]
        lines.append(f"  Значения:           {rcd_bytes.hex().upper()}")
        lines.append(f"    [201-202]:        Vendor ID (Low, High) = 0x00, 0x00")
        lines.append(f"    [203]:            Разделитель = 0x20")
        lines.append(f"    [204-205]:        Device ID (Low, High) = 0x00, 0x00")
        lines.append(f"    [206]:            Разделитель = 0x20")
        lines.append(f"    [207]:            Revision = 0x00")
        lines.append(f"    [208]:            Разделитель = 0x20")
        lines.append("")
        return lines
    
    def _build_secret_key_section(self):
        """Построение секции Secret Key"""
        lines = []
        lines.append("5. SECRET KEY:")
        lines.append("-" * 80)
        lines.append(f"  Размер:             {len(COPYRIGHT_STRING)} байт")
        lines.append(f"  В буфере:           индекс 209-243")
        lines.append(f"  Текст:             '{COPYRIGHT_STRING.decode('ascii', errors='replace')}'")
        lines.append(f"  Hex:               {COPYRIGHT_STRING.hex().upper()}")
        lines.append("")
        return lines
    
    def _build_buffer_structure_section(self, buffer):
        """Построение секции структуры буфера"""
        lines = []
        lines.append("6. СТРУКТУРА БУФЕРА V69 (244 байта):")
        lines.append("-" * 80)
        lines.append(f"  [0-129]:            SPD данные (0x7E-0xFF) - 130 байт")
        lines.append(f"  [130-193]:          SPD Extended (0x140-0x17F) - 64 байта")
        lines.append(f"  [194]:              Разделитель 0x20")
        lines.append(f"  [195-196]:          Sensor Register 6 (High, Low)")
        lines.append(f"  [197]:              Разделитель 0x20")
        lines.append(f"  [198-199]:          Sensor Register 7 (High, Low)")
        lines.append(f"  [200]:              Разделитель 0x20")
        lines.append(f"  [201-208]:          RCD данные - 8 байт")
        lines.append(f"  [209-243]:          Secret Key - 35 байт")
        lines.append(f"  Итого:              {len(buffer)} байт")
        lines.append("")
        return lines
    
    def _build_buffer_bytes_section(self, buffer):
        """Построение секции с байтами буфера (все байты полностью)"""
        lines = []
        lines.append("7. ВСЕ БАЙТЫ БУФЕРА (244 байта):")
        lines.append("-" * 80)
        
        # Показываем все байты буфера по 16 байт в строке
        for i in range(0, len(buffer), 16):
            chunk = buffer[i:min(i+16, len(buffer))]
            hex_str = ' '.join(f'{b:02X}' for b in chunk)
            # Дополняем hex_str пробелами до 48 символов для выравнивания
            hex_str = hex_str.ljust(48)
            ascii_str = ''.join(chr(b) if 32 <= b < 127 else '.' for b in chunk)
            lines.append(f"    [{i:3d}-{i+len(chunk)-1:3d}]: {hex_str} {ascii_str}")
        
        lines.append("")
        return lines
    
    def _build_calculation_result_section(self, buffer):
        """Построение секции результата расчета"""
        lines = []
        secure_id = read_secure_id(self.spd_data)
        if secure_id:
            encryption_id = calculate_crc32_makehpid(buffer, BUFFER_V69_SIZE)
            lines.append("8. РЕЗУЛЬТАТ РАСЧЕТА:")
            lines.append("-" * 80)
            lines.append(f"  Encryption ID:     0x{encryption_id:08X}")
            lines.append(f"  Secure ID:         0x{secure_id:08X}")
            lines.append(f"  Совпадение:        {'ДА' if encryption_id == secure_id else 'НЕТ'}")
            if encryption_id != secure_id:
                diff = encryption_id ^ secure_id
                lines.append(f"  Разница:           0x{diff:08X}")
        return lines
    
    def _display_table_data(self, table_window, lines):
        """
        Отображение данных таблицы в окне
        
        Args:
            table_window: Окно с таблицей данных
            lines: Список строк для отображения
        """
        text_widget = table_window.text_widget
        text_widget.insert(tk.END, '\n'.join(lines))
        text_widget.config(state=tk.DISABLED)  # Только для чтения


# ============================================================================
# ТОЧКА ВХОДА
# ============================================================================

def main():
    """
    Главная функция приложения
    
    Создает и запускает GUI приложение для расчета Encryption ID.
    """
    root = tk.Tk()
    app = EncryptionIDCalculator(root)
    root.mainloop()


if __name__ == "__main__":
    main()
