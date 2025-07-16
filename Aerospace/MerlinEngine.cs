using System;

namespace HB_NLP_Research_Lab.Aerospace
{
    public class MerlinEngine : RocketEngineBase
    {
        public MerlinEngine()
        {
            Name = "Merlin";
            Thrust = 845; // kN
            SpecificImpulse = 282; // s
            ChamberPressure = 98; // bar
            Propellant = "RP-1/LOX";
        }

        public override void RunDiagnostics()
        {
            Console.WriteLine($"[Diagnostics] {Name} Engine: Thrust={Thrust}kN, ISP={SpecificImpulse}s, Chamber Pressure={ChamberPressure}bar");
        }
    }
} 