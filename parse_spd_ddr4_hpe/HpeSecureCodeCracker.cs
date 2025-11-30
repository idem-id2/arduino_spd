using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;

class HpeSecureCodeCracker
{
    // Известная пара из HPE диагностики
    const uint KNOWN_SERIAL = 0x457661DF;
    const uint KNOWN_HASH = 0xAD642CD5;
    
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("🔓 HPE Secure Code Algorithm Cracker");
        Console.WriteLine("=====================================\n");
        
        Console.WriteLine("✅ Контрольная пара из HPE диагностики:");
        Console.WriteLine(string.Format("   S/N:  0x{0:X8}", KNOWN_SERIAL));
        Console.WriteLine(string.Format("   Hash: 0x{0:X8}\n", KNOWN_HASH));
        
        // Загружаем данные из дампов
        var files = Directory.GetFiles(".", "*.bin");
        var dumpPairs = new List<Tuple<uint, uint>>();
        
        foreach (var file in files)
        {
            var data = File.ReadAllBytes(file);
            if (data.Length < 512) continue;
            
            uint serial = BitConverter.ToUInt32(data, 325); // SPD serial
            uint hash = BitConverter.ToUInt32(data, 388);    // Secure ID at 0x184
            
            dumpPairs.Add(Tuple.Create(serial, hash));
        }
        
        Console.WriteLine(string.Format("📁 Загружено {0} пар из дампов\n", dumpPairs.Count));
        
        // Все пары для тестирования
        var allPairs = new List<Tuple<uint, uint>>();
        allPairs.Add(Tuple.Create(KNOWN_SERIAL, KNOWN_HASH));
        allPairs.AddRange(dumpPairs);
        
        Console.WriteLine("🧪 Тестирование алгоритмов...\n");
        Console.WriteLine("┌────────────────────────────────┬──────────┬────────────┐");
        Console.WriteLine("│ Алгоритм                       │ Совп.    │ Точность   │");
        Console.WriteLine("├────────────────────────────────┼──────────┼────────────┤");
        
        var algorithms = new List<Tuple<string, Func<uint, uint>>>
        {
            // Базовые алгоритмы
            Tuple.Create("CRC32", (Func<uint, uint>)CalculateCrc32),
            Tuple.Create("CRC32 + XOR const", (Func<uint, uint>)(sn => CalculateCrc32(sn) ^ 0xFFFFFFFF)),
            Tuple.Create("CRC32 + rotate", (Func<uint, uint>)(sn => RotateLeft(CalculateCrc32(sn), 16))),
            
            // XOR комбинации
            Tuple.Create("S/N XOR HPT header", (Func<uint, uint>)(sn => sn ^ 0x48505400)),
            Tuple.Create("S/N XOR P030", (Func<uint, uint>)(sn => sn ^ 0x50303033)),
            Tuple.Create("S/N XOR both", (Func<uint, uint>)(sn => sn ^ 0x48505400 ^ 0x50303033)),
            
            // Математические операции
            Tuple.Create("S/N * prime", (Func<uint, uint>)(sn => sn * 0x01000193)),
            Tuple.Create("S/N * golden ratio", (Func<uint, uint>)(sn => sn * 0x9E3779B9)),
            Tuple.Create("(S/N * prime) XOR S/N", (Func<uint, uint>)(sn => (sn * 0x01000193) ^ sn)),
            
            // Побитовые манипуляции
            Tuple.Create("Rotate + XOR", (Func<uint, uint>)(sn => RotateLeft(sn, 16) ^ sn)),
            Tuple.Create("Swap bytes + XOR", (Func<uint, uint>)(sn => SwapBytes(sn) ^ sn)),
            Tuple.Create("Mirror bits", (Func<uint, uint>)MirrorBits),
            
            // Сложные комбинации
            Tuple.Create("Hash mix v1", (Func<uint, uint>)HashMix1),
            Tuple.Create("Hash mix v2", (Func<uint, uint>)HashMix2),
            Tuple.Create("Hash mix v3", (Func<uint, uint>)HashMix3),
            Tuple.Create("Hash mix v4", (Func<uint, uint>)HashMix4),
            Tuple.Create("Hash mix v5", (Func<uint, uint>)HashMix5),
            
            // Polynomial hashes
            Tuple.Create("Poly hash 31", (Func<uint, uint>)(sn => PolyHash(sn, 31))),
            Tuple.Create("Poly hash 37", (Func<uint, uint>)(sn => PolyHash(sn, 37))),
            Tuple.Create("Poly hash 41", (Func<uint, uint>)(sn => PolyHash(sn, 41))),
            
            // Custom алгоритмы на основе анализа
            Tuple.Create("Custom v1", (Func<uint, uint>)CustomHash1),
            Tuple.Create("Custom v2", (Func<uint, uint>)CustomHash2),
            Tuple.Create("Custom v3", (Func<uint, uint>)CustomHash3),
            Tuple.Create("Custom v4", (Func<uint, uint>)CustomHash4),
            Tuple.Create("Custom v5", (Func<uint, uint>)CustomHash5),
        };
        
        var bestMatch = new { Name = "", Matches = 0, Accuracy = 0.0 };
        
        foreach (var algo in algorithms)
        {
            int matches = 0;
            foreach (var pair in allPairs)
            {
                uint calculated = algo.Item2(pair.Item1);
                if (calculated == pair.Item2)
                {
                    matches++;
                }
            }
            
            double accuracy = 100.0 * matches / allPairs.Count;
            string matchStr = string.Format("{0}/{1}", matches, allPairs.Count);
            string accStr = string.Format("{0:F1}%", accuracy);
            
            Console.WriteLine(string.Format("│ {0,-30} │ {1,-8} │ {2,-10} │", 
                algo.Item1, matchStr, accStr));
            
            if (matches > bestMatch.Matches)
            {
                bestMatch = new { Name = algo.Item1, Matches = matches, Accuracy = accuracy };
            }
        }
        
        Console.WriteLine("└────────────────────────────────┴──────────┴────────────┘\n");
        
        if (bestMatch.Matches > 0)
        {
            Console.WriteLine(string.Format("🎯 Лучший результат: {0}", bestMatch.Name));
            Console.WriteLine(string.Format("   Совпадений: {0}/{1} ({2:F1}%)\n", 
                bestMatch.Matches, allPairs.Count, bestMatch.Accuracy));
        }
        else
        {
            Console.WriteLine("❌ Ни один алгоритм не подошёл\n");
        }
        
        // Детальный анализ контрольной пары
        Console.WriteLine("🔬 Детальный анализ контрольной пары:\n");
        AnalyzeKnownPair();
        
        // Попытка брутфорса простых операций
        Console.WriteLine("\n🔍 Брутфорс простых операций...\n");
        BruteForceSimpleOps();
        
        Console.WriteLine("\n✅ Анализ завершён.");
    }
    
    static void AnalyzeKnownPair()
    {
        uint sn = KNOWN_SERIAL;
        uint hash = KNOWN_HASH;
        
        Console.WriteLine(string.Format("  S/N:        0x{0:X8} = {0}", sn));
        Console.WriteLine(string.Format("  Hash:       0x{0:X8} = {0}", hash));
        Console.WriteLine(string.Format("  XOR:        0x{0:X8}", sn ^ hash));
        Console.WriteLine(string.Format("  AND:        0x{0:X8}", sn & hash));
        Console.WriteLine(string.Format("  OR:         0x{0:X8}", sn | hash));
        Console.WriteLine(string.Format("  Diff:       {0}", (long)hash - (long)sn));
        Console.WriteLine(string.Format("  Hash / S/N: {0:F6}", (double)hash / sn));
        Console.WriteLine(string.Format("  S/N >> 16:  0x{0:X8}", sn >> 16));
        Console.WriteLine(string.Format("  Hash >> 16: 0x{0:X8}", hash >> 16));
        
        // Побайтовый анализ
        Console.WriteLine("\n  Побайтовое сравнение:");
        byte[] snBytes = BitConverter.GetBytes(sn);
        byte[] hashBytes = BitConverter.GetBytes(hash);
        
        for (int i = 0; i < 4; i++)
        {
            Console.WriteLine(string.Format("    Byte {0}: S/N=0x{1:X2}, Hash=0x{2:X2}, XOR=0x{3:X2}", 
                i, snBytes[i], hashBytes[i], snBytes[i] ^ hashBytes[i]));
        }
    }
    
    static void BruteForceSimpleOps()
    {
        uint sn = KNOWN_SERIAL;
        uint target = KNOWN_HASH;
        
        // Пробуем XOR с различными константами
        Console.WriteLine("  Тест XOR с константами:");
        uint[] testConsts = { 
            0x48505400, 0x50303033, 0xFFFFFFFF, 0x12345678, 0x9E3779B9,
            0x01000193, 0xDEADBEEF, 0xCAFEBABE, 0x00000000, 0xAAAAAAAA
        };
        
        foreach (uint c in testConsts)
        {
            uint result = sn ^ c;
            if (result == target)
            {
                Console.WriteLine(string.Format("    ✅ НАЙДЕНО: S/N XOR 0x{0:X8} = Hash", c));
                return;
            }
        }
        
        // Пробуем умножение + XOR
        Console.WriteLine("\n  Тест умножения:");
        uint[] primes = { 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97 };
        
        foreach (uint p in primes)
        {
            uint result = sn * p;
            if (result == target)
            {
                Console.WriteLine(string.Format("    ✅ НАЙДЕНО: S/N * {0} = Hash", p));
                return;
            }
            
            result = (sn * p) ^ sn;
            if (result == target)
            {
                Console.WriteLine(string.Format("    ✅ НАЙДЕНО: (S/N * {0}) XOR S/N = Hash", p));
                return;
            }
        }
        
        // Пробуем сдвиги + операции
        Console.WriteLine("\n  Тест сдвигов:");
        for (int shift = 1; shift < 32; shift++)
        {
            uint result = (sn << shift) ^ (sn >> (32 - shift));
            if (result == target)
            {
                Console.WriteLine(string.Format("    ✅ НАЙДЕНО: (S/N << {0}) XOR (S/N >> {1}) = Hash", 
                    shift, 32 - shift));
                return;
            }
        }
        
        Console.WriteLine("    ❌ Простые операции не найдены");
    }
    
    // ===== АЛГОРИТМЫ =====
    
    static uint CalculateCrc32(uint serial)
    {
        byte[] data = BitConverter.GetBytes(serial);
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
    
    static uint RotateLeft(uint value, int shift)
    {
        return (value << shift) | (value >> (32 - shift));
    }
    
    static uint SwapBytes(uint value)
    {
        return ((value & 0x000000FF) << 24) |
               ((value & 0x0000FF00) << 8) |
               ((value & 0x00FF0000) >> 8) |
               ((value & 0xFF000000) >> 24);
    }
    
    static uint MirrorBits(uint value)
    {
        uint result = 0;
        for (int i = 0; i < 32; i++)
        {
            if ((value & (1u << i)) != 0)
                result |= 1u << (31 - i);
        }
        return result;
    }
    
    static uint PolyHash(uint serial, uint prime)
    {
        byte[] data = BitConverter.GetBytes(serial);
        uint hash = 0;
        
        foreach (byte b in data)
        {
            hash = hash * prime + b;
        }
        
        return hash;
    }
    
    static uint HashMix1(uint x)
    {
        x ^= x >> 16;
        x *= 0x85EBCA6B;
        x ^= x >> 13;
        x *= 0xC2B2AE35;
        x ^= x >> 16;
        return x;
    }
    
    static uint HashMix2(uint x)
    {
        x = ((x >> 16) ^ x) * 0x45D9F3B;
        x = ((x >> 16) ^ x) * 0x45D9F3B;
        x = (x >> 16) ^ x;
        return x;
    }
    
    static uint HashMix3(uint x)
    {
        x ^= (x << 13);
        x ^= (x >> 17);
        x ^= (x << 5);
        return x;
    }
    
    static uint HashMix4(uint x)
    {
        x = (x ^ 61) ^ (x >> 16);
        x = x + (x << 3);
        x = x ^ (x >> 4);
        x = x * 0x27D4EB2D;
        x = x ^ (x >> 15);
        return x;
    }
    
    static uint HashMix5(uint x)
    {
        uint h = x;
        h ^= h >> 15;
        h *= 0x2C1B3C6D;
        h ^= h >> 12;
        h *= 0x297A2D39;
        h ^= h >> 15;
        return h;
    }
    
    static uint CustomHash1(uint x)
    {
        // Попытка: комбинация с HPE константами
        x ^= 0x48505400; // "HPT\0"
        x = RotateLeft(x, 13);
        x ^= 0x50303033; // "P030"
        return x;
    }
    
    static uint CustomHash2(uint x)
    {
        // CRC32 + манипуляции
        uint crc = CalculateCrc32(x);
        return crc ^ RotateLeft(x, 16);
    }
    
    static uint CustomHash3(uint x)
    {
        // Polynomial с модификацией
        byte[] data = BitConverter.GetBytes(x);
        uint hash = 0x811C9DC5; // FNV offset basis
        
        foreach (byte b in data)
        {
            hash ^= b;
            hash *= 0x01000193; // FNV prime
        }
        
        return hash;
    }
    
    static uint CustomHash4(uint x)
    {
        // Комбинация сдвигов специфичная для наблюдаемых данных
        uint h = x;
        h ^= (h >> 11);
        h += (h << 7);
        h ^= (h >> 18);
        return h;
    }
    
    static uint CustomHash5(uint x)
    {
        // Murmur-like hash
        uint h = x;
        h ^= h >> 16;
        h *= 0x85EBCA6B;
        h ^= h >> 13;
        h *= 0xC2B2AE35;
        h ^= h >> 16;
        return h;
    }
}

