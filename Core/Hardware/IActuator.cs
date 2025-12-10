using System;
using System.Threading;
using System.Threading.Tasks;

namespace HB_NLP_Research_Lab.Core.Hardware
{
    /// <summary>
    /// Interface for hardware actuators (valves, pumps, gimbals, etc.)
    /// </summary>
    public interface IActuator
    {
        /// <summary>
        /// Unique identifier for this actuator
        /// </summary>
        string ActuatorId { get; }
        
        /// <summary>
        /// Human-readable name/description
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Actuator type (Valve, Pump, Gimbal, etc.)
        /// </summary>
        ActuatorType Type { get; }
        
        /// <summary>
        /// Set actuator position/state (0.0 to 1.0 for position, or specific units)
        /// </summary>
        Task<bool> SetPositionAsync(double position, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get current actuator position/state
        /// </summary>
        Task<double> GetPositionAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Current actuator status
        /// </summary>
        ActuatorStatus Status { get; }
        
        /// <summary>
        /// Minimum position value
        /// </summary>
        double MinPosition { get; }
        
        /// <summary>
        /// Maximum position value
        /// </summary>
        double MaxPosition { get; }
        
        /// <summary>
        /// Response time (seconds) to reach commanded position
        /// </summary>
        double ResponseTimeSeconds { get; }
        
        /// <summary>
        /// Maximum rate of change (units per second)
        /// </summary>
        double MaxRateOfChange { get; }
        
        /// <summary>
        /// Enable/disable actuator
        /// </summary>
        Task<bool> EnableAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Disable actuator (safe state)
        /// </summary>
        Task<bool> DisableAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Is actuator enabled
        /// </summary>
        bool IsEnabled { get; }
        
        /// <summary>
        /// Event fired when actuator position changes
        /// </summary>
        event EventHandler<ActuatorPositionChangedEventArgs> PositionChanged;
    }
    
    public enum ActuatorType
    {
        Valve,
        Pump,
        Gimbal,
        Throttle,
        Igniter,
        Other
    }
    
    public enum ActuatorStatus
    {
        Unknown,
        Initializing,
        Ready,
        Moving,
        AtPosition,
        Error,
        Disabled,
        Fault
    }
    
    public class ActuatorPositionChangedEventArgs : EventArgs
    {
        public double OldPosition { get; set; }
        public double NewPosition { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
