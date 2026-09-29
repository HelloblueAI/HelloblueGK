using System;
using FluentAssertions;
using HB_NLP_Research_Lab.Physics;
using Xunit;

namespace HelloblueGK.Tests.Unit.Physics;

/// <summary>
/// Verification of the ideal-rocket nozzle relations.
///
/// Two kinds of test here, and the distinction matters. The first compares computed specific
/// impulse, and where the data allow it thrust, against published figures for flight engines.
/// That catalog is <see cref="PublishedNozzleReference"/>. It is the only evidence that the
/// formulas describe reality rather than merely being self-consistent. The second pins physical
/// invariants — scale invariance, monotonicity, the sign of the pressure term — which catch
/// algebra errors that a single-point comparison would let through.
///
/// Propellant properties are literature values for each propellant combination, not figures
/// tuned per engine. Chamber pressure and expansion ratio are the published values for the
/// specific engine.
/// </summary>
public class IdealRocketNozzleTests
{
    private const double SeaLevelPressure = 101325.0;

    /// <summary>
    /// Specific impulse is independent of throat area, so a representative area suffices for any
    /// test that only looks at Isp. Merlin's true throat area is not published.
    /// </summary>
    private const double ReferenceThroatArea = 0.05;

    private static EngineOperatingPoint Merlin1DSeaLevel() =>
        PublishedNozzleReference.OperatingPoint(
            PublishedNozzleReference.SpecificImpulseById("merlin-1d-sea-level"),
            ReferenceThroatArea);

    private static EngineOperatingPoint RaptorSeaLevel() =>
        PublishedNozzleReference.OperatingPoint(
            PublishedNozzleReference.SpecificImpulseById("raptor-sea-level"),
            ReferenceThroatArea);

    private static EngineOperatingPoint RS25Vacuum() =>
        PublishedNozzleReference.OperatingPoint(
            PublishedNozzleReference.SpecificImpulseById("rs-25-vacuum"),
            ReferenceThroatArea);

    // ---------------------------------------------------------------------------------
    // Validation against published engine performance
    // ---------------------------------------------------------------------------------

    public static IEnumerable<object[]> PublishedSpecificImpulseIds() =>
        PublishedNozzleReference.SpecificImpulse.Select(point => new object[] { point.Id });

    /// <summary>
    /// The central claim: given published chamber conditions and nozzle geometry, these relations
    /// reproduce the published specific impulse of real engines, and they do not underpredict it.
    ///
    /// The 3% band is deliberately wider than the agreement on the original three points. Ideal
    /// theory should sit above a real engine, since it assumes no combustion inefficiency, no
    /// friction, no heat loss, and fully axial exit flow. A tolerance tight enough to exclude
    /// that physical margin would be pinning coincidence rather than testing the model.
    /// The published number and its source live in <see cref="PublishedNozzleReference"/>.
    /// Changing either is a visible edit. CI fails when the solver leaves the band.
    /// </summary>
    [Theory]
    [MemberData(nameof(PublishedSpecificImpulseIds))]
    public void Solve_ReproducesPublishedSpecificImpulseAndDoesNotUnderpredictIt(string id)
    {
        var published = PublishedNozzleReference.SpecificImpulseById(id);
        published.PerformanceSource.Should().Contain("http", "every reference value has to name its source");

        var solution = IdealRocketNozzle.Solve(
            PublishedNozzleReference.OperatingPoint(published, ReferenceThroatArea));

        solution.SpecificImpulse.Should().BeGreaterThanOrEqualTo(
            published.PublishedSpecificImpulseSeconds,
            $"{published.Label} would be an underprediction, which ideal theory must not produce");
        solution.SpecificImpulse.Should().BeApproximately(
            published.PublishedSpecificImpulseSeconds,
            published.PublishedSpecificImpulseSeconds * PublishedNozzleReference.RelativeBand,
            published.Label);
    }

    /// <summary>
    /// Vacuum and sea-level thrust are both on the RS-25 specification sheet. Their ratio does
    /// not depend on throat area, so it can be checked without inventing a diameter. The band is
    /// two-sided: sea-level and vacuum losses do not push the ratio in one direction the way they
    /// push specific impulse down.
    /// </summary>
    [Fact]
    public void Solve_Rs25VacuumToSeaLevelThrustRatio_StaysInsideThePublishedBand()
    {
        var ratio = PublishedNozzleReference.Rs25ThrustRatio;
        ratio.Source.Should().Contain("http");
        ratio.Vacuum.ExpansionRatio.Should().Be(ratio.SeaLevel.ExpansionRatio);
        ratio.Vacuum.ChamberPressurePascals.Should().Be(ratio.SeaLevel.ChamberPressurePascals);

        const double throatArea = 1.0;
        var vacuum = IdealRocketNozzle.Solve(PublishedNozzleReference.OperatingPoint(ratio.Vacuum, throatArea));
        var seaLevel = IdealRocketNozzle.Solve(PublishedNozzleReference.OperatingPoint(ratio.SeaLevel, throatArea));
        var idealRatio = vacuum.Thrust / seaLevel.Thrust;

        idealRatio.Should().BeApproximately(
            ratio.PublishedRatio,
            ratio.PublishedRatio * PublishedNozzleReference.RelativeBand);
    }

    /// <summary>
    /// Characteristic velocity measures combustion quality alone. Published c* for these
    /// propellant combinations sits in the 1700-2350 m/s range, hydrogen highest because its
    /// products are lightest.
    /// </summary>
    [Fact]
    public void CharacteristicVelocity_MatchesPublishedRangesAndOrdersPropellantsCorrectly()
    {
        var kerosene = IdealRocketNozzle.CharacteristicVelocity(1.24, 0.0223, 3500);
        var methane = IdealRocketNozzle.CharacteristicVelocity(1.20, 0.0206, 3600);
        var hydrogen = IdealRocketNozzle.CharacteristicVelocity(1.19, 0.0136, 3588);

        kerosene.Should().BeInRange(1650, 1850);
        methane.Should().BeInRange(1750, 1950);
        hydrogen.Should().BeInRange(2150, 2400);

        hydrogen.Should().BeGreaterThan(methane, "lighter exhaust products raise c*");
        methane.Should().BeGreaterThan(kerosene);
    }

    // ---------------------------------------------------------------------------------
    // The defect class this solver exists to avoid
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// The reason this code exists. The existing solvers accept a model and ignore it, so their
    /// output is identical for every engine. Changing any input here must change the answer.
    /// </summary>
    [Fact]
    public void Solve_OutputDependsOnEveryInput()
    {
        var baseline = IdealRocketNozzle.Solve(Merlin1DSeaLevel());

        var higherChamberPressure = IdealRocketNozzle.Solve(
            new EngineOperatingPoint
            {
                ChamberPressure = 15e6,
                ChamberTemperature = 3500,
                SpecificHeatRatio = 1.24,
                MolarMass = 0.0223,
                ExpansionRatio = 16,
                ThroatArea = ReferenceThroatArea,
                AmbientPressure = SeaLevelPressure
            });

        var biggerThroat = IdealRocketNozzle.Solve(
            new EngineOperatingPoint
            {
                ChamberPressure = 9.7e6,
                ChamberTemperature = 3500,
                SpecificHeatRatio = 1.24,
                MolarMass = 0.0223,
                ExpansionRatio = 16,
                ThroatArea = ReferenceThroatArea * 2,
                AmbientPressure = SeaLevelPressure
            });

        var wider = IdealRocketNozzle.Solve(
            new EngineOperatingPoint
            {
                ChamberPressure = 9.7e6,
                ChamberTemperature = 3500,
                SpecificHeatRatio = 1.24,
                MolarMass = 0.0223,
                ExpansionRatio = 40,
                ThroatArea = ReferenceThroatArea,
                AmbientPressure = SeaLevelPressure
            });

        higherChamberPressure.Thrust.Should().BeGreaterThan(baseline.Thrust);
        biggerThroat.MassFlowRate.Should().BeApproximately(baseline.MassFlowRate * 2, 1e-6);
        wider.ExitMachNumber.Should().BeGreaterThan(baseline.ExitMachNumber);
    }

    /// <summary>
    /// Thrust scales linearly with throat area at fixed chamber conditions, but specific impulse
    /// does not change at all: it is a property of the expansion, not of engine size. Getting
    /// this wrong is a classic error, and it is the invariant that lets the validation tests above
    /// use a representative throat area.
    /// </summary>
    [Fact]
    public void Solve_ThrustScalesWithThroatAreaWhileSpecificImpulseDoesNot()
    {
        var small = Merlin1DSeaLevel();
        var large = new EngineOperatingPoint
        {
            ChamberPressure = small.ChamberPressure,
            ChamberTemperature = small.ChamberTemperature,
            SpecificHeatRatio = small.SpecificHeatRatio,
            MolarMass = small.MolarMass,
            ExpansionRatio = small.ExpansionRatio,
            ThroatArea = small.ThroatArea * 7.5,
            AmbientPressure = small.AmbientPressure
        };

        var a = IdealRocketNozzle.Solve(small);
        var b = IdealRocketNozzle.Solve(large);

        b.Thrust.Should().BeApproximately(a.Thrust * 7.5, a.Thrust * 7.5 * 1e-9);
        b.SpecificImpulse.Should().BeApproximately(a.SpecificImpulse, 1e-9);
        b.ThrustCoefficient.Should().BeApproximately(a.ThrustCoefficient, 1e-9);
    }

    // ---------------------------------------------------------------------------------
    // Physical invariants
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// The same engine must produce more thrust in vacuum than at sea level, because the ambient
    /// term (Pe − Pa)·Ae grows as Pa falls. This is the behaviour that makes ambient pressure an
    /// input rather than a constant.
    /// </summary>
    [Fact]
    public void Solve_VacuumThrustExceedsSeaLevelThrustForTheSameEngine()
    {
        var seaLevel = Merlin1DSeaLevel();
        var vacuum = new EngineOperatingPoint
        {
            ChamberPressure = seaLevel.ChamberPressure,
            ChamberTemperature = seaLevel.ChamberTemperature,
            SpecificHeatRatio = seaLevel.SpecificHeatRatio,
            MolarMass = seaLevel.MolarMass,
            ExpansionRatio = seaLevel.ExpansionRatio,
            ThroatArea = seaLevel.ThroatArea,
            AmbientPressure = 0
        };

        var sl = IdealRocketNozzle.Solve(seaLevel);
        var vac = IdealRocketNozzle.Solve(vacuum);

        vac.Thrust.Should().BeGreaterThan(sl.Thrust);
        vac.SpecificImpulse.Should().BeGreaterThan(sl.SpecificImpulse);

        // Mass flow is set at the choked throat and cannot depend on back pressure.
        vac.MassFlowRate.Should().BeApproximately(sl.MassFlowRate, 1e-9);

        // The entire difference is the ambient term acting over the exit area.
        (vac.Thrust - sl.Thrust).Should().BeApproximately(SeaLevelPressure * sl.ExitArea, 1e-6);
    }

    /// <summary>
    /// A nozzle expanded well past ambient pressure is over-expanded: exit pressure falls below
    /// ambient and the pressure term becomes a genuine thrust loss. Reporting that as a negative
    /// contribution rather than clamping it at zero is the physically correct behaviour.
    /// </summary>
    [Fact]
    public void Solve_OverExpandedNozzleProducesNegativePressureThrust()
    {
        var overExpanded = new EngineOperatingPoint
        {
            ChamberPressure = 9.7e6,
            ChamberTemperature = 3500,
            SpecificHeatRatio = 1.24,
            MolarMass = 0.0223,
            ExpansionRatio = 150,
            ThroatArea = ReferenceThroatArea,
            AmbientPressure = SeaLevelPressure
        };

        var solution = IdealRocketNozzle.Solve(overExpanded);

        solution.ExitPressure.Should().BeLessThan(SeaLevelPressure);
        solution.PressureThrust.Should().BeNegative();
        solution.Thrust.Should().BeLessThan(solution.MomentumThrust);
        solution.Thrust.Should().BePositive("an over-expanded nozzle still produces net thrust");
    }

    /// <summary>
    /// In vacuum, more expansion is always better. At sea level it is not, which is why launch
    /// engines use modest area ratios and vacuum engines use large ones.
    /// </summary>
    [Fact]
    public void Solve_InVacuumLargerExpansionRatioAlwaysRaisesSpecificImpulse()
    {
        double IspAt(double expansionRatio) => IdealRocketNozzle.Solve(
            new EngineOperatingPoint
            {
                ChamberPressure = 9.7e6,
                ChamberTemperature = 3500,
                SpecificHeatRatio = 1.24,
                MolarMass = 0.0223,
                ExpansionRatio = expansionRatio,
                ThroatArea = ReferenceThroatArea,
                AmbientPressure = 0
            }).SpecificImpulse;

        var ratios = new[] { 5.0, 16.0, 40.0, 80.0, 150.0 };

        for (var i = 1; i < ratios.Length; i++)
        {
            IspAt(ratios[i]).Should().BeGreaterThan(
                IspAt(ratios[i - 1]),
                $"vacuum Isp must rise from ε={ratios[i - 1]} to ε={ratios[i]}");
        }
    }

    /// <summary>
    /// Inverting the area-Mach relation and applying it forward must return the original area
    /// ratio. This is the strongest available check on the numerical solve, since it does not
    /// depend on any reference figure.
    /// </summary>
    [Theory]
    [InlineData(1.19)]
    [InlineData(1.24)]
    [InlineData(1.40)]
    public void ExitMachNumber_RoundTripsThroughTheAreaRelation(double gamma)
    {
        foreach (var areaRatio in new[] { 1.5, 5.0, 16.0, 34.0, 69.0, 200.0 })
        {
            var mach = IdealRocketNozzle.ExitMachNumber(gamma, areaRatio);

            IdealRocketNozzle.AreaRatioForMach(gamma, mach)
                .Should().BeApproximately(areaRatio, areaRatio * 1e-9);
        }
    }

    /// <summary>
    /// The throat is by definition where the flow reaches Mach 1, so an area ratio of exactly 1
    /// must give Mach 1. This is the boundary of the supersonic branch being solved.
    /// </summary>
    [Fact]
    public void ExitMachNumber_IsUnityAtTheThroat()
    {
        IdealRocketNozzle.ExitMachNumber(1.24, 1.0).Should().BeApproximately(1.0, 1e-9);
    }

    /// <summary>
    /// Exit Mach depends only on area ratio and gamma, and must increase with area ratio. A
    /// non-monotonic result would mean the bisection had latched onto the subsonic branch.
    /// </summary>
    [Fact]
    public void ExitMachNumber_IncreasesMonotonicallyWithAreaRatio()
    {
        var previous = 1.0;

        foreach (var areaRatio in new[] { 1.1, 2.0, 10.0, 50.0, 100.0, 300.0 })
        {
            var mach = IdealRocketNozzle.ExitMachNumber(1.22, areaRatio);
            mach.Should().BeGreaterThan(previous);
            previous = mach;
        }
    }

    /// <summary>
    /// Exit static pressure and temperature must both fall below chamber stagnation values, since
    /// expansion converts thermal energy into kinetic energy.
    /// </summary>
    [Fact]
    public void Solve_ExpansionReducesPressureAndTemperatureFromChamberConditions()
    {
        var point = RaptorSeaLevel();

        var solution = IdealRocketNozzle.Solve(point);

        solution.ExitPressure.Should().BeLessThan(point.ChamberPressure);
        solution.ExitTemperature.Should().BeLessThan(point.ChamberTemperature);
        solution.ExitVelocity.Should().BePositive();
        solution.ExitMachNumber.Should().BeGreaterThan(1.0, "the nozzle is supersonic past the throat");
    }

    /// <summary>
    /// Thrust coefficient is dimensionless and physically bounded: it cannot fall below about 1
    /// for a working supersonic nozzle and cannot approach the theoretical vacuum limit of
    /// roughly 2.25 for these gases. A value outside that band means the thrust and the
    /// Pc·At normalisation have diverged.
    /// </summary>
    [Fact]
    public void Solve_ThrustCoefficientStaysWithinPhysicalBounds()
    {
        foreach (var point in new[] { Merlin1DSeaLevel(), RaptorSeaLevel(), RS25Vacuum() })
        {
            IdealRocketNozzle.Solve(point).ThrustCoefficient.Should().BeInRange(1.0, 2.3);
        }
    }

    /// <summary>
    /// Effective exhaust velocity is thrust per unit mass flow, and specific impulse is the same
    /// quantity divided by standard gravity. They must remain consistent by construction.
    /// </summary>
    [Fact]
    public void Solve_SpecificImpulseAndEffectiveExhaustVelocityAgree()
    {
        var solution = IdealRocketNozzle.Solve(RS25Vacuum());

        solution.SpecificImpulse.Should().BeApproximately(
            solution.EffectiveExhaustVelocity / IdealRocketNozzle.StandardGravity, 1e-9);

        (solution.MomentumThrust + solution.PressureThrust)
            .Should().BeApproximately(solution.Thrust, 1e-6);
    }

    /// <summary>
    /// Lighter combustion products raise specific impulse at fixed temperature, which is the
    /// physical reason hydrogen/oxygen outperforms hydrocarbon/oxygen. Holding everything else
    /// equal isolates that single effect.
    /// </summary>
    [Fact]
    public void Solve_LighterExhaustRaisesSpecificImpulseAtFixedTemperature()
    {
        double IspForMolarMass(double molarMass) => IdealRocketNozzle.Solve(
            new EngineOperatingPoint
            {
                ChamberPressure = 20e6,
                ChamberTemperature = 3500,
                SpecificHeatRatio = 1.22,
                MolarMass = molarMass,
                ExpansionRatio = 40,
                ThroatArea = ReferenceThroatArea,
                AmbientPressure = 0
            }).SpecificImpulse;

        IspForMolarMass(0.0136).Should().BeGreaterThan(IspForMolarMass(0.0223));
    }

    // ---------------------------------------------------------------------------------
    // Input validation
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Invalid inputs must throw rather than produce a NaN. A NaN would propagate silently into a
    /// result object and be reported as an engine performance figure, which is worse than a crash.
    /// </summary>
    [Theory]
    [InlineData(0, 3500, 1.24, 0.0223, 0.05, 16, 101325)]
    [InlineData(9.7e6, 0, 1.24, 0.0223, 0.05, 16, 101325)]
    [InlineData(9.7e6, 3500, 1.0, 0.0223, 0.05, 16, 101325)]
    [InlineData(9.7e6, 3500, 1.24, 0, 0.05, 16, 101325)]
    [InlineData(9.7e6, 3500, 1.24, 0.0223, 0, 16, 101325)]
    [InlineData(9.7e6, 3500, 1.24, 0.0223, 0.05, 0.5, 101325)]
    [InlineData(9.7e6, 3500, 1.24, 0.0223, 0.05, 16, -1)]
    public void Solve_RejectsPhysicallyImpossibleInputs(
        double chamberPressure,
        double chamberTemperature,
        double gamma,
        double molarMass,
        double throatArea,
        double expansionRatio,
        double ambientPressure)
    {
        var point = new EngineOperatingPoint
        {
            ChamberPressure = chamberPressure,
            ChamberTemperature = chamberTemperature,
            SpecificHeatRatio = gamma,
            MolarMass = molarMass,
            ThroatArea = throatArea,
            ExpansionRatio = expansionRatio,
            AmbientPressure = ambientPressure
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.Solve(point));
    }

    /// <summary>
    /// NaN and positive infinity fail every upper-unbounded comparison, so they used to pass
    /// validation and come back as a performance figure. Vacuum ambient pressure and an
    /// expansion ratio of 1 remain valid.
    /// </summary>
    [Theory]
    [InlineData(double.NaN, 3500, 1.24, 0.0223, 0.05, 16, 101325)]
    [InlineData(double.PositiveInfinity, 3500, 1.24, 0.0223, 0.05, 16, 101325)]
    [InlineData(9.7e6, double.NaN, 1.24, 0.0223, 0.05, 16, 101325)]
    [InlineData(9.7e6, double.PositiveInfinity, 1.24, 0.0223, 0.05, 16, 101325)]
    [InlineData(9.7e6, 3500, double.NaN, 0.0223, 0.05, 16, 101325)]
    [InlineData(9.7e6, 3500, double.PositiveInfinity, 0.0223, 0.05, 16, 101325)]
    [InlineData(9.7e6, 3500, 1.24, double.NaN, 0.05, 16, 101325)]
    [InlineData(9.7e6, 3500, 1.24, double.PositiveInfinity, 0.05, 16, 101325)]
    [InlineData(9.7e6, 3500, 1.24, 0.0223, double.NaN, 16, 101325)]
    [InlineData(9.7e6, 3500, 1.24, 0.0223, double.PositiveInfinity, 16, 101325)]
    [InlineData(9.7e6, 3500, 1.24, 0.0223, 0.05, double.NaN, 101325)]
    [InlineData(9.7e6, 3500, 1.24, 0.0223, 0.05, double.PositiveInfinity, 101325)]
    [InlineData(9.7e6, 3500, 1.24, 0.0223, 0.05, 16, double.NaN)]
    [InlineData(9.7e6, 3500, 1.24, 0.0223, 0.05, 16, double.PositiveInfinity)]
    public void Solve_RejectsNonFiniteInputs(
        double chamberPressure,
        double chamberTemperature,
        double gamma,
        double molarMass,
        double throatArea,
        double expansionRatio,
        double ambientPressure)
    {
        var point = new EngineOperatingPoint
        {
            ChamberPressure = chamberPressure,
            ChamberTemperature = chamberTemperature,
            SpecificHeatRatio = gamma,
            MolarMass = molarMass,
            ThroatArea = throatArea,
            ExpansionRatio = expansionRatio,
            AmbientPressure = ambientPressure
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.Solve(point));
    }

    [Fact]
    public void ExitMachNumber_RejectsAreaRatioBelowTheThroat()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.ExitMachNumber(1.24, 0.9));
    }

    [Fact]
    public void Solve_RejectsNullOperatingPoint()
    {
        Assert.Throws<ArgumentNullException>(() => IdealRocketNozzle.Solve(null!));
    }

    /// <summary>
    /// The relations are public and callable individually, not only through <c>Solve</c>, so each
    /// one has to defend its own domain. <c>Solve</c> validates the operating point up front and
    /// therefore never reaches these guards, which is exactly why they need their own tests: an
    /// unexercised guard is an assumption, and a caller reaching one of these functions directly
    /// with a bad argument should get an exception rather than a NaN.
    /// </summary>
    [Fact]
    public void PublicRelations_GuardTheirOwnDomains()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.SpecificGasConstant(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.SpecificGasConstant(-0.02));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.CharacteristicVelocity(1.24, 0.0223, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.CharacteristicVelocity(1.24, 0.0223, -100));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.MassFlowRate(0, 0.05, 1.24, 0.0223, 3500));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.MassFlowRate(9.7e6, 0, 1.24, 0.0223, 3500));

        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.AreaRatioForMach(1.24, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.AreaRatioForMach(1.24, -2));
    }

    /// <summary>
    /// The same domain guards have to reject a missing or infinite argument. NaN used to fall
    /// through and either come back as NaN or, for exit Mach, converge to a plausible Mach 1.
    /// </summary>
    [Fact]
    public void PublicRelations_RejectNonFiniteInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.SpecificGasConstant(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.SpecificGasConstant(double.PositiveInfinity));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.VandenkerckhoveFunction(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.VandenkerckhoveFunction(double.PositiveInfinity));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.CharacteristicVelocity(double.NaN, 0.0223, 3500));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.CharacteristicVelocity(1.24, double.NaN, 3500));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.CharacteristicVelocity(1.24, 0.0223, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.CharacteristicVelocity(1.24, 0.0223, double.PositiveInfinity));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.MassFlowRate(double.NaN, 0.05, 1.24, 0.0223, 3500));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.MassFlowRate(double.PositiveInfinity, 0.05, 1.24, 0.0223, 3500));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.MassFlowRate(9.7e6, double.NaN, 1.24, 0.0223, 3500));

        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.ExitMachNumber(double.NaN, 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.ExitMachNumber(1.24, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.ExitMachNumber(1.24, double.PositiveInfinity));

        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.AreaRatioForMach(1.24, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.AreaRatioForMach(1.24, double.PositiveInfinity));

        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.ExitPressure(double.NaN, 1.24, 3));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.ExitPressure(double.PositiveInfinity, 1.24, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.ExitPressure(9.7e6, 1.24, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.ExitPressure(9.7e6, 1.24, -1));

        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.ExitTemperature(double.NaN, 1.24, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.ExitTemperature(0, 1.24, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.ExitTemperature(-100, 1.24, 3));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.ExitTemperature(3500, 1.24, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.ExitTemperature(3500, 1.24, -1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.ExitVelocity(1.24, double.NaN, 1500, 3));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.ExitVelocity(1.24, 0.0223, double.NaN, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.ExitVelocity(1.24, 0.0223, -10, 3));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IdealRocketNozzle.ExitVelocity(1.24, 0.0223, 1500, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.ExitVelocity(1.24, 0.0223, 1500, -1));
    }

    /// <summary>
    /// Mach 0 is stagnation, not an invalid nozzle state. Exit static conditions match the chamber.
    /// </summary>
    [Fact]
    public void ExitState_AtMachZero_IsTheChamberState()
    {
        IdealRocketNozzle.ExitPressure(9.7e6, 1.24, 0).Should().Be(9.7e6);
        IdealRocketNozzle.ExitTemperature(3500, 1.24, 0).Should().Be(3500);
        IdealRocketNozzle.ExitVelocity(1.24, 0.0223, 1500, 0).Should().Be(0);
    }

    /// <summary>
    /// γ = 1 makes the isentropic exponents singular, so every relation that uses it must reject
    /// that value rather than return infinity. One test per entry point, because each validates
    /// independently.
    /// </summary>
    [Fact]
    public void Relations_RejectSpecificHeatRatioAtOrBelowUnity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.VandenkerckhoveFunction(1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.VandenkerckhoveFunction(0.9));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.ExitMachNumber(1.0, 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.AreaRatioForMach(1.0, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.ExitPressure(9.7e6, 1.0, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.ExitTemperature(3500, 1.0, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdealRocketNozzle.ExitVelocity(1.0, 0.0223, 1500, 3));
    }

    /// <summary>
    /// Sanity check on the gas constant itself: combustion products of hydrogen/oxygen have a
    /// specific gas constant near 611 J/(kg·K), and of kerosene/oxygen near 373.
    /// </summary>
    [Fact]
    public void SpecificGasConstant_MatchesKnownPropellantValues()
    {
        IdealRocketNozzle.SpecificGasConstant(0.0136).Should().BeApproximately(611.4, 1.0);
        IdealRocketNozzle.SpecificGasConstant(0.0223).Should().BeApproximately(372.8, 1.0);
    }
}
