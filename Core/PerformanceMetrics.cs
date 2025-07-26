using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq; // Added for TakeLast and Select

namespace HB_NLP_Research_Lab.Core
{
    /// <summary>
    /// Advanced performance metrics system for aerospace engine monitoring
    /// </summary>
    public class EnginePerformanceMetrics
    {
        public string EngineId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double Thrust { get; set; }
        public double SpecificImpulse { get; set; }
        public double ChamberPressure { get; set; }
        public double MassFlowRate { get; set; }
        public double Efficiency { get; set; }
        public double Temperature { get; set; }
        public double VibrationLevel { get; set; }
        public double FuelConsumption { get; set; }
        public Dictionary<string, double> CustomMetrics { get; set; } = new();

        public EnginePerformanceMetrics()
        {
            Timestamp = DateTime.UtcNow;
        }

        public double CalculateOverallPerformance()
        {
            // Weighted performance calculation
            var thrustScore = Thrust / 2000000.0; // Normalized to 2MN
            var ispScore = SpecificImpulse / 450.0; // Normalized to 450s
            var efficiencyScore = Efficiency;
            var temperatureScore = 1.0 - (Temperature / 3000.0); // Lower is better

            return (thrustScore * 0.3 + ispScore * 0.25 + efficiencyScore * 0.25 + temperatureScore * 0.2) * 100;
        }

        public bool IsWithinSafeLimits()
        {
            return Temperature < 2800 && 
                   VibrationLevel < 0.1 && 
                   ChamberPressure < 350 && 
                   Efficiency > 0.85;
        }

        public string GetPerformanceGrade()
        {
            var performance = CalculateOverallPerformance();
            return performance switch
            {
                >= 95 => "A+",
                >= 90 => "A",
                >= 85 => "B+",
                >= 80 => "B",
                >= 75 => "C+",
                >= 70 => "C",
                _ => "D"
            };
        }
    }

    /// <summary>
    /// Real-time performance monitoring system
    /// </summary>
    public class PerformanceMonitor
    {
        private readonly Dictionary<string, List<EnginePerformanceMetrics>> _historicalData = new();
        private readonly object _lockObject = new();

        public async Task<EnginePerformanceMetrics> CaptureMetricsAsync(string engineId, Dictionary<string, double> sensorData)
        {
            var metrics = new EnginePerformanceMetrics
            {
                EngineId = engineId,
                Thrust = sensorData.GetValueOrDefault("thrust", 0),
                SpecificImpulse = sensorData.GetValueOrDefault("isp", 0),
                ChamberPressure = sensorData.GetValueOrDefault("pressure", 0),
                MassFlowRate = sensorData.GetValueOrDefault("massFlow", 0),
                Efficiency = sensorData.GetValueOrDefault("efficiency", 0),
                Temperature = sensorData.GetValueOrDefault("temperature", 0),
                VibrationLevel = sensorData.GetValueOrDefault("vibration", 0),
                FuelConsumption = sensorData.GetValueOrDefault("fuelConsumption", 0)
            };

            // Store historical data
            lock (_lockObject)
            {
                if (!_historicalData.ContainsKey(engineId))
                    _historicalData[engineId] = new List<EnginePerformanceMetrics>();
                
                _historicalData[engineId].Add(metrics);
                
                // Keep only last 1000 measurements
                if (_historicalData[engineId].Count > 1000)
                    _historicalData[engineId].RemoveAt(0);
            }

            return await Task.FromResult(metrics);
        }

        public async Task<List<EnginePerformanceMetrics>> GetHistoricalDataAsync(string engineId, int count = 100)
        {
            List<EnginePerformanceMetrics> result;
            lock (_lockObject)
            {
                if (_historicalData.ContainsKey(engineId))
                {
                    var data = _historicalData[engineId];
                    result = data.TakeLast(count).ToList();
                }
                else
                {
                    result = new List<EnginePerformanceMetrics>();
                }
            }
            return await Task.FromResult(result);
        }

        public async Task<EnginePerformanceMetrics> GetLatestMetricsAsync(string engineId)
        {
            EnginePerformanceMetrics result;
            lock (_lockObject)
            {
                if (_historicalData.ContainsKey(engineId) && _historicalData[engineId].Count > 0)
                {
                    result = _historicalData[engineId].Last();
                }
                else
                {
                    result = new EnginePerformanceMetrics { EngineId = engineId };
                }
            }
            return await Task.FromResult(result);
        }

        public async Task<double> CalculateTrendAsync(string engineId, string metric, int dataPoints = 10)
        {
            var historicalData = await GetHistoricalDataAsync(engineId, dataPoints);
            if (historicalData.Count < 2) return 0;

            var values = historicalData.Select(m => GetMetricValue(m, metric)).ToList();
            var xValues = Enumerable.Range(0, values.Count).Select(x => (double)x).ToList();

            // Simple linear regression
            var n = values.Count;
            var sumX = xValues.Sum();
            var sumY = values.Sum();
            var sumXY = xValues.Zip(values, (x, y) => x * y).Sum();
            var sumX2 = xValues.Sum(x => x * x);

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            return slope;
        }

        private static double GetMetricValue(EnginePerformanceMetrics metrics, string metric)
        {
            return metric.ToLower() switch
            {
                "thrust" => metrics.Thrust,
                "isp" => metrics.SpecificImpulse,
                "pressure" => metrics.ChamberPressure,
                "efficiency" => metrics.Efficiency,
                "temperature" => metrics.Temperature,
                "vibration" => metrics.VibrationLevel,
                _ => 0
            };
        }
    }
} 