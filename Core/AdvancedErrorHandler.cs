using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HB_NLP_Research_Lab.Core
{
    /// <summary>
    /// Advanced error handling system for aerospace engine operations
    /// </summary>
    public class AdvancedErrorHandler
    {
        private readonly ILogger<AdvancedErrorHandler> _logger;
        private readonly Dictionary<string, int> _errorCounts = new();
        private readonly Dictionary<string, DateTime> _lastErrorTimes = new();
        private readonly object _lockObject = new();

        public AdvancedErrorHandler(ILogger<AdvancedErrorHandler> logger)
        {
            _logger = logger;
        }

        public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName, int maxRetries = 3)
        {
            var attempt = 0;
            while (attempt < maxRetries)
            {
                try
                {
                    attempt++;
                    _logger.LogInformation("Executing {OperationName} (attempt {Attempt}/{MaxRetries})", operationName, attempt, maxRetries);
                    
                    var result = await operation();
                    
                    if (attempt > 1)
                    {
                        _logger.LogInformation("Operation {OperationName} succeeded on attempt {Attempt}", operationName, attempt);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    LogError(ex, operationName, attempt, maxRetries);
                    
                    if (attempt >= maxRetries)
                    {
                        _logger.LogError("Operation {OperationName} failed after {MaxRetries} attempts", operationName, maxRetries);
                        throw;
                    }
                    
                    var delay = CalculateBackoffDelay(attempt);
                    _logger.LogInformation("Retrying {OperationName} in {Delay}ms", operationName, delay);
                    await Task.Delay(delay);
                }
            }
            
            throw new InvalidOperationException($"Operation {operationName} failed after {maxRetries} attempts");
        }

        public async Task ExecuteWithCircuitBreakerAsync(Func<Task> operation, string operationName, int failureThreshold = 5, TimeSpan resetTimeout = default)
        {
            if (resetTimeout == default)
                resetTimeout = TimeSpan.FromMinutes(1);

            lock (_lockObject)
            {
                if (_errorCounts.ContainsKey(operationName) && _errorCounts[operationName] >= failureThreshold)
                {
                    if (_lastErrorTimes.ContainsKey(operationName) && 
                        DateTime.UtcNow - _lastErrorTimes[operationName] < resetTimeout)
                    {
                        throw new CircuitBreakerOpenException($"Circuit breaker for {operationName} is open");
                    }
                    else
                    {
                        // Reset circuit breaker
                        _errorCounts[operationName] = 0;
                        _logger.LogInformation("Circuit breaker for {OperationName} has been reset", operationName);
                    }
                }
            }

            try
            {
                await operation();
                
                // Reset error count on success
                lock (_lockObject)
                {
                    if (_errorCounts.ContainsKey(operationName))
                        _errorCounts[operationName] = 0;
                }
            }
            catch (Exception ex)
            {
                lock (_lockObject)
                {
                    if (!_errorCounts.ContainsKey(operationName))
                        _errorCounts[operationName] = 0;
                    
                    _errorCounts[operationName]++;
                    _lastErrorTimes[operationName] = DateTime.UtcNow;
                }
                
                LogError(ex, operationName, 1, 1);
                throw;
            }
        }

        public async Task<T> ExecuteWithTimeoutAsync<T>(Func<Task<T>> operation, string operationName, TimeSpan timeout)
        {
            using var cts = new System.Threading.CancellationTokenSource(timeout);
            
            try
            {
                var task = operation();
                var result = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));
                
                if (result == task)
                {
                    return await task;
                }
                else
                {
                    throw new TimeoutException($"Operation {operationName} timed out after {timeout.TotalSeconds} seconds");
                }
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Operation {operationName} timed out after {timeout.TotalSeconds} seconds");
            }
        }

        private void LogError(Exception ex, string operationName, int attempt, int maxRetries)
        {
            var errorContext = new Dictionary<string, object>
            {
                ["OperationName"] = operationName,
                ["Attempt"] = attempt,
                ["MaxRetries"] = maxRetries,
                ["ErrorType"] = ex.GetType().Name,
                ["ErrorMessage"] = ex.Message,
                ["StackTrace"] = ex.StackTrace
            };

            _logger.LogError(ex, "Error in {OperationName} (attempt {Attempt}/{MaxRetries}): {ErrorMessage}", 
                operationName, attempt, maxRetries, ex.Message);
        }

        private int CalculateBackoffDelay(int attempt)
        {
            // Exponential backoff with jitter
            var baseDelay = Math.Pow(2, attempt - 1) * 1000; // Base delay in milliseconds
            var jitter = new Random().Next(0, 100); // Add up to 100ms of jitter
            return (int)(baseDelay + jitter);
        }

        public async Task<ErrorReport> GenerateErrorReportAsync(string operationName, DateTime startTime, DateTime endTime)
        {
            var lastErrorTime = GetLastErrorTime(operationName);
            
            var report = new ErrorReport
            {
                OperationName = operationName,
                StartTime = startTime,
                EndTime = endTime,
                Duration = endTime - startTime,
                ErrorCount = _errorCounts.GetValueOrDefault(operationName, 0),
                LastErrorTime = lastErrorTime
            };

            return await Task.FromResult(report);
        }

        private DateTime GetLastErrorTime(string operationName)
        {
            DateTime result = DateTime.MinValue;
            if (_lastErrorTimes.ContainsKey(operationName))
            {
                result = _lastErrorTimes[operationName];
            }
            return result;
        }
    }

    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException(string message) : base(message) { }
    }

    public class ErrorReport
    {
        public string OperationName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public int ErrorCount { get; set; }
        public DateTime LastErrorTime { get; set; }
        public double SuccessRate => ErrorCount == 0 ? 100.0 : Math.Max(0, 100.0 - (ErrorCount * 10.0));
    }
} 