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

        try
        {
            var states = await Task.Run(() =>
            {
                // Проверяем, что устройство все еще подключено перед использованием
                if (currentDevice == null || !currentDevice.IsConnected)
                {
                    return Array.Empty<bool>();
                }

                lock (_lock)
                {
                    // Повторная проверка после получения lock
                    if (currentDevice == null || !currentDevice.IsConnected || _activeDevice != currentDevice)
                    {
                        return Array.Empty<bool>();
                    }

                    currentDevice.I2CAddress = _activeI2cAddress;
                    bool[] blockStates = new bool[blockCount];
                    for (byte block = 0; block < blockCount; block++)
                    {
                        try
                        {
                            blockStates[block] = currentDevice.GetRswp(block);
                        }
                        catch
                        {
                            // Если блок не поддерживается, считаем его незащищенным
                            blockStates[block] = false;
                        }
                    }
                    return blockStates;
                }
            }).WaitAsync(OperationTimeout).ConfigureAwait(true);

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
            LogError($"{currentDevice?.PortName}: Таймаут при проверке RSWP. Соединение потеряно.");
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                DisconnectInternal(false);
            }
            return Array.Empty<bool>();
        }
        catch (InvalidOperationException ex)
        {
            LogError($"{currentDevice?.PortName}: Устройство недоступно при проверке RSWP: {ex.Message}");
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                DisconnectInternal(false);
            }
            return Array.Empty<bool>();
        }
        catch (TaskCanceledException)
        {
            LogError($"{currentDevice?.PortName}: Операция проверки RSWP отменена.");
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
        if (!IsConnected || _activeDevice == null || !_spdReady)
        {
            UpdateMemoryType(SpdMemoryType.Unknown);
            return;
        }

        // Захватываем ссылку на устройство локально, чтобы избежать race condition
        var currentDevice = _activeDevice;
        if (currentDevice == null)
        {
            UpdateMemoryType(SpdMemoryType.Unknown);
            return;
        }

        try
        {
            var detectedType = await Task.Run(() =>
            {
                // Проверяем, что устройство все еще подключено перед использованием
                if (currentDevice == null || !currentDevice.IsConnected)
                {
                    return SpdMemoryType.Unknown;
                }

                lock (_lock)
                {
                    // Повторная проверка после получения lock
                    if (currentDevice == null || !currentDevice.IsConnected || _activeDevice != currentDevice)
                    {
                        return SpdMemoryType.Unknown;
                    }

                    currentDevice.I2CAddress = _activeI2cAddress;
                    if (currentDevice.DetectDdr5())
                    {
                        return SpdMemoryType.Ddr5;
                    }

                    if (currentDevice.DetectDdr4())
                    {
                        return SpdMemoryType.Ddr4;
                    }

                    return SpdMemoryType.Unknown;
                }
            }).WaitAsync(OperationTimeout).ConfigureAwait(true);

            UpdateMemoryType(detectedType);
        }
        catch (TimeoutException)
        {
            LogWarn($"{currentDevice?.PortName}: Таймаут при определении типа памяти. Соединение потеряно.");
            if (currentDevice != null && !currentDevice.IsConnected)
            {
                DisconnectInternal(false);
            }
            UpdateMemoryType(SpdMemoryType.Unknown);
        }
        catch (InvalidOperationException ex)
        {
            LogWarn($"{currentDevice?.PortName}: Устройство недоступно при определении типа памяти: {ex.Message}");
            if (currentDevice != null && !currentDevice.IsConnected)
            {
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
        if (_activeDevice == null || sender != _activeDevice)
        {
            return;
        }

        if (e.Code == Hardware.Arduino.AlertCodes.SlaveIncrement)
        {
            // Отменяем предыдущие операции, если они еще выполняются
            _alertOperationCancellation?.Cancel();
            _alertOperationCancellation?.Dispose();
            _alertOperationCancellation = new CancellationTokenSource();
            var cancellationToken = _alertOperationCancellation.Token;

            // Проверяем, не обрабатывается ли уже другой алерт
            if (_isHandlingAlert)
            {
                LogWarn($"[DEBUG] {_activeDevice.PortName}: HandleAlert: Пропуск - уже обрабатывается другой алерт");
                return;
            }

            // Сохраняем ссылку на устройство локально, чтобы избежать race condition
            var device = _activeDevice;
            if (device == null)
            {
                return;
            }
            
            _spdReady = true;
            SpdStateChanged?.Invoke(this, _spdReady);
            LogInfo($"{device.PortName}: Обнаружена новая SPD EEPROM");
            
            // Выполняем все операции последовательно в одной задаче, как в старом коде
            _isHandlingAlert = true;
            _ = Task.Run(async () =>
            {
                try
                {
                    // Проверяем отмену перед началом
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    LogInfo($"[DEBUG] {device.PortName}: HandleAlert: Начало обработки SlaveIncrement");
                    
                    // Проверяем, что устройство все еще подключено
                    if (device == null || !device.IsConnected || _activeDevice != device)
                    {
                        LogWarn($"[DEBUG] {device?.PortName ?? "Unknown"}: HandleAlert: Устройство отключено перед обработкой");
                        return;
                    }
                    
                    // Сначала выполняем полное сканирование всех I2C адресов (как в старом коде - синхронно)
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Run(() =>
                    {
                        lock (_lock)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            // Повторная проверка после получения lock
                            if (device == null || !device.IsConnected || _activeDevice != device)
                            {
                                LogWarn($"[DEBUG] {device?.PortName ?? "Unknown"}: HandleAlert: Устройство отключено после получения lock");
                                return;
                            }
                            
                            LogInfo($"[DEBUG] {device.PortName}: HandleAlert: Выполнение ScanFull()");
                            device.I2CAddress = _activeI2cAddress;
                            byte[] fullAddresses = device.ScanFull();
                            
                            if (fullAddresses.Length > 0)
                            {
                                var addressList = string.Join(", ", fullAddresses.Select(a => $"0x{a:X2}"));
                                LogInfo($"{device.PortName}: Полное сканирование I2C: найдено {fullAddresses.Length} устройств ({addressList})");
                            }
                            else
                            {
                                LogInfo($"{device.PortName}: Полное сканирование I2C: устройства не найдены");
                            }
                            
                            // Сохраняем результаты полного сканирования
                            _fullScanAddresses = fullAddresses;
                            
                            // Затем выполняем быстрый скан для определения SPD адресов
                            LogInfo($"[DEBUG] {device.PortName}: HandleAlert: Выполнение Scan()");
                            var addresses = device.Scan();
                            if (addresses.Length > 0)
                            {
                                _activeI2cAddress = addresses[0];
                            }
                        }
                    }, cancellationToken).WaitAsync(OperationTimeout).ConfigureAwait(false);
                    
                    // Проверяем отмену перед обновлением типа памяти
                    cancellationToken.ThrowIfCancellationRequested();
                    if (device == null || !device.IsConnected || _activeDevice != device)
                    {
                        LogWarn($"[DEBUG] {device?.PortName ?? "Unknown"}: HandleAlert: Устройство отключено перед обновлением типа памяти");
                        return;
                    }
                    
                    // Обновляем тип памяти
                    LogInfo($"[DEBUG] {device.PortName}: HandleAlert: Обновление типа памяти");
                    var refreshTask = RefreshMemoryTypeAsync();
                    await refreshTask.WaitAsync(OperationTimeout, cancellationToken).ConfigureAwait(false);
                    
                    // Проверяем отмену перед проверкой RSWP
                    cancellationToken.ThrowIfCancellationRequested();
                    if (device == null || !device.IsConnected || _activeDevice != device)
                    {
                        LogWarn($"[DEBUG] {device?.PortName ?? "Unknown"}: HandleAlert: Устройство отключено перед проверкой RSWP");
                        return;
                    }
                    
                    // Проверяем RSWP
                    LogInfo($"[DEBUG] {device.PortName}: HandleAlert: Проверка RSWP");
                    var checkRswpTask = CheckRswpAsync();
                    await checkRswpTask.WaitAsync(OperationTimeout, cancellationToken).ConfigureAwait(false);
                    
                    // Уведомляем об изменении состояния для обновления UI после завершения всех операций
                    OnStateChanged();
                    LogInfo($"[DEBUG] {device.PortName}: HandleAlert: Обработка SlaveIncrement завершена успешно");
                }
                catch (OperationCanceledException)
                {
                    LogWarn($"[DEBUG] {device?.PortName ?? "Unknown"}: HandleAlert: Операция отменена (новый алерт получен)");
                }
                catch (TimeoutException)
                {
                    LogError($"[DEBUG] {device?.PortName ?? "Unknown"}: HandleAlert: Таймаут при обработке SlaveIncrement. Соединение потеряно.");
                    if (device != null && !device.IsConnected)
                    {
                        DisconnectInternal(false);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    LogError($"[DEBUG] {device?.PortName ?? "Unknown"}: HandleAlert: Устройство недоступно: {ex.Message}");
                    if (device != null && !device.IsConnected)
                    {
                        DisconnectInternal(false);
                    }
                }
                catch (InvalidDataException ex)
                {
                    LogWarn($"[DEBUG] {device?.PortName ?? "Unknown"}: HandleAlert: Неверные данные: {ex.Message}");
                }
                catch (Exception ex)
                {
                    LogWarn($"[DEBUG] {device?.PortName ?? "Unknown"}: HandleAlert: Ошибка при обработке SlaveIncrement: {ex.Message}");
                }
                finally
                {
                    _isHandlingAlert = false;
                }
            }, cancellationToken);
        }
        else if (e.Code == Hardware.Arduino.AlertCodes.SlaveDecrement)
        {
            // Отменяем операции, если они выполняются
            _alertOperationCancellation?.Cancel();
            
            _spdReady = false;
            SpdStateChanged?.Invoke(this, _spdReady);
            LogInfo($"{_activeDevice.PortName}: SPD EEPROM извлечена.");
            UpdateMemoryType(SpdMemoryType.Unknown);
            // Очищаем состояние RSWP при удалении SPD
            RswpStateChanged?.Invoke(this, Array.Empty<bool>());
            // Очищаем результаты полного сканирования при извлечении EEPROM
            _fullScanAddresses = null;
            _isHandlingAlert = false; // Сбрасываем флаг обработки
            OnStateChanged();
        }
    }

        private void HandleConnectionLost(object? sender, EventArgs e)
        {
            if (_activeDevice == null || sender != _activeDevice)
            {
                return;
            }

            var device = _activeDevice;
            LogWarn($"[DEBUG] {device.PortName}: HandleConnectionLost: Соединение потеряно. Начало отключения.");
            DisconnectInternal(false);
            LogWarn($"[DEBUG] {device.PortName}: HandleConnectionLost: Отключение завершено.");
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

