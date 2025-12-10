using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HB_NLP_Research_Lab.Core.Hardware;
using HB_NLP_Research_Lab.Core.Control;

namespace HB_NLP_Research_Lab.Core.Safety
{
    /// <summary>
    /// Real-time safety monitoring system with hardware interlocks
    /// Monitors critical parameters and triggers emergency shutdown if limits exceeded
    /// </summary>
    public class HardwareSafetyMonitor : RealTimeControlLoop
    {
        private readonly List<ISensor<double>> _criticalSensors;
        private readonly IActuator? _emergencyShutdownActuator;
        private readonly Dictionary<string, SafetyLimit> _safetyLimits;
        
        private bool _emergencyShutdownActive = false;
        private readonly object _lock = new object();
        
        public bool IsEmergencyShutdownActive => _emergencyShutdownActive;
        public event EventHandler<SafetyViolationEventArgs>? SafetyViolationDetected;
        public event EventHandler<EmergencyShutdownEventArgs>? EmergencyShutdownTriggered;
        
        public HardwareSafetyMonitor(
            List<ISensor<double>> criticalSensors,
            IActuator? emergencyShutdownActuator = null,
            int frequencyHz = 100) : base(frequencyHz)
        {
            _criticalSensors = criticalSensors ?? throw new ArgumentNullException(nameof(criticalSensors));
            _emergencyShutdownActuator = emergencyShutdownActuator;
            
            // Initialize safety limits
            _safetyLimits = new Dictionary<string, SafetyLimit>
            {
                { "ChamberPressure", new SafetyLimit { Min = 0, Max = 35_000_000, Critical = true } }, // 35 MPa max
                { "ChamberTemperature", new SafetyLimit { Min = 0, Max = 4000, Critical = true } }, // 4000 K max
                { "FuelFlow", new SafetyLimit { Min = 0, Max = 1000, Critical = true } }, // kg/s
                { "OxidizerFlow", new SafetyLimit { Min = 0, Max = 2000, Critical = true } }, // kg/s
                { "TurbopumpSpeed", new SafetyLimit { Min = 0, Max = 100000, Critical = true } }, // RPM
            };
        }
        
        /// <summary>
        /// Add or update a safety limit
        /// </summary>
        public void SetSafetyLimit(string parameterName, double min, double max, bool critical = true)
        {
            _safetyLimits[parameterName] = new SafetyLimit
            {
                Min = min,
                Max = max,
                Critical = critical
            };
        }
        
        /// <summary>
        /// Reset emergency shutdown (requires manual intervention)
        /// </summary>
        public void ResetEmergencyShutdown()
        {
            lock (_lock)
            {
                if (_emergencyShutdownActive)
                {
                    Console.WriteLine("[Safety Monitor] 🔄 Resetting emergency shutdown (requires verification)");
                    _emergencyShutdownActive = false;
                }
            }
        }
        
        protected override async Task ExecuteControlLoopAsync(CancellationToken cancellationToken)
        {
            if (_emergencyShutdownActive)
            {
                // Keep shutdown active, don't check sensors
                return;
            }
            
            // Check all critical sensors
            foreach (var sensor in _criticalSensors)
            {
                try
                {
                    var value = await sensor.ReadAsync(cancellationToken);
                    var sensorName = sensor.Name;
                    
                    // Check against safety limits
                    if (_safetyLimits.TryGetValue(sensorName, out var limit) && 
                        (value < limit.Min || value > limit.Max))
                    {
                        await HandleSafetyViolationAsync(sensorName, value, limit, cancellationToken);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // Sensor not available or not initialized
                    Console.WriteLine($"[Safety Monitor] ⚠️ Sensor {sensor.Name} not available: {ex.Message}");
                    await HandleSensorFailureAsync(sensor.Name, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // Sensor read timeout - treat as critical
                    Console.WriteLine($"[Safety Monitor] ⚠️ Sensor {sensor.Name} read timeout");
                    await HandleSensorFailureAsync(sensor.Name, cancellationToken);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NullReferenceException)
                {
                    // Sensor data validation errors - treat as critical
                    Console.WriteLine($"[Safety Monitor] ❌ Sensor {sensor.Name} data error: {ex.Message}");
                    await HandleSensorFailureAsync(sensor.Name, cancellationToken);
                }
                // codeql[generic-catch-clause]: Intentional final catch-all for safety - all specific exceptions handled above
                catch (Exception ex)
                {
                    // Catch-all for unexpected sensor errors - treat as critical
                    Console.WriteLine($"[Safety Monitor] ❌ Sensor {sensor.Name} read failure: {ex.Message}");
                    await HandleSensorFailureAsync(sensor.Name, cancellationToken);
                }
            }
        }
        
        private async Task HandleSafetyViolationAsync(
            string parameterName,
            double value,
            SafetyLimit limit,
            CancellationToken cancellationToken)
        {
            var violation = new SafetyViolation
            {
                ParameterName = parameterName,
                Value = value,
                Limit = limit,
                Timestamp = DateTime.UtcNow
            };
            
            Console.WriteLine($"[Safety Monitor] ⚠️ Safety violation: {parameterName} = {value} (limit: {limit.Min}-{limit.Max})");
            
            // Fire event
            SafetyViolationDetected?.Invoke(this, new SafetyViolationEventArgs { Violation = violation });
            
            // If critical, trigger emergency shutdown
            if (limit.Critical)
            {
                await TriggerEmergencyShutdownAsync($"Critical safety violation: {parameterName}", cancellationToken);
            }
        }
        
        private async Task HandleSensorFailureAsync(string sensorName, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[Safety Monitor] ⚠️ Sensor failure: {sensorName}");
            
            // Sensor failure is critical - trigger shutdown
            await TriggerEmergencyShutdownAsync($"Sensor failure: {sensorName}", cancellationToken);
        }
        
        private async Task TriggerEmergencyShutdownAsync(string reason, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (_emergencyShutdownActive)
                    return; // Already shutdown
                
                _emergencyShutdownActive = true;
            }
            
            Console.WriteLine($"[Safety Monitor] 🚨 EMERGENCY SHUTDOWN: {reason}");
            
            // Activate hardware shutdown if available
            if (_emergencyShutdownActuator != null)
            {
                try
                {
                    await _emergencyShutdownActuator.SetPositionAsync(1.0, cancellationToken); // Activate shutdown
                }
                catch (InvalidOperationException ex)
                {
                    // Actuator not ready or not initialized
                    Console.WriteLine($"[Safety Monitor] ⚠️ Hardware shutdown actuator not ready: {ex.Message}");
                }
                catch (TaskCanceledException)
                {
                    // Actuator command timeout
                    Console.WriteLine($"[Safety Monitor] ⚠️ Hardware shutdown command timeout");
                }
                catch (Exception ex) when (ex is ArgumentException || ex is ArgumentOutOfRangeException)
                {
                    // Invalid actuator command
                    Console.WriteLine($"[Safety Monitor] ⚠️ Invalid shutdown command: {ex.Message}");
                }
                // codeql[generic-catch-clause]: Intentional final catch-all for safety - all specific exceptions handled above
                catch (Exception ex)
                {
                    // Catch-all for unexpected actuator errors
                    Console.WriteLine($"[Safety Monitor] ❌ Failed to activate hardware shutdown: {ex.Message}");
                }
            }
            
            // Fire event
            EmergencyShutdownTriggered?.Invoke(this, new EmergencyShutdownEventArgs
            {
                Reason = reason,
                Timestamp = DateTime.UtcNow
            });
        }
        
        protected override Task OnLoopStartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"[Safety Monitor] Starting hardware safety monitor at {LoopFrequencyHz} Hz");
            Console.WriteLine($"[Safety Monitor] Monitoring {_criticalSensors.Count} critical sensors");
            return Task.CompletedTask;
        }
        
        protected override Task OnLoopStopAsync()
        {
            Console.WriteLine("[Safety Monitor] Stopping safety monitor");
            return Task.CompletedTask;
        }
    }
    
    public class SafetyLimit
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public bool Critical { get; set; }
    }
    
    public class SafetyViolation
    {
        public string ParameterName { get; set; } = string.Empty;
        public double Value { get; set; }
        public SafetyLimit Limit { get; set; } = new SafetyLimit();
        public DateTime Timestamp { get; set; }
    }
    
    public class SafetyViolationEventArgs : EventArgs
    {
        public SafetyViolation Violation { get; set; } = new SafetyViolation();
    }
    
    public class EmergencyShutdownEventArgs : EventArgs
    {
        public string Reason { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
