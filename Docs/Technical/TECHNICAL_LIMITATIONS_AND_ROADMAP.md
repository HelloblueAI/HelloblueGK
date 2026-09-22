# Technical Limitations and Roadmap

What this platform does not do, and the order in which we intend to address it.
[`VERIFICATION_SCOPE.md`](../VERIFICATION_SCOPE.md) is the authoritative record of what is
verified; this document is the shorter, forward-looking companion to it.

No technology readiness level is claimed for this repository. No third-party certification,
accreditation, or qualification has been obtained. See
[`OPEN_SOURCE_SCOPE.md`](../../OPEN_SOURCE_SCOPE.md) for what the Community Edition warrants.

## Current limitations

### Solvers that do not read their input

`AdvancedCFDSolver` and `AdvancedStructuralSolver` accept a model parameter and never read it.
Their output is identical for every engine analysed, so no result either produces describes a
physical system. Several generative and orchestration methods likewise return constants after an
artificial delay. Each instance is named in `VERIFICATION_SCOPE.md`.

### The nozzle solver is one-dimensional

`IdealRocketNozzle` is the one component validated against published flight-engine data,
reproducing the specific impulse of Merlin 1D, Raptor, and RS-25 to within 1%. It is a quasi-1D
isentropic equilibrium model. It says nothing about combustion stability, boundary layers, flow
separation, nozzle heat transfer, or off-design transients, and it cannot be substituted for a
flow solver.

### The concept engine's parameters are internally inconsistent

The declared thrust of the concept engine is not consistent with its declared geometry and
chamber conditions — the discrepancy is roughly a factor of six. The deviations are measured and
gated by `Tests/Unit/Aerospace/EngineDesignConsistencyTests.cs`, which fails if their magnitude
changes, so they cannot drift unnoticed. The design package under `Docs/Designs/` declares a
different thrust and specific impulse again, for the same model ID.

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

Overall coverage is 58.4% line and 56.4% branch, enforced per directory by
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

**Correctness first.** Resolve the concept engine's parameter inconsistency so that geometry,
thrust, and specific impulse agree, and reconcile the design package with the engine defined in
code. These are the last known internal contradictions in the declared design.

**Then input-dependence.** Make the CFD and structural solvers read the model they are given, or
retire them in favour of interfaces to established external solvers. Either outcome is an
improvement on returning constants.

**Then coverage and scope.** Continue raising the per-directory floors, concentrating on the
directories currently lowest. Extend the certification boundary only as fast as real traceability
and MC/DC evidence can be produced for each added file.

**Then validation breadth.** Extend comparison against published engine data beyond the three
engines currently used, and document each new comparison with its source.

Contributions in any of these areas are welcome; see
[`CONTRIBUTING.md`](../../CONTRIBUTING.md).
