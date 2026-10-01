using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HB_NLP_Research_Lab.Core.Hardware;

namespace HB_NLP_Research_Lab.Core.Control
{
    /// <summary>
    /// Redundant control system with voting logic
    /// Implements Triple Modular Redundancy (TMR) or N-Modular Redundancy (NMR)
    /// Used by SpaceX, NASA, and other critical aerospace systems
    /// </summary>
    public class RedundantControlSystem : IDisposable
    {
        private readonly List<RealTimeControlLoop> _controlLoops;
        private readonly VotingStrategy _votingStrategy;
        private readonly IActuator _primaryActuator;
        private readonly List<IActuator> _redundantActuators;
        
        // Use volatile to ensure visibility across threads for the flag read in MonitorAndVoteAsync
        private volatile bool _isRunning = false;
        private readonly object _lock = new object();
        private Task? _monitoringTask;

        public int RedundancyLevel => _controlLoops.Count;
        public VotingStrategy Strategy => _votingStrategy;
        public bool IsRunning => _isRunning;
        
        public RedundantControlSystem(
            List<RealTimeControlLoop> controlLoops,
            VotingStrategy votingStrategy,
            IActuator primaryActuator,
            List<IActuator>? redundantActuators = null)
        {
            if (controlLoops == null || controlLoops.Count < 2)
                throw new ArgumentException("At least 2 control loops required for redundancy", nameof(controlLoops));
            
            _controlLoops = controlLoops;
            _votingStrategy = votingStrategy;
            _primaryActuator = primaryActuator ?? throw new ArgumentNullException(nameof(primaryActuator));
            _redundantActuators = redundantActuators ?? new List<IActuator>();
        }
        
        /// <summary>
        /// Start all redundant control loops
        /// </summary>
        public async Task StartAsync()
        {
            lock (_lock)
            {
                if (_isRunning)
                    throw new InvalidOperationException("Redundant control system is already running");
                
                _isRunning = true;
            }
            
            Console.WriteLine($"[Redundant Control] Starting {RedundancyLevel}-redundant control system");
            Console.WriteLine($"[Redundant Control] Voting strategy: {_votingStrategy}");
            
            // Start all control loops in parallel
            var startTasks = _controlLoops.Select(loop => loop.StartAsync()).ToArray();
            await Task.WhenAll(startTasks);
            
            // Start voting/monitoring task; it exits when _isRunning becomes false (see StopAsync)
            _monitoringTask = Task.Run(() => MonitorAndVoteAsync(CancellationToken.None));
        }
        
        /// <summary>
        /// Stop all control loops
        /// </summary>
        public async Task StopAsync()
        {
            lock (_lock)
            {
                if (!_isRunning)
                    return;
                
                _isRunning = false;
            }
            
            Console.WriteLine("[Redundant Control] Stopping redundant control system");
            
            var stopTasks = _controlLoops.Select(loop => loop.StopAsync()).ToArray();
            await Task.WhenAll(stopTasks);

            // Monitoring loop exits when _isRunning is set false above; wait for it (with timeout)
            if (_monitoringTask != null)
            {
                try
                {
                    await _monitoringTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException)
                {
                    Console.WriteLine("[Redundant Control] ⚠️ Monitoring task did not complete within 5s");
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested during shutdown.
                }
                catch (ObjectDisposedException)
                {
                    // Expected if the task or related resources were disposed during shutdown.
                }
            }
        }
        
        /// <summary>
        /// Monitor control loops and vote on outputs
        /// </summary>
        private async Task MonitorAndVoteAsync(CancellationToken cancellationToken)
        {
            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Get outputs from all control loops
                    var outputs = await GetControlOutputsAsync();
                    
                    // Vote on the outputs
                    var votedOutput = VoteOnOutputs(outputs);
                    
                    // Check for faults
                    var faults = DetectFaults(outputs);
                    
                    if (faults.Count > 0)
                    {
                        HandleFaults(faults);
                    }
                    
                    // Apply voted output to primary actuator
                    if (votedOutput.HasValue)
                    {
                        await _primaryActuator.SetPositionAsync(votedOutput.Value, CancellationToken.None);
                        
                        // Also apply to redundant actuators if available
                        foreach (var actuator in _redundantActuators)
                        {
                            await actuator.SetPositionAsync(votedOutput.Value, CancellationToken.None);
                        }
                    }
                    
                    await Task.Delay(10, cancellationToken); // 100 Hz monitoring
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine($"[Redundant Control] ⚠️ Invalid operation in voting: {ex.Message}");
                    await Task.Delay(100, cancellationToken);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NullReferenceException)
                {
                    Console.WriteLine($"[Redundant Control] ⚠️ Data error in voting: {ex.Message}");
                    await Task.Delay(100, cancellationToken);
                }
            }
        }
        
        private async Task<List<double>> GetControlOutputsAsync()
        {
            // In a real system, control loops would expose their current outputs
            // For now, we'll get actuator positions as proxy
            var outputs = new List<double>();
            
            // This is simplified - in production, control loops would expose their outputs
            // For demonstration, we'll use a placeholder
            // Note: In production, would get actual control output from each loop
            for (int i = 0; i < _controlLoops.Count; i++)
            {
                // Would get actual control output from loop
                // For now, use a simulated value
                outputs.Add(0.5); // Placeholder
            }
            
            // Explicitly mark as async operation
            await Task.CompletedTask;
            return outputs;
        }
        
        private double? VoteOnOutputs(List<double> outputs)
        {
            return SelectVote(_votingStrategy, outputs);
        }

        /// <summary>
        /// Selects one finite actuator command, or null to hold.
        /// LINQ Min and Max skip a NaN channel, then Average folds that NaN back in,
        /// so consensus of two healthy channels plus a failed channel commanded NaN.
        /// </summary>
        internal static double? SelectVote(VotingStrategy strategy, IReadOnlyList<double> outputs)
        {
            if (outputs == null || outputs.Count == 0)
                return null;

            return strategy switch
            {
                VotingStrategy.MajorityVote => MajorityVote(outputs),
                VotingStrategy.MedianVote => MedianVote(outputs),
                VotingStrategy.AverageVote => AverageVote(outputs),
                VotingStrategy.MidValueSelect => MidValueSelect(outputs),
                VotingStrategy.Consensus => ConsensusVote(outputs),
                _ => MedianVote(outputs)
            };
        }

        internal static double? MajorityVote(IReadOnlyList<double> outputs)
        {
            if (outputs == null || outputs.Count == 0)
                return null;

            // Group finite values within tolerance. A NaN group used to win
            // because Double.Equals treats NaN as equal to NaN, and the winner
            // was commanded even when it was not a majority of the channels.
            const double tolerance = 0.01;
            var groups = outputs
                .Where(static value => double.IsFinite(value))
                .GroupBy(value => Math.Round(value / tolerance) * tolerance)
                .OrderByDescending(group => group.Count())
                .ToList();

            if (groups.Count == 0)
                return null;

            var winnerCount = groups[0].Count();
            if (winnerCount * 2 <= outputs.Count)
                return null;

            var key = groups[0].Key;
            return double.IsFinite(key) ? key : null;
        }

        internal static double? MedianVote(IReadOnlyList<double> outputs)
        {
            if (outputs == null || outputs.Count == 0)
                return null;

            var sorted = outputs.OrderBy(value => value).ToList();
            int mid = sorted.Count / 2;
            var selected = sorted.Count % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2.0
                : sorted[mid];

            return double.IsFinite(selected) ? selected : null;
        }

        internal static double? AverageVote(IReadOnlyList<double> outputs)
        {
            if (outputs == null || outputs.Count == 0)
                return null;

            double sum = 0;
            foreach (var value in outputs)
            {
                if (!double.IsFinite(value))
                    return null;

                sum += value;
            }

            var average = sum / outputs.Count;
            return double.IsFinite(average) ? average : null;
        }

        internal static double? MidValueSelect(IReadOnlyList<double> outputs)
        {
            if (outputs == null || outputs.Count == 0)
                return null;

            // Middle value of the sorted channels (TMR). A non-finite middle holds.
            var sorted = outputs.OrderBy(value => value).ToList();
            var selected = sorted[sorted.Count / 2];
            return double.IsFinite(selected) ? selected : null;
        }

        internal static double? ConsensusVote(IReadOnlyList<double> outputs)
        {
            if (outputs == null || outputs.Count == 0)
                return null;

            const double tolerance = 0.01;
            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;
            double sum = 0;
            foreach (var value in outputs)
            {
                if (!double.IsFinite(value))
                    return null;

                if (value < min)
                    min = value;
                if (value > max)
                    max = value;
                sum += value;
            }

            var span = max - min;
            if (!double.IsFinite(span) || span > tolerance)
                return null;

            var average = sum / outputs.Count;
            return double.IsFinite(average) ? average : null;
        }

        private List<Fault> DetectFaults(List<double> outputs)
        {
            return DetectChannelFaults(outputs);
        }

        /// <summary>
        /// A non-finite channel is a fault. Comparisons with NaN are false, so the
        /// old deviation check never reported a failed channel, and one NaN made
        /// the mean NaN so healthy outliers were missed too.
        /// </summary>
        internal static List<Fault> DetectChannelFaults(IReadOnlyList<double> outputs)
        {
            var faults = new List<Fault>();
            if (outputs == null || outputs.Count < 2)
                return faults;

            var finite = new List<double>();
            foreach (var value in outputs)
            {
                if (double.IsFinite(value))
                    finite.Add(value);
            }

            double? mean = finite.Count > 0 ? finite.Average() : null;
            var stdDev = 0.0;
            if (finite.Count >= 2 && mean.HasValue)
            {
                var variance = finite.Select(value => Math.Pow(value - mean.Value, 2)).Average();
                stdDev = Math.Sqrt(variance);
            }

            for (int i = 0; i < outputs.Count; i++)
            {
                var value = outputs[i];
                if (!double.IsFinite(value))
                {
                    faults.Add(new Fault
                    {
                        ControllerIndex = i,
                        OutputValue = value,
                        ExpectedValue = mean ?? double.NaN,
                        Deviation = double.PositiveInfinity,
                        Timestamp = DateTime.UtcNow
                    });
                    continue;
                }

                if (!mean.HasValue || !double.IsFinite(stdDev) || stdDev <= 0.001)
                    continue;

                var deviation = Math.Abs(value - mean.Value);
                if (deviation > 3 * stdDev)
                {
                    faults.Add(new Fault
                    {
                        ControllerIndex = i,
                        OutputValue = value,
                        ExpectedValue = mean.Value,
                        Deviation = deviation,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            return faults;
        }
        
        private void HandleFaults(List<Fault> faults)
        {
            foreach (var fault in faults)
            {
                Console.WriteLine($"[Redundant Control] ⚠️ Fault detected in controller {fault.ControllerIndex}: " +
                    $"output={fault.OutputValue:F3}, expected={fault.ExpectedValue:F3}, deviation={fault.Deviation:F3}");
                
                // In production, would:
                // 1. Log fault
                // 2. Isolate faulty controller
                // 3. Switch to backup if needed
                // 4. Alert operators
            }
        }
        
        public void Dispose()
        {
            // Use Task.Run to avoid deadlocks when disposing from sync context
            try
            {
                Task.Run(async () => await StopAsync().ConfigureAwait(false))
                    .Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Task may have already completed or been cancelled - this is expected during disposal
            }
        }
    }
    
    public enum VotingStrategy
    {
        MajorityVote,    // Strict majority of channels; otherwise hold
        MedianVote,      // Median value
        AverageVote,      // Average of all values
        MidValueSelect,  // Middle value (TMR)
        Consensus        // All must agree
    }
    
    public class Fault
    {
        public int ControllerIndex { get; set; }
        public double OutputValue { get; set; }
        public double ExpectedValue { get; set; }
        public double Deviation { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
