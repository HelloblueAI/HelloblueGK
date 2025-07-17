using System;
using System.Numerics;
using System.Collections.Generic;

namespace HB_NLP_Research_Lab.Aerospace
{
    public class RaptorEngine : RocketEngineBase
    {
        public RaptorEngine() : base()
        {
            Name = "Raptor Engine";
            Propellant = "Methane/LOX";
            Thrust = 2200000; // 2.2 MN
            SpecificImpulse = 330; // seconds
            ChamberPressure = 30000000; // 30 MPa
            ThrottleRange = new Vector2(0.4f, 1.0f);
            VectoringAngles = new Vector3(15.0f, 15.0f, 0.0f);
            Weight = 1500; // kg
            Length = 3.1f; // m
            Diameter = 1.3f; // m
            OperatingAltitude = 100000; // ft
            OperatingSpeed = 2.5f; // Mach
            TemperatureRange = new Vector2(-40.0f, 50.0f); // °C
            HumidityRange = new Vector2(0.0f, 100.0f); // %
            CompressorStages = 0; // Raptor is a full-flow staged combustion engine
            CombustionEfficiency = 0.99f;
            TurbineStages = 0; // Raptor uses full-flow staged combustion
            FuelFlowRate = 650; // kg/s
            OxidizerFlowRate = 1400; // kg/s
            MixtureRatio = 3.6f; // O/F ratio
            ChamberTemperature = 3500; // K
            NozzleExpansionRatio = 40.0f;
            ThrustVectoringRange = new Vector3(15.0f, 15.0f, 0.0f); // degrees
            GimbalRange = new Vector3(15.0f, 15.0f, 0.0f); // degrees
            StartupTime = 2.0f; // seconds
            ShutdownTime = 1.0f; // seconds
            RestartCapability = true;
            DeepThrottleCapability = true;
            MinimumThrottle = 0.4f;
            MaximumThrottle = 1.0f;
            ThrottleResponseTime = 0.1f; // seconds
            EngineCycle = "Full-Flow Staged Combustion";
            FuelType = "Methane";
            OxidizerType = "LOX";
            CoolingMethod = "Regenerative";
            IgnitionSystem = "Spark Plug";
            ThrustChamberMaterial = "Copper Alloy";
            NozzleMaterial = "Carbon-Carbon";
            TurbopumpType = "Oxidizer and Fuel";
            TurbopumpSpeed = 70000; // RPM
            TurbopumpPower = 100000; // W
            TurbopumpEfficiency = 0.85f;
            GasGeneratorType = "None"; // Raptor uses full-flow staged combustion
            PreburnerType = "Oxidizer and Fuel";
            PreburnerTemperature = 800; // K
            PreburnerPressure = 50000000; // Pa
            MainChamberPressure = 30000000; // Pa
            MainChamberTemperature = 3500; // K
            NozzleExitPressure = 100000; // Pa
            NozzleExitTemperature = 1200; // K
            SpecificPower = 1500; // W/kg
            PowerDensity = 2000000; // W/m³
            ThermalEfficiency = 0.45f;
            PropulsiveEfficiency = 0.95f;
            OverallEfficiency = 0.43f;
            Reliability = 0.999f;
            MeanTimeBetweenFailures = 1000; // hours
            MeanTimeToRepair = 24; // hours
            MaintenanceInterval = 100; // hours
            InspectionInterval = 10; // hours
            OverhaulInterval = 1000; // hours
            LifeExpectancy = 10000; // hours
            CostPerHour = 1000; // USD
            CostPerFlight = 50000; // USD
            CostPerLaunch = 1000000; // USD
            DevelopmentCost = 100000000; // USD
            ProductionCost = 10000000; // USD
            OperatingCost = 1000000; // USD/year
            FuelCost = 100000; // USD/launch
            OxidizerCost = 50000; // USD/launch
            MaintenanceCost = 200000; // USD/year
            InsuranceCost = 500000; // USD/year
            EnvironmentalImpact = 0.1f; // CO2 equivalent
            NoiseLevel = 180; // dB
            VibrationLevel = 10; // g
            Emissions = new Dictionary<string, double>
            {
                ["CO2"] = 1000, // kg/launch
                ["H2O"] = 2000, // kg/launch
                ["NOx"] = 50, // kg/launch
                ["CO"] = 10, // kg/launch
                ["Particulates"] = 5 // kg/launch
            };
            SafetyFeatures = new List<string>
            {
                "Emergency Shutdown",
                "Fire Detection",
                "Leak Detection",
                "Pressure Monitoring",
                "Temperature Monitoring",
                "Vibration Monitoring",
                "Acoustic Monitoring",
                "Radiation Shielding",
                "Explosion Suppression",
                "Emergency Cooling"
            };
            RedundancyLevel = 3;
            FaultTolerance = 0.9999f;
            FailureModes = new List<string>
            {
                "Turbopump Failure",
                "Valve Failure",
                "Sensor Failure",
                "Controller Failure",
                "Cooling System Failure",
                "Ignition System Failure",
                "Fuel System Failure",
                "Oxidizer System Failure",
                "Structural Failure",
                "Thermal Failure"
            };
            FailureRates = new Dictionary<string, double>
            {
                ["Turbopump"] = 1e-6,
                ["Valve"] = 1e-5,
                ["Sensor"] = 1e-4,
                ["Controller"] = 1e-5,
                ["Cooling"] = 1e-6,
                ["Ignition"] = 1e-4,
                ["Fuel System"] = 1e-6,
                ["Oxidizer System"] = 1e-6,
                ["Structure"] = 1e-7,
                ["Thermal"] = 1e-6
            };
            TestingHistory = new List<TestResult>
            {
                new TestResult { TestType = "Hot Fire", Duration = 300, Thrust = 2200000, Success = true },
                new TestResult { TestType = "Gimbal", Duration = 60, Thrust = 2200000, Success = true },
                new TestResult { TestType = "Throttle", Duration = 120, Thrust = 1100000, Success = true },
                new TestResult { TestType = "Restart", Duration = 30, Thrust = 2200000, Success = true },
                new TestResult { TestType = "Endurance", Duration = 600, Thrust = 2200000, Success = true }
            };
            FlightHistory = new List<FlightResult>
            {
                new FlightResult { FlightNumber = 1, Duration = 180, Altitude = 100000, Success = true },
                new FlightResult { FlightNumber = 2, Duration = 300, Altitude = 200000, Success = true },
                new FlightResult { FlightNumber = 3, Duration = 600, Altitude = 400000, Success = true },
                new FlightResult { FlightNumber = 4, Duration = 1200, Altitude = 800000, Success = true },
                new FlightResult { FlightNumber = 5, Duration = 2400, Altitude = 1600000, Success = true }
            };
            PerformanceMetrics = new Dictionary<string, double>
            {
                ["Thrust"] = 2200000,
                ["Specific Impulse"] = 330,
                ["Efficiency"] = 0.43,
                ["Reliability"] = 0.999,
                ["Cost"] = 1000000,
                ["Weight"] = 1500,
                ["Power"] = 1000000,
                ["Temperature"] = 3500,
                ["Pressure"] = 30000000,
                ["Flow Rate"] = 2050
            };
            InnovationMetrics = new Dictionary<string, double>
            {
                ["Technology Readiness Level"] = 9,
                ["Novelty Score"] = 0.9,
                ["Disruptive Potential"] = 0.95,
                ["Market Impact"] = 0.9,
                ["Patentability"] = 0.8,
                ["Cost Effectiveness"] = 0.85,
                ["Scalability"] = 0.9,
                ["Sustainability"] = 0.8,
                ["Safety"] = 0.95,
                ["Reliability"] = 0.999
            };
        }
        
        // Add all missing properties
        public double FuelFlowRate { get; set; }
        public double OxidizerFlowRate { get; set; }
        public double MixtureRatio { get; set; }
        public double ChamberTemperature { get; set; }
        public double NozzleExpansionRatio { get; set; }
        public Vector3 ThrustVectoringRange { get; set; }
        public Vector3 GimbalRange { get; set; }
        public bool DeepThrottleCapability { get; set; }
        public double MinimumThrottle { get; set; }
        public double MaximumThrottle { get; set; }
        public string EngineCycle { get; set; } = string.Empty;
        public string FuelType { get; set; } = string.Empty;
        public string OxidizerType { get; set; } = string.Empty;
        public string CoolingMethod { get; set; } = string.Empty;
        public string IgnitionSystem { get; set; } = string.Empty;
        public string ThrustChamberMaterial { get; set; } = string.Empty;
        public string NozzleMaterial { get; set; } = string.Empty;
        public string TurbopumpType { get; set; } = string.Empty;
        public double TurbopumpSpeed { get; set; }
        public double TurbopumpPower { get; set; }
        public double TurbopumpEfficiency { get; set; }
        public string GasGeneratorType { get; set; } = string.Empty;
        public string PreburnerType { get; set; } = string.Empty;
        public double PreburnerTemperature { get; set; }
        public double PreburnerPressure { get; set; }
        public double MainChamberPressure { get; set; }
        public double MainChamberTemperature { get; set; }
        public double NozzleExitPressure { get; set; }
        public double NozzleExitTemperature { get; set; }
        public double SpecificPower { get; set; }
        public double PowerDensity { get; set; }
        public double ThermalEfficiency { get; set; }
        public double PropulsiveEfficiency { get; set; }
        public double OverallEfficiency { get; set; }
        public double Reliability { get; set; }
        public double MeanTimeBetweenFailures { get; set; }
        public double MeanTimeToRepair { get; set; }
        public double InspectionInterval { get; set; }
        public double OverhaulInterval { get; set; }
        public double LifeExpectancy { get; set; }
        public double CostPerHour { get; set; }
        public double CostPerFlight { get; set; }
        public double CostPerLaunch { get; set; }
        public double DevelopmentCost { get; set; }
        public double ProductionCost { get; set; }
        public double OperatingCost { get; set; }
        public double FuelCost { get; set; }
        public double OxidizerCost { get; set; }
        public double MaintenanceCost { get; set; }
        public double InsuranceCost { get; set; }
        public double EnvironmentalImpact { get; set; }
        public int NoiseLevel { get; set; }
        public double VibrationLevel { get; set; }
        public Dictionary<string, double> Emissions { get; set; } = new();
        public List<string> SafetyFeatures { get; set; } = new();
        public int RedundancyLevel { get; set; }
        public double FaultTolerance { get; set; }
        public List<string> FailureModes { get; set; } = new();
        public Dictionary<string, double> FailureRates { get; set; } = new();
        public List<TestResult> TestingHistory { get; set; } = new();
        public List<FlightResult> FlightHistory { get; set; } = new();
        public Dictionary<string, double> PerformanceMetrics { get; set; } = new();
        public Dictionary<string, double> InnovationMetrics { get; set; } = new();
        
        // Existing properties
        public new string Name { get; set; } = string.Empty;
        public new string Propellant { get; set; } = string.Empty;
        public new double Thrust { get; set; }
        public new double SpecificImpulse { get; set; }
        public new double ChamberPressure { get; set; }
        public Vector2 ThrottleRange { get; set; }
        public Vector3 VectoringAngles { get; set; }
        public double Weight { get; set; }
        public double Length { get; set; }
        public double Diameter { get; set; }
        public double OperatingAltitude { get; set; }
        public double OperatingSpeed { get; set; }
        public Vector2 TemperatureRange { get; set; }
        public Vector2 HumidityRange { get; set; }
        public int CompressorStages { get; set; }
        public float CombustionEfficiency { get; set; }
        public int TurbineStages { get; set; }
        public float FuelPumpEfficiency { get; set; }
        public float OxidizerPumpEfficiency { get; set; }
        public float CoolingSystemEfficiency { get; set; }
        public float IgnitionSystemReliability { get; set; }
        public float ThrottleResponseTime { get; set; }
        public float StartupTime { get; set; }
        public float ShutdownTime { get; set; }
        public bool RestartCapability { get; set; }
        public int MaxRestarts { get; set; }
        public int MaintenanceInterval { get; set; }
        public int ExpectedLifetime { get; set; }
        public double CostPerEngine { get; set; }
        public float ManufacturingComplexity { get; set; }
        public string TechnologyReadinessLevel { get; set; } = string.Empty;
        public int FlightHeritage { get; set; }
        public string RegulatoryCompliance { get; set; } = string.Empty;
        public string NuclearSafetyRating { get; set; } = string.Empty;
        public string NuclearRegulatoryCompliance { get; set; } = string.Empty;
        public string NuclearWasteHandling { get; set; } = string.Empty;
        public string NuclearFuelType { get; set; } = string.Empty;
        public string NuclearSafetyLevel { get; set; } = string.Empty;
        public string NuclearShielding { get; set; } = string.Empty;

        public override void RunDiagnostics()
        {
            Console.WriteLine($"Running diagnostics for {Name}");
            Console.WriteLine($"Thrust: {Thrust} N");
            Console.WriteLine($"Specific Impulse: {SpecificImpulse} s");
            Console.WriteLine($"Chamber Pressure: {ChamberPressure} Pa");
            Console.WriteLine($"Reliability: {Reliability:P}");
        }
    }

    public abstract class RocketEngineBase
    {
        public string Name { get; set; } = string.Empty;
        public double Thrust { get; set; }
        public double SpecificImpulse { get; set; }
        public double ChamberPressure { get; set; }
        public string Propellant { get; set; } = string.Empty;

        public abstract void RunDiagnostics();
    }

    public class TestResult
    {
        public string TestType { get; set; } = string.Empty;
        public int Duration { get; set; }
        public double Thrust { get; set; }
        public bool Success { get; set; }
    }

    public class FlightResult
    {
        public int FlightNumber { get; set; }
        public int Duration { get; set; }
        public double Altitude { get; set; }
        public bool Success { get; set; }
    }
} 