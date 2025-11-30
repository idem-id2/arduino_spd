using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace HexEditor.Arduino.Services
{
    /// <summary>
    /// Интерфейс для Arduino сервиса.
    /// Позволяет легко создавать mock'и для тестирования.
    /// </summary>
    internal interface IArduinoService
    {
        #region Properties
        
        ObservableCollection<ArduinoDeviceInfo> Devices { get; }
        ArduinoDeviceInfo? SelectedDevice { get; }
        bool IsScanning { get; }
        bool IsConnecting { get; }
        bool IsConnected { get; }
        bool IsReading { get; }
        bool IsSpdReady { get; }
        SpdMemoryType ActiveMemoryType { get; }
        int ActiveRswpBlockCount { get; }
        
        #endregion

        #region Methods
        
        void SetSelectedDevice(ArduinoDeviceInfo? device);
        Task ScanAsync();
        Task ConnectAsync();
        void Disconnect();
        Task<byte[]?> ReadSpdDumpAsync();
        Task<bool> WriteSpdDumpAsync(byte[] data, bool skipProtectedBlocks = false);
        Task<bool> SetDeviceNameAsync(string name);
        Task<bool[]> CheckRswpAsync();
        Task<bool> SetRswpAsync(byte block);
        Task SetMultipleRswpAsync(byte[] blocks);
        Task<bool> ClearRswpAsync();
        Arduino.Hardware.Arduino? GetActiveDevice();
        
        #endregion

        #region Events
        
        event EventHandler<ArduinoLogEventArgs>? LogGenerated;
        event EventHandler<bool>? ConnectionStateChanged;
        event EventHandler<bool>? SpdStateChanged;
        event EventHandler<ArduinoConnectionInfo>? ConnectionInfoChanged;
        event EventHandler<bool[]>? RswpStateChanged;
        event EventHandler<SpdMemoryType>? MemoryTypeChanged;
        event EventHandler? StateChanged;
        
        #endregion
    }
}

