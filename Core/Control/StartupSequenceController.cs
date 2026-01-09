using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using HB_NLP_Research_Lab.Core.Hardware;

namespace HB_NLP_Research_Lab.Core.Control
{
    /// <summary>
    /// Engine startup sequence controller
    /// Manages the complex startup sequence with safety checks
    /// </summary>
    public class StartupSequenceController : RealTimeControlLoop
    {
        private readonly IActuator _fuelValve;
        private readonly IActuator _oxidizerValve;
        private readonly IActuator _igniter;
        private readonly ISensor<double> _chamberPressureSensor;
        private readonly ISensor<double> _chamberTemperatureSensor;
        private readonly ISensor<double> _fuelFlowSensor;
        private readonly ISensor<double> _oxidizerFlowSensor;
        
        private StartupState _currentState = StartupState.Idle;
        private DateTime _stateStartTime;
        private readonly Dictionary<StartupState, TimeSpan> _stateTimeouts;
        
        public StartupState CurrentState => _currentState;
        public bool IsStartupComplete => _currentState == StartupState.Running;
        public bool HasError => _currentState == StartupState.Error;
        
        public StartupSequenceController(
            IActuator fuelValve,
            IActuator oxidizerValve,
            IActuator igniter,
            ISensor<double> chamberPressureSensor,
            ISensor<double> chamberTemperatureSensor,
            ISensor<double> fuelFlowSensor,
            ISensor<double> oxidizerFlowSensor,
            int frequencyHz = 10) : base(frequencyHz)
        {
            _fuelValve = fuelValve ?? throw new ArgumentNullException(nameof(fuelValve));
            _oxidizerValve = oxidizerValve ?? throw new ArgumentNullException(nameof(oxidizerValve));
            _igniter = igniter ?? throw new ArgumentNullException(nameof(igniter));
            _chamberPressureSensor = chamberPressureSensor ?? throw new ArgumentNullException(nameof(chamberPressureSensor));
            _chamberTemperatureSensor = chamberTemperatureSensor ?? throw new ArgumentNullException(nameof(chamberTemperatureSensor));
            _fuelFlowSensor = fuelFlowSensor ?? throw new ArgumentNullException(nameof(fuelFlowSensor));
            _oxidizerFlowSensor = oxidizerFlowSensor ?? throw new ArgumentNullException(nameof(oxidizerFlowSensor));
            
            // Define state timeouts
            _stateTimeouts = new Dictionary<StartupState, TimeSpan>
            {
                { StartupState.PreStartupChecks, TimeSpan.FromSeconds(30) },
                { StartupState.Purge, TimeSpan.FromSeconds(10) },
                { StartupState.FuelFlowInitiation, TimeSpan.FromSeconds(5) },
                { StartupState.OxidizerFlowInitiation, TimeSpan.FromSeconds(5) },
                { StartupState.Ignition, TimeSpan.FromSeconds(3) },
                { StartupState.CombustionVerification, TimeSpan.FromSeconds(5) },
                { StartupState.ThrottleUp, TimeSpan.FromSeconds(10) },
                { StartupState.Running, TimeSpan.MaxValue } // No timeout when running
            };
        }
        
        /// <summary>
        /// Start the engine startup sequence
        /// </summary>
        public void BeginStartup()
        {
            if (_currentState != StartupState.Idle)
                throw new InvalidOperationException($"Cannot start engine: current state is {_currentState}");
            
            _currentState = StartupState.PreStartupChecks;
            _stateStartTime = DateTime.UtcNow;
            Console.WriteLine("[Startup Sequence] 🚀 Beginning engine startup sequence");
        }
        
        /// <summary>
        /// Abort startup sequence
        /// </summary>
        public void AbortStartup()
        {
            if (_currentState == StartupState.Idle || _currentState == StartupState.Running)
                return;
            
            Console.WriteLine("[Startup Sequence] ⛔ Aborting startup sequence");
            _currentState = StartupState.Aborted;
            PerformShutdown();
        }
        
        protected override async Task ExecuteControlLoopAsync(CancellationToken cancellationToken)
        {
            if (_currentState == StartupState.Idle || _currentState == StartupState.Running)
                return;
            
            // Check for timeout
            if (CheckStateTimeout())
            {
                Console.WriteLine($"[Startup Sequence] ⚠️ State {_currentState} timed out");
                _currentState = StartupState.Error;
                await PerformShutdownAsync();
                return;
            }
            
            // Execute state machine
            switch (_currentState)
            {
                case StartupState.PreStartupChecks:
                    await ExecutePreStartupChecksAsync(cancellationToken);
                    break;
                case StartupState.Purge:
                    await ExecutePurgeAsync(cancellationToken);
                    break;
                case StartupState.FuelFlowInitiation:
                    await ExecuteFuelFlowInitiationAsync(cancellationToken);
                    break;
                case StartupState.OxidizerFlowInitiation:
                    await ExecuteOxidizerFlowInitiationAsync(cancellationToken);
                    break;
                case StartupState.Ignition:
                    await ExecuteIgnitionAsync(cancellationToken);
                    break;
                case StartupState.CombustionVerification:
                    await ExecuteCombustionVerificationAsync(cancellationToken);
                    break;
                case StartupState.ThrottleUp:
                    await ExecuteThrottleUpAsync(cancellationToken);
                    break;
                case StartupState.Error:
                case StartupState.Aborted:
                    await PerformShutdownAsync();
                    break;
            }
        }
        
        private async Task ExecutePreStartupChecksAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[Startup Sequence] 🔍 Performing pre-startup checks...");
            
            // Check sensors
            var pressure = await _chamberPressureSensor.ReadAsync(cancellationToken);
            var temperature = await _chamberTemperatureSensor.ReadAsync(cancellationToken);
            
            // Verify sensors are reading valid values
            if (pressure < 0 || pressure > 100000) // 0 to 100 kPa (atmospheric)
            {
                Console.WriteLine($"[Startup Sequence] ❌ Invalid pressure reading: {pressure}");
                _currentState = StartupState.Error;
                return;
            }
            
            if (temperature < 200 || temperature > 400) // 200-400 K (reasonable ambient)
            {
                Console.WriteLine($"[Startup Sequence] ❌ Invalid temperature reading: {temperature}");
                _currentState = StartupState.Error;
                return;
            }
            
            // Check actuators
            if (_fuelValve.Status != ActuatorStatus.Ready)
            {
                Console.WriteLine($"[Startup Sequence] ❌ Fuel valve not ready: {_fuelValve.Status}");
                _currentState = StartupState.Error;
                return;
            }
            
            // All checks passed - move to next state
            TransitionToState(StartupState.Purge);
        }
        
        private async Task ExecutePurgeAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[Startup Sequence] 💨 Purging system...");
            
            // Open purge valves (simplified - would need purge valve actuator)
            // For now, just wait for purge duration
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            
            TransitionToState(StartupState.FuelFlowInitiation);
        }
        
        private async Task ExecuteFuelFlowInitiationAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[Startup Sequence] ⛽ Initiating fuel flow...");
            
            // Open fuel valve gradually
            await _fuelValve.SetPositionAsync(0.1, cancellationToken); // 10% open
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            
            // Verify fuel flow
            var fuelFlow = await _fuelFlowSensor.ReadAsync(cancellationToken);
            if (fuelFlow < 0.01) // Minimum flow threshold
            {
                Console.WriteLine($"[Startup Sequence] ❌ Fuel flow too low: {fuelFlow}");
                _currentState = StartupState.Error;
                return;
            }
            
            TransitionToState(StartupState.OxidizerFlowInitiation);
        }
        
        private async Task ExecuteOxidizerFlowInitiationAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[Startup Sequence] 💧 Initiating oxidizer flow...");
            
            // Open oxidizer valve gradually
            await _oxidizerValve.SetPositionAsync(0.1, cancellationToken); // 10% open
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            
            // Verify oxidizer flow
            var oxidizerFlow = await _oxidizerFlowSensor.ReadAsync(cancellationToken);
            if (oxidizerFlow < 0.01) // Minimum flow threshold
            {
                Console.WriteLine($"[Startup Sequence] ❌ Oxidizer flow too low: {oxidizerFlow}");
                _currentState = StartupState.Error;
                return;
            }
            
            TransitionToState(StartupState.Ignition);
        }
        
        private async Task ExecuteIgnitionAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[Startup Sequence] 🔥 Igniting...");
            
            // Activate igniter
            await _igniter.SetPositionAsync(1.0, cancellationToken); // Full on
            await Task.Delay(TimeSpan.FromSeconds(0.5), cancellationToken);
            
            // Deactivate igniter (spark plug style - short pulse)
            await _igniter.SetPositionAsync(0.0, cancellationToken);
            
            TransitionToState(StartupState.CombustionVerification);
        }
        
        private async Task ExecuteCombustionVerificationAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[Startup Sequence] ✅ Verifying combustion...");
            
            // Wait for combustion to establish
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            
            // Check for combustion indicators
            var pressure = await _chamberPressureSensor.ReadAsync(cancellationToken);
            var temperature = await _chamberTemperatureSensor.ReadAsync(cancellationToken);
            
            // Verify combustion (pressure and temperature should increase)
            if (pressure < 50000 || temperature < 1000) // 50 kPa, 1000 K minimum
            {
                Console.WriteLine($"[Startup Sequence] ❌ Combustion not detected: P={pressure}, T={temperature}");
                _currentState = StartupState.Error;
                return;
            }
            
            Console.WriteLine("[Startup Sequence] ✅ Combustion verified!");
            TransitionToState(StartupState.ThrottleUp);
        }
        
        private async Task ExecuteThrottleUpAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[Startup Sequence] 🚀 Throttling up to operating level...");
            
            // Gradually increase throttle (would interface with throttle controller)
            // For now, just verify we're running
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            
            var pressure = await _chamberPressureSensor.ReadAsync(cancellationToken);
            if (pressure > 100000) // 100 kPa - reasonable operating pressure
            {
                Console.WriteLine("[Startup Sequence] ✅ Engine running!");
                TransitionToState(StartupState.Running);
            }
        }
        
        private void TransitionToState(StartupState newState)
        {
            Console.WriteLine($"[Startup Sequence] Transitioning: {_currentState} → {newState}");
            _currentState = newState;
            _stateStartTime = DateTime.UtcNow;
        }
        
        private bool CheckStateTimeout()
        {
            if (!_stateTimeouts.TryGetValue(_currentState, out var timeout))
                return false;
            
            var elapsed = DateTime.UtcNow - _stateStartTime;
            return elapsed > timeout;
        }
        
        private async Task PerformShutdownAsync()
        {
            Console.WriteLine("[Startup Sequence] 🔄 Performing shutdown...");
            
            // Close valves
            await _fuelValve.SetPositionAsync(0.0, CancellationToken.None);
            await _oxidizerValve.SetPositionAsync(0.0, CancellationToken.None);
            await _igniter.SetPositionAsync(0.0, CancellationToken.None);
        }
        
        private void PerformShutdown()
        {
            // Use Task.Run to avoid deadlocks when calling async from sync context
            try
            {
                Task.Run(async () => await PerformShutdownAsync().ConfigureAwait(false))
                    .Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Task may have already completed or been cancelled - this is expected during shutdown
            }
        }
        
        protected override Task OnLoopStartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"[Startup Sequence] Starting startup sequence controller at {LoopFrequencyHz} Hz");
            _stateStartTime = DateTime.UtcNow;
            return Task.CompletedTask;
        }
    }
    
    public enum StartupState
    {
        Idle,
        PreStartupChecks,
        Purge,
        FuelFlowInitiation,
        OxidizerFlowInitiation,
        Ignition,
        CombustionVerification,
        ThrottleUp,
        Running,
        Error,
        Aborted
    }
}
