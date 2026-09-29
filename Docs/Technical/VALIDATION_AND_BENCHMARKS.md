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
published chamber conditions and expansion ratios, it reproduces published specific impulse. The
numbers and the URL or document each one came from are in
`Tests/Unit/Physics/PublishedNozzleReference.cs`. CI runs
`Solve_ReproducesPublishedSpecificImpulseAndDoesNotUnderpredictIt` and fails the build if the
solver leaves the band.

| Engine | Published Isp | Computed | Delta |
|--------|---------------|----------|-------|
| Merlin 1D, sea level | 282.0 s | 284.2 s | +0.79% |
| Raptor, sea level, 300 bar, area ratio 34 | 330.0 s | 332.7 s | +0.83% |
| RS-25, vacuum, 109% | 452.3 s | 453.0 s | +0.15% |
| RS-25, sea level, 109% | 366.0 s | 373.8 s | +2.14% |

The RS-25 specification sheet also publishes vacuum thrust 512,300 lb and sea-level thrust
418,000 lb. The ideal vacuum-to-sea-level thrust ratio is 1.212 against the published 1.226
(1.1% low). `Solve_Rs25VacuumToSeaLevelThrustRatio_StaysInsideThePublishedBand` keeps that inside
3%. Absolute thrust is not checked: these sheets do not give a throat area, and a commonly
repeated 10.3 inch throat makes the ideal thrust fall short of the published thrust, so that
diameter is not used.

The direction of the specific-impulse error matters more than its size. Ideal theory assumes
complete combustion, no friction, no heat loss, and fully axial exit flow, so it must not fall
below a published specific impulse. A model that came in below the published value would indicate
an error in the relations or in the propellant properties rather than a better model.

The assertion band is 3% rather than 1%, deliberately wider than the agreement on the original
three points, because a tolerance tight enough to exclude the physical loss margin would be
pinning coincidence rather than testing the model.

### Points that were not added

These were looked up and left out. Adding them would mean retuning gamma, temperature, or molar
mass, or gluing numbers from different engines into one operating point.

- **Merlin 1D Vacuum, 348 s at an expansion ratio of 165.** The Merlin article states that pair
  together. With the Merlin 1D chamber pressure of 9.7 MPa and the shared RP-1/LOX assumptions,
  the ideal specific impulse is about 342 s, below the published 348 s. The same article's infobox
  lists a vacuum specific impulse of 311 s and marks it as needing an update; the 2011 Merlin 1D
  paragraph calls 310 s a design goal. Neither vacuum figure is in the gate.
- **Current Raptor infobox rows.** The page lists sea-level specific impulse 327 s, chamber
  pressure 330 bar, and a sea-level nozzle ratio of 34.34, but the 34.34 citation is a 2019 plume
  study at about 253 bar, and the thrust cells are split across Raptor 1, 2, and 3. A 2019 public
  statement gives about 350 s for a sea-level Raptor in vacuum and about 380 s with a larger
  vacuum nozzle, without an expansion ratio in that sentence. At 300 bar and an expansion ratio
  of 80 the ideal result is about 367 s, below 380 s. None of those combinations is in the gate.
- **Absolute thrust for Merlin, Raptor, or RS-25.** The RS-25 sheet publishes thrust and area
  ratio, not a throat diameter. A commonly repeated 10.3 inch throat makes ideal thrust fall
  short of the published thrust, so it is not used. Merlin and Raptor likewise have no throat
  area on the same source as the specific impulse.

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

- **The legacy CFD, thermal, and structural solvers are not validated analyses, and they are not
  a hardware-performance claim.** `AdvancedCFDSolver` and `AdvancedStructuralSolver` read chamber
  pressure and refuse a model that does not carry one. Their fields, including structural
  displacement, fatigue life, and buckling margin, are closed-form estimates rather than a
  Navier-Stokes solution or a finite-element analysis. No comparison against wind tunnel data,
  engine test data, or a reference solver has been performed for them. `AdvancedThermalSolver`
  reads chamber temperature and refuses a model that does not carry one. Its field is a linear
  conduction estimate, not a heat-transfer solution. Until those solvers are real analyses, their
  numbers stay out of any claim about how an engine performs. `NozzleFlowSolver` and
  `IdealRocketNozzle` are the nozzle path that is compared with published engine data.
- **No material property model exists.** Material temperature and strength figures in the engine
  definitions are declared constants, not predictions, and nothing validates them against literature
  databases.
- **HB-NLP-REV-001 is an idealized simulation concept.** Its declared thrust, throat, exit, and
  specific impulse are the ideal-rocket ceiling at the declared chamber pressure and area ratio.
  Those numbers are theoretical ceilings, not fired-engine performance, and they are not part of
  the validation table above. See `Docs/VERIFICATION_SCOPE.md` and
  `Tests/Unit/Aerospace/EngineDesignConsistencyTests.cs`.
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
| AS9100 / ISO 9001 | `Core/QualityAssuranceSystem.cs` implements audit checks against these criteria. They grade evidence the caller supplies; with none supplied they report non-compliance |
| FIPS 140-2 | `Aerospace/AerospaceComplianceSystem.cs` implements a compliance *check*. The Community Edition makes no FIPS claim and is not validated |

A compliance check that can fail is a tool. It is not evidence that the project passes it. Since
this repository supplies no evidence to these checks, they currently report nothing but
non-compliance — see [`TECHNICAL_LIMITATIONS_AND_ROADMAP.md`](TECHNICAL_LIMITATIONS_AND_ROADMAP.md).

## Reproducing these results

```bash
dotnet test Tests/HelloblueGK.Tests.csproj -c Release
```

The physics validation cases are in `Tests/Unit/Physics/IdealRocketNozzleTests.cs`. The coverage
floors and certification boundary are evaluated by `Tools/CertificationGate/` in CI; see
`.github/workflows/ci.yml` for the exact invocation.
