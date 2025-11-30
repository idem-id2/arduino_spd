using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;

class HpeSecureCodeGenerator
{
    // Структура HPE Secure Code
    const int SECURE_CODE_OFFSET = 384;
    const int SECURE_CODE_SIZE = 32;
    
    // Магические константы HPE
    static readonly byte[] HPE_HEADER = { 0x48, 0x50, 0x54, 0x00 }; // "HPT\0"
    static readonly string HPE_PRODUCT_CODE = "P030530A1";
    
    class ModuleInfo
    {
        public string FileName;
        public uint SerialNumber;
        public byte[] SecureCode;
        public byte[] Hash;
    }

    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("🔐 HPE Secure Code Generator & Analyzer");
        Console.WriteLine("========================================\n");

        var files = Directory.GetFiles(".", "*.bin");
        if (files.Length == 0)
        {
            Console.WriteLine("❌ Не найдено .bin файлов");
            return;
        }

        var modules = new List<ModuleInfo>();

        // Анализируем существующие модули
        Console.WriteLine(string.Format("📊 Анализ {0} модулей...\n", files.Length));
        
        foreach (var file in files)
        {
            var data = File.ReadAllBytes(file);
            if (data.Length < 512) continue;

            var module = new ModuleInfo
            {
                FileName = Path.GetFileName(file),
                SerialNumber = ReadSerialNumber(data),
                SecureCode = new byte[SECURE_CODE_SIZE]
            };
            
            Array.Copy(data, SECURE_CODE_OFFSET, module.SecureCode, 0, SECURE_CODE_SIZE);
            
            // Извлекаем 4-байтовый hash
            module.Hash = new byte[4];
            Array.Copy(module.SecureCode, 4, module.Hash, 0, 4);
            
            modules.Add(module);
        }

        // Выводим таблицу
        Console.WriteLine("┌────────────┬──────────────┬─────────────────────────┐");
        Console.WriteLine("│ Serial     │ Hash (4 byte)│ Validation              │");
        Console.WriteLine("├────────────┼──────────────┼─────────────────────────┤");

        foreach (var mod in modules)
        {
            bool valid = ValidateSecureCode(mod.SecureCode);
            string status = valid ? "✅ Valid" : "❌ Invalid";
            string hashStr = BitConverter.ToString(mod.Hash).Replace("-", " ");
            
            Console.WriteLine(string.Format("│ {0:X8}   │ {1} │ {2,-23} │", 
                mod.SerialNumber, hashStr, status));
        }
        Console.WriteLine("└────────────┴──────────────┴─────────────────────────┘\n");

        // Пытаемся вычислить алгоритм
        Console.WriteLine("🔍 Reverse Engineering алгоритма...\n");
        
        var algorithms = new Dictionary<string, Func<uint, byte[]>>
        {
            { "CRC32", sn => BitConverter.GetBytes(CalculateCrc32(sn)) },
            { "CRC32 Reversed", sn => BitConverter.GetBytes(CalculateCrc32Reversed(sn)) },
            { "XOR Hash", sn => CalculateXorHash(sn) },
            { "Custom Hash v1", sn => CalculateCustomHash1(sn) },
            { "Custom Hash v2", sn => CalculateCustomHash2(sn) },
            { "Polynomial Hash", sn => CalculatePolynomialHash(sn) },
        };

        var bestMatch = new { Algorithm = "", Matches = 0 };

        foreach (var algo in algorithms)
        {
            int matches = 0;
            foreach (var mod in modules)
            {
                var calculated = algo.Value(mod.SerialNumber);
                if (calculated.SequenceEqual(mod.Hash))
                {
                    matches++;
                }
            }

            string result = matches > 0 
                ? string.Format("✅ {0}/{1} совпадений!", matches, modules.Count)
                : "❌ 0 совпадений";
            Console.WriteLine(string.Format("  {0,-20}: {1}", algo.Key, result));

            if (matches > bestMatch.Matches)
            {
                bestMatch = new { Algorithm = algo.Key, Matches = matches };
            }
        }

        if (bestMatch.Matches > 0)
        {
            Console.WriteLine(string.Format("\n🎯 Найден рабочий алгоритм: {0}", bestMatch.Algorithm));
            double accuracy = 100.0 * bestMatch.Matches / modules.Count;
            Console.WriteLine(string.Format("   Точность: {0}/{1} ({2:F1}%)\n", 
                bestMatch.Matches, modules.Count, accuracy));
        }
        else
        {
            Console.WriteLine("\n⚠️  Не удалось определить алгоритм автоматически");
            Console.WriteLine("   Возможно требуется дополнительный ключ или более сложный алгоритм\n");
        }

        // Интерактивный режим
        Console.WriteLine("========================================");
        Console.WriteLine("🛠️  Режимы работы:");
        Console.WriteLine("  1) Сгенерировать код для серийного номера");
        Console.WriteLine("  2) Патчить существующий дамп");
        Console.WriteLine("  3) Анализ паттернов в hash");
        Console.WriteLine("  4) Выход");
        Console.Write("\nВыбор: ");
        
        var choice = Console.ReadLine();
        
        switch (choice)
        {
            case "1":
                GenerateCodeInteractive(modules, algorithms);
                break;
            case "2":
                PatchDumpInteractive(modules, algorithms);
                break;
            case "3":
                AnalyzePatterns(modules);
                break;
        }

        Console.WriteLine("\n✅ Завершено.");
    }

    static void GenerateCodeInteractive(List<ModuleInfo> modules, Dictionary<string, Func<uint, byte[]>> algorithms)
    {
        Console.Write("\nВведите серийный номер (hex, например 4448ECFB): ");
        var input = Console.ReadLine();
        
        uint serial;
        if (!uint.TryParse(input, System.Globalization.NumberStyles.HexNumber, null, out serial))
        {
            Console.WriteLine("❌ Неверный формат");
            return;
        }

        Console.WriteLine(string.Format("\n📝 Генерация Secure Code для S/N: 0x{0:X8}", serial));
        Console.WriteLine("─────────────────────────────────────────");

        foreach (var algo in algorithms)
        {
            var hash = algo.Value(serial);
            var secureCode = BuildSecureCode(hash);
            
            Console.WriteLine(string.Format("\n{0}:", algo.Key));
            Console.WriteLine(string.Format("  Hash: {0}", 
                BitConverter.ToString(hash).Replace("-", " ")));
            Console.WriteLine("  Full code (32 bytes):");
            Console.WriteLine(string.Format("  {0}", 
                BitConverter.ToString(secureCode).Replace("-", " ")));
        }
    }

    static void PatchDumpInteractive(List<ModuleInfo> modules, Dictionary<string, Func<uint, byte[]>> algorithms)
    {
        Console.Write("\nВведите имя файла для патча: ");
        var filename = Console.ReadLine();
        
        if (!File.Exists(filename))
        {
            Console.WriteLine("❌ Файл не найден");
            return;
        }

        var data = File.ReadAllBytes(filename);
        var serial = ReadSerialNumber(data);
        
        Console.WriteLine(string.Format("\nТекущий S/N: 0x{0:X8}", serial));
        Console.Write("Новый S/N (Enter - оставить): ");
        var newSerial = Console.ReadLine();
        
        if (!string.IsNullOrWhiteSpace(newSerial))
        {
            uint ns;
            if (uint.TryParse(newSerial, System.Globalization.NumberStyles.HexNumber, null, out ns))
            {
                serial = ns;
                WriteSerialNumber(data, serial);
                Console.WriteLine(string.Format("✅ S/N изменен на 0x{0:X8}", serial));
            }
        }

        Console.WriteLine("\nВыберите алгоритм:");
        int idx = 1;
        foreach (var algo in algorithms)
        {
            Console.WriteLine(string.Format("  {0}) {1}", idx, algo.Key));
            idx++;
        }
        Console.Write("Выбор: ");
        
        int choice;
        if (int.TryParse(Console.ReadLine(), out choice) && choice >= 1 && choice <= algorithms.Count)
        {
            var selectedAlgo = algorithms.ElementAt(choice - 1);
            var hash = selectedAlgo.Value(serial);
            var secureCode = BuildSecureCode(hash);
            
            Array.Copy(secureCode, 0, data, SECURE_CODE_OFFSET, SECURE_CODE_SIZE);
            
            var outputFile = Path.GetFileNameWithoutExtension(filename) + "_patched.bin";
            File.WriteAllBytes(outputFile, data);
            
            Console.WriteLine(string.Format("\n✅ Файл сохранен: {0}", outputFile));
            Console.WriteLine(string.Format("   Алгоритм: {0}", selectedAlgo.Key));
            Console.WriteLine(string.Format("   Hash: {0}", 
                BitConverter.ToString(hash).Replace("-", " ")));
        }
    }

    static void AnalyzePatterns(List<ModuleInfo> modules)
    {
        Console.WriteLine("\n🔍 Анализ паттернов в hash значениях...\n");

        // Корреляция между S/N и Hash
        Console.WriteLine("📊 Корреляция S/N → Hash:");
        foreach (var mod in modules.Take(5))
        {
            uint sn = mod.SerialNumber;
            uint hash = BitConverter.ToUInt32(mod.Hash, 0);
            
            Console.WriteLine(string.Format("  S/N: 0x{0:X8} → Hash: 0x{1:X8}", sn, hash));
            Console.WriteLine(string.Format("    XOR: 0x{0:X8}", (sn ^ hash)));
            Console.WriteLine(string.Format("    Diff: {0}", (long)hash - (long)sn));
        }

        // Статистика битов
        Console.WriteLine("\n📈 Статистика распределения битов:");
        var allHashes = modules.Select(m => BitConverter.ToUInt32(m.Hash, 0)).ToArray();
        
        uint xorAll = 0;
        foreach (var h in allHashes) xorAll ^= h;
        
        Console.WriteLine(string.Format("  XOR всех hash: 0x{0:X8}", xorAll));
        Console.WriteLine(string.Format("  Min hash: 0x{0:X8}", allHashes.Min()));
        Console.WriteLine(string.Format("  Max hash: 0x{0:X8}", allHashes.Max()));
    }

    // ===== АЛГОРИТМЫ ВЫЧИСЛЕНИЯ HASH =====

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

    static uint CalculateCrc32Reversed(uint serial)
    {
        byte[] data = BitConverter.GetBytes(serial);
        Array.Reverse(data);
        return CalculateCrc32(BitConverter.ToUInt32(data, 0));
    }

    static byte[] CalculateXorHash(uint serial)
    {
        uint hash = serial ^ 0x48505400; // XOR with "HPT\0"
        hash = ((hash << 16) | (hash >> 16)); // Rotate
        hash ^= 0x50303030; // XOR with product code prefix
        return BitConverter.GetBytes(hash);
    }

    static byte[] CalculateCustomHash1(uint serial)
    {
        // Попытка: (serial * magic) ^ (serial >> 16)
        uint magic = 0x01000193; // FNV prime
        uint hash = (serial * magic) ^ (serial >> 16);
        return BitConverter.GetBytes(hash);
    }

    static byte[] CalculateCustomHash2(uint serial)
    {
        // Попытка: комбинация сдвигов и XOR
        uint hash = serial;
        hash ^= (hash << 13);
        hash ^= (hash >> 17);
        hash ^= (hash << 5);
        return BitConverter.GetBytes(hash);
    }

    static byte[] CalculatePolynomialHash(uint serial)
    {
        // Polynomial rolling hash
        uint hash = 0;
        byte[] data = BitConverter.GetBytes(serial);
        uint prime = 31;
        
        foreach (byte b in data)
        {
            hash = hash * prime + b;
        }
        
        return BitConverter.GetBytes(hash);
    }

    // ===== ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ =====

    static byte[] BuildSecureCode(byte[] hash)
    {
        var code = new byte[SECURE_CODE_SIZE];
        
        // Заголовок "HPT\0"
        Array.Copy(HPE_HEADER, 0, code, 0, 4);
        
        // 4-байтовый hash
        Array.Copy(hash, 0, code, 4, 4);
        
        // 8 байт нулей (резерв)
        // уже заполнено нулями
        
        // Код продукта "P030530A1"
        var productBytes = Encoding.ASCII.GetBytes(HPE_PRODUCT_CODE);
        Array.Copy(productBytes, 0, code, 16, productBytes.Length);
        code[16 + productBytes.Length] = 0x09; // Разделитель
        
        return code;
    }

    static bool ValidateSecureCode(byte[] code)
    {
        if (code.Length != SECURE_CODE_SIZE) return false;
        
        // Проверка заголовка
        for (int i = 0; i < HPE_HEADER.Length; i++)
        {
            if (code[i] != HPE_HEADER[i]) return false;
        }
        
        // Проверка кода продукта
        var productBytes = Encoding.ASCII.GetBytes(HPE_PRODUCT_CODE);
        for (int i = 0; i < productBytes.Length; i++)
        {
            if (code[16 + i] != productBytes[i]) return false;
        }
        
        return true;
    }

    static uint ReadSerialNumber(byte[] data)
    {
        // DDR4 SPD: Serial Number at bytes 325-328
        if (data.Length < 329) return 0;
        return BitConverter.ToUInt32(data, 325);
    }

    static void WriteSerialNumber(byte[] data, uint serial)
    {
        var bytes = BitConverter.GetBytes(serial);
        Array.Copy(bytes, 0, data, 325, 4);
    }
}
