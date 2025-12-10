using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HexEditor.Arduino.Hardware;

namespace HexEditor.Arduino.Services;

public enum SpdMemoryType
{
    Unknown,
    Ddr4,
    Ddr5
}

internal sealed partial class ArduinoService
{
    private readonly Hardware.Arduino.SerialPortSettings _portSettings = new(115200, true, true, 10);
    private readonly object _lock = new();
    private CancellationTokenSource? _scanCancellation;
    private CancellationTokenSource? _alertOperationCancellation; // Для отмены операций в HandleAlert
    private static readonly TimeSpan PortProbeTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(30); // Таймаут для операций с устройством

    private Hardware.Arduino? _activeDevice;
    private byte _activeI2cAddress = 0x50;
    private bool _spdReady;
    private SpdMemoryType _memoryType = SpdMemoryType.Unknown;
    private byte[]? _fullScanAddresses; // Результаты полного сканирования I2C шины
    private volatile bool _isHandlingAlert; // Флаг для предотвращения одновременной обработки алертов

    public ObservableCollection<ArduinoDeviceInfo> Devices { get; } = new();

    public ArduinoDeviceInfo? SelectedDevice { get; private set; }

    public bool IsScanning { get; private set; }
    public bool IsConnecting { get; private set; }
    public bool IsConnected => _activeDevice?.IsConnected ?? false;

    public Hardware.Arduino? GetActiveDevice() => _activeDevice;
    public bool IsReading { get; private set; }
    public bool IsSpdReady => _spdReady;
    public SpdMemoryType ActiveMemoryType => _memoryType;
    public byte[]? GetFullScanAddresses() => _fullScanAddresses;
    public int ActiveRswpBlockCount => _memoryType switch
    {
        SpdMemoryType.Ddr5 => 16,
        SpdMemoryType.Ddr4 => 4,
        _ => 0
    };

    public event EventHandler<ArduinoLogEventArgs>? LogGenerated;
    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<bool>? SpdStateChanged;
    public event EventHandler<ArduinoConnectionInfo>? ConnectionInfoChanged;
    public event EventHandler<bool[]>? RswpStateChanged;
    public event EventHandler<SpdMemoryType>? MemoryTypeChanged;
    public event EventHandler? StateChanged;

    public void SetSelectedDevice(ArduinoDeviceInfo? device)
    {
        SelectedDevice = device;
        OnStateChanged();
    }

    public async Task ScanAsync()
    {
        if (IsScanning)
        {
            return;
        }

        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        _scanCancellation = new CancellationTokenSource();
        var token = _scanCancellation.Token;

        IsScanning = true;
        OnStateChanged();
        LogInfo("Запуск сканирования Arduino...");

        var discovered = new List<ArduinoDeviceInfo>();
        var scanStopwatch = Stopwatch.StartNew();
        string[] ports;
        try
        {
            ports = SerialPort.GetPortNames()
                              .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                              .ToArray();
        }
        catch (Exception ex)
        {
            LogError($"Не удалось перечислить COM-порты: {ex.Message}");
            IsScanning = false;
            OnStateChanged();
            return;
        }

        if (ports.Length == 0)
        {
            LogWarn("COM-порты не обнаружены.");
            IsScanning = false;
            OnStateChanged();
            return;
        }

        try
        {
            foreach (var port in ports)
            {
                if (token.IsCancellationRequested)
                {
                    LogWarn("Сканирование Arduino отменено.");
                    break;
                }

                var info = await ProbePortWithTimeoutAsync(port, token).ConfigureAwait(true);
                if (info != null)
                {
                    discovered.Add(info);
                    LogInfo($"{port}: Arduino обнаружен (проверка завершена за {info.ProbeDurationMs} мс).");
                }
            }
        }
        catch (OperationCanceledException)
        {
            LogWarn("Сканирование Arduino отменено.");
        }
        finally
        {
            _scanCancellation?.Dispose();
            _scanCancellation = null;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            Devices.Clear();
            foreach (var device in discovered.OrderBy(d => d.Port, StringComparer.OrdinalIgnoreCase))
            {
                Devices.Add(device);
            }
            SetSelectedDevice(Devices.FirstOrDefault());
        });

        scanStopwatch.Stop();
        LogInfo($"Сканирование Arduino завершено. Проверено портов: {ports.Length}, найдено устройств: {discovered.Count}, длительность: {scanStopwatch.ElapsedMilliseconds} мс.");
        IsScanning = false;
        OnStateChanged();
    }

    public async Task ConnectAsync()
    {
        if (IsConnected || IsConnecting)
        {
            return;
        }

        if (SelectedDevice == null)
        {
            LogWarn("Выберите устройство перед подключением.");
            return;
        }

        IsConnecting = true;
        OnStateChanged();

        string targetPort = SelectedDevice.Port;
        var device = new Hardware.Arduino(_portSettings, targetPort);
        _activeDevice = device;
        _spdReady = false;
        SpdStateChanged?.Invoke(this, _spdReady);
        ConnectionInfoChanged?.Invoke(this, ArduinoConnectionInfo.Empty);

        device.AlertReceived += HandleAlert;
        device.ConnectionLost += HandleConnectionLost;

        string firmwareText = "—";
        string name = "—";
        string clockText = "—";
        string ddr4Text = "—";
        bool spdDetected = false;
        byte detectedAddress = _activeI2cAddress;

        try
        {
            await Task.Run(() =>
            {
                LogInfo($"{targetPort}: Подключение к устройству");
                device.Connect();

                int firmware = device.FirmwareVersion;
                firmwareText = FormatFirmwareVersion(firmware);

                LogInfo($"{targetPort}: Проверка имени");
                name = device.Name;

                LogInfo($"{targetPort}: Проверка частоты I2C");
                ushort clock = device.I2CClock;
                clockText = clock == Hardware.Arduino.ClockMode.Fast ? "400 kHz" : "100 kHz";
                LogInfo($"{targetPort}: Частота I2C {(clock == Hardware.Arduino.ClockMode.Fast ? "Быстрая (400 кГц)" : "Стандартная (100 кГц)")}");

                LogInfo($"{targetPort}: Проверка RSWP");
                byte rswp = device.RswpTypeSupport;
                bool ddr4Rswp = (rswp & Hardware.Arduino.RswpSupport.DDR4) != 0;
                ddr4Text = ddr4Rswp ? "Да" : "Нет";
                LogRswpState(targetPort, rswp);

                LogInfo($"{targetPort}: Проверка шины I2C");
                // Сначала выполняем полное сканирование всех I2C адресов
                // Это нужно сделать до быстрого скана, но только если не идет чтение SPD
                byte[] fullAddresses = Array.Empty<byte>();
                try
                {
                    fullAddresses = device.ScanFull();
                    if (fullAddresses.Length > 0)
                    {
                        var addressList = string.Join(", ", fullAddresses.Select(a => $"0x{a:X2}"));
                        LogInfo($"{targetPort}: Полное сканирование I2C: найдено {fullAddresses.Length} устройств ({addressList})");
                    }
                    else
                    {
                        LogInfo($"{targetPort}: Полное сканирование I2C: устройства не найдены");
                    }
                }
                catch (Exception ex)
                {
                    LogWarn($"{targetPort}: Не удалось выполнить полное сканирование I2C: {ex.Message}");
                }
                
                // Сохраняем результаты полного сканирования
                _fullScanAddresses = fullAddresses;
                
                // Затем выполняем быстрый скан для определения SPD адресов
                var addresses = device.Scan();
                if (addresses.Length > 0)
                {
                    LogInfo($"{targetPort}: обнаружена новая SPD EEPROM");
                    spdDetected = true;
                    detectedAddress = addresses[0];
                }
                else
                {
                    LogInfo($"{targetPort}: SPD EEPROM извлечена.");
                    spdDetected = false;
                }

                LogInfo($"{targetPort} подключен (Имя: {name}, FW: v.{firmwareText})");
            }).ConfigureAwait(true);

            if (_activeDevice != device)
            {
                // Connection was interrupted/disposed
                return;
            }

            _activeI2cAddress = detectedAddress;
            _spdReady = spdDetected;
            SpdStateChanged?.Invoke(this, _spdReady);
            
            // Очищаем результаты полного сканирования при отключении
            if (!spdDetected)
            {
                _fullScanAddresses = null;
            }

            if (_spdReady)
            {
                await RefreshMemoryTypeAsync().ConfigureAwait(true);
                await CheckRswpAsync().ConfigureAwait(true);
            }
            else
            {
                UpdateMemoryType(SpdMemoryType.Unknown);
                RswpStateChanged?.Invoke(this, Array.Empty<bool>());
            }

            ConnectionInfoChanged?.Invoke(this, new ArduinoConnectionInfo(
                targetPort,
                firmwareText,
                name,
                clockText,
                ddr4Text));

            ConnectionStateChanged?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            LogError($"{targetPort}: {ex.Message}");
            DisconnectInternal(false);
        }
        finally
        {
            IsConnecting = false;
            OnStateChanged();
        }
    }

    public void Disconnect()
    {
        DisconnectInternal(true);
    }

    public async Task<byte[]?> ReadSpdDumpAsync()
    {
        if (!IsConnected || _activeDevice == null)
        {
            LogWarn("Устройство не подключено.");
            return null;
        }

        if (!_spdReady)
        {
            LogWarn("SPD EEPROM не обнаружена.");
            return null;
        }

        if (IsReading)
        {
            return null;
        }

        IsReading = true;
        OnStateChanged();

        // Захватываем ссылку на устройство локально, чтобы избежать race condition
        var currentDevice = _activeDevice;
        if (currentDevice == null)
        {
            IsReading = false;
            OnStateChanged();
            return null;
        }

        try
        {
            // Определяем тип памяти перед чтением, если он еще не определен
            // Это гарантирует правильную валидацию размера после чтения
            if (_memoryType == SpdMemoryType.Unknown)
            {
                await RefreshMemoryTypeAsync().ConfigureAwait(true);
            }
            
            var data = await Task.Run(() =>
            {
                // Проверяем, что устройство все еще подключено перед использованием
                if (currentDevice == null || !currentDevice.IsConnected)
                {
                    return null;
                }

                lock (_lock)
                {
                    // Повторная проверка после получения lock
                    if (currentDevice == null || !currentDevice.IsConnected || _activeDevice != currentDevice)
                    {
                        return null;
                    }

                    currentDevice.I2CAddress = _activeI2cAddress;
                    // Вызываем ReadSpdDump без параметра - использует значение по умолчанию 512
                    // Это работает как в старом коде arduino_spd_87
                    return currentDevice.ReadSpdDump();
                }
            }).WaitAsync(OperationTimeout).ConfigureAwait(true);

            if (data == null)
            {
                LogWarn($"{currentDevice?.PortName}: Не удалось прочитать дамп SPD (устройство отключено).");
                return null;
            }

            LogInfo($"{currentDevice.PortName}: Дамп SPD загружен ({data.Length} байт).");
            
            // Валидация размера данных в зависимости от типа памяти
            int expectedSize = _memoryType switch
            {
                SpdMemoryType.Ddr4 => 512,
                SpdMemoryType.Ddr5 => 1024,
                _ => 256 // DDR3 и ниже
            };
            
            if (data.Length != expectedSize)
            {
                LogError($"{currentDevice.PortName}: Неверный размер SPD: ожидается {expectedSize} байт для {_memoryType}, получено {data.Length} байт.");
                return null;
            }
            
            // Проверяем RSWP после чтения
            await CheckRswpAsync().ConfigureAwait(true);
            
            return data;
        }
        catch (TimeoutException)
        {
            LogError($"{currentDevice?.PortName}: Таймаут при чтении SPD. Соединение потеряно.");
            // При таймауте отключаемся
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                DisconnectInternal(false);
            }
            return null;
        }
        catch (InvalidOperationException ex)
        {
            LogError($"{currentDevice?.PortName}: Устройство недоступно при чтении SPD: {ex.Message}");
            // При ошибке соединения отключаемся
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                DisconnectInternal(false);
            }
            return null;
        }
        catch (TaskCanceledException)
        {
            LogError($"{currentDevice?.PortName}: Операция чтения SPD отменена.");
            return null;
        }
        catch (InvalidDataException ex)
        {
            LogError($"{currentDevice?.PortName}: Неверные данные при чтении SPD: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            LogError($"{currentDevice?.PortName}: Ошибка чтения SPD ({ex.Message})");
            return null;
        }
        finally
        {
            IsReading = false;
            OnStateChanged();
        }
    }

    public async Task<bool> WriteSpdDumpAsync(byte[] data, bool skipProtectedBlocks = false)
    {
        if (!IsConnected || _activeDevice == null)
        {
            LogWarn("Устройство не подключено.");
            return false;
        }

        if (!_spdReady)
        {
            LogWarn("SPD EEPROM не обнаружена.");
            return false;
        }

        if (data == null || data.Length == 0)
        {
            LogWarn("Нет данных для записи.");
            return false;
        }

        // Валидация размера данных в зависимости от типа памяти
        int expectedSize = _memoryType switch
        {
            SpdMemoryType.Ddr4 => 512,
            SpdMemoryType.Ddr5 => 1024,
            _ => 256 // DDR3 и ниже
        };
        
        // Захватываем ссылку на устройство локально, чтобы избежать race condition
        var currentDevice = _activeDevice;
        if (currentDevice == null)
        {
            return false;
        }

        if (data.Length != expectedSize)
        {
            LogError($"{currentDevice.PortName}: Неверный размер SPD для записи: ожидается {expectedSize} байт для {_memoryType}, получено {data.Length} байт.");
            return false;
        }

        try
        {
            var result = await Task.Run(() =>
            {
                // Проверяем, что устройство все еще подключено перед использованием
                if (currentDevice == null || !currentDevice.IsConnected)
                {
                    return false;
                }

                lock (_lock)
                {
                    // Повторная проверка после получения lock
                    if (currentDevice == null || !currentDevice.IsConnected || _activeDevice != currentDevice)
                    {
                        return false;
                    }

                    currentDevice.I2CAddress = _activeI2cAddress;
                    int bytesWritten = 0;

                    // Записываем байт за байтом, точно как в оригинальном коде
                    // В оригинале нет проверки RSWP перед записью - просто записывает
                    StringBuilder hexLine = new StringBuilder();
                    const int bytesPerRow = 16;
                    
                    for (ushort i = 0; i < data.Length; i++)
                    {
                        byte b = data[i];
                        
                        // Начинаем новую строку каждые 16 байт (как в оригинале)
                        if (i % bytesPerRow == 0)
                        {
                            if (hexLine.Length > 0)
                            {
                                LogInfo(hexLine.ToString());
                                hexLine.Clear();
                            }
                            hexLine.Append($"{i:X4}: ");
                        }
                        
                        // Используем Update, как в оригинальном коде (проверяет значение перед записью)
                        // В оригинале используется Eeprom.Update по умолчанию (только если не /writeforce)
                        bool writeResult = currentDevice.UpdateSpd(i, b);
                        
                        if (!writeResult)
                        {
                            LogError($"{currentDevice.PortName}: Не удалось записать байт {i} в EEPROM по адресу {currentDevice.I2CAddress} на порту {currentDevice.PortName}.");
                            return false;
                        }
                        
                        // Добавляем байт в hex формат (как в оригинале ConsoleDisplayByte)
                        hexLine.Append($"{b:X2}");
                        if (i % bytesPerRow != bytesPerRow - 1)
                        {
                            hexLine.Append(" ");
                        }
                        
                        bytesWritten++;
                    }
                    
                    // Выводим последнюю строку, если она не пустая
                    if (hexLine.Length > 0)
                    {
                        LogInfo(hexLine.ToString());
                    }

                    LogInfo($"{currentDevice.PortName}: Записано {bytesWritten} байт в SPD.");
                    return true;
                }
            }).WaitAsync(OperationTimeout).ConfigureAwait(true);

            return result;
        }
        catch (TimeoutException)
        {
            LogError($"{currentDevice?.PortName}: Таймаут при записи SPD. Соединение потеряно.");
            // При таймауте отключаемся
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                DisconnectInternal(false);
            }
            return false;
        }
        catch (InvalidOperationException ex)
        {
            LogError($"{currentDevice?.PortName}: Устройство недоступно при записи SPD: {ex.Message}");
            // При ошибке соединения отключаемся
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                DisconnectInternal(false);
            }
            return false;
        }
        catch (TaskCanceledException)
        {
            LogError($"{currentDevice?.PortName}: Операция записи SPD отменена.");
            return false;
        }
        catch (InvalidDataException ex)
        {
            LogError($"{currentDevice?.PortName}: Неверные данные при записи SPD: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            LogError($"{currentDevice?.PortName}: Ошибка записи SPD ({ex.Message})");
            return false;
        }
    }

    public async Task<bool> SetDeviceNameAsync(string name)
    {
        if (!IsConnected || _activeDevice == null)
        {
            LogWarn("Устройство не подключено.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            LogWarn("Имя устройства не может быть пустым.");
            return false;
        }

        if (name.Length > Hardware.Arduino.Command.NAMELENGTH)
        {
            LogWarn($"Имя устройства не может быть длиннее {Hardware.Arduino.Command.NAMELENGTH} символов.");
            return false;
        }

        // Проверка на ASCII символы (0-127)
        if (name.Any(c => c > 127))
        {
            LogWarn("Имя устройства может содержать только ASCII символы (английские буквы, цифры и спецсимволы).");
            return false;
        }

        // Захватываем ссылку на устройство локально, чтобы избежать race condition
        var currentDevice = _activeDevice;
        if (currentDevice == null)
        {
            return false;
        }

        try
        {
            var result = await Task.Run(() =>
            {
                // Проверяем, что устройство все еще подключено перед использованием
                if (currentDevice == null || !currentDevice.IsConnected)
                {
                    return false;
                }

                lock (_lock)
                {
                    // Повторная проверка после получения lock
                    if (currentDevice == null || !currentDevice.IsConnected || _activeDevice != currentDevice)
                    {
                        return false;
                    }

                    return currentDevice.SetName(name);
                }
            }).WaitAsync(OperationTimeout).ConfigureAwait(true);

            if (result)
            {
                LogInfo($"{currentDevice.PortName}: Имя устройства установлено на '{name}'.");
                // Обновляем информацию о подключении
                await RefreshConnectionInfoAsync().ConfigureAwait(true);
            }
            else
            {
                LogWarn($"{currentDevice.PortName}: Имя устройства уже '{name}'.");
            }

            return result;
        }
        catch (TimeoutException)
        {
            LogError($"{currentDevice?.PortName}: Таймаут при установке имени устройства. Соединение потеряно.");
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                DisconnectInternal(false);
            }
            return false;
        }
        catch (InvalidOperationException ex)
        {
            LogError($"{currentDevice?.PortName}: Устройство недоступно при установке имени: {ex.Message}");
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                DisconnectInternal(false);
            }
            return false;
        }
        catch (TaskCanceledException)
        {
            LogError($"{currentDevice?.PortName}: Операция установки имени отменена.");
            return false;
        }
        catch (InvalidDataException ex)
        {
            LogError($"{currentDevice?.PortName}: Неверные данные при установке имени: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            LogError($"{currentDevice?.PortName}: Не удалось установить имя устройства ({ex.Message})");
            return false;
        }
    }

    private async Task RefreshConnectionInfoAsync()
    {
        if (!IsConnected || _activeDevice == null)
        {
            return;
        }

        // Захватываем ссылку на устройство локально, чтобы избежать race condition
        var currentDevice = _activeDevice;
        if (currentDevice == null)
        {
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                // Проверяем, что устройство все еще подключено перед использованием
                if (currentDevice == null || !currentDevice.IsConnected)
                {
                    return;
                }

                lock (_lock)
                {
                    // Повторная проверка после получения lock
                    if (currentDevice == null || !currentDevice.IsConnected || _activeDevice != currentDevice)
                    {
                        return;
                    }

                    string name = currentDevice.Name;
                    int firmware = currentDevice.FirmwareVersion;
                    string firmwareText = FormatFirmwareVersion(firmware);
                    ushort clock = currentDevice.I2CClock;
                    string clockText = clock == Hardware.Arduino.ClockMode.Fast ? "400 kHz" : "100 kHz";
                    byte rswp = currentDevice.RswpTypeSupport;
                    bool ddr4Rswp = (rswp & Hardware.Arduino.RswpSupport.DDR4) != 0;
                    string ddr4Text = ddr4Rswp ? "Да" : "Нет";

                    ConnectionInfoChanged?.Invoke(this, new ArduinoConnectionInfo(
                        currentDevice.PortName,
                        firmwareText,
                        name,
                        clockText,
                        ddr4Text));
                }
            }).WaitAsync(OperationTimeout).ConfigureAwait(true);
        }
        catch (TimeoutException)
        {
            LogWarn($"{currentDevice?.PortName}: Таймаут при обновлении информации о подключении.");
            // При таймауте отключаемся
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                DisconnectInternal(false);
            }
        }
        catch (InvalidOperationException ex)
        {
            LogWarn($"{currentDevice?.PortName}: Устройство недоступно при обновлении информации: {ex.Message}");
            // При ошибке соединения отключаемся
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                DisconnectInternal(false);
            }
        }
        catch (TaskCanceledException)
        {
            LogWarn($"{currentDevice?.PortName}: Операция обновления информации отменена.");
        }
        catch (InvalidDataException ex)
        {
            LogWarn($"{currentDevice?.PortName}: Неверные данные при обновлении информации: {ex.Message}");
        }
        catch (Exception ex)
        {
            LogWarn($"Не удалось обновить информацию о подключении: {ex.Message}");
        }
    }

    public async Task<bool[]> CheckRswpAsync()
    {
        if (!IsConnected || _activeDevice == null)
        {
            LogWarn("Устройство не подключено.");
            return Array.Empty<bool>();
        }

        if (!_spdReady)
        {
            LogWarn("SPD EEPROM не обнаружена.");
            return Array.Empty<bool>();
        }

        // Захватываем ссылку на устройство локально, чтобы избежать race condition
        var currentDevice = _activeDevice;
        if (currentDevice == null)
        {
            return Array.Empty<bool>();
        }

        int blockCount = ActiveRswpBlockCount;
        if (blockCount == 0)
        {
            await RefreshMemoryTypeAsync().ConfigureAwait(true);
            blockCount = ActiveRswpBlockCount;
            if (blockCount == 0)
            {
                LogWarn($"{currentDevice.PortName}: Не удалось определить тип памяти SPD. Статус RSWP недоступен.");
                return Array.Empty<bool>();
            }
        }

        System.Diagnostics.Debug.WriteLine($"[CheckRswpAsync] {currentDevice.PortName}: Запуск Task.Run, blockCount={blockCount}");
        try
        {
            var states = await Task.Run(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[CheckRswpAsync] {currentDevice?.PortName ?? "Unknown"}: Task.Run начал выполнение");
                // Проверяем, что устройство все еще подключено перед использованием
                if (currentDevice == null || !currentDevice.IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine("[CheckRswpAsync] Выход - устройство не подключено");
                    return Array.Empty<bool>();
                }

                System.Diagnostics.Debug.WriteLine($"[CheckRswpAsync] {currentDevice.PortName}: Попытка получить lock");
                lock (_lock)
                {
                    System.Diagnostics.Debug.WriteLine($"[CheckRswpAsync] {currentDevice.PortName}: Lock получен");
                    // Повторная проверка после получения lock
                    if (currentDevice == null || !currentDevice.IsConnected || _activeDevice != currentDevice)
                    {
                        System.Diagnostics.Debug.WriteLine("[CheckRswpAsync] Выход из lock - устройство недоступно");
                        return Array.Empty<bool>();
                    }

                    currentDevice.I2CAddress = _activeI2cAddress;
                    bool[] blockStates = new bool[blockCount];
                    System.Diagnostics.Debug.WriteLine($"[CheckRswpAsync] {currentDevice.PortName}: Начало цикла проверки блоков, blockCount={blockCount}");
                    for (byte block = 0; block < blockCount; block++)
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"[CheckRswpAsync] {currentDevice.PortName}: Проверка блока {block}");
                            blockStates[block] = currentDevice.GetRswp(block);
                            System.Diagnostics.Debug.WriteLine($"[CheckRswpAsync] {currentDevice.PortName}: Блок {block} = {blockStates[block]}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[CheckRswpAsync] {currentDevice.PortName}: Ошибка при проверке блока {block}: {ex.Message}");
                            // Если блок не поддерживается, считаем его незащищенным
                            blockStates[block] = false;
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"[CheckRswpAsync] {currentDevice.PortName}: Цикл завершен, Lock освобожден");
                    return blockStates;
                }
            }).WaitAsync(OperationTimeout).ConfigureAwait(true);
            System.Diagnostics.Debug.WriteLine($"[CheckRswpAsync] {currentDevice.PortName}: Task.Run завершен");
            System.Diagnostics.Debug.WriteLine($"[CheckRswpAsync] {currentDevice.PortName}: Завершен успешно, получено {states.Length} состояний");

            RswpStateChanged?.Invoke(this, states);
            
            // Построчный вывод состояния всех блоков
            LogInfo($"{currentDevice.PortName}: Статус RSWP:");
            for (int i = 0; i < states.Length; i++)
            {
                LogInfo($"{currentDevice.PortName}: Блок {i} [{(states[i] ? "Защищен" : "Не защищен")}]");
            }
            
            return states;
        }
        catch (TimeoutException)
        {
            // Таймаут может быть из-за извлечения EEPROM - это нормально
            // Отключаемся только если устройство действительно отключено
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                LogError($"{currentDevice.PortName}: Таймаут при проверке RSWP. Соединение потеряно.");
                DisconnectInternal(false);
            }
            return Array.Empty<bool>();
        }
        catch (InvalidOperationException ex)
        {
            // Ошибка может быть из-за извлечения EEPROM - это нормально
            // Отключаемся только если устройство действительно отключено
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                LogError($"{currentDevice.PortName}: Устройство недоступно при проверке RSWP: {ex.Message}");
                DisconnectInternal(false);
            }
            return Array.Empty<bool>();
        }
        catch (TaskCanceledException)
        {
            // Операция отменена - это нормально при извлечении EEPROM
            return Array.Empty<bool>();
        }
        catch (InvalidDataException ex)
        {
            LogError($"{currentDevice?.PortName}: Неверные данные при проверке RSWP: {ex.Message}");
            return Array.Empty<bool>();
        }
        catch (Exception ex)
        {
            LogError($"{currentDevice?.PortName}: Ошибка проверки RSWP ({ex.Message})");
            return Array.Empty<bool>();
        }
    }

    public async Task<bool> SetRswpAsync(byte block)
    {
        if (!IsConnected || _activeDevice == null)
        {
            LogWarn("Устройство не подключено.");
            return false;
        }

        if (!_spdReady)
        {
            LogWarn("SPD EEPROM не обнаружена.");
            return false;
        }

        if (ActiveRswpBlockCount == 0)
        {
            await RefreshMemoryTypeAsync().ConfigureAwait(true);
            if (ActiveRswpBlockCount == 0)
            {
                LogWarn($"{_activeDevice.PortName}: Не удалось определить тип памяти SPD. Очистка RSWP прервана.");
                return false;
            }
        }

        int blockCount = ActiveRswpBlockCount;
        if (blockCount == 0)
        {
            await RefreshMemoryTypeAsync().ConfigureAwait(true);
            blockCount = ActiveRswpBlockCount;
        }

        if (blockCount == 0)
        {
            LogWarn($"{_activeDevice.PortName}: Unable to determine SPD memory type. Set RSWP aborted.");
            return false;
        }

        if (block >= blockCount)
        {
            LogWarn($"{_activeDevice.PortName}: Block {block} is not available for {_memoryType} SPD.");
            return false;
        }

        // Захватываем ссылку на устройство локально, чтобы избежать race condition
        var currentDevice = _activeDevice;
        if (currentDevice == null)
        {
            return false;
        }

        try
        {
            var result = await Task.Run(() =>
            {
                // Проверяем, что устройство все еще подключено перед использованием
                if (currentDevice == null || !currentDevice.IsConnected)
                {
                    return false;
                }

                lock (_lock)
                {
                    // Повторная проверка после получения lock
                    if (currentDevice == null || !currentDevice.IsConnected || _activeDevice != currentDevice)
                    {
                        return false;
                    }

                    currentDevice.I2CAddress = _activeI2cAddress;
                    return currentDevice.SetRswp(block);
                }
            }).WaitAsync(OperationTimeout).ConfigureAwait(true);

            if (result)
            {
                LogInfo($"{currentDevice.PortName}: RSWP успешно установлен для блока {block}.");
                // ПРИМЕЧАНИЕ: Проверка состояния (CheckRswpAsync) вызывается в MainWindow после установки ВСЕХ блоков
                // Это избегает проблем с недоступностью SPD после HV операций
            }
            else
            {
                LogWarn($"{currentDevice.PortName}: ⚠️ Не удалось установить RSWP для блока {block}.");
            }

            return result;
        }
        catch (TimeoutException)
        {
            LogError($"{currentDevice?.PortName}: Таймаут при установке RSWP. Соединение потеряно.");
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                DisconnectInternal(false);
            }
            return false;
        }
        catch (InvalidOperationException ex)
        {
            LogError($"{currentDevice?.PortName}: Устройство недоступно при установке RSWP: {ex.Message}");
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                DisconnectInternal(false);
            }
            return false;
        }
        catch (TaskCanceledException)
        {
            LogError($"{currentDevice?.PortName}: Операция установки RSWP отменена.");
            return false;
        }
        catch (InvalidDataException ex)
        {
            LogError($"{currentDevice?.PortName}: Неверные данные при установке RSWP: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            LogError($"{currentDevice?.PortName}: Ошибка установки RSWP ({ex.Message})");
            return false;
        }
    }

    public async Task SetMultipleRswpAsync(byte[] blocks)
    {
        if (blocks == null || blocks.Length == 0)
            return;

        // Логирование выбранных блоков ПЕРЕД установкой
        LogInfo($"{_activeDevice?.PortName}: Установка RSWP для выбранных блоков: [{string.Join(", ", blocks)}]");

        // Устанавливаем блоки с задержками (SPD чип должен восстановиться после HV операции)
        for (int i = 0; i < blocks.Length; i++)
        {
            await SetRswpAsync(blocks[i]);
            
            // Задержка между блоками (кроме последнего)
            if (i < blocks.Length - 1)
            {
                await Task.Delay(100).ConfigureAwait(true); // 100 мс между блоками
            }
        }

        // Итоговое сообщение после установки всех блоков
        LogInfo($"{_activeDevice?.PortName}: Операция RSWP завершена для {blocks.Length} блок(ов).");
        
        // ПРИМЕЧАНИЕ: Проверка состояния (CheckRswpAsync) выполнится автоматически
        // после переинициализации SPD (событие SlaveIncrement)
    }

    public async Task<bool> ClearRswpAsync()
    {
        if (!IsConnected || _activeDevice == null)
        {
            LogWarn("Устройство не подключено.");
            return false;
        }

        if (!_spdReady)
        {
            LogWarn("SPD EEPROM не обнаружена.");
            return false;
        }

        // Захватываем ссылку на устройство локально, чтобы избежать race condition
        var currentDevice = _activeDevice;
        if (currentDevice == null)
        {
            return false;
        }

        try
        {
            var result = await Task.Run(() =>
            {
                // Проверяем, что устройство все еще подключено перед использованием
                if (currentDevice == null || !currentDevice.IsConnected)
                {
                    return false;
                }

                lock (_lock)
                {
                    // Повторная проверка после получения lock
                    if (currentDevice == null || !currentDevice.IsConnected || _activeDevice != currentDevice)
                    {
                        return false;
                    }

                    currentDevice.I2CAddress = _activeI2cAddress;
                    return currentDevice.ClearRswp();
                }
            }).WaitAsync(OperationTimeout).ConfigureAwait(true);

            if (result)
            {
                LogInfo($"{currentDevice.PortName}: RSWP очищен для всех блоков.");
                // ПРИМЕЧАНИЕ: Проверка состояния (CheckRswpAsync) выполнится автоматически
                // после переинициализации SPD (событие SlaveIncrement)
            }
            else
            {
                LogWarn($"{currentDevice.PortName}: ⚠️ Не удалось очистить RSWP.");
            }

            return result;
        }
        catch (TimeoutException)
        {
            LogError($"{currentDevice?.PortName}: Таймаут при очистке RSWP. Соединение потеряно.");
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                DisconnectInternal(false);
            }
            return false;
        }
        catch (InvalidOperationException ex)
        {
            LogError($"{currentDevice?.PortName}: Устройство недоступно при очистке RSWP: {ex.Message}");
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                DisconnectInternal(false);
            }
            return false;
        }
        catch (TaskCanceledException)
        {
            LogError($"{currentDevice?.PortName}: Операция очистки RSWP отменена.");
            return false;
        }
        catch (InvalidDataException ex)
        {
            LogError($"{currentDevice?.PortName}: Неверные данные при очистке RSWP: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            LogError($"{currentDevice?.PortName}: Ошибка очистки RSWP ({ex.Message})");
            return false;
        }
    }

    private async Task RefreshMemoryTypeAsync()
    {
        System.Diagnostics.Debug.WriteLine("[RefreshMemoryTypeAsync] Начало");
        
        if (!IsConnected || _activeDevice == null || !_spdReady)
        {
            System.Diagnostics.Debug.WriteLine($"[RefreshMemoryTypeAsync] Выход - IsConnected={IsConnected}, _activeDevice={_activeDevice != null}, _spdReady={_spdReady}");
            UpdateMemoryType(SpdMemoryType.Unknown);
            return;
        }

        // Захватываем ссылку на устройство локально, чтобы избежать race condition
        var currentDevice = _activeDevice;
        if (currentDevice == null)
        {
            System.Diagnostics.Debug.WriteLine("[RefreshMemoryTypeAsync] Выход - currentDevice null");
            UpdateMemoryType(SpdMemoryType.Unknown);
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[RefreshMemoryTypeAsync] {currentDevice.PortName}: Запуск Task.Run");
        try
        {
            var detectedType = await Task.Run(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[RefreshMemoryTypeAsync] {currentDevice?.PortName ?? "Unknown"}: Task.Run начал выполнение");
                // Проверяем, что устройство все еще подключено перед использованием
                if (currentDevice == null || !currentDevice.IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine("[RefreshMemoryTypeAsync] Выход - устройство не подключено");
                    return SpdMemoryType.Unknown;
                }

                System.Diagnostics.Debug.WriteLine($"[RefreshMemoryTypeAsync] {currentDevice.PortName}: Попытка получить lock");
                lock (_lock)
                {
                    System.Diagnostics.Debug.WriteLine($"[RefreshMemoryTypeAsync] {currentDevice.PortName}: Lock получен");
                    // Повторная проверка после получения lock
                    if (currentDevice == null || !currentDevice.IsConnected || _activeDevice != currentDevice)
                    {
                        System.Diagnostics.Debug.WriteLine("[RefreshMemoryTypeAsync] Выход из lock - устройство недоступно");
                        return SpdMemoryType.Unknown;
                    }

                    currentDevice.I2CAddress = _activeI2cAddress;
                    System.Diagnostics.Debug.WriteLine($"[RefreshMemoryTypeAsync] {currentDevice.PortName}: Вызов DetectDdr5()");
                    if (currentDevice.DetectDdr5())
                    {
                        System.Diagnostics.Debug.WriteLine($"[RefreshMemoryTypeAsync] {currentDevice.PortName}: DetectDdr5() = true");
                        return SpdMemoryType.Ddr5;
                    }

                    System.Diagnostics.Debug.WriteLine($"[RefreshMemoryTypeAsync] {currentDevice.PortName}: Вызов DetectDdr4()");
                    if (currentDevice.DetectDdr4())
                    {
                        System.Diagnostics.Debug.WriteLine($"[RefreshMemoryTypeAsync] {currentDevice.PortName}: DetectDdr4() = true");
                        return SpdMemoryType.Ddr4;
                    }

                    System.Diagnostics.Debug.WriteLine($"[RefreshMemoryTypeAsync] {currentDevice.PortName}: Тип памяти Unknown");
                    return SpdMemoryType.Unknown;
                }
            }).WaitAsync(OperationTimeout).ConfigureAwait(true);
            System.Diagnostics.Debug.WriteLine($"[RefreshMemoryTypeAsync] {currentDevice.PortName}: Task.Run завершен, detectedType={detectedType}");

            UpdateMemoryType(detectedType);
            System.Diagnostics.Debug.WriteLine($"[RefreshMemoryTypeAsync] {currentDevice.PortName}: Завершен успешно");
        }
        catch (TimeoutException)
        {
            // Таймаут может быть из-за извлечения EEPROM - это нормально
            // Отключаемся только если устройство действительно отключено
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                LogWarn($"{currentDevice.PortName}: Таймаут при определении типа памяти. Соединение потеряно.");
                DisconnectInternal(false);
            }
            UpdateMemoryType(SpdMemoryType.Unknown);
        }
        catch (InvalidOperationException ex)
        {
            // Ошибка может быть из-за извлечения EEPROM - это нормально
            // Отключаемся только если устройство действительно отключено
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                LogWarn($"{currentDevice.PortName}: Устройство недоступно при определении типа памяти: {ex.Message}");
                DisconnectInternal(false);
            }
            UpdateMemoryType(SpdMemoryType.Unknown);
        }
        catch (TaskCanceledException)
        {
            LogWarn($"{currentDevice?.PortName}: Операция определения типа памяти отменена.");
            UpdateMemoryType(SpdMemoryType.Unknown);
        }
        catch (InvalidDataException ex)
        {
            LogWarn($"{currentDevice?.PortName}: Неверные данные при определении типа памяти: {ex.Message}");
            UpdateMemoryType(SpdMemoryType.Unknown);
        }
        catch (Exception ex)
        {
            LogWarn($"{currentDevice?.PortName}: Memory type detection failed ({ex.Message}).");
            UpdateMemoryType(SpdMemoryType.Unknown);
        }
    }

    private void UpdateMemoryType(SpdMemoryType newType)
    {
        if (_memoryType == newType)
        {
            return;
        }

        _memoryType = newType;
        if (_activeDevice != null)
        {
            string portName = _activeDevice.PortName;
            switch (newType)
            {
                case SpdMemoryType.Ddr4:
                    LogInfo($"{portName}: Обнаружена DDR4 SPD.");
                    break;
                case SpdMemoryType.Ddr5:
                    LogInfo($"{portName}: Обнаружена DDR5 SPD.");
                    break;
                default:
                    // Не логируем Unknown - это временное состояние при переинициализации SPD
                    break;
            }
        }

        MemoryTypeChanged?.Invoke(this, _memoryType);
    }

    private async Task<ArduinoDeviceInfo?> ProbePortWithTimeoutAsync(string portName, CancellationToken token)
    {
        var probeStopwatch = Stopwatch.StartNew();

        try
        {
            token.ThrowIfCancellationRequested();

            var probeTask = Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                return ProbePort(portName);
            }, token);

            var delayTask = Task.Delay(PortProbeTimeout, token);
            var completedTask = await Task.WhenAny(probeTask, delayTask).ConfigureAwait(false);

            if (completedTask == probeTask)
            {
                var info = await probeTask.ConfigureAwait(false);
                probeStopwatch.Stop();
                if (info != null)
                {
                    info.ProbeDurationMs = probeStopwatch.ElapsedMilliseconds;
                }
                return info;
            }

            LogWarn($"{portName}: Тайм-аут проверки.");
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            LogWarn($"{portName}: probe failed ({ex.Message}).");
            return null;
        }
        finally
        {
            probeStopwatch.Stop();
        }
    }

    private ArduinoDeviceInfo? ProbePort(string portName)
    {
        // Используем отдельные настройки для сканирования портов:
        // таймаут немного больше (3 с), чтобы настоящие устройства успевали ответить,
        // а "пустые" аппаратные COM-порты всё равно быстро освобождались благодаря внешнему PortProbeTimeout.
        var scanPortSettings = new Hardware.Arduino.SerialPortSettings(115200, true, true, 3);
        
        LogInfo($"{portName}: Проверка сигнатуры устройства...");

        using var device = new Hardware.Arduino(scanPortSettings, portName);
        try
        {
            device.Connect();
            LogInfo($"{portName}: Проверка успешна.");

            int firmware = device.FirmwareVersion;
            string firmwareText = FormatFirmwareVersion(firmware);
            string name = device.Name;

            return new ArduinoDeviceInfo
            {
                Port = portName,
                FirmwareVersion = firmwareText,
                Name = name
            };
        }
        catch (TimeoutException)
        {
            // Таймаут - порт не содержит Arduino устройство, это нормально
            return null;
        }
        catch (Exception ex)
        {
            LogWarn($"{portName}: Ошибка проверки ({ex.Message}).");
            return null;
        }
        finally
        {
            try
            {
                if (device.IsConnected)
                {
                    device.Disconnect();
                }
            }
            catch
            {
                // Игнорируем ошибки при отключении
            }
        }
    }

    private void HandleAlert(object? sender, Hardware.Arduino.ArduinoAlertEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[HandleAlert] Начало обработки алерта {e.Code}");
        
        if (_activeDevice == null || sender != _activeDevice)
        {
            System.Diagnostics.Debug.WriteLine("[HandleAlert] Выход - устройство null или sender не совпадает");
            return;
        }

        if (e.Code == Hardware.Arduino.AlertCodes.SlaveIncrement)
        {
            System.Diagnostics.Debug.WriteLine($"[HandleAlert] {_activeDevice.PortName}: SlaveIncrement получен");
            
            // Отменяем предыдущие операции, если они еще выполняются
            _alertOperationCancellation?.Cancel();
            _alertOperationCancellation?.Dispose();
            _alertOperationCancellation = new CancellationTokenSource();
            var cancellationToken = _alertOperationCancellation.Token;

            // Проверяем, не обрабатывается ли уже другой алерт
            if (_isHandlingAlert)
            {
                System.Diagnostics.Debug.WriteLine($"[HandleAlert] {_activeDevice.PortName}: Выход - уже обрабатывается другой алерт");
                return;
            }

            // Сохраняем ссылку на устройство локально
            var device = _activeDevice;
            if (device == null)
            {
                System.Diagnostics.Debug.WriteLine("[HandleAlert] Выход - device null после получения ссылки");
                return;
            }
            
            _spdReady = true;
            SpdStateChanged?.Invoke(this, _spdReady);
            LogInfo($"{device.PortName}: Обнаружена новая SPD EEPROM");
            
            // Выполняем операции в отдельном потоке, как в старом коде (new Thread(() => HandleAlert(...)).Start())
            _isHandlingAlert = true;
            _ = Task.Run(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Task.Run начал выполнение");
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Выполняем сканирование I2C синхронно в lock (как в старом коде)
                    // Делаем это быстро, чтобы не блокировать другие операции
                    // ВАЖНО: Проверяем cancellation token ПЕРЕД получением lock, чтобы не блокировать другие потоки
                    cancellationToken.ThrowIfCancellationRequested();
                    System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Попытка получить lock для сканирования I2C");
                    lock (_lock)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Lock получен, начинаем сканирование");
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // Проверяем только подключение устройства, не проверяем _spdReady
                        // (EEPROM может быть извлечена - это нормально)
                        if (device == null || !device.IsConnected || _activeDevice != device)
                        {
                            System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Выход из lock - устройство недоступно");
                            return;
                        }
                        
                        device.I2CAddress = _activeI2cAddress;
                        
                        // Полное сканирование I2C
                        // ВАЖНО: Проверяем cancellation token ПЕРЕД вызовом ScanFull(), чтобы не блокировать lock при отмене
                        cancellationToken.ThrowIfCancellationRequested();
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Выполнение ScanFull()");
                        byte[] fullAddresses = device.ScanFull();
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: ScanFull() завершен, найдено {fullAddresses.Length} адресов");
                        
                        if (fullAddresses.Length > 0)
                        {
                            var addressList = string.Join(", ", fullAddresses.Select(a => $"0x{a:X2}"));
                            LogInfo($"{device.PortName}: Полное сканирование I2C: найдено {fullAddresses.Length} устройств ({addressList})");
                        }
                        else
                        {
                            LogInfo($"{device.PortName}: Полное сканирование I2C: устройства не найдены");
                        }
                        
                        _fullScanAddresses = fullAddresses;
                        
                        // Быстрый скан для определения SPD адресов
                        cancellationToken.ThrowIfCancellationRequested();
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Выполнение Scan()");
                        var addresses = device.Scan();
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Scan() завершен, найдено {addresses.Length} адресов");
                        if (addresses.Length > 0)
                        {
                            _activeI2cAddress = addresses[0];
                        }
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Lock освобожден");
                    }
                    
                    // Проверяем отмену и состояние перед длительными операциями
                    cancellationToken.ThrowIfCancellationRequested();
                    if (device == null || !device.IsConnected || _activeDevice != device)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Выход перед RefreshMemoryTypeAsync - устройство недоступно");
                        return;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Начало RefreshMemoryTypeAsync()");
                    // Обновляем тип памяти с таймаутом и проверкой отмены
                    // Если EEPROM извлечена, операция может завершиться с ошибкой - это нормально
                    try
                    {
                        await RefreshMemoryTypeAsync().WaitAsync(OperationTimeout, cancellationToken).ConfigureAwait(false);
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: RefreshMemoryTypeAsync() завершен успешно");
                    }
                    catch (OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: RefreshMemoryTypeAsync() отменен");
                        return; // Операция отменена - выходим без обновления UI
                    }
                    catch (Exception ex) when (ex is TimeoutException || ex is InvalidOperationException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: RefreshMemoryTypeAsync() ошибка: {ex.GetType().Name} - {ex.Message}");
                        // Ошибка может быть из-за извлечения EEPROM - это нормально, не логируем как ошибку
                        // Просто выходим без продолжения
                        return;
                    }
                    
                    // Проверяем отмену и состояние перед проверкой RSWP
                    cancellationToken.ThrowIfCancellationRequested();
                    if (device == null || !device.IsConnected || _activeDevice != device)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Выход перед CheckRswpAsync - устройство недоступно");
                        return;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Начало CheckRswpAsync()");
                    // Проверяем RSWP с таймаутом и проверкой отмены
                    // Если EEPROM извлечена, операция может завершиться с ошибкой - это нормально
                    try
                    {
                        await CheckRswpAsync().WaitAsync(OperationTimeout, cancellationToken).ConfigureAwait(false);
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: CheckRswpAsync() завершен успешно");
                    }
                    catch (OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: CheckRswpAsync() отменен");
                        return; // Операция отменена - выходим без обновления UI
                    }
                    catch (Exception ex) when (ex is TimeoutException || ex is InvalidOperationException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: CheckRswpAsync() ошибка: {ex.GetType().Name} - {ex.Message}");
                        // Ошибка может быть из-за извлечения EEPROM - это нормально, не логируем как ошибку
                        // Просто выходим без продолжения
                        return;
                    }
                    
                    // Обновляем UI только если все операции завершились успешно и не были отменены
                    cancellationToken.ThrowIfCancellationRequested();
                    if (device != null && device.IsConnected && _activeDevice == device)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Вызов OnStateChanged()");
                        OnStateChanged();
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: OnStateChanged() завершен");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: OnStateChanged() не вызван - устройство недоступно");
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Task.Run завершен успешно");
                }
                catch (OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Task.Run отменен (OperationCanceledException)");
                    // Операция отменена - это нормально при новом алерте или извлечении EEPROM
                }
                catch (Exception ex) when (ex is TimeoutException || ex is InvalidOperationException || ex is InvalidDataException)
                {
                    System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Task.Run ошибка: {ex.GetType().Name} - {ex.Message}");
                    // Ошибки могут возникать при извлечении EEPROM - это нормально
                    // Отключаемся только если действительно потеряна связь с Arduino
                    if (device != null && !device.IsConnected)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Устройство отключено, вызываем DisconnectInternal");
                        DisconnectInternal(false);
                    }
                    // Иначе просто игнорируем ошибку (EEPROM может быть извлечена)
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Task.Run неожиданная ошибка: {ex.GetType().Name} - {ex.Message}");
                    // Неожиданные ошибки логируем, но не отключаемся
                    // (могут быть из-за извлечения EEPROM)
                    LogWarn($"{device?.PortName ?? "Unknown"}: Неожиданная ошибка при обработке алерта: {ex.Message}");
                }
                finally
                {
                    System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Task.Run finally - сброс _isHandlingAlert");
                    _isHandlingAlert = false;
                    System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Task.Run завершен");
                }
            }, cancellationToken);
        }
        else if (e.Code == Hardware.Arduino.AlertCodes.SlaveDecrement)
        {
            System.Diagnostics.Debug.WriteLine($"[HandleAlert] {_activeDevice.PortName}: SlaveDecrement получен");
            
            // Отменяем операции, если они выполняются (это должно быстро остановить выполняющиеся операции)
            _alertOperationCancellation?.Cancel();
            
            // Сбрасываем состояние сразу
            _spdReady = false;
            SpdStateChanged?.Invoke(this, _spdReady);
            LogInfo($"{_activeDevice.PortName}: SPD EEPROM извлечена.");
            UpdateMemoryType(SpdMemoryType.Unknown);
            RswpStateChanged?.Invoke(this, Array.Empty<bool>());
            _fullScanAddresses = null;
            _isHandlingAlert = false;
            
            // Обновляем UI (событие должно обрабатываться быстро обработчиками)
            OnStateChanged();
            System.Diagnostics.Debug.WriteLine($"[HandleAlert] {_activeDevice.PortName}: SlaveDecrement обработан");
        }
    }

        private void HandleConnectionLost(object? sender, EventArgs e)
        {
            if (_activeDevice == null || sender != _activeDevice)
            {
                return;
            }

            var device = _activeDevice;
            System.Diagnostics.Debug.WriteLine($"[HandleConnectionLost] {device.PortName}: Соединение потеряно. Начало отключения.");
            // Это событие вызывается только при реальной потере связи с Arduino
            // (не при извлечении EEPROM)
            DisconnectInternal(false);
            System.Diagnostics.Debug.WriteLine($"[HandleConnectionLost] {device.PortName}: Отключение завершено.");
        }

    private void DisconnectInternal(bool logDisconnect)
    {
        if (_activeDevice == null)
        {
            return;
        }

        // Отменяем все операции при отключении
        _alertOperationCancellation?.Cancel();
        _alertOperationCancellation?.Dispose();
        _alertOperationCancellation = null;
        _isHandlingAlert = false;

        var device = _activeDevice;
        _activeDevice = null;

        try
        {
            device.AlertReceived -= HandleAlert;
            device.ConnectionLost -= HandleConnectionLost;

                if (device.IsConnected)
                {
                    try
                    {
                        device.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        LogWarn($"Disconnect error: {ex.Message}");
                    }
                }

                if (logDisconnect)
                {
                    LogInfo($"{device.PortName} disconnected");
                }
        }
        catch (Exception ex)
        {
            LogWarn($"Disconnect error: {ex.Message}");
        }
        finally
        {
            device.Dispose();
            _spdReady = false;
            SpdStateChanged?.Invoke(this, _spdReady);
            UpdateMemoryType(SpdMemoryType.Unknown);
            RswpStateChanged?.Invoke(this, Array.Empty<bool>());
            ConnectionInfoChanged?.Invoke(this, ArduinoConnectionInfo.Empty);
            ConnectionStateChanged?.Invoke(this, false);
            OnStateChanged();
        }
    }

    private static string FormatFirmwareVersion(int version)
    {
        return version <= 0 ? "unknown" : version.ToString();
    }

    private void LogRswpState(string portName, byte rswp)
    {
        LogInfo((rswp & Hardware.Arduino.RswpSupport.DDR5) != 0
            ? $"{portName}: DDR5 RSWP доступен"
            : $"{portName}: DDR5 RSWP отключен; Оффлайн режим недоступен");

        LogInfo((rswp & Hardware.Arduino.RswpSupport.DDR4) != 0
            ? $"{portName}: DDR4 RSWP доступен"
            : $"{portName}: DDR4 RSWP недоступен");

        LogInfo((rswp & Hardware.Arduino.RswpSupport.DDR3) != 0
            ? $"{portName}: DDR3-SDRAM RSWP доступен"
            : $"{portName}: DDR3-SDRAM RSWP недоступен");
    }

    private void LogInfo(string message) => Log("Info", message);
    private void LogWarn(string message) => Log("Warn", message);
    private void LogError(string message) => Log("Error", message);

    private void Log(string level, string message)
    {
        LogGenerated?.Invoke(this, new ArduinoLogEventArgs(level, message));
    }

    private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class ArduinoDeviceInfo : INotifyPropertyChanged
{
    private string _port = string.Empty;
    private string _firmwareVersion = "—";
    private string _name = "—";

    public string Port
    {
        get => _port;
        set
        {
            if (_port != value)
            {
                _port = value;
                OnPropertyChanged(nameof(Port));
                OnPropertyChanged(nameof(DisplaySummary));
            }
        }
    }

    public string FirmwareVersion
    {
        get => _firmwareVersion;
        set
        {
            if (_firmwareVersion != value)
            {
                _firmwareVersion = value;
                OnPropertyChanged(nameof(FirmwareVersion));
                OnPropertyChanged(nameof(DisplaySummary));
            }
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(DisplaySummary));
            }
        }
    }

    public long ProbeDurationMs { get; set; }

    public string DisplaySummary
    {
        get
        {
            string namePart = string.IsNullOrWhiteSpace(Name) ? "—" : Name;
            string fwPart = string.IsNullOrWhiteSpace(FirmwareVersion) ? "—" : FirmwareVersion;
            return $"COM: {Port}, NAME: {namePart}, FW: {fwPart}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed class ArduinoConnectionInfo
{
    public ArduinoConnectionInfo(string port, string firmware, string name, string clock, string ddr4)
    {
        Port = port;
        FirmwareVersion = firmware;
        Name = name;
        I2CClock = clock;
        Ddr4Rswp = ddr4;
    }

    public string Port { get; }
    public string FirmwareVersion { get; }
    public string Name { get; }
    public string I2CClock { get; }
    public string Ddr4Rswp { get; }

    public static ArduinoConnectionInfo Empty { get; } = new ArduinoConnectionInfo("—", "—", "—", "—", "—");
}

internal sealed class ArduinoLogEventArgs : EventArgs
{
    public ArduinoLogEventArgs(string level, string message)
    {
        Level = level;
        Message = message;
    }

    public string Level { get; }
    public string Message { get; }
}

