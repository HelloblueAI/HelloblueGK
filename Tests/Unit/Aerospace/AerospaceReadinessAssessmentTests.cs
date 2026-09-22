using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using HB_NLP_Research_Lab.Aerospace;
using Xunit;

namespace HelloblueGK.Tests.Unit.Aerospace;

/// <summary>
/// Verification of the readiness assessment's decision logic: the mission-level requirement
/// tables, the go/no-go thresholds, and the score aggregation that feeds them.
///
/// These are driven directly rather than through PerformComprehensiveAssessmentAsync, which
/// hardcodes every underlying assessment to "satisfied" and therefore cannot demonstrate that
/// the logic responds to evidence at all. Tested here, the same code is shown to distinguish
/// a research prototype from a mission-critical vehicle.
/// </summary>
public class AerospaceReadinessAssessmentTests
{
    private static readonly MissionLevel[] IncreasinglyStrict =
    {
        MissionLevel.Research,
        MissionLevel.Prototype,
        MissionLevel.Qualification,
        MissionLevel.Operational,
        MissionLevel.Critical
    };

    private static AerospaceReadinessAssessment Subject() => new();

    private static AerospaceReadinessReport ReportWith(params (ReadinessCategory Category, double Score)[] categories)
        => new()
        {
            ReadinessCategories = categories
                .Select(c => new ReadinessCategoryReport { Category = c.Category, ReadinessScore = c.Score })
                .ToList()
        };

    // ---------------------------------------------------------------------------------
    // Mission-level requirement tables
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// A more demanding mission may never require less of the vehicle. Asserting the ordering
    /// rather than each literal is what actually protects the tables: a transposed pair of
    /// entries — operational demanding more flight heritage than critical, say — still looks
    /// plausible line by line but inverts the safety argument.
    /// </summary>
    [Fact]
    public void RequirementTables_NeverRelaxAsTheMissionBecomesMoreDemanding()
    {
        var assessment = Subject();

        var trl = IncreasinglyStrict.Select(assessment.GetTRLForMissionLevel).ToList();
        var heritage = IncreasinglyStrict.Select(assessment.GetFlightHeritageForMissionLevel).ToList();
        var safetyFactor = IncreasinglyStrict.Select(assessment.GetSafetyFactorForMissionLevel).ToList();
        var redundancy = IncreasinglyStrict.Select(assessment.GetRedundancyLevelForMissionLevel).ToList();
        var mtbf = IncreasinglyStrict.Select(assessment.GetMTBFForMissionLevel).ToList();

        trl.Should().BeInAscendingOrder();
        heritage.Should().BeInAscendingOrder();
        safetyFactor.Should().BeInAscendingOrder();
        redundancy.Should().BeInAscendingOrder();
        mtbf.Should().BeInAscendingOrder();

        // Repair time is the exception: a more critical mission must be restored faster.
        IncreasinglyStrict.Select(assessment.GetMTTRForMissionLevel)
            .Should().BeInDescendingOrder();
    }

    /// <summary>
    /// The critical mission level must demand strictly more than research across every
    /// dimension. Monotonicity alone would be satisfied by a flat table that asks the same of
    /// a lab experiment and a crewed vehicle.
    /// </summary>
    [Fact]
    public void CriticalMissions_DemandStrictlyMoreThanResearch()
    {
        var assessment = Subject();

        assessment.GetTRLForMissionLevel(MissionLevel.Critical)
            .Should().BeGreaterThan(assessment.GetTRLForMissionLevel(MissionLevel.Research));
        assessment.GetFlightHeritageForMissionLevel(MissionLevel.Critical)
            .Should().BeGreaterThan(assessment.GetFlightHeritageForMissionLevel(MissionLevel.Research));
        assessment.GetSafetyFactorForMissionLevel(MissionLevel.Critical)
            .Should().BeGreaterThan(assessment.GetSafetyFactorForMissionLevel(MissionLevel.Research));
        assessment.GetRedundancyLevelForMissionLevel(MissionLevel.Critical)
            .Should().BeGreaterThan(assessment.GetRedundancyLevelForMissionLevel(MissionLevel.Research));
        assessment.GetMTBFForMissionLevel(MissionLevel.Critical)
            .Should().BeGreaterThan(assessment.GetMTBFForMissionLevel(MissionLevel.Research));
        assessment.GetMTTRForMissionLevel(MissionLevel.Critical)
            .Should().BeLessThan(assessment.GetMTTRForMissionLevel(MissionLevel.Research));
    }

    /// <summary>
    /// Operational and critical flight both require a fully matured technology, which is the
    /// one place the table legitimately plateaus. Pinned so a future edit that relaxes TRL 9
    /// for operational flight has to be deliberate.
    /// </summary>
    [Fact]
    public void OperationalAndCriticalMissions_BothRequireFullTechnologyMaturity()
    {
        var assessment = Subject();

        assessment.GetTRLForMissionLevel(MissionLevel.Operational).Should().Be(9);
        assessment.GetTRLForMissionLevel(MissionLevel.Critical).Should().Be(9);
    }

    /// <summary>
    /// An unrecognised mission level must fall back to prototype-grade requirements rather
    /// than to zero. Defaulting to no requirement would let an out-of-range value through as
    /// trivially compliant.
    /// </summary>
    [Fact]
    public void UnrecognisedMissionLevel_FallsBackToPrototypeGradeRequirements()
    {
        var assessment = Subject();
        var unrecognised = (MissionLevel)99;

        assessment.GetTRLForMissionLevel(unrecognised).Should().Be(6);
        assessment.GetSafetyFactorForMissionLevel(unrecognised).Should().Be(2.0);
        assessment.GetRedundancyLevelForMissionLevel(unrecognised).Should().Be(2);
        assessment.GetMTBFForMissionLevel(unrecognised).Should().BeGreaterThan(0);
        assessment.GetMTTRForMissionLevel(unrecognised).Should().BeGreaterThan(0);
    }

    // ---------------------------------------------------------------------------------
    // Go / no-go thresholds
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// The readiness thresholds are the actual go/no-go decision, so each is asserted at its
    /// boundary and one step below. A score exactly at the threshold is ready; anything under
    /// it is not.
    /// </summary>
    [Theory]
    [InlineData(MissionLevel.Research, 0.70)]
    [InlineData(MissionLevel.Prototype, 0.80)]
    [InlineData(MissionLevel.Qualification, 0.85)]
    [InlineData(MissionLevel.Operational, 0.90)]
    [InlineData(MissionLevel.Critical, 0.95)]
    public void ReadinessStatus_IsInclusiveAtTheThresholdAndRefusesBelowIt(
        MissionLevel missionLevel,
        double threshold)
    {
        var assessment = Subject();

        assessment.DetermineReadinessStatus(threshold, missionLevel).Should().Be("READY");
        assessment.DetermineReadinessStatus(threshold - 0.001, missionLevel).Should().Be("NOT READY");
        assessment.DetermineReadinessStatus(1.0, missionLevel).Should().Be("READY");
    }

    /// <summary>
    /// The same score must not clear a stricter mission than it qualifies for. This is the
    /// property that keeps the mission levels meaningful: 0.92 is ready for qualification and
    /// operational flight, but not for mission-critical operations.
    /// </summary>
    [Fact]
    public void AScoreReadyForOneMissionLevel_IsNotAutomaticallyReadyForAStricterOne()
    {
        var assessment = Subject();

        assessment.DetermineReadinessStatus(0.92, MissionLevel.Qualification).Should().Be("READY");
        assessment.DetermineReadinessStatus(0.92, MissionLevel.Operational).Should().Be("READY");
        assessment.DetermineReadinessStatus(0.92, MissionLevel.Critical).Should().Be("NOT READY");
    }

    /// <summary>
    /// A zero score must never be reported as ready, at any mission level.
    /// </summary>
    [Fact]
    public void ZeroReadiness_IsNeverReportedAsReady()
    {
        var assessment = Subject();

        foreach (var level in IncreasinglyStrict)
        {
            assessment.DetermineReadinessStatus(0.0, level).Should().Be("NOT READY");
        }
    }

    // ---------------------------------------------------------------------------------
    // Score aggregation
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// With nothing assessed there is no evidence of readiness, so the score must be zero
    /// rather than a vacuous average.
    /// </summary>
    [Fact]
    public void OverallReadiness_WithNoCategoriesAssessed_IsZero()
    {
        Subject().CalculateOverallReadinessScore(ReportWith()).Should().Be(0.0);
    }

    /// <summary>
    /// Safety, regulatory, and technical readiness carry extra weight, so a weak score in a
    /// critical category must pull the total below the plain average. If it did not, a vehicle
    /// could offset a safety shortfall with financial and operational strength.
    /// </summary>
    [Fact]
    public void OverallReadiness_WeightsCriticalCategoriesAboveTheirPlainAverage()
    {
        var weakSafety = ReportWith(
            (ReadinessCategory.Safety, 0.50),
            (ReadinessCategory.Regulatory, 0.50),
            (ReadinessCategory.Technical, 0.50),
            (ReadinessCategory.Financial, 1.00),
            (ReadinessCategory.Operational, 1.00));

        var plainAverage = 0.7; // (0.5 * 3 + 1.0 * 2) / 5

        Subject().CalculateOverallReadinessScore(weakSafety)
            .Should().BeLessThan(plainAverage);
    }

    /// <summary>
    /// The mirror case: strength in the critical categories must be rewarded above the plain
    /// average, so the weighting is genuinely a weighting rather than a one-sided penalty.
    /// </summary>
    [Fact]
    public void OverallReadiness_RewardsStrengthInCriticalCategories()
    {
        var strongSafety = ReportWith(
            (ReadinessCategory.Safety, 1.00),
            (ReadinessCategory.Regulatory, 1.00),
            (ReadinessCategory.Technical, 1.00),
            (ReadinessCategory.Financial, 0.50),
            (ReadinessCategory.Operational, 0.50));

        Subject().CalculateOverallReadinessScore(strongSafety)
            .Should().BeGreaterThan(0.8);
    }

    /// <summary>
    /// Regression test for a crash rather than a wrong answer: the aggregation guarded against
    /// an empty category list but then averaged only the critical categories, which throws on
    /// an empty sequence. A partial report covering just operational and financial readiness —
    /// a perfectly reasonable thing to ask for — brought the assessment down instead of
    /// returning a score. With no critical category to weight, the plain average is the whole
    /// of what was measured.
    /// </summary>
    [Fact]
    public void OverallReadiness_WithNoCriticalCategories_FallsBackToThePlainAverage()
    {
        var report = ReportWith(
            (ReadinessCategory.Operational, 0.80),
            (ReadinessCategory.Financial, 0.60));

        Subject().CalculateOverallReadinessScore(report)
            .Should().BeApproximately(0.70, 1e-9);
    }

    /// <summary>
    /// A report made up entirely of critical categories must reduce to their average, since
    /// the weighted and unweighted terms coincide.
    /// </summary>
    [Fact]
    public void OverallReadiness_WithOnlyCriticalCategories_EqualsTheirAverage()
    {
        var report = ReportWith(
            (ReadinessCategory.Safety, 0.90),
            (ReadinessCategory.Regulatory, 0.80),
            (ReadinessCategory.Technical, 0.70));

        Subject().CalculateOverallReadinessScore(report)
            .Should().BeApproximately(0.80, 1e-9);
    }

    /// <summary>
    /// Perfect readiness everywhere must produce exactly 1.0, not 0.999 or 1.0000001. The
    /// go/no-go comparison is an inequality against this value, so drift here moves the gate.
    /// </summary>
    [Fact]
    public void OverallReadiness_WithEverythingPerfect_IsExactlyOne()
    {
        var report = ReportWith(
            (ReadinessCategory.Safety, 1.0),
            (ReadinessCategory.Regulatory, 1.0),
            (ReadinessCategory.Technical, 1.0),
            (ReadinessCategory.Operational, 1.0));

        Subject().CalculateOverallReadinessScore(report).Should().BeApproximately(1.0, 1e-12);
    }

    // ---------------------------------------------------------------------------------
    // Compliance contribution
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Failing compliance contributes nothing, regardless of how many certificates happen to
    /// be on file. Partial credit here would let a non-compliant system accumulate readiness
    /// from paperwork.
    /// </summary>
    [Fact]
    public void ComplianceReadiness_WhenNotCompliant_ContributesNothing()
    {
        var report = new ComplianceReport
        {
            OverallCompliance = false,
            Certifications = Enumerable.Range(0, 20)
                .Select(_ => new CertificationDocument())
                .ToList()
        };

        Subject().CalculateComplianceReadinessScore(report, MissionLevel.Critical).Should().Be(0.0);
    }

    /// <summary>
    /// Compliance readiness is the fraction of the required certifications actually held, so a
    /// mission needing more of them must score lower on the same evidence.
    /// </summary>
    [Fact]
    public void ComplianceReadiness_ScalesWithWhatTheMissionRequires()
    {
        var sixCertificates = new ComplianceReport
        {
            OverallCompliance = true,
            Certifications = Enumerable.Range(0, 6).Select(_ => new CertificationDocument()).ToList()
        };
        var assessment = Subject();

        // Research needs 3 and is oversupplied; critical needs 9 and is one third short.
        assessment.CalculateComplianceReadinessScore(sixCertificates, MissionLevel.Research).Should().Be(1.0);
        assessment.CalculateComplianceReadinessScore(sixCertificates, MissionLevel.Critical)
            .Should().BeApproximately(6.0 / 9.0, 1e-9);
    }

    /// <summary>
    /// A critical mission requires nine certifications, which is exactly the number
    /// <c>AerospaceComplianceSystem</c> can issue. Requiring twelve made regulatory readiness
    /// cap at 0.75 and kept both public readiness predicates false no matter what was proved.
    /// </summary>
    [Fact]
    public void CriticalMissions_CanBeFullyReadyWhenEveryEvaluatedStandardIsCertified()
    {
        const int standardsTheComplianceSystemEvaluates = 9;
        var everyStandardCertified = new ComplianceReport
        {
            OverallCompliance = true,
            Certifications = Enumerable.Range(0, standardsTheComplianceSystemEvaluates)
                .Select(_ => new CertificationDocument()).ToList()
        };
        var assessment = Subject();

        assessment.CalculateComplianceReadinessScore(everyStandardCertified, MissionLevel.Operational)
            .Should().Be(1.0);
        assessment.CalculateComplianceReadinessScore(everyStandardCertified, MissionLevel.Critical)
            .Should().Be(1.0);
    }

    /// <summary>
    /// Surplus certification cannot push readiness above fully ready, or a well-papered
    /// programme could offset a genuine shortfall elsewhere in the weighted total.
    /// </summary>
    [Fact]
    public void ComplianceReadiness_IsCappedAtFullyReady()
    {
        var report = new ComplianceReport
        {
            OverallCompliance = true,
            Certifications = Enumerable.Range(0, 100).Select(_ => new CertificationDocument()).ToList()
        };

        Subject().CalculateComplianceReadinessScore(report, MissionLevel.Critical).Should().Be(1.0);
    }

    /// <summary>
    /// Compliant but with nothing certified yet is zero readiness, not full marks.
    /// </summary>
    [Fact]
    public void ComplianceReadiness_WithNoCertificationsHeld_IsZero()
    {
        var report = new ComplianceReport { OverallCompliance = true };

        Subject().CalculateComplianceReadinessScore(report, MissionLevel.Operational).Should().Be(0.0);
    }

    // ---------------------------------------------------------------------------------
    // Category scores respond to evidence
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Technical readiness must fall when technology maturity falls. This is the check that
    /// the score reads its input at all — the surrounding orchestration always passes TRL 9,
    /// so nothing else in the codebase would notice if the term were ignored.
    /// </summary>
    [Fact]
    public void TechnicalReadiness_FallsWithTechnologyMaturity()
    {
        var assessment = Subject();

        var mature = new TechnicalReadinessAssessment
        {
            TechnologyReadinessLevel = 9,
            PerformanceValidation = true,
            ReliabilityAnalysis = true
        };
        var immature = new TechnicalReadinessAssessment
        {
            TechnologyReadinessLevel = 3,
            PerformanceValidation = true,
            ReliabilityAnalysis = true
        };

        assessment.CalculateTechnicalReadinessScore(immature, MissionLevel.Critical)
            .Should().BeLessThan(assessment.CalculateTechnicalReadinessScore(mature, MissionLevel.Critical));
    }

    /// <summary>
    /// Unvalidated performance and unanalysed reliability must each cost readiness.
    /// </summary>
    [Fact]
    public void TechnicalReadiness_PenalisesMissingValidationAndReliabilityWork()
    {
        var assessment = Subject();
        var complete = new TechnicalReadinessAssessment
        {
            TechnologyReadinessLevel = 9,
            PerformanceValidation = true,
            ReliabilityAnalysis = true
        };

        var noPerformanceValidation = new TechnicalReadinessAssessment
        {
            TechnologyReadinessLevel = 9,
            PerformanceValidation = false,
            ReliabilityAnalysis = true
        };
        var noReliabilityAnalysis = new TechnicalReadinessAssessment
        {
            TechnologyReadinessLevel = 9,
            PerformanceValidation = true,
            ReliabilityAnalysis = false
        };

        var baseline = assessment.CalculateTechnicalReadinessScore(complete, MissionLevel.Operational);

        assessment.CalculateTechnicalReadinessScore(noPerformanceValidation, MissionLevel.Operational)
            .Should().BeLessThan(baseline);
        assessment.CalculateTechnicalReadinessScore(noReliabilityAnalysis, MissionLevel.Operational)
            .Should().BeLessThan(baseline);
    }

    /// <summary>
    /// Safety readiness must respond to safety factor, redundancy, and fault tolerance
    /// together, and a fully redundant high-margin design must outscore a marginal one.
    /// </summary>
    [Fact]
    public void SafetyReadiness_RisesWithMarginRedundancyAndFaultTolerance()
    {
        var assessment = Subject();

        var marginal = new SafetyReadinessAssessment
        {
            SafetyFactor = 1.5,
            RedundancyLevel = 1,
            FaultTolerance = 0.90
        };
        var robust = new SafetyReadinessAssessment
        {
            SafetyFactor = 4.0,
            RedundancyLevel = 4,
            FaultTolerance = 0.9999
        };

        assessment.CalculateSafetyReadinessScore(marginal, MissionLevel.Critical)
            .Should().BeLessThan(assessment.CalculateSafetyReadinessScore(robust, MissionLevel.Critical));
    }

    /// <summary>
    /// Safety margin beyond the design point must not inflate the score without limit; the
    /// safety-factor term is capped. A 100x margin is not ten times as ready as a 10x one, and
    /// letting it read that way would allow one dimension to mask the others.
    /// </summary>
    [Fact]
    public void SafetyReadiness_CapsCreditForExcessSafetyFactor()
    {
        var assessment = Subject();

        var atCap = new SafetyReadinessAssessment { SafetyFactor = 4.0, RedundancyLevel = 4, FaultTolerance = 1.0 };
        var farBeyondCap = new SafetyReadinessAssessment { SafetyFactor = 400.0, RedundancyLevel = 4, FaultTolerance = 1.0 };

        assessment.CalculateSafetyReadinessScore(farBeyondCap, MissionLevel.Critical)
            .Should().Be(assessment.CalculateSafetyReadinessScore(atCap, MissionLevel.Critical));
    }

    /// <summary>
    /// Operational readiness must track availability, maintainability, and supportability
    /// rather than reporting a fixed base score.
    /// </summary>
    [Fact]
    public void OperationalReadiness_TracksAvailabilityAndSupportability()
    {
        var assessment = Subject();

        var poor = new OperationalReadinessAssessment { Availability = 0.5, Maintainability = 0.5, Supportability = 0.5 };
        var strong = new OperationalReadinessAssessment { Availability = 1.0, Maintainability = 1.0, Supportability = 1.0 };

        assessment.CalculateOperationalReadinessScore(poor, MissionLevel.Operational)
            .Should().BeLessThan(assessment.CalculateOperationalReadinessScore(strong, MissionLevel.Operational));
    }

    /// <summary>
    /// Every category score must stay inside [0, 1] so the weighted total remains comparable
    /// against the thresholds. A term escaping the range would silently move the go/no-go gate.
    /// </summary>
    [Fact]
    public void CategoryScores_StayWithinTheUnitInterval()
    {
        var assessment = Subject();

        foreach (var level in IncreasinglyStrict)
        {
            assessment.CalculateTechnicalReadinessScore(
                new TechnicalReadinessAssessment { TechnologyReadinessLevel = 9, PerformanceValidation = true, ReliabilityAnalysis = true },
                level).Should().BeInRange(0.0, 1.0);

            assessment.CalculateSafetyReadinessScore(
                new SafetyReadinessAssessment { SafetyFactor = 4.0, RedundancyLevel = 4, FaultTolerance = 1.0 },
                level).Should().BeInRange(0.0, 1.0);

            assessment.CalculateOperationalReadinessScore(
                new OperationalReadinessAssessment { Availability = 1.0, Maintainability = 1.0, Supportability = 1.0 },
                level).Should().BeInRange(0.0, 1.0);
        }
    }

    /// <summary>
    /// Credit above fully ready is not a stronger result. One term left unclamped — fault
    /// tolerance was — lets a single absurd measurement outvote safety factor and redundancy
    /// and push the weighted total through the mission-critical gate.
    /// </summary>
    [Fact]
    public void SafetyReadiness_DoesNotLetUnboundedFaultToleranceMaskMissingMargin()
    {
        var assessment = Subject();
        var atCap = new SafetyReadinessAssessment { SafetyFactor = 4.0, RedundancyLevel = 4, FaultTolerance = 1.0 };
        var onlyInflatedTolerance = new SafetyReadinessAssessment { FaultTolerance = 1_000_000 };
        var excessOnAnOtherwiseCompleteDesign = new SafetyReadinessAssessment
        {
            SafetyFactor = 400.0,
            RedundancyLevel = 400,
            FaultTolerance = 1_000_000
        };
        var nonFinite = new SafetyReadinessAssessment
        {
            SafetyFactor = double.NaN,
            RedundancyLevel = -4,
            FaultTolerance = double.PositiveInfinity
        };

        assessment.CalculateSafetyReadinessScore(onlyInflatedTolerance, MissionLevel.Critical)
            .Should().BeApproximately(1.0 / 3.0, 1e-9);
        assessment.CalculateSafetyReadinessScore(onlyInflatedTolerance, MissionLevel.Critical)
            .Should().BeLessThan(assessment.CalculateSafetyReadinessScore(atCap, MissionLevel.Critical));
        assessment.CalculateSafetyReadinessScore(excessOnAnOtherwiseCompleteDesign, MissionLevel.Critical)
            .Should().Be(assessment.CalculateSafetyReadinessScore(atCap, MissionLevel.Critical));
        assessment.CalculateSafetyReadinessScore(nonFinite, MissionLevel.Critical).Should().Be(0.0);
    }

    /// <summary>
    /// TRL is a fraction of 9. An impossible maturity level must not score as more than fully
    /// mature, and a negative level must not drag the other terms below zero.
    /// </summary>
    [Fact]
    public void TechnicalReadiness_DoesNotTreatAnImpossibleTrlAsExtraCredit()
    {
        var assessment = Subject();
        var mature = new TechnicalReadinessAssessment
        {
            TechnologyReadinessLevel = 9,
            PerformanceValidation = true,
            ReliabilityAnalysis = true
        };
        var impossible = new TechnicalReadinessAssessment
        {
            TechnologyReadinessLevel = 90,
            PerformanceValidation = true,
            ReliabilityAnalysis = true
        };
        var onlyImpossible = new TechnicalReadinessAssessment { TechnologyReadinessLevel = 90 };
        var negative = new TechnicalReadinessAssessment
        {
            TechnologyReadinessLevel = -9,
            PerformanceValidation = true,
            ReliabilityAnalysis = true
        };

        assessment.CalculateTechnicalReadinessScore(impossible, MissionLevel.Critical)
            .Should().Be(assessment.CalculateTechnicalReadinessScore(mature, MissionLevel.Critical));
        assessment.CalculateTechnicalReadinessScore(onlyImpossible, MissionLevel.Critical)
            .Should().BeApproximately(1.0 / 3.0, 1e-9);
        assessment.CalculateTechnicalReadinessScore(negative, MissionLevel.Critical)
            .Should().BeApproximately(2.0 / 3.0, 1e-9);
    }

    /// <summary>
    /// Availability, maintainability, and supportability are fractions. A value of 50 is not
    /// fifty times as ready as a value of 1, and a non-finite term contributes nothing.
    /// </summary>
    [Fact]
    public void OperationalReadiness_ClampsEachTermToTheUnitInterval()
    {
        var assessment = Subject();
        var saturated = new OperationalReadinessAssessment { Availability = 1, Maintainability = 1, Supportability = 1 };
        var inflated = new OperationalReadinessAssessment { Availability = 50, Maintainability = 50, Supportability = 50 };
        var oneInflated = new OperationalReadinessAssessment { Availability = 50 };
        var nonFinite = new OperationalReadinessAssessment
        {
            Availability = -1,
            Maintainability = double.NaN,
            Supportability = double.NegativeInfinity
        };

        assessment.CalculateOperationalReadinessScore(inflated, MissionLevel.Operational)
            .Should().Be(assessment.CalculateOperationalReadinessScore(saturated, MissionLevel.Operational));
        assessment.CalculateOperationalReadinessScore(oneInflated, MissionLevel.Operational)
            .Should().BeApproximately(1.0 / 3.0, 1e-9);
        assessment.CalculateOperationalReadinessScore(nonFinite, MissionLevel.Operational).Should().Be(0.0);
    }

    /// <summary>
    /// The aggregator is the go/no-go input. A category handed in above 1 must be treated as
    /// fully ready for that category, not as extra weight that clears a stricter mission.
    /// </summary>
    [Fact]
    public void OverallReadiness_IgnoresCreditAboveFullyReady()
    {
        var assessment = Subject();
        var inflated = ReportWith(
            (ReadinessCategory.Safety, 1_000),
            (ReadinessCategory.Regulatory, 0),
            (ReadinessCategory.Technical, 0),
            (ReadinessCategory.Operational, 0),
            (ReadinessCategory.Financial, 0),
            (ReadinessCategory.Quality, 0),
            (ReadinessCategory.Security, 0),
            (ReadinessCategory.Environmental, 0));
        var capped = ReportWith(
            (ReadinessCategory.Safety, 1),
            (ReadinessCategory.Regulatory, 0),
            (ReadinessCategory.Technical, 0),
            (ReadinessCategory.Operational, 0),
            (ReadinessCategory.Financial, 0),
            (ReadinessCategory.Quality, 0),
            (ReadinessCategory.Security, 0),
            (ReadinessCategory.Environmental, 0));

        var score = assessment.CalculateOverallReadinessScore(inflated);

        score.Should().BeApproximately(assessment.CalculateOverallReadinessScore(capped), 1e-9);
        score.Should().BeInRange(0.0, 1.0);
        assessment.DetermineReadinessStatus(score, MissionLevel.Critical).Should().Be("NOT READY");
    }

    /// <summary>
    /// NaN and infinities must not survive into the weighted total. A NaN comparison against
    /// the threshold is not a decision, and infinity clears every mission level.
    /// </summary>
    [Fact]
    public void OverallReadiness_TreatsNonFiniteCategoryScoresAsNoCredit()
    {
        var report = ReportWith(
            (ReadinessCategory.Safety, double.NaN),
            (ReadinessCategory.Technical, double.PositiveInfinity),
            (ReadinessCategory.Regulatory, double.NegativeInfinity));

        Subject().CalculateOverallReadinessScore(report).Should().Be(0.0);
    }
}
