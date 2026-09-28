# Technical Limitations and Roadmap

What this platform does not do, and the order in which we intend to address it.
[`VERIFICATION_SCOPE.md`](../VERIFICATION_SCOPE.md) is the authoritative record of what is
verified; this document is the shorter, forward-looking companion to it.

No technology readiness level is claimed for this repository. No third-party certification,
accreditation, or qualification has been obtained. See
[`OPEN_SOURCE_SCOPE.md`](../../OPEN_SOURCE_SCOPE.md) for what the Community Edition warrants.

## Current limitations

### The schematic solvers are not flow or structural analysis

`AdvancedCFDSolver` and `AdvancedStructuralSolver` now refuse a model that does not carry a
chamber pressure, and they scale their fields by the value they are given. Structural displacement,
fatigue life, and buckling margin follow that pressure, and the safety-factor map reports the
computed margins. The fields are still a closed-form estimate on a fixed grid and a thin-wall
stress estimate, not a Navier-Stokes solution and not a finite-element analysis.
`AdvancedThermalSolver` still does not read its input. Several
generative and orchestration methods likewise return constants after an artificial delay. Each
instance is named in `VERIFICATION_SCOPE.md`.

### The nozzle solver is one-dimensional

`IdealRocketNozzle` is the one component validated against published flight-engine data,
reproducing the specific impulse of Merlin 1D, Raptor, and RS-25 to within 1%. It is a quasi-1D
isentropic equilibrium model. It says nothing about combustion stability, boundary layers, flow
separation, nozzle heat transfer, or off-design transients, and it cannot be substituted for a
flow solver.

### The compliance audits need evidence this repository does not ship

The quality, security, and readiness subsystems used to assert their own inputs — every boolean
set to `true` and every metric to a passing constant — and then grade those inputs against
thresholds, which made a passing verdict unconditional. They now read their inputs from an
`AuditEvidence` instance supplied by the caller, and a key that is absent fails the threshold that
depends on it.

Because this repository ships no such evidence, the audits deliberately report nothing: no
certification is issued and every readiness category scores zero. The decision logic is sound and
tested, but these subsystems say nothing about a running system until an operator supplies
measured evidence, and supplying it is outside the scope of the Community Edition.

### Coverage is uneven

Overall coverage is 58.0% line and 56.4% branch, enforced per directory by
[`coverage-floors.json`](../../Certification/Artifacts/coverage-floors.json), which is the
authoritative source. It ranges from above 90% in `Certification/` to below 30% in `AI/`. The
floors ratchet upward rather than describing a finished state.

### The certification boundary is deliberately narrow

The DO-178C Level A gate applies to a small declared set of files that genuinely meet its
objectives, not to the repository as a whole. A passing gate demonstrates statement and decision
coverage, MC/DC with recorded independence pairs, and requirements traceability for those files
only. It is not a certification, and it is not an airworthiness finding.

### Capabilities that do not exist

No quantum hardware integration and no quantum advantage of any kind. No novel materials have
been discovered or validated; the material data is standard published aerospace properties. No
hardware interfaces, real-time control loops, or flight software. Nothing here has been built,
fired, or flown.

## Roadmap

Ordered by priority rather than by date, because dates for unfunded work are guesses.

**Input-dependence.** Replace the schematic CFD, structural, and thermal fields with solvers
that consume a real operating point, or retire them in favour of interfaces to established external
solvers. The CFD and structural solvers already refuse a model with no chamber pressure; they are
still not flow or structural analysis, and the thermal solver still ignores its input.

**Then coverage and scope.** Continue raising the per-directory floors, concentrating on the
directories currently lowest. Extend the certification boundary only as fast as real traceability
and MC/DC evidence can be produced for each added file.

**Then validation breadth.** Extend comparison against published engine data beyond the three
engines currently used, and document each new comparison with its source.

Contributions in any of these areas are welcome; see
[`CONTRIBUTING.md`](../../CONTRIBUTING.md).
