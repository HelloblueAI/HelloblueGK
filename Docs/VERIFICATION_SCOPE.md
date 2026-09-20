# Verification scope

This document states plainly which parts of HelloblueGK are verified engineering and which
are simulation scaffolding. It exists because the distinction is not visible from the outside:
a method named `RunQuantumCFDAnalysisAsync` that awaits `Task.Delay` and returns constants
looks, from its signature and its console output, exactly like one that solves something.

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

**Partial readiness reports crashed.** `CalculateOverallReadinessScore` guarded against an
empty category list, then averaged only the *critical* categories — which throws on an empty
sequence. Assessing only operational and financial readiness, a reasonable request, threw
`InvalidOperationException` instead of returning a score.

## What is simulation scaffolding

The following code runs, produces well-formed output, and is useful for demonstration and for
exercising interfaces. It is **not** verified engineering, and no result it produces should be
cited as an analysis of a physical system.

**The physics solvers ignore their inputs.** `AdvancedCFDSolver.RunSimulation` and
`AdvancedStructuralSolver` accept a model parameter and do not read it. Their outputs are
determined by hardcoded constants, so they are identical for every engine analysed. The CFD
solver is inside the certification boundary because its *control flow* is fully verified; the
boundary artifact records explicitly that this is not a claim about physical correctness. In
`AdvancedStructuralSolver.PredictFailure`, `maxStress > yieldStrength` compares two constants
(350 MPa against 250 MPa), so it always reports "Yield" and the "Safe" branch is unreachable.

**Most `Create*` and `Analyze*` orchestration discards its arguments.** Across
`RevolutionaryEngineArchitectures`, `HB_NLP_RevolutionaryEngine`, and
`QuantumClassicalHybridEngine`, methods taking a specification object build a hardcoded result
and ignore the specification. `AnalyzeRevolutionaryEngineAsync` awaits real physics calls and
then discards the results in favour of constants. `Task.Delay` stands in for computation.

**The genetic optimiser is unreachable.** `AIOptimizationEngine` contains a real
population/crossover/mutation implementation, but `OptimizeEngineDesignAsync` returns a fixed
result without calling it.

**`NeuralNetworkEngine.TrainAsync` ignores its training data** and returns fixed accuracy and
loss figures. The forward pass is real, but layer weights come from an unseeded `Random`, so
predictions are not reproducible between runs.

Coverage in these areas is deliberately not pursued. Tests asserting that a stub returns its
hardcoded constant would raise the coverage percentage while establishing nothing, and would
make the suite actively misleading — the number would imply verification that does not exist.
This is why `Aerospace/` carries a 49% *branch* floor against only a 20% *line* floor: the
decision logic is verified, and the constant-returning scaffolding around it is not counted as
though it were.

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
