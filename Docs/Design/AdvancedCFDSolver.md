# Design description — AdvancedCFDSolver

Design data for the one unit currently inside the
[certification boundary](../../Certification/Artifacts/certification-boundary.json). This document is the design
artifact that requirements trace to, so the identifiers below are referenced by
`requirements[].designElementId` and must not be renamed without updating the boundary.

Scope is limited to `Physics/AdvancedCFDSolver.cs`. It describes what the code does
today, not what it might do later — a design document that outruns the implementation
cannot serve as trace evidence.

## Known limitation — the model parameter is ignored

`RunSimulation(object model)` never reads `model`. The parameter is present in the
signature and unreferenced in the body, so the solver returns identical results for
every input: pressure and velocity are functions of grid index alone, and turbulence
intensity and heat transfer derive from hardcoded constants.

This is recorded here and in the boundary artifact because it bounds what the
certification evidence means. Coverage and traceability are complete for the code as
written; that is a statement about the code, not about whether the solver analyses the
engine it is handed. The requirements traced to this document are deliberately scoped to
initialization and convergence behaviour, which the implementation genuinely exhibits,
rather than to physical accuracy, which cannot be claimed while the input is unused.

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

- `CalculatePressureDistribution` applies isentropic flow relations with γ = 1.4,
  referenced to standard sea-level pressure of 101325 Pa.
- `CalculateVelocityField` applies potential flow scaled by the speed of sound, 340 m/s.
- `CalculateTurbulenceIntensity` accumulates turbulent kinetic energy and dissipation
  rate under a k-ε formulation, returning a value normalized by the speed of sound.
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
