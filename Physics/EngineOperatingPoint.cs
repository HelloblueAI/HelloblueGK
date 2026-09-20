using System;

namespace HB_NLP_Research_Lab.Physics
{
    /// <summary>
    /// The complete set of inputs an ideal-rocket nozzle solution depends on. Everything is SI,
    /// stated explicitly per property, because mixing bar with pascals or kg/mol with g/mol is
    /// the most common way this class of calculation goes silently wrong.
    ///
    /// This type exists so a solver has something concrete to consume. The physics solvers in
    /// this project took an <c>object model</c> and never read it, which made their results the
    /// same for every engine; an operating point makes the dependency explicit and checkable.
    /// </summary>
    public sealed class EngineOperatingPoint
    {
        /// <summary>Stagnation pressure in the combustion chamber, Pa.</summary>
        public double ChamberPressure { get; init; }

        /// <summary>
        /// Stagnation (flame) temperature in the chamber, K. This is the equilibrium combustion
        /// temperature, not a wall temperature.
        /// </summary>
        public double ChamberTemperature { get; init; }

        /// <summary>
        /// Ratio of specific heats of the combustion products, dimensionless. Around 1.2 for
        /// hydrocarbon/LOX and 1.25 or so for hydrogen/LOX — notably not the 1.4 of cold air,
        /// which is what these solvers previously assumed.
        /// </summary>
        public double SpecificHeatRatio { get; init; }

        /// <summary>
        /// Mean molar mass of the combustion products, kg/mol. Hydrogen/oxygen products are light
        /// (about 0.013 kg/mol), which is the main reason hydrogen engines reach high specific
        /// impulse.
        /// </summary>
        public double MolarMass { get; init; }

        /// <summary>Nozzle throat cross-sectional area, m². Sets the mass flow once choked.</summary>
        public double ThroatArea { get; init; }

        /// <summary>Nozzle area ratio Aexit/Athroat, dimensionless and at least 1.</summary>
        public double ExpansionRatio { get; init; }

        /// <summary>
        /// Ambient back pressure, Pa. 101325 for sea level, 0 for vacuum. This is what separates
        /// an engine's sea-level rating from its vacuum rating.
        /// </summary>
        public double AmbientPressure { get; init; }

        /// <summary>
        /// Throws if any value is outside the range the isentropic relations are defined on.
        /// Failing here is much better than returning a NaN that propagates into a result object
        /// and gets reported as an engine performance figure.
        /// </summary>
        public void Validate()
        {
            if (ChamberPressure <= 0)
                throw new ArgumentOutOfRangeException(nameof(ChamberPressure), "Chamber pressure must be positive.");

            if (ChamberTemperature <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(ChamberTemperature), "Chamber temperature must be positive.");

            if (SpecificHeatRatio <= 1.0)
                throw new ArgumentOutOfRangeException(
                    nameof(SpecificHeatRatio), "Specific heat ratio must exceed 1.");

            if (MolarMass <= 0)
                throw new ArgumentOutOfRangeException(nameof(MolarMass), "Molar mass must be positive.");

            if (ThroatArea <= 0)
                throw new ArgumentOutOfRangeException(nameof(ThroatArea), "Throat area must be positive.");

            if (ExpansionRatio < 1.0)
                throw new ArgumentOutOfRangeException(
                    nameof(ExpansionRatio), "Expansion ratio cannot be below 1.");

            if (AmbientPressure < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(AmbientPressure), "Ambient pressure cannot be negative.");
        }
    }

    /// <summary>
    /// Result of solving a nozzle. Momentum and pressure thrust are reported separately because
    /// their relative size is what tells you whether a nozzle is over- or under-expanded for the
    /// altitude it is operating at.
    /// </summary>
    public sealed class NozzleSolution
    {
        /// <summary>Mach number at the exit plane, dimensionless.</summary>
        public double ExitMachNumber { get; init; }

        /// <summary>Static pressure at the exit plane, Pa.</summary>
        public double ExitPressure { get; init; }

        /// <summary>Static temperature at the exit plane, K.</summary>
        public double ExitTemperature { get; init; }

        /// <summary>Exhaust velocity at the exit plane, m/s.</summary>
        public double ExitVelocity { get; init; }

        /// <summary>Nozzle exit area, m².</summary>
        public double ExitArea { get; init; }

        /// <summary>Propellant mass flow, kg/s.</summary>
        public double MassFlowRate { get; init; }

        /// <summary>Characteristic velocity c*, m/s — a measure of combustion quality alone.</summary>
        public double CharacteristicVelocity { get; init; }

        /// <summary>Momentum contribution to thrust, N.</summary>
        public double MomentumThrust { get; init; }

        /// <summary>
        /// Pressure contribution to thrust, N. Negative when the nozzle is over-expanded for the
        /// ambient pressure, which is a real and correct loss rather than an error.
        /// </summary>
        public double PressureThrust { get; init; }

        /// <summary>Total thrust, N.</summary>
        public double Thrust { get; init; }

        /// <summary>Thrust coefficient Cf = F/(Pc·At), dimensionless.</summary>
        public double ThrustCoefficient { get; init; }

        /// <summary>Specific impulse, s, referenced to standard gravity.</summary>
        public double SpecificImpulse { get; init; }

        /// <summary>Effective exhaust velocity c = F/ṁ, m/s.</summary>
        public double EffectiveExhaustVelocity { get; init; }
    }
}
