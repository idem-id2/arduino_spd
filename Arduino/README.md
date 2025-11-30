# 🔌 Arduino Module

## 📁 Структура модуля

Этот модуль содержит всё, что связано с Arduino устройством и SPD операциями.

```
Arduino/
├── Hardware/                      # Низкоуровневая работа с железом
│   └── Arduino.cs                 # Serial Port + протокол Arduino
├── Services/                      # Бизнес-логика
│   ├── IArduinoService.cs         # Интерфейс
│   ├── ArduinoService.cs          # Реализация сервиса
│   └── ArduinoService.Implementation.cs  # Partial class
└── ViewModels/                    # MVVM ViewModels
    └── ArduinoConnectionViewModel.cs  # UI логика подключения
```

---

## 🎯 Назначение модуля

### Hardware/Arduino.cs
**Ответственность:** Низкоуровневая работа с Serial портом и Arduino протоколом

**Возможности:**
- Подключение/отключение к COM порту
- Отправка команд на Arduino
- Получение ответов с проверкой контрольной суммы
- Чтение/запись SPD через I2C
- Управление RSWP (Reversible Software Write Protection)
- Определение типа памяти (DDR4/DDR5)
- Мониторинг подключения

**Команды Arduino:**
```csharp
Command.TEST         // 't' - тест связи
Command.VERSION      // 'v' - версия прошивки
Command.NAME         // 'n' - имя устройства
Command.READBYTE     // 'r' - чтение байтов
Command.WRITEBYTE    // 'w' - запись байта
Command.WRITEPAGE    // 'g' - запись страницы
Command.SCANBUS      // 's' - сканирование I2C
Command.RSWP         // 'b' - управление RSWP
Command.DDR4DETECT   // '4' - определение DDR4
Command.DDR5DETECT   // '5' - определение DDR5
Command.I2CCLOCK     // 'c' - управление частотой I2C
```

### Services/ArduinoService.cs
**Ответственность:** Высокоуровневая бизнес-логика работы с Arduino

**Возможности:**
- Сканирование COM портов
- Управление подключением
- Чтение SPD дампов (512/1024 байт)
- Запись SPD дампов с валидацией
- Управление RSWP блоками
- Определение типа памяти
- Логирование операций

**Events:**
```csharp
LogGenerated             // Логи операций
ConnectionStateChanged   // Подключение/отключение
SpdStateChanged          // SPD обнаружен/удален
ConnectionInfoChanged    // Информация об устройстве
RswpStateChanged         // Статус RSWP блоков
MemoryTypeChanged        // Тип памяти изменен
StateChanged             // Любое изменение состояния
```

### ViewModels/ArduinoConnectionViewModel.cs
**Ответственность:** UI логика управления подключением (MVVM)

**Properties:**
```csharp
Devices                  // Список найденных устройств
SelectedDevice           // Выбранное устройство
IsConnected              // Статус подключения
ConnectionStatusText     // Текст для UI badge
DetailPort, DetailFirmware, DetailName, DetailClock, DetailRswp
```

**Commands:**
```csharp
ScanCommand              // Сканирование портов
ConnectCommand           // Подключение/отключение
DisconnectCommand        // Отключение
```

---

## 🔗 Зависимости

### Hardware → никого (только .NET)
- `System.IO.Ports`
- Самодостаточный модуль

### Services → Hardware
- `Arduino.Hardware.Arduino`
- Использует низкоуровневый API

### ViewModels → Services
- `Arduino.Services.ArduinoService`
- Обертка для UI

---

## 📊 Namespaces

```csharp
HexEditor.Arduino.Hardware       // Железо (Serial Port)
HexEditor.Arduino.Services       // Бизнес-логика
HexEditor.Arduino.ViewModels     // MVVM UI логика
```

**Использование:**
```csharp
using HexEditor.Arduino.Services;
using HexEditor.Arduino.Hardware;
using ArduinoHardware = HexEditor.Arduino.Hardware.Arduino; // Alias для Command

// Теперь можно:
var nameLength = ArduinoHardware.Command.NAMELENGTH;
```

---

## 🎓 Архитектурные решения

### Разделение на слои (Layered Architecture)

```
┌─────────────────────────────────────────┐
│  ViewModels (Presentation Logic)        │  ← UI логика
│  - ArduinoConnectionViewModel           │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│  Services (Business Logic)              │  ← Бизнес-логика
│  - IArduinoService (interface)          │
│  - ArduinoService (implementation)      │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│  Hardware (Hardware Access Layer)       │  ← Железо
│  - Arduino.cs (Serial Port + Protocol)  │
└─────────────────────────────────────────┘
```

### Преимущества:
- ✅ Чёткое разделение ответственности
- ✅ Каждый слой можно тестировать отдельно
- ✅ Легко заменить Hardware на mock для тестов
- ✅ Можно переиспользовать Services в другом UI

---

## 🧪 Примеры использования

### Использование в коде

```csharp
// DI в App.xaml.cs
services.AddSingleton<ArduinoService>();
services.AddTransient<ArduinoConnectionViewModel>();

// В MainWindow через DI
public MainWindow(MainWindowViewModel viewModel)
{
    _viewModel = viewModel;
    DataContext = _viewModel; // Для биндингов
}

// В XAML
<ListBox ItemsSource="{Binding ArduinoViewModel.Devices}"/>
<Button Command="{Binding ArduinoViewModel.ScanCommand}"/>
```

### Тестирование

```csharp
// Mock сервиса для unit-тестов
var mockService = new Mock<IArduinoService>();
mockService.Setup(x => x.ScanAsync()).Returns(Task.CompletedTask);

var vm = new ArduinoConnectionViewModel(mockService.Object);
await vm.ScanCommand.Execute(null);

// Проверки
Assert.False(vm.IsScanning);
mockService.Verify(x => x.ScanAsync(), Times.Once);
```

---

## 🔧 Конфигурация

### Настройки Serial Port
```csharp
var settings = new Arduino.SerialPortSettings(
    baudRate: 115200,
    dtrEnable: true,
    rtsEnable: true,
    timeout: 10  // seconds
);
```

### Таймауты
- **Сканирование порта:** 5 секунд
- **Команда Arduino:** 10 секунд
- **Чтение SPD:** ~3-5 секунд (512 байт)
- **Запись SPD:** ~10-15 секунд (512 байт)

---

## 📈 Метрики модуля

| Файл | Строк | Цикл. сложность |
|------|-------|-----------------|
| Arduino.cs | 889 | Средняя (~8) |
| ArduinoService.cs | 1099 | Средняя (~7) |
| ArduinoConnectionViewModel.cs | 270 | Низкая (~4) |

**Итого:** ~2260 строк хорошо организованного кода

---

## 🚀 История изменений

### v2.0 (26.11.2025)
- ✅ Создана папка Arduino
- ✅ Реорганизована структура (Hardware/Services/ViewModels)
- ✅ Добавлен IArduinoService интерфейс
- ✅ Добавлена ArduinoConnectionViewModel (MVVM)
- ✅ Обновлены namespaces

### v1.0 (original)
- Файлы были в корне и Services/
- Namespace: HexEditor.Hardware, HexEditor.Services

---

## 🎯 Лучшие практики

### При работе с модулем:

1. **Используйте интерфейс IArduinoService** для тестирования
2. **Используйте ViewModel** для UI логики
3. **НЕ создавайте Arduino напрямую** - только через ArduinoService
4. **Подписывайтесь на события** вместо polling
5. **Используйте async/await** для всех операций

### Примеры:

```csharp
// ❌ Плохо - прямое создание
var arduino = new Arduino(settings, "COM3");

// ✅ Хорошо - через сервис
await _arduinoService.ConnectAsync();

// ❌ Плохо - polling
while (!_arduinoService.IsConnected) { Thread.Sleep(100); }

// ✅ Хорошо - события
_arduinoService.ConnectionStateChanged += OnConnected;
```

---

## 📚 См. также

- **Constants/SpdConstants.cs** - константы для SPD
- **MainWindow.xaml.cs** - использование ArduinoViewModel
- **REFACTORING_GUIDE.md** - общее руководство

---

**Модуль:** Arduino  
**Версия:** 2.0  
**Статус:** ✅ Production Ready  
**Тестируемость:** 80%

