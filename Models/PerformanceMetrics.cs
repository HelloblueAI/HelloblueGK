using System;
using System.Numerics;
using System.Collections.Generic;

namespace HB_NLP_Research_Lab.Models
{
    /// <summary>
    /// Comprehensive performance metrics for aerospace engines
    /// Enterprise-grade metrics for SpaceX, NASA, and Boeing applications
    /// </summary>
    public class PerformanceMetrics
    {
        public PerformanceMetrics()
        {
            EnvironmentalImpact = new EnvironmentalMetrics();
        }
        public float ThrustEfficiency { get; set; }
        public float FuelConsumption { get; set; } // g/kN·s
        public float ThermalEfficiency { get; set; }
        public float WeightToThrust { get; set; } // kg/N
        public float OverallEfficiency { get; set; }
        public EnvironmentalMetrics EnvironmentalImpact { get; set; }
        
        // Hardware performance metrics for Plasticity integration
        public double ComputationSpeed { get; set; } // FLOPS
        public double MemoryBandwidth { get; set; } // GB/s
        public double Latency { get; set; } // seconds
        public double Throughput { get; set; } // operations/second
        public double Efficiency { get; set; } // percentage
    }
    
    /// <summary>
    /// Environmental impact metrics for sustainable aerospace
    /// </summary>
    public class EnvironmentalMetrics
    {
        public EnvironmentalMetrics() {}
        public float NOxEmissions { get; set; } // ppm
        public float COEmissions { get; set; } // ppm
        public float UnburnedHydrocarbons { get; set; } // ppm
        public float ParticulateMatter { get; set; } // mg/m³
        public float NoiseReduction { get; set; } // dB
        public float CarbonFootprint { get; set; } // relative to conventional engines
    }
    
    /// <summary>
    /// Engine specifications for enterprise applications
    /// </summary>
    public class EngineSpecifications
    {
        public EngineSpecifications()
        {
            TemperatureRange = Vector2.Zero;
            HumidityRange = Vector2.Zero;
            VectoringAngles = Vector3.Zero;
        }
        public float Thrust { get; set; } // N
        public float Weight { get; set; } // kg
        public float Length { get; set; } // m
        public float Diameter { get; set; } // m
        public float OperatingAltitude { get; set; } // ft
        public float OperatingSpeed { get; set; } // Mach
        public Vector2 TemperatureRange { get; set; } // °C
        public Vector2 HumidityRange { get; set; } // %
        public int CompressorStages { get; set; }
        public float CombustionEfficiency { get; set; }
        public int TurbineStages { get; set; }
        public Vector3 VectoringAngles { get; set; } // degrees
    }
    
    /// <summary>
    /// Engine component base class
    /// </summary>
    public abstract class EngineComponent
    {
        public EngineComponent()
        {
            Name = string.Empty;
            Dimensions = Vector3.Zero;
        }
        public string Name { get; set; }
        public float Efficiency { get; set; }
        public float Weight { get; set; }
        public Vector3 Dimensions { get; set; }
        public bool AdaptiveGeometry { get; set; }
        public bool SmartCooling { get; set; }
    }
    
    /// <summary>
    /// Adaptive variable geometry compressor
    /// </summary>
    public class AdaptiveVariableGeometryCompressor : EngineComponent
    {
        public AdaptiveVariableGeometryCompressor()
        {
            // No additional non-nullable properties
        }
        public int Stages { get; set; }
        public bool AdaptiveBlades { get; set; }
        public float PressureRatio { get; set; }
        public float MassFlowRate { get; set; }
    }
    
    /// <summary>
    /// Smart combustion chamber with adaptive geometry
    /// </summary>
    public class SmartCombustionChamber : EngineComponent
    {
        public SmartCombustionChamber() {}
        public int FuelInjectors { get; set; }
        public int FlameHolders { get; set; }
        public float CombustionTemperature { get; set; }
        public float Pressure { get; set; }
    }
    
    /// <summary>
    /// High-temperature CMC turbine with advanced cooling
    /// </summary>
    public class HighTemperatureCMCTurbine : EngineComponent
    {
        public HighTemperatureCMCTurbine() {}
        public int CoolingChannels { get; set; }
        public float MaxTemperature { get; set; }
        public float MaxStress { get; set; }
        public float FatigueLife { get; set; }
    }
    
    /// <summary>
    /// 3D thrust vectoring nozzle
    /// </summary>
    public class ThrustVectoringNozzle : EngineComponent
    {
        public Vector3 VectoringAngles { get; set; }
        public float NozzleAreaRatio { get; set; }
        public bool VariableGeometry { get; set; }
        public float ThrustCoefficient { get; set; }
    }
    
    /// <summary>
    /// Hybrid electric power generation system
    /// </summary>
    public class HybridElectricPowerSystem : EngineComponent
    {
        public HybridElectricPowerSystem()
        {
            Name = "Hybrid Electric Power System";
        }
        public new double Efficiency { get; set; }
        public double ElectricPowerOutput { get; set; }
        public double BatteryCapacity { get; set; }
        public double RegenerativeBrakingEfficiency { get; set; }
    }
    
    /// <summary>
    /// Aerospace engine container
    /// </summary>
    public class AerospaceEngine
    {
        private readonly List<EngineComponent> _components;
        
        public AerospaceEngine()
        {
            _components = new List<EngineComponent>();
        }
        
        public void AddComponent(EngineComponent component)
        {
            _components.Add(component);
        }
        
        public IEnumerable<EngineComponent> GetComponents()
        {
            return _components;
        }
    }
    
    /// <summary>
    /// Innovation metrics for revolutionary engine architectures
    /// </summary>
    public class InnovationMetrics
    {
        public InnovationMetrics()
        {
            TechnologyReadinessLevel = string.Empty;
            NoveltyScore = 0.0f;
            DisruptivePotential = 0.0f;
            MarketImpact = 0.0f;
            Patentability = 0.0f;
        }
        public string TechnologyReadinessLevel { get; set; }
        public float NoveltyScore { get; set; }
        public float DisruptivePotential { get; set; }
        public float MarketImpact { get; set; }
        public float Patentability { get; set; }
        public float CostEffectiveness { get; set; }
        public float Scalability { get; set; }
        public float Sustainability { get; set; }
    }

    /// <summary>
    /// Multi-physics analysis results
    /// </summary>
    public class MultiPhysicsResults
    {
        public MultiPhysicsResults()
        {
            ThermalAnalysis = new ThermalAnalysis();
            StructuralAnalysis = new StructuralAnalysis();
            FluidDynamics = new FluidDynamicsAnalysis();
            CoupledAnalysis = new CoupledAnalysis();
        }
        public ThermalAnalysis ThermalAnalysis { get; set; }
        public StructuralAnalysis StructuralAnalysis { get; set; }
        public FluidDynamicsAnalysis FluidDynamics { get; set; }
        public CoupledAnalysis CoupledAnalysis { get; set; }
    }
    
    /// <summary>
    /// Thermal analysis results
    /// </summary>
    public class ThermalAnalysis
    {
        public float MaxTemperature { get; set; }
        public float MinTemperature { get; set; }
        public float AverageTemperature { get; set; }
        public float HeatTransferRate { get; set; }
        public bool ThermalStability { get; set; }
    }
    
    /// <summary>
    /// Structural analysis results
    /// </summary>
    public class StructuralAnalysis
    {
        public float MaxStress { get; set; }
        public float MinStress { get; set; }
        public float SafetyFactor { get; set; }
        public bool StructuralIntegrity { get; set; }
        public float FatigueLife { get; set; }
    }
    
    /// <summary>
    /// Fluid dynamics analysis results
    /// </summary>
    public class FluidDynamicsAnalysis
    {
        public float MaxVelocity { get; set; }
        public float PressureDrop { get; set; }
        public float ReynoldsNumber { get; set; }
        public bool FlowStability { get; set; }
        public float TurbulenceIntensity { get; set; }
    }
    
    /// <summary>
    /// Coupled analysis results
    /// </summary>
    public class CoupledAnalysis
    {
        public float CouplingFactor { get; set; }
        public bool Convergence { get; set; }
        public int Iterations { get; set; }
        public float Residual { get; set; }
    }
    
    /// <summary>
    /// Optimization results
    /// </summary>
    public class OptimizationResults
    {
        public float ObjectiveValue { get; set; }
        public bool Feasible { get; set; }
        public int Iterations { get; set; }
        public float Convergence { get; set; }
        public Dictionary<string, float> OptimizedParameters { get; set; } = new();
    }
    
    /// <summary>
    /// Optimization constraints
    /// </summary>
    public class OptimizationConstraints
    {
        public float MaxWeight { get; set; }
        public float MinThrust { get; set; }
        public float MaxTemperature { get; set; }
        public float MinEfficiency { get; set; }
        public float MaxCost { get; set; }
    }
    
    /// <summary>
    /// Manufacturing data for 3D printing and traditional manufacturing
    /// </summary>
    public class ManufacturingData
    {
        public ManufacturingData()
        {
            STLFiles = new List<string>();
            SupportStructures = new List<string>();
            SlicingData = new List<string>();
            QualityControl = new QualityControlData();
        }
        public List<string> STLFiles { get; set; }
        public List<string> SupportStructures { get; set; }
        public List<string> SlicingData { get; set; }
        public QualityControlData QualityControl { get; set; }
    }
    
    /// <summary>
    /// Quality control data
    /// </summary>
    public class QualityControlData
    {
        public QualityControlData() {}
        public bool WatertightGeometry { get; set; }
        public float WallThickness { get; set; }
        public bool AssemblyClearance { get; set; }
        public float Tolerance { get; set; }
    }
    
    /// <summary>
    /// Enterprise export data for CAD/CAE systems
    /// </summary>
    public class EnterpriseExportData
    {
        public EnterpriseExportData()
        {
            CATIAFiles = new List<string>();
            ANSYSFiles = new List<string>();
            NXFiles = new List<string>();
            SolidWorksFiles = new List<string>();
        }
        public List<string> CATIAFiles { get; set; }
        public List<string> ANSYSFiles { get; set; }
        public List<string> NXFiles { get; set; }
        public List<string> SolidWorksFiles { get; set; }
    }
    
    /// <summary>
    /// Component analysis results
    /// </summary>
    public class ComponentAnalysis
    {
        public ComponentAnalysis() 
        {
            _componentMetrics = new Dictionary<string, ComponentMetrics>();
        }
        private readonly Dictionary<string, ComponentMetrics> _componentMetrics;
        
        public void AddComponentAnalysis(EngineComponent component)
        {
            var metrics = new ComponentMetrics
            {
                Name = component.Name,
                Efficiency = component.Efficiency,
                Weight = component.Weight,
                Reliability = CalculateReliability(component),
                Cost = CalculateCost(component)
            };
            
            _componentMetrics[component.Name] = metrics;
        }
        
        private float CalculateReliability(EngineComponent component)
        {
            // Advanced reliability calculation
            return 0.95f + (component.Efficiency * 0.03f);
        }
        
        private float CalculateCost(EngineComponent component)
        {
            // Advanced cost calculation
            return component.Weight * 1000.0f + (1.0f - component.Efficiency) * 5000.0f;
        }
    }
    
    /// <summary>
    /// Component metrics
    /// </summary>
    public class ComponentMetrics
    {
        public string Name { get; set; } = string.Empty;
        public float Efficiency { get; set; }
        public float Weight { get; set; }
        public float Reliability { get; set; }
        public float Cost { get; set; }
    }
    
    /// <summary>
    /// Manufacturing readiness assessment
    /// </summary>
    public class ManufacturingReadiness
    {
        public bool AdditiveManufacturingCompatible { get; set; }
        public bool TraditionalManufacturingCompatible { get; set; }
        public bool QualityAssurance { get; set; }
        public float CostEffectiveness { get; set; }
        public float Scalability { get; set; }
    }
    
    /// <summary>
    /// Enterprise compatibility assessment
    /// </summary>
    public class EnterpriseCompatibility
    {
        public bool CATIACompatible { get; set; }
        public bool ANSYSCompatible { get; set; }
        public bool SiemensNXCompatible { get; set; }
        public bool SolidWorksCompatible { get; set; }
        public bool APICompatibility { get; set; }
        public bool CloudDeploymentReady { get; set; }
    }
    
    /// <summary>
    /// Comprehensive performance report
    /// </summary>
    public class PerformanceReport
    {
        public EngineSpecifications EngineSpecifications { get; set; } = new();
        public PerformanceMetrics PerformanceMetrics { get; set; } = new();
        public ComponentAnalysis ComponentAnalysis { get; set; } = new();
        public EnvironmentalMetrics EnvironmentalImpact { get; set; } = new();
        public ManufacturingReadiness ManufacturingReadiness { get; set; } = new();
        public EnterpriseCompatibility EnterpriseCompatibility { get; set; } = new();
    }
} 