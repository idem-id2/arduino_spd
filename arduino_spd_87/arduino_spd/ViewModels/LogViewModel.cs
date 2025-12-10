using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using HexEditor.Constants;
using HexEditor.Arduino.Services;

namespace HexEditor.ViewModels
{
    /// <summary>
    /// ViewModel для логов
    /// </summary>
    internal class LogViewModel : ViewModelBase
    {
        private readonly ArduinoService _arduinoService;
        private readonly ObservableCollection<string> _logEntries = new();

        public LogViewModel(ArduinoService arduinoService)
        {
            _arduinoService = arduinoService ?? throw new ArgumentNullException(nameof(arduinoService));

            // Подписка на события логирования
            _arduinoService.LogGenerated += OnArduinoLogGenerated;
        }

        public ObservableCollection<string> LogEntries => _logEntries;

        public void AppendLog(string level, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                string entry = $"[{level}] {DateTime.Now:dd.MM.yyyy HH:mm:ss}: {message}";
                _logEntries.Add(entry);

                // Ограничиваем размер лога
                while (_logEntries.Count > UiConstants.MAX_LOG_ENTRIES)
                {
                    _logEntries.RemoveAt(0);
                }
            });
        }

        public string GetAllLogsAsText()
        {
            var sb = new StringBuilder();
            foreach (var entry in _logEntries)
            {
                sb.AppendLine(entry);
            }
            return sb.ToString();
        }

        public void ClearLogs()
        {
            _logEntries.Clear();
        }

        private void OnArduinoLogGenerated(object? sender, ArduinoLogEventArgs e)
        {
            AppendLog(e.Level, e.Message);
        }

        public void Dispose()
        {
            _arduinoService.LogGenerated -= OnArduinoLogGenerated;
        }
    }
}

