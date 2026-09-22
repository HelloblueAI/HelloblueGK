# HB-NLP-REV-001 Design Summary

**This is a concept design. No engine has been built, fired, or tested, and nothing here is an
analysis result.** The figures below are the same declared inputs as
[`Aerospace/HB_NLP_RevolutionaryEngine.cs`](../../../Aerospace/HB_NLP_RevolutionaryEngine.cs).
See [VERIFICATION_SCOPE.md](../../VERIFICATION_SCOPE.md) for what this project verifies and what
it does not.

## Declared parameters

| Parameter | Value |
|-----------|-------|
| Model ID | HB-NLP-REV-001 |
| Thrust | 3,500,000 N (3.5 MN) |
| Specific impulse | 351.5 s |
| Chamber pressure | 280 bar |
| Expansion ratio | 28.5:1 |

Thrust, throat, exit, and specific impulse agree with ideal-rocket theory at this chamber
pressure and area ratio. Ideal theory neglects combustion inefficiency, friction, heat loss, and
divergence, so the numbers bound a real engine from above. They are not a measurement.
`Tests/Unit/Aerospace/EngineDesignConsistencyTests.cs` fails if the engine in code diverges from
those relations.

## Geometry

| Parameter | Value |
|-----------|-------|
| Chamber diameter | 0.65 m |
| Chamber length | 1.4 m |
| Throat diameter | 0.293 m |
| Exit diameter | 1.564192 m |
| Nozzle length | 2.8 m |

The exit diameter is the throat times √28.5, so the expansion ratio is an area ratio. No
expansion angle is declared separately.

## Materials

| Component | Name in the engine declaration |
|-----------|--------------------------------|
| Chamber | Quantum-Enhanced Chamber Alloy |
| Nozzle | Self-Healing Nozzle Composite |
| Injector | Not declared |
| Turbopump | Not declared |

The chamber and nozzle names are the ones in code. No thermal, structural, or compatibility
analysis supports them.

## What this package does not claim

No technology readiness level. The earlier package assigned TRL 9; nothing here has been built,
fired, or flown, so that assignment was removed rather than copied forward.

No efficiency, solver-convergence, hardware-utilization, or power figures. Those used to be
constants in `design.json`. They were not computed from this engine, so they were removed.

## Files

| File | Contents |
|------|----------|
| `design.json` | The declared parameters above |
| `design_script.py` | Sketch of that geometry for Plasticity. Injector, turbopump, and throat axial length are not part of the declaration |
