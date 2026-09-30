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
                // Read current sensor values
                var currentThrust = await _thrustSensor.ReadAsync(cancellationToken);
                // Note: Chamber pressure available but not used in current control algorithm
                // await _chamberPressureSensor.ReadAsync(cancellationToken);
                
                // Calculate throttle command
                double throttleCommand;
                
                // Use ternary for cleaner code
                throttleCommand = _targetThrust > 0
                    ? CalculateThrottleFromThrust(currentThrust, _targetThrust)  // Closed-loop control
                    : _commandedThrottle;  // Open-loop control
                
                // Rate limit, then clamp. A non-finite command holds the last finite position
                // instead of passing NaN through Math.Clamp, which returns NaN.
                throttleCommand = ConstrainThrottleCommand(throttleCommand);
                
                // Send command to actuator
                var success = await _throttleActuator.SetPositionAsync(throttleCommand, cancellationToken);
                
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
                // Operation was cancelled - expected behavior
                throw; // Re-throw cancellation
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
        
        protected override Task OnLoopStartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"[Throttle Controller] Starting throttle control loop at {LoopFrequencyHz} Hz");
            _lastUpdate = DateTime.UtcNow;
            return Task.CompletedTask;
        }
        
        protected override async Task OnLoopStopAsync()
        {
            Console.WriteLine("[Throttle Controller] Stopping throttle control loop");
            // Set throttle to safe position (0 or minimum)
            try
            {
                await _throttleActuator.SetPositionAsync(0.0, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            // codeql[generic-catch-clause]: Intentional final catch-all for shutdown safety - all specific exceptions handled above
            // Shutdown must be resilient and not throw exceptions per .NET guidelines
            catch (Exception ex)
            {
                // Log but don't throw - shutdown should be resilient
                Console.WriteLine($"[Throttle Controller] ⚠︝ Error setting safe position: {ex.Message}");
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
