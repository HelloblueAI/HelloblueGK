using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HB_NLP_Research_Lab.Core.Performance
{
    /// <summary>
    /// Advanced performance profiling and optimization system
    /// Tracks execution time, memory usage, and system resources
    /// Used for performance optimization and bottleneck identification
    /// </summary>
    public class PerformanceProfiler : IDisposable
    {
        private readonly ConcurrentDictionary<string, PerformanceMetric> _metrics;
        private readonly Timer _samplingTimer;
        private readonly PerformanceConfiguration _config;
        private bool _isRunning = false;
        
        public PerformanceProfiler(PerformanceConfiguration? config = null)
        {
            _config = config ?? new PerformanceConfiguration();
            _metrics = new ConcurrentDictionary<string, PerformanceMetric>();
            
            var intervalMs = (int)(1000.0 / _config.SamplingFrequencyHz);
            _samplingTimer = new Timer(SampleSystemMetrics, null, Timeout.Infinite, intervalMs);
        }
        
        /// <summary>
        /// Start profiling
        /// </summary>
        public void Start()
        {
            if (_isRunning)
                return;
            
            _isRunning = true;
            Console.WriteLine($"[Performance] Starting profiler at {_config.SamplingFrequencyHz} Hz");
            
            var intervalMs = (int)(1000.0 / _config.SamplingFrequencyHz);
            _samplingTimer.Change(0, intervalMs);
        }
        
        /// <summary>
        /// Stop profiling
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
                return;
            
            _isRunning = false;
            Console.WriteLine("[Performance] Stopping profiler");
            _samplingTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        
        /// <summary>
        /// Measure execution time of an operation
        /// </summary>
        public T Measure<T>(string operationName, Func<T> operation)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = operation();
                stopwatch.Stop();
                
                RecordMetric(operationName, stopwatch.Elapsed.TotalMilliseconds);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                RecordMetric(operationName, stopwatch.Elapsed.TotalMilliseconds, failed: true);
                throw;
            }
        }
        
        /// <summary>
        /// Measure execution time of an async operation
        /// </summary>
        public async Task<T> MeasureAsync<T>(string operationName, Func<Task<T>> operation)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = await operation();
                stopwatch.Stop();
                
                RecordMetric(operationName, stopwatch.Elapsed.TotalMilliseconds);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                RecordMetric(operationName, stopwatch.Elapsed.TotalMilliseconds, failed: true);
                throw;
            }
        }
        
        /// <summary>
        /// Create a performance scope for automatic measurement
        /// </summary>
        public PerformanceScope CreateScope(string operationName)
        {
            return new PerformanceScope(this, operationName);
        }
        
        private void RecordMetric(string operationName, double milliseconds, bool failed = false)
        {
            var metric = _metrics.GetOrAdd(operationName, _ => new PerformanceMetric
            {
                OperationName = operationName,
                CallCount = 0,
                TotalTimeMs = 0,
                MinTimeMs = double.MaxValue,
                MaxTimeMs = 0,
                FailedCount = 0
            });
            
            lock (metric)
            {
                metric.CallCount++;
                metric.TotalTimeMs += milliseconds;
                metric.MinTimeMs = Math.Min(metric.MinTimeMs, milliseconds);
                metric.MaxTimeMs = Math.Max(metric.MaxTimeMs, milliseconds);
                
                if (failed)
                    metric.FailedCount++;
            }
        }
        
        private void SampleSystemMetrics(object? state)
        {
            if (!_isRunning)
                return;
            
            try
            {
                // Sample CPU usage
                var process = Process.GetCurrentProcess();
                var cpuUsage = process.TotalProcessorTime.TotalMilliseconds;
                RecordMetric("System.CPU", cpuUsage);
                
                // Sample memory usage
                var memoryUsage = process.WorkingSet64 / (1024.0 * 1024.0); // MB
                RecordMetric("System.Memory", memoryUsage);
                
                // Sample thread count
                var threadCount = process.Threads.Count;
                RecordMetric("System.Threads", threadCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Performance] ❌ Error sampling metrics: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get performance report for an operation
        /// </summary>
        public PerformanceReport GetReport(string operationName)
        {
            if (!_metrics.TryGetValue(operationName, out var metric))
                return new PerformanceReport { OperationName = operationName };
            
            lock (metric)
            {
                return new PerformanceReport
                {
                    OperationName = operationName,
                    CallCount = metric.CallCount,
                    TotalTimeMs = metric.TotalTimeMs,
                    AverageTimeMs = metric.TotalTimeMs / metric.CallCount,
                    MinTimeMs = metric.MinTimeMs,
                    MaxTimeMs = metric.MaxTimeMs,
                    FailedCount = metric.FailedCount,
                    SuccessRate = (metric.CallCount - metric.FailedCount) / (double)metric.CallCount
                };
            }
        }
        
        /// <summary>
        /// Get all performance reports
        /// </summary>
        public List<PerformanceReport> GetAllReports()
        {
            return _metrics.Keys.Select(GetReport).ToList();
        }
        
        /// <summary>
        /// Get slowest operations
        /// </summary>
        public List<PerformanceReport> GetSlowestOperations(int count = 10)
        {
            return GetAllReports()
                .OrderByDescending(r => r.AverageTimeMs)
                .Take(count)
                .ToList();
        }
        
        /// <summary>
        /// Reset all metrics
        /// </summary>
        public void Reset()
        {
            _metrics.Clear();
            Console.WriteLine("[Performance] Metrics reset");
        }
        
        public void Dispose()
        {
            Stop();
            _samplingTimer?.Dispose();
        }
    }
    
    public class PerformanceScope : IDisposable
    {
        private readonly PerformanceProfiler _profiler;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;
        
        public PerformanceScope(PerformanceProfiler profiler, string operationName)
        {
            _profiler = profiler;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
        }
        
        public void Dispose()
        {
            _stopwatch.Stop();
            _profiler.RecordMetric(_operationName, _stopwatch.Elapsed.TotalMilliseconds);
        }
    }
    
    public class PerformanceMetric
    {
        public string OperationName { get; set; } = string.Empty;
        public long CallCount { get; set; }
        public double TotalTimeMs { get; set; }
        public double MinTimeMs { get; set; }
        public double MaxTimeMs { get; set; }
        public long FailedCount { get; set; }
    }
    
    public class PerformanceReport
    {
        public string OperationName { get; set; } = string.Empty;
        public long CallCount { get; set; }
        public double TotalTimeMs { get; set; }
        public double AverageTimeMs { get; set; }
        public double MinTimeMs { get; set; }
        public double MaxTimeMs { get; set; }
        public long FailedCount { get; set; }
        public double SuccessRate { get; set; }
    }
    
    public class PerformanceConfiguration
    {
        public int SamplingFrequencyHz { get; set; } = 10;
        public bool EnableDetailedMetrics { get; set; } = true;
        public TimeSpan MetricsRetentionPeriod { get; set; } = TimeSpan.FromHours(1);
    }
    
    // Extension method for easy profiling
    public static class PerformanceProfilerExtensions
    {
        public static T Profile<T>(this PerformanceProfiler profiler, string operationName, Func<T> operation)
        {
            return profiler.Measure(operationName, operation);
        }
        
        public static async Task<T> ProfileAsync<T>(this PerformanceProfiler profiler, string operationName, Func<Task<T>> operation)
        {
            return await profiler.MeasureAsync(operationName, operation);
        }
    }
}
