using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq;

namespace HB_NLP_Research_Lab.Core
{
    /// <summary>
    /// Advanced configuration management system for aerospace engine simulation
    /// </summary>
    public class ConfigurationManager
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConfigurationManager> _logger;
        private readonly Dictionary<string, object> _runtimeSettings = new();

        public ConfigurationManager(IConfiguration configuration, ILogger<ConfigurationManager> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public EngineConfiguration GetEngineConfiguration()
        {
            var config = new EngineConfiguration
            {
                UseAdvancedSolvers = _configuration.GetValue<bool>("EngineConfiguration:UseAdvancedSolvers", true),
                OpenFOAMPath = _configuration.GetValue<string>("EngineConfiguration:OpenFOAMPath", "/opt/openfoam8"),
                MaxSimulationTime = _configuration.GetValue<int>("EngineConfiguration:MaxSimulationTime", 3600),
                EnableRealTimeTelemetry = _configuration.GetValue<bool>("EngineConfiguration:EnableRealTimeTelemetry", true),
                EnableQuantumAdvantage = _configuration.GetValue<bool>("EngineConfiguration:EnableQuantumAdvantage", true),
                EnableShapeShifting = _configuration.GetValue<bool>("EngineConfiguration:EnableShapeShifting", true),
                DefaultThrust = _configuration.GetValue<double>("EngineConfiguration:DefaultThrust", 2000000),
                DefaultISP = _configuration.GetValue<double>("EngineConfiguration:DefaultISP", 380),
                DefaultChamberPressure = _configuration.GetValue<double>("EngineConfiguration:DefaultChamberPressure", 250),
                SafetyThresholds = new SafetyThresholds
                {
                    MaxTemperature = _configuration.GetValue<double>("EngineConfiguration:SafetyThresholds:MaxTemperature", 2800),
                    MaxVibration = _configuration.GetValue<double>("EngineConfiguration:SafetyThresholds:MaxVibration", 0.1),
                    MaxPressure = _configuration.GetValue<double>("EngineConfiguration:SafetyThresholds:MaxPressure", 350),
                    MinEfficiency = _configuration.GetValue<double>("EngineConfiguration:SafetyThresholds:MinEfficiency", 0.85)
                }
            };

            _logger.LogInformation("Engine configuration loaded: AdvancedSolvers={AdvancedSolvers}, MaxSimTime={MaxSimTime}s", 
                config.UseAdvancedSolvers, config.MaxSimulationTime);

            return config;
        }

        public RevolutionaryFeaturesConfiguration GetRevolutionaryFeaturesConfiguration()
        {
            var config = new RevolutionaryFeaturesConfiguration
            {
                AIDrivenDesign = _configuration.GetValue<bool>("RevolutionaryFeatures:AIDrivenDesign", true),
                DigitalTwinLearning = _configuration.GetValue<bool>("RevolutionaryFeatures:DigitalTwinLearning", true),
                QuantumHybridComputing = _configuration.GetValue<bool>("RevolutionaryFeatures:QuantumHybridComputing", true),
                RevolutionaryArchitectures = _configuration.GetValue<bool>("RevolutionaryFeatures:RevolutionaryArchitectures", true),
                MultiPhysicsCoupling = _configuration.GetValue<bool>("RevolutionaryFeatures:MultiPhysicsCoupling", true),
                RealTimeLearning = _configuration.GetValue<bool>("RevolutionaryFeatures:RealTimeLearning", true),
                PredictiveModeling = _configuration.GetValue<bool>("RevolutionaryFeatures:PredictiveModeling", true),
                AutonomousTesting = _configuration.GetValue<bool>("RevolutionaryFeatures:AutonomousTesting", true)
            };

            _logger.LogInformation("Revolutionary features configuration loaded: {EnabledFeatures} features enabled", 
                GetEnabledFeatureCount(config));

            return config;
        }

        public PerformanceConfiguration GetPerformanceConfiguration()
        {
            var config = new PerformanceConfiguration
            {
                EnableMetrics = _configuration.GetValue<bool>("Performance:EnableMetrics", true),
                MetricsInterval = _configuration.GetValue<int>("Performance:MetricsInterval", 1000),
                EnableTrendAnalysis = _configuration.GetValue<bool>("Performance:EnableTrendAnalysis", true),
                HistoricalDataRetention = _configuration.GetValue<int>("Performance:HistoricalDataRetention", 1000),
                EnableRealTimeAlerts = _configuration.GetValue<bool>("Performance:EnableRealTimeAlerts", true),
                AlertThresholds = new AlertThresholds
                {
                    CriticalTemperature = _configuration.GetValue<double>("Performance:AlertThresholds:CriticalTemperature", 3000),
                    CriticalVibration = _configuration.GetValue<double>("Performance:AlertThresholds:CriticalVibration", 0.15),
                    CriticalPressure = _configuration.GetValue<double>("Performance:AlertThresholds:CriticalPressure", 400),
                    LowEfficiency = _configuration.GetValue<double>("Performance:AlertThresholds:LowEfficiency", 0.80)
                }
            };

            return config;
        }

        public void SetRuntimeSetting(string key, object value)
        {
            _runtimeSettings[key] = value;
            _logger.LogDebug("Runtime setting updated: {Key} = {Value}", key, value);
        }

        public T GetRuntimeSetting<T>(string key, T defaultValue)
        {
            if (_runtimeSettings.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        public T GetRuntimeSetting<T>(string key)
        {
            if (_runtimeSettings.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default(T)!;
        }

        public async Task SaveConfigurationAsync(string filePath)
        {
            try
            {
                var configData = new
                {
                    EngineConfiguration = GetEngineConfiguration(),
                    RevolutionaryFeatures = GetRevolutionaryFeaturesConfiguration(),
                    Performance = GetPerformanceConfiguration(),
                    RuntimeSettings = _runtimeSettings
                };

                var json = JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
                
                _logger.LogInformation("Configuration saved to {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save configuration to {FilePath}", filePath);
                throw;
            }
        }

        public async Task LoadConfigurationAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Configuration file {FilePath} not found", filePath);
                    return;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var configData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                if (configData != null && configData.TryGetValue("RuntimeSettings", out var runtimeSettings))
                {
                    foreach (var setting in runtimeSettings.EnumerateObject())
                    {
                        _runtimeSettings[setting.Name] = setting.Value.GetRawText() ?? string.Empty;
                    }
                }

                _logger.LogInformation("Configuration loaded from {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration from {FilePath}", filePath);
                throw;
            }
        }

        private static int GetEnabledFeatureCount(RevolutionaryFeaturesConfiguration config)
        {
            var features = new[] 
            { 
                config.AIDrivenDesign, config.DigitalTwinLearning, config.QuantumHybridComputing,
                config.RevolutionaryArchitectures, config.MultiPhysicsCoupling, config.RealTimeLearning,
                config.PredictiveModeling, config.AutonomousTesting 
            };
            
            return features.Count(f => f);
        }
    }

    public class EngineConfiguration
    {
        public bool UseAdvancedSolvers { get; set; } = true;
        public string OpenFOAMPath { get; set; } = "/opt/openfoam8";
        public int MaxSimulationTime { get; set; } = 3600;
        public bool EnableRealTimeTelemetry { get; set; } = true;
        public bool EnableQuantumAdvantage { get; set; } = true;
        public bool EnableShapeShifting { get; set; } = true;
        public double DefaultThrust { get; set; } = 2000000;
        public double DefaultISP { get; set; } = 380;
        public double DefaultChamberPressure { get; set; } = 250;
        public SafetyThresholds SafetyThresholds { get; set; } = new();
    }

    public class SafetyThresholds
    {
        public double MaxTemperature { get; set; } = 2800;
        public double MaxVibration { get; set; } = 0.1;
        public double MaxPressure { get; set; } = 350;
        public double MinEfficiency { get; set; } = 0.85;
    }

    public class RevolutionaryFeaturesConfiguration
    {
        public bool AIDrivenDesign { get; set; } = true;
        public bool DigitalTwinLearning { get; set; } = true;
        public bool QuantumHybridComputing { get; set; } = true;
        public bool RevolutionaryArchitectures { get; set; } = true;
        public bool MultiPhysicsCoupling { get; set; } = true;
        public bool RealTimeLearning { get; set; } = true;
        public bool PredictiveModeling { get; set; } = true;
        public bool AutonomousTesting { get; set; } = true;
    }

    public class PerformanceConfiguration
    {
        public bool EnableMetrics { get; set; } = true;
        public int MetricsInterval { get; set; } = 1000;
        public bool EnableTrendAnalysis { get; set; } = true;
        public int HistoricalDataRetention { get; set; } = 1000;
        public bool EnableRealTimeAlerts { get; set; } = true;
        public AlertThresholds AlertThresholds { get; set; } = new();
    }

    public class AlertThresholds
    {
        public double CriticalTemperature { get; set; } = 3000;
        public double CriticalVibration { get; set; } = 0.15;
        public double CriticalPressure { get; set; } = 400;
        public double LowEfficiency { get; set; } = 0.80;
    }
} 