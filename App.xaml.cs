using System.Configuration;
using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HexEditor.Arduino.Services;
using HexEditor.Arduino.ViewModels;
using HexEditor.ViewModels;

namespace HexEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        public IServiceProvider ServiceProvider => _serviceProvider 
            ?? throw new InvalidOperationException("ServiceProvider not initialized");

        protected override void OnStartup(StartupEventArgs e)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            
            // Загружаем шрифт JetBrainsMonoNL-Regular.ttf для всего приложения
            LoadApplicationFont();
            
            // Настройка Dependency Injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Создаем главное окно через DI
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        /// <summary>
        /// Загружает шрифт для всего приложения.
        /// Использует системные шрифты, оптимизированные для WPF с поддержкой кириллицы.
        /// </summary>
        private void LoadApplicationFont()
        {
            FontFamily? appFontFamily = null;
            
            // Список шрифтов в порядке приоритета (моноширинные с поддержкой кириллицы):
            // 1. Segoe UI Mono - современный моноширинный от Microsoft (Windows 10+), отличная кириллица
            // 2. Consolas - классический моноширинный от Microsoft, отлично работает в WPF (кириллица ограничена)
            // 3. Lucida Console - хорошая поддержка кириллицы, моноширинный
            // 4. Courier New - универсальный моноширинный, базовая поддержка кириллицы
            // 5. Arial - пропорциональный, отличная кириллица (fallback)
            
            string[] fontCandidates = new[]
            {
                "Segoe UI Mono",           // Windows 10+ - лучший выбор для моноширинного с кириллицей
                "Consolas",                // Отлично работает в WPF, но кириллица может быть ограничена
                "Lucida Console",          // Хорошая поддержка кириллицы
                "Courier New",             // Универсальный fallback
                "Arial"                    // Пропорциональный fallback с отличной кириллицей
            };
            
            foreach (string fontName in fontCandidates)
            {
                try
                {
                    var testFont = new FontFamily(fontName);
                    appFontFamily = testFont;
                    System.Diagnostics.Debug.WriteLine($"Using {fontName} (system font, optimized for WPF with Cyrillic support)");
                    break; // Успешно загружен, выходим из цикла
                }
                catch
                {
                    // Шрифт не доступен, пробуем следующий
                    continue;
                }
            }
            
            // Если ни один шрифт не загрузился, используем безопасный fallback
            if (appFontFamily == null)
            {
                try
                {
                    appFontFamily = new FontFamily("Arial, 'Courier New', monospace");
                    System.Diagnostics.Debug.WriteLine("Using Arial fallback (guaranteed to be available)");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load any font: {ex.Message}");
                    // Последний резерв - системный шрифт по умолчанию
                    appFontFamily = SystemFonts.MessageFontFamily;
                }
            }

            // Добавляем FontFamily в ресурсы приложения
            if (appFontFamily != null)
            {
                Resources["ApplicationFontFamily"] = appFontFamily;
                
                // Выводим информацию о загруженном шрифте для отладки
                System.Diagnostics.Debug.WriteLine($"ApplicationFontFamily set to: {appFontFamily.Source}");
                System.Diagnostics.Debug.WriteLine($"FontFamily.FamilyNames: {string.Join(", ", appFontFamily.FamilyNames.Values)}");
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Сервисы (синглтоны - один экземпляр на всё приложение)
            services.AddSingleton<ArduinoService>();

            // ViewModels (transient - новый экземпляр при каждом запросе)
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<ArduinoConnectionViewModel>();
            services.AddTransient<RswpViewModel>();
            services.AddTransient<LogViewModel>();

            // Views (главное окно)
            services.AddTransient<MainWindow>();

            // Логирование (опционально, для будущего использования)
            services.AddLogging(builder =>
            {
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }

}
