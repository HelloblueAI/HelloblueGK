using System.Threading.Tasks;

namespace HelloblueGK.Enterprise.API.Services
{
    public interface IEngineService
    {
        /// <summary>
        /// Creates an AI-driven autonomous engine design
        /// </summary>
        Task<EngineDesignResult> CreateAIDrivenEngineAsync(AIDesignRequest request);

        /// <summary>
        /// Creates a digital twin with real-time learning capabilities
        /// </summary>
        Task<DigitalTwinResult> CreateDigitalTwinAsync(DigitalTwinRequest request);

        /// <summary>
        /// Performs quantum-classical hybrid computing analysis
        /// </summary>
        Task<QuantumAnalysisResult> PerformQuantumAnalysisAsync(QuantumAnalysisRequest request);

        /// <summary>
        /// Runs advanced multi-physics simulation
        /// </summary>
        Task<MultiPhysicsResult> RunMultiPhysicsSimulationAsync(MultiPhysicsRequest request);

        /// <summary>
        /// Retrieves engine performance metrics
        /// </summary>
        Task<EnginePerformanceResult> GetEnginePerformanceAsync(string engineId);
    }

    public class EngineDesignResult
    {
        public string EngineId { get; set; } = string.Empty;
        public double InnovationLevel { get; set; }
        public double Thrust { get; set; }
        public double Efficiency { get; set; }
        public double Reliability { get; set; }
    }

    public class DigitalTwinResult
    {
        public string TwinId { get; set; } = string.Empty;
        public double PredictionAccuracy { get; set; }
        public double LearningRate { get; set; }
    }

    public class QuantumAnalysisResult
    {
        public string AnalysisId { get; set; } = string.Empty;
        public double QuantumAdvantage { get; set; }
        public double MaterialDiscoveryAccuracy { get; set; }
        public double OptimizationImprovement { get; set; }
    }

    public class MultiPhysicsResult
    {
        public string SimulationId { get; set; } = string.Empty;
        public double CouplingEfficiency { get; set; }
        public double ConvergenceRate { get; set; }
        public int TotalIterations { get; set; }
    }

    public class EnginePerformanceResult
    {
        public string EngineId { get; set; } = string.Empty;
        public double Thrust { get; set; }
        public double SpecificImpulse { get; set; }
        public double ChamberPressure { get; set; }
        public double Efficiency { get; set; }
        public double Reliability { get; set; }
    }
} 