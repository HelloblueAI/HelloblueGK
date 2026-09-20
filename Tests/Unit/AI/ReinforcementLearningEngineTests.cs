using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using HB_NLP_Research_Lab.AI;
using Xunit;

namespace HelloblueGK.Tests.Unit.AI;

/// <summary>
/// Verification of the reinforcement-learning controller's decision logic.
///
/// The engine selects throttle and fuel-flow actions, so what matters is not that it runs
/// but that its value update, its reward shaping, and its exploration policy compute what
/// the algorithm says they compute. These tests drive the units directly with a seeded
/// Random so every outcome is reproducible; the training loop itself is left alone because
/// it is a simulated environment with Task.Delay pacing, and pinning it would verify the
/// simulation rather than the controller.
/// </summary>
public class ReinforcementLearningEngineTests
{
    private static EngineState State(
        double thrust = 1000,
        double temperature = 1200,
        double pressure = 100,
        double fuelFlow = 7)
        => new() { Thrust = thrust, Temperature = temperature, Pressure = pressure, FuelFlow = fuelFlow };

    private static ReinforcementLearningEngine Engine(
        double learningRate = 0.5,
        double discountFactor = 0.9,
        double epsilon = 0.0,
        int seed = 20260920)
        => new(
            new RLConfig
            {
                LearningRate = learningRate,
                DiscountFactor = discountFactor,
                Epsilon = epsilon
            },
            new Random(seed));

    // ---------------------------------------------------------------------------------
    // Bellman backup
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// The defect this pins: the lookahead term was read with a state-only key that no
    /// update ever wrote, so max Q(s',a') was permanently 0.0 and the update degenerated
    /// into a running average of immediate reward. An agent in that state cannot learn
    /// that an action is worthwhile because of where it leads, which is the entire purpose
    /// of Q-learning. Here the successor state already has value, so a zero-reward
    /// transition into it must still raise Q.
    /// </summary>
    [Fact]
    public async Task UpdateQValueAsync_PropagatesValueBackwardFromSuccessorState()
    {
        var engine = Engine(learningRate: 0.5, discountFactor: 0.9);
        var start = State(thrust: 1000);
        var successor = State(thrust: 1100);

        // Give the successor state a known value under one of its actions.
        await engine.UpdateQValueAsync(successor, EngineAction.IncreaseThrust, reward: 100, nextState: State(thrust: 1200));
        var successorValue = engine.MaxQOver(successor);
        successorValue.Should().BeApproximately(50.0, 1e-9, "0 + 0.5 * (100 + 0.9 * 0 - 0)");

        // A transition into that successor earning no immediate reward at all.
        await engine.UpdateQValueAsync(start, EngineAction.MaintainOptimal, reward: 0, nextState: successor);

        // 0 + 0.5 * (0 + 0.9 * 50 - 0) = 22.5. With the lookahead term broken this is 0.
        engine.QValueFor(start, EngineAction.MaintainOptimal)
            .Should().BeApproximately(22.5, 1e-9);
    }

    /// <summary>
    /// The lookahead must be the maximum over the successor's actions, not the value of
    /// whichever action happened to be updated last. Taking the wrong one would make the
    /// agent pessimistic about states it has already found a good move in.
    /// </summary>
    [Fact]
    public async Task MaxQOver_TakesBestActionNotMostRecentlyUpdated()
    {
        var engine = Engine(learningRate: 1.0, discountFactor: 0.0);
        var state = State();

        await engine.UpdateQValueAsync(state, EngineAction.IncreaseThrust, reward: 80, nextState: State());
        await engine.UpdateQValueAsync(state, EngineAction.DecreaseThrust, reward: 10, nextState: State());

        engine.MaxQOver(state).Should().BeApproximately(80.0, 1e-9);
    }

    /// <summary>
    /// Q(s,a) is per action. If the key collapsed to the state alone, every action would
    /// share one estimate and the greedy policy could never prefer one over another.
    /// </summary>
    [Fact]
    public async Task UpdateQValueAsync_KeepsActionsIndependentWithinAState()
    {
        var engine = Engine(learningRate: 1.0, discountFactor: 0.0);
        var state = State();

        await engine.UpdateQValueAsync(state, EngineAction.IncreaseThrust, reward: 40, nextState: State());

        engine.QValueFor(state, EngineAction.IncreaseThrust).Should().BeApproximately(40.0, 1e-9);
        engine.QValueFor(state, EngineAction.DecreaseThrust).Should().Be(0.0);
    }

    /// <summary>
    /// A zero discount factor must reduce the update to immediate reward only. This is the
    /// boundary that distinguishes a myopic agent from a broken lookahead, and it is worth
    /// pinning precisely because the two are indistinguishable by observing behaviour alone.
    /// </summary>
    [Fact]
    public async Task UpdateQValueAsync_WithZeroDiscount_IgnoresSuccessorValue()
    {
        var engine = Engine(learningRate: 1.0, discountFactor: 0.0);
        var start = State(thrust: 1000);
        var successor = State(thrust: 1100);

        await engine.UpdateQValueAsync(successor, EngineAction.IncreaseThrust, reward: 100, nextState: State());
        await engine.UpdateQValueAsync(start, EngineAction.MaintainOptimal, reward: 5, nextState: successor);

        engine.QValueFor(start, EngineAction.MaintainOptimal).Should().BeApproximately(5.0, 1e-9);
    }

    /// <summary>
    /// Repeated identical transitions must converge on the reward rather than diverge or
    /// oscillate. With learning rate 0.5 and no discount the error halves each visit.
    /// </summary>
    [Fact]
    public async Task UpdateQValueAsync_ConvergesTowardRewardOnRepeatedVisits()
    {
        var engine = Engine(learningRate: 0.5, discountFactor: 0.0);
        var state = State();

        for (var visit = 0; visit < 20; visit++)
        {
            await engine.UpdateQValueAsync(state, EngineAction.IncreaseFuelFlow, reward: 10, nextState: State());
        }

        engine.QValueFor(state, EngineAction.IncreaseFuelFlow).Should().BeApproximately(10.0, 1e-4);
    }

    // ---------------------------------------------------------------------------------
    // Greedy policy
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// With epsilon at zero the policy is purely greedy, so the learned best action must be
    /// the one selected. This is the link between the value table and actual engine commands.
    /// </summary>
    [Fact]
    public async Task SelectOptimalActionAsync_WithoutExploration_ChoosesHighestValuedAction()
    {
        var engine = Engine(epsilon: 0.0, learningRate: 1.0, discountFactor: 0.0);
        var state = State();

        await engine.UpdateQValueAsync(state, EngineAction.DecreaseFuelFlow, reward: 75, nextState: State());

        (await engine.SelectOptimalActionAsync(state)).Should().Be(EngineAction.DecreaseFuelFlow);
    }

    /// <summary>
    /// With epsilon at 1.0 every step explores, so selection must not be pinned to the
    /// greedy action. Seeded Random makes this deterministic rather than flaky.
    /// </summary>
    [Fact]
    public async Task SelectOptimalActionAsync_WithFullExploration_DoesNotCollapseToOneAction()
    {
        var engine = Engine(epsilon: 1.0, learningRate: 1.0, discountFactor: 0.0);
        var state = State();

        await engine.UpdateQValueAsync(state, EngineAction.MaintainOptimal, reward: 1000, nextState: State());

        var chosen = new List<EngineAction>();
        for (var i = 0; i < 40; i++)
        {
            chosen.Add(await engine.SelectOptimalActionAsync(state));
        }

        chosen.Distinct().Should().HaveCountGreaterThan(1);
    }

    /// <summary>
    /// An untouched state has no preference to express. Returning the first action rather
    /// than throwing is the documented behaviour, and callers rely on always getting a
    /// command back.
    /// </summary>
    [Fact]
    public async Task SelectOptimalActionAsync_OnUnseenState_ReturnsAValidAction()
    {
        var engine = Engine(epsilon: 0.0);

        var action = await engine.SelectOptimalActionAsync(State(thrust: 1234, fuelFlow: 9.5));

        Enum.IsDefined(action).Should().BeTrue();
    }

    // ---------------------------------------------------------------------------------
    // Reward shaping
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// The thrust band is the controller's primary objective, so its edges are worth
    /// pinning exactly. 800 and 1200 are inside the band; a step outside flips the sign of
    /// the contribution from +10 to -5.
    /// </summary>
    [Theory]
    [InlineData(800, true)]
    [InlineData(1000, true)]
    [InlineData(1200, true)]
    [InlineData(799.9, false)]
    [InlineData(1200.1, false)]
    public void CalculateReward_RewardsThrustInsideBandAndPenalisesOutside(double thrust, bool insideBand)
    {
        // Temperature below the limit and fuel flow between 3 and 8 isolate the thrust term:
        // inside the band scores 10 + 5, outside scores -5 + 5.
        var next = State(thrust: thrust, temperature: 1200, fuelFlow: 7);

        var reward = ReinforcementLearningEngine.CalculateReward(State(), EngineAction.MaintainOptimal, next);

        reward.Should().Be(insideBand ? 15 : 0);
    }

    /// <summary>
    /// Overtemperature is the safety-relevant term: it must outweigh the reward for being
    /// on-target so the agent cannot be paid to run the chamber hot. 1400 K is the limit
    /// and is not itself a violation.
    /// </summary>
    [Theory]
    [InlineData(1400, 15)]
    [InlineData(1400.1, -5)]
    [InlineData(2000, -5)]
    public void CalculateReward_PenalisesOvertemperatureEnoughToOutweighOnTargetThrust(
        double temperature,
        double expected)
    {
        var next = State(thrust: 1000, temperature: temperature, fuelFlow: 7);

        ReinforcementLearningEngine.CalculateReward(State(), EngineAction.MaintainOptimal, next)
            .Should().Be(expected);
    }

    /// <summary>
    /// Documents reward shaping that is easy to misread: the efficiency bonus for fuel flow
    /// below 8 still applies when flow is below the 3 minimum, so a starved engine collects
    /// +5 and -10 for a net -5 rather than the full -10 penalty. The penalty still dominates,
    /// so the agent is steered away from starvation, but the margin is smaller than the
    /// constants suggest. Recorded rather than silently changed, because altering the
    /// shaping changes what the agent learns and is a design decision, not a fix.
    /// </summary>
    [Fact]
    public void CalculateReward_EfficiencyBonusOverlapsStarvationPenalty()
    {
        var onTarget = State(thrust: 1000, temperature: 1200, fuelFlow: 7);
        var starved = State(thrust: 1000, temperature: 1200, fuelFlow: 2.5);

        ReinforcementLearningEngine.CalculateReward(State(), EngineAction.DecreaseFuelFlow, onTarget)
            .Should().Be(15, "10 for thrust in band, 5 for flow under 8");

        ReinforcementLearningEngine.CalculateReward(State(), EngineAction.DecreaseFuelFlow, starved)
            .Should().Be(5, "10 + 5 - 10: the efficiency bonus offsets part of the starvation penalty");

        ReinforcementLearningEngine.CalculateReward(State(), EngineAction.DecreaseFuelFlow, starved)
            .Should().BeLessThan(
                ReinforcementLearningEngine.CalculateReward(State(), EngineAction.DecreaseFuelFlow, onTarget),
                "starvation must still be worse than operating on target");
    }

    /// <summary>
    /// Worst case must be unambiguously the lowest scoring: out of band, overtemperature,
    /// and starved together.
    /// </summary>
    [Fact]
    public void CalculateReward_CompoundsPenaltiesForSimultaneousViolations()
    {
        var bad = State(thrust: 1500, temperature: 1600, fuelFlow: 2);

        // -5 out of band, -20 overtemperature, +5 under 8, -10 starved.
        ReinforcementLearningEngine.CalculateReward(State(), EngineAction.IncreaseThrust, bad)
            .Should().Be(-30);
    }

    // ---------------------------------------------------------------------------------
    // Action dynamics
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Each action must move the state in its own direction, and the actuator limits must
    /// hold. Without the clamps the agent could drive thrust unbounded by repeating one
    /// action, so the saturation points are the safety-relevant behaviour here.
    /// </summary>
    [Fact]
    public async Task SimulateActionAsync_IncreaseThrustRaisesThrustAndSaturatesAtLimit()
    {
        var engine = Engine();

        var raised = await engine.SimulateActionAsync(State(thrust: 1000), EngineAction.IncreaseThrust);
        raised.Thrust.Should().BeApproximately(1100, 1e-9);
        raised.Temperature.Should().BeApproximately(1250, 1e-9, "heating accompanies more thrust");
        raised.FuelFlow.Should().BeApproximately(7.5, 1e-9);

        var atCeiling = await engine.SimulateActionAsync(State(thrust: 1_000_000), EngineAction.IncreaseThrust);
        atCeiling.Thrust.Should().Be(1500);
    }

    [Fact]
    public async Task SimulateActionAsync_DecreaseThrustLowersThrustAndSaturatesAtFloor()
    {
        var engine = Engine();

        var lowered = await engine.SimulateActionAsync(State(thrust: 1000), EngineAction.DecreaseThrust);
        lowered.Thrust.Should().BeApproximately(900, 1e-9);
        lowered.Temperature.Should().BeApproximately(1170, 1e-9);

        var atFloor = await engine.SimulateActionAsync(State(thrust: 1), EngineAction.DecreaseThrust);
        atFloor.Thrust.Should().Be(300);
    }

    [Fact]
    public async Task SimulateActionAsync_FuelFlowActionsClampToTheirLimits()
    {
        var engine = Engine();

        var more = await engine.SimulateActionAsync(State(fuelFlow: 1000), EngineAction.IncreaseFuelFlow);
        more.FuelFlow.Should().Be(15);

        var less = await engine.SimulateActionAsync(State(fuelFlow: 0.1), EngineAction.DecreaseFuelFlow);
        less.FuelFlow.Should().Be(2);
    }

    /// <summary>
    /// MaintainOptimal perturbs temperature and pressure to model an unsteady plant, so it
    /// must leave the commanded quantities untouched and stay within its stated band. The
    /// seeded Random keeps this a fixed assertion rather than a flaky one.
    /// </summary>
    [Fact]
    public async Task SimulateActionAsync_MaintainOptimalLeavesCommandedQuantitiesAlone()
    {
        var engine = Engine();
        var state = State(thrust: 1000, temperature: 1200, pressure: 100, fuelFlow: 7);

        var next = await engine.SimulateActionAsync(state, EngineAction.MaintainOptimal);

        next.Thrust.Should().Be(1000);
        next.FuelFlow.Should().Be(7);
        next.Temperature.Should().BeInRange(1195, 1205);
        next.Pressure.Should().BeInRange(97.5, 102.5);
    }

    /// <summary>
    /// The simulator must not mutate the state it was handed; the training loop keeps the
    /// previous state to compute a reward against, and aliasing would corrupt it.
    /// </summary>
    [Fact]
    public async Task SimulateActionAsync_DoesNotMutateTheInputState()
    {
        var engine = Engine();
        var state = State(thrust: 1000, temperature: 1200, pressure: 100, fuelFlow: 7);

        var next = await engine.SimulateActionAsync(state, EngineAction.IncreaseThrust);

        state.Thrust.Should().Be(1000);
        state.Temperature.Should().Be(1200);
        state.FuelFlow.Should().Be(7);
        next.Should().NotBeSameAs(state);
    }

    // ---------------------------------------------------------------------------------
    // Key schema
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// The state key quantises continuous telemetry, which is what lets a table-based agent
    /// generalise at all. States that round together must share an entry, and states that
    /// do not must stay separate, or the table either never matches or conflates distinct
    /// operating points.
    /// </summary>
    [Fact]
    public void QKey_QuantisesTelemetrySoNearbyStatesShareAnEntry()
    {
        var a = State(thrust: 1000.4, temperature: 1200.2, pressure: 100.1, fuelFlow: 7.02);
        var b = State(thrust: 1000.1, temperature: 1199.8, pressure: 100.4, fuelFlow: 7.04);
        var far = State(thrust: 1400, temperature: 1200, pressure: 100, fuelFlow: 7);

        ReinforcementLearningEngine.QKey(a, EngineAction.IncreaseThrust)
            .Should().Be(ReinforcementLearningEngine.QKey(b, EngineAction.IncreaseThrust));

        ReinforcementLearningEngine.QKey(far, EngineAction.IncreaseThrust)
            .Should().NotBe(ReinforcementLearningEngine.QKey(a, EngineAction.IncreaseThrust));
    }

    /// <summary>
    /// Same state, different action must never collide. This is the invariant whose
    /// violation made the lookahead term permanently zero.
    /// </summary>
    [Fact]
    public void QKey_DistinguishesEveryActionWithinAState()
    {
        var state = State();

        var keys = Enum.GetValues<EngineAction>()
            .Select(action => ReinforcementLearningEngine.QKey(state, action))
            .ToList();

        keys.Distinct().Should().HaveCount(keys.Count);
    }
}
