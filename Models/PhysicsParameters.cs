using System;
using System.Collections.Generic;

namespace HB_NLP_Research_Lab.Models
{
    /// <summary>
    /// CFD Analysis Parameters
    /// Parameters for Computational Fluid Dynamics analysis
    /// </summary>
    public class CfdParameters
    {
        public double ReynoldsNumber { get; set; } = 1e6;
        public double MachNumber { get; set; } = 3.5;
        public double TimeStep { get; set; } = 0.001;
        public Dictionary<string, string> BoundaryConditions { get; set; } = new();
        public string TurbulenceModel { get; set; } = "k-epsilon";
        public double ConvergenceTolerance { get; set; } = 1e-6;
        public int MaxIterations { get; set; } = 10000;
    }

    /// <summary>
    /// Multi-Physics Analysis Parameters
    /// Parameters for coupled multi-physics analysis
    /// </summary>
    public class MultiPhysicsParameters
    {
        public double TimeStep { get; set; } = 0.001;
        public bool FluidStructureInteraction { get; set; } = true;
        public bool ThermalAnalysis { get; set; } = true;
        public bool ElectromagneticAnalysis { get; set; } = true;
        public bool AcousticAnalysis { get; set; } = false;
        public bool CombustionModeling { get; set; } = true;
        public string CouplingMethod { get; set; } = "Strong";
        public double ConvergenceTolerance { get; set; } = 1e-6;
        public int MaxIterations { get; set; } = 5000;
        public bool RealTimeProcessing { get; set; } = true;
    }

    /// <summary>
    /// Optimization Parameters
    /// Parameters for engine design optimization
    /// </summary>
    public class OptimizationParameters
    {
        public string ObjectiveFunction { get; set; } = "Maximize Thrust Efficiency";
        public List<string> Constraints { get; set; } = new();
        public string OptimizationAlgorithm { get; set; } = "Genetic Algorithm";
        public int PopulationSize { get; set; } = 100;
        public int Generations { get; set; } = 1000;
        public double MutationRate { get; set; } = 0.1;
        public double CrossoverRate { get; set; } = 0.8;
        public double ConvergenceTolerance { get; set; } = 1e-6;
        public bool HardwareAcceleration { get; set; } = true;
        public bool RealTimeOptimization { get; set; } = true;
    }

    /// <summary>
    /// CFD Analysis Result
    /// Results from Computational Fluid Dynamics analysis
    /// </summary>
    public class CfdAnalysisResult
    {
        public Dictionary<string, double> PressureDistribution { get; set; } = new();
        public Dictionary<string, System.Numerics.Vector3> VelocityField { get; set; } = new();
        public Dictionary<string, double> TemperatureField { get; set; } = new();
        public Dictionary<string, double> TurbulenceIntensity { get; set; } = new();
        public Dictionary<string, double> WallShearStress { get; set; } = new();
        public List<double> ConvergenceHistory { get; set; } = new();
        public CfdPerformanceMetrics PerformanceMetrics { get; set; } = new();
    }

    /// <summary>
    /// CFD Performance Metrics
    /// Performance metrics for CFD analysis
    /// </summary>
    public class CfdPerformanceMetrics
    {
        public double ComputationTime { get; set; }
        public double MemoryUsage { get; set; }
        public double ConvergenceRate { get; set; }
        public double Accuracy { get; set; }
        public double HardwareUtilization { get; set; }
        public double ComputationSpeed { get; set; } = 1.5e12; // 1.5 TFLOPS
        public double MemoryBandwidth { get; set; } = 1008; // GB/s
        public double Latency { get; set; } = 0.001; // 1ms
        public double Throughput { get; set; } = 1000; // simulations/second
        public double Efficiency { get; set; } = 0.95;
    }

    /// <summary>
    /// Structural Analysis Result
    /// Results from structural analysis
    /// </summary>
    public class StructuralAnalysisResult
    {
        public Dictionary<string, double> StressDistribution { get; set; } = new();
        public Dictionary<string, System.Numerics.Vector3> DisplacementField { get; set; } = new();
        public Dictionary<string, double> StrainField { get; set; } = new();
        public List<double> NaturalFrequencies { get; set; } = new();
        public List<System.Numerics.Vector3[]> ModalShapes { get; set; } = new();
    }

    /// <summary>
    /// Thermal Analysis Result
    /// Results from thermal analysis
    /// </summary>
    public class ThermalAnalysisResult
    {
        public Dictionary<string, double> TemperatureField { get; set; } = new();
        public Dictionary<string, double> HeatFlux { get; set; } = new();
        public Dictionary<string, System.Numerics.Vector3> ThermalGradients { get; set; } = new();
        public Dictionary<string, double> ConvectionCoefficients { get; set; } = new();
    }

    /// <summary>
    /// Electromagnetic Analysis Result
    /// Results from electromagnetic analysis
    /// </summary>
    public class ElectromagneticAnalysisResult
    {
        public Dictionary<string, System.Numerics.Vector3> ElectricField { get; set; } = new();
        public Dictionary<string, System.Numerics.Vector3> MagneticField { get; set; } = new();
        public Dictionary<string, System.Numerics.Vector3> ElectromagneticForces { get; set; } = new();
        public Dictionary<string, double> EddyCurrents { get; set; } = new();
    }

    /// <summary>
    /// Coupling Metrics
    /// Metrics for multi-physics coupling
    /// </summary>
    public class CouplingMetrics
    {
        public double FluidStructureCoupling { get; set; }
        public double ThermalCoupling { get; set; }
        public double ElectromagneticCoupling { get; set; }
        public double OverallCoupling { get; set; }
    }

    /// <summary>
    /// Multi-Physics Analysis Result
    /// Results from coupled multi-physics analysis
    /// </summary>
    public class FluidStructureThermalElectromagneticResult
    {
        public CfdAnalysisResult FluidAnalysis { get; set; } = new();
        public StructuralAnalysisResult StructuralAnalysis { get; set; } = new();
        public ThermalAnalysisResult ThermalAnalysis { get; set; } = new();
        public ElectromagneticAnalysisResult ElectromagneticAnalysis { get; set; } = new();
        public CouplingMetrics CouplingMetrics { get; set; } = new();
    }

    // PerformanceMetrics and InnovationMetrics are defined elsewhere

    /// <summary>
    /// Plasticity Optimization Result
    /// Result from Plasticity hardware optimization
    /// </summary>
    public class PlasticityOptimizationResult
    {
        public string DesignId { get; set; } = string.Empty;
        public double ObjectiveValue { get; set; }
        public Dictionary<string, double> OptimizedParameters { get; set; } = new();
        public OptimizationMetrics OptimizationMetrics { get; set; } = new();
        public List<double> ConvergenceHistory { get; set; } = new();
        public List<string> ConstraintViolations { get; set; } = new();
        public string Status { get; set; } = string.Empty;
        public string? Error { get; set; }
    }

    /// <summary>
    /// Optimization Metrics
    /// Metrics for optimization performance
    /// </summary>
    public class OptimizationMetrics
    {
        public int Iterations { get; set; }
        public double ComputationTime { get; set; }
        public double ConvergenceRate { get; set; }
        public double ObjectiveImprovement { get; set; }
        public bool ConvergenceAchieved { get; set; }
    }
} 