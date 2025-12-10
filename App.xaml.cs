using System.Configuration;
using System.Data;
using System.Text;
using System.Windows;
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
        /// Загружает шрифт JetBrainsMonoNL-Regular.ttf и добавляет его в ресурсы приложения.
        /// Если шрифт не найден, используется fallback на распространённые моноширинные шрифты.
        /// </summary>
        private void LoadApplicationFont()
        {
            FontFamily? appFontFamily = null;
            
            try
            {
                string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string ttfPath = Path.Combine(exeDirectory, "JetBrainsMonoNL-Regular.ttf");

                if (File.Exists(ttfPath))
                {
                    var fontUri = new Uri(ttfPath, UriKind.Absolute);
                    // Создаём FontFamily с fallback на распространённые моноширинные шрифты
                    appFontFamily = new FontFamily(fontUri, "./#JetBrains Mono NL, Consolas, 'Courier New', monospace");
                    System.Diagnostics.Debug.WriteLine("Loaded JetBrainsMonoNL-Regular.ttf for application-wide use");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("JetBrainsMonoNL-Regular.ttf not found, using Consolas fallback");
                    // Используем fallback на распространённые моноширинные шрифты
                    appFontFamily = new FontFamily("Consolas, 'Courier New', monospace");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load custom font: {ex.Message}, using Consolas fallback");
                // Используем fallback на распространённые моноширинные шрифты
                appFontFamily = new FontFamily("Consolas, 'Courier New', monospace");
            }

            // Добавляем FontFamily в ресурсы приложения
            if (appFontFamily != null)
            {
                Resources["ApplicationFontFamily"] = appFontFamily;
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
