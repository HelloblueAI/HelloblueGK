using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
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
        
        private readonly object _stateGate = new();
        private StartupState _currentState = StartupState.Idle;
        private DateTime _stateStartTime;
        private readonly Dictionary<StartupState, TimeSpan> _stateTimeouts;

        /// <summary>
        /// Set by abort, fail-closed, and stop before the valves are commanded shut.
        /// An in-flight step can otherwise finish its open command after that close
        /// and then transition out of <see cref="StartupState.Aborted"/>.
        /// </summary>
        private volatile bool _sequenceClosed;
        
        public StartupState CurrentState
        {
            get
            {
                lock (_stateGate)
                    return _currentState;
            }
        }

        public bool IsStartupComplete => CurrentState == StartupState.Running;
        public bool HasError => CurrentState == StartupState.Error;

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

        internal const int ShutdownCloseFailureEscalationThreshold = 3;
        internal const int ShutdownCloseFailureLogInterval = 10;

        private int _consecutiveFailedShutdownCloses;

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
        /// A valve may be commanded open only from Ready. Fault, Error, Disabled,
        /// Unknown, and a valve that is still moving are not a safe pre-start.
        /// AtPosition is not Ready: the fuel valve already used that same gate.
        /// </summary>
        internal static bool IsActuatorReadyForStartup(ActuatorStatus status) =>
            status == ActuatorStatus.Ready;

        /// <summary>
        /// A sample is evidence only while the sensor is Ready. Disconnected, Error,
        /// Calibrating, Unknown, and Initializing can still return a cached number.
        /// Reading is not Ready: a sample that has not finished is not a measurement.
        /// </summary>
        internal static bool IsSensorReadyForStartup(SensorStatus status) =>
            status == SensorStatus.Ready;

        /// <summary>
        /// One startup pass, the same step the control loop runs.
        /// </summary>
        internal Task ExecuteOnceAsync(CancellationToken cancellationToken = default) =>
            ExecuteControlLoopAsync(cancellationToken);

        internal void MoveToState(StartupState state)
        {
            lock (_stateGate)
            {
                _currentState = state;
                _stateStartTime = DateTime.UtcNow;
                _sequenceClosed = state is StartupState.Aborted or StartupState.Error;
            }
        }

        /// <summary>
        /// Moves the state clock back so a test can enter the last slice of a deadline
        /// without waiting out the production timeout.
        /// </summary>
        internal void SetStateEnteredAt(DateTime enteredAt)
        {
            lock (_stateGate)
                _stateStartTime = enteredAt;
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
            lock (_stateGate)
            {
                if (_currentState != StartupState.Idle || _sequenceClosed)
                    throw new InvalidOperationException($"Cannot start engine: current state is {_currentState}");

                _sequenceClosed = false;
                _currentState = StartupState.PreStartupChecks;
                _stateStartTime = DateTime.UtcNow;
            }

            Console.WriteLine("[Startup Sequence] 🚀 Beginning engine startup sequence");
        }
        
        /// <summary>
        /// Stop the control loop and close any valve this sequence may have opened.
        /// The base stop cancels the loop token; if that cancellation wins before the
        /// loop body starts, <see cref="OnLoopStopAsync"/> never runs. A fault from
        /// the loop task must not skip this close either.
        /// </summary>
        public override async Task StopAsync()
        {
            Exception? stopException = null;
            Exception? shutdownException = null;

            try
            {
                await base.StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                stopException = ex;
            }

            try
            {
                // The loop's own stop already tried. This is the attempt that still
                // runs when that stop did not, and the one that reports a valve that
                // stayed open after the loop is gone.
                if (!await SafeValvesOnStopAsync().ConfigureAwait(false))
                {
                    shutdownException = new InvalidOperationException(
                        "[Startup Sequence] Shutdown did not close every valve.");
                }
            }
            catch (Exception ex)
            {
                shutdownException = ex;
            }

            if (stopException != null && shutdownException != null)
            {
                throw new AggregateException(
                    "[Startup Sequence] Stop failed and shutdown did not close every valve.",
                    stopException,
                    shutdownException);
            }

            if (shutdownException != null)
            {
                ExceptionDispatchInfo.Capture(shutdownException).Throw();
            }

            if (stopException != null)
            {
                ExceptionDispatchInfo.Capture(stopException).Throw();
            }
        }

        /// <summary>
        /// Abort startup sequence
        /// </summary>
        public void AbortStartup()
        {
            // Idle has not opened a valve. Running used to take this same early return,
            // so abort left the propellant valves at their last commanded position.
            // Latch closed before commanding the valves. The step already in flight
            // does not see a later state write, and its next open would land after this close.
            lock (_stateGate)
            {
                if (_currentState == StartupState.Idle)
                    return;

                _sequenceClosed = true;
                _currentState = StartupState.Aborted;
                _stateStartTime = DateTime.UtcNow;
            }

            Console.WriteLine("[Startup Sequence] ⛔ Aborting startup sequence");
            PerformShutdown();
        }
        
        protected override async Task ExecuteControlLoopAsync(CancellationToken cancellationToken)
        {
            // A closed sequence must not run another open. Abort does not cancel the
            // loop token, so the next tick would otherwise resume the state that was
            // current when this pass started.
            if (_sequenceClosed)
            {
                if (CurrentState != StartupState.Idle)
                {
                    // False means a valve rejected the close or threw. Stay on this
                    // path and command all three again on the next pass.
                    await PerformShutdownAsync().ConfigureAwait(false);
                }

                return;
            }

            if (CurrentState == StartupState.Idle)
                return;

            if (CurrentState == StartupState.Running)
            {
                // Startup is otherwise finished, and this loop used to leave the
                // valves at the last open command until stop or abort. A valve that
                // faults after the running transition is the same loss of a Ready
                // actuator as a fault during throttle-up. Stop and abort still close.
                await RequireStartupActuatorsReadyAsync().ConfigureAwait(false);
                return;
            }
            
            var state = CurrentState;

            // Check for timeout
            if (CheckStateTimeout())
            {
                await FailClosedAsync($"[Startup Sequence] ⚠️ State {state} timed out").ConfigureAwait(false);
                return;
            }
            
            // Execute state machine. Re-read after the timeout check so an abort that
            // landed while we were reading the clock is not overwritten by this pass.
            if (_sequenceClosed)
            {
                await PerformShutdownAsync().ConfigureAwait(false);
                return;
            }

            // The timeout above only runs when a pass returns. Fuel and oxidizer each
            // wait inside the pass after the valve is already open. A command or read
            // that never returns used to leave that valve open: the deadline was not
            // consulted again, and the loop task never reached the next check.
            if (!TryGetRemainingStateBudget(state, out var remaining) || remaining <= TimeSpan.Zero)
            {
                if (remaining <= TimeSpan.Zero && HasFiniteStateTimeout(state))
                {
                    await FailClosedAsync($"[Startup Sequence] ⚠️ State {state} timed out").ConfigureAwait(false);
                }
                else
                {
                    await ExecuteCurrentStateAsync(state, cancellationToken).ConfigureAwait(false);
                }

                return;
            }

            var stateCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stateCts.CancelAfter(remaining);
            var stateWork = ExecuteCurrentStateAsync(state, stateCts.Token);
            var deadline = Task.Delay(remaining, deadlineCts.Token);
            try
            {
                await Task.WhenAny(stateWork, deadline).ConfigureAwait(false);
                if (stateWork.IsCompleted)
                {
                    try
                    {
                        await stateWork.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (OperationCanceledException) when (stateCts.IsCancellationRequested)
                    {
                        // The deadline cancelled this pass. A sensor or actuator that
                        // times out on its own token still has this token live, so that
                        // exception propagates and is not relabeled as a state timeout.
                        await FailClosedAsync($"[Startup Sequence] ⚠️ State {state} timed out").ConfigureAwait(false);
                    }

                    return;
                }

                // Do not wait for the pass. A read that ignores cancellation would
                // keep the valve open for as long as this method stayed on that await.
                stateCts.Cancel();
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                await FailClosedAsync($"[Startup Sequence] ⚠️ State {state} timed out").ConfigureAwait(false);
            }
            finally
            {
                deadlineCts.Cancel();
                ReleaseStatePass(stateWork, stateCts);
            }
        }

        private async Task ExecuteCurrentStateAsync(StartupState state, CancellationToken cancellationToken)
        {
            switch (state)
            {
                case StartupState.PreStartupChecks:
                    await ExecutePreStartupChecksAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case StartupState.Purge:
                    await ExecutePurgeAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case StartupState.FuelFlowInitiation:
                    await ExecuteFuelFlowInitiationAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case StartupState.OxidizerFlowInitiation:
                    await ExecuteOxidizerFlowInitiationAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case StartupState.Ignition:
                    await ExecuteIgnitionAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case StartupState.CombustionVerification:
                    await ExecuteCombustionVerificationAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case StartupState.ThrottleUp:
                    await ExecuteThrottleUpAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case StartupState.Error:
                case StartupState.Aborted:
                    await PerformShutdownAsync().ConfigureAwait(false);
                    break;
            }
        }

        private bool HasFiniteStateTimeout(StartupState state) =>
            _stateTimeouts.TryGetValue(state, out var timeout)
            && timeout > TimeSpan.Zero
            && timeout != TimeSpan.MaxValue;

        /// <summary>
        /// Time left in <paramref name="state"/> before <see cref="CheckStateTimeout"/>
        /// would fail closed. False when this state has no finite deadline.
        /// </summary>
        private bool TryGetRemainingStateBudget(StartupState state, out TimeSpan remaining)
        {
            remaining = default;
            if (!HasFiniteStateTimeout(state))
                return false;

            var timeout = _stateTimeouts[state];
            DateTime started;
            lock (_stateGate)
                started = _stateStartTime;

            remaining = timeout - (DateTime.UtcNow - started);
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;
            return true;
        }

        /// <summary>
        /// Observes a pass that missed its deadline without waiting for it, and disposes
        /// the deadline source once that pass is no longer using the token.
        /// </summary>
        private static void ReleaseStatePass(Task stateWork, CancellationTokenSource stateCts)
        {
            if (stateWork.IsCompleted)
            {
                if (stateWork.IsFaulted)
                    _ = stateWork.Exception;
                stateCts.Dispose();
                return;
            }

            _ = stateWork.ContinueWith(
                completed =>
                {
                    if (completed.IsFaulted)
                        _ = completed.Exception;
                    stateCts.Dispose();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        
        private async Task ExecutePreStartupChecksAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[Startup Sequence] 🔍 Performing pre-startup checks...");
            
            // Check sensors. A disconnected chamber sensor can still return a cached
            // ambient value. That number is not a pre-start measurement. A timeout
            // is not a sample either: it fail-closes instead of stopping the loop.
            var pressureRead = await TryReadStartupSensorAsync(_chamberPressureSensor, cancellationToken)
                .ConfigureAwait(false);
            if (!pressureRead.Completed)
                return;
            var temperatureRead = await TryReadStartupSensorAsync(_chamberTemperatureSensor, cancellationToken)
                .ConfigureAwait(false);
            if (!temperatureRead.Completed)
                return;

            var pressure = pressureRead.Value;
            var temperature = temperatureRead.Value;

            if (!await RequireSensorReadyAsync(_chamberPressureSensor).ConfigureAwait(false))
                return;
            if (!await RequireSensorReadyAsync(_chamberTemperatureSensor).ConfigureAwait(false))
                return;
            
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

            // Fuel, oxidizer, and igniter. A faulted oxidizer or igniter used to pass
            // this gate, so purge ran and the next pass opened fuel before the later
            // rejected command could fail closed.
            if (!await RequireStartupActuatorsReadyAsync().ConfigureAwait(false))
                return;
            
            // All checks passed - move to next state
            TransitionToState(StartupState.Purge);
        }
        
        private async Task ExecutePurgeAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[Startup Sequence] 💨 Purging system...");
            
            // Open purge valves (simplified - would need purge valve actuator)
            // For now, just wait for purge duration
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            if (!await ContinueSequenceAsync().ConfigureAwait(false))
                return;
            
            TransitionToState(StartupState.FuelFlowInitiation);
        }
        
        private async Task ExecuteFuelFlowInitiationAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[Startup Sequence] ⛽ Initiating fuel flow...");

            // Re-check every actuator. One can fault during purge, after pre-start
            // already passed, and this open is the command that admits propellant.
            if (!await RequireStartupActuatorsReadyAsync().ConfigureAwait(false))
                return;

            // Do not admit fuel to measure a sensor that is already not Ready.
            if (!await RequireSensorReadyAsync(_fuelFlowSensor).ConfigureAwait(false))
                return;

            // Pre-start is the last time the chamber was required to be ambient.
            // Purge does not read it again. A rise, a pegged or non-finite sample,
            // or a sensor that is no longer Ready must not be the chamber fuel is
            // admitted into.
            if (!await RequireAmbientChamberAsync(cancellationToken).ConfigureAwait(false))
                return;
            
            // Open fuel valve gradually. If abort closed the valve while this command
            // was in flight, do not leave the late open applied or advance the sequence.
            if (!await CommandIfSequenceActiveAsync(_fuelValve, 0.1, cancellationToken).ConfigureAwait(false))
                return;

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            if (!await ContinueSequenceAsync().ConfigureAwait(false))
                return;
            
            // Verify fuel flow is inside the safety band. A pegged sensor used to pass
            // the minimum check and the sequence continued into ignition. A sensor
            // that disconnects during the wait can return that same cached in-band value.
            // A timeout after the valve is open must close it, not stop the loop.
            var fuelFlowRead = await TryReadStartupSensorAsync(_fuelFlowSensor, cancellationToken)
                .ConfigureAwait(false);
            if (!fuelFlowRead.Completed)
                return;
            var fuelFlow = fuelFlowRead.Value;
            if (!await RequireSensorReadyAsync(_fuelFlowSensor).ConfigureAwait(false))
                return;

            if (!IsPropellantFlowInBand(fuelFlow, MaximumFuelFlowKgPerSecond))
            {
                await FailClosedAsync($"[Startup Sequence] ❌ Fuel flow outside the allowed band: {fuelFlow}");
                return;
            }

            // The chamber can leave the ambient band during the flow wait. Oxidizer
            // admission is the next hazardous command, and it must not be reached
            // on a stale pre-start sample.
            if (!await RequireAmbientChamberAsync(cancellationToken).ConfigureAwait(false))
                return;
            
            TransitionToState(StartupState.OxidizerFlowInitiation);
        }
        
        private async Task ExecuteOxidizerFlowInitiationAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[Startup Sequence] 💧 Initiating oxidizer flow...");

            if (!await RequireStartupActuatorsReadyAsync().ConfigureAwait(false))
                return;

            if (!await RequireSensorReadyAsync(_oxidizerFlowSensor).ConfigureAwait(false))
                return;

            // Fuel is already open. The chamber sample that allowed that open is
            // stale. A hot, pressurized, pegged, or unread chamber must not receive
            // oxidizer.
            if (!await RequireAmbientChamberAsync(cancellationToken).ConfigureAwait(false))
                return;
            
            // Open oxidizer valve gradually
            if (!await CommandIfSequenceActiveAsync(_oxidizerValve, 0.1, cancellationToken).ConfigureAwait(false))
                return;

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            if (!await ContinueSequenceAsync().ConfigureAwait(false))
                return;
            
            // Verify oxidizer flow is inside the safety band. Re-check status: the
            // sensor can disconnect after the valve is open and still return a cached flow.
            // A timeout after the valve is open must close it, not stop the loop.
            var oxidizerFlowRead = await TryReadStartupSensorAsync(_oxidizerFlowSensor, cancellationToken)
                .ConfigureAwait(false);
            if (!oxidizerFlowRead.Completed)
                return;
            var oxidizerFlow = oxidizerFlowRead.Value;
            if (!await RequireSensorReadyAsync(_oxidizerFlowSensor).ConfigureAwait(false))
                return;

            if (!IsPropellantFlowInBand(oxidizerFlow, MaximumOxidizerFlowKgPerSecond))
            {
                await FailClosedAsync($"[Startup Sequence] ❌ Oxidizer flow outside the allowed band: {oxidizerFlow}");
                return;
            }

            // Both propellant valves are open. Recheck the chamber before the
            // spark state, including a sample that changed during this wait.
            if (!await RequireAmbientChamberAsync(cancellationToken).ConfigureAwait(false))
                return;
            
            TransitionToState(StartupState.Ignition);
        }
        
        private async Task ExecuteIgnitionAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[Startup Sequence] 🔥 Igniting...");

            // A faulted igniter must not be sparked. The later off command is still
            // attempted when a spark was already accepted.
            if (!await RequireStartupActuatorsReadyAsync().ConfigureAwait(false))
                return;

            // Combustion is a rise out of the ambient band after this pulse. A chamber
            // that has already left that band, or a sensor that can no longer show it,
            // must not be sparked.
            if (!await RequireAmbientChamberAsync(cancellationToken).ConfigureAwait(false))
                return;
            
            // Activate igniter. A late spark command must not stay on after abort.
            if (!await CommandIfSequenceActiveAsync(_igniter, 1.0, cancellationToken).ConfigureAwait(false))
                return;

            await Task.Delay(TimeSpan.FromSeconds(0.5), cancellationToken);
            if (!await ContinueSequenceAsync().ConfigureAwait(false))
                return;
            
            // Deactivate igniter (spark plug style - short pulse)
            if (!await CommandIfSequenceActiveAsync(_igniter, 0.0, cancellationToken).ConfigureAwait(false))
                return;

            // The spark command is the last time this state required the valves to be
            // Ready. A fuel or oxidizer fault during the pulse must not be carried
            // into combustion with the propellant valves still open.
            if (!await RequireStartupActuatorsReadyAsync().ConfigureAwait(false))
                return;
            
            TransitionToState(StartupState.CombustionVerification);
        }
        
        private async Task ExecuteCombustionVerificationAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[Startup Sequence] ✅ Verifying combustion...");

            // Propellant is already admitted. Combustion verification used to ignore
            // actuator status, so a faulted valve stayed at its last open command
            // while a Ready pressure and temperature advanced the sequence.
            // AtPosition is not proof the valve is still where this sequence left it.
            if (!await RequireStartupActuatorsReadyAsync().ConfigureAwait(false))
                return;
            
            // Wait for combustion to establish
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            if (!await ContinueSequenceAsync().ConfigureAwait(false))
                return;
            
            // Check for combustion indicators. A disconnected sensor can return a
            // cached pressure and temperature that look like combustion. A timeout
            // with the propellant valves open must close them, not stop the loop.
            var pressureRead = await TryReadStartupSensorAsync(_chamberPressureSensor, cancellationToken)
                .ConfigureAwait(false);
            if (!pressureRead.Completed)
                return;
            var temperatureRead = await TryReadStartupSensorAsync(_chamberTemperatureSensor, cancellationToken)
                .ConfigureAwait(false);
            if (!temperatureRead.Completed)
                return;

            var pressure = pressureRead.Value;
            var temperature = temperatureRead.Value;
            if (!await RequireSensorReadyAsync(_chamberPressureSensor).ConfigureAwait(false))
                return;
            if (!await RequireSensorReadyAsync(_chamberTemperatureSensor).ConfigureAwait(false))
                return;

            // A valve can fault during the wait and still leave a Ready combustion reading.
            if (!await RequireStartupActuatorsReadyAsync().ConfigureAwait(false))
                return;
            
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

            // Running does not command these valves again. A faulted valve with a
            // Ready operating pressure used to take that path and leave the last
            // open command applied. The same Ready gate as the open commands applies
            // before the ramp and again before the sequence is allowed to finish.
            if (!await RequireStartupActuatorsReadyAsync().ConfigureAwait(false))
                return;

            var pressureRead = await TryReadStartupSensorAsync(_chamberPressureSensor, cancellationToken)
                .ConfigureAwait(false);
            if (!pressureRead.Completed)
                return;
            var pressure = pressureRead.Value;
            if (!await RequireSensorReadyAsync(_chamberPressureSensor).ConfigureAwait(false))
                return;

            // A pegged or missing reading is not "still throttling up". Waiting out the
            // ramp left the propellant valves open on a failed transducer. A disconnected
            // sensor that returns a cached operating pressure is not a running engine,
            // and a cached reading below the floor is not a reason to keep waiting.
            if (!IsPlausibleChamberPressure(pressure))
            {
                await FailClosedAsync(
                    $"[Startup Sequence] ❌ Chamber pressure outside the allowed band: {pressure}");
                return;
            }

            if (!await RequireStartupActuatorsReadyAsync().ConfigureAwait(false))
                return;

            if (IsOperatingPressure(pressure))
            {
                Console.WriteLine("[Startup Sequence] ✅ Engine running!");
                TransitionToState(StartupState.Running);
                return;
            }

            // Still below the operating floor. The delay stands in for the throttle ramp.
            // Re-check when it ends so a fault during the ramp is not left until the
            // next pass.
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            await RequireStartupActuatorsReadyAsync().ConfigureAwait(false);
        }
        
        private void TransitionToState(StartupState newState)
        {
            lock (_stateGate)
            {
                if (_sequenceClosed
                    || _currentState is StartupState.Aborted or StartupState.Error or StartupState.Idle)
                {
                    return;
                }

                Console.WriteLine($"[Startup Sequence] Transitioning: {_currentState} → {newState}");
                _currentState = newState;
                _stateStartTime = DateTime.UtcNow;
            }
        }
        
        private bool CheckStateTimeout()
        {
            StartupState state;
            DateTime started;
            lock (_stateGate)
            {
                state = _currentState;
                started = _stateStartTime;
            }

            if (!_stateTimeouts.TryGetValue(state, out var timeout))
                return false;
            
            var elapsed = DateTime.UtcNow - started;
            return elapsed > timeout;
        }
        
        private async Task FailClosedAsync(string message)
        {
            Console.WriteLine(message);
            lock (_stateGate)
            {
                // Abort already owns the terminal state. Do not reopen it as Error
                // and do not command another open from this pass.
                _sequenceClosed = true;
                if (_currentState != StartupState.Aborted)
                {
                    _currentState = StartupState.Error;
                    _stateStartTime = DateTime.UtcNow;
                }
            }

            // Close on this pass. Waiting for the next loop tick left the fuel valve
            // at the last commanded position if the loop stopped in between.
            // The bool is intentionally ignored here: a rejected close leaves the
            // sequence latched closed, and the closed-state loop keeps retrying it.
            await PerformShutdownAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Returns false after fail-closed when any startup actuator is not Ready.
        /// An abort that already won is left as abort and is not rewritten as an actuator fault.
        /// </summary>
        private async Task<bool> RequireStartupActuatorsReadyAsync()
        {
            if (!await ContinueSequenceAsync().ConfigureAwait(false))
                return false;

            if (!await RequireActuatorReadyAsync(_fuelValve).ConfigureAwait(false))
                return false;
            if (!await RequireActuatorReadyAsync(_oxidizerValve).ConfigureAwait(false))
                return false;

            return await RequireActuatorReadyAsync(_igniter).ConfigureAwait(false);
        }

        private async Task<bool> RequireActuatorReadyAsync(IActuator actuator)
        {
            if (IsActuatorReadyForStartup(actuator.Status))
                return true;

            await FailClosedAsync(
                $"[Startup Sequence] ❌ {actuator.Name} not ready: {actuator.Status}").ConfigureAwait(false);
            return false;
        }

        /// <summary>
        /// Reads one startup sensor. A timeout is a <see cref="TaskCanceledException"/>,
        /// which is an <see cref="OperationCanceledException"/>. The base loop treats
        /// every exception of that type as a clean stop, leaves <c>IsRunning</c> true,
        /// and closes the valves only once. A timeout that is not the loop token is
        /// the same loss of measurement as a sensor that is not Ready: fail-close on
        /// this pass, and let the closed-state loop retry a close that did not land.
        /// Any other exception still escapes so stop can report that fault together
        /// with a valve that stayed open.
        /// </summary>
        private async Task<StartupSensorSample> TryReadStartupSensorAsync(
            ISensor<double> sensor,
            CancellationToken cancellationToken)
        {
            try
            {
                var value = await sensor.ReadAsync(cancellationToken).ConfigureAwait(false);
                return StartupSensorSample.Success(value);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                await FailClosedAsync(
                    $"[Startup Sequence] ❌ {sensor.Name} read timed out").ConfigureAwait(false);
                return StartupSensorSample.TimedOut();
            }
        }

        private readonly struct StartupSensorSample
        {
            private StartupSensorSample(bool completed, double value)
            {
                Completed = completed;
                Value = value;
            }

            public bool Completed { get; }
            public double Value { get; }

            public static StartupSensorSample Success(double value) => new(true, value);

            public static StartupSensorSample TimedOut() => new(false, 0);
        }

        /// <summary>
        /// Returns false after fail-closed when the chamber is no longer a Ready ambient sample.
        /// Fuel, oxidizer, and the igniter are commanded only while this is still the chamber
        /// pre-start accepted. An abort that already won is left as abort.
        /// A timeout that is not the loop token fail-closes. Any other read failure still escapes.
        /// </summary>
        private async Task<bool> RequireAmbientChamberAsync(CancellationToken cancellationToken)
        {
            if (!await ContinueSequenceAsync().ConfigureAwait(false))
                return false;

            var pressureRead = await TryReadStartupSensorAsync(_chamberPressureSensor, cancellationToken)
                .ConfigureAwait(false);
            if (!pressureRead.Completed)
                return false;

            var temperatureRead = await TryReadStartupSensorAsync(_chamberTemperatureSensor, cancellationToken)
                .ConfigureAwait(false);
            if (!temperatureRead.Completed)
                return false;

            if (!await RequireSensorReadyAsync(_chamberPressureSensor).ConfigureAwait(false))
                return false;
            if (!await RequireSensorReadyAsync(_chamberTemperatureSensor).ConfigureAwait(false))
                return false;

            if (IsAmbientPressure(pressureRead.Value) && IsAmbientTemperature(temperatureRead.Value))
                return true;

            await FailClosedAsync(
                $"[Startup Sequence] ❌ Chamber left the ambient band: P={pressureRead.Value}, T={temperatureRead.Value}")
                .ConfigureAwait(false);
            return false;
        }

        /// <summary>
        /// Returns false after fail-closed when the sensor is not Ready.
        /// An abort that already won is left as abort and is not rewritten as a sensor fault.
        /// </summary>
        private async Task<bool> RequireSensorReadyAsync(ISensor<double> sensor)
        {
            if (!await ContinueSequenceAsync().ConfigureAwait(false))
                return false;

            if (IsSensorReadyForStartup(sensor.Status))
                return true;

            await FailClosedAsync(
                $"[Startup Sequence] ❌ {sensor.Name} not ready: {sensor.Status}").ConfigureAwait(false);
            return false;
        }

        /// <summary>
        /// Commands <paramref name="position"/> only while the sequence is still active.
        /// When the command finishes after a close, the valves are shut again and the
        /// caller must not advance.
        /// </summary>
        private async Task<bool> CommandIfSequenceActiveAsync(
            IActuator actuator,
            double position,
            CancellationToken cancellationToken)
        {
            if (_sequenceClosed)
                return false;

            // A false return is a refused move, not a completed one. Fuel and oxidizer
            // used to continue into the next state anyway, and a refused igniter-off
            // left the spark commanded on through combustion and throttle-up.
            // A timeout is a TaskCanceledException, which is an OperationCanceledException.
            // The base loop treats every one of those as a clean stop, so IsRunning stayed
            // true and the safe close ran only once. A refused command already fail-closes
            // and the next pass retries. A timeout that is not the loop token must do the same.
            bool accepted;
            try
            {
                accepted = await actuator.SetPositionAsync(position, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                await FailClosedAsync(
                    $"[Startup Sequence] ❌ {actuator.Name} command {position} timed out").ConfigureAwait(false);
                return false;
            }

            if (!await ContinueSequenceAsync().ConfigureAwait(false))
                return false;

            if (accepted)
                return true;

            await FailClosedAsync(
                $"[Startup Sequence] ❌ {actuator.Name} rejected command {position}").ConfigureAwait(false);
            return false;
        }

        /// <summary>
        /// Returns true when the sequence may advance. A close that won the race shuts
        /// the valves again, because the previous command may have landed after it.
        /// </summary>
        private async Task<bool> ContinueSequenceAsync()
        {
            if (!_sequenceClosed)
                return true;

            await PerformShutdownAsync().ConfigureAwait(false);
            return false;
        }

        /// <summary>
        /// Commands every valve shut. One failure must not skip the valves that
        /// have not been commanded yet. Returns false when any close was rejected
        /// or threw, so the next closed pass can try again.
        /// </summary>
        private async Task<bool> PerformShutdownAsync()
        {
            if (Volatile.Read(ref _consecutiveFailedShutdownCloses) == 0)
                Console.WriteLine("[Startup Sequence] 🔄 Performing shutdown...");

            // Fuel used to be awaited first. A throw from that command left the
            // oxidizer and igniter at their last positions, and the same throw on
            // the loop-stop path faulted the task before those valves were commanded.
            var fuelClosed = await CloseForShutdownAsync(_fuelValve).ConfigureAwait(false);
            var oxidizerClosed = await CloseForShutdownAsync(_oxidizerValve).ConfigureAwait(false);
            var igniterClosed = await CloseForShutdownAsync(_igniter).ConfigureAwait(false);
            var closed = fuelClosed.Closed && oxidizerClosed.Closed && igniterClosed.Closed;
            RecordShutdownCloseResult(closed, fuelClosed, oxidizerClosed, igniterClosed);
            return closed;
        }

        private async Task<ValveCloseResult> CloseForShutdownAsync(IActuator actuator)
        {
            try
            {
                var accepted = await actuator.SetPositionAsync(0.0, CancellationToken.None)
                    .ConfigureAwait(false);
                return accepted
                    ? ValveCloseResult.Success(actuator.Name)
                    : ValveCloseResult.Failure(actuator.Name, "rejected close command");
            }
            // codeql[generic-catch-clause]: A throw from one valve must not skip the others.
            catch (Exception ex)
            {
                return ValveCloseResult.Failure(actuator.Name, ex.Message);
            }
        }

        private void RecordShutdownCloseResult(bool closed, params ValveCloseResult[] results)
        {
            if (closed)
            {
                Interlocked.Exchange(ref _consecutiveFailedShutdownCloses, 0);
                return;
            }

            var failedAttempts = Interlocked.Increment(ref _consecutiveFailedShutdownCloses);
            if (!ShouldLogShutdownCloseFailure(failedAttempts))
                return;

            var failures = new List<string>();
            foreach (var result in results)
            {
                if (!result.Closed)
                    failures.Add($"{result.ActuatorName}: {result.Reason}");
            }

            var detail = failures.Count == 0
                ? string.Empty
                : $" ({string.Join("; ", failures)})";

            if (failedAttempts >= ShutdownCloseFailureEscalationThreshold)
            {
                Console.WriteLine(
                    $"[Startup Sequence] 🚨 ALARM: Shutdown close has failed {failedAttempts} consecutive attempts; keeping the sequence closed and retrying.{detail}");
                return;
            }

            Console.WriteLine(
                $"[Startup Sequence] Valve close failed on shutdown attempt {failedAttempts}; will retry.{detail}");
        }

        private static bool ShouldLogShutdownCloseFailure(int failedAttempts) =>
            failedAttempts <= ShutdownCloseFailureEscalationThreshold
            || failedAttempts % ShutdownCloseFailureLogInterval == 0;

        private readonly struct ValveCloseResult
        {
            private ValveCloseResult(string actuatorName, bool closed, string reason)
            {
                ActuatorName = actuatorName;
                Closed = closed;
                Reason = reason;
            }

            public string ActuatorName { get; }
            public bool Closed { get; }
            public string Reason { get; }

            public static ValveCloseResult Success(string actuatorName) =>
                new(actuatorName, true, string.Empty);

            public static ValveCloseResult Failure(string actuatorName, string reason) =>
                new(actuatorName, false, reason);
        }
        
        private void PerformShutdown()
        {
            // Use Task.Run to avoid deadlocks when calling async from sync context
            try
            {
                var shutdown = Task.Run(async () => await PerformShutdownAsync().ConfigureAwait(false));
                if (!shutdown.Wait(TimeSpan.FromSeconds(5)))
                {
                    Console.WriteLine("[Startup Sequence] Shutdown timed out before every valve reported.");
                    return;
                }

                if (!shutdown.Result)
                {
                    Console.WriteLine(
                        "[Startup Sequence] Shutdown did not close every valve. A later closed pass will retry.");
                }
            }
            catch (AggregateException)
            {
                // The wait itself failed. CloseForShutdownAsync already isolates valve exceptions.
            }
        }
        
        protected override Task OnLoopStartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"[Startup Sequence] Starting startup sequence controller at {LoopFrequencyHz} Hz");
            lock (_stateGate)
                _stateStartTime = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        protected override Task OnLoopStopAsync() => SafeValvesOnStopAsync();

        /// <summary>
        /// Closes the valves and latches the sequence. Returns false when a valve
        /// stayed open. The loop stop ignores that result so a failed close does
        /// not fault the task; <see cref="StopAsync"/> reports it after its own try.
        /// </summary>
        private async Task<bool> SafeValvesOnStopAsync()
        {
            Console.WriteLine("[Startup Sequence] Stopping startup sequence controller");
            // Idle has not opened a valve. Every other state may have, including Running.
            // A Ready running pass does not command the valves; stop still closes them.
            // Latch before the close so the pass still inside a valve command cannot
            // transition back into the sequence when this stop returns.
            bool idle;
            lock (_stateGate)
            {
                idle = _currentState == StartupState.Idle;
                if (!idle)
                    _sequenceClosed = true;
            }

            if (idle)
                return true;

            var closed = await PerformShutdownAsync().ConfigureAwait(false);
            lock (_stateGate)
            {
                if (_currentState != StartupState.Error && _currentState != StartupState.Aborted)
                {
                    _currentState = StartupState.Aborted;
                    _stateStartTime = DateTime.UtcNow;
                }
            }

            return closed;
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
