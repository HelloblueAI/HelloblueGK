using System;
using System.Collections.Generic;

namespace HB_NLP_Research_Lab.Models
{
    public class AIDesignRequest
    {
        public string EngineType { get; set; } = "revolutionary";
        public double InnovationLevel { get; set; } = 98.0;
        public OptimizationTargets OptimizationTargets { get; set; } = new();
        public AutonomousFeatures AutonomousFeatures { get; set; } = new();
    }

    public class OptimizationTargets
    {
        public double Thrust { get; set; } = 2000000;
        public double Efficiency { get; set; } = 0.95;
        public double Reliability { get; set; } = 0.999;
    }

    public class AutonomousFeatures
    {
        public bool SelfOptimization { get; set; } = true;
        public bool FailurePrediction { get; set; } = true;
        public bool RealTimeLearning { get; set; } = true;
    }

    public class DigitalTwinRequest
    {
        public string EngineId { get; set; } = string.Empty;
        public EngineModel EngineModel { get; set; } = new();
        public LearningCapabilities LearningCapabilities { get; set; } = new();
    }

    public class EngineModel
    {
        public string Name { get; set; } = "Revolutionary Engine";
        public Dictionary<string, double> Parameters { get; set; } = new();
    }

    public class LearningCapabilities
    {
        public bool RealTimeLearning { get; set; } = true;
        public bool PredictiveModeling { get; set; } = true;
        public bool FailurePrediction { get; set; } = true;
    }

    public class QuantumAnalysisRequest
    {
        public string AnalysisType { get; set; } = "quantum-cfd";
        public bool QuantumAdvantage { get; set; } = true;
        public MaterialDiscovery MaterialDiscovery { get; set; } = new();
        public OptimizationSpecs OptimizationSpecs { get; set; } = new();
    }

    public class MaterialDiscovery
    {
        public string TargetApplication { get; set; } = "Engine Components";
        public double RequiredStrength { get; set; } = 500e6;
        public double RequiredTemperatureResistance { get; set; } = 2500;
    }

    public class OptimizationSpecs
    {
        public string Algorithm { get; set; } = "quantum-annealing";
        public Dictionary<string, double> Targets { get; set; } = new();
    }

    public class MultiPhysicsRequest
    {
        public string EngineId { get; set; } = string.Empty;
        public bool EnableCFD { get; set; } = true;
        public bool EnableThermal { get; set; } = true;
        public bool EnableStructural { get; set; } = true;
        public bool EnableElectromagnetic { get; set; } = true;
        public bool EnableMolecularDynamics { get; set; } = true;
        public CouplingOptions CouplingOptions { get; set; } = new();
    }

    public class CouplingOptions
    {
        public bool RealTimeCoupling { get; set; } = true;
        public bool AdaptiveMeshRefinement { get; set; } = true;
        public bool ConvergenceTracking { get; set; } = true;
        public int MaxIterations { get; set; } = 1000;
        public double ConvergenceTolerance { get; set; } = 1e-6;
    }
} 