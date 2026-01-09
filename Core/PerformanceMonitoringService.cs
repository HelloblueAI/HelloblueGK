using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace HB_NLP_Research_Lab.Core
{
    /// <summary>
    /// Advanced performance monitoring service for aerospace engine operations
    /// Provides real-time metrics, profiling, and performance analytics
    /// </summary>
    public class PerformanceMonitoringService : IHostedService, IDisposable
    {
        private readonly ILogger<PerformanceMonitoringService> _logger;
        private readonly ConcurrentDictionary<string, PerformanceMetric> _metrics;
        private readonly ConcurrentDictionary<string, List<PerformanceSample>> _samples;
        private readonly Timer _collectionTimer;
        private readonly PerformanceCounter? _cpuCounter;
        private readonly PerformanceCounter? _memoryCounter;
        private readonly Process _currentProcess;
        private bool _disposed = false;
        private bool _isStarted = false;

        public PerformanceMonitoringService(ILogger<PerformanceMonitoringService> logger)
        {
            _logger = logger;
            _metrics = new ConcurrentDictionary<string, PerformanceMetric>();
            _samples = new ConcurrentDictionary<string, List<PerformanceSample>>();
            _currentProcess = Process.GetCurrentProcess();

            // Initialize performance counters (Windows only)
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Performance counters not available on this platform");
            }

            // Create timer but don't start it yet - will be started in StartAsync
            _collectionTimer = new Timer(CollectMetrics, null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_isStarted)
            {
                _logger.LogWarning("Performance monitoring service already started");
                return;
            }

            _logger.LogInformation("Performance monitoring service started");
            // Start periodic collection
            _collectionTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(5));
            _isStarted = true;
            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (!_isStarted)
            {
                return;
            }

            _logger.LogInformation("Performance monitoring service stopped");
            // Stop the timer
            _collectionTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _isStarted = false;
            await Task.CompletedTask;
        }

        public void RecordMetric(string name, double value, string category = "General")
        {
            _metrics.AddOrUpdate(name, 
                new PerformanceMetric { Name = name, Category = category, Value = value, Count = 1, LastUpdated = DateTime.UtcNow },
                (key, existing) => 
                {
                    existing.Value = value;
                    existing.Count++;
                    existing.LastUpdated = DateTime.UtcNow;
                    return existing;
                });

            // Store sample for trend analysis
            var samples = _samples.GetOrAdd(name, _ => new List<PerformanceSample>());
            samples.Add(new PerformanceSample { Timestamp = DateTime.UtcNow, Value = value });
            
            // Keep only last 100 samples per metric
            if (samples.Count > 100)
            {
                samples.RemoveRange(0, samples.Count - 100);
            }
        }

        public void RecordExecutionTime(string operationName, TimeSpan duration, string category = "Performance")
        {
            var metricName = $"{operationName}_Duration";
            RecordMetric(metricName, duration.TotalMilliseconds, category);
            
            // Also record as throughput if applicable
            if (duration.TotalSeconds > 0)
            {
                var throughput = 1.0 / duration.TotalSeconds;
                RecordMetric($"{operationName}_Throughput", throughput, category);
            }
        }

        public async Task<PerformanceReport> GeneratePerformanceReportAsync()
        {
            var report = new PerformanceReport
            {
                GeneratedAt = DateTime.UtcNow,
                SystemMetrics = await GetSystemMetricsAsync(),
                ApplicationMetrics = GetApplicationMetrics(),
                PerformanceTrends = GetPerformanceTrends(),
                Recommendations = GenerateRecommendations()
            };

            return report;
        }

        public async Task<PerformanceSnapshot> GetCurrentSnapshotAsync()
        {
            var snapshot = new PerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                SystemMetrics = await GetSystemMetricsAsync(),
                TopMetrics = GetTopMetrics(10),
                ActiveOperations = GetActiveOperations()
            };

            return snapshot;
        }

        public PerformanceMetric? GetMetric(string name)
        {
            return _metrics.TryGetValue(name, out var metric) ? metric : null;
        }

        public IEnumerable<PerformanceMetric> GetMetricsByCategory(string category)
        {
            return _metrics.Values.Where(m => m.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<PerformanceTrend> GetTrendAnalysis(string metricName, TimeSpan lookbackPeriod)
        {
            if (!_samples.TryGetValue(metricName, out var samples))
                return Enumerable.Empty<PerformanceTrend>();

            var cutoff = DateTime.UtcNow - lookbackPeriod;
            var recentSamples = samples.Where(s => s.Timestamp >= cutoff).ToList();

            if (recentSamples.Count < 2)
                return Enumerable.Empty<PerformanceTrend>();

            var trends = new List<PerformanceTrend>();
            
            // Calculate trend direction
            var firstHalf = recentSamples.Take(recentSamples.Count / 2).Average(s => s.Value);
            var secondHalf = recentSamples.Skip(recentSamples.Count / 2).Average(s => s.Value);
            
            var trend = new PerformanceTrend
            {
                MetricName = metricName,
                Direction = secondHalf > firstHalf ? TrendDirection.Increasing : 
                           secondHalf < firstHalf ? TrendDirection.Decreasing : TrendDirection.Stable,
                // Use epsilon comparison for floating point equality check
                ChangePercentage = Math.Abs(firstHalf) > double.Epsilon ? ((secondHalf - firstHalf) / firstHalf) * 100 : 0,
                AverageValue = recentSamples.Average(s => s.Value),
                MinValue = recentSamples.Min(s => s.Value),
                MaxValue = recentSamples.Max(s => s.Value),
                SampleCount = recentSamples.Count
            };

            trends.Add(trend);
            return trends;
        }

        private void CollectMetrics(object? state)
        {
            try
            {
                // Collect system metrics (Windows only for performance counters)
                if (_cpuCounter != null && _memoryCounter != null)
                {
#pragma warning disable CA1416 // Platform-specific API
                    var cpuUsage = _cpuCounter.NextValue();
                    var availableMemory = _memoryCounter.NextValue();
#pragma warning restore CA1416
                    RecordMetric("System_CPU_Usage", cpuUsage, "System");
                    RecordMetric("System_Available_Memory", availableMemory, "System");
                }

                // Collect process metrics (cross-platform)
                var workingSet = _currentProcess.WorkingSet64 / 1024 / 1024; // MB
                var privateMemory = _currentProcess.PrivateMemorySize64 / 1024 / 1024; // MB

                RecordMetric("Process_Working_Set", workingSet, "Process");
                RecordMetric("Process_Private_Memory", privateMemory, "Process");
                RecordMetric("Process_Thread_Count", _currentProcess.Threads.Count, "Process");
                RecordMetric("Process_Handle_Count", _currentProcess.HandleCount, "Process");

                // Collect GC metrics
                var gen0Collections = GC.CollectionCount(0);
                var gen1Collections = GC.CollectionCount(1);
                var gen2Collections = GC.CollectionCount(2);

                RecordMetric("GC_Gen0_Collections", gen0Collections, "Memory");
                RecordMetric("GC_Gen1_Collections", gen1Collections, "Memory");
                RecordMetric("GC_Gen2_Collections", gen2Collections, "Memory");
                // Explicit cast to double to avoid precision loss warning (intentional conversion from bytes to MB)
                RecordMetric("GC_Total_Memory", (double)GC.GetTotalMemory(false) / 1024.0 / 1024.0, "Memory"); // MB
            }
            catch (InvalidOperationException ex)
            {
                // Performance counter may not be available on all platforms
                _logger.LogWarning(ex, "Performance counter unavailable - metrics collection skipped");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // Windows-specific performance counter errors
                _logger.LogWarning(ex, "Windows performance counter error - metrics collection skipped");
            }
            // codeql[generic-catch-clause]: Intentional final catch-all for safety - all specific exceptions handled above
            catch (Exception ex)
            {
                // Catch-all for unexpected errors to prevent service crash
                // Logging the full exception for debugging while maintaining service availability
                _logger.LogError(ex, "Unexpected error collecting performance metrics");
            }
        }

        // Quick synchronous operation maintained as async for consistency with async chain
        // CS1998 warning suppressed - this method is intentionally synchronous but part of async pattern
#pragma warning disable CS1998 // Async method lacks 'await' operators
        private async Task<SystemMetrics> GetSystemMetricsAsync()
        {
            var metrics = new SystemMetrics
            {
                // Windows-specific performance counters (null on Linux/macOS) - suppress warning
#pragma warning disable CA1416
                CPUUsage = _cpuCounter?.NextValue() ?? 0,
                AvailableMemory = _memoryCounter?.NextValue() ?? 0,
#pragma warning restore CA1416
                // Explicit cast to double for division to avoid precision loss warning (intentional conversion from bytes to MB)
                ProcessWorkingSet = (double)_currentProcess.WorkingSet64 / 1024.0 / 1024.0,
                ProcessPrivateMemory = (double)_currentProcess.PrivateMemorySize64 / 1024.0 / 1024.0,
                ThreadCount = _currentProcess.Threads.Count,
                HandleCount = _currentProcess.HandleCount,
                GCMemory = (double)GC.GetTotalMemory(false) / 1024.0 / 1024.0,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2)
            };
            
            return metrics;
        }
#pragma warning restore CS1998

        private ApplicationMetrics GetApplicationMetrics()
        {
            return new ApplicationMetrics
            {
                TotalMetrics = _metrics.Count,
                TotalSamples = _samples.Values.Sum(s => s.Count),
                MetricCategories = _metrics.Values.Select(m => m.Category).Distinct().ToList(),
                LastUpdate = _metrics.Values.Any() ? _metrics.Values.Max(m => m.LastUpdated) : DateTime.UtcNow
            };
        }

        private List<PerformanceTrend> GetPerformanceTrends()
        {
            var trends = new List<PerformanceTrend>();
            var lookbackPeriod = TimeSpan.FromMinutes(5);

            foreach (var metricName in _samples.Keys)
            {
                var trend = GetTrendAnalysis(metricName, lookbackPeriod).FirstOrDefault();
                if (trend != null)
                {
                    trends.Add(trend);
                }
            }

            return trends.OrderByDescending(t => Math.Abs(t.ChangePercentage)).Take(10).ToList();
        }

        private List<string> GetTopMetrics(int count)
        {
            return _metrics.Values
                .OrderByDescending(m => m.Count)
                .ThenByDescending(m => m.LastUpdated)
                .Take(count)
                .Select(m => m.Name)
                .ToList();
        }

        private List<string> GetActiveOperations()
        {
            // Return metrics that have been updated recently (within last minute)
            var recent = DateTime.UtcNow.AddMinutes(-1);
            return _metrics.Values
                .Where(m => m.LastUpdated >= recent)
                .OrderByDescending(m => m.LastUpdated)
                .Take(5)
                .Select(m => m.Name)
                .ToList();
        }

        private List<string> GenerateRecommendations()
        {
            var recommendations = new List<string>();

            // Check for high CPU usage
            var cpuMetric = GetMetric("System_CPU_Usage");
            if (cpuMetric != null && cpuMetric.Value > 80)
            {
                recommendations.Add("High CPU usage detected. Consider optimizing CPU-intensive operations.");
            }

            // Check for high memory usage
            var memoryMetric = GetMetric("Process_Working_Set");
            if (memoryMetric != null && memoryMetric.Value > 1000) // 1GB
            {
                recommendations.Add("High memory usage detected. Consider implementing memory optimization strategies.");
            }

            // Check for frequent GC collections
            var gcMetric = GetMetric("GC_Gen2_Collections");
            if (gcMetric != null && gcMetric.Count > 10)
            {
                recommendations.Add("Frequent garbage collections detected. Consider object pooling and memory management optimization.");
            }

            // Check for performance trends - Use Where instead of foreach for efficiency
            var criticalTrends = GetPerformanceTrends()
                .Where(trend => trend.Direction == TrendDirection.Increasing && trend.ChangePercentage > 20)
                .Take(3);
            foreach (var trend in criticalTrends)
            {
                recommendations.Add($"Performance degradation detected in {trend.MetricName}. Consider investigation.");
            }

            return recommendations;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _collectionTimer?.Dispose();
                _cpuCounter?.Dispose();
                _memoryCounter?.Dispose();
                _currentProcess?.Dispose();
                _disposed = true;
            }
        }
    }

    public class PerformanceMetric
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double Value { get; set; }
        public long Count { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class PerformanceSample
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    public class PerformanceReport
    {
        public DateTime GeneratedAt { get; set; }
        public SystemMetrics SystemMetrics { get; set; } = new();
        public ApplicationMetrics ApplicationMetrics { get; set; } = new();
        public List<PerformanceTrend> PerformanceTrends { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public class PerformanceSnapshot
    {
        public DateTime Timestamp { get; set; }
        public SystemMetrics SystemMetrics { get; set; } = new();
        public List<string> TopMetrics { get; set; } = new();
        public List<string> ActiveOperations { get; set; } = new();
    }

    public class SystemMetrics
    {
        public double CPUUsage { get; set; }
        public double AvailableMemory { get; set; }
        public double ProcessWorkingSet { get; set; }
        public double ProcessPrivateMemory { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public double GCMemory { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
    }

    public class ApplicationMetrics
    {
        public int TotalMetrics { get; set; }
        public int TotalSamples { get; set; }
        public List<string> MetricCategories { get; set; } = new();
        public DateTime LastUpdate { get; set; }
    }

    public class PerformanceTrend
    {
        public string MetricName { get; set; } = string.Empty;
        public TrendDirection Direction { get; set; }
        public double ChangePercentage { get; set; }
        public double AverageValue { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public int SampleCount { get; set; }
    }

    public enum TrendDirection
    {
        Increasing,
        Decreasing,
        Stable
    }
}
