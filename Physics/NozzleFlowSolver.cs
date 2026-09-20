using System;

namespace HB_NLP_Research_Lab.Physics
{
    /// <summary>
    /// An <see cref="IPhysicsSolver"/> whose output is determined entirely by its input.
    ///
    /// The distinction from the other solvers in this directory is deliberate and is the reason
    /// this type exists: <see cref="AdvancedCFDSolver"/> and <see cref="AdvancedStructuralSolver"/>
    /// accept a model parameter and never read it, so they produce identical results for every
    /// engine. This solver requires an <see cref="EngineOperatingPoint"/> and rejects anything
    /// else rather than quietly falling back to defaults, because a solver that silently ignores
    /// its input is indistinguishable from one that works.
    /// </summary>
    public sealed class NozzleFlowSolver : IPhysicsSolver
    {
        public string Name => "Ideal Rocket Nozzle Solver - Quasi-1D Isentropic Expansion";

        /// <summary>
        /// No state to set up. The solver is stateless and its relations are pure functions, so
        /// results cannot depend on call order or on whether this was invoked.
        /// </summary>
        public void Initialize()
        {
        }

        /// <summary>
        /// Solves the nozzle for the supplied operating point.
        /// </summary>
        /// <param name="model">Must be an <see cref="EngineOperatingPoint"/>.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the caller passes something else. Returning a plausible-looking default
        /// would reproduce exactly the defect this solver exists to avoid.
        /// </exception>
        public PhysicsResult RunSimulation(object model)
        {
            ArgumentNullException.ThrowIfNull(model);

            if (model is not EngineOperatingPoint point)
            {
                throw new ArgumentException(
                    $"{nameof(NozzleFlowSolver)} requires an {nameof(EngineOperatingPoint)}; " +
                    $"received {model.GetType().Name}. This solver derives every output from its " +
                    "input and has no defaults to fall back on.",
                    nameof(model));
            }

            var solution = IdealRocketNozzle.Solve(point);

            return new NozzleFlowResult
            {
                Status = "Success",
                Solution = solution,
                Data = new[]
                {
                    solution.Thrust,
                    solution.SpecificImpulse,
                    solution.MassFlowRate,
                    solution.ExitVelocity
                }
            };
        }
    }

    public sealed class NozzleFlowResult : PhysicsResult
    {
        public NozzleFlowResult()
        {
            Solution = new NozzleSolution();
        }

        public NozzleSolution Solution { get; init; }
    }
}
