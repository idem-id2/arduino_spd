using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;

class HpePatternFinder
{
    class ModuleData
    {
        public byte[] FullData;
        public uint Hash;
        public uint Serial;
        public string FileName;
        public byte[] ManufData; // 349-382
    }
    
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("🔍 HPE Pattern Finder - Детальный анализ");
        Console.WriteLine("=========================================\n");
        
        var modules = new List<ModuleData>();
        
        var files = Directory.GetFiles(".", "*.bin");
        foreach (var file in files)
        {
            var data = File.ReadAllBytes(file);
            if (data.Length < 512) continue;
            
            var mod = new ModuleData
            {
                FullData = data,
                Hash = BitConverter.ToUInt32(data, 388),
                Serial = BitConverter.ToUInt32(data, 325),
                FileName = Path.GetFileName(file),
                ManufData = new byte[34]
            };
            
            Array.Copy(data, 349, mod.ManufData, 0, 34);
            modules.Add(mod);
        }
        
        Console.WriteLine(string.Format("📁 Загружено: {0} модулей\n", modules.Count));
        
        // Анализ различий в Manufacturing Data
        Console.WriteLine("🔬 Анализ Manufacturing Data (349-382)...\n");
        AnalyzeManufacturingData(modules);
        
        // Поиск байтов которые коррелируют с hash
        Console.WriteLine("\n🎯 Поиск корреляций между данными и hash...\n");
        FindCorrelations(modules);
        
        // Анализ конкретных байтов что различаются
        Console.WriteLine("\n📊 Анализ изменяющихся байтов...\n");
        AnalyzeChangingBytes(modules);
        
        // Попытка найти формулу
        Console.WriteLine("\n🧮 Попытка найти математическую формулу...\n");
        TryFormulas(modules);
        
        Console.WriteLine("\n✅ Анализ завершён.");
    }
    
    static void AnalyzeManufacturingData(List<ModuleData> modules)
    {
        Console.WriteLine("  Сравнение Manufacturing Data между модулями:\n");
        
        // Найдем какие байты различаются
        bool[] varies = new bool[34];
        
        for (int i = 0; i < 34; i++)
        {
            var values = modules.Select(m => m.ManufData[i]).Distinct().ToArray();
            varies[i] = (values.Length > 1);
            
            if (varies[i])
            {
                Console.WriteLine(string.Format("    Байт {0} (offset {1}): {2} различных значений", 
                    i, 349 + i, values.Length));
                
                // Показываем первые несколько
                foreach (var mod in modules.Take(5))
                {
                    Console.WriteLine(string.Format("      {0}: 0x{1:X2} (Hash: 0x{2:X8})", 
                        Path.GetFileNameWithoutExtension(mod.FileName).Substring(0, 20), 
                        mod.ManufData[i], mod.Hash));
                }
                Console.WriteLine();
            }
        }
        
        int varyingCount = varies.Count(v => v);
        Console.WriteLine(string.Format("  Итого изменяющихся байт: {0}/34\n", varyingCount));
    }
    
    static void FindCorrelations(List<ModuleData> modules)
    {
        // Проверяем каждый байт SPD на корреляцию с hash
        Console.WriteLine("  Тестирование байтов SPD на корреляцию с hash...\n");
        
        var strongCorrelations = new List<Tuple<int, double>>();
        
        for (int offset = 320; offset < 388; offset++)
        {
            var byteValues = modules.Select(m => (double)m.FullData[offset]).ToArray();
            var hashValues = modules.Select(m => (double)m.Hash).ToArray();
            
            double correlation = CalculateCorrelation(byteValues, hashValues);
            
            if (Math.Abs(correlation) > 0.3)
            {
                strongCorrelations.Add(Tuple.Create(offset, correlation));
            }
        }
        
        if (strongCorrelations.Any())
        {
            Console.WriteLine("  Найдены сильные корреляции:\n");
            foreach (var corr in strongCorrelations.OrderByDescending(c => Math.Abs(c.Item2)))
            {
                Console.WriteLine(string.Format("    Байт {0} (0x{0:X}): корреляция {1:F3}", 
                    corr.Item1, corr.Item2));
            }
        }
        else
        {
            Console.WriteLine("  ❌ Сильных корреляций не найдено");
        }
    }
    
    static double CalculateCorrelation(double[] x, double[] y)
    {
        if (x.Length != y.Length) return 0;
        
        double meanX = x.Average();
        double meanY = y.Average();
        
        double numerator = 0;
        double denomX = 0;
        double denomY = 0;
        
        for (int i = 0; i < x.Length; i++)
        {
            double dx = x[i] - meanX;
            double dy = y[i] - meanY;
            
            numerator += dx * dy;
            denomX += dx * dx;
            denomY += dy * dy;
        }
        
        if (denomX == 0 || denomY == 0) return 0;
        
        return numerator / Math.Sqrt(denomX * denomY);
    }
    
    static void AnalyzeChangingBytes(List<ModuleData> modules)
    {
        // Фокусируемся на байтах которые различаются
        Console.WriteLine("  Детальный анализ изменяющихся байтов:\n");
        
        // Serial Number (325-328) - всегда различается
        Console.WriteLine("  Serial Number vs Hash:");
        foreach (var mod in modules.Take(10))
        {
            Console.WriteLine(string.Format("    S/N: 0x{0:X8} → Hash: 0x{1:X8} (XOR: 0x{2:X8})", 
                mod.Serial, mod.Hash, mod.Serial ^ mod.Hash));
        }
        
        // Проверяем специфические байты Manufacturing Data
        Console.WriteLine("\n  Manufacturing Data специфические поля:");
        
        for (int i = 0; i < 34; i++)
        {
            var uniqueVals = modules.Select(m => m.ManufData[i]).Distinct().Count();
            if (uniqueVals > 1 && uniqueVals < modules.Count)
            {
                Console.WriteLine(string.Format("\n    Offset {0} (абс {1}):", i, 349 + i));
                var groups = modules.GroupBy(m => m.ManufData[i]);
                foreach (var g in groups)
                {
                    var avgHash = g.Average(m => (double)m.Hash);
                    Console.WriteLine(string.Format("      Значение 0x{0:X2}: {1} модулей, avg hash 0x{2:X8}", 
                        g.Key, g.Count(), (uint)avgHash));
                }
            }
        }
    }
    
    static void TryFormulas(List<ModuleData> modules)
    {
        Console.WriteLine("  Пробуем различные комбинации...\n");
        
        // Формула 1: Hash зависит только от байтов которые различаются
        var varyingBytes = new List<int>();
        for (int i = 320; i < 388; i++)
        {
            if (modules.Select(m => m.FullData[i]).Distinct().Count() > 1)
            {
                varyingBytes.Add(i);
            }
        }
        
        Console.WriteLine(string.Format("    Изменяющихся байтов: {0}", varyingBytes.Count));
        Console.WriteLine("    Позиции: " + string.Join(", ", varyingBytes.Take(10)));
        
        // Пробуем простую сумму
        Console.WriteLine("\n  Тест простой суммы изменяющихся байтов:");
        foreach (var mod in modules.Take(5))
        {
            uint sum = 0;
            foreach (var offset in varyingBytes)
            {
                sum += mod.FullData[offset];
            }
            
            Console.WriteLine(string.Format("    Sum: 0x{0:X8}, Hash: 0x{1:X8} {2}", 
                sum, mod.Hash, sum == mod.Hash ? "✅" : "❌"));
        }
        
        // Пробуем CRC32 только от изменяющихся байтов
        Console.WriteLine("\n  Тест CRC32 от изменяющихся байтов:");
        foreach (var mod in modules.Take(5))
        {
            var varyingData = varyingBytes.Select(offset => mod.FullData[offset]).ToArray();
            uint crc = CalculateCrc32(varyingData);
            
            Console.WriteLine(string.Format("    CRC: 0x{0:X8}, Hash: 0x{1:X8} {2}", 
                crc, mod.Hash, crc == mod.Hash ? "✅" : "❌"));
        }
        
        // Попытка найти lookup table
        Console.WriteLine("\n  Поиск возможной lookup table:");
        CheckLookupTable(modules);
    }
    
    static void CheckLookupTable(List<ModuleData> modules)
    {
        // Возможно hash хранится в одном из блоков SPD
        // Проверяем блоки по 4 байта
        
        for (int searchOffset = 0; searchOffset < 320; searchOffset += 4)
        {
            int matches = 0;
            
            foreach (var mod in modules)
            {
                uint value = BitConverter.ToUInt32(mod.FullData, searchOffset);
                if (value == mod.Hash) matches++;
            }
            
            if (matches > 0)
            {
                Console.WriteLine(string.Format("    ✅ Offset {0} (0x{0:X}): {1} совпадений!", 
                    searchOffset, matches));
            }
        }
        
        Console.WriteLine("    Проверка завершена");
    }
    
    static uint CalculateCrc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 1) != 0)
                    crc = (crc >> 1) ^ 0xEDB88320;
                else
                    crc >>= 1;
            }
        }
        
        return ~crc;
    }
}

