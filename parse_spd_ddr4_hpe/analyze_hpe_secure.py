#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Анализ HPE Secure Code и SMART данных в SPD"""

import os
import sys
from pathlib import Path

def hex_dump(data, offset, length, line_prefix="", bytes_per_line=16):
    """Форматированный hex dump"""
    lines = []
    for i in range(offset, min(offset + length, len(data)), bytes_per_line):
        hex_bytes = " ".join(f"{b:02X}" for b in data[i:i+bytes_per_line])
        ascii_repr = "".join(chr(b) if 32 <= b < 127 else "." for b in data[i:i+bytes_per_line])
        lines.append(f"{line_prefix}{i:03X}: {hex_bytes:<48} {ascii_repr}")
    return "\n".join(lines)

def analyze_hpe_secure(data, filename):
    """Анализ HPE Secure Code и SMART данных"""
    
    print(f"\n{'='*80}")
    print(f"  {os.path.basename(filename)}")
    print(f"{'='*80}\n")
    
    # Основная информация
    part_num = data[329:349].decode('ascii', errors='ignore').strip()
    serial = int.from_bytes(data[325:329], 'big')
    
    print(f"📝 Part Number: {part_num}")
    print(f"🔢 Serial: 0x{serial:08X}")
    
    # DDR4 SPD Layout:
    # 0-127: Page 0 Lower
    # 128-255: Page 0 Upper
    # 256-319: Page 1 Block 0 (Manufacturing info)
    # 320-383: Page 1 Block 1 (Module specific)
    # 384-511: Page 1 Block 2-3 (Vendor specific / HPE Secure + SMART)
    
    print(f"\n{'='*80}")
    print("🔒 HPE SECURE CODE / AUTHENTICATION")
    print(f"{'='*80}")
    
    # Обычно HPE размещает secure code в районе байтов 384-415
    secure_area = data[384:416]
    print("\nБайты 384-415 (возможная область Secure Code):")
    print(hex_dump(data, 384, 32, "  "))
    
    # Проверка на наличие данных (не все нули/FF)
    if not all(b == 0 for b in secure_area) and not all(b == 0xFF for b in secure_area):
        print("\n✅ Обнаружены данные (не пустая область)")
        
        # Попытка найти паттерны
        # HPE часто использует CRC или хэш в начале
        if secure_area[0] != 0 or secure_area[1] != 0:
            print(f"   Возможный заголовок/CRC: 0x{secure_area[0]:02X}{secure_area[1]:02X}")
    else:
        print("\n⚠️  Область пуста или заполнена 0xFF")
    
    print(f"\n{'='*80}")
    print("📊 HPE SMART DATA / HEALTH MONITORING")
    print(f"{'='*80}")
    
    # SMART данные обычно после secure code, в районе 416-480
    smart_area = data[416:480]
    print("\nБайты 416-479 (возможная область SMART данных):")
    print(hex_dump(data, 416, 64, "  "))
    
    if not all(b == 0 for b in smart_area) and not all(b == 0xFF for b in smart_area):
        print("\n✅ Обнаружены данные")
        
        # Типичные SMART параметры для памяти:
        # - Счетчик включений
        # - Часы работы
        # - Температурные данные
        # - Количество ошибок
        # - Износ (для NVDIMM)
        
        # Попытка интерпретации
        if any(b != 0 for b in smart_area[0:8]):
            power_on_count = int.from_bytes(smart_area[0:4], 'little')
            power_on_hours = int.from_bytes(smart_area[4:8], 'little')
            
            if power_on_count < 100000:  # Разумное значение
                print(f"\n   Возможные данные:")
                print(f"   Power-On Count: {power_on_count}")
                print(f"   Power-On Hours: {power_on_hours} ч ({power_on_hours/24:.1f} дней)")
    else:
        print("\n⚠️  Область пуста или заполнена 0xFF")
    
    print(f"\n{'='*80}")
    print("🔍 ПОЛНЫЙ HEX DUMP VENDOR ОБЛАСТИ (384-511)")
    print(f"{'='*80}\n")
    print(hex_dump(data, 384, 128, "  "))
    
    print(f"\n{'='*80}")
    print("📋 ПОИСК ПАТТЕРНОВ")
    print(f"{'='*80}\n")
    
    # Поиск ASCII строк
    vendor_data = data[384:512]
    ascii_strings = []
    current_string = []
    
    for i, b in enumerate(vendor_data):
        if 32 <= b < 127:  # Печатаемый ASCII
            current_string.append(chr(b))
        else:
            if len(current_string) >= 4:  # Строка минимум 4 символа
                ascii_strings.append(''.join(current_string))
            current_string = []
    
    if current_string and len(current_string) >= 4:
        ascii_strings.append(''.join(current_string))
    
    if ascii_strings:
        print("Найдены ASCII строки:")
        for s in ascii_strings:
            print(f"  '{s}'")
    else:
        print("ASCII строки не найдены")
    
    # Поиск повторяющихся паттернов
    print("\nПоиск повторяющихся байт:")
    byte_counts = {}
    for b in vendor_data:
        byte_counts[b] = byte_counts.get(b, 0) + 1
    
    most_common = sorted(byte_counts.items(), key=lambda x: x[1], reverse=True)[:5]
    for byte_val, count in most_common:
        percentage = (count / len(vendor_data)) * 100
        print(f"  0x{byte_val:02X}: {count} раз ({percentage:.1f}%)")
    
    # Энтропия (простая оценка)
    unique_bytes = len(set(vendor_data))
    entropy = (unique_bytes / 256) * 100
    print(f"\nУникальных байт: {unique_bytes}/256 ({entropy:.1f}% энтропия)")
    
    if entropy > 50:
        print("  ✅ Высокая энтропия - вероятно содержит зашифрованные/хэшированные данные")
    elif entropy > 20:
        print("  ⚠️  Средняя энтропия - содержит смешанные данные")
    else:
        print("  ❌ Низкая энтропия - вероятно пустая или заполненная область")

def compare_secure_codes(files):
    """Сравнение Secure Code между модулями"""
    
    print(f"\n{'='*80}")
    print("🔐 СРАВНЕНИЕ SECURE CODES")
    print(f"{'='*80}\n")
    
    secure_codes = {}
    
    for f in files:
        data = open(f, 'rb').read(512)
        serial = int.from_bytes(data[325:329], 'big')
        secure_code = data[384:416]
        
        # Используем первые 16 байт как идентификатор
        code_id = secure_code[:16].hex()
        
        if code_id not in secure_codes:
            secure_codes[code_id] = []
        secure_codes[code_id].append((f, serial))
    
    print(f"Найдено уникальных Secure Codes: {len(secure_codes)}\n")
    
    if len(secure_codes) == 1:
        print("✅ Все модули имеют ОДИНАКОВЫЙ Secure Code")
        print("   (Возможно, это партийный код или пусто)")
    else:
        print("⚠️  Модули имеют РАЗНЫЕ Secure Codes")
        for i, (code_id, modules) in enumerate(secure_codes.items(), 1):
            print(f"\n  Код #{i}: {code_id[:32]}...")
            print(f"  Модулей: {len(modules)}")
            for fname, serial in modules[:3]:  # Показываем первые 3
                print(f"    - {os.path.basename(fname)} (S/N: 0x{serial:08X})")
            if len(modules) > 3:
                print(f"    ... и еще {len(modules) - 3}")

def main():
    """Главная функция"""
    print("🔍 Анализатор HPE Secure Code и SMART данных\n")
    
    # Ищем все .bin файлы
    bin_files = sorted(list(Path('.').glob('*.bin')))
    
    if not bin_files:
        print("❌ Не найдено .bin файлов")
        return
    
    print(f"📁 Найдено файлов: {len(bin_files)}\n")
    
    # Детальный анализ первых 3 файлов
    for bin_file in bin_files[:3]:
        try:
            data = open(str(bin_file), 'rb').read(512)
            analyze_hpe_secure(data, str(bin_file))
        except Exception as e:
            print(f"❌ Ошибка: {e}")
    
    if len(bin_files) > 3:
        print(f"\n... (остальные {len(bin_files) - 3} файлов пропущены для краткости)")
    
    # Сравнительный анализ всех файлов
    try:
        compare_secure_codes([str(f) for f in bin_files])
    except Exception as e:
        print(f"❌ Ошибка сравнения: {e}")
    
    print(f"\n{'='*80}")
    print("✅ Анализ завершен")
    print(f"{'='*80}\n")

if __name__ == '__main__':
    main()

