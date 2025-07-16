using System;

namespace HelloblueGK.Aerospace
{
    public class RaptorEngine : RocketEngineBase
    {
        public RaptorEngine()
        {
            Name = "Raptor";
            Thrust = 2300; // kN
            SpecificImpulse = 350; // s
            ChamberPressure = 300; // bar
            Propellant = "Methane/LOX";
        }

        public override void RunDiagnostics()
        {
            Console.WriteLine($"[Diagnostics] {Name} Engine: Thrust={Thrust}kN, ISP={SpecificImpulse}s, Chamber Pressure={ChamberPressure}bar");
        }
    }

    public abstract class RocketEngineBase
    {
        public string Name { get; set; }
        public double Thrust { get; set; }
        public double SpecificImpulse { get; set; }
        public double ChamberPressure { get; set; }
        public string Propellant { get; set; }

        public abstract void RunDiagnostics();
    }
} 