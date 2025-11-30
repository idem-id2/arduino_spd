using System;
using System.IO;
using System.Text;

class HpeSecureCodeCloner
{
    const int SERIAL_OFFSET = 325;
    const int SECURE_CODE_OFFSET = 384;
    const int SECURE_CODE_SIZE = 32;
    
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("🔧 HPE Secure Code Cloner & Patcher");
        Console.WriteLine("===================================\n");
        
        Console.WriteLine("Этот инструмент позволяет:");
        Console.WriteLine("  1) Клонировать Secure Code с оригинального HPE модуля");
        Console.WriteLine("  2) Патчить SPD дамп совместимого модуля\n");
        
        Console.WriteLine("════════════════════════════════════════════════════════\n");
        
        // Режим 1: Клонирование
        Console.WriteLine("📋 РЕЖИМ 1: Полное клонирование");
        Console.WriteLine("─────────────────────────────────\n");
        Console.Write("Источник (оригинальный HPE дамп): ");
        string sourceFile = Console.ReadLine();
        
        if (!File.Exists(sourceFile))
        {
            Console.WriteLine("❌ Файл не найден!");
            return;
        }
        
        var sourceData = File.ReadAllBytes(sourceFile);
        if (sourceData.Length < 512)
        {
            Console.WriteLine("❌ Неверный размер файла!");
            return;
        }
        
        // Извлекаем данные из источника
        uint sourceSerial = BitConverter.ToUInt32(sourceData, SERIAL_OFFSET);
        byte[] secureCode = new byte[SECURE_CODE_SIZE];
        Array.Copy(sourceData, SECURE_CODE_OFFSET, secureCode, 0, SECURE_CODE_SIZE);
        uint secureId = BitConverter.ToUInt32(sourceData, 388);
        
        Console.WriteLine("\n✅ Данные из источника извлечены:");
        Console.WriteLine(string.Format("   Serial Number: 0x{0:X8}", sourceSerial));
        Console.WriteLine(string.Format("   HPE Secure ID: 0x{0:X8}", secureId));
        Console.WriteLine("   Secure Code (32 bytes):");
        Console.WriteLine("   " + BitConverter.ToString(secureCode).Replace("-", " "));
        
        Console.WriteLine("\n─────────────────────────────────\n");
        Console.Write("Целевой файл (куда записать): ");
        string targetFile = Console.ReadLine();
        
        if (!File.Exists(targetFile))
        {
            Console.WriteLine("❌ Файл не найден!");
            return;
        }
        
        var targetData = File.ReadAllBytes(targetFile);
        if (targetData.Length < 512)
        {
            Console.WriteLine("❌ Неверный размер файла!");
            return;
        }
        
        uint targetSerial = BitConverter.ToUInt32(targetData, SERIAL_OFFSET);
        Console.WriteLine(string.Format("\n📝 Текущий Serial целевого модуля: 0x{0:X8}", targetSerial));
        
        Console.WriteLine("\n════════════════════════════════════════════════════════");
        Console.WriteLine("⚠️  ВЫБЕРИТЕ РЕЖИМ КЛОНИРОВАНИЯ:\n");
        Console.WriteLine("  1) Полное клонирование (S/N + Secure Code)");
        Console.WriteLine("     → Модуль будет идентичен оригиналу");
        Console.WriteLine("     → Может конфликтовать с существующим модулем\n");
        Console.WriteLine("  2) Только Secure Code (сохранить S/N целевого модуля)");
        Console.WriteLine("     → S/N остается уникальным");
        Console.WriteLine("     ⚠️  Secure ID не будет соответствовать S/N!");
        Console.WriteLine("     → HPE может не принять такой модуль\n");
        Console.WriteLine("  3) Попытка адаптации (экспериментально)");
        Console.WriteLine("     → Попытка пересчитать Secure ID для нового S/N");
        Console.WriteLine("     ⚠️  Требует знания алгоритма HPE\n");
        
        Console.Write("Выбор (1/2/3): ");
        string choice = Console.ReadLine();
        
        switch (choice)
        {
            case "1":
                CloneComplete(sourceData, targetData, targetFile);
                break;
            case "2":
                CloneSecureCodeOnly(secureCode, targetData, targetFile);
                break;
            case "3":
                TryAdaptation(sourceSerial, secureId, targetSerial, targetData, targetFile);
                break;
            default:
                Console.WriteLine("❌ Неверный выбор");
                break;
        }
    }
    
    static void CloneComplete(byte[] sourceData, byte[] targetData, string targetFile)
    {
        Console.WriteLine("\n🔄 Выполняется полное клонирование...\n");
        
        // Копируем Serial Number
        Array.Copy(sourceData, SERIAL_OFFSET, targetData, SERIAL_OFFSET, 4);
        
        // Копируем Secure Code
        Array.Copy(sourceData, SECURE_CODE_OFFSET, targetData, SECURE_CODE_OFFSET, SECURE_CODE_SIZE);
        
        // Сохраняем
        string outputFile = Path.GetFileNameWithoutExtension(targetFile) + "_cloned.bin";
        File.WriteAllBytes(outputFile, targetData);
        
        uint newSerial = BitConverter.ToUInt32(targetData, SERIAL_OFFSET);
        uint newSecureId = BitConverter.ToUInt32(targetData, 388);
        
        Console.WriteLine("✅ Клонирование завершено!");
        Console.WriteLine(string.Format("   Новый S/N:        0x{0:X8}", newSerial));
        Console.WriteLine(string.Format("   Новый Secure ID:  0x{0:X8}", newSecureId));
        Console.WriteLine(string.Format("   Файл сохранен:    {0}\n", outputFile));
        
        Console.WriteLine("⚠️  ВАЖНО:");
        Console.WriteLine("   - Этот модуль теперь имеет тот же S/N что и оригинал");
        Console.WriteLine("   - Используйте только ОДИН из них в системе");
        Console.WriteLine("   - Рекомендуется обновить Serial Number вручную");
    }
    
    static void CloneSecureCodeOnly(byte[] secureCode, byte[] targetData, string targetFile)
    {
        Console.WriteLine("\n🔄 Копирование только Secure Code...\n");
        
        uint targetSerial = BitConverter.ToUInt32(targetData, SERIAL_OFFSET);
        
        // Копируем только Secure Code, S/N не трогаем
        Array.Copy(secureCode, 0, targetData, SECURE_CODE_OFFSET, SECURE_CODE_SIZE);
        
        string outputFile = Path.GetFileNameWithoutExtension(targetFile) + "_patched.bin";
        File.WriteAllBytes(outputFile, targetData);
        
        uint secureId = BitConverter.ToUInt32(secureCode, 4); // hash at offset 4 in secure code
        
        Console.WriteLine("✅ Патч применен!");
        Console.WriteLine(string.Format("   S/N (без изменений): 0x{0:X8}", targetSerial));
        Console.WriteLine(string.Format("   Secure ID (скопирован): 0x{0:X8}", secureId));
        Console.WriteLine(string.Format("   Файл сохранен:    {0}\n", outputFile));
        
        Console.WriteLine("⚠️  ВНИМАНИЕ:");
        Console.WriteLine("   - Secure ID НЕ СООТВЕТСТВУЕТ Serial Number!");
        Console.WriteLine("   - HPE BIOS может отклонить такой модуль");
        Console.WriteLine("   - Эксперимент на свой риск");
    }
    
    static void TryAdaptation(uint sourceSerial, uint sourceSecureId, uint targetSerial, byte[] targetData, string targetFile)
    {
        Console.WriteLine("\n🧪 Попытка адаптации (экспериментально)...\n");
        Console.WriteLine("⚠️  Алгоритм HPE неизвестен, используется эвристика\n");
        
        // Пробуем несколько эвристических подходов
        
        // Подход 1: XOR разница
        uint xorDiff = sourceSerial ^ sourceSecureId;
        uint newSecureId1 = targetSerial ^ xorDiff;
        
        // Подход 2: Сохраняем относительную разницу
        long diff = (long)sourceSecureId - (long)sourceSerial;
        uint newSecureId2 = (uint)((long)targetSerial + diff);
        
        // Подход 3: Сохраняем коэффициент
        double ratio = (double)sourceSecureId / sourceSerial;
        uint newSecureId3 = (uint)(targetSerial * ratio);
        
        Console.WriteLine("Рассчитанные варианты Secure ID:");
        Console.WriteLine(string.Format("  1) XOR метод:    0x{0:X8}", newSecureId1));
        Console.WriteLine(string.Format("  2) Diff метод:   0x{0:X8}", newSecureId2));
        Console.WriteLine(string.Format("  3) Ratio метод:  0x{0:X8}\n", newSecureId3));
        
        Console.Write("Выберите вариант (1/2/3) или 0 для отмены: ");
        string variantChoice = Console.ReadLine();
        
        uint selectedSecureId;
        switch (variantChoice)
        {
            case "1": selectedSecureId = newSecureId1; break;
            case "2": selectedSecureId = newSecureId2; break;
            case "3": selectedSecureId = newSecureId3; break;
            default:
                Console.WriteLine("❌ Отменено");
                return;
        }
        
        // Строим новый Secure Code
        byte[] newSecureCode = new byte[SECURE_CODE_SIZE];
        
        // Header "HPT\0"
        newSecureCode[0] = 0x48;
        newSecureCode[1] = 0x50;
        newSecureCode[2] = 0x54;
        newSecureCode[3] = 0x00;
        
        // Secure ID (4 байта)
        var idBytes = BitConverter.GetBytes(selectedSecureId);
        Array.Copy(idBytes, 0, newSecureCode, 4, 4);
        
        // Product code "P030530A1" at offset 16
        var product = Encoding.ASCII.GetBytes("P030530A1");
        Array.Copy(product, 0, newSecureCode, 16, product.Length);
        newSecureCode[16 + product.Length] = 0x09;
        
        // Применяем
        Array.Copy(newSecureCode, 0, targetData, SECURE_CODE_OFFSET, SECURE_CODE_SIZE);
        
        string outputFile = Path.GetFileNameWithoutExtension(targetFile) + "_adapted.bin";
        File.WriteAllBytes(outputFile, targetData);
        
        Console.WriteLine("\n✅ Адаптация выполнена!");
        Console.WriteLine(string.Format("   Новый S/N:         0x{0:X8}", targetSerial));
        Console.WriteLine(string.Format("   Новый Secure ID:   0x{0:X8}", selectedSecureId));
        Console.WriteLine(string.Format("   Файл сохранен:     {0}\n", outputFile));
        
        Console.WriteLine("⚠️  ЭКСПЕРИМЕНТАЛЬНО:");
        Console.WriteLine("   - Secure ID рассчитан эвристически");
        Console.WriteLine("   - Вероятность работы очень низкая");
        Console.WriteLine("   - Требуется тестирование на реальном сервере HPE");
    }
}

