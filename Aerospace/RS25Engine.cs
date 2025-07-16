using System;

namespace HB_NLP_Research_Lab.Aerospace
{
    public class RS25Engine : RocketEngineBase
    {
        public RS25Engine()
        {
            Name = "RS-25";
            Thrust = 1860; // kN
            SpecificImpulse = 452; // s
            ChamberPressure = 207; // bar
            Propellant = "Hydrogen/LOX";
        }

        public override void RunDiagnostics()
        {
            Console.WriteLine($"[Diagnostics] {Name} Engine: Thrust={Thrust}kN, ISP={SpecificImpulse}s, Chamber Pressure={ChamberPressure}bar");
        }
    }
} 