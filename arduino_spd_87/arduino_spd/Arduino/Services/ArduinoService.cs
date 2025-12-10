using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
    private static readonly TimeSpan PortProbeTimeout = TimeSpan.FromSeconds(5);

    private Hardware.Arduino? _activeDevice;
    private byte _activeI2cAddress = 0x50;
    private bool _spdReady;
    private SpdMemoryType _memoryType = SpdMemoryType.Unknown;

    public ObservableCollection<ArduinoDeviceInfo> Devices { get; } = new();

    public ArduinoDeviceInfo? SelectedDevice { get; private set; }

    public bool IsScanning { get; private set; }
    public bool IsConnecting { get; private set; }
    public bool IsConnected => _activeDevice?.IsConnected ?? false;

    public Hardware.Arduino? GetActiveDevice() => _activeDevice;
    public bool IsReading { get; private set; }
    public bool IsSpdReady => _spdReady;
    public SpdMemoryType ActiveMemoryType => _memoryType;
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

        try
        {
            var data = await Task.Run(() =>
            {
                lock (_lock)
                {
                    _activeDevice.I2CAddress = _activeI2cAddress;
                    return _activeDevice.ReadSpdDump();
                }
            }).ConfigureAwait(true);

            LogInfo($"{_activeDevice.PortName}: Дамп SPD загружен ({data.Length} байт).");
            
            // Валидация размера данных в зависимости от типа памяти
            int expectedSize = _memoryType switch
            {
                SpdMemoryType.Ddr4 => 512,
                SpdMemoryType.Ddr5 => 1024,
                _ => 256 // DDR3 и ниже
            };
            
            if (data.Length != expectedSize)
            {
                LogError($"{_activeDevice.PortName}: Неверный размер SPD: ожидается {expectedSize} байт для {_memoryType}, получено {data.Length} байт.");
                return null;
            }
            
            // Проверяем RSWP после чтения
            await CheckRswpAsync().ConfigureAwait(true);
            
            return data;
        }
        catch (Exception ex)
        {
            LogError($"{_activeDevice.PortName}: Ошибка чтения SPD ({ex.Message})");
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
        
        if (data.Length != expectedSize)
        {
            LogError($"{_activeDevice.PortName}: Неверный размер SPD для записи: ожидается {expectedSize} байт для {_memoryType}, получено {data.Length} байт.");
            return false;
        }

        try
        {
            var result = await Task.Run(() =>
            {
                lock (_lock)
                {
                    _activeDevice.I2CAddress = _activeI2cAddress;
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
                        bool writeResult = _activeDevice.UpdateSpd(i, b);
                        
                        if (!writeResult)
                        {
                            LogError($"{_activeDevice.PortName}: Не удалось записать байт {i} в EEPROM по адресу {_activeDevice.I2CAddress} на порту {_activeDevice.PortName}.");
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

                    LogInfo($"{_activeDevice.PortName}: Записано {bytesWritten} байт в SPD.");
                    return true;
                }
            }).ConfigureAwait(true);

            return result;
        }
        catch (Exception ex)
        {
            LogError($"{_activeDevice.PortName}: Ошибка записи SPD ({ex.Message})");
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

        try
        {
            var result = await Task.Run(() =>
            {
                lock (_lock)
                {
                    return _activeDevice.SetName(name);
                }
            }).ConfigureAwait(true);

            if (result)
            {
                LogInfo($"{_activeDevice.PortName}: Имя устройства установлено на '{name}'.");
                // Обновляем информацию о подключении
                await RefreshConnectionInfoAsync().ConfigureAwait(true);
            }
            else
            {
                LogWarn($"{_activeDevice.PortName}: Имя устройства уже '{name}'.");
            }

            return result;
        }
        catch (Exception ex)
        {
            LogError($"{_activeDevice.PortName}: Не удалось установить имя устройства ({ex.Message})");
            return false;
        }
    }

    private async Task RefreshConnectionInfoAsync()
    {
        if (!IsConnected || _activeDevice == null)
        {
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    string name = _activeDevice.Name;
                    int firmware = _activeDevice.FirmwareVersion;
                    string firmwareText = FormatFirmwareVersion(firmware);
                    ushort clock = _activeDevice.I2CClock;
                    string clockText = clock == Hardware.Arduino.ClockMode.Fast ? "400 kHz" : "100 kHz";
                    byte rswp = _activeDevice.RswpTypeSupport;
                    bool ddr4Rswp = (rswp & Hardware.Arduino.RswpSupport.DDR4) != 0;
                    string ddr4Text = ddr4Rswp ? "Да" : "Нет";

                    ConnectionInfoChanged?.Invoke(this, new ArduinoConnectionInfo(
                        _activeDevice.PortName,
                        firmwareText,
                        name,
                        clockText,
                        ddr4Text));
                }
            }).ConfigureAwait(true);
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

        int blockCount = ActiveRswpBlockCount;
        if (blockCount == 0)
        {
            await RefreshMemoryTypeAsync().ConfigureAwait(true);
            blockCount = ActiveRswpBlockCount;
            if (blockCount == 0)
            {
                LogWarn($"{_activeDevice.PortName}: Не удалось определить тип памяти SPD. Статус RSWP недоступен.");
                return Array.Empty<bool>();
            }
        }

        try
        {
            var states = await Task.Run(() =>
            {
                lock (_lock)
                {
                    _activeDevice.I2CAddress = _activeI2cAddress;
                    bool[] blockStates = new bool[blockCount];
                    for (byte block = 0; block < blockCount; block++)
                    {
                        try
                        {
                            blockStates[block] = _activeDevice.GetRswp(block);
                        }
                        catch
                        {
                            // Если блок не поддерживается, считаем его незащищенным
                            blockStates[block] = false;
                        }
                    }
                    return blockStates;
                }
            }).ConfigureAwait(true);

            RswpStateChanged?.Invoke(this, states);
            
            // Построчный вывод состояния всех блоков
            LogInfo($"{_activeDevice.PortName}: Статус RSWP:");
            for (int i = 0; i < states.Length; i++)
            {
                LogInfo($"{_activeDevice.PortName}: Блок {i} [{(states[i] ? "Защищен" : "Не защищен")}]");
            }
            
            return states;
        }
        catch (Exception ex)
        {
            LogError($"{_activeDevice.PortName}: Ошибка проверки RSWP ({ex.Message})");
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

        try
        {
            var result = await Task.Run(() =>
            {
                lock (_lock)
                {
                    _activeDevice.I2CAddress = _activeI2cAddress;
                    return _activeDevice.SetRswp(block);
                }
            }).ConfigureAwait(true);

            if (result)
            {
                LogInfo($"{_activeDevice.PortName}: RSWP успешно установлен для блока {block}.");
                // ПРИМЕЧАНИЕ: Проверка состояния (CheckRswpAsync) вызывается в MainWindow после установки ВСЕХ блоков
                // Это избегает проблем с недоступностью SPD после HV операций
            }
            else
            {
                LogWarn($"{_activeDevice.PortName}: ⚠️ Не удалось установить RSWP для блока {block}.");
            }

            return result;
        }
        catch (Exception ex)
        {
            LogError($"{_activeDevice.PortName}: Ошибка установки RSWP ({ex.Message})");
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

        try
        {
            var result = await Task.Run(() =>
            {
                lock (_lock)
                {
                    _activeDevice.I2CAddress = _activeI2cAddress;
                    return _activeDevice.ClearRswp();
                }
            }).ConfigureAwait(true);

            if (result)
            {
                LogInfo($"{_activeDevice.PortName}: RSWP очищен для всех блоков.");
                // ПРИМЕЧАНИЕ: Проверка состояния (CheckRswpAsync) выполнится автоматически
                // после переинициализации SPD (событие SlaveIncrement)
            }
            else
            {
                LogWarn($"{_activeDevice.PortName}: ⚠️ Не удалось очистить RSWP.");
            }

            return result;
        }
        catch (Exception ex)
        {
            LogError($"{_activeDevice.PortName}: Ошибка очистки RSWP ({ex.Message})");
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

        try
        {
            var detectedType = await Task.Run(() =>
            {
                lock (_lock)
                {
                    _activeDevice.I2CAddress = _activeI2cAddress;
                    if (_activeDevice.DetectDdr5())
                    {
                        return SpdMemoryType.Ddr5;
                    }

                    if (_activeDevice.DetectDdr4())
                    {
                        return SpdMemoryType.Ddr4;
                    }

                    return SpdMemoryType.Unknown;
                }
            }).ConfigureAwait(true);

            UpdateMemoryType(detectedType);
        }
        catch (Exception ex)
        {
            LogWarn($"{_activeDevice?.PortName}: Memory type detection failed ({ex.Message}).");
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
            _spdReady = true;
            SpdStateChanged?.Invoke(this, _spdReady);
            LogInfo($"{_activeDevice.PortName}: Обнаружена новая SPD EEPROM");
            // Обновляем тип памяти и RSWP при обнаружении нового SPD
            _ = Task.Run(async () =>
            {
                await RefreshMemoryTypeAsync().ConfigureAwait(false);
                await CheckRswpAsync().ConfigureAwait(false);
            });
        }
        else if (e.Code == Hardware.Arduino.AlertCodes.SlaveDecrement)
        {
            _spdReady = false;
            SpdStateChanged?.Invoke(this, _spdReady);
            LogInfo($"{_activeDevice.PortName}: SPD EEPROM извлечена.");
            UpdateMemoryType(SpdMemoryType.Unknown);
            // Очищаем состояние RSWP при удалении SPD
            RswpStateChanged?.Invoke(this, Array.Empty<bool>());
        }
    }

        private void HandleConnectionLost(object? sender, EventArgs e)
        {
            if (_activeDevice == null || sender != _activeDevice)
            {
                return;
            }

            LogWarn($"{_activeDevice.PortName}: Соединение потеряно.");
            DisconnectInternal(false);
        }

    private void DisconnectInternal(bool logDisconnect)
    {
        if (_activeDevice == null)
        {
            return;
        }

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

