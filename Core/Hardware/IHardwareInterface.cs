using System;
using System.Threading;
using System.Threading.Tasks;

namespace HB_NLP_Research_Lab.Core.Hardware
{
    /// <summary>
    /// Base interface for hardware communication protocols
    /// (CAN, Modbus, Serial, Ethernet, etc.)
    /// </summary>
    public interface IHardwareInterface : IDisposable
    {
        /// <summary>
        /// Interface name/identifier
        /// </summary>
        string InterfaceName { get; }
        
        /// <summary>
        /// Communication protocol type
        /// </summary>
        ProtocolType Protocol { get; }
        
        /// <summary>
        /// Initialize the hardware interface
        /// </summary>
        Task<bool> InitializeAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Connect to hardware
        /// </summary>
        Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Disconnect from hardware
        /// </summary>
        Task DisconnectAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Is interface currently connected
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// Interface status
        /// </summary>
        InterfaceStatus Status { get; }
        
        /// <summary>
        /// Read data from hardware
        /// </summary>
        Task<byte[]> ReadAsync(int bytesToRead, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Write data to hardware
        /// </summary>
        Task<bool> WriteAsync(byte[] data, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Event fired when connection state changes
        /// </summary>
        event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;
        
        /// <summary>
        /// Event fired when data is received
        /// </summary>
        event EventHandler<DataReceivedEventArgs> DataReceived;
    }
    
    public enum ProtocolType
    {
        CAN,
        Modbus,
        Serial,
        Ethernet,
        SPI,
        I2C,
        GPIO,
        Other
    }
    
    public enum InterfaceStatus
    {
        Unknown,
        Initializing,
        Ready,
        Connected,
        Disconnected,
        Error,
        Fault
    }
    
    public class ConnectionStateChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public DateTime Timestamp { get; set; }
        public string? ErrorMessage { get; set; }
    }
    
    public class DataReceivedEventArgs : EventArgs
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public DateTime Timestamp { get; set; }
    }
}
