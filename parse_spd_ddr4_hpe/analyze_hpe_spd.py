#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Анализ HPE DDR4 SPD дампов"""

import os
import sys
from pathlib import Path

# JEDEC Manufacturer IDs (байты 320-321)
MANUFACTURERS = {
    0x80CE: "Samsung",
    0x802C: "Micron",
    0x80AD: "Hynix",
    0x859B: "Kingston",
}

# DDR4 Module Types (байт 3)
MODULE_TYPES = {
    0x01: "RDIMM",
    0x02: "UDIMM",
    0x03: "SODIMM",
    0x04: "LRDIMM",
}

def read_spd(filename):
    """Чтение SPD дампа"""
    with open(filename, 'rb') as f:
        return f.read(512)

def hex_dump(data, offset, length, line_prefix=""):
    """Форматированный hex dump"""
    lines = []
    for i in range(offset, min(offset + length, len(data)), 16):
        hex_bytes = " ".join(f"{b:02X}" for b in data[i:i+16])
        ascii_repr = "".join(chr(b) if 32 <= b < 127 else "." for b in data[i:i+16])
        lines.append(f"{line_prefix}{i:03X}: {hex_bytes:<48} {ascii_repr}")
    return "\n".join(lines)

def analyze_spd(data, filename):
    """Детальный анализ SPD"""
    results = {}
    results['filename'] = os.path.basename(filename)
    
    # Основные параметры SPD
    results['spd_bytes_used'] = data[0]
    results['spd_revision'] = f"{data[1] >> 4}.{data[1] & 0x0F}"
    results['dram_type'] = data[2]
    results['module_type_code'] = data[3]
    results['module_type'] = MODULE_TYPES.get(data[3] & 0x0F, "Unknown")
    
    # SDRAM параметры
    density = data[4] & 0x0F
    densities = {0: "256Mb", 1: "512Mb", 2: "1Gb", 3: "2Gb", 4: "4Gb", 5: "8Gb", 6: "16Gb", 7: "32Gb"}
    results['sdram_density'] = densities.get(density, "Unknown")
    
    banks = (data[4] >> 4) & 0x03
    banks_count = {0: "4", 1: "8"}
    results['sdram_banks'] = banks_count.get(banks, "Unknown")
    
    # Адресация
    rows = data[5] & 0x07
    cols = (data[5] >> 3) & 0x07
    results['row_addr'] = 12 + rows
    results['col_addr'] = 9 + cols
    
    # Module organization
    device_width = data[12] & 0x07
    widths = {0: "x4", 1: "x8", 2: "x16", 3: "x32"}
    results['device_width'] = widths.get(device_width, "Unknown")
    
    ranks = ((data[12] >> 3) & 0x07) + 1
    results['ranks'] = ranks
    
    # Timing parameters
    results['mtb'] = 0.125  # Medium Timebase = 125ps для DDR4
    results['tck_min'] = data[18] * results['mtb']
    results['tck_max'] = data[19] * results['mtb']
    results['freq_mhz'] = int(2000 / results['tck_min'])
    
    # CAS Latencies
    cl_low = data[20] | (data[21] << 8) | (data[22] << 16) | (data[23] << 24)
    cl_supported = [i for i in range(32) if cl_low & (1 << i)]
    results['cas_latencies'] = cl_supported
    
    # Timings
    results['taa_min'] = data[24] * results['mtb']  # ns
    results['trcd_min'] = data[25] * results['mtb']
    results['trp_min'] = data[26] * results['mtb']
    results['tras_min'] = ((data[27] & 0x0F) << 8 | data[28]) * results['mtb']
    results['trc_min'] = (((data[27] & 0xF0) << 4) | data[29]) * results['mtb']
    
    # Module Manufacturer (байты 320-321)
    mfg_id = (data[320] << 8) | data[321]
    results['module_mfg_id'] = mfg_id
    results['module_mfg'] = MANUFACTURERS.get(mfg_id, f"Unknown (0x{mfg_id:04X})")
    
    # Module Part Number (байты 329-348)
    part_num = data[329:349].decode('ascii', errors='ignore').strip()
    results['part_number'] = part_num
    
    # Module Serial Number (байты 325-328)
    serial = int.from_bytes(data[325:329], 'big')
    results['serial_number'] = f"0x{serial:08X}"
    
    # Manufacturing Date (байты 323-324)
    mfg_year = 2000 + data[323]
    mfg_week = data[324]
    results['mfg_date'] = f"Week {mfg_week}, {mfg_year}"
    
    # Manufacturing Location (байт 322)
    results['mfg_location'] = data[322]
    
    # DRAM Manufacturer (байты 350-351)
    dram_mfg_id = (data[350] << 8) | data[351]
    results['dram_mfg_id'] = dram_mfg_id
    results['dram_mfg'] = MANUFACTURERS.get(dram_mfg_id, f"Unknown (0x{dram_mfg_id:04X})")
    
    # Register Manufacturer (RDIMM only, байты 133-134)
    if results['module_type'] == "RDIMM":
        reg_mfg_id = (data[133] << 8) | data[134]
        reg_mfgs = {0x8083: "Montage", 0x80B3: "IDT", 0x80CE: "Samsung"}
        results['register_mfg_id'] = reg_mfg_id
        results['register_mfg'] = reg_mfgs.get(reg_mfg_id, f"Unknown (0x{reg_mfg_id:04X})")
        
        # Register Revision (байт 135)
        results['register_rev'] = data[135]
    
    # Checksum (байт 127 для первых 128 байт, байт 255 для вторых 128 байт)
    crc_page0 = data[126] | (data[127] << 8)
    crc_page1 = data[254] | (data[255] << 8)
    results['crc_page0'] = f"0x{crc_page0:04X}"
    results['crc_page1'] = f"0x{crc_page1:04X}"
    
    return results

def print_detailed_analysis(results):
    """Красивый вывод результатов анализа"""
    print(f"\n{'='*80}")
    print(f"  {results['filename']}")
    print(f"{'='*80}\n")
    
    print("📋 ОСНОВНАЯ ИНФОРМАЦИЯ:")
    print(f"  SPD Revision:          {results['spd_revision']}")
    print(f"  DRAM Type:             DDR4 (0x{results['dram_type']:02X})")
    print(f"  Module Type:           {results['module_type']} (0x{results['module_type_code']:02X})")
    
    print("\n💾 ПАРАМЕТРЫ ПАМЯТИ:")
    print(f"  SDRAM Density:         {results['sdram_density']}")
    print(f"  SDRAM Banks:           {results['sdram_banks']} banks")
    print(f"  Device Width:          {results['device_width']}")
    print(f"  Ranks:                 {results['ranks']}")
    print(f"  Row Address:           {results['row_addr']} bits")
    print(f"  Column Address:        {results['col_addr']} bits")
    
    print(f"\n⚡ ЧАСТОТА И ТАЙМИНГИ:")
    print(f"  Frequency:             DDR4-{results['freq_mhz']} ({results['tck_min']:.3f} ns)")
    print(f"  tAA (CAS Latency):     {results['taa_min']:.3f} ns")
    print(f"  tRCD:                  {results['trcd_min']:.3f} ns")
    print(f"  tRP:                   {results['trp_min']:.3f} ns")
    print(f"  tRAS:                  {results['tras_min']:.3f} ns")
    print(f"  tRC:                   {results['trc_min']:.3f} ns")
    
    # Рассчитываем CL в тактах для основной частоты
    cl_cycles = int(results['taa_min'] / results['tck_min'])
    trcd_cycles = int(results['trcd_min'] / results['tck_min'])
    trp_cycles = int(results['trp_min'] / results['tck_min'])
    tras_cycles = int(results['tras_min'] / results['tck_min'])
    print(f"  Timings (cycles):      {cl_cycles}-{trcd_cycles}-{trp_cycles}-{tras_cycles}")
    
    print(f"  Supported CAS:         {', '.join(map(str, results['cas_latencies'][-8:]))}")
    
    print(f"\n🏭 ПРОИЗВОДИТЕЛЬ:")
    print(f"  Module Mfg:            {results['module_mfg']} (ID: 0x{results['module_mfg_id']:04X})")
    print(f"  Part Number:           {results['part_number']}")
    print(f"  Serial Number:         {results['serial_number']}")
    print(f"  Manufacturing Date:    {results['mfg_date']}")
    print(f"  Manufacturing Loc:     0x{results['mfg_location']:02X}")
    
    print(f"\n💿 DRAM ЧИПЫ:")
    print(f"  DRAM Mfg:              {results['dram_mfg']} (ID: 0x{results['dram_mfg_id']:04X})")
    
    if 'register_mfg' in results:
        print(f"\n🔌 РЕГИСТР (RDIMM):")
        print(f"  Register Mfg:          {results['register_mfg']} (ID: 0x{results['register_mfg_id']:04X})")
        print(f"  Register Revision:     0x{results['register_rev']:02X}")
    
    print(f"\n✅ КОНТРОЛЬНЫЕ СУММЫ:")
    print(f"  CRC Page 0 (0-127):    {results['crc_page0']}")
    print(f"  CRC Page 1 (128-255):  {results['crc_page1']}")

def compare_modules(results_list):
    """Сравнение нескольких модулей"""
    if len(results_list) < 2:
        return
    
    print(f"\n{'='*80}")
    print(f"  СРАВНЕНИЕ МОДУЛЕЙ")
    print(f"{'='*80}\n")
    
    # Группируем по параметрам
    part_numbers = set(r['part_number'] for r in results_list)
    frequencies = set(r['freq_mhz'] for r in results_list)
    serials = [r['serial_number'] for r in results_list]
    
    print(f"📊 СТАТИСТИКА:")
    print(f"  Всего модулей:         {len(results_list)}")
    print(f"  Уникальных PN:         {len(part_numbers)}")
    print(f"  Частоты:               {', '.join(f'DDR4-{f}' for f in sorted(frequencies))}")
    
    if len(part_numbers) == 1:
        print(f"\n✅ Все модули одной модели: {part_numbers.pop()}")
    else:
        print(f"\n⚠️  Найдено несколько моделей:")
        for pn in part_numbers:
            count = sum(1 for r in results_list if r['part_number'] == pn)
            print(f"    - {pn}: {count} шт.")
    
    print(f"\n🔢 СЕРИЙНЫЕ НОМЕРА:")
    for i, serial in enumerate(serials, 1):
        print(f"  {i:2d}. {serial}")
    
    # Проверяем различия в таймингах
    timings_set = set()
    for r in results_list:
        cl = int(r['taa_min'] / r['tck_min'])
        trcd = int(r['trcd_min'] / r['tck_min'])
        trp = int(r['trp_min'] / r['tck_min'])
        tras = int(r['tras_min'] / r['tck_min'])
        timings_set.add((cl, trcd, trp, tras))
    
    if len(timings_set) == 1:
        print(f"\n✅ Тайминги одинаковые у всех модулей")
    else:
        print(f"\n⚠️  Найдены различия в таймингах:")
        for timing in timings_set:
            count = sum(1 for r in results_list 
                       if (int(r['taa_min']/r['tck_min']), int(r['trcd_min']/r['tck_min']), 
                           int(r['trp_min']/r['tck_min']), int(r['tras_min']/r['tck_min'])) == timing)
            print(f"    {timing[0]}-{timing[1]}-{timing[2]}-{timing[3]}: {count} модулей")

def main():
    """Главная функция"""
    print("🔍 Анализатор HPE DDR4 SPD дампов\n")
    
    # Ищем все .bin файлы
    bin_files = list(Path('.').glob('*.bin'))
    
    if not bin_files:
        print("❌ Не найдено .bin файлов в текущей директории")
        return
    
    print(f"📁 Найдено файлов: {len(bin_files)}\n")
    
    results_list = []
    
    for bin_file in sorted(bin_files):
        try:
            data = read_spd(str(bin_file))
            results = analyze_spd(data, str(bin_file))
            results_list.append(results)
            print_detailed_analysis(results)
            
            # Опционально: вывод hex dump
            if '--hex' in sys.argv:
                print(f"\n📝 HEX DUMP (первые 256 байт):")
                print(hex_dump(data, 0, 256, "  "))
            
        except Exception as e:
            print(f"❌ Ошибка при обработке {bin_file}: {e}")
    
    # Сравнительный анализ
    if results_list:
        compare_modules(results_list)
    
    print(f"\n{'='*80}")
    print("✅ Анализ завершен")
    print(f"{'='*80}\n")

if __name__ == '__main__':
    main()

