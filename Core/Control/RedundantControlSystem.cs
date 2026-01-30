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
        private CancellationTokenSource? _monitorCts;
        
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
            
            // Start voting/monitoring task with cancellation so StopAsync can wait for it to exit
            _monitorCts = new CancellationTokenSource();
            _monitoringTask = Task.Run(() => MonitorAndVoteAsync(_monitorCts.Token));
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
            
            // Cancel monitoring loop and wait for it to exit (with timeout)
            if (_monitorCts != null)
            {
                try
                {
                    _monitorCts.Cancel();
                }
                catch (ObjectDisposedException) { }
            }

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
                catch (Exception ex)
                {
                    Console.WriteLine($"[Redundant Control] ⚠️ Error waiting for monitoring task: {ex.Message}");
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
            if (outputs.Count == 0)
                return null;
            
            switch (_votingStrategy)
            {
                case VotingStrategy.MajorityVote:
                    return MajorityVote(outputs);
                
                case VotingStrategy.MedianVote:
                    return MedianVote(outputs);
                
                case VotingStrategy.AverageVote:
                    return AverageVote(outputs);
                
                case VotingStrategy.MidValueSelect:
                    return MidValueSelect(outputs);
                
                case VotingStrategy.Consensus:
                    return ConsensusVote(outputs);
                
                default:
                    return MedianVote(outputs);
            }
        }
        
        private double MajorityVote(List<double> outputs)
        {
            if (outputs == null || outputs.Count == 0)
            {
                throw new InvalidOperationException("MajorityVote requires at least one output value.");
            }

            // Group by value (within tolerance)
            const double tolerance = 0.01;
            var groups = outputs
                .GroupBy(v => Math.Round(v / tolerance) * tolerance)
                .OrderByDescending(g => g.Count())
                .ToList();
            
            if (groups.Count == 0)
            {
                throw new InvalidOperationException("MajorityVote could not form any voting groups.");
            }

            // Return value from largest group
            return groups[0].Key;
        }
        
        private double MedianVote(List<double> outputs)
        {
            var sorted = outputs.OrderBy(v => v).ToList();
            int mid = sorted.Count / 2;
            
            return sorted.Count % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2.0
                : sorted[mid];
        }
        
        private double AverageVote(List<double> outputs)
        {
            return outputs.Average();
        }
        
        private double MidValueSelect(List<double> outputs)
        {
            // Select middle value (used in TMR)
            var sorted = outputs.OrderBy(v => v).ToList();
            return sorted[sorted.Count / 2];
        }
        
        private double? ConsensusVote(List<double> outputs)
        {
            // All outputs must agree within tolerance
            const double tolerance = 0.01;
            var min = outputs.Min();
            var max = outputs.Max();
            
            return max - min <= tolerance
                ? outputs.Average()
                : (double?)null; // No consensus
        }
        
        private List<Fault> DetectFaults(List<double> outputs)
        {
            var faults = new List<Fault>();
            
            if (outputs.Count < 2)
                return faults;
            
            // Calculate statistics
            var mean = outputs.Average();
            var stdDev = Math.Sqrt(outputs.Select(v => Math.Pow(v - mean, 2)).Average());
            
            // Detect outliers (faulty controllers)
            for (int i = 0; i < outputs.Count; i++)
            {
                var deviation = Math.Abs(outputs[i] - mean);
                
                // If deviation is more than 3 standard deviations, consider it a fault
                if (deviation > 3 * stdDev && stdDev > 0.001)
                {
                    faults.Add(new Fault
                    {
                        ControllerIndex = i,
                        OutputValue = outputs[i],
                        ExpectedValue = mean,
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
            finally
            {
                _monitorCts?.Dispose();
                _monitorCts = null;
            }
        }
    }
    
    public enum VotingStrategy
    {
        MajorityVote,    // Most common value
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
