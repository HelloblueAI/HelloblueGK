# Design description — AdvancedCFDSolver

Design data for the one unit currently inside the
[certification boundary](../../Certification/Artifacts/certification-boundary.json). This document is the design
artifact that requirements trace to, so the identifiers below are referenced by
`requirements[].designElementId` and must not be renamed without updating the boundary.

Scope is limited to `Physics/AdvancedCFDSolver.cs`. It describes what the code does
today, not what it might do later — a design document that outruns the implementation
cannot serve as trace evidence.

## Known limitation — only chamber pressure is read

`RunSimulation` reads a positive chamber pressure from an `EngineOperatingPoint` or from
an `EngineModel` parameter of that name, and refuses any other model. Static pressure on
the fixed grid is that stagnation pressure divided by an isentropic ratio of the grid
index, using the specific-heat ratio of air. Velocity, turbulence intensity, and heat
transfer do not depend on the operating point. The velocity field is a potential-flow
shape scaled by 340 m/s. Turbulence intensity is the root-mean-square of that field's
mean kinetic energy, divided by 340 m/s. The grid sum is not used as kinetic energy:
doing so multiplies the intensity by the square root of the sample count. Heat transfer
is a Dittus-Boelter evaluation at a fixed Reynolds number. None of this is a
Navier-Stokes solution or a turbulence model.

This is recorded here and in the boundary artifact because it bounds what the
certification evidence means. Coverage and traceability are complete for the code as
written; that is a statement about the code, not about whether the solver analyses the
engine it is handed. The requirements traced to this document are deliberately scoped to
initialization and convergence behaviour, which the implementation genuinely exhibits,
rather than to physical accuracy.

## DE-CFD-INIT — Initialization guard

`RunSimulation` must produce valid fields whether or not the caller invoked
`Initialize()` first. The solver holds four 1000x1000 field arrays whose allocation is
the only initialization step, so the guard re-runs `Initialize()` when
`isInitialized` is false and is otherwise skipped.

The guard is idempotent: a second call finds the flag set and does not reallocate. This
matters because reallocating mid-sequence would discard the field state a caller may be
reading.

## DE-CFD-FIELDS — Field computation

Four independent field computations, each over the full 1000x1000 grid:

- `CalculatePressureDistribution` applies isentropic flow relations with γ = 1.4.
  The stagnation pressure is the chamber pressure the caller supplied, not a fixed
  sea-level pressure.
- `CalculateVelocityField` applies potential flow scaled by the speed of sound, 340 m/s.
- `CalculateTurbulenceIntensity` averages kinetic energy over the 1000×1000 velocity
  samples and returns `sqrt(2/3 · k_mean) / 340`. Dissipation is accumulated for the
  same samples and does not enter the reported intensity.
- `CalculateHeatTransfer` evaluates the Dittus-Boelter correlation at Re = 1e6 and
  Pr = 0.71.

Pressure and velocity are computed with `Parallel.For` over the outer index. Each
iteration writes only its own row, so no synchronization is required.

## DE-CFD-CONVERGENCE — Convergence termination

`RunConvergenceAnalysis` records an exponentially decaying residual and terminates as
soon as it falls below 1e-6. The loop carries a defensive upper bound of 1000
iterations.

That bound is unreachable. With an initial residual of 1.0 and decay `exp(-0.1 * n)`,
the threshold is crossed at iteration 139 for every possible input, so the loop always
exits through the residual test. The boundary artifact records the loop-exit outcome as
an infeasible MC/DC outcome for this reason, rather than claiming it as demonstrated.

The bound is retained deliberately: it converts a hypothetical non-terminating loop into
a bounded one, which is the property that matters for a solver on a critical path.
