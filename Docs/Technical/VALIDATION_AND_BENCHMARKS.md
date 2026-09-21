# Validation and Benchmarks

This document records what has actually been validated in this repository, and against what. Every
figure below is either produced by a test in `Tests/` or read from a version-controlled artifact, so
it can be reproduced from a clean checkout. Where something is not validated, it is listed as not
validated rather than omitted.

`Docs/VERIFICATION_SCOPE.md` is the companion document: it describes which parts of the codebase
contain verified logic and which are simulation scaffolding.

## Validated: quasi-1D nozzle performance against published engine data

`Physics/IdealRocketNozzle.cs` implements quasi-one-dimensional isentropic nozzle flow after Sutton
and Biblarz, *Rocket Propulsion Elements*, 9th ed., chapters 3 and 5, and NASA SP-8120. Given
published chamber conditions and expansion ratios, it reproduces the published specific impulse of
three flight engines.

| Engine | Published Isp | Computed | Delta |
|--------|---------------|----------|-------|
| Merlin 1D, sea level | 282.0 s | 284.2 s | +0.79% |
| Raptor, sea level | 330.0 s | 332.7 s | +0.83% |
| RS-25, vacuum | 452.3 s | 453.0 s | +0.15% |

Source: `Tests/Unit/Physics/IdealRocketNozzleTests.cs`,
`Solve_ReproducesPublishedSpecificImpulseOfFlightEngines`.

The direction of the error matters more than its size. Ideal theory assumes complete combustion, no
friction, no heat loss, and fully axial exit flow, so it must *overpredict* a real engine. All three
deltas are positive, and a separate test
(`Solve_DoesNotUnderpredictRealEnginePerformance`) asserts this one-sided bound directly. A model
that matched published values exactly, or came in below them, would indicate an error in the
relations or in the propellant properties rather than a better model.

The assertion band in the test is 3% rather than 1%, deliberately wider than the agreement observed,
because a tolerance tight enough to exclude the physical loss margin would be pinning coincidence
rather than testing the model.

### Invariants pinned by test

These are properties of the physics rather than comparisons to data, and each is enforced by a test:

- **Scale invariance** — thrust scales linearly with throat area while specific impulse does not
  change, since Isp is intensive.
- **Vacuum exceeds sea level** — for identical geometry, vacuum thrust and Isp both exceed sea-level
  values.
- **Thrust coefficient bounds** — stays within 1.0 to 2.3 across the tested envelope.
- **Characteristic velocity ordering** — computed c* falls in the published 1,700–2,350 m/s band and
  orders propellant combinations correctly, with hydrogen highest because its products are lightest.
- **Area–Mach round trip** — `ExitMachNumber` and `AreaRatioForMach` invert each other.
- **Internal consistency** — specific impulse and effective exhaust velocity agree by construction.

`Physics/IdealRocketNozzle.cs`, `Physics/EngineOperatingPoint.cs`, and `Physics/NozzleFlowSolver.cs`
are at 100% line and 100% branch coverage.

## Validated: certification tooling behaviour

The certification systems under `Certification/` are verified to fail closed: placeholder identity
tokens, missing separation of duties, unverified evidence, and vacuous requirement text are all
rejected rather than silently accepted. This is behavioural verification of the tooling, and is not
a statement that any artifact produced by it has been accepted by a certification authority.

The DO-178C Level A gate in `Tools/CertificationGate/` runs in CI against a deliberately narrow
declared boundary in `Certification/Artifacts/certification-boundary.json`. It verifies statement
and decision coverage, MC/DC with recorded independence pairs, requirements traceability, and
per-directory coverage floors. The boundary currently contains **one file**. The gate's own output
states what a pass does and does not establish.

## Measured: coverage

`Certification/Artifacts/coverage-floors.json` holds the authoritative, CI-enforced minimum line and
branch coverage per directory. It is checked on every run from the Cobertura report that
`dotnet test` produces, so it cannot drift from reality without failing the build.

Read that file for current values rather than relying on a number transcribed here. Coverage across
`Aerospace/` and `AI/` is low by design: much of those directories is specification data and demo
scaffolding with no decisions in it, and `Docs/VERIFICATION_SCOPE.md` explains why raising those numbers
by testing stubs would be misleading rather than useful.

## Not validated

Listed explicitly, because their absence is easy to mistake for an oversight.

- **The legacy CFD, thermal, and structural solvers do not consume their inputs.** `AdvancedCFDSolver`
  and `AdvancedStructuralSolver` accept a `model` parameter and ignore it, so their outputs are fixed
  constants rather than analyses of the system passed in. No comparison against wind tunnel data,
  engine test data, or analytical solutions has been performed for them. `NozzleFlowSolver` is the
  only solver that computes from its inputs.
- **No material property model exists.** Material temperature and strength figures in the engine
  definitions are declared constants, not predictions, and nothing validates them against literature
  databases.
- **The concept engine's declared parameters are internally inconsistent.** Its expansion ratio,
  thrust, and specific impulse disagree with each other and with first-principles theory; see
  `Docs/VERIFICATION_SCOPE.md` and `Tests/Unit/Aerospace/EngineDesignConsistencyTests.cs`. These figures
  should not be cited as the specification of anything.
- **No performance or throughput benchmarking has been conducted.** There are no measured figures for
  mesh sizes, calculations per second, memory footprint, or parallel scaling.

## Standards posture

The repository implements workflows *oriented toward* the standards below and, where noted, systems
that *evaluate* compliance against them. Nothing here constitutes certification, qualification, or
third-party assessment, and none has been sought or granted.

| Standard | What exists in this repository |
|----------|-------------------------------|
| DO-178C | Objectives-oriented tooling, and a Level A gate over a one-file declared boundary |
| NASA NPR 7150.2 | Workflow structure oriented to its software engineering requirements |
| AS9100 / ISO 9001 | `Core/QualityAssuranceSystem.cs` implements audit checks that evaluate these criteria and can report non-compliance |
| FIPS 140-2 | `Aerospace/AerospaceComplianceSystem.cs` implements a compliance *check*. The Community Edition makes no FIPS claim and is not validated |

A compliance check that can fail is a tool. It is not evidence that the project passes it.

## Reproducing these results

```bash
dotnet test Tests/HelloblueGK.Tests.csproj -c Release
```

The physics validation cases are in `Tests/Unit/Physics/IdealRocketNozzleTests.cs`. The coverage
floors and certification boundary are evaluated by `Tools/CertificationGate/` in CI; see
`.github/workflows/ci.yml` for the exact invocation.
