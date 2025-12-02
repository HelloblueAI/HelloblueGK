using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HB_NLP_Research_Lab.Core
{
    /// <summary>
    /// Enhanced structured logging service for aerospace operations
    /// Provides consistent logging patterns and structured data
    /// </summary>
    public class StructuredLoggingService
    {
        private readonly ILogger<StructuredLoggingService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public StructuredLoggingService(ILogger<StructuredLoggingService> logger)
        {
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public void LogEngineOperation(string operation, string engineId, object? parameters = null, LogLevel level = LogLevel.Information)
        {
            var logData = new Dictionary<string, object>
            {
                ["Operation"] = operation,
                ["EngineId"] = engineId,
                ["Timestamp"] = DateTime.UtcNow,
                ["OperationType"] = "EngineOperation"
            };

            if (parameters != null)
            {
                logData["Parameters"] = SerializeToDictionary(parameters);
            }

            _logger.Log(level, "Engine operation: {Operation} for engine {EngineId}", operation, engineId);
            LogStructuredData(logData, level);
        }

        public void LogPhysicsSimulation(string simulationType, string engineId, object results, TimeSpan duration)
        {
            var logData = new Dictionary<string, object>
            {
                ["SimulationType"] = simulationType,
                ["EngineId"] = engineId,
                ["Duration"] = duration.TotalMilliseconds,
                ["Timestamp"] = DateTime.UtcNow,
                ["OperationType"] = "PhysicsSimulation",
                ["Results"] = SerializeToDictionary(results)
            };

            _logger.LogInformation("Physics simulation completed: {SimulationType} for engine {EngineId} in {Duration}ms", 
                simulationType, engineId, duration.TotalMilliseconds);
            LogStructuredData(logData, LogLevel.Information);
        }

        public void LogAIOptimization(string optimizationType, string engineId, object optimizationResults)
        {
            var logData = new Dictionary<string, object>
            {
                ["OptimizationType"] = optimizationType,
                ["EngineId"] = engineId,
                ["Timestamp"] = DateTime.UtcNow,
                ["OperationType"] = "AIOptimization",
                ["Results"] = SerializeToDictionary(optimizationResults)
            };

            _logger.LogInformation("AI optimization completed: {OptimizationType} for engine {EngineId}", 
                optimizationType, engineId);
            LogStructuredData(logData, LogLevel.Information);
        }

        public void LogPerformanceMetric(string metricName, double value, string category = "General", Dictionary<string, object>? additionalData = null)
        {
            var logData = new Dictionary<string, object>
            {
                ["MetricName"] = metricName,
                ["Value"] = value,
                ["Category"] = category,
                ["Timestamp"] = DateTime.UtcNow,
                ["OperationType"] = "PerformanceMetric"
            };

            if (additionalData != null)
            {
                foreach (var kvp in additionalData)
                {
                    logData[kvp.Key] = kvp.Value;
                }
            }

            _logger.LogInformation("Performance metric: {MetricName} = {Value} in category {Category}", 
                metricName, value, category);
            LogStructuredData(logData, LogLevel.Information);
        }

        public void LogSecurityEvent(string eventType, string description, string? userId = null, string? ipAddress = null)
        {
            var logData = new Dictionary<string, object>
            {
                ["EventType"] = eventType,
                ["Description"] = description,
                ["Timestamp"] = DateTime.UtcNow,
                ["OperationType"] = "SecurityEvent"
            };

            if (!string.IsNullOrEmpty(userId))
                logData["UserId"] = userId;
            
            if (!string.IsNullOrEmpty(ipAddress))
                logData["IpAddress"] = ipAddress;

            _logger.LogWarning("Security event: {EventType} - {Description}", eventType, description);
            LogStructuredData(logData, LogLevel.Warning);
        }

        public void LogSystemEvent(string eventType, string description, object? data = null)
        {
            var logData = new Dictionary<string, object>
            {
                ["EventType"] = eventType,
                ["Description"] = description,
                ["Timestamp"] = DateTime.UtcNow,
                ["OperationType"] = "SystemEvent"
            };

            if (data != null)
            {
                logData["Data"] = SerializeToDictionary(data);
            }

            _logger.LogInformation("System event: {EventType} - {Description}", eventType, description);
            LogStructuredData(logData, LogLevel.Information);
        }

        public void LogError(string operation, Exception exception, object? context = null)
        {
            var logData = new Dictionary<string, object>
            {
                ["Operation"] = operation,
                ["ExceptionType"] = exception.GetType().Name,
                ["ExceptionMessage"] = exception.Message,
                ["StackTrace"] = exception.StackTrace ?? string.Empty,
                ["Timestamp"] = DateTime.UtcNow,
                ["OperationType"] = "Error"
            };

            if (context != null)
            {
                logData["Context"] = SerializeToDictionary(context);
            }

            _logger.LogError(exception, "Error in operation: {Operation}", operation);
            LogStructuredData(logData, LogLevel.Error);
        }

        public void LogBusinessEvent(string eventType, string description, object? data = null)
        {
            var logData = new Dictionary<string, object>
            {
                ["EventType"] = eventType,
                ["Description"] = description,
                ["Timestamp"] = DateTime.UtcNow,
                ["OperationType"] = "BusinessEvent"
            };

            if (data != null)
            {
                logData["Data"] = SerializeToDictionary(data);
            }

            _logger.LogInformation("Business event: {EventType} - {Description}", eventType, description);
            LogStructuredData(logData, LogLevel.Information);
        }

        private void LogStructuredData(Dictionary<string, object> logData, LogLevel level)
        {
            try
            {
                var jsonData = JsonSerializer.Serialize(logData, _jsonOptions);
                
                switch (level)
                {
                    case LogLevel.Debug:
                        _logger.LogDebug("StructuredLog: {JsonData}", jsonData);
                        break;
                    case LogLevel.Information:
                        _logger.LogInformation("StructuredLog: {JsonData}", jsonData);
                        break;
                    case LogLevel.Warning:
                        _logger.LogWarning("StructuredLog: {JsonData}", jsonData);
                        break;
                    case LogLevel.Error:
                        _logger.LogError("StructuredLog: {JsonData}", jsonData);
                        break;
                    case LogLevel.Critical:
                        _logger.LogCritical("StructuredLog: {JsonData}", jsonData);
                        break;
                    default:
                        _logger.LogInformation("StructuredLog: {JsonData}", jsonData);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serialize structured log data");
            }
        }

        private Dictionary<string, object> SerializeToDictionary(object obj)
        {
            try
            {
                var json = JsonSerializer.Serialize(obj, _jsonOptions);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions);
                return dict ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object> { ["SerializationError"] = "Failed to serialize object" };
            }
        }

        public IDisposable BeginOperationScope(string operationType, string operationId)
        {
            var scopeData = new Dictionary<string, object>
            {
                ["OperationType"] = operationType,
                ["OperationId"] = operationId,
                ["StartTime"] = DateTime.UtcNow
            };

            return _logger.BeginScope(scopeData) ?? new NullDisposable();
        }
        
        private class NullDisposable : IDisposable
        {
            public void Dispose() { }
        }

        public void LogOperationComplete(string operationType, string operationId, TimeSpan duration, bool success = true, object? result = null)
        {
            var logData = new Dictionary<string, object>
            {
                ["OperationType"] = "OperationComplete",
                ["Operation"] = operationType,
                ["OperationId"] = operationId,
                ["Duration"] = duration.TotalMilliseconds,
                ["Success"] = success,
                ["Timestamp"] = DateTime.UtcNow
            };

            if (result != null)
            {
                logData["Result"] = SerializeToDictionary(result);
            }

            var level = success ? LogLevel.Information : LogLevel.Warning;
            _logger.Log(level, "Operation completed: {Operation} {OperationId} in {Duration}ms (Success: {Success})", 
                operationType, operationId, duration.TotalMilliseconds, success);
            LogStructuredData(logData, level);
        }
    }
}
