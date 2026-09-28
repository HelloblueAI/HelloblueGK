using System;

namespace HB_NLP_Research_Lab.Physics
{
    /// <summary>
    /// The inputs the schematic solvers are willing to consume. CFD and structural analysis read
    /// chamber pressure in pascals. The thermal solver reads chamber temperature in kelvin.
    ///
    /// Those solvers used to accept <c>object model</c> and never read it, so every engine
    /// produced the same fields. They now refuse a model that does not carry the value they
    /// scale by, and they have no default to substitute. The fields are still not a
    /// Navier-Stokes solution, a finite-element analysis, or a heat-transfer solution.
    /// </summary>
    public static class SolverOperatingPoint
    {
        public static double RequireChamberPressure(object model)
        {
            ArgumentNullException.ThrowIfNull(model);

            switch (model)
            {
                case EngineOperatingPoint point:
                    if (!double.IsFinite(point.ChamberPressure) || point.ChamberPressure <= 0)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(model),
                            "Chamber pressure must be a positive finite value, in pascals.");
                    }

                    return point.ChamberPressure;

                case EngineModel engine:
                    if (engine.Parameters.TryGetValue("ChamberPressure", out var raw)
                        && raw is double pascals
                        && double.IsFinite(pascals)
                        && pascals > 0)
                    {
                        return pascals;
                    }

                    throw new ArgumentException(
                        $"Engine model '{engine.Name}' has no positive ChamberPressure parameter, in pascals. "
                        + "The schematic solvers scale their fields by that value and have no default to substitute.",
                        nameof(model));

                default:
                    throw new ArgumentException(
                        $"{model.GetType().Name} does not carry a chamber pressure. Pass an "
                        + $"{nameof(EngineOperatingPoint)}, or an {nameof(EngineModel)} whose Parameters "
                        + "include \"ChamberPressure\" in pascals.",
                        nameof(model));
            }
        }

        public static double RequireChamberTemperature(object model)
        {
            ArgumentNullException.ThrowIfNull(model);

            switch (model)
            {
                case EngineOperatingPoint point:
                    if (!double.IsFinite(point.ChamberTemperature) || point.ChamberTemperature <= 0)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(model),
                            "Chamber temperature must be a positive finite value, in kelvin.");
                    }

                    return point.ChamberTemperature;

                case EngineModel engine:
                    if (engine.Parameters.TryGetValue("ChamberTemperature", out var raw)
                        && raw is double kelvin
                        && double.IsFinite(kelvin)
                        && kelvin > 0)
                    {
                        return kelvin;
                    }

                    throw new ArgumentException(
                        $"Engine model '{engine.Name}' has no positive ChamberTemperature parameter, in kelvin. "
                        + "The schematic thermal solver scales its fields by that value and has no default to substitute.",
                        nameof(model));

                default:
                    throw new ArgumentException(
                        $"{model.GetType().Name} does not carry a chamber temperature. Pass an "
                        + $"{nameof(EngineOperatingPoint)}, or an {nameof(EngineModel)} whose Parameters "
                        + "include \"ChamberTemperature\" in kelvin.",
                        nameof(model));
            }
        }
    }
}
