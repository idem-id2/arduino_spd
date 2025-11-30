using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

class HpeAdvancedCracker
{
    const uint KNOWN_SERIAL = 0x457661DF;
    const uint KNOWN_HASH = 0xAD642CD5;
    const string KNOWN_PART = "P03053-0A1";
    
    class ModuleData
    {
        public uint Serial;
        public uint Hash;
        public string PartNumber;
        public byte ManufWeek;
        public byte ManufYear;
        public byte[] FullSpd;
    }
    
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("🔓 HPE Secure Code Advanced Cracker");
        Console.WriteLine("====================================\n");
        
        var modules = new List<ModuleData>();
        
        // Добавляем известный модуль
        modules.Add(new ModuleData
        {
            Serial = KNOWN_SERIAL,
            Hash = KNOWN_HASH,
            PartNumber = KNOWN_PART,
            ManufWeek = 21,
            ManufYear = 23
        });
        
        // Загружаем дампы
        var files = Directory.GetFiles(".", "*.bin");
        foreach (var file in files)
        {
            var data = File.ReadAllBytes(file);
            if (data.Length < 512) continue;
            
            var mod = new ModuleData
            {
                Serial = BitConverter.ToUInt32(data, 325),
                Hash = BitConverter.ToUInt32(data, 388),
                PartNumber = ExtractPartNumber(data),
                ManufWeek = data[323],
                ManufYear = data[324],
                FullSpd = data
            };
            
            modules.Add(mod);
        }
        
        Console.WriteLine(string.Format("📁 Загружено модулей: {0}\n", modules.Count));
        
        // Статистический анализ
        Console.WriteLine("📊 Статистический анализ:\n");
        AnalyzeStatistics(modules);
        
        // Поиск корреляций
        Console.WriteLine("\n🔍 Поиск корреляций...\n");
        FindCorrelations(modules);
        
        // Тест с Part Number
        Console.WriteLine("\n🧪 Тест комбинаций с Part Number...\n");
        TestWithPartNumber(modules);
        
        // Тест с датой производства
        Console.WriteLine("\n📅 Тест с датой производства...\n");
        TestWithManufDate(modules);
        
        // Тест криптографических хешей
        Console.WriteLine("\n🔐 Тест криптографических алгоритмов...\n");
        TestCryptoHashes(modules);
        
        // Анализ энтропии
        Console.WriteLine("\n📈 Анализ энтропии hash значений...\n");
        AnalyzeEntropy(modules);
        
        Console.WriteLine("\n✅ Анализ завершён.");
    }
    
    static void AnalyzeStatistics(List<ModuleData> modules)
    {
        var serials = modules.Select(m => m.Serial).ToArray();
        var hashes = modules.Select(m => m.Hash).ToArray();
        
        Console.WriteLine("  Серийные номера:");
        Console.WriteLine(string.Format("    Min: 0x{0:X8}", serials.Min()));
        Console.WriteLine(string.Format("    Max: 0x{0:X8}", serials.Max()));
        Console.WriteLine(string.Format("    Avg: 0x{0:X8}", (uint)serials.Average(s => (long)s)));
        
        Console.WriteLine("\n  Hash значения:");
        Console.WriteLine(string.Format("    Min: 0x{0:X8}", hashes.Min()));
        Console.WriteLine(string.Format("    Max: 0x{0:X8}", hashes.Max()));
        Console.WriteLine(string.Format("    Avg: 0x{0:X8}", (uint)hashes.Average(h => (long)h)));
        
        // Коэффициенты Hash/Serial
        Console.WriteLine("\n  Коэффициенты Hash/Serial:");
        var ratios = new List<double>();
        for (int i = 0; i < Math.Min(5, modules.Count); i++)
        {
            double ratio = (double)modules[i].Hash / modules[i].Serial;
            ratios.Add(ratio);
            Console.WriteLine(string.Format("    0x{0:X8} / 0x{1:X8} = {2:F6}", 
                modules[i].Hash, modules[i].Serial, ratio));
        }
        
        if (ratios.Count > 1)
        {
            double avgRatio = ratios.Average();
            double stdDev = Math.Sqrt(ratios.Average(r => Math.Pow(r - avgRatio, 2)));
            Console.WriteLine(string.Format("    Среднее: {0:F6} ± {1:F6}", avgRatio, stdDev));
        }
    }
    
    static void FindCorrelations(List<ModuleData> modules)
    {
        // Проверяем, есть ли закономерность между соседними байтами
        Console.WriteLine("  Побайтовый анализ:");
        
        for (int bytePos = 0; bytePos < 4; bytePos++)
        {
            Console.WriteLine(string.Format("\n  Байт {0}:", bytePos));
            
            for (int i = 0; i < Math.Min(5, modules.Count); i++)
            {
                byte snByte = (byte)((modules[i].Serial >> (bytePos * 8)) & 0xFF);
                byte hashByte = (byte)((modules[i].Hash >> (bytePos * 8)) & 0xFF);
                int diff = hashByte - snByte;
                
                Console.WriteLine(string.Format("    S/N[{0}]=0x{1:X2} → Hash[{0}]=0x{2:X2} (diff={3,4})", 
                    bytePos, snByte, hashByte, diff));
            }
        }
    }
    
    static void TestWithPartNumber(List<ModuleData> modules)
    {
        // Попытка: Serial + PartNumber → Hash
        foreach (var mod in modules.Take(3))
        {
            if (string.IsNullOrEmpty(mod.PartNumber)) continue;
            
            // Вариант 1: Serial XOR Part
            var partBytes = Encoding.ASCII.GetBytes(mod.PartNumber);
            uint partHash = 0;
            foreach (byte b in partBytes)
            {
                partHash = (partHash << 5) + partHash + b;
            }
            
            uint test1 = mod.Serial ^ partHash;
            bool match1 = (test1 == mod.Hash);
            
            Console.WriteLine(string.Format("  S/N 0x{0:X8} + Part '{1}':", mod.Serial, mod.PartNumber));
            Console.WriteLine(string.Format("    S/N XOR PartHash:  0x{0:X8} {1}", 
                test1, match1 ? "✅" : "❌"));
            
            // Вариант 2: Комбинированный hash
            using (var sha = SHA256.Create())
            {
                var combined = new List<byte>();
                combined.AddRange(BitConverter.GetBytes(mod.Serial));
                combined.AddRange(partBytes);
                
                var hash = sha.ComputeHash(combined.ToArray());
                uint test2 = BitConverter.ToUInt32(hash, 0);
                bool match2 = (test2 == mod.Hash);
                
                Console.WriteLine(string.Format("    SHA256(S/N+Part):  0x{0:X8} {1}", 
                    test2, match2 ? "✅" : "❌"));
            }
        }
    }
    
    static void TestWithManufDate(List<ModuleData> modules)
    {
        foreach (var mod in modules.Take(3))
        {
            // Попытка включить дату в вычисление
            ushort date = (ushort)((mod.ManufYear << 8) | mod.ManufWeek);
            
            uint test1 = mod.Serial ^ date;
            uint test2 = mod.Serial ^ (uint)(date << 16);
            uint test3 = mod.Serial + (uint)(date * 1000);
            
            Console.WriteLine(string.Format("  S/N 0x{0:X8}, Date {1}/20{2}:", 
                mod.Serial, mod.ManufWeek, mod.ManufYear));
            Console.WriteLine(string.Format("    S/N XOR date:      0x{0:X8} {1}", 
                test1, (test1 == mod.Hash) ? "✅" : "❌"));
            Console.WriteLine(string.Format("    S/N XOR (date<<16): 0x{0:X8} {1}", 
                test2, (test2 == mod.Hash) ? "✅" : "❌"));
            Console.WriteLine(string.Format("    S/N + (date*1000): 0x{0:X8} {1}", 
                test3, (test3 == mod.Hash) ? "✅" : "❌"));
        }
    }
    
    static void TestCryptoHashes(List<ModuleData> modules)
    {
        var knownKeys = new string[] 
        { 
            "HPE", "HEWLETT-PACKARD", "SMARTMEMORY", "P03053", 
            "SECRET", "KEY", "0123456789ABCDEF" 
        };
        
        foreach (var key in knownKeys)
        {
            int matches = 0;
            var keyBytes = Encoding.ASCII.GetBytes(key);
            
            using (var hmac = new HMACSHA256(keyBytes))
            {
                foreach (var mod in modules)
                {
                    var snBytes = BitConverter.GetBytes(mod.Serial);
                    var hash = hmac.ComputeHash(snBytes);
                    uint result = BitConverter.ToUInt32(hash, 0);
                    
                    if (result == mod.Hash) matches++;
                }
            }
            
            if (matches > 0)
            {
                Console.WriteLine(string.Format("  ✅ HMAC-SHA256 с ключом '{0}': {1} совпадений!", 
                    key, matches));
            }
        }
        
        Console.WriteLine("  ❌ Стандартные ключи не подошли");
    }
    
    static void AnalyzeEntropy(List<ModuleData> modules)
    {
        // Анализ распределения битов в hash
        int[] bitCounts = new int[32];
        
        foreach (var mod in modules)
        {
            for (int i = 0; i < 32; i++)
            {
                if ((mod.Hash & (1u << i)) != 0)
                    bitCounts[i]++;
            }
        }
        
        Console.WriteLine("  Распределение битов в hash (ожидается ~50%):");
        for (int i = 0; i < 32; i += 8)
        {
            Console.Write("    Биты " + i.ToString("D2") + "-" + (i + 7).ToString("D2") + ": ");
            for (int j = i; j < i + 8 && j < 32; j++)
            {
                double percent = 100.0 * bitCounts[j] / modules.Count;
                Console.Write(string.Format("{0:F0}% ", percent));
            }
            Console.WriteLine();
        }
        
        // Hamming distance между hash значениями
        if (modules.Count >= 2)
        {
            Console.WriteLine("\n  Hamming distance между парами hash:");
            for (int i = 0; i < Math.Min(3, modules.Count - 1); i++)
            {
                uint xor = modules[i].Hash ^ modules[i + 1].Hash;
                int hammingDist = CountBits(xor);
                Console.WriteLine(string.Format("    Hash[{0}] vs Hash[{1}]: {2} бит различий", 
                    i, i + 1, hammingDist));
            }
        }
    }
    
    static int CountBits(uint value)
    {
        int count = 0;
        while (value != 0)
        {
            count += (int)(value & 1);
            value >>= 1;
        }
        return count;
    }
    
    static string ExtractPartNumber(byte[] data)
    {
        // Part Number обычно в байтах 329-348 (DDR4 SPD)
        if (data.Length < 349) return "";
        
        var sb = new StringBuilder();
        for (int i = 329; i < 349; i++)
        {
            if (data[i] >= 32 && data[i] < 127)
                sb.Append((char)data[i]);
            else if (data[i] == 0)
                break;
        }
        
        return sb.ToString().Trim();
    }
}

