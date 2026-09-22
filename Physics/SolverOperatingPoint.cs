using System;

namespace HB_NLP_Research_Lab.Physics
{
    /// <summary>
    /// The one input the schematic CFD and structural solvers are willing to consume: chamber
    /// pressure in pascals.
    ///
    /// Those solvers used to accept <c>object model</c> and never read it, so every engine
    /// produced the same fields. They now refuse a model that does not carry a positive, finite
    /// chamber pressure, and they scale their schematic fields by the value they were given.
    /// The fields are still not a Navier-Stokes solution or a finite-element analysis.
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
    }
}
