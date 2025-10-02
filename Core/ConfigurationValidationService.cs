using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace HB_NLP_Research_Lab.Core
{
    /// <summary>
    /// Configuration validation service for startup validation
    /// Ensures all required configuration is present and valid
    /// </summary>
    public class ConfigurationValidationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConfigurationValidationService> _logger;

        public ConfigurationValidationService(IConfiguration configuration, ILogger<ConfigurationValidationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ConfigurationValidationResult> ValidateConfigurationAsync()
        {
            var result = new ConfigurationValidationResult
            {
                IsValid = true,
                ValidationTimestamp = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("Starting configuration validation...");

                // Validate engine configuration
                ValidateEngineConfiguration(result);

                // Validate API configuration
                ValidateApiConfiguration(result);

                // Validate performance monitoring configuration
                ValidatePerformanceMonitoringConfiguration(result);

                // Validate rate limiting configuration
                ValidateRateLimitingConfiguration(result);

                // Validate logging configuration
                ValidateLoggingConfiguration(result);

                // Validate security configuration
                ValidateSecurityConfiguration(result);

                // Validate database configuration (if present)
                ValidateDatabaseConfiguration(result);

                result.IsValid = result.Errors.Count == 0;
                
                if (result.IsValid)
                {
                    _logger.LogInformation("Configuration validation completed successfully");
                }
                else
                {
                    _logger.LogWarning("Configuration validation completed with {ErrorCount} errors", result.Errors.Count);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Configuration validation failed");
                result.IsValid = false;
                result.Errors.Add($"Validation process failed: {ex.Message}");
                return result;
            }
        }

        private void ValidateEngineConfiguration(ConfigurationValidationResult result)
        {
            _logger.LogDebug("Validating engine configuration...");

            var engineConfig = new AdvancedEngineConfiguration();
            _configuration.GetSection("EngineConfiguration").Bind(engineConfig);

            // Validate using data annotations
            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var validationContext = new ValidationContext(engineConfig);

            if (!Validator.TryValidateObject(engineConfig, validationContext, validationResults, true))
            {
                foreach (var validationResult in validationResults)
                {
                    result.Errors.Add($"Engine Configuration: {validationResult.ErrorMessage}");
                }
            }

            // Additional custom validation
            if (engineConfig.MaxSimulationTime <= 0)
            {
                result.Errors.Add("Engine Configuration: MaxSimulationTime must be greater than 0");
            }

            if (engineConfig.MaxSimulationTime > 3600) // 1 hour
            {
                result.Warnings.Add("Engine Configuration: MaxSimulationTime is very high, consider reducing for better performance");
            }

            if (string.IsNullOrEmpty(engineConfig.DefaultEngineType))
            {
                result.Errors.Add("Engine Configuration: DefaultEngineType is required");
            }
        }

        private void ValidateApiConfiguration(ConfigurationValidationResult result)
        {
            _logger.LogDebug("Validating API configuration...");

            var apiUrl = _configuration["ApiUrl"];
            if (string.IsNullOrEmpty(apiUrl))
            {
                result.Warnings.Add("API Configuration: ApiUrl not configured, using default");
            }

            var apiTimeout = _configuration.GetValue<int>("ApiTimeout");
            if (apiTimeout <= 0)
            {
                result.Warnings.Add("API Configuration: ApiTimeout not configured, using default");
            }

            var maxConcurrentRequests = _configuration.GetValue<int>("MaxConcurrentRequests");
            if (maxConcurrentRequests <= 0)
            {
                result.Warnings.Add("API Configuration: MaxConcurrentRequests not configured, using default");
            }
        }

        private void ValidatePerformanceMonitoringConfiguration(ConfigurationValidationResult result)
        {
            _logger.LogDebug("Validating performance monitoring configuration...");

            var enablePerformanceMonitoring = _configuration.GetValue<bool>("EnablePerformanceMonitoring", true);
            var metricsInterval = _configuration.GetValue<int>("MetricsInterval", 5000);

            if (metricsInterval < 1000)
            {
                result.Warnings.Add("Performance Monitoring: MetricsInterval is very low, may impact performance");
            }

            if (metricsInterval > 60000)
            {
                result.Warnings.Add("Performance Monitoring: MetricsInterval is very high, may miss important metrics");
            }
        }

        private void ValidateRateLimitingConfiguration(ConfigurationValidationResult result)
        {
            _logger.LogDebug("Validating rate limiting configuration...");

            var enableRateLimiting = _configuration.GetValue<bool>("EnableRateLimiting", true);
            var defaultRateLimit = _configuration.GetValue<int>("DefaultRateLimit", 100);

            if (defaultRateLimit <= 0)
            {
                result.Errors.Add("Rate Limiting: DefaultRateLimit must be greater than 0");
            }

            if (defaultRateLimit > 10000)
            {
                result.Warnings.Add("Rate Limiting: DefaultRateLimit is very high, may not provide adequate protection");
            }
        }

        private void ValidateLoggingConfiguration(ConfigurationValidationResult result)
        {
            _logger.LogDebug("Validating logging configuration...");

            var logLevel = _configuration["Logging:LogLevel:Default"];
            if (string.IsNullOrEmpty(logLevel))
            {
                result.Warnings.Add("Logging: Default log level not configured");
            }

            var logFile = _configuration["Logging:File:Path"];
            if (string.IsNullOrEmpty(logFile))
            {
                result.Warnings.Add("Logging: File logging not configured");
            }
        }

        private void ValidateSecurityConfiguration(ConfigurationValidationResult result)
        {
            _logger.LogDebug("Validating security configuration...");

            var jwtKey = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                result.Warnings.Add("Security: JWT key not configured, authentication may not work");
            }

            var jwtIssuer = _configuration["Jwt:Issuer"];
            if (string.IsNullOrEmpty(jwtIssuer))
            {
                result.Warnings.Add("Security: JWT issuer not configured");
            }

            var corsOrigins = _configuration["Cors:Origins"];
            if (string.IsNullOrEmpty(corsOrigins))
            {
                result.Warnings.Add("Security: CORS origins not configured, may have security implications");
            }
        }

        private void ValidateDatabaseConfiguration(ConfigurationValidationResult result)
        {
            _logger.LogDebug("Validating database configuration...");

            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                result.Warnings.Add("Database: Connection string not configured, database features may not work");
            }
            else
            {
                // Basic connection string validation
                if (!connectionString.Contains("Server=") && !connectionString.Contains("Host="))
                {
                    result.Warnings.Add("Database: Connection string appears to be missing server/host information");
                }
            }
        }

        public async Task<ConfigurationHealth> GetConfigurationHealthAsync()
        {
            var validation = await ValidateConfigurationAsync();
            
            return new ConfigurationHealth
            {
                IsHealthy = validation.IsValid,
                ValidationTimestamp = validation.ValidationTimestamp,
                ErrorCount = validation.Errors.Count,
                WarningCount = validation.Warnings.Count,
                Errors = validation.Errors,
                Warnings = validation.Warnings,
                ConfigurationSections = GetConfigurationSections()
            };
        }

        private Dictionary<string, object> GetConfigurationSections()
        {
            var sections = new Dictionary<string, object>();

            // Get all top-level configuration sections
            foreach (var section in _configuration.GetChildren())
            {
                try
                {
                    sections[section.Key] = section.Get<object>() ?? new { };
                }
                catch (Exception ex)
                {
                    sections[section.Key] = new { Error = ex.Message };
                }
            }

            return sections;
        }
    }

    public class ConfigurationValidationResult
    {
        public bool IsValid { get; set; }
        public DateTime ValidationTimestamp { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class ConfigurationHealth
    {
        public bool IsHealthy { get; set; }
        public DateTime ValidationTimestamp { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> ConfigurationSections { get; set; } = new();
    }

    public class AdvancedEngineConfiguration
    {
        [Required]
        public string DefaultEngineType { get; set; } = string.Empty;
        
        [Range(0.1, 1.0)]
        public double DefaultEfficiency { get; set; } = 0.89;
        
        [Range(1, 3600)]
        public int MaxSimulationTime { get; set; } = 300;
        
        [Required]
        public bool UseAdvancedSolvers { get; set; } = true;
        
        public string? OpenFOAMPath { get; set; }
        
        public bool EnableRealTimeTelemetry { get; set; } = true;
        
        public bool EnableQuantumAdvantage { get; set; } = false;
        
        public bool EnableShapeShifting { get; set; } = false;
    }
}
