using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace HexEditor.Arduino.Hardware;

/// <summary>
/// Minimal Arduino client extracted from the reference project.
/// Supports the subset of commands required by the HexEditor UI.
/// </summary>
internal sealed class Arduino : IDisposable
{
    private SerialPort? _serialPort;
    private PacketData _response = new PacketData();
    private byte[]? _inputBuffer;
    private int _bytesReceived;
    private int _bytesSent;
    private readonly object _portLock = new();
    private readonly object _receiveLock = new();
        private byte[]? _cachedAddresses;
        private int _rswpTypeSupport = -1;
    private CancellationTokenSource? _connectionMonitorCts;
    private Task? _connectionMonitorTask;
    private volatile bool _connectionLostWasRaised;

    private static readonly AutoResetEvent DataReady = new(false);

        public event EventHandler<ArduinoAlertEventArgs>? AlertReceived;
        public event EventHandler? ConnectionLost;

    public Arduino(SerialPortSettings portSettings, string portName)
    {
        PortSettings = portSettings;
        PortName = portName ?? throw new ArgumentNullException(nameof(portName));
    }

    public SerialPortSettings PortSettings { get; }

    public string PortName { get; }

    public bool IsConnected
    {
        get
        {
            try
            {
                return _serialPort?.IsOpen == true;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool Connect()
    {
        lock (_portLock)
        {
            if (IsConnected)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(PortName))
            {
                throw new InvalidOperationException("Port name must be specified.");
            }

            _serialPort = new SerialPort
            {
                PortName = PortName,
                BaudRate = PortSettings.BaudRate,
                DtrEnable = PortSettings.DtrEnable,
                RtsEnable = PortSettings.RtsEnable,
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                ReceivedBytesThreshold = PacketData.MinSize,
            };

            _serialPort.DataReceived += DataReceivedHandler;
            _serialPort.ErrorReceived += ErrorReceivedHandler;

            try
            {
                _serialPort.Open();
                _bytesReceived = 0;
                _bytesSent = 0;
                _connectionLostWasRaised = false;

                ExecuteCommand<bool>(Command.TEST);
                StartConnectionMonitor();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        return true;
    }

    public bool Disconnect()
    {
        lock (_portLock)
        {
            if (!IsConnected)
            {
                return true;
            }

            try
            {
                StopConnectionMonitor();
                _serialPort!.DataReceived -= DataReceivedHandler;
                _serialPort.ErrorReceived -= ErrorReceivedHandler;
                _serialPort.Close();
                OnConnectionLost();
            }
            finally
            {
                _serialPort = null;
                _cachedAddresses = null;
                _rswpTypeSupport = -1;
            }
        }

        return true;
    }

    public void Dispose()
    {
        Disconnect();
    }

    public string Name => GetName();

    public byte I2CAddress { get; set; } = 0x50;

    public int FirmwareVersion => GetFirmwareVersion();

    public ushort I2CClock => GetI2CClock() ? ClockMode.Fast : ClockMode.Standard;

    public byte RswpTypeSupport
    {
        get
        {
            if (_rswpTypeSupport == -1)
            {
                _rswpTypeSupport = GetRswpSupport();
            }

            return (byte)_rswpTypeSupport;
        }
    }

    public byte[] Scan()
    {
        lock (_portLock)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Device is not connected.");
            }

            if (_cachedAddresses != null)
            {
                return _cachedAddresses;
            }

            byte mask = ExecuteCommand<byte>(Command.SCANBUS);
            List<byte> addresses = new();

            for (byte i = 0; i <= 7; i++)
            {
                if (ProtocolHelpers.GetBit(mask, i))
                {
                    addresses.Add((byte)(0x50 + i));
                }
            }

            _cachedAddresses = addresses.ToArray();
            return _cachedAddresses;
        }
    }

    /// <summary>
    /// Сканирует всю I2C шину (адреса 0x08-0x77) используя команду PROBEADDRESS
    /// </summary>
    public byte[] ScanFull()
    {
        lock (_portLock)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Device is not connected.");
            }

            List<byte> addresses = new();

            // Сканируем весь диапазон I2C адресов (0x08-0x77)
            // Пропускаем зарезервированные адреса 0x00-0x07
            for (byte address = 0x08; address <= 0x77; address++)
            {
                try
                {
                    // Используем команду PROBEADDRESS для проверки каждого адреса
                    bool found = ExecuteCommand<bool>(Command.PROBEADDRESS, address);
                    if (found)
                    {
                        addresses.Add(address);
                    }
                }
                catch
                {
                    // Игнорируем ошибки при проверке отдельных адресов
                    // и продолжаем сканирование
                }
            }

            return addresses.ToArray();
        }
    }

    public byte ReadSpd(ushort offset)
    {
        return ExecuteCommand<byte>(
            new byte[]
            {
                Command.READBYTE,
                I2CAddress,
                (byte)(offset >> 8),
                (byte)offset,
                1
            });
    }

    public byte[] ReadSpd(ushort offset, byte count)
    {
        if (count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        return ExecuteCommand<byte[]>(
            new[]
            {
                Command.READBYTE,
                I2CAddress,
                (byte)(offset >> 8),
                (byte)offset,
                count
            });
    }

    public byte[] ReadSpdDump(int length = 512)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        // Читаем по 32 байта, начиная с offset=0, точно как в старом коде arduino_spd_87
        List<byte> result = new(length);
        int offset = 0;

        while (offset < length)
        {
            byte chunk = (byte)Math.Min(32, length - offset);
            var portion = ReadSpd((ushort)offset, chunk);

            if (portion.Length == 0)
            {
                break;
            }

            result.AddRange(portion);
            offset += portion.Length;

            if (portion.Length < chunk)
            {
                break;
            }
        }

        return result.ToArray();
    }

    public bool WriteSpd(ushort offset, byte value)
    {
        lock (_portLock)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Device is not connected.");
            }

            try
            {
                byte[] command = new[]
                {
                    Command.WRITEBYTE,
                    I2CAddress,
                    (byte)(offset >> 8),
                    (byte)offset,
                    value
                };
                
                byte[] response = ExecuteCommand(command);
                
                if (response == null || response.Length == 0)
                {
                    return false;
                }
                
                return response[0] == Response.TRUE;
            }
            catch (TimeoutException ex)
            {
                System.Diagnostics.Debug.WriteLine($"WriteSpd timeout at offset 0x{offset:X4}: {ex.Message}");
                return false;
            }
            catch (InvalidDataException ex)
            {
                System.Diagnostics.Debug.WriteLine($"WriteSpd invalid data at offset 0x{offset:X4}: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WriteSpd exception at offset 0x{offset:X4}: {ex.Message}");
                return false;
            }
        }
    }

    public bool UpdateSpd(ushort offset, byte value)
    {
        // Проверяем текущее значение перед записью (как в исходном коде)
        // Используем оптимизированную перегрузку ReadSpd для одного байта
        try
        {
            byte currentValue = ReadSpd(offset);
            if (currentValue == value)
            {
                // Значение уже совпадает, запись не нужна
                return true;
            }
        }
        catch
        {
            // Если не удалось прочитать, продолжаем с записью
        }

        // Записываем новое значение
        return WriteSpd(offset, value);
    }

    public bool WriteSpdPage(ushort offset, byte[] data)
    {
        if (data == null || data.Length == 0 || data.Length > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(data), "Page size must be between 1 and 32 bytes");
        }

        lock (_portLock)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Device is not connected.");
            }

            byte[] command = new byte[5 + data.Length];
            command[0] = Command.WRITEPAGE;
            command[1] = I2CAddress;
            command[2] = (byte)(offset >> 8);
            command[3] = (byte)offset;
            command[4] = (byte)data.Length;
            Array.Copy(data, 0, command, 5, data.Length);

            return ExecuteCommand<bool>(command);
        }
    }

    private int GetFirmwareVersion()
    {
        lock (_portLock)
        {
            return ExecuteCommand<int>(Command.VERSION);
        }
    }

    private string GetName()
    {
        lock (_portLock)
        {
            return ExecuteCommand<string>(Command.NAME, Command.GET).Trim();
        }
    }

    public bool SetName(string name)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        if (name.Length == 0)
        {
            throw new ArgumentException("Name can't be blank");
        }
        if (name.Length > Command.NAMELENGTH)
        {
            throw new ArgumentOutOfRangeException(nameof(name), $"Name can't be longer than {Command.NAMELENGTH} characters");
        }

        lock (_portLock)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Device is not connected.");
            }

            try
            {
                string newName = name.Trim();

                if (newName == GetName())
                {
                    return false;
                }

                // Prepare a byte array containing cmd byte + name length + name
                byte[] command = new byte[2 + newName.Length];
                command[0] = Command.NAME;
                command[1] = (byte)newName.Length;
                System.Text.Encoding.ASCII.GetBytes(newName, 0, newName.Length, command, 2);

                return ExecuteCommand<bool>(command);
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to assign name to {PortName}: {ex.Message}");
            }
        }
    }

    private bool GetI2CClock()
    {
        lock (_portLock)
        {
            return ExecuteCommand<bool>(Command.I2CCLOCK, Command.GET);
        }
    }

    private byte GetRswpSupport()
    {
        lock (_portLock)
        {
            return ExecuteCommand<byte>(Command.RSWPREPORT);
        }
    }

    public bool GetRswp(byte block)
    {
        lock (_portLock)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Device is not connected.");
            }

            return ExecuteCommand<bool>(
                new[]
                {
                    Command.RSWP,
                    I2CAddress,
                    block,
                    Command.GET
                });
        }
    }

    public bool SetRswp(byte block)
    {
        lock (_portLock)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Device is not connected.");
            }

            return ExecuteCommand<bool>(
                new[]
                {
                    Command.RSWP,
                    I2CAddress,
                    block,
                    (byte)1
                });
        }
    }

    public bool ClearRswp()
    {
        lock (_portLock)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Device is not connected.");
            }

            return ExecuteCommand<bool>(
                new[]
                {
                    Command.RSWP,
                    I2CAddress,
                    (byte)0,
                    (byte)0
                });
        }
    }

    public bool DetectDdr4()
    {
        lock (_portLock)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Device is not connected.");
            }

            return ExecuteCommand<bool>(Command.DDR4DETECT, I2CAddress);
        }
    }

    public bool DetectDdr5()
    {
        lock (_portLock)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Device is not connected.");
            }

            return ExecuteCommand<bool>(Command.DDR5DETECT, I2CAddress);
        }
    }

    /// <summary>
    /// Читает регистр термодатчика по I2C адресу
    /// По стандарту JC-42.4 регистры 6 и 7 - это 16-битные значения (2 байта)
    /// </summary>
    /// <param name="sensorAddress">I2C адрес термодатчика (например, 0x18-0x1F)</param>
    /// <param name="register">Адрес регистра (обычно 6 или 7)</param>
    /// <returns>16-битное значение регистра или null при ошибке</returns>
    public ushort? ReadSensorRegister(byte sensorAddress, byte register)
    {
        lock (_portLock)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Device is not connected.");
            }

            try
            {
                var result = ExecuteCommand<byte[]>(
                    new[]
                    {
                        Command.READSENSORREG,
                        sensorAddress,
                        register
                    });
                
                // Проверяем, что получили 2 байта
                if (result == null || result.Length < 2)
                {
                    return null;
                }
                
                // Объединяем байты в 16-битное значение (big-endian: MSB первый, LSB второй)
                // По стандарту JC-42.4 регистры читаются в формате big-endian
                ushort value = (ushort)((result[0] << 8) | result[1]);
                return value;
            }
            catch
            {
                return null;
            }
        }
    }

    private void ClearBuffer()
    {
        if (_serialPort == null)
        {
            return;
        }

        if (_serialPort.BytesToRead > 0)
        {
            _serialPort.DiscardInBuffer();
        }
        if (_serialPort.BytesToWrite > 0)
        {
            _serialPort.DiscardOutBuffer();
        }
    }

    private void DataReceivedHandler(object? sender, SerialDataReceivedEventArgs e)
    {
        lock (_receiveLock)
        {
            if (sender != _serialPort || !IsConnected)
            {
                return;
            }

            while (_serialPort!.BytesToRead < _serialPort.ReceivedBytesThreshold)
            {
                Thread.Sleep(5);
            }

            _inputBuffer = new byte[PacketData.MaxSize];
            _bytesReceived += _serialPort.Read(_inputBuffer, 0, _serialPort.ReceivedBytesThreshold);

            switch (_inputBuffer[0])
            {
                case Header.ALERT:
                    byte alertCode = _inputBuffer[1];
                    OnAlertReceived(alertCode);
                    break;

                case Header.RESPONSE:
                    while (_serialPort.BytesToRead < _inputBuffer[1] + 1)
                    {
                        Thread.Sleep(1);
                    }
                    _bytesReceived += _serialPort.Read(_inputBuffer, PacketData.MinSize, _inputBuffer[1] + 1);
                    _response.RawBytes = _inputBuffer;
                    break;
            }

            _inputBuffer = null;
        }
    }

        private static void ErrorReceivedHandler(object? sender, SerialErrorReceivedEventArgs e)
    {
        if (sender is SerialPort port)
        {
            throw new IOException($"Error received on {port.PortName}: {e.EventType}");
        }
    }

        private void OnAlertReceived(byte alertCode)
        {
            AlertReceived?.Invoke(this, new ArduinoAlertEventArgs(alertCode));
        }

        private void OnConnectionLost()
        {
            if (_connectionLostWasRaised)
            {
                return;
            }

            _connectionLostWasRaised = true;
            ConnectionLost?.Invoke(this, EventArgs.Empty);
            StopConnectionMonitor();
        }

        private void StartConnectionMonitor()
        {
            StopConnectionMonitor();

            _connectionMonitorCts = new CancellationTokenSource();
            var token = _connectionMonitorCts.Token;

            _connectionMonitorTask = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        await Task.Delay(500, token).ConfigureAwait(false);

                        if (!IsConnected)
                        {
                            OnConnectionLost();
                            break;
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // ignore
                }
            }, token);
        }

        private void StopConnectionMonitor()
        {
            try
            {
                _connectionMonitorCts?.Cancel();
                _connectionMonitorTask?.Wait(1000);
            }
            catch
            {
                // ignored
            }
            finally
            {
                _connectionMonitorCts?.Dispose();
                _connectionMonitorCts = null;
                _connectionMonitorTask = null;
            }
        }

    #region Command execution helpers

    public T ExecuteCommand<T>(byte command) => ExecuteCommand<T>(new[] { command });

    public T ExecuteCommand<T>(byte command, byte parameter) => ExecuteCommand<T>(new[] { command, parameter });

    public T ExecuteCommand<T>(byte[] command)
    {
        byte[] response = ExecuteCommand(command);

        if (typeof(T).IsArray)
        {
            if (typeof(T).GetElementType() == typeof(byte))
            {
                return (T)(object)response;
            }

            throw new NotSupportedException($"Array type {typeof(T).Name} is not supported.");
        }

        if (typeof(T) == typeof(bool))
        {
            return (T)(object)(response[0] == Response.TRUE);
        }

        if (typeof(T) == typeof(byte))
        {
            return (T)(object)response[0];
        }

        if (typeof(T) == typeof(int))
        {
            return (T)(object)BitConverter.ToInt32(ProtocolHelpers.Slice(response, 0, 4));
        }

        if (typeof(T) == typeof(ushort))
        {
            return (T)(object)BitConverter.ToUInt16(ProtocolHelpers.Slice(response, 0, 2));
        }

        if (typeof(T) == typeof(string))
        {
            return (T)(object)ProtocolHelpers.BytesToString(response);
        }

        throw new NotSupportedException($"Type {typeof(T).Name} is not supported.");
    }

    private byte[] ExecuteCommand(byte[] command)
    {
        if (!IsConnected || _serialPort == null)
        {
            throw new InvalidOperationException("Device is not connected.");
        }

        lock (_portLock)
        {
            try
            {
                ClearBuffer();
                _serialPort.BaseStream.Write(command, 0, command.Length);
                _serialPort.BaseStream.Flush();
                _bytesSent += command.Length;

                if (!DataReady.WaitOne(TimeSpan.FromSeconds(PortSettings.Timeout)))
                {
                    throw new TimeoutException($"{PortName} response timeout");
                }

                if (_response.Type != Header.RESPONSE)
                {
                    throw new InvalidDataException("Invalid response header");
                }

                if (!_response.IsChecksumValid)
                {
                    throw new InvalidDataException("Checksum validation failed");
                }

                return _response.Body;
            }
            catch (Exception)
            {
                // В старом коде ExecuteCommand НЕ вызывал OnConnectionLost при исключениях
                // OnConnectionLost вызывается только из ConnectionMonitor при реальной потере связи
                // Просто пробрасываем исключение дальше
                throw;
            }
            finally
            {
                _response = new PacketData();
                DataReady.Reset();
            }
        }
    }

    #endregion

    #region Nested types

    public struct SerialPortSettings
    {
        public SerialPortSettings(int baudRate = 115200, bool dtrEnable = true, bool rtsEnable = true, int timeout = 10)
        {
            BaudRate = baudRate;
            DtrEnable = dtrEnable;
            RtsEnable = rtsEnable;
            Timeout = timeout;
        }

        public int BaudRate { get; }
        public bool DtrEnable { get; }
        public bool RtsEnable { get; }
        public int Timeout { get; }
    }

    private struct PacketData
    {
        public const int MaxSize = 1 + 1 + 32 + 1;
        public const int MinSize = 1 + 1;

        private byte[]? _rawBytes;

        public byte[] RawBytes
        {
            get => _rawBytes ?? Array.Empty<byte>();
            set
            {
                if (value.Length < MinSize || value.Length > MaxSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _rawBytes = value;
                DataReady.Set();
            }
        }

        public byte Type => _rawBytes?[0] ?? 0;

        public byte Length => _rawBytes?[1] ?? 0;

        public byte[] Body => _rawBytes == null ? Array.Empty<byte>() : ProtocolHelpers.Slice(_rawBytes, MinSize, Length);

        public bool IsChecksumValid
        {
            get
            {
                if (_rawBytes == null)
                {
                    return false;
                }

                byte checksum = _rawBytes[Length + MinSize];
                return ProtocolHelpers.CalculateChecksum(Body) == checksum;
            }
        }
    }

        public struct Response
    {
        public const byte TRUE = 0x01;
    }

    public struct Header
    {
        public const byte RESPONSE = (byte)'&';
        public const byte ALERT = (byte)'@';
    }

        public struct Command
    {
        public const byte TEST = (byte)'t';
        public const byte VERSION = (byte)'v';
        public const byte NAME = (byte)'n';
        public const byte NAMELENGTH = 16;
        public const byte GET = (byte)'?';
        public const byte I2CCLOCK = (byte)'c';
        public const byte RSWPREPORT = (byte)'f';
        public const byte SCANBUS = (byte)'s';
        public const byte READBYTE = (byte)'r';
        public const byte WRITEBYTE = (byte)'w';
        public const byte WRITEPAGE = (byte)'g';
        public const byte RSWP = (byte)'b';
        public const byte DDR4DETECT = (byte)'4';
        public const byte DDR5DETECT = (byte)'5';
        public const byte PROBEADDRESS = (byte)'a';
        public const byte READSENSORREG = (byte)'R';
    }

    public struct RswpSupport
    {
        public const byte DDR3 = 1 << 3;
        public const byte DDR4 = 1 << 4;
        public const byte DDR5 = 1 << 5;
    }

        public struct ClockMode
    {
        public const ushort Fast = 400;
        public const ushort Standard = 100;
    }

        public struct AlertCodes
        {
            public const byte SlaveIncrement = (byte)'+';
            public const byte SlaveDecrement = (byte)'-';
        }

        public sealed class ArduinoAlertEventArgs : EventArgs
        {
            public ArduinoAlertEventArgs(byte code)
            {
                Code = code;
            }

            public byte Code { get; }
        }

    private static class ProtocolHelpers
    {
        public static byte[] Slice(byte[] source, int index, int length)
        {
            byte[] buffer = new byte[length];
            Buffer.BlockCopy(source, index, buffer, 0, length);
            return buffer;
        }

        public static byte CalculateChecksum(byte[] data)
        {
            byte checksum = 0;

            foreach (byte b in data)
            {
                checksum += b;
            }

            return checksum;
        }

        public static bool GetBit(byte value, int position) => ((value >> position) & 1) == 1;

        public static string BytesToString(byte[] bytes)
        {
            return Encoding.ASCII.GetString(bytes).TrimEnd('\0');
        }
    }

    #endregion
}

