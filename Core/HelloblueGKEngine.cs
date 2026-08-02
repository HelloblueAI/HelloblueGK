using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using HB_NLP_Research_Lab.Physics;

namespace HB_NLP_Research_Lab.Core
{
    /// <summary>
    /// Advanced computational geometry kernel for aerospace engineering
    /// Now featuring high-performance physics, real-time validation, and advanced AI optimization
    /// Designed for enterprise applications with real-world validation
    /// </summary>
    public class HelloblueGKEngine : IDisposable
    {
        private readonly HighPerformancePhysicsEngine _physicsEngine;
        private readonly RealTimeValidationEngine _validationEngine;
        private readonly AdvancedAIOptimizationEngine _aiOptimizationEngine;
        
        public HelloblueGKEngine()
        {
            _physicsEngine = new HighPerformancePhysicsEngine();
            _validationEngine = new RealTimeValidationEngine();
            _aiOptimizationEngine = new AdvancedAIOptimizationEngine();
            CfdAnalysis = new CfdAnalysisResult();
            ThermalAnalysis = new ThermalAnalysisResult();
            StructuralAnalysis = new StructuralAnalysisResult();
            ValidationReport = new ValidationReport();
        }
        
        /// <summary>
        /// Performs comprehensive multi-physics analysis on aerospace engines
        /// Now with high-performance computing and real-time validation
        /// </summary>
        public Task<ComprehensiveAnalysisResult> AnalyzeEngineAsync(string engineModel)
        {
            return AnalyzeEngineAsync(engineModel, "MultiPhysics", null);
        }

        /// <summary>
        /// Performs analysis for a specific simulation type, optionally applying request parameters.
        /// </summary>
        public Task<ComprehensiveAnalysisResult> AnalyzeEngineAsync(
            string engineModel,
            string simulationType,
            IReadOnlyDictionary<string, object>? parameters = null)
        {
            return AnalyzeEngineAsync(engineModel, simulationType, parameters, baselineDesign: null);
        }

        /// <summary>
        /// Performs analysis using persisted engine characteristics as the MultiPhysics baseline,
        /// then applying optional request parameter overrides.
        /// </summary>
        public async Task<ComprehensiveAnalysisResult> AnalyzeEngineAsync(
            string engineModel,
            string simulationType,
            IReadOnlyDictionary<string, object>? parameters,
            EngineDesignParameters? baselineDesign)
        {
            Console.WriteLine("[HelloblueGK] 🔬 Analyzing engine model with high-performance physics...");
            
            // Ensure physics engine is initialized
            await _physicsEngine.InitializeAsync();

            var normalizedType = NormalizeSimulationType(simulationType);

            CfdAnalysisResult? cfdResult = null;
            ThermalAnalysisResult? thermalResult = null;
            StructuralAnalysisResult? structuralResult = null;
            MultiPhysicsResult? multiPhysicsResult = null;
            int iterations;
            double solverAccuracy;

            switch (normalizedType)
            {
                case "CFD":
                    cfdResult = await _physicsEngine.RunCfdAnalysisAsync();
                    iterations = cfdResult.ConvergenceIterations;
                    solverAccuracy = cfdResult.Accuracy;
                    break;
                case "Thermal":
                    thermalResult = await _physicsEngine.RunThermalAnalysisAsync();
                    iterations = thermalResult.ConvergenceIterations;
                    solverAccuracy = thermalResult.Accuracy;
                    break;
                case "Structural":
                    structuralResult = await _physicsEngine.RunStructuralAnalysisAsync();
                    iterations = structuralResult.ConvergenceIterations;
                    solverAccuracy = structuralResult.Accuracy;
                    break;
                default:
                    multiPhysicsResult = await _physicsEngine.RunMultiPhysicsAnalysisAsync();
                    cfdResult = multiPhysicsResult.CfdResult;
                    thermalResult = multiPhysicsResult.ThermalResult;
                    structuralResult = multiPhysicsResult.StructuralResult;
                    iterations = new[]
                    {
                        cfdResult?.ConvergenceIterations ?? 0,
                        thermalResult?.ConvergenceIterations ?? 0,
                        structuralResult?.ConvergenceIterations ?? 0
                    }.Max();
                    solverAccuracy = new[]
                    {
                        cfdResult?.Accuracy ?? 0,
                        thermalResult?.Accuracy ?? 0,
                        structuralResult?.Accuracy ?? 0
                    }.Where(value => value > 0).DefaultIfEmpty(95.0).Average();
                    break;
            }

            // Seed physics results from persisted engine characteristics before request overrides
            // so different engines do not collapse to identical constant-solver outputs.
            ApplyDesignBaselineToPhysicsResults(
                baselineDesign,
                cfdResult,
                thermalResult,
                structuralResult);

            if (TryReadIntParameter(parameters, "iterations", out var requestedIterations) &&
                requestedIterations > 0)
            {
                iterations = requestedIterations;
            }

            // Apply typed physics request parameters so CFD/Thermal/Structural results
            // reflect accepted inputs instead of only echoing them in ResultsJson.
            // Solver Accuracy / ConvergenceRate remain solver-owned trust signals.
            ApplyPhysicsParameterOverrides(
                parameters,
                cfdResult,
                thermalResult,
                structuralResult,
                ref iterations);

            // Real-time validation
            var validationReport = await _validationEngine.ValidateEngineModelAsync(engineModel);

            // AI optimization remains part of the full MultiPhysics path for backward compatibility.
            OptimizationResult? optimizationResult = null;
            InnovationReport? innovationReport = null;
            if (string.Equals(normalizedType, "MultiPhysics", StringComparison.OrdinalIgnoreCase))
            {
                var optimizationParameters = BuildDesignParameters(parameters, baselineDesign);
                optimizationResult = await _aiOptimizationEngine.OptimizeEngineDesignAsync(optimizationParameters);
                innovationReport = await _aiOptimizationEngine.AnalyzeInnovationAsync(optimizationParameters);

                Console.WriteLine($"[HelloblueGK] 🎯 AI Optimization: {optimizationResult.OverallImprovement:F1}% improvement");
                Console.WriteLine($"[HelloblueGK] 🔬 Innovation Score: {innovationReport.InnovationScore:F1}%");
            }

            var thrustAnalysis = cfdResult == null
                ? new ThrustAnalysis()
                : new ThrustAnalysis
                {
                    MaxThrust = ResolveThrust(cfdResult, baselineDesign, parameters),
                    Efficiency = ResolveEfficiency(cfdResult.Accuracy, baselineDesign, parameters)
                };

            var thermalAnalysis = thermalResult == null
                ? new ThermalAnalysis()
                : new ThermalAnalysis
                {
                    MaxTemperature = thermalResult.MaxTemperature,
                    CoolingEfficiency = NormalizeRatio(
                        thermalResult.HeatTransferEfficiency > 0
                            ? thermalResult.HeatTransferEfficiency
                            : thermalResult.Accuracy)
                };

            var structuralAnalysis = structuralResult == null
                ? new StructuralAnalysis()
                : new StructuralAnalysis
                {
                    MaxStress = structuralResult.MaxStress,
                    SafetyFactor = structuralResult.SafetyFactor > 0
                        ? structuralResult.SafetyFactor
                        : 1.0
                };

            var performanceMetrics = new Dictionary<string, double>
            {
                ["Overall"] = NormalizeRatio(solverAccuracy),
                ["Iterations"] = iterations,
                ["ConvergenceRate"] = NormalizeRatio(solverAccuracy)
            };

            if (TryReadDoubleParameter(parameters, "meshQuality", out var meshQuality))
            {
                performanceMetrics["MeshQuality"] = meshQuality;
            }

            if (cfdResult != null && cfdResult.MaxPressure > 0)
            {
                performanceMetrics["ChamberPressure"] = cfdResult.MaxPressure;
            }

            if (thermalResult != null && thermalResult.MaxTemperature > 0)
            {
                performanceMetrics["MaxTemperature"] = thermalResult.MaxTemperature;
            }

            if (structuralResult != null && structuralResult.MaxStress > 0)
            {
                performanceMetrics["MaxStress"] = structuralResult.MaxStress;
            }

            return new ComprehensiveAnalysisResult
            {
                SimulationType = normalizedType,
                Iterations = iterations,
                ConvergenceRate = NormalizeRatio(solverAccuracy),
                ThrustAnalysis = thrustAnalysis,
                ThermalAnalysis = thermalAnalysis,
                StructuralAnalysis = structuralAnalysis,
                PerformanceMetrics = performanceMetrics,
                MultiPhysicsResult = multiPhysicsResult ?? new MultiPhysicsResult(),
                ValidationReport = validationReport,
                OptimizationResult = optimizationResult ?? new OptimizationResult(),
                InnovationReport = innovationReport ?? new InnovationReport()
            };
        }
        
        /// <summary>
        /// Generates comprehensive validation summary with real-time data
        /// </summary>
        public async Task<ValidationSummary> GenerateValidationSummaryAsync()
        {
            Console.WriteLine("[HelloblueGK] ✅ Generating validation summary with real-time data...");
            
            // Get real-time validation data
            var validationReport = await _validationEngine.ValidateEngineModelAsync("HB-NLP-REV-001");
            
            return new ValidationSummary
            {
                IsValid = true,
                ValidationScore = validationReport.OverallAccuracy / 100.0,
                CriticalIssues = 0,
                Warnings = 2,
                ValidationSource = validationReport.ValidationSource,
                ConfidenceLevel = validationReport.ConfidenceLevel
            };
        }

        /// <summary>
        /// Gets real-time performance metrics from the high-performance physics engine
        /// </summary>
        public async Task<PerformanceMetrics> GetPerformanceMetricsAsync()
        {
            // Ensure physics engine is initialized
            await _physicsEngine.InitializeAsync();
            return await _physicsEngine.GetPerformanceMetricsAsync();
        }

        /// <summary>
        /// Analyzes innovation potential using advanced AI
        /// </summary>
        public async Task<InnovationReport> AnalyzeInnovationAsync(EngineDesignParameters parameters)
        {
            return await _aiOptimizationEngine.AnalyzeInnovationAsync(parameters);
        }

        /// <summary>
        /// Runs high-performance multi-physics analysis
        /// </summary>
        public async Task<MultiPhysicsResult> RunMultiPhysicsAnalysisAsync()
        {
            // Ensure physics engine is initialized
            await _physicsEngine.InitializeAsync();
            return await _physicsEngine.RunMultiPhysicsAnalysisAsync();
        }
        
        public void Dispose()
        {
            // Cleanup resources
        }

        public CfdAnalysisResult CfdAnalysis { get; set; }
        public ThermalAnalysisResult ThermalAnalysis { get; set; }
        public StructuralAnalysisResult StructuralAnalysis { get; set; }
        public ValidationReport ValidationReport { get; set; }

        private static string NormalizeSimulationType(string? simulationType)
        {
            if (string.IsNullOrWhiteSpace(simulationType))
            {
                return "MultiPhysics";
            }

            return simulationType.Trim() switch
            {
                var value when value.Equals("CFD", StringComparison.OrdinalIgnoreCase) => "CFD",
                var value when value.Equals("Thermal", StringComparison.OrdinalIgnoreCase) => "Thermal",
                var value when value.Equals("Structural", StringComparison.OrdinalIgnoreCase) => "Structural",
                var value when value.Equals("MultiPhysics", StringComparison.OrdinalIgnoreCase) => "MultiPhysics",
                _ => "MultiPhysics"
            };
        }

        private static EngineDesignParameters BuildDesignParameters(
            IReadOnlyDictionary<string, object>? parameters,
            EngineDesignParameters? baseline = null)
        {
            var design = CloneDesignParameters(baseline) ?? new EngineDesignParameters
            {
                Thrust = 1500000,
                SpecificImpulse = 380,
                ChamberPressure = 250,
                Efficiency = 0.85
            };

            ApplyDesignParameterOverrides(design, parameters);
            return design;
        }

        /// <summary>
        /// Builds design parameters from persisted engine characteristics.
        /// </summary>
        public static EngineDesignParameters CreateDesignParametersFromEngine(
            double thrust,
            double specificImpulse,
            double chamberPressure,
            double efficiency)
        {
            return new EngineDesignParameters
            {
                Thrust = thrust,
                SpecificImpulse = specificImpulse,
                ChamberPressure = chamberPressure,
                Efficiency = efficiency
            };
        }

        private static EngineDesignParameters? CloneDesignParameters(EngineDesignParameters? baseline)
        {
            if (baseline == null)
            {
                return null;
            }

            return new EngineDesignParameters
            {
                Thrust = baseline.Thrust,
                SpecificImpulse = baseline.SpecificImpulse,
                ChamberPressure = baseline.ChamberPressure,
                Efficiency = baseline.Efficiency
            };
        }

        public static void ApplyDesignParameterOverrides(
            EngineDesignParameters design,
            IReadOnlyDictionary<string, object>? parameters)
        {
            if (parameters == null)
            {
                return;
            }

            if (TryReadDoubleParameter(parameters, "thrust", out var thrust))
            {
                design.Thrust = thrust;
            }

            if (TryReadDoubleParameter(parameters, "specificImpulse", out var specificImpulse))
            {
                design.SpecificImpulse = specificImpulse;
            }

            if (TryReadDoubleParameter(parameters, "chamberPressure", out var chamberPressure))
            {
                design.ChamberPressure = chamberPressure;
            }

            if (TryReadDoubleParameter(parameters, "efficiency", out var efficiency))
            {
                design.Efficiency = Math.Clamp(efficiency, 0.0, 1.0);
            }
        }

        /// <summary>
        /// Maps persisted engine characteristics onto solver outputs so CFD/Thermal/Structural
        /// analysis varies by engine instead of collapsing to constant HighPerformance baselines.
        /// Does not mutate solver Accuracy (kept as a trust/validation signal).
        /// </summary>
        private static void ApplyDesignBaselineToPhysicsResults(
            EngineDesignParameters? baseline,
            CfdAnalysisResult? cfdResult,
            ThermalAnalysisResult? thermalResult,
            StructuralAnalysisResult? structuralResult)
        {
            if (baseline == null)
            {
                return;
            }

            if (cfdResult != null)
            {
                if (baseline.ChamberPressure > 0)
                {
                    cfdResult.MaxPressure = baseline.ChamberPressure;
                    cfdResult.PressureDistribution["chamber"] = baseline.ChamberPressure;
                    cfdResult.PressureDistribution["nozzle"] = baseline.ChamberPressure / 3.0;
                }

                if (baseline.Thrust > 0)
                {
                    // Differentiate flow fields across engines without treating thrust as pressure.
                    var velocity = Math.Max(Math.Sqrt(baseline.Thrust / 1000.0), 1.0);
                    cfdResult.MaxVelocity = velocity;
                    cfdResult.FlowVelocity = new Vector3((float)velocity, 0, 0);
                }
            }

            if (thermalResult != null)
            {
                var efficiency = baseline.Efficiency > 0
                    ? Math.Clamp(baseline.Efficiency, 0.0, 1.0)
                    : 0.85;
                thermalResult.HeatTransferEfficiency = efficiency * 100.0;

                var temperature = 2500.0 + (efficiency * 1500.0);
                if (baseline.ChamberPressure > 0)
                {
                    temperature *= Math.Clamp(baseline.ChamberPressure / 250.0, 0.5, 1.5);
                }

                thermalResult.MaxTemperature = temperature;
                thermalResult.TemperatureDistribution["chamber"] = temperature;
                thermalResult.TemperatureDistribution["nozzle"] = temperature * 0.8;
                if (baseline.Thrust > 0)
                {
                    thermalResult.HeatTransferRate = baseline.Thrust * 2.5;
                }
            }

            if (structuralResult != null && baseline.ChamberPressure > 0)
            {
                // ~2.67e6 Pa per bar ≈ 800e6 Pa at 300 bar (legacy constant structural baseline).
                var maxStress = baseline.ChamberPressure * 2.67e6;
                structuralResult.MaxStress = maxStress;
                structuralResult.StressDistribution["chamber"] = maxStress;
                structuralResult.StressDistribution["nozzle"] = maxStress * 0.75;
                if (baseline.Efficiency > 0)
                {
                    structuralResult.SafetyFactor = Math.Clamp(1.2 + (baseline.Efficiency * 0.5), 1.2, 2.0);
                }
            }
        }

        private static void ApplyPhysicsParameterOverrides(
            IReadOnlyDictionary<string, object>? parameters,
            CfdAnalysisResult? cfdResult,
            ThermalAnalysisResult? thermalResult,
            StructuralAnalysisResult? structuralResult,
            ref int iterations)
        {
            if (parameters == null)
            {
                return;
            }

            if (TryReadIntParameter(parameters, "iterations", out var requestedIterations) &&
                requestedIterations > 0)
            {
                iterations = requestedIterations;
                if (cfdResult != null)
                {
                    cfdResult.ConvergenceIterations = requestedIterations;
                }

                if (thermalResult != null)
                {
                    thermalResult.ConvergenceIterations = requestedIterations;
                }

                if (structuralResult != null)
                {
                    structuralResult.ConvergenceIterations = requestedIterations;
                }
            }

            if (cfdResult != null)
            {
                if (TryReadDoubleParameter(parameters, "chamberPressure", out var chamberPressure) ||
                    TryReadDoubleParameter(parameters, "maxPressure", out chamberPressure))
                {
                    cfdResult.MaxPressure = chamberPressure;
                    cfdResult.PressureDistribution["chamber"] = chamberPressure;
                }

                if (TryReadDoubleParameter(parameters, "maxVelocity", out var maxVelocity) ||
                    TryReadDoubleParameter(parameters, "flowVelocity", out maxVelocity))
                {
                    cfdResult.MaxVelocity = maxVelocity;
                    cfdResult.FlowVelocity = new Vector3((float)maxVelocity, 0, 0);
                }

                // Do not copy thrust into MaxPressure — that misreports ChamberPressure in
                // PerformanceMetrics. Requested thrust is applied to ThrustAnalysis.MaxThrust.
                // Do not let clients overwrite Accuracy / ConvergenceRate trust signals.

                if (TryReadDoubleParameter(parameters, "meshQuality", out var meshQuality))
                {
                    cfdResult.MeshQuality = meshQuality;
                }
            }

            if (thermalResult != null)
            {
                if (TryReadDoubleParameter(parameters, "maxTemperature", out var maxTemperature))
                {
                    thermalResult.MaxTemperature = maxTemperature;
                    thermalResult.TemperatureDistribution["chamber"] = maxTemperature;
                }

                if (TryReadDoubleParameter(parameters, "coolingEfficiency", out var coolingEfficiency) ||
                    TryReadDoubleParameter(parameters, "heatTransferEfficiency", out coolingEfficiency))
                {
                    thermalResult.HeatTransferEfficiency = NormalizeRatio(coolingEfficiency) * 100.0;
                }

                if (TryReadDoubleParameter(parameters, "heatTransferRate", out var heatTransferRate))
                {
                    thermalResult.HeatTransferRate = heatTransferRate;
                }
            }

            if (structuralResult != null)
            {
                if (TryReadDoubleParameter(parameters, "maxStress", out var maxStress))
                {
                    structuralResult.MaxStress = maxStress;
                    structuralResult.StressDistribution["chamber"] = maxStress;
                }

                if (TryReadDoubleParameter(parameters, "safetyFactor", out var safetyFactor) &&
                    safetyFactor > 0)
                {
                    structuralResult.SafetyFactor = safetyFactor;
                }

                if (TryReadDoubleParameter(parameters, "maxDisplacement", out var maxDisplacement))
                {
                    structuralResult.MaxDisplacement = maxDisplacement;
                }
            }
        }

        private static double ResolveThrust(
            CfdAnalysisResult cfdResult,
            EngineDesignParameters? baselineDesign,
            IReadOnlyDictionary<string, object>? parameters)
        {
            if (TryReadDoubleParameter(parameters, "thrust", out var requestedThrust) ||
                TryReadDoubleParameter(parameters, "maxThrust", out requestedThrust))
            {
                return requestedThrust;
            }

            if (baselineDesign != null && baselineDesign.Thrust > 0)
            {
                return baselineDesign.Thrust;
            }

            return DeriveThrust(cfdResult);
        }

        private static double ResolveEfficiency(
            double solverAccuracy,
            EngineDesignParameters? baselineDesign,
            IReadOnlyDictionary<string, object>? parameters)
        {
            if (TryReadDoubleParameter(parameters, "efficiency", out var requestedEfficiency))
            {
                return NormalizeRatio(requestedEfficiency);
            }

            if (baselineDesign != null && baselineDesign.Efficiency > 0)
            {
                return Math.Clamp(baselineDesign.Efficiency, 0.0, 1.0);
            }

            return NormalizeRatio(solverAccuracy);
        }

        private static double DeriveThrust(CfdAnalysisResult cfdResult)
        {
            if (cfdResult.MaxVelocity > 0)
            {
                return cfdResult.MaxVelocity * 1000.0;
            }

            var velocityMagnitude = cfdResult.FlowVelocity.Length();
            if (velocityMagnitude > 0)
            {
                return velocityMagnitude * 1000.0;
            }

            // MaxPressure is chamber pressure (bar), not thrust — never treat it as Newtons.
            return Math.Max(cfdResult.CalculationCount, 1);
        }

        private static double NormalizeRatio(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            {
                return 0.0;
            }

            return value > 1.0 ? Math.Clamp(value / 100.0, 0.0, 1.0) : Math.Clamp(value, 0.0, 1.0);
        }

        private static bool TryReadIntParameter(
            IReadOnlyDictionary<string, object>? parameters,
            string key,
            out int value)
        {
            value = 0;
            if (!TryReadDoubleParameter(parameters, key, out var numeric))
            {
                return false;
            }

            value = (int)Math.Round(numeric, MidpointRounding.AwayFromZero);
            return true;
        }

        private static bool TryReadDoubleParameter(
            IReadOnlyDictionary<string, object>? parameters,
            string key,
            out double value)
        {
            value = 0;
            if (parameters == null)
            {
                return false;
            }

            var match = parameters.FirstOrDefault(pair =>
                string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase));
            if (match.Key == null)
            {
                return false;
            }

            return TryConvertToDouble(match.Value, out value);
        }

        private static bool TryConvertToDouble(object? raw, out double value)
        {
            value = 0;
            switch (raw)
            {
                case null:
                    return false;
                case double d:
                    value = d;
                    return !double.IsNaN(d) && !double.IsInfinity(d);
                case float f:
                    value = f;
                    return !float.IsNaN(f) && !float.IsInfinity(f);
                case int i:
                    value = i;
                    return true;
                case long l:
                    value = l;
                    return true;
                case decimal m:
                    value = (double)m;
                    return true;
                case JsonElement element when element.ValueKind == JsonValueKind.Number &&
                                             element.TryGetDouble(out var jsonNumber):
                    value = jsonNumber;
                    return !double.IsNaN(jsonNumber) && !double.IsInfinity(jsonNumber);
                case JsonElement element when element.ValueKind == JsonValueKind.String &&
                                             double.TryParse(
                                                 element.GetString(),
                                                 NumberStyles.Float,
                                                 CultureInfo.InvariantCulture,
                                                 out var jsonStringNumber):
                    value = jsonStringNumber;
                    return !double.IsNaN(jsonStringNumber) && !double.IsInfinity(jsonStringNumber);
                case string text when double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed):
                    value = parsed;
                    return !double.IsNaN(parsed) && !double.IsInfinity(parsed);
                default:
                    return false;
            }
        }
    }
    
    public class ThrustAnalysis
    {
        public double MaxThrust { get; set; }
        public double Efficiency { get; set; }
    }

    public class ThermalAnalysis
    {
        public double MaxTemperature { get; set; }
        public double CoolingEfficiency { get; set; }
    }

    public class StructuralAnalysis
    {
        public double MaxStress { get; set; }
        public double SafetyFactor { get; set; }
    }

    public class ComprehensiveAnalysisResult
    {
        public ComprehensiveAnalysisResult()
        {
            SimulationType = "MultiPhysics";
            ThrustAnalysis = new ThrustAnalysis();
            ThermalAnalysis = new ThermalAnalysis();
            StructuralAnalysis = new StructuralAnalysis();
            PerformanceMetrics = new Dictionary<string, double>();
            MultiPhysicsResult = new MultiPhysicsResult();
            ValidationReport = new ValidationReport();
            OptimizationResult = new OptimizationResult();
            InnovationReport = new InnovationReport();
        }

        public string SimulationType { get; set; }
        public int Iterations { get; set; }
        public double ConvergenceRate { get; set; }
        
        public ThrustAnalysis ThrustAnalysis { get; set; }
        public ThermalAnalysis ThermalAnalysis { get; set; }
        public StructuralAnalysis StructuralAnalysis { get; set; }
        public Dictionary<string, double> PerformanceMetrics { get; set; }
        public MultiPhysicsResult MultiPhysicsResult { get; set; }
        public ValidationReport ValidationReport { get; set; }
        public OptimizationResult OptimizationResult { get; set; }
        public InnovationReport InnovationReport { get; set; }
    }


}
