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
        // codeql[cs/missing-disposable-call]: The live source is disposed in Dispose. StopAsync disposes a cancelled source after replacing it.
        protected CancellationTokenSource _cancellationTokenSource;
        protected readonly Stopwatch _stopwatch;
        // codeql[cs/missing-disposable-call]: Disposed in Dispose after the loop has stopped.
        private readonly SemaphoreSlim _lifecycle = new(1, 1);
        
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
        public virtual async Task StartAsync()
        {
            await _lifecycle.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_isRunning)
                    throw new InvalidOperationException("Control loop is already running");

                // Stop cancels the previous source. Task.Run will not invoke the
                // delegate when that token is already cancelled, so IsRunning would
                // stay true and the actuator would never be commanded again.
                EnsureLiveCancellationSource();
                _isRunning = true;
                _controlLoopTask = Task.Run(RunLoopAsync, _cancellationTokenSource.Token);
            }
            finally
            {
                _lifecycle.Release();
            }
        }
        
        /// <summary>
        /// Stop the control loop
        /// </summary>
        public virtual async Task StopAsync()
        {
            await _lifecycle.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!_isRunning)
                    return;

                _isRunning = false;
                var retired = _cancellationTokenSource;
                retired.Cancel();

                try
                {
                    if (_controlLoopTask != null)
                    {
                        await _controlLoopTask.ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling
                }
                finally
                {
                    // CancellationTokenSource cannot be reused after Cancel.
                    _cancellationTokenSource = new CancellationTokenSource();
                    retired.Dispose();
                }
            }
            finally
            {
                _lifecycle.Release();
            }
        }

        private void EnsureLiveCancellationSource()
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
                return;

            var retired = _cancellationTokenSource;
            _cancellationTokenSource = new CancellationTokenSource();
            retired.Dispose();
        }
        
        /// <summary>
        /// Main control loop execution
        /// </summary>
        private async Task RunLoopAsync()
        {
            // Bind this generation to the source it started with. StopAsync replaces
            // the field only after this method returns.
            var cancellationToken = _cancellationTokenSource.Token;
            _stopwatch.Restart();
            var nextLoopTime = _stopwatch.Elapsed;
            
            try
            {
                await OnLoopStartAsync(cancellationToken);
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    var loopStart = _stopwatch.Elapsed;
                    
                    // Execute the control logic
                    await ExecuteControlLoopAsync(cancellationToken);
                    
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
                        await Task.Delay(sleepTime, cancellationToken);
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
            Task? stopTask = null;
            try
            {
                // Run StopAsync on a thread pool thread and wait with a timeout to
                // reduce deadlock risk when Dispose is called from a sync context.
                stopTask = Task.Run(() => StopAsync());
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
                // A timed-out stop still holds the gate. Disposing it here would throw
                // from that stop's release.
                if (stopTask == null || stopTask.IsCompleted)
                    _lifecycle.Dispose();
            }
        }
    }
}
