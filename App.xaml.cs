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
        /// Использует системные шрифты, оптимизированные для WPF (Consolas, Segoe UI Mono),
        /// так как JetBrains Mono может плохо рендериться в WPF.
        /// </summary>
        private void LoadApplicationFont()
        {
            FontFamily? appFontFamily = null;
            
            try
            {
                // Пробуем использовать системные шрифты, которые отлично работают в WPF
                // Порядок приоритета:
                // 1. Segoe UI Mono (Windows 10+, современный и красивый)
                // 2. Consolas (классический моноширинный от Microsoft, отлично работает в WPF)
                // 3. Courier New (универсальный fallback)
                
                // Проверяем доступность Segoe UI Mono
                try
                {
                    var testFont = new FontFamily("Segoe UI Mono");
                    // Если шрифт доступен, используем его
                    appFontFamily = testFont;
                    System.Diagnostics.Debug.WriteLine("Using Segoe UI Mono (system font, optimized for WPF)");
                }
                catch
                {
                    // Segoe UI Mono не доступен, используем Consolas
                    appFontFamily = new FontFamily("Consolas, 'Courier New', monospace");
                    System.Diagnostics.Debug.WriteLine("Using Consolas (system font, excellent WPF rendering)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load system fonts: {ex.Message}, using Consolas fallback");
                // Используем fallback на распространённые моноширинные шрифты
                appFontFamily = new FontFamily("Consolas, 'Courier New', monospace");
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
