using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HelloblueGK.Aerospace
{
    public class ParametricEngineDesigner
    {
        public class EngineParameters
        {
            public string Name { get; set; }
            public double Thrust { get; set; } // kN
            public double SpecificImpulse { get; set; } // s
            public double ChamberPressure { get; set; } // bar
            public string Propellant { get; set; }
            public double Mass { get; set; } // kg
            public double Length { get; set; } // m
            public double Diameter { get; set; } // m
        }

        public RocketEngineBase CreateEngine(EngineParameters parameters)
        {
            return new ParametricEngine(parameters);
        }

        public async Task<List<EngineMetrics>> RunBatchSimulationAsync(List<EngineParameters> designs)
        {
            var results = new List<EngineMetrics>();
            
            foreach (var design in designs)
            {
                var engine = CreateEngine(design);
                var metrics = await CalculateEngineMetricsAsync(engine);
                results.Add(metrics);
            }
            
            return results;
        }

        private async Task<EngineMetrics> CalculateEngineMetricsAsync(RocketEngineBase engine)
        {
            await Task.Delay(10); // Simulate calculation time
            return new EngineMetrics
            {
                Thrust = engine.Thrust,
                SpecificImpulse = engine.SpecificImpulse,
                Efficiency = Math.Round(engine.SpecificImpulse / 500.0, 3),
                Reliability = 0.99
            };
        }
    }

    public class ParametricEngine : RocketEngineBase
    {
        public ParametricEngine(ParametricEngineDesigner.EngineParameters parameters)
        {
            Name = parameters.Name;
            Thrust = parameters.Thrust;
            SpecificImpulse = parameters.SpecificImpulse;
            ChamberPressure = parameters.ChamberPressure;
            Propellant = parameters.Propellant;
        }

        public override void RunDiagnostics()
        {
            Console.WriteLine($"[Parametric] {Name}: Thrust={Thrust}kN, ISP={SpecificImpulse}s, Chamber={ChamberPressure}bar");
        }
    }
} 