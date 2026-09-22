# Verification scope

This document states plainly which parts of HelloblueGK are verified engineering and which
are simulation scaffolding. It exists because the distinction is not visible from the outside:
a method that awaits `Task.Delay` and returns constants looks, from its signature and its
console output, exactly like one that solves something.

The practice being followed here is the one aerospace software standards are built on. Under
DO-178C, a claim is only as good as the evidence traced to it, and anything outside the
verified boundary is explicitly out of scope rather than quietly assumed. Overstating scope is
the failure mode that standard exists to prevent, so the intent of this document is to make
the boundary legible to a reviewer in a few minutes.

## What is verified

Each area below is covered by tests that were demonstrated to fail when the behaviour they
describe is removed. That demonstration matters: a test that passes both with and without the
code it purports to verify is evidence of nothing.

| Area | What is established | Where |
|---|---|---|
| Certification evidence gates | DO-178C Level A coverage and requirements traceability for the declared boundary, enforced in CI | `Certification/`, `Tools/CertificationGate` |
| Coverage floors | Per-directory line and branch minimums, enforced from the Cobertura report of the same test run | `Certification/Artifacts/coverage-floors.json` |
| RL control logic | Bellman backup propagates value, reward shaping penalises overtemperature and starvation, action dynamics saturate at actuator limits, epsilon-greedy policy is reproducible under a seeded `Random` | `Tests/Unit/AI/ReinforcementLearningEngineTests.cs` |
| Compliance checks | Every declared requirement flag can independently veto a compliance claim, and the mission-critical numeric thresholds hold at their boundaries | `Tests/Unit/Aerospace/AerospaceComplianceCheckTests.cs` |
| Readiness decision logic | Mission-level requirement tables never relax as missions get stricter, go/no-go thresholds are inclusive at the boundary, critical categories are genuinely weighted | `Tests/Unit/Aerospace/AerospaceReadinessAssessmentTests.cs` |
| Engine model polymorphism | Every engine reports its performance envelope through `RocketEngineBase` | `Tests/Unit/Aerospace/EngineModelTests.cs` |
| CFD solver contract | Full decision coverage of `AdvancedCFDSolver`, including the self-initialising path | `Tests/Unit/Physics/PhysicsSolverContractTests.cs` |
| Ideal rocket nozzle | Quasi-1D isentropic nozzle solution validated against the published specific impulse of Merlin 1D, Raptor, and RS-25; scale invariance, expansion monotonicity, and the sign of the pressure term pinned | `Tests/Unit/Physics/IdealRocketNozzleTests.cs`, `Tests/Unit/Physics/NozzleFlowSolverTests.cs` |

### Defects this verification found

These were latent in code that had passed CI for months. Each is now fixed, and each fix is
pinned by a test that fails without it.

**The reinforcement-learning controller could not learn.** `UpdateQValueAsync` stored
`Q(s,a)` under a `"{state}_{action}"` key but read its lookahead term under a state-only key
that no update ever wrote. `max Q(s',a')` was therefore permanently `0.0`, and the update rule
collapsed from Q-learning to a running average of immediate reward. The agent structurally
could not learn that an action was worthwhile because of where it led — the entire purpose of
the algorithm. Nothing detected it because the code ran, converged on something, and printed
plausible rewards.

**A 2.2 MN engine reported zero thrust.** `RaptorEngine` redeclared `Name`, `Thrust`,
`SpecificImpulse`, `ChamberPressure`, and `Propellant` with `new`, which shadows rather than
overrides. The constructor filled the derived copies while every `RocketEngineBase` reference
read the inherited ones, which nothing ever assigned. Sorting engines by thrust through the
base class ranked the 845 kN Merlin above the 2.2 MN Raptor. The existing tests missed it
because they read each engine through its concrete type, which is the one view where the
shadowed copy looks correct. `VariableGeometryEngine.Name` had the same defect.

**The same engine comparison mixed units.** `RaptorEngine` declared thrust in newtons and
chamber pressure in pascals while `MerlinEngine` and `RS25Engine` declared kN and bar, so a
collection typed as `RocketEngineBase` held figures differing by three orders of magnitude.
The highest-thrust test passed throughout, because 2,200,000 outranks 1,860 regardless of
units — which is why it never caught this. Raptor now reports kN and bar like the other two,
`RocketEngineBase` documents the expected unit on each property, and a test pins all three
models to those ranges.

**A reference model carried invented test and flight records.** `RaptorEngine` declared a
`TestingHistory` of five hot fire, gimbal, throttle, restart, and endurance runs and a
`FlightHistory` of five flights, every one marked successful, alongside reliability, fault
tolerance, MTBF, per-launch and development costs, emissions, noise, failure rates, and a
technology readiness level of 9. None of it was sourced, and all of it described another
company's hardware. The model now carries only approximate figures SpaceX has published or
discussed publicly; anything else is absent rather than filled in with a plausible-looking
number.

**Partial readiness reports crashed.** `CalculateOverallReadinessScore` guarded against an
empty category list, then averaged only the *critical* categories — which throws on an empty
sequence. Assessing only operational and financial readiness, a reasonable request, threw
`InvalidOperationException` instead of returning a score.

**Every audit passed itself.** `QualityAssuranceSystem`, `SecurityAuditSystem`,
`AerospaceComplianceSystem`, and `AerospaceReadinessAssessment` constructed their own inputs —
every boolean literally `true`, every metric a passing constant such as `Reliability = 0.9999`,
`CodeCoverage = 0.95`, `DefectRate = 3.4`, and `FIPS140Compliance = true` — and then graded
those inputs against thresholds. The thresholds were real and the arithmetic was correct, but
the verdict was a tautology: AS9100, ISO 9001, Six Sigma, DO-178C, NASA NPR 7150.2, ITAR, and
FIPS 140 all reported compliant unconditionally, and the code minted certification documents
naming the FAA, NASA, and NIST as the certifying authority. Nothing about the system under
audit could change any of it.

Two further mechanisms inflated readiness. Each category score began from a `baseScore` of
0.90 to 0.98 awarded before any input was examined, and a failed check scored 0.5 rather than
0. Worse, mission *requirements* were scored as *achievements*: `TechnologyReadinessLevel` was
set from `GetTRLForMissionLevel(missionLevel)`, so declaring a mission critical enough to
require TRL 9 was what credited the system with TRL 9. The more demanding the mission, the
higher it scored.

These systems now read every input from an `AuditEvidence` instance supplied by the caller.
An absent key yields `false`, `0`, or an empty string, so an audit with no evidence fails its
thresholds instead of passing them, category scores derive entirely from supplied values, and
the certifying authority is named by the evidence rather than asserted. This repository ships
no evidence, so running the audits scores zero across every category and issues no
certification — which is the correct result for a repository that has none.
`Tests/Unit/Core/AuditEvidenceGatingTests.cs` pins that, so reintroducing a hardcoded
attestation fails the build. The mission-level requirement tables are unchanged and still
state what each mission level demands; they are simply no longer mistaken for evidence that
those demands are met.

## One solver now computes from its inputs

`NozzleFlowSolver` and the `IdealRocketNozzle` relations behind it are the first physics in this
repository whose output is a function of its argument. Given chamber pressure, chamber
temperature, propellant specific-heat ratio and molar mass, throat area, expansion ratio, and
ambient pressure, they solve the quasi-one-dimensional isentropic nozzle and return thrust,
specific impulse, mass flow, characteristic velocity, and exit conditions.

The relations are validated rather than merely self-consistent: using published chamber
conditions and area ratios, computed specific impulse lands within about 1% of the published
figures for Merlin 1D at sea level, Raptor at sea level, and RS-25 in vacuum, and the tests
additionally assert that ideal theory never *under*predicts a real engine — every loss the ideal
model neglects reduces real performance, so underprediction would indicate an error in the
algebra or the propellant properties.

What this does not claim: it is a one-dimensional equilibrium model, not a flow solver. It says
nothing about combustion stability, boundary layers, flow separation, nozzle heat transfer, or
off-design transients, and it assumes frozen chamber composition with fully axial exit flow.

`NozzleFlowSolver` throws when handed anything other than an `EngineOperatingPoint` instead of
falling back to defaults, because a solver that silently ignores its input is indistinguishable
from one that works — which is precisely the defect described next.

## What is simulation scaffolding

The following code runs, produces well-formed output, and is useful for demonstration and for
exercising interfaces. It is **not** verified engineering, and no result it produces should be
cited as an analysis of a physical system.

**The legacy physics solvers ignore their inputs.** `AdvancedCFDSolver.RunSimulation` and
`AdvancedStructuralSolver` accept a model parameter and do not read it. Their outputs are
determined by hardcoded constants, so they are identical for every engine analysed. The CFD
solver is inside the certification boundary because its *control flow* is fully verified; the
boundary artifact records explicitly that this is not a claim about physical correctness. Its
console output also advertises k-ε, k-ω, and LES turbulence models across 1,000,000 elements on
32 cores; what it actually evaluates is a closed-form expression over a 1000x1000 array using the
specific-heat ratio and gas constant of *air*, not of combustion products. Migrating its field
generation onto the validated nozzle solution is the obvious next step and is tracked separately,
because changing that file means re-deriving the MC-DC analysis recorded for it. In
`AdvancedStructuralSolver.PredictFailure`, `maxStress > yieldStrength` compares two constants
(350 MPa against 250 MPa), so it always reports "Yield" and the "Safe" branch is unreachable.

**Most `Create*` and `Analyze*` orchestration discards its arguments.** Across
`RevolutionaryEngineArchitectures` and `HB_NLP_RevolutionaryEngine`, methods taking a
specification object build a hardcoded result and ignore the specification. `AnalyzeRevolutionaryEngineAsync` awaits real physics calls and
then discards the results in favour of constants. `Task.Delay` stands in for computation.

**The genetic optimiser is unreachable.** `AIOptimizationEngine` contains a real
population/crossover/mutation implementation, but `OptimizeEngineDesignAsync` returns a fixed
result without calling it. The file is kept because it also declares `NeuralNetwork` and
`GeneticAlgorithm`, which `AutonomousEngineDesigner` uses.

**Removed rather than documented.** `QuantumClassicalHybridEngine` and `NeuralNetworkEngine`
were scaffolding that nothing constructed, and the README advertised a
`POST /api/v1/quantum/hybrid-analysis` endpoint that was never routed, a `Category=Quantum`
test filter matching no test, and a `hellobluegk_quantum_advantage` gauge that was declared
and never assigned. Documenting unreachable code as scaffolding is the right call when
something depends on it; when nothing does, deleting it and the claims it supported is
better than carrying both.

Coverage in these areas is deliberately not pursued. Tests asserting that a stub returns its
hardcoded constant would raise the coverage percentage while establishing nothing, and would
make the suite actively misleading — the number would imply verification that does not exist.
This is why `Aerospace/` carries a higher *branch* floor than *line* floor: the decision logic
is verified, and the constant-returning scaffolding around it is not counted as though it were.
`Certification/Artifacts/coverage-floors.json` holds the current numbers.

## Known limitations carried deliberately

**Parameter DTO shadowing.** `AdvancedCfdParameters`, `AdvancedMultiPhysicsParameters`, and
`EngineOptimizationParameters` shadow base-class members with `new`. Unlike the `RaptorEngine`
case, these are only ever held and read through their derived type, so the defect is latent
rather than active. They are left alone because changing them alters how physics parameters
reach the solvers, which warrants its own change with its own verification rather than being
folded into unrelated work.

**Reward shaping overlap.** In `CalculateReward`, the efficiency bonus for fuel flow below 8
still applies when flow is below the starvation threshold of 3, so a starved engine nets −5
rather than −10. The penalty still dominates, so the agent is steered away from starvation,
but the margin is smaller than the constants suggest. This is recorded and pinned by a test
rather than changed, because altering reward shaping changes what the agent learns and is a
design decision, not a bug fix.

**`Aerospace/` and `AI/` line coverage remains low** — around 21% and 18%. Given the above,
that is an accurate reflection of how much of those directories contains logic worth
verifying, not a gap to be closed by writing tests against stubs. The way to raise these
numbers honestly is to replace the scaffolding with implementations that consume their inputs,
at which point the new logic earns real tests.

## The concept engine's declared parameters are internally inconsistent

`HB_NLP_RevolutionaryEngine` is a concept design, and its declared numbers do not agree with each
other. This was found by pointing the validated `IdealRocketNozzle` relations at the declaration —
a check the repository could have run at any time and never had.

| Declared | First principles | Gap |
|----------|------------------|-----|
| `ExpansionRatio = 28.5` | The declared 0.12 m throat and 3.4 m exit give an area ratio of 802.8 | The diameter ratio is 28.33, so 28.5 appears to be a diameter ratio stored in an area-ratio field |
| `Thrust = 3.5 MN` | 280 bar through a 0.12 m throat gives 587 kN in vacuum | 6× overstated; 3.5 MN at the declared expansion ratio needs a 0.293 m throat |
| `SpecificImpulse = 420 s` | 351.5 s at the declared expansion ratio, 390.4 s even at the geometric 802.8 | 420 s is 98.6% of the propellant's thermodynamic ceiling of 425.8 s |

Ideal theory neglects combustion inefficiency, friction, heat loss, and divergence, so it bounds a
real engine from above. A declared value that exceeds it cannot be recovered by better engineering,
which is what makes these gaps errors in the declaration rather than optimism about the design.

At most one of the geometry and the expansion ratio can describe the intended engine, so these
figures should not be cited as the specification of anything. They are read by the API, the design
exports, and the Blender visualization, so the error propagates wherever they are displayed.

`Tests/Unit/Aerospace/EngineDesignConsistencyTests.cs` now gates this. The three discrepancies are
recorded there as a waiver register under configuration control rather than suppressed: a new
inconsistency fails the build, a registered one whose magnitude drifts fails the build, and
reconciling one also fails the build until its entry is deleted. The numbers cannot quietly get
worse, and they cannot quietly get better without the record being updated.

The specific impulse and ceiling checks assume a chamber temperature of 3600 K, since the engine
declares none; the geometry and thrust checks do not depend on it, because a thrust coefficient is
set by the specific heat ratio and the area ratio alone.

### Claims outside physics that are also unsupported

Recorded here rather than changed, since editing them is a decision about how the project describes
itself:

- `TechnologyReadinessLevel = "TRL 8"`. Under NPR 7123.1, TRL 8 means an actual system completed and
  flight-qualified through test and demonstration. This engine has never been built.
- `EngineType = "quantum_classical_hybrid"` and `QuantumComputing = true`, both still present after
  the quantum scaffolding they referred to was deleted.
- `InnovationLevel = 0.98`, `Efficiency = 0.96`, and `CombustionEfficiency = 0.99` have no stated
  basis or derivation.

`MixtureRatio = 3.5` is, for contrast, a reasonable oxidizer-to-fuel ratio for methane/LOX against a
stoichiometric 4.0, and a 280 bar chamber is aggressive but in the same class as Raptor's 300 bar.
