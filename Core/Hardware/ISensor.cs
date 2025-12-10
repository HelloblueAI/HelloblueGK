using System;
using System.Threading;
using System.Threading.Tasks;

namespace HB_NLP_Research_Lab.Core.Hardware
{
    /// <summary>
    /// Interface for hardware sensors
    /// Supports pressure, temperature, flow, position, etc.
    /// </summary>
    public interface ISensor<T> where T : struct
    {
        /// <summary>
        /// Unique identifier for this sensor
        /// </summary>
        string SensorId { get; }
        
        /// <summary>
        /// Human-readable name/description
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Sensor type (Pressure, Temperature, Flow, etc.)
        /// </summary>
        SensorType Type { get; }
        
        /// <summary>
        /// Read current sensor value
        /// </summary>
        Task<T> ReadAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validate sensor is functioning correctly
        /// </summary>
        Task<bool> ValidateAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Current sensor status
        /// </summary>
        SensorStatus Status { get; }
        
        /// <summary>
        /// Minimum valid reading
        /// </summary>
        T MinValue { get; }
        
        /// <summary>
        /// Maximum valid reading
        /// </summary>
        T MaxValue { get; }
        
        /// <summary>
        /// Sensor reading frequency (Hz)
        /// </summary>
        int MaxFrequencyHz { get; }
        
        /// <summary>
        /// Last reading timestamp
        /// </summary>
        DateTime LastReadingTime { get; }
        
        /// <summary>
        /// Event fired when sensor reading changes significantly
        /// </summary>
        event EventHandler<SensorReadingChangedEventArgs<T>> ReadingChanged;
    }
    
    public enum SensorType
    {
        Pressure,
        Temperature,
        Flow,
        Position,
        Velocity,
        Acceleration,
        Vibration,
        Acoustic,
        Voltage,
        Current,
        Other
    }
    
    public enum SensorStatus
    {
        Unknown,
        Initializing,
        Ready,
        Reading,
        Error,
        Disconnected,
        Calibrating
    }
    
    public class SensorReadingChangedEventArgs<T> : EventArgs where T : struct
    {
        public T OldValue { get; set; }
        public T NewValue { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
