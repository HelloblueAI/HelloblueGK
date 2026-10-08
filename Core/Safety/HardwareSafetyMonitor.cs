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
        private bool _shutdownPending = false;
        private string _pendingShutdownReason = string.Empty;
        private readonly object _lock = new object();

        /// <summary>
        /// True only after the shutdown command has been accepted, or when no actuator
        /// is configured. A rejected or thrown command leaves this false so the next
        /// pass can try again.
        /// </summary>
        public bool IsEmergencyShutdownActive => _emergencyShutdownActive;

        /// <summary>
        /// One monitor pass, the same check the control loop runs. Tests call this so a
        /// non-finite reading can be asserted without waiting on the scheduler.
        /// </summary>
        internal Task CheckSensorsOnceAsync(CancellationToken cancellationToken = default) =>
            ExecuteControlLoopAsync(cancellationToken);
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
        /// Add or update a safety limit. A non-finite or inverted bound cannot be installed:
        /// comparisons with NaN are false, so a NaN ceiling would disable the interlock.
        /// </summary>
        public void SetSafetyLimit(string parameterName, double min, double max, bool critical = true)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                throw new ArgumentException("Parameter name is required.", nameof(parameterName));
            }

            if (!IsUsableLimit(min, max))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(max),
                    "Safety limits must be finite, and the minimum must not exceed the maximum.");
            }

            _safetyLimits[parameterName] = new SafetyLimit
            {
                Min = min,
                Max = max,
                Critical = critical
            };
        }

        /// <summary>
        /// A reading is outside its limit when it is non-finite or beyond the inclusive band.
        /// NaN is neither below the minimum nor above the maximum, so the range check alone
        /// used to leave a failed sensor running.
        /// </summary>
        internal static bool ReadingViolatesLimit(double value, double min, double max)
        {
            if (!IsUsableLimit(min, max) || !double.IsFinite(value))
            {
                return true;
            }

            return value < min || value > max;
        }

        private static bool IsUsableLimit(double min, double max) =>
            double.IsFinite(min) && double.IsFinite(max) && min <= max;
        
        /// <summary>
        /// Reset emergency shutdown (requires manual intervention)
        /// </summary>
        public void ResetEmergencyShutdown()
        {
            lock (_lock)
            {
                if (_emergencyShutdownActive || _shutdownPending)
                {
                    Console.WriteLine("[Safety Monitor] 🔄 Resetting emergency shutdown (requires verification)");
                    _emergencyShutdownActive = false;
                    _shutdownPending = false;
                    _pendingShutdownReason = string.Empty;
                }
            }
        }
        
        protected override async Task ExecuteControlLoopAsync(CancellationToken cancellationToken)
        {
            if (_emergencyShutdownActive)
            {
                // Hardware shutdown was accepted. Do not resume sensor checks.
                return;
            }

            if (_shutdownPending)
            {
                // The last command was rejected or threw. A later in-range reading
                // must not cancel the shutdown that was already required.
                await CompletePendingShutdownAsync(cancellationToken);
                return;
            }
            
            // Check all critical sensors
            foreach (var sensor in _criticalSensors)
            {
                try
                {
                    var value = await sensor.ReadAsync(cancellationToken);
                    var sensorName = sensor.Name;
                    
                    // Check against safety limits. A missing or non-finite reading is a
                    // violation: the range comparison does not reject NaN.
                    if (_safetyLimits.TryGetValue(sensorName, out var limit) &&
                        ReadingViolatesLimit(value, limit.Min, limit.Max))
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
                if (_emergencyShutdownActive || _shutdownPending)
                    return;

                // Remember that shutdown is required before the command. The flag that
                // skips sensor checks is set only after the actuator accepts, so a
                // thrown or rejected command is retried on the next pass.
                _shutdownPending = true;
                _pendingShutdownReason = reason;
            }
            
            Console.WriteLine($"[Safety Monitor] 🚨 EMERGENCY SHUTDOWN: {reason}");
            await CompletePendingShutdownAsync(cancellationToken);
        }

        private async Task CompletePendingShutdownAsync(CancellationToken cancellationToken)
        {
            if (!await CommandShutdownActuatorAsync(cancellationToken))
                return;

            string reason;
            lock (_lock)
            {
                // Reset may have cleared the pending shutdown while the command was in flight.
                if (!_shutdownPending || _emergencyShutdownActive)
                    return;

                _emergencyShutdownActive = true;
                reason = _pendingShutdownReason;
            }

            EmergencyShutdownTriggered?.Invoke(this, new EmergencyShutdownEventArgs
            {
                Reason = reason,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Returns true when shutdown is achieved. No actuator is a software-only
        /// shutdown. A false return or a thrown command leaves the pending latch set.
        /// </summary>
        private async Task<bool> CommandShutdownActuatorAsync(CancellationToken cancellationToken)
        {
            if (_emergencyShutdownActuator == null)
                return true;

            try
            {
                var accepted = await _emergencyShutdownActuator.SetPositionAsync(1.0, cancellationToken);
                if (!accepted)
                {
                    Console.WriteLine("[Safety Monitor] ⚠️ Hardware shutdown command was rejected");
                }

                return accepted;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (InvalidOperationException ex)
            {
                // Actuator not ready or not initialized
                Console.WriteLine($"[Safety Monitor] ⚠️ Hardware shutdown actuator not ready: {ex.Message}");
                return false;
            }
            catch (TaskCanceledException)
            {
                // Actuator command timeout
                Console.WriteLine($"[Safety Monitor] ⚠️ Hardware shutdown command timeout");
                return false;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is ArgumentOutOfRangeException)
            {
                // Invalid actuator command
                Console.WriteLine($"[Safety Monitor] ⚠️ Invalid shutdown command: {ex.Message}");
                return false;
            }
            // codeql[generic-catch-clause]: Intentional final catch-all for safety - all specific exceptions handled above
            catch (Exception ex)
            {
                // Catch-all for unexpected actuator errors
                Console.WriteLine($"[Safety Monitor] ❌ Failed to activate hardware shutdown: {ex.Message}");
                return false;
            }
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
