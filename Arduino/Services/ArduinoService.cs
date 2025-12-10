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
    private volatile bool _isSettingRswp; // Флаг для предотвращения обработки алертов во время установки RSWP

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
        System.Diagnostics.Debug.WriteLine("[ScanAsync] Начало сканирования");
        
        if (IsScanning)
        {
            System.Diagnostics.Debug.WriteLine("[ScanAsync] Выход - сканирование уже выполняется");
            return;
        }

        System.Diagnostics.Debug.WriteLine("[ScanAsync] Отмена предыдущего сканирования");
        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        _scanCancellation = new CancellationTokenSource();
        var token = _scanCancellation.Token;

        IsScanning = true;
        OnStateChanged();
        LogInfo("Запуск сканирования Arduino...");
        System.Diagnostics.Debug.WriteLine("[ScanAsync] Флаг IsScanning установлен");

        var discovered = new List<ArduinoDeviceInfo>();
        var scanStopwatch = Stopwatch.StartNew();
        string[] ports;
        
        System.Diagnostics.Debug.WriteLine("[ScanAsync] Получение списка COM-портов...");
        try
        {
            ports = SerialPort.GetPortNames()
                              .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                              .ToArray();
            System.Diagnostics.Debug.WriteLine($"[ScanAsync] Найдено COM-портов: {ports.Length} ({string.Join(", ", ports)})");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScanAsync] Ошибка получения списка портов: {ex.GetType().Name} - {ex.Message}");
            LogError($"Не удалось перечислить COM-порты: {ex.Message}");
            IsScanning = false;
            OnStateChanged();
            return;
        }

        if (ports.Length == 0)
        {
            System.Diagnostics.Debug.WriteLine("[ScanAsync] COM-порты не обнаружены");
            LogWarn("COM-порты не обнаружены.");
            IsScanning = false;
            OnStateChanged();
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[ScanAsync] Начало проверки {ports.Length} портов");
        try
        {
            int portIndex = 0;
            foreach (var port in ports)
            {
                portIndex++;
                System.Diagnostics.Debug.WriteLine($"[ScanAsync] Проверка порта {portIndex}/{ports.Length}: {port}");
                
                if (token.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine("[ScanAsync] Сканирование отменено через CancellationToken");
                    LogWarn("Сканирование Arduino отменено.");
                    break;
                }

                System.Diagnostics.Debug.WriteLine($"[ScanAsync] Вызов ProbePortWithTimeoutAsync для {port}");
                var portCheckStart = Stopwatch.StartNew();
                
                var info = await ProbePortWithTimeoutAsync(port, token).ConfigureAwait(true);
                
                portCheckStart.Stop();
                System.Diagnostics.Debug.WriteLine($"[ScanAsync] ProbePortWithTimeoutAsync для {port} завершен за {portCheckStart.ElapsedMilliseconds} мс, результат: {(info != null ? "найдено" : "не найдено")}");
                
                if (info != null)
                {
                    discovered.Add(info);
                    LogInfo($"{port}: Arduino обнаружен (проверка завершена за {info.ProbeDurationMs} мс).");
                    System.Diagnostics.Debug.WriteLine($"[ScanAsync] {port}: Arduino добавлен в список найденных устройств");
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[ScanAsync] Проверка всех портов завершена. Найдено устройств: {discovered.Count}");
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[ScanAsync] OperationCanceledException - сканирование отменено");
            LogWarn("Сканирование Arduino отменено.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScanAsync] Неожиданная ошибка в цикле проверки портов: {ex.GetType().Name} - {ex.Message}");
            LogError($"Ошибка при сканировании портов: {ex.Message}");
        }
        finally
        {
            System.Diagnostics.Debug.WriteLine("[ScanAsync] Очистка CancellationTokenSource");
            _scanCancellation?.Dispose();
            _scanCancellation = null;
        }

        System.Diagnostics.Debug.WriteLine("[ScanAsync] Обновление UI через Dispatcher.Invoke");
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine("[ScanAsync] Dispatcher.Invoke: начало обновления Devices");
                Devices.Clear();
                foreach (var device in discovered.OrderBy(d => d.Port, StringComparer.OrdinalIgnoreCase))
                {
                    Devices.Add(device);
                }
                SetSelectedDevice(Devices.FirstOrDefault());
                System.Diagnostics.Debug.WriteLine($"[ScanAsync] Dispatcher.Invoke: обновлено {Devices.Count} устройств");
            });
            System.Diagnostics.Debug.WriteLine("[ScanAsync] Dispatcher.Invoke завершен");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScanAsync] Ошибка в Dispatcher.Invoke: {ex.GetType().Name} - {ex.Message}");
            LogError($"Ошибка при обновлении списка устройств: {ex.Message}");
        }

        scanStopwatch.Stop();
        System.Diagnostics.Debug.WriteLine($"[ScanAsync] Сканирование завершено. Время: {scanStopwatch.ElapsedMilliseconds} мс, портов: {ports.Length}, найдено: {discovered.Count}");
        LogInfo($"Сканирование Arduino завершено. Проверено портов: {ports.Length}, найдено устройств: {discovered.Count}, длительность: {scanStopwatch.ElapsedMilliseconds} мс.");
        
        IsScanning = false;
        OnStateChanged();
        System.Diagnostics.Debug.WriteLine("[ScanAsync] Флаг IsScanning сброшен, сканирование завершено");
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

        // Устанавливаем флаг для игнорирования алертов во время установки RSWP
        // При установке RSWP для DDR4 используется высокое напряжение (HV), что может вызывать
        // временное отключение EEPROM на I2C шине и алерты SlaveDecrement/SlaveIncrement
        // Эти алерты не должны прерывать операцию установки RSWP
        _isSettingRswp = true;
        
        try
        {
            // Устанавливаем блоки с задержками (SPD чип должен восстановиться после HV операции)
            for (int i = 0; i < blocks.Length; i++)
            {
                await SetRswpAsync(blocks[i]);
                
                // Задержка между блоками (кроме последнего)
                // Это позволяет EEPROM восстановиться после HV операции
                if (i < blocks.Length - 1)
                {
                    await Task.Delay(100).ConfigureAwait(true); // 100 мс между блоками
                }
            }

            // Итоговое сообщение после установки всех блоков
            LogInfo($"{_activeDevice?.PortName}: Операция RSWP завершена для {blocks.Length} блок(ов).");
        }
        finally
        {
            // Сбрасываем флаг после завершения операции
            _isSettingRswp = false;
        }
        
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

        // Устанавливаем флаг для игнорирования алертов во время очистки RSWP
        // Очистка RSWP также использует высокое напряжение (HV), что может вызывать алерты
        _isSettingRswp = true;
        
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
        finally
        {
            // Сбрасываем флаг после завершения операции
            _isSettingRswp = false;
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
        System.Diagnostics.Debug.WriteLine($"[ProbePortWithTimeoutAsync] {portName}: Начало проверки порта");
        var probeStopwatch = Stopwatch.StartNew();

        try
        {
            System.Diagnostics.Debug.WriteLine($"[ProbePortWithTimeoutAsync] {portName}: Проверка CancellationToken");
            token.ThrowIfCancellationRequested();

            System.Diagnostics.Debug.WriteLine($"[ProbePortWithTimeoutAsync] {portName}: Запуск Task.Run для ProbePort");
            var probeTask = Task.Run(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[ProbePortWithTimeoutAsync] {portName}: Task.Run начал выполнение ProbePort");
                token.ThrowIfCancellationRequested();
                var result = ProbePort(portName);
                System.Diagnostics.Debug.WriteLine($"[ProbePortWithTimeoutAsync] {portName}: ProbePort завершен, результат: {(result != null ? "найдено" : "не найдено")}");
                return result;
            }, token);

            System.Diagnostics.Debug.WriteLine($"[ProbePortWithTimeoutAsync] {portName}: Запуск Task.Delay с таймаутом {PortProbeTimeout.TotalMilliseconds} мс");
            var delayTask = Task.Delay(PortProbeTimeout, token);
            
            System.Diagnostics.Debug.WriteLine($"[ProbePortWithTimeoutAsync] {portName}: Ожидание завершения probeTask или delayTask");
            var completedTask = await Task.WhenAny(probeTask, delayTask).ConfigureAwait(false);

            if (completedTask == probeTask)
            {
                System.Diagnostics.Debug.WriteLine($"[ProbePortWithTimeoutAsync] {portName}: probeTask завершился первым");
                var info = await probeTask.ConfigureAwait(false);
                probeStopwatch.Stop();
                if (info != null)
                {
                    info.ProbeDurationMs = probeStopwatch.ElapsedMilliseconds;
                    System.Diagnostics.Debug.WriteLine($"[ProbePortWithTimeoutAsync] {portName}: Arduino найден за {info.ProbeDurationMs} мс");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ProbePortWithTimeoutAsync] {portName}: Arduino не найден");
                }
                return info;
            }

            System.Diagnostics.Debug.WriteLine($"[ProbePortWithTimeoutAsync] {portName}: delayTask завершился первым - таймаут");
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
        System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: Начало проверки порта");
        
        // Используем отдельные настройки для сканирования портов:
        // таймаут немного больше (3 с), чтобы настоящие устройства успевали ответить,
        // а "пустые" аппаратные COM-порты всё равно быстро освобождались благодаря внешнему PortProbeTimeout.
        var scanPortSettings = new Hardware.Arduino.SerialPortSettings(115200, true, true, 3);
        
        System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: Создание SerialPortSettings: BaudRate={scanPortSettings.BaudRate}, Timeout={scanPortSettings.Timeout} с");
        LogInfo($"{portName}: Проверка сигнатуры устройства...");

        System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: Создание экземпляра Hardware.Arduino");
        using var device = new Hardware.Arduino(scanPortSettings, portName);
        
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: Вызов device.Connect()");
            var connectStart = Stopwatch.StartNew();
            
            device.Connect();
            
            connectStart.Stop();
            System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: device.Connect() завершен за {connectStart.ElapsedMilliseconds} мс, IsConnected={device.IsConnected}");
            LogInfo($"{portName}: Проверка успешна.");

            System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: Получение FirmwareVersion");
            var firmwareStart = Stopwatch.StartNew();
            int firmware = device.FirmwareVersion;
            firmwareStart.Stop();
            string firmwareText = FormatFirmwareVersion(firmware);
            System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: FirmwareVersion получен за {firmwareStart.ElapsedMilliseconds} мс: {firmware} ({firmwareText})");

            System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: Получение Name");
            var nameStart = Stopwatch.StartNew();
            string name = device.Name;
            nameStart.Stop();
            System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: Name получен за {nameStart.ElapsedMilliseconds} мс: {name}");

            var info = new ArduinoDeviceInfo
            {
                Port = portName,
                FirmwareVersion = firmwareText,
                Name = name
            };
            
            System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: ArduinoDeviceInfo создан успешно");
            return info;
        }
        catch (TimeoutException ex)
        {
            // Таймаут - порт не содержит Arduino устройство, это нормально
            System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: TimeoutException - {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: Ошибка: {ex.GetType().Name} - {ex.Message}");
            LogWarn($"{portName}: Ошибка проверки ({ex.Message}).");
            return null;
        }
        finally
        {
            System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: finally блок - начало отключения");
            try
            {
                if (device.IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: Вызов device.Disconnect()");
                    var disconnectStart = Stopwatch.StartNew();
                    device.Disconnect();
                    disconnectStart.Stop();
                    System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: device.Disconnect() завершен за {disconnectStart.ElapsedMilliseconds} мс");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: Устройство не подключено, Disconnect() не вызывается");
                }
            }
            catch (Exception ex)
            {
                // Игнорируем ошибки при отключении
                System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: Ошибка при отключении (игнорируется): {ex.GetType().Name} - {ex.Message}");
            }
            System.Diagnostics.Debug.WriteLine($"[ProbePort] {portName}: Проверка порта завершена");
        }
    }

    /// <summary>
    /// Обрабатывает алерты от Arduino устройства.
    /// 
    /// АЛГОРИТМ ОБРАБОТКИ АЛЕРТОВ:
    /// ===========================
    /// 
    /// 1. ВАЛИДАЦИЯ АЛЕРТА:
    ///    - Проверяется, что устройство активно и sender совпадает с _activeDevice
    ///    - Если проверка не пройдена, обработка прекращается
    /// 
    /// 2. ОБРАБОТКА SlaveIncrement (обнаружена новая EEPROM):
    ///    - Устанавливается флаг _spdReady = true
    ///    - Вызывается событие SpdStateChanged
    ///    - Логируется сообщение об обнаружении EEPROM
    ///    - Запускается асинхронная обработка в отдельном потоке (Task.Run)
    /// 
    /// 3. СКАНИРОВАНИЕ I2C ШИНЫ (внутри lock для потокобезопасности):
    ///    a) Полное сканирование (ScanFull):
    ///       - Сканирует весь диапазон адресов I2C (0x08-0x77)
    ///       - Сохраняет результаты в _fullScanAddresses для отображения в UI
    ///       - Ошибки обрабатываются тихо (InvalidDataException, TimeoutException, InvalidOperationException)
    ///       - Это нормально при извлечении EEPROM во время сканирования
    /// 
    ///    b) Быстрое сканирование SPD адресов (Scan):
    ///       - Сканирует только диапазон SPD адресов (0x50-0x57)
    ///       - Обновляет _activeI2cAddress первым найденным адресом
    ///       - Ошибки обрабатываются тихо
    /// 
    /// 4. ОБНОВЛЕНИЕ UI:
    ///    - Вызывается OnStateChanged() для обновления интерфейса
    ///    - Это происходит синхронно после сканирования
    /// 
    /// 5. ФОНОВЫЕ ОПЕРАЦИИ (асинхронно, без блокировки):
    ///    - Запускается в отдельном Task.Run без await
    ///    - Определение типа памяти (RefreshMemoryTypeAsync):
    ///      * Вызывает DetectDdr5() и DetectDdr4() на Arduino
    ///      * Обновляет _memoryType (Ddr4, Ddr5, Unknown)
    ///      * Логирует "Обнаружена DDR4 SPD" или "Обнаружена DDR5 SPD"
    /// 
    ///    - Проверка RSWP (CheckRswpAsync):
    ///      * Определяет количество блоков на основе типа памяти
    ///      * Проверяет состояние защиты каждого блока через GetRswp(block)
    ///      * Логирует статус каждого блока ("Защищен" / "Не защищен")
    /// 
    ///    - Финальное обновление UI:
    ///      * Вызывается OnStateChanged() после всех операций
    /// 
    /// 6. ОБРАБОТКА ОШИБОК:
    ///    - InvalidDataException, TimeoutException, InvalidOperationException:
    ///      * Игнорируются тихо (это нормально при извлечении EEPROM)
    ///      * Отключение происходит только если !device.IsConnected
    /// 
    ///    - Другие исключения:
    ///      * Логируются как предупреждения
    ///      * Не вызывают отключение устройства
    /// 
    /// 7. ОБРАБОТКА SlaveDecrement (EEPROM извлечена):
    ///    - Устанавливается _spdReady = false
    ///    - Сбрасывается тип памяти в Unknown
    ///    - Очищается состояние RSWP
    ///    - Очищается _fullScanAddresses
    ///    - Вызывается OnStateChanged() для обновления UI
    /// 
    /// ВАЖНЫЕ ОСОБЕННОСТИ:
    /// ===================
    /// 
    /// - Каждый алерт обрабатывается независимо в отдельном потоке
    ///   (как в старом коде: new Thread(() => HandleAlert(...)).Start())
    /// 
    /// - Нет проверки _isHandlingAlert - алерты могут обрабатываться параллельно
    /// 
    /// - Нет CancellationToken - операции не прерываются при новом алерте
    /// 
    /// - Длительные операции (RefreshMemoryTypeAsync, CheckRswpAsync) выполняются
    ///   в фоне без блокировки, чтобы не вызывать зависания GUI
    /// 
    /// - Исключения при извлечении EEPROM обрабатываются тихо - это нормальное состояние
    /// 
    /// - Отключение происходит только при реальной потере связи с Arduino
    ///   (через HandleConnectionLost, который вызывается из ConnectionMonitor)
    /// </summary>
    /// <param name="sender">Источник события (Arduino устройство)</param>
    /// <param name="e">Аргументы события с кодом алерта</param>
    private void HandleAlert(object? sender, Hardware.Arduino.ArduinoAlertEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[HandleAlert] Начало обработки алерта {e.Code}");
        
        // ВАЛИДАЦИЯ: Проверяем, что устройство активно и sender совпадает
        if (_activeDevice == null || sender != _activeDevice)
        {
            System.Diagnostics.Debug.WriteLine("[HandleAlert] Выход - устройство null или sender не совпадает");
            return;
        }
        
        // ИГНОРИРУЕМ АЛЕРТЫ ВО ВРЕМЯ УСТАНОВКИ RSWP:
        // При установке RSWP для DDR4 используется высокое напряжение (HV), что может вызывать
        // временное отключение EEPROM на I2C шине. Это приводит к алертам SlaveDecrement/SlaveIncrement,
        // которые не должны прерывать операцию установки RSWP.
        if (_isSettingRswp)
        {
            System.Diagnostics.Debug.WriteLine($"[HandleAlert] {_activeDevice.PortName}: Алерт {e.Code} игнорируется - выполняется установка RSWP");
            return;
        }

        // ОБРАБОТКА SlaveIncrement: Обнаружена новая SPD EEPROM на I2C шине
        if (e.Code == Hardware.Arduino.AlertCodes.SlaveIncrement)
        {
            System.Diagnostics.Debug.WriteLine($"[HandleAlert] {_activeDevice.PortName}: SlaveIncrement получен");
            
            // ШАГ 1: Сохраняем ссылку на устройство локально для предотвращения race condition
            // Если _activeDevice изменится во время обработки, мы все равно будем работать с правильным устройством
            var device = _activeDevice;
            if (device == null)
            {
                System.Diagnostics.Debug.WriteLine("[HandleAlert] Выход - device null");
                return;
            }
            
            // ШАГ 2: Обновляем состояние SPD EEPROM
            // Устанавливаем флаг готовности и уведомляем подписчиков
            _spdReady = true;
            SpdStateChanged?.Invoke(this, _spdReady);
            LogInfo($"{device.PortName}: Обнаружена новая SPD EEPROM");
            
            // ШАГ 3: Запускаем обработку в отдельном потоке
            // В старом коде: new Thread(() => HandleAlert(...)).Start()
            // Каждый алерт обрабатывается независимо, без проверок и cancellation tokens
            // Это позволяет обрабатывать быстрые подключения/отключения EEPROM без зависаний
            _ = Task.Run(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Task.Run начал выполнение");
                    
                    // ШАГ 4: СКАНИРОВАНИЕ I2C ШИНЫ
                    // Выполняем в lock для потокобезопасности (как в старом коде)
                    // Lock защищает от одновременного доступа к устройству из разных потоков
                    // ВАЖНО: Операции внутри lock должны быть быстрыми, чтобы не блокировать другие потоки
                    lock (_lock)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Lock получен, начинаем сканирование");
                        
                        // ШАГ 4.1: Повторная проверка состояния устройства после получения lock
                        // Устройство могло отключиться или измениться пока мы ждали lock
                        if (device == null || !device.IsConnected || _activeDevice != device)
                        {
                            System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Выход из lock - устройство недоступно");
                            return;
                        }
                        
                        // Устанавливаем активный I2C адрес для операций
                        device.I2CAddress = _activeI2cAddress;
                        
                        // ШАГ 4.2: ПОЛНОЕ СКАНИРОВАНИЕ I2C ШИНЫ
                        // Сканирует весь диапазон адресов (0x08-0x77) для отображения всех устройств
                        // В старом коде этого не было, но нужно для отображения полного списка устройств
                        // ВАЖНО: ScanFull() может занять время (сканирует ~112 адресов), поэтому
                        // при ошибке нужно быстро выйти из lock, чтобы не блокировать другие потоки
                        byte[] fullAddresses = Array.Empty<byte>();
                        try
                        {
                            // Выполняем полное сканирование через команду PROBEADDRESS
                            // Это может занять некоторое время, но выполняется внутри lock
                            fullAddresses = device.ScanFull();
                            System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: ScanFull() завершен, найдено {fullAddresses.Length} адресов");
                            
                            // Проверяем состояние устройства после ScanFull()
                            // Если устройство отключилось во время сканирования, выходим
                            if (device == null || !device.IsConnected || _activeDevice != device)
                            {
                                System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Выход после ScanFull() - устройство недоступно");
                                return;
                            }
                            
                            // Логируем результаты для отладки и пользователя
                            if (fullAddresses.Length > 0)
                            {
                                var addressList = string.Join(", ", fullAddresses.Select(a => $"0x{a:X2}"));
                                LogInfo($"{device.PortName}: Полное сканирование I2C: найдено {fullAddresses.Length} устройств ({addressList})");
                            }
                        }
                        catch (Exception ex) when (ex is InvalidDataException || ex is TimeoutException || ex is InvalidOperationException)
                        {
                            // ОБРАБОТКА ОШИБОК СКАНИРОВАНИЯ:
                            // Эти ошибки могут возникать при извлечении EEPROM во время сканирования
                            // Это нормальное состояние - EEPROM может быть извлечена в любой момент
                            // Не логируем как ошибку и быстро выходим из lock, чтобы не блокировать другие потоки
                            System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: ScanFull() ошибка (игнорируется): {ex.GetType().Name}");
                            
                            // Проверяем состояние устройства - если отключилось, выходим
                            if (device == null || !device.IsConnected || _activeDevice != device)
                            {
                                System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Выход после ошибки ScanFull() - устройство недоступно");
                                return;
                            }
                            
                            // Продолжаем с пустым массивом, если устройство все еще подключено
                        }
                        
                        // Сохраняем результаты для отображения в UI
                        _fullScanAddresses = fullAddresses;
                        
                        // ШАГ 4.3: БЫСТРОЕ СКАНИРОВАНИЕ SPD АДРЕСОВ
                        // Сканирует только диапазон SPD адресов (0x50-0x57) через команду SCANBUS
                        // Это быстрее чем полное сканирование и используется для определения активного адреса
                        // В старом коде: _addresses = Scan()
                        try
                        {
                            // Проверяем состояние устройства перед Scan()
                            if (device == null || !device.IsConnected || _activeDevice != device)
                            {
                                System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Выход перед Scan() - устройство недоступно");
                                return;
                            }
                            
                            // Выполняем быстрое сканирование SPD адресов
                            var addresses = device.Scan();
                            System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Scan() завершен, найдено {addresses.Length} адресов");
                            
                            // Проверяем состояние устройства после Scan()
                            if (device == null || !device.IsConnected || _activeDevice != device)
                            {
                                System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Выход после Scan() - устройство недоступно");
                                return;
                            }
                            
                            // Обновляем активный I2C адрес первым найденным SPD адресом
                            if (addresses.Length > 0)
                            {
                                _activeI2cAddress = addresses[0];
                            }
                        }
                        catch (Exception ex) when (ex is InvalidDataException || ex is TimeoutException || ex is InvalidOperationException)
                        {
                            // ОБРАБОТКА ОШИБОК СКАНИРОВАНИЯ:
                            // Аналогично полному сканированию - ошибки при извлечении EEPROM нормальны
                            // Быстро выходим из lock, чтобы не блокировать другие потоки
                            System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Scan() ошибка (игнорируется): {ex.GetType().Name}");
                            
                            // Проверяем состояние устройства - если отключилось, выходим
                            if (device == null || !device.IsConnected || _activeDevice != device)
                            {
                                System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Выход после ошибки Scan() - устройство недоступно");
                                return;
                            }
                            
                            // Продолжаем без обновления _activeI2cAddress, если устройство все еще подключено
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Lock освобожден");
                    }
                    
                    // ШАГ 5: ОБНОВЛЕНИЕ UI ПОСЛЕ СКАНИРОВАНИЯ
                    // Обновляем интерфейс сразу после сканирования, чтобы пользователь видел результаты
                    if (device != null && device.IsConnected && _activeDevice == device)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Вызов OnStateChanged()");
                        OnStateChanged();
                        
                        // ШАГ 6: ФОНОВЫЕ ОПЕРАЦИИ (асинхронно, без блокировки)
                        // Запускаем определение типа памяти и проверку RSWP в фоне без await
                        // Это нужно для логирования "Обнаружена DDR4 SPD" и "Статус RSWP"
                        // Выполняем асинхронно, чтобы не блокировать обработку следующих алертов
                        // Если EEPROM будет извлечена во время выполнения, ошибки будут обработаны тихо
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                // ШАГ 6.1: ОПРЕДЕЛЕНИЕ ТИПА ПАМЯТИ
                                // Проверяем состояние устройства перед операцией
                                if (device != null && device.IsConnected && _activeDevice == device)
                                {
                                    // Вызываем RefreshMemoryTypeAsync():
                                    // - Выполняет DetectDdr5() и DetectDdr4() на Arduino
                                    // - Обновляет _memoryType (Ddr4, Ddr5, Unknown)
                                    // - Логирует "Обнаружена DDR4 SPD" или "Обнаружена DDR5 SPD"
                                    await RefreshMemoryTypeAsync().ConfigureAwait(false);
                                }
                                
                                // ШАГ 6.2: ПРОВЕРКА RSWP (Reversible Software Write Protection)
                                // Проверяем состояние устройства перед операцией
                                if (device != null && device.IsConnected && _activeDevice == device)
                                {
                                    // Вызываем CheckRswpAsync():
                                    // - Определяет количество блоков на основе типа памяти (DDR4: 4 блока, DDR5: 8 блоков)
                                    // - Проверяет состояние защиты каждого блока через GetRswp(block)
                                    // - Логирует статус каждого блока ("Защищен" / "Не защищен")
                                    await CheckRswpAsync().ConfigureAwait(false);
                                }
                                
                                // ШАГ 6.3: ФИНАЛЬНОЕ ОБНОВЛЕНИЕ UI
                                // Обновляем интерфейс после всех операций для отображения типа памяти и RSWP
                                if (device != null && device.IsConnected && _activeDevice == device)
                                {
                                    OnStateChanged();
                                }
                            }
                            catch (Exception ex) when (ex is TimeoutException || ex is InvalidOperationException || ex is InvalidDataException)
                            {
                                // ОБРАБОТКА ОШИБОК ФОНОВЫХ ОПЕРАЦИЙ:
                                // Эти ошибки могут возникать при извлечении EEPROM во время выполнения
                                // Это нормальное состояние - не логируем как ошибку
                                System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Фоновые операции ошибка (игнорируется): {ex.GetType().Name}");
                            }
                            catch (Exception ex)
                            {
                                // Неожиданные ошибки логируем для отладки, но не прерываем работу
                                System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Фоновые операции неожиданная ошибка: {ex.GetType().Name}");
                            }
                        });
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Task.Run завершен успешно");
                }
                catch (Exception ex) when (ex is TimeoutException || ex is InvalidOperationException || ex is InvalidDataException)
                {
                    // ШАГ 7: ОБРАБОТКА ОШИБОК СКАНИРОВАНИЯ
                    // Эти ошибки могут возникать при извлечении EEPROM во время сканирования
                    // Это нормальное состояние - EEPROM может быть извлечена в любой момент
                    // Не логируем как ошибку и не отключаемся, если устройство все еще подключено
                    System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Task.Run ошибка (игнорируется): {ex.GetType().Name}");
                    
                    // Отключаемся только если действительно потеряна связь с Arduino
                    // Проверяем !device.IsConnected, а не наличие исключения
                    if (device != null && !device.IsConnected)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device.PortName}: Устройство отключено, вызываем DisconnectInternal");
                        DisconnectInternal(false);
                    }
                }
                catch (Exception ex)
                {
                    // ШАГ 8: ОБРАБОТКА НЕОЖИДАННЫХ ОШИБОК
                    // Неожиданные ошибки логируем как предупреждения для отладки
                    // Но не вызываем отключение - могут быть из-за извлечения EEPROM
                    System.Diagnostics.Debug.WriteLine($"[HandleAlert] {device?.PortName ?? "Unknown"}: Task.Run неожиданная ошибка: {ex.GetType().Name} - {ex.Message}");
                    LogWarn($"{device?.PortName ?? "Unknown"}: Неожиданная ошибка при обработке алерта: {ex.Message}");
                }
            });
        }
        // ОБРАБОТКА SlaveDecrement: EEPROM извлечена с I2C шины
        else if (e.Code == Hardware.Arduino.AlertCodes.SlaveDecrement)
        {
            System.Diagnostics.Debug.WriteLine($"[HandleAlert] {_activeDevice.PortName}: SlaveDecrement получен");
            
            // ШАГ 9: ОБРАБОТКА SlaveDecrement (EEPROM извлечена)
            // Это нормальное состояние - EEPROM может быть извлечена в любой момент
            // Не вызываем отключение устройства - Arduino все еще подключен
            
            // Отменяем фоновые операции, если они выполняются
            // Это позволяет быстро остановить выполняющиеся операции определения типа памяти и проверки RSWP
            _alertOperationCancellation?.Cancel();
            
            // Сбрасываем флаг готовности SPD EEPROM
            _spdReady = false;
            SpdStateChanged?.Invoke(this, _spdReady);
            LogInfo($"{_activeDevice.PortName}: SPD EEPROM извлечена.");
            
            // Сбрасываем состояние типа памяти и RSWP
            // Тип памяти становится Unknown, так как EEPROM больше нет
            UpdateMemoryType(SpdMemoryType.Unknown);
            // Очищаем состояние RSWP (нет блоков для проверки)
            RswpStateChanged?.Invoke(this, Array.Empty<bool>());
            // Очищаем результаты полного сканирования
            _fullScanAddresses = null;
            
            // Обновляем UI для отображения изменений
            // Событие должно обрабатываться быстро обработчиками, чтобы не блокировать поток
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
            
            // Логируем потерю связи в обычный лог для пользователя
            LogWarn($"{device.PortName}: Соединение с Arduino потеряно. Устройство отключается...");
            
            // Это событие вызывается только при реальной потере связи с Arduino
            // (не при извлечении EEPROM)
            DisconnectInternal(false);
            
            System.Diagnostics.Debug.WriteLine($"[HandleConnectionLost] {device.PortName}: Отключение завершено.");
            
            // Логируем завершение отключения
            LogInfo($"{device.PortName}: Устройство отключено.");
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

