using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;

class HpeFinalCracker
{
    const int DATA_BLOCK_START = 320;  // 0x140
    const int DATA_BLOCK_END = 387;    // 0x183
    const int DATA_BLOCK_SIZE = 68;    // 320-387 (без самого hash)
    const int HASH_OFFSET = 388;       // 0x184 - позиция Secure ID
    
    const uint KNOWN_SERIAL = 0x457661DF;
    const uint KNOWN_HASH = 0xAD642CD5;
    
    class ModuleData
    {
        public byte[] DataBlock;  // 320-387 (68 bytes)
        public uint Hash;         // 388-391 (4 bytes)
        public string FileName;
        public uint Serial;
        public string PartNumber;
    }
    
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("🔓 HPE Secure Code Final Cracker");
        Console.WriteLine("=================================\n");
        Console.WriteLine("Гипотеза: Secure ID = Hash(байты 320-387)\n");
        
        var modules = new List<ModuleData>();
        
        // Загружаем все дампы
        var files = Directory.GetFiles(".", "*.bin");
        Console.WriteLine(string.Format("📁 Найдено файлов: {0}\n", files.Length));
        
        foreach (var file in files)
        {
            var data = File.ReadAllBytes(file);
            if (data.Length < 512) continue;
            
            var mod = new ModuleData
            {
                DataBlock = new byte[DATA_BLOCK_SIZE],
                Hash = BitConverter.ToUInt32(data, HASH_OFFSET),
                FileName = Path.GetFileName(file),
                Serial = BitConverter.ToUInt32(data, 325),
                PartNumber = ExtractPartNumber(data)
            };
            
            Array.Copy(data, DATA_BLOCK_START, mod.DataBlock, 0, DATA_BLOCK_SIZE);
            modules.Add(mod);
            
            Console.WriteLine(string.Format("  {0}", mod.FileName));
            Console.WriteLine(string.Format("    S/N: 0x{0:X8}, Hash: 0x{1:X8}, Part: {2}", 
                mod.Serial, mod.Hash, mod.PartNumber));
        }
        
        Console.WriteLine("\n════════════════════════════════════════════════════════\n");
        Console.WriteLine("🧪 Тестирование криптографических хешей от блока 320-387...\n");
        
        TestHashAlgorithms(modules);
        
        Console.WriteLine("\n════════════════════════════════════════════════════════\n");
        Console.WriteLine("🔍 Тестирование CRC32 вариаций...\n");
        
        TestCrcVariations(modules);
        
        Console.WriteLine("\n════════════════════════════════════════════════════════\n");
        Console.WriteLine("🔬 Анализ структуры блока 320-387...\n");
        
        AnalyzeDataBlock(modules);
        
        Console.WriteLine("\n════════════════════════════════════════════════════════\n");
        Console.WriteLine("🎲 Брутфорс XOR ключей...\n");
        
        BruteForceXorKeys(modules);
        
        Console.WriteLine("\n✅ Анализ завершён.");
    }
    
    static void TestHashAlgorithms(List<ModuleData> modules)
    {
        var algorithms = new Dictionary<string, Func<byte[], byte[]>>
        {
            { "MD5", data => MD5.Create().ComputeHash(data) },
            { "SHA1", data => SHA1.Create().ComputeHash(data) },
            { "SHA256", data => SHA256.Create().ComputeHash(data) },
            { "SHA384", data => SHA384.Create().ComputeHash(data) },
            { "SHA512", data => SHA512.Create().ComputeHash(data) },
        };
        
        foreach (var algo in algorithms)
        {
            int matches = 0;
            
            foreach (var mod in modules)
            {
                var hash = algo.Value(mod.DataBlock);
                uint hash32 = BitConverter.ToUInt32(hash, 0);
                
                if (hash32 == mod.Hash) matches++;
            }
            
            string status = matches > 0 
                ? string.Format("✅ {0} совпадений!", matches)
                : "❌";
            
            Console.WriteLine(string.Format("  {0,-15}: {1}", algo.Key, status));
        }
        
        // Тест с разными частями хеша
        Console.WriteLine("\n  Тестирование разных частей SHA256:");
        foreach (var mod in modules.Take(3))
        {
            var sha256 = SHA256.Create().ComputeHash(mod.DataBlock);
            
            Console.WriteLine(string.Format("\n  Файл: {0}", mod.FileName));
            Console.WriteLine(string.Format("    Целевой hash: 0x{0:X8}", mod.Hash));
            Console.WriteLine(string.Format("    SHA256[0:4]:  0x{0:X8} {1}", 
                BitConverter.ToUInt32(sha256, 0),
                BitConverter.ToUInt32(sha256, 0) == mod.Hash ? "✅" : "❌"));
            Console.WriteLine(string.Format("    SHA256[4:8]:  0x{0:X8} {1}", 
                BitConverter.ToUInt32(sha256, 4),
                BitConverter.ToUInt32(sha256, 4) == mod.Hash ? "✅" : "❌"));
            Console.WriteLine(string.Format("    SHA256[8:12]: 0x{0:X8} {1}", 
                BitConverter.ToUInt32(sha256, 8),
                BitConverter.ToUInt32(sha256, 8) == mod.Hash ? "✅" : "❌"));
        }
    }
    
    static void TestCrcVariations(List<ModuleData> modules)
    {
        var crcTests = new Dictionary<string, Func<byte[], uint>>
        {
            { "CRC32", data => CalculateCrc32(data, 0xFFFFFFFF, 0xEDB88320, true) },
            { "CRC32 (no init)", data => CalculateCrc32(data, 0x00000000, 0xEDB88320, true) },
            { "CRC32 (no final XOR)", data => CalculateCrc32(data, 0xFFFFFFFF, 0xEDB88320, false) },
            { "CRC32-C (Castagnoli)", data => CalculateCrc32(data, 0xFFFFFFFF, 0x82F63B78, true) },
            { "CRC32-K (Koopman)", data => CalculateCrc32(data, 0xFFFFFFFF, 0xEB31D82E, true) },
            { "CRC32-Q", data => CalculateCrc32(data, 0x00000000, 0x814141AB, false) },
        };
        
        foreach (var test in crcTests)
        {
            int matches = 0;
            
            foreach (var mod in modules)
            {
                uint crc = test.Value(mod.DataBlock);
                if (crc == mod.Hash) matches++;
            }
            
            string status = matches > 0 
                ? string.Format("✅ {0} совпадений!", matches)
                : "❌";
            
            Console.WriteLine(string.Format("  {0,-25}: {1}", test.Key, status));
        }
        
        // Тест CRC + XOR
        Console.WriteLine("\n  Тестирование CRC32 + XOR константа:");
        
        uint[] xorConsts = { 0xFFFFFFFF, 0x48505400, 0x50303033, 0x12345678, 0xABCDEF00 };
        
        foreach (uint xorConst in xorConsts)
        {
            int matches = 0;
            
            foreach (var mod in modules)
            {
                uint crc = CalculateCrc32(mod.DataBlock, 0xFFFFFFFF, 0xEDB88320, true);
                uint result = crc ^ xorConst;
                
                if (result == mod.Hash) matches++;
            }
            
            if (matches > 0)
            {
                Console.WriteLine(string.Format("    ✅ CRC32 XOR 0x{0:X8}: {1} совпадений!", 
                    xorConst, matches));
            }
        }
    }
    
    static void AnalyzeDataBlock(List<ModuleData> modules)
    {
        Console.WriteLine("  Структура блока 320-387 для первых 3 модулей:\n");
        
        foreach (var mod in modules.Take(3))
        {
            Console.WriteLine(string.Format("  📦 {0}", mod.FileName));
            Console.WriteLine(string.Format("     Target Hash: 0x{0:X8}\n", mod.Hash));
            
            // Показываем структуру блока
            Console.WriteLine("     320-323 (Manufacturer ID):");
            Console.WriteLine("       " + BitConverter.ToString(mod.DataBlock, 0, 4));
            
            Console.WriteLine("     324 (Location): 0x" + mod.DataBlock[4].ToString("X2"));
            
            Console.WriteLine("     325-328 (Serial):");
            Console.WriteLine("       " + BitConverter.ToString(mod.DataBlock, 5, 4));
            
            Console.WriteLine("     329-348 (Part Number):");
            var partBytes = new byte[20];
            Array.Copy(mod.DataBlock, 9, partBytes, 0, 20);
            Console.WriteLine("       " + Encoding.ASCII.GetString(partBytes).TrimEnd('\0'));
            Console.WriteLine("       " + BitConverter.ToString(partBytes));
            
            Console.WriteLine("     349-382 (Manuf Data):");
            Console.WriteLine("       " + BitConverter.ToString(mod.DataBlock, 29, 34));
            
            Console.WriteLine("     384-387 (HPE Header 'HPT'):");
            Console.WriteLine("       " + BitConverter.ToString(mod.DataBlock, 64, 4));
            
            Console.WriteLine();
        }
    }
    
    static void BruteForceXorKeys(List<ModuleData> modules)
    {
        Console.WriteLine("  Поиск XOR ключа методом известного текста...\n");
        
        // Предполагаем, что алгоритм: Hash = CRC32(data) XOR key
        // Можем вычислить key = Hash XOR CRC32(data)
        
        var possibleKeys = new Dictionary<uint, int>();
        
        foreach (var mod in modules)
        {
            uint crc = CalculateCrc32(mod.DataBlock, 0xFFFFFFFF, 0xEDB88320, true);
            uint key = mod.Hash ^ crc;
            
            if (!possibleKeys.ContainsKey(key))
                possibleKeys[key] = 0;
            
            possibleKeys[key]++;
        }
        
        Console.WriteLine("  Топ-5 кандидатов на XOR ключ:\n");
        
        var topKeys = possibleKeys.OrderByDescending(kv => kv.Value).Take(5);
        
        foreach (var kv in topKeys)
        {
            Console.WriteLine(string.Format("    0x{0:X8}: встречается в {1} модулях {2}", 
                kv.Key, kv.Value,
                kv.Value == modules.Count ? "✅ ВСЕ!" : ""));
            
            if (kv.Value == modules.Count)
            {
                Console.WriteLine("\n    🎯 НАЙДЕН КЛЮЧ! Проверка...\n");
                VerifyKey(modules, kv.Key);
                return;
            }
        }
        
        Console.WriteLine("\n  ❌ Единый XOR ключ не найден");
        
        // Попробуем побайтовый XOR
        Console.WriteLine("\n  Попытка побайтового XOR...\n");
        TryByteWiseXor(modules);
    }
    
    static void TryByteWiseXor(List<ModuleData> modules)
    {
        // Для каждого байта пытаемся найти общий XOR ключ
        byte[] keyBytes = new byte[4];
        bool[] keyFound = new bool[4];
        
        for (int bytePos = 0; bytePos < 4; bytePos++)
        {
            var byteCandidates = new Dictionary<byte, int>();
            
            foreach (var mod in modules)
            {
                uint crc = CalculateCrc32(mod.DataBlock, 0xFFFFFFFF, 0xEDB88320, true);
                byte crcByte = (byte)((crc >> (bytePos * 8)) & 0xFF);
                byte hashByte = (byte)((mod.Hash >> (bytePos * 8)) & 0xFF);
                byte keyByte = (byte)(hashByte ^ crcByte);
                
                if (!byteCandidates.ContainsKey(keyByte))
                    byteCandidates[keyByte] = 0;
                
                byteCandidates[keyByte]++;
            }
            
            var bestByte = byteCandidates.OrderByDescending(kv => kv.Value).First();
            
            if (bestByte.Value == modules.Count)
            {
                keyBytes[bytePos] = bestByte.Key;
                keyFound[bytePos] = true;
                Console.WriteLine(string.Format("    Байт {0}: 0x{1:X2} ✅ (все модули)", 
                    bytePos, bestByte.Key));
            }
            else
            {
                Console.WriteLine(string.Format("    Байт {0}: не найден единый ключ", bytePos));
            }
        }
        
        if (keyFound.All(f => f))
        {
            uint fullKey = BitConverter.ToUInt32(keyBytes, 0);
            Console.WriteLine(string.Format("\n    🎯 НАЙДЕН ПОБАЙТОВЫЙ КЛЮЧ: 0x{0:X8}\n", fullKey));
            VerifyKey(modules, fullKey);
        }
    }
    
    static void VerifyKey(List<ModuleData> modules, uint key)
    {
        Console.WriteLine(string.Format("  Проверка ключа 0x{0:X8}:\n", key));
        
        int successes = 0;
        foreach (var mod in modules)
        {
            uint crc = CalculateCrc32(mod.DataBlock, 0xFFFFFFFF, 0xEDB88320, true);
            uint calculated = crc ^ key;
            bool match = (calculated == mod.Hash);
            
            if (match) successes++;
            
            if (modules.IndexOf(mod) < 5 || !match)
            {
                Console.WriteLine(string.Format("    {0}:", mod.FileName));
                Console.WriteLine(string.Format("      CRC32:      0x{0:X8}", crc));
                Console.WriteLine(string.Format("      Calculated: 0x{0:X8}", calculated));
                Console.WriteLine(string.Format("      Expected:   0x{0:X8} {1}", 
                    mod.Hash, match ? "✅" : "❌"));
            }
        }
        
        Console.WriteLine(string.Format("\n  📊 Результат: {0}/{1} совпадений ({2:F1}%)", 
            successes, modules.Count, 100.0 * successes / modules.Count));
        
        if (successes == modules.Count)
        {
            Console.WriteLine("\n  🎉 АЛГОРИТМ НАЙДЕН!");
            Console.WriteLine(string.Format("  ════════════════════════════════════════════════════════"));
            Console.WriteLine(string.Format("  HPE Secure ID = CRC32(bytes 320-387) XOR 0x{0:X8}", key));
            Console.WriteLine(string.Format("  ════════════════════════════════════════════════════════"));
            
            SaveAlgorithm(key);
        }
    }
    
    static void SaveAlgorithm(uint key)
    {
        string code = string.Format(@"
// ✅ НАЙДЕННЫЙ АЛГОРИТМ HPE SECURE ID

uint CalculateHpeSecureId(byte[] spdData)
{{
    // Извлекаем блок данных 320-387 (68 bytes)
    byte[] dataBlock = new byte[68];
    Array.Copy(spdData, 320, dataBlock, 0, 68);
    
    // Вычисляем CRC32
    uint crc = 0xFFFFFFFF;
    foreach (byte b in dataBlock)
    {{
        crc ^= b;
        for (int i = 0; i < 8; i++)
        {{
            if ((crc & 1) != 0)
                crc = (crc >> 1) ^ 0xEDB88320;
            else
                crc >>= 1;
        }}
    }}
    crc = ~crc;
    
    // Применяем XOR ключ
    uint secureId = crc ^ 0x{0:X8};
    
    return secureId;
}}
", key);
        
        File.WriteAllText("HPE_ALGORITHM_FOUND.txt", code);
        Console.WriteLine("\n  💾 Алгоритм сохранен в HPE_ALGORITHM_FOUND.txt");
    }
    
    static uint CalculateCrc32(byte[] data, uint init, uint poly, bool finalXor)
    {
        uint crc = init;
        
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 1) != 0)
                    crc = (crc >> 1) ^ poly;
                else
                    crc >>= 1;
            }
        }
        
        return finalXor ? ~crc : crc;
    }
    
    static string ExtractPartNumber(byte[] data)
    {
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

