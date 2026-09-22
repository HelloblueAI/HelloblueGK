using System;
using System.Numerics;

namespace HB_NLP_Research_Lab.Aerospace
{
    /// <summary>
    /// Reference model of SpaceX's Raptor, built from approximate figures the manufacturer
    /// has published or discussed publicly. It exists so the codebase has a well-known
    /// engine to compare against; it is not vendor data and carries no accuracy guarantee.
    ///
    /// Fields this class leaves unset are unset on purpose. An earlier version declared
    /// reliability, fault tolerance, mean time between failures, per-launch and development
    /// costs, emissions, noise, failure rates, a technology readiness level, and — most
    /// seriously — a TestingHistory of five hot fire, gimbal, throttle, restart, and
    /// endurance runs plus a FlightHistory of five flights, every one of them marked
    /// successful. None of that was sourced; it was invented for another company's hardware
    /// and then presented as record. Anything not publicly documented is now simply absent
    /// rather than filled in with a plausible-looking number.
    ///
    /// Units follow MerlinEngine and RS25Engine: thrust in kN, chamber pressure in bar.
    /// </summary>
    public class RaptorEngine : RocketEngineBase
    {
        public RaptorEngine() : base()
        {
            Name = "Raptor Engine";
            Propellant = "Methane/LOX";
            Thrust = 2200; // kN, sea level
            SpecificImpulse = 330; // s
            ChamberPressure = 300; // bar

            FuelType = "Methane";
            OxidizerType = "LOX";
            EngineCycle = "Full-Flow Staged Combustion";
            CoolingMethod = "Regenerative";
            IgnitionSystem = "Torch";
            MixtureRatio = 3.6f; // O/F

            Weight = 1600; // kg, dry
            Length = 3.1; // m
            Diameter = 1.3; // m

            MinimumThrottle = 0.4;
            MaximumThrottle = 1.0;
            ThrottleRange = new Vector2(0.4f, 1.0f);
            GimbalRange = new Vector3(15.0f, 15.0f, 0.0f); // degrees
            RestartCapability = true;
            DeepThrottleCapability = true;
        }

        public string FuelType { get; set; } = string.Empty;
        public string OxidizerType { get; set; } = string.Empty;
        public string EngineCycle { get; set; } = string.Empty;
        public string CoolingMethod { get; set; } = string.Empty;
        public string IgnitionSystem { get; set; } = string.Empty;
        public float MixtureRatio { get; set; }

        public double Weight { get; set; }
        public double Length { get; set; }
        public double Diameter { get; set; }

        public double MinimumThrottle { get; set; }
        public double MaximumThrottle { get; set; }
        public Vector2 ThrottleRange { get; set; }
        public Vector3 GimbalRange { get; set; }
        public bool RestartCapability { get; set; }
        public bool DeepThrottleCapability { get; set; }

        // Name, Propellant, Thrust, SpecificImpulse, and ChamberPressure are inherited from
        // RocketEngineBase and deliberately not redeclared here. Redeclaring them with `new`
        // shadows rather than overrides: the constructor would populate the derived copies
        // while every RocketEngineBase reference — any list of engines, any comparison across
        // engine types — kept reading the base copies, which nothing ever assigned, and so
        // reported a thrust of zero for a 2.2 MN engine.

        public override void RunDiagnostics()
        {
            Console.WriteLine($"[Diagnostics] {Name}: Thrust={Thrust}kN, ISP={SpecificImpulse}s, Chamber Pressure={ChamberPressure}bar, Cycle={EngineCycle}");
        }
    }

    public abstract class RocketEngineBase
    {
        /// <summary>Sea-level thrust in kN.</summary>
        public double Thrust { get; set; }

        /// <summary>Specific impulse in seconds.</summary>
        public double SpecificImpulse { get; set; }

        /// <summary>Chamber pressure in bar.</summary>
        public double ChamberPressure { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Propellant { get; set; } = string.Empty;

        public abstract void RunDiagnostics();
    }
}
