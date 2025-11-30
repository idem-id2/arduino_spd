#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import sys

file1 = "64Gb_Samsung_2Rx4_M393A8G40CB4-CWE_M88DR4RCD02P_HPE_4448ECFB.bin"
file2 = "64Gb_Samsung_2Rx4_M393A8G40CB4-CWE_M88DR4RCD02P_HPE_4448ED07.bin"

print("🔍 HPE SPD Comparator - Быстрое сравнение")
print("=" * 60)
print(f"\nФайл 1: {file1}")
print(f"Файл 2: {file2}\n")

try:
    with open(file1, 'rb') as f:
        data1 = f.read()
    with open(file2, 'rb') as f:
        data2 = f.read()
except Exception as e:
    print(f"❌ Ошибка чтения: {e}")
    sys.exit(1)

print(f"Размер 1: {len(data1)} bytes")
print(f"Размер 2: {len(data2)} bytes\n")

# Находим различия
diffs = []
min_len = min(len(data1), len(data2))

for i in range(min_len):
    if data1[i] != data2[i]:
        diffs.append(i)

print(f"Различий: {len(diffs)} из {min_len} ({100.0*len(diffs)/min_len:.1f}%)\n")

if len(diffs) == 0:
    print("✅ Файлы идентичны!")
else:
    print("=" * 60)
    print("📋 ВСЕ РАЗЛИЧАЮЩИЕСЯ БАЙТЫ")
    print("=" * 60)
    print()
    
    def get_block_name(offset):
        if offset < 128: return "Base Config"
        if offset < 256: return "Module Params"
        if offset < 320: return "Reserved"
        if offset < 325: return "Manufacturer ID"
        if offset == 324: return "Location"
        if offset < 329: return "Serial Number"
        if offset < 349: return "Part Number"
        if offset < 384: return "Manuf Data"
        if offset < 388: return "HPE Header"
        if offset < 392: return "HPE Secure ID"
        if offset < 400: return "HPE Reserved"
        if offset < 416: return "HPE Product Code"
        return "Extended"
    
    for pos in diffs:
        block = get_block_name(pos)
        print(f"  Offset {pos:3d} (0x{pos:03X}) [{block}]:")
        print(f"    Файл 1: 0x{data1[pos]:02X}")
        print(f"    Файл 2: 0x{data2[pos]:02X}")
        print()

# Анализ по блокам
print("=" * 60)
print("📊 АНАЛИЗ ПО БЛОКАМ")
print("=" * 60)
print()

blocks = [
    ("Base Config (0-127)", 0, 128),
    ("Module Params (128-255)", 128, 128),
    ("Reserved (256-319)", 256, 64),
    ("Manufacturing (320-383)", 320, 64),
    ("HPE Secure Code (384-415)", 384, 32),
    ("Extended (416-511)", 416, 96)
]

for name, start, size in blocks:
    block_diffs = sum(1 for d in diffs if start <= d < start + size)
    percent = 100.0 * block_diffs / size if size > 0 else 0
    status = "✅ Идентичны" if block_diffs == 0 else \
             "⚠️  Мало различий" if percent < 10 else "❌ Много различий"
    print(f"  {name:<30} {block_diffs:3d}/{size:3d} ({percent:5.1f}%) {status}")

# Детальный анализ critical блока
print()
print("=" * 60)
print("🔬 КРИТИЧНЫЙ БЛОК: MANUFACTURING + SECURE (320-415)")
print("=" * 60)
print()

fields = [
    ("Manufacturer ID", 320, 4),
    ("Location", 324, 1),
    ("Serial Number", 325, 4),
    ("Part Number", 329, 20),
    ("Manuf Data", 349, 34),
    ("CRC", 382, 2),
    ("HPE Header", 384, 4),
    ("HPE Secure ID", 388, 4),
    ("HPE Reserved", 392, 8),
    ("HPE Product", 400, 16),
]

for name, offset, size in fields:
    same = all(data1[offset+i] == data2[offset+i] for i in range(size) if offset+i < min_len)
    status = "✅" if same else "❌"
    
    print(f"  {status} {name:<20}", end="")
    
    if not same:
        print()
        print(f"    Файл 1: {' '.join(f'{data1[offset+i]:02X}' for i in range(size) if offset+i < len(data1))}")
        print(f"    Файл 2: {' '.join(f'{data2[offset+i]:02X}' for i in range(size) if offset+i < len(data2))}")
    else:
        preview = ' '.join(f'{data1[offset+i]:02X}' for i in range(min(4, size)) if offset+i < len(data1))
        if size > 4:
            preview += " ..."
        print(f" = {preview}")

# Выводы
print()
print("=" * 60)
print("💡 ВЫВОДЫ")
print("=" * 60)
print()

serial_diff = any(325 <= d < 329 for d in diffs)
secure_diff = any(388 <= d < 392 for d in diffs)
manuf_diff = any(349 <= d < 384 for d in diffs)
base_diff = any(d < 320 for d in diffs)

if serial_diff and secure_diff and not base_diff and not manuf_diff:
    print("  ✅ Различаются ТОЛЬКО Serial Number и Secure ID")
    print("     → Это нормально для разных модулей одной серии")
    print("     → HPE Secure ID пересчитывается для каждого S/N")

if manuf_diff:
    print("\n  ⚠️  Различается Manufacturing Data (349-382)")
    print("     → Это могут быть версии, даты, счетчики")
    print("     → HPE может проверять эти поля!")

if base_diff:
    print("\n  ❌ Различается Base Configuration (0-319)")
    print("     → Это КРИТИЧНО - параметры памяти отличаются")

# Показываем Secure ID
if len(data1) > 391 and len(data2) > 391:
    import struct
    serial1 = struct.unpack('<I', data1[325:329])[0]
    hash1 = struct.unpack('<I', data1[388:392])[0]
    serial2 = struct.unpack('<I', data2[325:329])[0]
    hash2 = struct.unpack('<I', data2[388:392])[0]
    
    print(f"\n  🔑 Контрольные значения:")
    print(f"     Файл 1: S/N=0x{serial1:08X} → Hash=0x{hash1:08X}")
    print(f"     Файл 2: S/N=0x{serial2:08X} → Hash=0x{hash2:08X}")

print("\n✅ Анализ завершён.")

