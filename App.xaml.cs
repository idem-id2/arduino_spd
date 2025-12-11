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
        /// Загружает шрифты JetBrainsMono-Regular.ttf и JetBrainsMono-Bold.ttf и добавляет их в ресурсы приложения.
        /// Если шрифты не найдены, используется fallback на распространённые моноширинные шрифты.
        /// </summary>
        private void LoadApplicationFont()
        {
            FontFamily? appFontFamily = null;
            
            try
            {
                string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string regularPath = Path.Combine(exeDirectory, "JetBrainsMono-Regular.ttf");
                string boldPath = Path.Combine(exeDirectory, "JetBrainsMono-Bold.ttf");

                if (File.Exists(regularPath))
                {
                    var regularUri = new Uri(regularPath, UriKind.Absolute);
                    
                    // Загружаем Regular как основной шрифт
                    // WPF автоматически будет использовать Bold файл для FontWeight.Bold и FontWeight.SemiBold, если он доступен
                    if (File.Exists(boldPath))
                    {
                        // Создаём FontFamily с Regular файлом
                        // WPF автоматически выберет правильный файл в зависимости от FontWeight
                        appFontFamily = new FontFamily(regularUri, "./#JetBrains Mono, Consolas, 'Courier New', monospace");
                        System.Diagnostics.Debug.WriteLine("Loaded JetBrainsMono-Regular.ttf and JetBrainsMono-Bold.ttf for application-wide use");
                    }
                    else
                    {
                        appFontFamily = new FontFamily(regularUri, "./#JetBrains Mono, Consolas, 'Courier New', monospace");
                        System.Diagnostics.Debug.WriteLine("Loaded JetBrainsMono-Regular.ttf for application-wide use (Bold version not found, will use system fallback)");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("JetBrainsMono-Regular.ttf not found, using Consolas fallback");
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
