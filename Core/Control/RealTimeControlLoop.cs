using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HB_NLP_Research_Lab.Core.Control
{
    /// <summary>
    /// Base class for real-time control loops with deterministic timing
    /// </summary>
    public abstract class RealTimeControlLoop : IDisposable
    {
        protected readonly int LoopFrequencyHz;
        protected readonly TimeSpan LoopPeriod;
        // codeql[cs/missing-disposable-call]: CancellationTokenSource is a field that must live for object lifetime, properly disposed in finally block
        protected readonly CancellationTokenSource _cancellationTokenSource;
        protected readonly Stopwatch _stopwatch;
        
        private Task? _controlLoopTask;
        private bool _isRunning = false;
        private long _timingViolations = 0;
        private long _totalIterations = 0;
        
        public bool IsRunning => _isRunning;
        public long TimingViolations => _timingViolations;
        public long TotalIterations => _totalIterations;
        public double AverageLoopTimeMs { get; private set; }
        public double MaxLoopTimeMs { get; private set; }
        
        protected RealTimeControlLoop(int frequencyHz)
        {
            if (frequencyHz <= 0 || frequencyHz > 10000)
                throw new ArgumentException("Frequency must be between 1 and 10000 Hz", nameof(frequencyHz));
            
            LoopFrequencyHz = frequencyHz;
            LoopPeriod = TimeSpan.FromMilliseconds(1000.0 / frequencyHz);
            _cancellationTokenSource = new CancellationTokenSource();
            _stopwatch = new Stopwatch();
        }
        
        /// <summary>
        /// Start the control loop
        /// </summary>
        public virtual Task StartAsync()
        {
            if (_isRunning)
                throw new InvalidOperationException("Control loop is already running");
            
            _isRunning = true;
            _controlLoopTask = Task.Run(RunLoopAsync, _cancellationTokenSource.Token);
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Stop the control loop
        /// </summary>
        public virtual async Task StopAsync()
        {
            if (!_isRunning)
                return;
            
            _isRunning = false;
            _cancellationTokenSource.Cancel();
            
            if (_controlLoopTask != null)
            {
                try
                {
                    await _controlLoopTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling
                }
            }
        }
        
        /// <summary>
        /// Main control loop execution
        /// </summary>
        private async Task RunLoopAsync()
        {
            _stopwatch.Restart();
            var nextLoopTime = _stopwatch.Elapsed;
            
            try
            {
                await OnLoopStartAsync(_cancellationTokenSource.Token);
                
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var loopStart = _stopwatch.Elapsed;
                    
                    // Execute the control logic
                    await ExecuteControlLoopAsync(_cancellationTokenSource.Token);
                    
                    _totalIterations++;
                    
                    // Calculate timing
                    var elapsed = _stopwatch.Elapsed - loopStart;
                    var elapsedMs = elapsed.TotalMilliseconds;
                    
                    // Update statistics
                    UpdateStatistics(elapsedMs);
                    
                    // Check for timing violations
                    if (elapsed > LoopPeriod)
                    {
                        _timingViolations++;
                        OnTimingViolation(elapsed, LoopPeriod);
                    }
                    
                    // Calculate next loop time
                    nextLoopTime += LoopPeriod;
                    var sleepTime = nextLoopTime - _stopwatch.Elapsed;
                    
                    // Sleep until next iteration
                    if (sleepTime > TimeSpan.Zero)
                    {
                        await Task.Delay(sleepTime, _cancellationTokenSource.Token);
                    }
                    else
                    {
                        // We're behind schedule, log warning
                        OnScheduleOverrun(sleepTime);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            finally
            {
                await OnLoopStopAsync();
                _stopwatch.Stop();
            }
        }
        
        private void UpdateStatistics(double elapsedMs)
        {
            // Simple moving average
            if (_totalIterations == 1)
            {
                AverageLoopTimeMs = elapsedMs;
                MaxLoopTimeMs = elapsedMs;
            }
            else
            {
                AverageLoopTimeMs = (AverageLoopTimeMs * (_totalIterations - 1) + elapsedMs) / _totalIterations;
                if (elapsedMs > MaxLoopTimeMs)
                    MaxLoopTimeMs = elapsedMs;
            }
        }
        
        /// <summary>
        /// Override to implement control logic
        /// </summary>
        protected abstract Task ExecuteControlLoopAsync(CancellationToken cancellationToken);
        
        /// <summary>
        /// Called when loop starts (override for initialization)
        /// </summary>
        protected virtual Task OnLoopStartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Called when loop stops (override for cleanup)
        /// </summary>
        protected virtual Task OnLoopStopAsync()
        {
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Called when timing violation occurs (override for logging/alerts)
        /// </summary>
        protected virtual void OnTimingViolation(TimeSpan actual, TimeSpan expected)
        {
            // Default: log warning
            Console.WriteLine($"[Control Loop] ⚠️ Timing violation: {actual.TotalMilliseconds:F2}ms > {expected.TotalMilliseconds:F2}ms");
        }
        
        /// <summary>
        /// Called when schedule overrun occurs (override for logging/alerts)
        /// </summary>
        protected virtual void OnScheduleOverrun(TimeSpan overrun)
        {
            // Default: log warning
            Console.WriteLine($"[Control Loop] ⚠️ Schedule overrun: {overrun.TotalMilliseconds:F2}ms behind");
        }
        
        public void Dispose()
        {
            // Use ConfigureAwait(false) to avoid deadlocks when called from sync context
            // Add timeout to prevent indefinite blocking
            try
            {
                // Run StopAsync on a thread pool thread and wait with a timeout to
                // reduce deadlock risk when Dispose is called from a sync context.
                var stopTask = Task.Run(() => StopAsync());
                if (!stopTask.Wait(TimeSpan.FromSeconds(5)))
                {
                    System.Diagnostics.Debug.WriteLine("Timeout while waiting for RealTimeControlLoop.StopAsync to complete during disposal.");
                }
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException || ex.InnerException is ObjectDisposedException)
            {
                // Expected during shutdown; ignore these inner exceptions
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping - ignore
            }
            catch (ObjectDisposedException)
            {
                // Already disposed - ignore
            }
            // codeql[cs/catch-of-all-exceptions]: Generic catch clause is intentional for disposal safety
            // Disposal must not throw exceptions per .NET guidelines, so we catch all remaining exceptions
            catch (Exception ex)
            {
                // Log but don't throw - disposal should not throw exceptions
                // The cancellation token will be disposed regardless
                System.Diagnostics.Debug.WriteLine($"Exception during disposal: {ex.Message}");
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
            }
        }
    }
}
