using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;

class HpeFullComparator
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("🔍 HPE SPD Full Comparator - Полное побайтовое сравнение");
        Console.WriteLine("=========================================================\n");
        
        Console.Write("Оригинальный HPE дамп: ");
        string hpeFile = Console.ReadLine();
        
        if (!File.Exists(hpeFile))
        {
            Console.WriteLine("❌ Файл не найден!");
            return;
        }
        
        Console.Write("Не-HPE дамп (целевой): ");
        string targetFile = Console.ReadLine();
        
        if (!File.Exists(targetFile))
        {
            Console.WriteLine("❌ Файл не найден!");
            return;
        }
        
        var hpeData = File.ReadAllBytes(hpeFile);
        var targetData = File.ReadAllBytes(targetFile);
        
        int minSize = Math.Min(hpeData.Length, targetData.Length);
        
        Console.WriteLine("\n════════════════════════════════════════════════════════");
        Console.WriteLine("📊 БАЗОВАЯ ИНФОРМАЦИЯ");
        Console.WriteLine("════════════════════════════════════════════════════════\n");
        
        Console.WriteLine(string.Format("HPE размер:    {0} bytes", hpeData.Length));
        Console.WriteLine(string.Format("Target размер: {0} bytes", targetData.Length));
        Console.WriteLine(string.Format("Анализ:        {0} bytes\n", minSize));
        
        // Быстрая статистика различий
        int totalDiff = 0;
        for (int i = 0; i < minSize; i++)
        {
            if (hpeData[i] != targetData[i]) totalDiff++;
        }
        
        Console.WriteLine(string.Format("Различий найдено: {0} из {1} ({2:F1}%)\n", 
            totalDiff, minSize, 100.0 * totalDiff / minSize));
        
        // Детальный анализ по блокам
        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.WriteLine("📋 АНАЛИЗ ПО БЛОКАМ SPD");
        Console.WriteLine("════════════════════════════════════════════════════════\n");
        
        AnalyzeBlock("Base Configuration (0-127)", hpeData, targetData, 0, 128);
        AnalyzeBlock("Module Parameters (128-255)", hpeData, targetData, 128, 128);
        AnalyzeBlock("Reserved (256-319)", hpeData, targetData, 256, 64);
        AnalyzeBlock("Manufacturing Info (320-383)", hpeData, targetData, 320, 64);
        AnalyzeBlock("HPE Secure Code (384-415)", hpeData, targetData, 384, 32);
        AnalyzeBlock("Extended Area (416-511)", hpeData, targetData, 416, 96);
        
        // Детальное сравнение Manufacturing + Secure
        Console.WriteLine("\n════════════════════════════════════════════════════════");
        Console.WriteLine("🔬 ДЕТАЛЬНОЕ СРАВНЕНИЕ: MANUFACTURING + SECURE (320-415)");
        Console.WriteLine("════════════════════════════════════════════════════════\n");
        
        DetailedCompare(hpeData, targetData, 320, 96);
        
        // Поиск критических полей
        Console.WriteLine("\n════════════════════════════════════════════════════════");
        Console.WriteLine("⚠️  КРИТИЧЕСКИЕ ПОЛЯ ДЛЯ HPE");
        Console.WriteLine("════════════════════════════════════════════════════════\n");
        
        CheckCriticalFields(hpeData, targetData);
        
        // Генерация патча
        Console.WriteLine("\n════════════════════════════════════════════════════════");
        Console.WriteLine("🔧 ГЕНЕРАЦИЯ ПОЛНОГО ПАТЧА");
        Console.WriteLine("════════════════════════════════════════════════════════\n");
        
        Console.Write("Создать полностью идентичный дамп? (y/n): ");
        if (Console.ReadLine()?.ToLower() == "y")
        {
            GenerateFullPatch(hpeData, targetData, targetFile);
        }
        
        Console.WriteLine("\n✅ Анализ завершён.");
    }
    
    static void AnalyzeBlock(string name, byte[] hpe, byte[] target, int offset, int size)
    {
        int diff = 0;
        for (int i = offset; i < offset + size && i < Math.Min(hpe.Length, target.Length); i++)
        {
            if (hpe[i] != target[i]) diff++;
        }
        
        double percent = 100.0 * diff / size;
        string status = diff == 0 ? "✅ Идентичны" : 
                       percent < 10 ? "⚠️  Мало различий" : "❌ Много различий";
        
        Console.WriteLine(string.Format("{0,-35} {1,3}/{2,3} различий ({3,5:F1}%) {4}", 
            name, diff, size, percent, status));
    }
    
    static void DetailedCompare(byte[] hpe, byte[] target, int offset, int size)
    {
        var ranges = new List<Tuple<string, int, int>>
        {
            Tuple.Create("Manufacturer ID", 320, 4),
            Tuple.Create("Location", 324, 1),
            Tuple.Create("Serial Number", 325, 4),
            Tuple.Create("Part Number", 329, 20),
            Tuple.Create("Revision Code", 349, 2),
            Tuple.Create("Manufacturing Date", 323, 2),
            Tuple.Create("Manuf Specific", 351, 32),
            Tuple.Create("CRC/Checksum", 382, 2),
            Tuple.Create("HPE Header", 384, 4),
            Tuple.Create("HPE Secure ID", 388, 4),
            Tuple.Create("HPE Reserved", 392, 8),
            Tuple.Create("HPE Product Code", 400, 16),
        };
        
        foreach (var range in ranges)
        {
            Console.WriteLine(string.Format("\n  📌 {0} (offset {1}, size {2}):", 
                range.Item1, range.Item2, range.Item3));
            
            bool identical = true;
            for (int i = 0; i < range.Item3; i++)
            {
                int pos = range.Item2 + i;
                if (pos >= Math.Min(hpe.Length, target.Length)) break;
                
                if (hpe[pos] != target[pos])
                {
                    identical = false;
                    break;
                }
            }
            
            if (identical)
            {
                Console.WriteLine("     ✅ Идентичны");
                
                // Показываем значение
                var sb = new StringBuilder();
                for (int i = 0; i < range.Item3; i++)
                {
                    int pos = range.Item2 + i;
                    if (pos >= hpe.Length) break;
                    sb.AppendFormat("{0:X2} ", hpe[pos]);
                }
                Console.WriteLine(string.Format("     Значение: {0}", sb.ToString().Trim()));
            }
            else
            {
                Console.WriteLine("     ❌ РАЗЛИЧАЮТСЯ!");
                
                // HPE
                var sbHpe = new StringBuilder();
                for (int i = 0; i < range.Item3; i++)
                {
                    int pos = range.Item2 + i;
                    if (pos >= hpe.Length) break;
                    sbHpe.AppendFormat("{0:X2} ", hpe[pos]);
                }
                Console.WriteLine(string.Format("     HPE:    {0}", sbHpe.ToString().Trim()));
                
                // Target
                var sbTarget = new StringBuilder();
                for (int i = 0; i < range.Item3; i++)
                {
                    int pos = range.Item2 + i;
                    if (pos >= target.Length) break;
                    sbTarget.AppendFormat("{0:X2} ", target[pos]);
                }
                Console.WriteLine(string.Format("     Target: {0}", sbTarget.ToString().Trim()));
                
                // ASCII если применимо
                if (range.Item1.Contains("Part Number") || range.Item1.Contains("Product Code"))
                {
                    string hpeStr = ExtractString(hpe, range.Item2, range.Item3);
                    string targetStr = ExtractString(target, range.Item2, range.Item3);
                    
                    if (!string.IsNullOrWhiteSpace(hpeStr) || !string.IsNullOrWhiteSpace(targetStr))
                    {
                        Console.WriteLine(string.Format("     HPE ASCII:    \"{0}\"", hpeStr));
                        Console.WriteLine(string.Format("     Target ASCII: \"{0}\"", targetStr));
                    }
                }
            }
        }
    }
    
    static void CheckCriticalFields(byte[] hpe, byte[] target)
    {
        var checks = new List<Tuple<string, Func<byte[], byte[], bool>>>
        {
            Tuple.Create<string, Func<byte[], byte[], bool>>(
                "HPE Header 'HPT\\0' присутствует", 
                (h, t) => t.Length > 387 && t[384] == 0x48 && t[385] == 0x50 && t[386] == 0x54 && t[387] == 0x00
            ),
            
            Tuple.Create<string, Func<byte[], byte[], bool>>(
                "HPE Product Code совпадает", 
                (h, t) => CompareRange(h, t, 400, 11)
            ),
            
            Tuple.Create<string, Func<byte[], byte[], bool>>(
                "Manufacturer ID совпадает", 
                (h, t) => CompareRange(h, t, 320, 4)
            ),
            
            Tuple.Create<string, Func<byte[], byte[], bool>>(
                "Part Number совпадает", 
                (h, t) => CompareRange(h, t, 329, 20)
            ),
            
            Tuple.Create<string, Func<byte[], byte[], bool>>(
                "Все Manufacturing Data совпадают", 
                (h, t) => CompareRange(h, t, 320, 64)
            ),
            
            Tuple.Create<string, Func<byte[], byte[], bool>>(
                "Весь блок 320-415 идентичен", 
                (h, t) => CompareRange(h, t, 320, 96)
            ),
            
            Tuple.Create<string, Func<byte[], byte[], bool>>(
                "Байты 0-127 (Base Config) совпадают", 
                (h, t) => CompareRange(h, t, 0, 128)
            ),
            
            Tuple.Create<string, Func<byte[], byte[], bool>>(
                "Байты 128-255 (Module Params) совпадают", 
                (h, t) => CompareRange(h, t, 128, 128)
            ),
        };
        
        foreach (var check in checks)
        {
            bool result = check.Item2(hpe, target);
            string status = result ? "✅" : "❌";
            Console.WriteLine(string.Format("  {0} {1}", status, check.Item1));
        }
        
        // Дополнительные проверки
        Console.WriteLine("\n  🔍 Дополнительные проверки:");
        
        // SPD Revision
        if (hpe.Length > 1 && target.Length > 1)
        {
            Console.WriteLine(string.Format("     SPD Revision: HPE=0x{0:X2}, Target=0x{1:X2} {2}", 
                hpe[1], target[1], hpe[1] == target[1] ? "✅" : "❌"));
        }
        
        // Memory Type
        if (hpe.Length > 2 && target.Length > 2)
        {
            Console.WriteLine(string.Format("     Memory Type:  HPE=0x{0:X2}, Target=0x{1:X2} {2}", 
                hpe[2], target[2], hpe[2] == target[2] ? "✅" : "❌"));
        }
        
        // Module Type
        if (hpe.Length > 3 && target.Length > 3)
        {
            Console.WriteLine(string.Format("     Module Type:  HPE=0x{0:X2}, Target=0x{1:X2} {2}", 
                hpe[3], target[3], hpe[3] == target[3] ? "✅" : "❌"));
        }
    }
    
    static bool CompareRange(byte[] a, byte[] b, int offset, int size)
    {
        for (int i = 0; i < size; i++)
        {
            int pos = offset + i;
            if (pos >= a.Length || pos >= b.Length) return false;
            if (a[pos] != b[pos]) return false;
        }
        return true;
    }
    
    static string ExtractString(byte[] data, int offset, int size)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < size; i++)
        {
            int pos = offset + i;
            if (pos >= data.Length) break;
            
            byte b = data[pos];
            if (b >= 32 && b < 127)
                sb.Append((char)b);
            else if (b == 0)
                break;
        }
        return sb.ToString().Trim();
    }
    
    static void GenerateFullPatch(byte[] hpe, byte[] target, string targetFile)
    {
        Console.WriteLine("  Создание полностью идентичной копии HPE дампа...\n");
        
        // Копируем ВСЁ из HPE в target
        var patched = new byte[Math.Max(hpe.Length, target.Length)];
        
        // Заполняем базой
        Array.Copy(target, patched, target.Length);
        
        // Перезаписываем ВСЁ из HPE
        Array.Copy(hpe, patched, Math.Min(hpe.Length, patched.Length));
        
        string outputFile = Path.GetFileNameWithoutExtension(targetFile) + "_full_hpe_clone.bin";
        File.WriteAllBytes(outputFile, patched);
        
        Console.WriteLine(string.Format("  ✅ Создан полный клон: {0}", outputFile));
        Console.WriteLine("\n  ⚠️  ВНИМАНИЕ:");
        Console.WriteLine("     - Это ПОЛНАЯ копия оригинального HPE дампа");
        Console.WriteLine("     - Serial Number, Part Number - всё идентично");
        Console.WriteLine("     - Если сервер не принимает, значит проверка идёт:");
        Console.WriteLine("       • По реальному чипу памяти (DIMM ID via I2C)");
        Console.WriteLine("       • По данным из DRAM SPD Hub");
        Console.WriteLine("       • По thermal sensor");
        Console.WriteLine("       • Или другим аппаратным способом\n");
    }
}

