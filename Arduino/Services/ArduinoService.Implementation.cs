using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace HexEditor.Arduino.Services
{
    /// <summary>
    /// Partial class для явной реализации интерфейса IArduinoService.
    /// Этот файл связывает существующий ArduinoService с новым интерфейсом.
    /// 
    /// ИСПОЛЬЗОВАНИЕ:
    /// 1. В App.xaml.cs зарегистрируйте:
    ///    services.AddSingleton<IArduinoService, ArduinoService>();
    /// 
    /// 2. В ViewModels используйте:
    ///    public ArduinoConnectionViewModel(IArduinoService arduinoService) { ... }
    /// 
    /// ПРЕИМУЩЕСТВА:
    /// - Можно создавать mock'и для тестов
    /// - Легко заменить реализацию
    /// - Следование Dependency Inversion Principle
    /// </summary>
    internal sealed partial class ArduinoService : IArduinoService
    {
        // Все члены интерфейса уже реализованы в ArduinoService.cs
        // Этот partial class просто явно объявляет реализацию интерфейса
        
        // Если нужны дополнительные члены интерфейса, добавьте их здесь
    }
}

