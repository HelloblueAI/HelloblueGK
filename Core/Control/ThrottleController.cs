using System;
using System.Threading;
using System.Threading.Tasks;
using HB_NLP_Research_Lab.Core.Hardware;

namespace HB_NLP_Research_Lab.Core.Control
{
    /// <summary>
    /// Real-time throttle controller for engine thrust regulation
    /// Runs at 100 Hz for responsive control
    /// </summary>
    public class ThrottleController : RealTimeControlLoop
    {
        private readonly IActuator _throttleActuator;
        private readonly ISensor<double> _thrustSensor;
        private readonly ISensor<double> _chamberPressureSensor;
        
        private double _commandedThrottle = 0.0; // 0.0 to 1.0
        private double _currentThrottle = 0.0;
        private double _targetThrust = 0.0; // Newtons
        
        // PID Controller parameters
        private readonly double _kp = 0.5;  // Proportional gain
        private readonly double _ki = 0.1;  // Integral gain
        private readonly double _kd = 0.05; // Derivative gain
        
        private double _integral = 0.0;
        private double _lastError = 0.0;
        private DateTime _lastUpdate = DateTime.UtcNow;
        
        // Safety limits
        private const double MinThrottle = 0.0;
        private const double MaxThrottle = 1.0;
        private const double MaxThrottleRate = 0.1; // per second (10% per second)
        
        public double CommandedThrottle => _commandedThrottle;
        public double CurrentThrottle => _currentThrottle;
        public double TargetThrust => _targetThrust;

        /// <summary>
        /// A thrust sample is evidence only while the sensor is Ready. Disconnected,
        /// Error, Calibrating, Unknown, and Initializing can still return a cached
        /// number. Reading is not Ready: a sample that has not finished is not a measurement.
        /// </summary>
        internal static bool IsSensorReadyForThrottle(SensorStatus status) =>
            status == SensorStatus.Ready;

        /// <summary>
        /// One control pass, the same step the control loop runs.
        /// </summary>
        internal Task ExecuteOnceAsync(CancellationToken cancellationToken = default) =>
            ExecuteControlLoopAsync(cancellationToken);
        
        public ThrottleController(
            IActuator throttleActuator,
            ISensor<double> thrustSensor,
            ISensor<double> chamberPressureSensor,
            int frequencyHz = 100) : base(frequencyHz)
        {
            _throttleActuator = throttleActuator ?? throw new ArgumentNullException(nameof(throttleActuator));
            _thrustSensor = thrustSensor ?? throw new ArgumentNullException(nameof(thrustSensor));
            _chamberPressureSensor = chamberPressureSensor ?? throw new ArgumentNullException(nameof(chamberPressureSensor));
        }
        
        /// <summary>
        /// Set target throttle position (0.0 to 1.0)
        /// </summary>
        public void SetThrottle(double throttle)
        {
            // NaN is neither below the minimum nor above the maximum, so the range check
            // alone used to store it and the open-loop path commanded that NaN.
            if (!double.IsFinite(throttle) || throttle < MinThrottle || throttle > MaxThrottle)
                throw new ArgumentOutOfRangeException(
                    nameof(throttle),
                    $"Throttle must be a finite value between {MinThrottle} and {MaxThrottle}");
            
            _commandedThrottle = throttle;
        }
        
        /// <summary>
        /// Set target thrust (Newtons) - controller will adjust throttle to achieve
        /// </summary>
        public void SetTargetThrust(double targetThrust)
        {
            if (!double.IsFinite(targetThrust) || targetThrust < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(targetThrust), "Target thrust must be a finite value >= 0");
            
            _targetThrust = targetThrust;
        }
        
        protected override async Task ExecuteControlLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Read current sensor values. ReadAsync may update Status, so the Ready
                // gate is applied to the sample that was just returned. A thrown read,
                // including a timeout that is not this loop stopping, is not a sample.
                // The old catch logged that exception and left the last command applied.
                // A Ready sensor can still return NaN or infinity. Every comparison with
                // NaN is false, so that sample used to become zero thrust error and the
                // loop held the last open command.
                double currentThrust = 0;
                var thrustReadFailed = false;
                var thrustSampleNonFinite = false;
                try
                {
                    currentThrust = await _thrustSensor.ReadAsync(cancellationToken).ConfigureAwait(false);
                    thrustSampleNonFinite = !double.IsFinite(currentThrust);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                // codeql[generic-catch-clause]: A failed thrust read must not escape into the loop-stop path.
                catch (Exception ex)
                {
                    OnControlError(ex);
                    thrustReadFailed = true;
                }

                // Note: Chamber pressure available but not used in current control algorithm
                // await _chamberPressureSensor.ReadAsync(cancellationToken);
                
                // Closed-loop control uses the thrust sample. A disconnected sensor can
                // return a cached in-band number, a failed read returns nothing, and a
                // non-finite number is not a thrust. None of those may drive the command.
                // The safe command is closed; the rate limiter steps toward it. Open-loop
                // does not use the sample, so a direct throttle command stands even when
                // the read throws or the sample is non-finite.
                double throttleCommand;
                if (_targetThrust > 0 &&
                    (thrustReadFailed ||
                     thrustSampleNonFinite ||
                     !IsSensorReadyForThrottle(_thrustSensor.Status)))
                {
                    Console.WriteLine(thrustReadFailed
                        ? "[Throttle Controller] Thrust read failed. Commanding closed."
                        : thrustSampleNonFinite
                            ? "[Throttle Controller] Thrust sample is not finite. Commanding closed."
                            : $"[Throttle Controller] Thrust sensor not ready: {_thrustSensor.Status}. Commanding closed.");
                    throttleCommand = MinThrottle;
                }
                else
                {
                    throttleCommand = _targetThrust > 0
                        ? CalculateThrottleFromThrust(currentThrust, _targetThrust)
                        : _commandedThrottle;
                }
                
                // Rate limit, then clamp. A non-finite command holds the last finite position
                // instead of passing NaN through Math.Clamp, which returns NaN.
                throttleCommand = ConstrainThrottleCommand(throttleCommand);
                
                // A false return, a timeout, or any other thrown command did not land.
                // Leave the last accepted position in place so the next pass retries
                // the same step. A throw is the same miss as a timeout: the loop stays
                // up and the open command is not left without a rejection.
                var success = await TrySetThrottlePositionAsync(throttleCommand, cancellationToken);
                
                if (success)
                {
                    _currentThrottle = throttleCommand;
                }
                else
                {
                    // Actuator failed - log error
                    OnActuatorError();
                }
            }
            catch (OperationCanceledException)
            {
                // The loop token was cancelled. Stop owns the safe close.
                throw;
            }
            catch (InvalidOperationException ex)
            {
                // Invalid operation (e.g., actuator not ready)
                OnControlError(ex);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NullReferenceException)
            {
                // Data validation errors
                OnControlError(ex);
            }
            // codeql[generic-catch-clause]: Intentional final catch-all for safety - all specific exceptions handled above
            catch (Exception ex)
            {
                // Catch-all for unexpected errors
                OnControlError(ex);
            }
        }
        
        private double CalculateThrottleFromThrust(double currentThrust, double targetThrust)
        {
            var now = DateTime.UtcNow;
            var dt = (now - _lastUpdate).TotalSeconds;
            _lastUpdate = now;
            
            if (dt <= 0 || dt > 1.0) // Sanity check
                dt = 1.0 / LoopFrequencyHz;
            
            // Error is a fraction of the commanded thrust. Subtracting Newtons and adding
            // the result to a 0�1 throttle saturates the rate limit for any real miss:
            // a 100 N error on a 1 MN engine is 0.01%, but 0.5 * 100 is already fifty
            // full-scale throttle commands.
            var error = NormalizedThrustError(currentThrust, targetThrust);
            
            // Proportional term
            var pTerm = _kp * error;
            
            // Integral term (with anti-windup)
            _integral += error * dt;
            _integral = Math.Clamp(_integral, -1.0, 1.0); // Anti-windup
            var iTerm = _ki * _integral;
            
            // Derivative term
            var dTerm = _kd * (error - _lastError) / dt;
            _lastError = error;
            
            // Calculate throttle adjustment
            var throttleAdjustment = pTerm + iTerm + dTerm;
            
            // Convert to throttle command (simplified - would need engine model)
            // For now, assume linear relationship
            var baseThrottle = _currentThrottle;
            var newThrottle = baseThrottle + throttleAdjustment;
            
            return newThrottle;
        }

        /// <summary>
        /// Dimensionless thrust error, (target - current) / target. Non-finite readings and a
        /// non-positive target contribute no error, so a NaN sensor cannot command NaN throttle.
        /// </summary>
        internal static double NormalizedThrustError(double currentThrust, double targetThrust)
        {
            if (!double.IsFinite(currentThrust) || !double.IsFinite(targetThrust) || targetThrust <= 0)
            {
                return 0;
            }

            var error = (targetThrust - currentThrust) / targetThrust;
            return double.IsFinite(error) ? error : 0;
        }

        /// <summary>
        /// Steps <paramref name="targetThrottle"/> toward a finite position no faster than
        /// <paramref name="maxChange"/>. A non-finite target holds the last finite position.
        /// A non-finite current position is treated as closed, so it cannot skip the rate limit.
        /// </summary>
        internal static double HoldOrStep(double currentThrottle, double targetThrottle, double maxChange)
        {
            var current = double.IsFinite(currentThrottle) ? currentThrottle : MinThrottle;
            if (!double.IsFinite(targetThrottle))
            {
                return current;
            }

            var change = targetThrottle - current;
            if (Math.Abs(change) > maxChange)
            {
                return current + (Math.Sign(change) * maxChange);
            }

            return targetThrottle;
        }

        internal double ConstrainThrottleCommand(double targetThrottle)
        {
            var maxChange = MaxThrottleRate / LoopFrequencyHz;
            var stepped = HoldOrStep(_currentThrottle, targetThrottle, maxChange);
            return Math.Clamp(stepped, MinThrottle, MaxThrottle);
        }

        /// <summary>
        /// Commands the actuator. A timeout is a <see cref="TaskCanceledException"/>,
        /// which is an <see cref="OperationCanceledException"/>. The base loop treats
        /// every exception of that type as a clean stop, leaves <c>IsRunning</c> true,
        /// and closes only once. An actuator that times out on its own token is not
        /// the loop stopping, so the miss stays in this pass and the next one retries.
        /// Any other throw used to reach the outer log-only catch. That pass did not
        /// count an actuator error, so a close that failed left the last open command
        /// applied without the same retry accounting as a timeout.
        /// </summary>
        private async Task<bool> TrySetThrottlePositionAsync(
            double throttleCommand,
            CancellationToken cancellationToken)
        {
            try
            {
                return await _throttleActuator.SetPositionAsync(throttleCommand, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                OnControlError(ex);
                return false;
            }
            // codeql[generic-catch-clause]: A thrown throttle command is a miss, not a loop stop.
            catch (Exception ex)
            {
                OnControlError(ex);
                return false;
            }
        }
        
        /// <summary>
        /// Stop the control loop and close the throttle.
        /// The base stop cancels the loop token; if that cancellation wins before the
        /// loop body starts, <see cref="OnLoopStopAsync"/> never runs.
        /// </summary>
        public override async Task StopAsync()
        {
            await base.StopAsync().ConfigureAwait(false);
            await SafeThrottleOnStopAsync().ConfigureAwait(false);
        }

        protected override Task OnLoopStartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"[Throttle Controller] Starting throttle control loop at {LoopFrequencyHz} Hz");
            _lastUpdate = DateTime.UtcNow;
            return Task.CompletedTask;
        }
        
        protected override Task OnLoopStopAsync() => SafeThrottleOnStopAsync();

        private async Task SafeThrottleOnStopAsync()
        {
            Console.WriteLine("[Throttle Controller] Stopping throttle control loop");
            // Closed is the safe position. Remember it, or the next rate limit steps
            // from the pre-stop command and the actuator jumps open.
            try
            {
                var closed = await _throttleActuator.SetPositionAsync(MinThrottle, CancellationToken.None)
                    .ConfigureAwait(false);
                if (closed)
                {
                    _currentThrottle = MinThrottle;
                }
            }
            // codeql[generic-catch-clause]: Intentional final catch-all for shutdown safety - all specific exceptions handled above
            // Shutdown must be resilient and not throw exceptions per .NET guidelines
            catch (Exception ex)
            {
                // Log but don't throw - shutdown should be resilient
                Console.WriteLine($"[Throttle Controller] Error setting safe position: {ex.Message}");
            }
        }
        
        protected virtual void OnActuatorError()
        {
            Console.WriteLine("[Throttle Controller] ⚠︝ Actuator error detected");
        }
        
        protected virtual void OnControlError(Exception ex)
        {
            Console.WriteLine($"[Throttle Controller] ❌ Control error: {ex.Message}");
        }
    }
}
