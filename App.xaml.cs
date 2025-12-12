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
            
            // Загружаем шрифт JetBrainsMono для всего приложения
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
        /// Загружает шрифт JetBrainsMono для всего приложения.
        /// Ищет файлы JetBrainsMono-Regular.ttf и JetBrainsMono-Bold.ttf в директории исполняемого файла.
        /// Если файлы не найдены, использует системные шрифты как fallback.
        /// </summary>
        private void LoadApplicationFont()
        {
            FontFamily? appFontFamily = null;
            
            try
            {
                // Получаем путь к директории исполняемого файла
                string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                
                // Пути к файлам шрифтов
                string regularFontPath = Path.Combine(exeDirectory, "JetBrainsMono-Regular.ttf");
                string boldFontPath = Path.Combine(exeDirectory, "JetBrainsMono-Bold.ttf");
                
                // Проверяем наличие файлов
                bool regularExists = File.Exists(regularFontPath);
                bool boldExists = File.Exists(boldFontPath);
                
                if (regularExists)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Attempting to load font from: {regularFontPath}");
                        
                        // Используем pack:// URI для ресурсов с указанием имени семейства
                        // Формат: pack://application:,,,/FontFileName.ttf#FontFamilyName
                        // Это позволяет WPF автоматически связать Regular и Bold файлы
                        Uri fontUri;
                        try
                        {
                            // Используем pack:// URI с указанием имени семейства в фрагменте
                            fontUri = new Uri("pack://application:,,,/JetBrainsMono-Regular.ttf#JetBrains Mono", UriKind.Absolute);
                            System.Diagnostics.Debug.WriteLine("Using pack:// URI for font resource with family name");
                        }
                        catch
                        {
                            // Fallback на file:// URI, если pack:// не работает
                            fontUri = new Uri(regularFontPath, UriKind.Absolute);
                            System.Diagnostics.Debug.WriteLine("Using file:// URI for font (fallback)");
                        }
                        
                        // Загружаем FontFamily напрямую из URI (имя семейства уже указано в URI фрагменте)
                        bool loaded = false;
                        try
                        {
                            // Если URI содержит имя семейства в фрагменте (#JetBrains Mono), используем его напрямую
                            if (fontUri.Fragment != null && fontUri.Fragment.Length > 1)
                            {
                                // Имя семейства указано в фрагменте URI (после #)
                                // Декодируем URL-encoded символы (например, %20 -> пробел)
                                string familyNameFromUri = Uri.UnescapeDataString(fontUri.Fragment.Substring(1)); // Убираем # и декодируем
                                System.Diagnostics.Debug.WriteLine($"Loading font with family name from URI fragment: '{familyNameFromUri}'");
                                // Используем правильное имя семейства без URL-encoding
                                appFontFamily = new FontFamily(fontUri, "./#JetBrains Mono");
                            }
                            else
                            {
                                // Пробуем разные варианты имени семейства
                                string[] fontFamilyNames = new[]
                                {
                                    "./#JetBrains Mono",      // Стандартный формат
                                    "./#JetBrainsMono",        // Без пробела
                                    "JetBrains Mono",          // Прямое имя
                                    "JetBrainsMono"            // Без пробела
                                };
                                
                                foreach (string familyName in fontFamilyNames)
                                {
                                    try
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Trying family name: '{familyName}'");
                                        appFontFamily = new FontFamily(fontUri, familyName);
                                        break;
                                    }
                                    catch
                                    {
                                        continue;
                                    }
                                }
                            }
                            
                            // Проверяем, что шрифт действительно загрузился
                            if (appFontFamily == null)
                            {
                                System.Diagnostics.Debug.WriteLine("✗ FontFamily is null after loading attempt");
                                loaded = false;
                            }
                            else
                            {
                                var testTypeface = new Typeface(appFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                                if (testTypeface.TryGetGlyphTypeface(out _))
                                {
                                    var actualFamilyName = string.Join(", ", appFontFamily.FamilyNames.Values);
                                    System.Diagnostics.Debug.WriteLine($"✓ Successfully loaded JetBrainsMono-Regular.ttf");
                                    System.Diagnostics.Debug.WriteLine($"  Actual family name: '{actualFamilyName}'");
                                    System.Diagnostics.Debug.WriteLine($"  Font URI: {fontUri}");
                                    loaded = true;
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Failed to get GlyphTypeface");
                                    appFontFamily = null;
                                    loaded = false;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Failed to load font: {ex.Message}");
                            appFontFamily = null;
                        }
                        
                        if (loaded && appFontFamily != null)
                        {
                            if (boldExists)
                            {
                                System.Diagnostics.Debug.WriteLine($"Attempting to ensure Bold font is linked to Regular...");
                                
                                // КРИТИЧНО: WPF не может автоматически связать два файла шрифтов через pack:// URI
                                // Нужно явно создать FontFamily, который указывает на оба файла
                                // Или использовать GlyphTypeface для предзагрузки Bold
                                try
                                {
                                    if (appFontFamily == null)
                                    {
                                        System.Diagnostics.Debug.WriteLine("✗ Cannot link Bold: main FontFamily is null");
                                        return;
                                    }
                                    
                                    // Пробуем создать Typeface с Bold через основной FontFamily
                                    // Если WPF не может найти Bold файл, он создаст синтетический Bold
                                    var mainBoldTypeface = new Typeface(appFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                                    
                                    if (mainBoldTypeface.TryGetGlyphTypeface(out var mainBoldGlyph))
                                    {
                                        // Проверяем, используется ли реальный Bold файл или синтетический
                                        // Для этого проверяем, что GlyphTypeface действительно загружен из Bold.ttf
                                        // К сожалению, нет прямого способа проверить это, но мы можем попробовать
                                        // загрузить Bold файл отдельно и сравнить
                                        
                                        Uri boldFontUri;
                                        try
                                        {
                                            boldFontUri = new Uri("pack://application:,,,/JetBrainsMono-Bold.ttf#JetBrains Mono", UriKind.Absolute);
                                        }
                                        catch
                                        {
                                            boldFontUri = new Uri(boldFontPath, UriKind.Absolute);
                                        }
                                        
                                        var boldFontFamily = new FontFamily(boldFontUri, "./#JetBrains Mono");
                                        var boldTypeface = new Typeface(boldFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                                        
                                        if (boldTypeface.TryGetGlyphTypeface(out var boldGlyph))
                                        {
                                            System.Diagnostics.Debug.WriteLine("✓ JetBrainsMono-Bold.ttf is available");
                                            System.Diagnostics.Debug.WriteLine("⚠ NOTE: WPF may still use synthetic bold if files are not properly linked");
                                            System.Diagnostics.Debug.WriteLine("  To ensure real Bold is used, verify that both files are in the same resource location");
                                            System.Diagnostics.Debug.WriteLine("  and that FontFamily name matches exactly in both TTF files");
                                        }
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine("✗ Failed to create Bold Typeface through main FontFamily");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Error checking Bold font: {ex.Message}");
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("⚠ JetBrainsMono-Bold.ttf not found - Bold text will use regular font (synthetic bold)");
                                
                                // Проверяем, что хотя бы синтетический Bold работает
                                try
                                {
                                    var boldTypeface = new Typeface(appFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                                    if (boldTypeface.TryGetGlyphTypeface(out _))
                                    {
                                        System.Diagnostics.Debug.WriteLine("⚠ Synthetic Bold will be used (regular font with bold weight)");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"✗ Error testing synthetic Bold: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("✗ Failed to load JetBrainsMono-Regular.ttf with all methods");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"✗ Exception loading JetBrainsMono-Regular.ttf: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                        appFontFamily = null;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"✗ JetBrainsMono-Regular.ttf not found in: {exeDirectory}");
                    System.Diagnostics.Debug.WriteLine($"Looking for: {regularFontPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading JetBrainsMono fonts: {ex.Message}");
            }
            
            // Если JetBrainsMono не загружен, используем системные шрифты как fallback
            if (appFontFamily == null)
            {
                // Список системных шрифтов в порядке приоритета (моноширинные с поддержкой кириллицы):
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
                        System.Diagnostics.Debug.WriteLine($"Using {fontName} (system font fallback, optimized for WPF with Cyrillic support)");
                        break;
                    }
                    catch
                    {
                        continue;
                    }
                }
                
                // Последний резерв
                if (appFontFamily == null)
                {
                    try
                    {
                        appFontFamily = new FontFamily("Arial, 'Courier New', monospace");
                        System.Diagnostics.Debug.WriteLine("Using Arial fallback (guaranteed to be available)");
                    }
                    catch
                    {
                        appFontFamily = SystemFonts.MessageFontFamily;
                        System.Diagnostics.Debug.WriteLine("Using system default font");
                    }
                }
            }

            // Добавляем FontFamily в ресурсы приложения
            // Устанавливаем в Application.Current.Resources, чтобы перезаписать любые статические определения
            if (appFontFamily != null)
            {
                // Удаляем старое определение, если оно есть (из AppStyles.xaml)
                if (Current.Resources.Contains("ApplicationFontFamily"))
                {
                    Current.Resources.Remove("ApplicationFontFamily");
                }
                
                // Устанавливаем новый ресурс
                Current.Resources["ApplicationFontFamily"] = appFontFamily;
                
                // КРИТИЧНО: WPF не может автоматически связать два файла шрифтов через pack:// URI
                // Создаем отдельный ресурс для Bold FontFamily, чтобы гарантировать использование реального Bold.ttf
                string boldFontPathCheck = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JetBrainsMono-Bold.ttf");
                if (File.Exists(boldFontPathCheck))
                {
                    try
                    {
                        Uri boldFontUri;
                        try
                        {
                            boldFontUri = new Uri("pack://application:,,,/JetBrainsMono-Bold.ttf#JetBrains Mono", UriKind.Absolute);
                        }
                        catch
                        {
                            boldFontUri = new Uri(boldFontPathCheck, UriKind.Absolute);
                        }
                        
                        var boldFontFamily = new FontFamily(boldFontUri, "./#JetBrains Mono");
                        
                        // Проверяем, что Bold FontFamily работает
                        var boldTypeface = new Typeface(boldFontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                        if (boldTypeface.TryGetGlyphTypeface(out _))
                        {
                            // Устанавливаем отдельный ресурс для Bold FontFamily
                            Current.Resources["ApplicationFontFamilyBold"] = boldFontFamily;
                            System.Diagnostics.Debug.WriteLine("✓ ApplicationFontFamilyBold resource created for explicit Bold font usage");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"✗ Failed to create ApplicationFontFamilyBold resource: {ex.Message}");
                        // Fallback: используем основной FontFamily для Bold
                        Current.Resources["ApplicationFontFamilyBold"] = appFontFamily;
                    }
                }
                else
                {
                    // Fallback: если Bold файл не найден, используем основной FontFamily
                    Current.Resources["ApplicationFontFamilyBold"] = appFontFamily;
                    System.Diagnostics.Debug.WriteLine("⚠ JetBrainsMono-Bold.ttf not found - ApplicationFontFamilyBold will use regular font");
                }
                
                // Выводим подробную информацию о загруженном шрифте для отладки
                System.Diagnostics.Debug.WriteLine("=== Font Loading Summary ===");
                System.Diagnostics.Debug.WriteLine($"ApplicationFontFamily set to: {appFontFamily.Source}");
                System.Diagnostics.Debug.WriteLine($"FontFamily.FamilyNames: {string.Join(", ", appFontFamily.FamilyNames.Values)}");
                
                // Проверяем, что ресурс действительно установлен
                if (Current.Resources.Contains("ApplicationFontFamily"))
                {
                    var resourceFont = Current.Resources["ApplicationFontFamily"] as FontFamily;
                    if (resourceFont != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"✓ Resource 'ApplicationFontFamily' successfully set in Application.Current.Resources");
                        System.Diagnostics.Debug.WriteLine($"  Resource value: {resourceFont.Source}");
                        System.Diagnostics.Debug.WriteLine($"  Resource FamilyNames: {string.Join(", ", resourceFont.FamilyNames.Values)}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"✗ Resource 'ApplicationFontFamily' is null");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Resource 'ApplicationFontFamily' not found in Application.Current.Resources");
                }
                
                // Пробуем создать Typeface для проверки
                try
                {
                    var testTypeface = new Typeface(appFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                    if (testTypeface.TryGetGlyphTypeface(out var glyphTypeface))
                    {
                        System.Diagnostics.Debug.WriteLine($"✓ Typeface created successfully");
                        System.Diagnostics.Debug.WriteLine($"  GlyphTypeface available: True");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"✗ Failed to get GlyphTypeface from Typeface");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Failed to create Typeface: {ex.Message}");
                }
                
                System.Diagnostics.Debug.WriteLine("===========================");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("✗ appFontFamily is null - font not loaded!");
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
