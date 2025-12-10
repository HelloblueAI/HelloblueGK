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
            if (throttle < MinThrottle || throttle > MaxThrottle)
                throw new ArgumentOutOfRangeException(nameof(throttle), $"Throttle must be between {MinThrottle} and {MaxThrottle}");
            
            _commandedThrottle = throttle;
        }
        
        /// <summary>
        /// Set target thrust (Newtons) - controller will adjust throttle to achieve
        /// </summary>
        public void SetTargetThrust(double targetThrust)
        {
            if (targetThrust < 0)
                throw new ArgumentOutOfRangeException(nameof(targetThrust), "Target thrust must be >= 0");
            
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
                
                // Apply rate limiting for safety
                throttleCommand = ApplyRateLimit(throttleCommand);
                
                // Apply safety limits
                throttleCommand = Math.Clamp(throttleCommand, MinThrottle, MaxThrottle);
                
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
            
            // Calculate error
            var error = targetThrust - currentThrust;
            
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
        
        private double ApplyRateLimit(double targetThrottle)
        {
            var maxChange = MaxThrottleRate / LoopFrequencyHz;
            var change = targetThrottle - _currentThrottle;
            
            if (Math.Abs(change) > maxChange)
            {
                return _currentThrottle + Math.Sign(change) * maxChange;
            }
            
            return targetThrottle;
        }
        
        protected override Task OnLoopStartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"[Throttle Controller] Starting throttle control loop at {LoopFrequencyHz} Hz");
            _lastUpdate = DateTime.UtcNow;
            return Task.CompletedTask;
        }
        
        protected override Task OnLoopStopAsync()
        {
            Console.WriteLine("[Throttle Controller] Stopping throttle control loop");
            // Set throttle to safe position (0 or minimum)
            _throttleActuator.SetPositionAsync(0.0, CancellationToken.None).Wait(TimeSpan.FromSeconds(1));
            return Task.CompletedTask;
        }
        
        protected virtual void OnActuatorError()
        {
            Console.WriteLine("[Throttle Controller] ⚠️ Actuator error detected");
        }
        
        protected virtual void OnControlError(Exception ex)
        {
            Console.WriteLine($"[Throttle Controller] ❌ Control error: {ex.Message}");
        }
    }
}
