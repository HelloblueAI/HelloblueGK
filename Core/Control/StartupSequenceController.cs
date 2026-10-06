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

        /// <summary>
        /// Absolute ambient band. Standard sea-level pressure is 101325 Pa, so a ceiling of
        /// 100 kPa rejected a correct ambient reading before the sequence could start.
        /// </summary>
        internal const double MaxAmbientPressurePascals = 105_000;

        internal const double MinAmbientTemperatureKelvin = 200;
        internal const double MaxAmbientTemperatureKelvin = 400;

        /// <summary>
        /// Combustion has to be a rise out of the ambient band. 50 kPa is below that band,
        /// so the old check accepted a chamber that pre-start still called "not running".
        /// </summary>
        internal const double MinCombustionTemperatureKelvin = 1_000;

        /// <summary>
        /// Operating pressure is above combustion detection. 100 kPa absolute is still
        /// inside the ambient band.
        /// </summary>
        internal const double MinOperatingPressurePascals = 150_000;

        /// <summary>
        /// Same chamber ceilings as the hardware safety monitor. A pegged transducer is
        /// finite and above the operating floor, so a floor-only check used to treat it
        /// as proof of combustion or of a running engine.
        /// </summary>
        internal const double MaximumChamberPressurePascals = 35_000_000;

        internal const double MaximumChamberTemperatureKelvin = 4_000;

        internal const double MinimumPropellantFlow = 0.01;

        /// <summary>
        /// Same ceilings as the hardware safety monitor. A pegged sensor is finite and
        /// far above the minimum, so a floor-only check used to treat it as proof of
        /// flow and continue into ignition.
        /// </summary>
        internal const double MaximumFuelFlowKgPerSecond = 1000;

        internal const double MaximumOxidizerFlowKgPerSecond = 2000;

        /// <summary>
        /// A finite pressure inside the ambient band. NaN used to pass: every comparison
        /// with NaN is false, so the old range check did not reject it.
        /// </summary>
        internal static bool IsAmbientPressure(double pressure) =>
            double.IsFinite(pressure) && pressure >= 0 && pressure <= MaxAmbientPressurePascals;

        internal static bool IsAmbientTemperature(double temperature) =>
            double.IsFinite(temperature)
            && temperature >= MinAmbientTemperatureKelvin
            && temperature <= MaxAmbientTemperatureKelvin;

        internal static bool IsCombustionEstablished(double pressure, double temperature) =>
            double.IsFinite(pressure)
            && pressure > MaxAmbientPressurePascals
            && pressure <= MaximumChamberPressurePascals
            && double.IsFinite(temperature)
            && temperature >= MinCombustionTemperatureKelvin
            && temperature <= MaximumChamberTemperatureKelvin;

        /// <summary>
        /// A chamber reading the startup sequence can keep acting on. Negative, non-finite,
        /// and above-ceiling values are a failed transducer, not "not yet at power".
        /// </summary>
        internal static bool IsPlausibleChamberPressure(double pressure) =>
            double.IsFinite(pressure) && pressure >= 0 && pressure <= MaximumChamberPressurePascals;

        internal static bool IsOperatingPressure(double pressure) =>
            IsPlausibleChamberPressure(pressure) && pressure >= MinOperatingPressurePascals;

        internal static bool HasMinimumPropellantFlow(double flow) =>
            double.IsFinite(flow) && flow >= MinimumPropellantFlow;

        /// <summary>
        /// Flow is established only inside the inclusive band. A non-finite reading or a
        /// non-finite ceiling is not established flow.
        /// </summary>
        internal static bool IsPropellantFlowInBand(double flow, double maximum) =>
            HasMinimumPropellantFlow(flow) && double.IsFinite(maximum) && flow <= maximum;

        /// <summary>
        /// One startup pass, the same step the control loop runs.
        /// </summary>
        internal Task ExecuteOnceAsync(CancellationToken cancellationToken = default) =>
            ExecuteControlLoopAsync(cancellationToken);

        internal void MoveToState(StartupState state)
        {
            _currentState = state;
            _stateStartTime = DateTime.UtcNow;
        }
        
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
        /// Stop the control loop and close any valve this sequence may have opened.
        /// The base stop cancels the loop token; if that cancellation wins before the
        /// loop body starts, <see cref="OnLoopStopAsync"/> never runs.
        /// </summary>
        public override async Task StopAsync()
        {
            await base.StopAsync().ConfigureAwait(false);
            await SafeValvesOnStopAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Abort startup sequence
        /// </summary>
        public void AbortStartup()
        {
            // Idle has not opened a valve. Running used to take this same early return,
            // so abort left the propellant valves at their last commanded position.
            if (_currentState == StartupState.Idle)
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
                await FailClosedAsync($"[Startup Sequence] ⚠️ State {_currentState} timed out");
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
            
            // Verify sensors are reading valid values. Non-finite readings are not ambient.
            if (!IsAmbientPressure(pressure))
            {
                await FailClosedAsync($"[Startup Sequence] ❌ Invalid pressure reading: {pressure}");
                return;
            }
            
            if (!IsAmbientTemperature(temperature))
            {
                await FailClosedAsync($"[Startup Sequence] ❌ Invalid temperature reading: {temperature}");
                return;
            }
            
            // Check actuators
            if (_fuelValve.Status != ActuatorStatus.Ready)
            {
                await FailClosedAsync($"[Startup Sequence] ❌ Fuel valve not ready: {_fuelValve.Status}");
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
            
            // Verify fuel flow is inside the safety band. A pegged sensor used to pass
            // the minimum check and the sequence continued into ignition.
            var fuelFlow = await _fuelFlowSensor.ReadAsync(cancellationToken);
            if (!IsPropellantFlowInBand(fuelFlow, MaximumFuelFlowKgPerSecond))
            {
                await FailClosedAsync($"[Startup Sequence] ❌ Fuel flow outside the allowed band: {fuelFlow}");
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
            
            // Verify oxidizer flow is inside the safety band.
            var oxidizerFlow = await _oxidizerFlowSensor.ReadAsync(cancellationToken);
            if (!IsPropellantFlowInBand(oxidizerFlow, MaximumOxidizerFlowKgPerSecond))
            {
                await FailClosedAsync($"[Startup Sequence] ❌ Oxidizer flow outside the allowed band: {oxidizerFlow}");
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
            
            // Pressure has to leave the ambient band and stay under the chamber ceiling.
            // A non-finite or pegged reading is not combustion.
            if (!IsCombustionEstablished(pressure, temperature))
            {
                await FailClosedAsync(
                    $"[Startup Sequence] ❌ Combustion not detected: P={pressure}, T={temperature}");
                return;
            }
            
            Console.WriteLine("[Startup Sequence] ✅ Combustion verified!");
            TransitionToState(StartupState.ThrottleUp);
        }
        
        private async Task ExecuteThrottleUpAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[Startup Sequence] 🚀 Throttling up to operating level...");

            var pressure = await _chamberPressureSensor.ReadAsync(cancellationToken);
            // A pegged or missing reading is not "still throttling up". Waiting out the
            // ramp left the propellant valves open on a failed transducer.
            if (!IsPlausibleChamberPressure(pressure))
            {
                await FailClosedAsync(
                    $"[Startup Sequence] ❌ Chamber pressure outside the allowed band: {pressure}");
                return;
            }

            if (IsOperatingPressure(pressure))
            {
                Console.WriteLine("[Startup Sequence] ✅ Engine running!");
                TransitionToState(StartupState.Running);
                return;
            }

            // Still below the operating floor. The delay stands in for the throttle ramp.
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
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
        
        private async Task FailClosedAsync(string message)
        {
            Console.WriteLine(message);
            _currentState = StartupState.Error;
            // Close on this pass. Waiting for the next loop tick left the fuel valve
            // at the last commanded position if the loop stopped in between.
            await PerformShutdownAsync();
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

        protected override Task OnLoopStopAsync() => SafeValvesOnStopAsync();

        private async Task SafeValvesOnStopAsync()
        {
            Console.WriteLine("[Startup Sequence] Stopping startup sequence controller");
            // Idle has not opened a valve. Every other state may have, including Running,
            // which the control loop otherwise leaves untouched.
            if (_currentState == StartupState.Idle)
            {
                return;
            }

            var failed = _currentState == StartupState.Error;
            await PerformShutdownAsync().ConfigureAwait(false);
            if (!failed && _currentState != StartupState.Aborted)
            {
                _currentState = StartupState.Aborted;
            }
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
