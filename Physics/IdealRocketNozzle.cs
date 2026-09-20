using System;

namespace HB_NLP_Research_Lab.Physics
{
    /// <summary>
    /// Quasi-one-dimensional ideal rocket theory: the closed-form relations that turn chamber
    /// conditions, propellant properties, and nozzle geometry into thrust, specific impulse,
    /// and exit conditions.
    ///
    /// Every function here is a pure function of its arguments. That is the entire point — the
    /// solvers in this project historically accepted a model parameter and ignored it, so their
    /// output was identical for every engine analysed. These relations cannot behave that way:
    /// change the throat area or the expansion ratio and the answer changes accordingly.
    ///
    /// Sources: Sutton and Biblarz, Rocket Propulsion Elements, 9th ed., chapters 3 and 5;
    /// NASA SP-8120 for the nozzle area-ratio relation.
    ///
    /// The assumptions are the standard ideal-rocket ones, and they are assumptions rather than
    /// approximations of this implementation: one-dimensional steady flow, a calorically perfect
    /// gas with constant specific-heat ratio, isentropic expansion with no friction or heat
    /// transfer through the nozzle, chemical equilibrium frozen at chamber composition, and
    /// axial flow at the exit plane. Real engines fall a few percent short of these numbers,
    /// which is why the theoretical values are reported separately from any efficiency factor
    /// applied to them.
    /// </summary>
    public static class IdealRocketNozzle
    {
        /// <summary>Universal gas constant, J/(mol·K).</summary>
        public const double UniversalGasConstant = 8.31446261815324;

        /// <summary>
        /// Standard gravity, m/s². Specific impulse in seconds is defined against this exact
        /// constant rather than local gravity, so it is fixed by definition.
        /// </summary>
        public const double StandardGravity = 9.80665;

        /// <summary>
        /// Specific gas constant of the combustion products, J/(kg·K), from their mean molar
        /// mass in kg/mol.
        /// </summary>
        public static double SpecificGasConstant(double molarMass)
        {
            if (molarMass <= 0)
                throw new ArgumentOutOfRangeException(nameof(molarMass), "Molar mass must be positive.");

            return UniversalGasConstant / molarMass;
        }

        /// <summary>
        /// Vandenkerckhove function Γ(γ), the mass-flow parameter of a choked nozzle. It appears
        /// in both the mass-flow and characteristic-velocity relations, so it is factored out
        /// once rather than re-derived at each call site.
        /// </summary>
        public static double VandenkerckhoveFunction(double gamma)
        {
            ValidateGamma(gamma);

            return Math.Sqrt(gamma) * Math.Pow(2.0 / (gamma + 1.0), (gamma + 1.0) / (2.0 * (gamma - 1.0)));
        }

        /// <summary>
        /// Characteristic velocity c*, m/s. This isolates the quality of the combustion from the
        /// nozzle that expands it: it depends only on chamber temperature and gas properties,
        /// not on the expansion ratio.
        /// </summary>
        public static double CharacteristicVelocity(double gamma, double molarMass, double chamberTemperature)
        {
            if (chamberTemperature <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(chamberTemperature), "Chamber temperature must be positive.");

            var r = SpecificGasConstant(molarMass);

            return Math.Sqrt(r * chamberTemperature) / VandenkerckhoveFunction(gamma);
        }

        /// <summary>
        /// Choked mass flow through the throat, kg/s. Once the nozzle is choked this is set by
        /// chamber conditions and throat area alone; nothing downstream can raise it.
        /// </summary>
        public static double MassFlowRate(
            double chamberPressure,
            double throatArea,
            double gamma,
            double molarMass,
            double chamberTemperature)
        {
            if (chamberPressure <= 0)
                throw new ArgumentOutOfRangeException(nameof(chamberPressure), "Chamber pressure must be positive.");
            if (throatArea <= 0)
                throw new ArgumentOutOfRangeException(nameof(throatArea), "Throat area must be positive.");

            return chamberPressure * throatArea / CharacteristicVelocity(gamma, molarMass, chamberTemperature);
        }

        /// <summary>
        /// Supersonic exit Mach number for an area ratio Aexit/Athroat.
        ///
        /// The area-Mach relation cannot be inverted in closed form, so this solves it
        /// numerically. Bisection is used rather than Newton's method because the relation is
        /// monotonic above Mach 1 and bisection cannot be thrown off by a poor initial guess or
        /// by the very flat gradient at high area ratios — robustness matters more here than
        /// the iteration count, since the whole solve costs microseconds.
        /// </summary>
        public static double ExitMachNumber(double gamma, double areaRatio)
        {
            ValidateGamma(gamma);

            if (areaRatio < 1.0)
                throw new ArgumentOutOfRangeException(
                    nameof(areaRatio), "Exit area cannot be smaller than the throat area.");

            if (Math.Abs(areaRatio - 1.0) < 1e-12)
                return 1.0;

            var low = 1.0;
            var high = 100.0;

            // 200 bisections on this bracket converge far below double precision; the loop is
            // bounded so a pathological input cannot hang a caller.
            for (var i = 0; i < 200; i++)
            {
                var mid = 0.5 * (low + high);

                if (AreaRatioForMach(gamma, mid) < areaRatio)
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            return 0.5 * (low + high);
        }

        /// <summary>
        /// Area ratio Aexit/Athroat required to reach a given Mach number, the forward form of
        /// the isentropic area relation.
        /// </summary>
        public static double AreaRatioForMach(double gamma, double mach)
        {
            ValidateGamma(gamma);

            if (mach <= 0)
                throw new ArgumentOutOfRangeException(nameof(mach), "Mach number must be positive.");

            var exponent = (gamma + 1.0) / (2.0 * (gamma - 1.0));
            var bracket = (2.0 / (gamma + 1.0)) * (1.0 + 0.5 * (gamma - 1.0) * mach * mach);

            return Math.Pow(bracket, exponent) / mach;
        }

        /// <summary>Static pressure at the nozzle exit, Pa, by isentropic expansion.</summary>
        public static double ExitPressure(double chamberPressure, double gamma, double exitMach)
        {
            ValidateGamma(gamma);

            var stagnationRatio = 1.0 + 0.5 * (gamma - 1.0) * exitMach * exitMach;

            return chamberPressure * Math.Pow(stagnationRatio, -gamma / (gamma - 1.0));
        }

        /// <summary>Static temperature at the nozzle exit, K.</summary>
        public static double ExitTemperature(double chamberTemperature, double gamma, double exitMach)
        {
            ValidateGamma(gamma);

            return chamberTemperature / (1.0 + 0.5 * (gamma - 1.0) * exitMach * exitMach);
        }

        /// <summary>Exit velocity, m/s, as the local Mach number times the local speed of sound.</summary>
        public static double ExitVelocity(double gamma, double molarMass, double exitTemperature, double exitMach)
        {
            ValidateGamma(gamma);

            var r = SpecificGasConstant(molarMass);

            return exitMach * Math.Sqrt(gamma * r * exitTemperature);
        }

        /// <summary>
        /// Solves the complete nozzle for an operating point.
        ///
        /// Thrust is the momentum term plus the pressure term: F = ṁ·Ve + (Pe − Pa)·Ae. The
        /// pressure term is what makes the same engine produce different thrust at sea level and
        /// in vacuum, and it is why ambient pressure is part of the operating point rather than
        /// a constant.
        /// </summary>
        public static NozzleSolution Solve(EngineOperatingPoint point)
        {
            ArgumentNullException.ThrowIfNull(point);
            point.Validate();

            var gamma = point.SpecificHeatRatio;
            var exitMach = ExitMachNumber(gamma, point.ExpansionRatio);
            var exitPressure = ExitPressure(point.ChamberPressure, gamma, exitMach);
            var exitTemperature = ExitTemperature(point.ChamberTemperature, gamma, exitMach);
            var exitVelocity = ExitVelocity(gamma, point.MolarMass, exitTemperature, exitMach);

            var massFlow = MassFlowRate(
                point.ChamberPressure,
                point.ThroatArea,
                gamma,
                point.MolarMass,
                point.ChamberTemperature);

            var exitArea = point.ThroatArea * point.ExpansionRatio;
            var momentumThrust = massFlow * exitVelocity;
            var pressureThrust = (exitPressure - point.AmbientPressure) * exitArea;
            var thrust = momentumThrust + pressureThrust;

            return new NozzleSolution
            {
                ExitMachNumber = exitMach,
                ExitPressure = exitPressure,
                ExitTemperature = exitTemperature,
                ExitVelocity = exitVelocity,
                ExitArea = exitArea,
                MassFlowRate = massFlow,
                CharacteristicVelocity = CharacteristicVelocity(gamma, point.MolarMass, point.ChamberTemperature),
                MomentumThrust = momentumThrust,
                PressureThrust = pressureThrust,
                Thrust = thrust,
                ThrustCoefficient = thrust / (point.ChamberPressure * point.ThroatArea),
                SpecificImpulse = thrust / (massFlow * StandardGravity),
                EffectiveExhaustVelocity = thrust / massFlow
            };
        }

        private static void ValidateGamma(double gamma)
        {
            if (gamma <= 1.0)
                throw new ArgumentOutOfRangeException(
                    nameof(gamma),
                    "Specific heat ratio must exceed 1; the isentropic relations are singular at 1.");
        }
    }
}
