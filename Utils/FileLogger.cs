using System;
using System.IO;
using System.Threading;

namespace HexEditor.Utils;

/// <summary>
/// Статический класс для записи всех логов в файл log.txt
/// </summary>
internal static class FileLogger
{
    private static readonly object _lock = new();
    private static readonly string _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");
    private static bool _initialized = false;

    /// <summary>
    /// Инициализация логгера (создание файла с заголовком)
    /// </summary>
    private static void Initialize()
    {
        if (_initialized)
            return;

        lock (_lock)
        {
            if (_initialized)
                return;

            try
            {
                // Создаем файл с заголовком сессии
                string header = $"{new string('=', 80)}\r\n" +
                               $"Лог сессии: {DateTime.Now:dd.MM.yyyy HH:mm:ss}\r\n" +
                               $"{new string('=', 80)}\r\n\r\n";
                
                File.WriteAllText(_logFilePath, header);
                _initialized = true;
            }
            catch
            {
                // Игнорируем ошибки инициализации
            }
        }
    }

    /// <summary>
    /// Записать лог в файл
    /// </summary>
    /// <param name="level">Уровень лога (Info, Debug, Error, Warn)</param>
    /// <param name="message">Сообщение</param>
    public static void WriteLog(string level, string message)
    {
        Initialize();

        try
        {
            string entry = $"[{level}] {DateTime.Now:dd.MM.yyyy HH:mm:ss}: {message}";
            
            lock (_lock)
            {
                File.AppendAllText(_logFilePath, entry + Environment.NewLine);
            }
        }
        catch
        {
            // Игнорируем ошибки записи, чтобы не нарушать работу приложения
        }
    }

    /// <summary>
    /// Записать произвольное сообщение в файл (для Console.WriteLine и Debug.WriteLine)
    /// </summary>
    /// <param name="message">Сообщение</param>
    public static void WriteLine(string message)
    {
        Initialize();

        try
            {
            lock (_lock)
            {
                File.AppendAllText(_logFilePath, message + Environment.NewLine);
            }
        }
        catch
        {
            // Игнорируем ошибки записи
        }
    }

    /// <summary>
    /// Очистить лог-файл
    /// </summary>
    public static void Clear()
    {
        lock (_lock)
        {
            try
            {
                _initialized = false;
                if (File.Exists(_logFilePath))
                {
                    File.Delete(_logFilePath);
                }
                Initialize();
            }
            catch
            {
                // Игнорируем ошибки
            }
        }
    }
}

