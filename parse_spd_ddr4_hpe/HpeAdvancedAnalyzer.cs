using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

class HpeAdvancedAnalyzer
{
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("🔬 Расширенный анализатор HPE Secure Code и SMART данных\n");

        var files = Directory.GetFiles(".", "*.bin").OrderBy(f => f).ToList();
        
        if (files.Length == 0)
        {
            Console.WriteLine("❌ Не найдено .bin файлов");
            return;
        }

        Console.WriteLine($"📁 Найдено файлов: {files.Length}\n");

        // Детальный анализ первых 3 файлов
        foreach (var file in files.Take(3))
        {
            try
            {
                var data = File.ReadAllBytes(file);
                AnalyzeAdvanced(data, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка: {ex.Message}\n");
            }
        }

        if (files.Length > 3)
        {
            Console.WriteLine($"\n... (остальные {files.Length - 3} файлов пропущены)\n");
        }

        // Сравнительный анализ
        CompareAll(files);
        
        // Статистика SMART
        AnalyzeSmartStats(files);

        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine("✅ Анализ завершен");
        Console.WriteLine(new string('=', 80) + "\n");
    }

    static void AnalyzeAdvanced(byte[] data, string filename)
    {
        Console.WriteLine(new string('=', 80));
        Console.WriteLine($"  {Path.GetFileName(filename)}");
        Console.WriteLine(new string('=', 80) + "\n");

        string partNum = Encoding.ASCII.GetString(data, 329, 20).Trim('\0', ' ');
        uint serial = BitConverter.ToUInt32(new byte[] { data[328], data[327], data[326], data[325] }, 0);
        
        Console.WriteLine($"📝 Part Number: {partNum}");
        Console.WriteLine($"🔢 Serial: 0x{serial:X8}");

        // ============================================
        // HPE SECURE CODE - детальный анализ
        // ============================================
        Console.WriteLine($"\n{new string('=', 80)}");
        Console.WriteLine("🔒 HPE SECURE CODE (байты 384-415)");
        Console.WriteLine(new string('=', 80) + "\n");
        
        var secureCode = new byte[32];
        Array.Copy(data, 384, secureCode, 0, 32);
        
        HexDump(data, 384, 32, "  ");
        
        // Проверка структуры Secure Code
        Console.WriteLine("\n📊 Анализ структуры:");
        
        bool isEmpty = secureCode.All(b => b == 0 || b == 0xFF);
        if (isEmpty)
        {
            Console.WriteLine("  ❌ Область пуста (все 0x00 или 0xFF)");
        }
        else
        {
            Console.WriteLine("  ✅ Содержит данные\n");
            
            // Возможная структура HPE Secure Code:
            // Байты 0-1: Magic/Version
            // Байты 2-3: CRC/Checksum
            // Байты 4-19: Hash/Signature (16 байт)
            // Байты 20-31: Доп. данные
            
            ushort magic = BitConverter.ToUInt16(secureCode, 0);
            ushort crc = BitConverter.ToUInt16(secureCode, 2);
            
            Console.WriteLine($"  Magic/Version:     0x{magic:X4}");
            Console.WriteLine($"  CRC/Checksum:      0x{crc:X4}");
            
            // Hash/Signature
            var hash = new byte[16];
            Array.Copy(secureCode, 4, hash, 0, 16);
            Console.WriteLine($"  Hash/Signature:    {BitConverter.ToString(hash).Replace("-", "")}");
            
            // Дополнительные данные
            var extraData = new byte[12];
            Array.Copy(secureCode, 20, extraData, 0, 12);
            
            if (extraData.Any(b => b != 0 && b != 0xFF))
            {
                Console.WriteLine($"  Доп. данные:       {BitConverter.ToString(extraData).Replace("-", " ")}");
            }
            
            // Попытка вычислить CRC для проверки
            ushort calculatedCrc = CalculateCrc16(secureCode, 4, 28);
            bool crcValid = (calculatedCrc == crc);
            Console.WriteLine($"\n  Проверка CRC:      {(crcValid ? "✅ Корректный" : "⚠️  Не совпадает")}");
            if (!crcValid)
            {
                Console.WriteLine($"  Вычисленный CRC:   0x{calculatedCrc:X4}");
            }
        }

        // ============================================
        // SMART DATA - детальное декодирование
        // ============================================
        Console.WriteLine($"\n{new string('=', 80)}");
        Console.WriteLine("📊 SMART DATA (байты 416-479)");
        Console.WriteLine(new string('=', 80) + "\n");
        
        var smartData = new byte[64];
        Array.Copy(data, 416, smartData, 0, 64);
        
        HexDump(data, 416, 64, "  ");
        
        Console.WriteLine("\n📈 Декодирование SMART параметров:\n");
        
        isEmpty = smartData.All(b => b == 0 || b == 0xFF);
        if (isEmpty)
        {
            Console.WriteLine("  ❌ Область пуста");
        }
        else
        {
            // Типичная структура SMART для серверной памяти:
            // Байты 0-3: Power-On Count (количество включений)
            // Байты 4-7: Power-On Hours (часы работы)
            // Байты 8-11: Temperature Max (°C * 100)
            // Байты 12-15: Temperature Min (°C * 100)
            // Байты 16-19: ECC Error Count (однобитные)
            // Байты 20-23: Uncorrectable Error Count
            // Байты 24-27: Refresh Count
            // Байты 28-31: Write Count
            
            uint powerOnCount = BitConverter.ToUInt32(smartData, 0);
            uint powerOnHours = BitConverter.ToUInt32(smartData, 4);
            
            if (powerOnCount > 0 && powerOnCount < 100000)
            {
                Console.WriteLine($"  Power-On Count:         {powerOnCount,10} раз");
                Console.WriteLine($"  Power-On Hours:         {powerOnHours,10} ч ({powerOnHours/24.0:F1} дней)");
                
                if (powerOnHours > 0 && powerOnCount > 0)
                {
                    double avgHoursPerBoot = (double)powerOnHours / powerOnCount;
                    Console.WriteLine($"  Ср. время работы:       {avgHoursPerBoot,10:F2} ч/включение");
                }
            }
            else
            {
                Console.WriteLine($"  Power-On Count:         0x{powerOnCount:X8} (сырое значение)");
                Console.WriteLine($"  Power-On Hours:         0x{powerOnHours:X8} (сырое значение)");
            }
            
            // Температура
            uint tempMax = BitConverter.ToUInt32(smartData, 8);
            uint tempMin = BitConverter.ToUInt32(smartData, 12);
            
            if (tempMax > 0 && tempMax < 20000) // Разумный диапазон (0-200°C * 100)
            {
                Console.WriteLine($"\n  Температура Max:        {tempMax/100.0,10:F1} °C");
                Console.WriteLine($"  Температура Min:        {tempMin/100.0,10:F1} °C");
            }
            
            // Ошибки
            uint eccErrors = BitConverter.ToUInt32(smartData, 16);
            uint uncorrErrors = BitConverter.ToUInt32(smartData, 20);
            
            if (eccErrors < 1000000 || uncorrErrors < 10000)
            {
                Console.WriteLine($"\n  ECC Errors (1-bit):     {eccErrors,10}");
                Console.WriteLine($"  Uncorrectable Errors:   {uncorrErrors,10}");
                
                if (uncorrErrors > 0)
                {
                    Console.WriteLine($"  ⚠️  ВНИМАНИЕ: Обнаружены неисправимые ошибки!");
                }
                else if (eccErrors == 0 && uncorrErrors == 0)
                {
                    Console.WriteLine($"  ✅ Ошибок не обнаружено");
                }
            }
            
            // Дополнительные счетчики
            uint refreshCount = BitConverter.ToUInt32(smartData, 24);
            uint writeCount = BitConverter.ToUInt32(smartData, 28);
            
            if (refreshCount < 10000000)
            {
                Console.WriteLine($"\n  Refresh Count:          {refreshCount,10}");
            }
            
            if (writeCount < 10000000)
            {
                Console.WriteLine($"  Write Count:            {writeCount,10}");
            }
            
            // Health Status (байты 32-35)
            uint healthStatus = BitConverter.ToUInt32(smartData, 32);
            if (healthStatus != 0 && healthStatus != 0xFFFFFFFF)
            {
                Console.WriteLine($"\n  Health Status:          0x{healthStatus:X8}");
                Console.WriteLine($"  Расшифровка:");
                if ((healthStatus & 0x01) != 0) Console.WriteLine($"    - Bit 0: Температурное предупреждение");
                if ((healthStatus & 0x02) != 0) Console.WriteLine($"    - Bit 1: ECC предупреждение");
                if ((healthStatus & 0x04) != 0) Console.WriteLine($"    - Bit 2: Критическая ошибка");
                if ((healthStatus & 0x08) != 0) Console.WriteLine($"    - Bit 3: Износ (для NVDIMM)");
                
                if (healthStatus == 0)
                {
                    Console.WriteLine($"    ✅ Все параметры в норме");
                }
            }
        }

        // ============================================
        // VENDOR DATA - полный анализ
        // ============================================
        Console.WriteLine($"\n{new string('=', 80)}");
        Console.WriteLine("🔍 VENDOR DATA (байты 384-511) - сводка");
        Console.WriteLine(new string('=', 80) + "\n");
        
        var vendorData = new byte[128];
        Array.Copy(data, 384, vendorData, 0, 128);
        
        // Статистика
        int nonZeroBytes = vendorData.Count(b => b != 0);
        int nonFFBytes = vendorData.Count(b => b != 0xFF);
        int uniqueBytes = vendorData.Distinct().Count();
        double entropy = (uniqueBytes / 256.0) * 100;
        
        Console.WriteLine($"  Ненулевых байт:        {nonZeroBytes}/128 ({nonZeroBytes*100.0/128:F1}%)");
        Console.WriteLine($"  Не 0xFF байт:          {nonFFBytes}/128 ({nonFFBytes*100.0/128:F1}%)");
        Console.WriteLine($"  Уникальных значений:   {uniqueBytes}/256 ({entropy:F1}% энтропия)");
        
        if (entropy > 60)
            Console.WriteLine($"  ✅ Высокая энтропия - вероятно зашифрованные/хэшированные данные");
        else if (entropy > 30)
            Console.WriteLine($"  ⚠️  Средняя энтропия - смешанные данные");
        else if (nonZeroBytes > 0)
            Console.WriteLine($"  📋 Низкая энтропия - структурированные данные");
        else
            Console.WriteLine($"  ❌ Пустая область");
        
        // Поиск паттернов
        var patterns = FindPatterns(vendorData);
        if (patterns.Count > 0)
        {
            Console.WriteLine($"\n  Найдено повторяющихся паттернов: {patterns.Count}");
            foreach (var p in patterns.Take(3))
            {
                Console.WriteLine($"    - {BitConverter.ToString(p.Item1).Replace("-", " ")} (повторений: {p.Item2})");
            }
        }
        
        Console.WriteLine();
    }

    static void CompareAll(string[] files)
    {
        Console.WriteLine(new string('=', 80));
        Console.WriteLine("🔐 СРАВНИТЕЛЬНЫЙ АНАЛИЗ");
        Console.WriteLine(new string('=', 80) + "\n");

        var secureCodes = new Dictionary<string, List<string>>();
        var smartHashes = new Dictionary<string, List<string>>();

        foreach (var file in files)
        {
            var data = File.ReadAllBytes(file);
            
            // Secure Code
            var secureCode = new byte[16];
            Array.Copy(data, 384, secureCode, 0, 16);
            string secId = BitConverter.ToString(secureCode).Replace("-", "");
            
            if (!secureCodes.ContainsKey(secId))
                secureCodes[secId] = new List<string>();
            secureCodes[secId].Add(Path.GetFileName(file));
            
            // SMART hash (первые 16 байт)
            var smartHash = new byte[16];
            Array.Copy(data, 416, smartHash, 0, 16);
            string smId = BitConverter.ToString(smartHash).Replace("-", "");
            
            if (!smartHashes.ContainsKey(smId))
                smartHashes[smId] = new List<string>();
            smartHashes[smId].Add(Path.GetFileName(file));
        }

        Console.WriteLine($"📊 Secure Codes:");
        Console.WriteLine($"  Уникальных: {secureCodes.Count}");
        if (secureCodes.Count == 1)
        {
            Console.WriteLine($"  ✅ Все одинаковые");
        }
        else
        {
            Console.WriteLine($"  ⚠️  Различаются:");
            int i = 1;
            foreach (var kvp in secureCodes.Take(3))
            {
                Console.WriteLine($"    Вариант {i}: {kvp.Value.Count} файлов");
                i++;
            }
        }

        Console.WriteLine($"\n📊 SMART данные:");
        Console.WriteLine($"  Уникальных: {smartHashes.Count}");
        if (smartHashes.Count == files.Length)
        {
            Console.WriteLine($"  ✅ Уникальные для каждого модуля (ожидаемо)");
        }
        else if (smartHashes.Count == 1)
        {
            Console.WriteLine($"  ⚠️  Все одинаковые (возможно пустые)");
        }
        else
        {
            Console.WriteLine($"  📊 {smartHashes.Count} различных вариантов");
        }
    }

    static void AnalyzeSmartStats(string[] files)
    {
        Console.WriteLine($"\n{new string('=', 80)}");
        Console.WriteLine("📈 СТАТИСТИКА SMART ПО ВСЕМ МОДУЛЯМ");
        Console.WriteLine(new string('=', 80) + "\n");

        var powerOnCounts = new List<uint>();
        var powerOnHours = new List<uint>();

        foreach (var file in files)
        {
            var data = File.ReadAllBytes(file);
            
            uint poc = BitConverter.ToUInt32(data, 416);
            uint poh = BitConverter.ToUInt32(data, 420);
            
            if (poc > 0 && poc < 100000)
            {
                powerOnCounts.Add(poc);
                powerOnHours.Add(poh);
            }
        }

        if (powerOnCounts.Count > 0)
        {
            Console.WriteLine($"Power-On Count:");
            Console.WriteLine($"  Минимум:    {powerOnCounts.Min(),10}");
            Console.WriteLine($"  Максимум:   {powerOnCounts.Max(),10}");
            Console.WriteLine($"  Среднее:    {powerOnCounts.Average(),10:F0}");
            
            Console.WriteLine($"\nPower-On Hours:");
            Console.WriteLine($"  Минимум:    {powerOnHours.Min(),10} ч ({powerOnHours.Min()/24.0:F1} дней)");
            Console.WriteLine($"  Максимум:   {powerOnHours.Max(),10} ч ({powerOnHours.Max()/24.0:F1} дней)");
            Console.WriteLine($"  Среднее:    {powerOnHours.Average(),10:F0} ч ({powerOnHours.Average()/24.0:F1} дней)");
            
            Console.WriteLine($"\nАнализ использования:");
            if (powerOnCounts.Max() - powerOnCounts.Min() < 10)
            {
                Console.WriteLine($"  ✅ Модули использовались одинаково");
            }
            else
            {
                Console.WriteLine($"  ⚠️  Модули использовались по-разному");
                Console.WriteLine($"  Разброс: {powerOnCounts.Max() - powerOnCounts.Min()} включений");
            }
        }
        else
        {
            Console.WriteLine("  ℹ️  SMART данные не обнаружены или пусты");
        }
    }

    static void HexDump(byte[] data, int offset, int length, string prefix = "")
    {
        for (int i = offset; i < Math.Min(offset + length, data.Length); i += 16)
        {
            Console.Write($"{prefix}{i:X3}: ");
            
            for (int j = 0; j < 16 && (i + j) < data.Length; j++)
            {
                Console.Write($"{data[i + j]:X2} ");
            }
            
            for (int j = Math.Min(16, data.Length - i); j < 16; j++)
            {
                Console.Write("   ");
            }
            
            Console.Write(" ");
            
            for (int j = 0; j < 16 && (i + j) < data.Length; j++)
            {
                byte b = data[i + j];
                Console.Write((b >= 32 && b < 127) ? (char)b : '.');
            }
            
            Console.WriteLine();
        }
    }

    static ushort CalculateCrc16(byte[] data, int offset, int length)
    {
        ushort crc = 0xFFFF;
        for (int i = offset; i < offset + length && i < data.Length; i++)
        {
            crc ^= (ushort)(data[i] << 8);
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x8000) != 0)
                    crc = (ushort)((crc << 1) ^ 0x1021);
                else
                    crc <<= 1;
            }
        }
        return crc;
    }

    static List<Tuple<byte[], int>> FindPatterns(byte[] data)
    {
        var patterns = new Dictionary<string, int>();
        
        for (int len = 2; len <= 4; len++)
        {
            for (int i = 0; i <= data.Length - len; i++)
            {
                var pattern = new byte[len];
                Array.Copy(data, i, pattern, 0, len);
                
                if (pattern.All(b => b == 0) || pattern.All(b => b == 0xFF))
                    continue;
                
                string key = BitConverter.ToString(pattern);
                if (!patterns.ContainsKey(key))
                    patterns[key] = 0;
                patterns[key]++;
            }
        }
        
        return patterns.Where(p => p.Value > 2)
                       .Select(p => Tuple.Create(
                           p.Key.Split('-').Select(s => Convert.ToByte(s, 16)).ToArray(),
                           p.Value))
                       .OrderByDescending(t => t.Item2)
                       .Take(5)
                       .ToList();
    }
}


