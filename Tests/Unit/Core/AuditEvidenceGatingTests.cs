using FluentAssertions;
using HB_NLP_Research_Lab.Aerospace;
using HB_NLP_Research_Lab.Core;
using Xunit;

namespace HelloblueGK.Tests.Unit.Core;

/// <summary>
/// The quality, security, and compliance audits used to assert their own inputs — every
/// boolean <c>true</c>, every metric a passing constant — and then grade those inputs
/// against thresholds, so a passing verdict was unconditional and said nothing about the
/// system under audit.
///
/// These tests pin the replacement: inputs come from <see cref="AuditEvidence"/>, and an
/// audit with no evidence fails rather than passes. Reintroducing a hardcoded attestation
/// makes one of these fail.
/// </summary>
public class AuditEvidenceGatingTests
{
    [Fact]
    public async Task QualityAudit_WithNoEvidence_AwardsNoControlsAndScoresZero()
    {
        var report = await new QualityAssuranceSystem().PerformQualityAuditAsync();

        report.Controls.Should().NotContain(
            c => c.Status == "Evidence accepted",
            "no quality objective can be accepted when no evidence was supplied");
        report.OverallQuality.Should().Be(0.0);
        report.Defects.Should().NotBeEmpty("each unmet objective should be recorded");
    }

    [Fact]
    public async Task SecurityAudit_WithNoEvidence_RecordsFindingsRatherThanPasses()
    {
        var report = await new SecurityAuditSystem().PerformSecurityAuditAsync();

        report.Controls.Should().NotContain(c => c.Status == "Evidence accepted");
        report.Vulnerabilities.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ComplianceAudit_WithNoEvidence_IssuesNoCertificationAndIsNotCompliant()
    {
        var report = await new AerospaceComplianceSystem().PerformFullComplianceAuditAsync();

        report.Certifications.Should().BeEmpty(
            "a certification must never be minted from an unevidenced audit");
        report.OverallCompliance.Should().BeFalse();
        report.Violations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ReadinessAssessment_WithNoEvidence_IsNotReady()
    {
        var subject = new AerospaceReadinessAssessment();

        var report = await subject.PerformComprehensiveAssessmentAsync(MissionLevel.Critical);

        report.ReadinessCategories.Should().OnlyContain(
            c => c.ReadinessScore == 0.0,
            "no category may award credit that no evidence supports");
        report.OverallReadiness.Should().Be(0.0);
        report.ReadinessStatus.Should().NotBe("READY");
        (await subject.IsReadyForAdvancedAerospaceAsync()).Should().BeFalse();
        (await subject.IsReadyForMissionCriticalOperationsAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task NasaAndSpaceXReadiness_WithNoEvidence_AreBothFalse()
    {
        var subject = new AerospaceComplianceSystem();

        (await subject.IsReadyForNASAAsync()).Should().BeFalse();
        (await subject.IsReadyForSpaceXAsync()).Should().BeFalse();
    }

    /// <summary>
    /// Environmental compliance was the only standard whose failure added no violation.
    /// Overall compliance is <c>Violations.Count == 0</c>, and NASA / SpaceX readiness
    /// require that flag. While quality and security could never pass, the omission was
    /// hidden. Evidence that meets every other standard must still be able to pass, and
    /// withdrawing one environmental requirement must fail the audit closed.
    /// </summary>
    [Fact]
    public async Task ComplianceAudit_UnmetEnvironmentalRequirement_FailsClosed()
    {
        var subject = new AerospaceComplianceSystem();
        var complete = EvidenceThatMeetsEveryRecordedThreshold();

        var passed = await subject.PerformFullComplianceAuditAsync(complete);

        passed.OverallCompliance.Should().BeTrue(
            "evidence that meets every recorded threshold must be able to pass");
        passed.Violations.Should().BeEmpty();
        passed.Certifications.Should().Contain(c => c.Type == "Environmental" && c.Status == "Evidence accepted");
        (await subject.IsReadyForNASAAsync(complete)).Should().BeTrue();
        (await subject.IsReadyForSpaceXAsync(complete)).Should().BeTrue();

        var missingEmissionsControl = EvidenceThatMeetsEveryRecordedThreshold()
            .Attest("Environmental.EmissionsControl", false);

        var failed = await subject.PerformFullComplianceAuditAsync(missingEmissionsControl);

        failed.OverallCompliance.Should().BeFalse(
            "an unmet environmental requirement is a compliance failure, not a silent omission");
        failed.Violations.Should().ContainSingle(v => v.Standard == "Environmental" && v.RemediationRequired);
        failed.Certifications.Should().NotContain(c => c.Type == "Environmental");
        (await subject.IsReadyForNASAAsync(missingEmissionsControl)).Should().BeFalse();
        (await subject.IsReadyForSpaceXAsync(missingEmissionsControl)).Should().BeFalse();
    }

    [Fact]
    public void Build_LeavesUnevidencedPropertiesAtTheirFailingDefaults()
    {
        var check = AuditEvidence.None.Build<FIPS140ComplianceCheck>("FIPS140");

        check.IsCompliant().Should().BeFalse();
    }

    [Fact]
    public void Build_ReadsBooleansNumbersAndTextFromEvidenceUnderTheStandardPrefix()
    {
        var evidence = new AuditEvidence()
            .Attest("FIPS140.CryptographicModule")
            .Attest("FIPS140.CryptographicAlgorithms")
            .Measure("FIPS140.Level", 2);

        var check = evidence.Build<FIPS140ComplianceCheck>("FIPS140");

        check.CryptographicModule.Should().BeTrue();
        check.CryptographicAlgorithms.Should().BeTrue();
        check.Level.Should().Be(2);
        check.KeyManagement.Should().BeFalse("it was never attested");

        var environmental = new AuditEvidence()
            .Record("Environmental.CarbonFootprint", "measured, 12 t CO2e")
            .Build<EnvironmentalComplianceCheck>("Environmental");

        environmental.CarbonFootprint.Should().Be("measured, 12 t CO2e");
        environmental.EnvironmentalImpact.Should().BeEmpty("it was never recorded");
    }

    [Fact]
    public void Build_IgnoresEvidenceFiledUnderADifferentStandard()
    {
        var evidence = new AuditEvidence().Attest("SomeOtherStandard.CryptographicModule");

        var check = evidence.Build<FIPS140ComplianceCheck>("FIPS140");

        check.CryptographicModule.Should().BeFalse();
    }

    [Fact]
    public async Task FullyEvidencedAudit_CanStillPass_SoTheGateIsNotSimplyAlwaysFalse()
    {
        // Attest every boolean the FIPS check requires, proving the change gates on
        // evidence rather than hardcoding failure.
        var evidence = new AuditEvidence();
        foreach (var property in typeof(FIPS140ComplianceCheck).GetProperties())
        {
            if (property.PropertyType == typeof(bool))
            {
                evidence.Attest($"FIPS140.{property.Name}");
            }
        }
        evidence.Measure("FIPS140.Level", 2);

        var check = evidence.Build<FIPS140ComplianceCheck>("FIPS140");

        check.IsCompliant().Should().BeTrue();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Records what the readiness predicates hand to the assessment. Both predicates take
    /// evidence and immediately delegate, so the only thing worth checking about them is
    /// whether what the caller supplied actually arrives.
    /// </summary>
    private sealed class EvidenceRecordingAssessment : AerospaceReadinessAssessment
    {
        public bool WasCalled { get; private set; }
        public AuditEvidence? ReceivedEvidence { get; private set; }
        public MissionLevel ReceivedMissionLevel { get; private set; }

        public override Task<AerospaceReadinessReport> PerformComprehensiveAssessmentAsync(
            MissionLevel missionLevel = MissionLevel.Critical,
            AuditEvidence? evidence = null)
        {
            WasCalled = true;
            ReceivedEvidence = evidence;
            ReceivedMissionLevel = missionLevel;
            return Task.FromResult(new AerospaceReadinessReport());
        }
    }

    /// <summary>
    /// <c>IsReadyForMissionCriticalOperationsAsync</c> accepted an <see cref="AuditEvidence"/>
    /// and then called the assessment without it, so caller-supplied evidence was silently
    /// discarded and that path always behaved like an empty audit no matter what was proven.
    ///
    /// The bug was invisible to every existing test because they all called the predicate with
    /// no evidence, where dropping the argument and honouring it produce the same answer. Its
    /// sibling <c>IsReadyForAdvancedAerospaceAsync</c> forwarded correctly, so both are pinned
    /// here rather than only the one that was broken.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadinessPredicates_ForwardTheEvidenceTheCallerSupplied(bool missionCritical)
    {
        var evidence = new AuditEvidence().Attest("Technical.PerformanceValidation");
        var subject = new EvidenceRecordingAssessment();

        if (missionCritical)
        {
            await subject.IsReadyForMissionCriticalOperationsAsync(evidence);
        }
        else
        {
            await subject.IsReadyForAdvancedAerospaceAsync(MissionLevel.Critical, evidence);
        }

        subject.WasCalled.Should().BeTrue();
        subject.ReceivedEvidence.Should().BeSameAs(evidence,
            "evidence the caller proved must reach the assessment, not be replaced by an empty audit");
        subject.ReceivedMissionLevel.Should().Be(MissionLevel.Critical);
    }

    /// <summary>
    /// A critical mission used to be unreachable: it required twelve certifications from a system
    /// that issues nine, and the quality and security gates demanded 0.99 from pass-path scores
    /// that average below that. With those two ceilings removed, evidence that actually meets
    /// every recorded threshold must be able to clear the mission-critical predicate.
    /// </summary>
    [Fact]
    public async Task MissionCriticalReadiness_CanBeClearedWhenEveryThresholdIsMet()
    {
        var evidence = EvidenceThatMeetsEveryRecordedThreshold();
        var subject = new AerospaceReadinessAssessment();

        var report = await subject.PerformComprehensiveAssessmentAsync(MissionLevel.Critical, evidence);

        report.ReadinessStatus.Should().Be("READY");
        report.OverallReadiness.Should().BeGreaterThanOrEqualTo(0.98);
        (await subject.IsReadyForMissionCriticalOperationsAsync(evidence)).Should().BeTrue();
        (await subject.IsReadyForAdvancedAerospaceAsync(MissionLevel.Critical, evidence)).Should().BeTrue();
    }

    private static AuditEvidence EvidenceThatMeetsEveryRecordedThreshold()
    {
        var evidence = new AuditEvidence();

        Fill<AS9100QualityAudit>(evidence, "AS9100");
        Fill<ISO9001QualityAudit>(evidence, "ISO9001");
        Fill<SixSigmaQualityAudit>(evidence, "SixSigma");
        Fill<MissionCriticalQualityAudit>(evidence, "MissionCritical");
        Fill<SoftwareQualityAudit>(evidence, "Software");
        Fill<HardwareQualityAudit>(evidence, "Hardware");
        Fill<ProcessQualityAudit>(evidence, "Process");
        Fill<SupplierQualityAudit>(evidence, "Supplier");

        Fill<CryptographicSecurityAudit>(evidence, "Cryptographic");
        Fill<NetworkSecurityAudit>(evidence, "Network");
        Fill<ApplicationSecurityAudit>(evidence, "Application");
        Fill<PhysicalSecurityAudit>(evidence, "Physical");
        Fill<AccessControlAudit>(evidence, "AccessControl");
        Fill<DataProtectionAudit>(evidence, "DataProtection");
        Fill<IncidentResponseAudit>(evidence, "IncidentResponse");
        Fill<SecurityComplianceAudit>(evidence, "SecurityCompliance");
        evidence.Record("Cryptographic.AlgorithmStrength", "AES-256");

        Fill<DO178CComplianceCheck>(evidence, "DO178C");
        Fill<NASANPR7150ComplianceCheck>(evidence, "NASANPR7150");
        Fill<ITARComplianceCheck>(evidence, "ITAR");
        Fill<FIPS140ComplianceCheck>(evidence, "FIPS140");
        Fill<MissionCriticalComplianceCheck>(evidence, "MissionCritical");
        Fill<EnvironmentalComplianceCheck>(evidence, "Environmental");
        Fill<ExportControlComplianceCheck>(evidence, "ExportControl");

        evidence
            .Measure("Technical.TechnologyReadinessLevel", 9)
            .Attest("Technical.PerformanceValidation")
            .Attest("Technical.ReliabilityAnalysis")
            .Measure("Safety.SafetyFactor", 4)
            .Measure("Safety.RedundancyLevel", 4)
            .Measure("Safety.FaultTolerance", 1)
            .Measure("Operational.Availability", 1)
            .Measure("Operational.Maintainability", 1)
            .Measure("Operational.Supportability", 1)
            .Attest("Environmental.EnvironmentalCertification")
            .Attest("Environmental.EnergyEfficiency")
            .Attest("Environmental.SustainableMaterials")
            .Attest("Financial.FinancialStability")
            .Measure("Financial.ReturnOnInvestment", 0.25)
            .Measure("Financial.CostEfficiency", 1);

        return evidence;
    }

    private static void Fill<T>(AuditEvidence evidence, string standard)
    {
        foreach (var property in typeof(T).GetProperties())
        {
            var key = $"{standard}.{property.Name}";
            if (property.PropertyType == typeof(bool))
            {
                evidence.Attest(key);
            }
            else if (property.PropertyType == typeof(string))
            {
                evidence.Record(key, "attested");
            }
            else if (property.PropertyType == typeof(double) || property.PropertyType == typeof(int))
            {
                evidence.Measure(key, PassingMeasure(property.Name));
            }
        }
    }

    private static double PassingMeasure(string name) => name switch
    {
        "DefectRate" => 3.4,
        "ProcessVariation" => 0.001,
        "CustomerSatisfaction" => 0.99,
        "CostOfPoorQuality" => 0.01,
        "Reliability" => 0.9999,
        "Availability" => 0.9995,
        "Maintainability" => 0.99,
        "Safety" => 0.99999,
        "FaultTolerance" => 0.9999,
        "MeanTimeBetweenFailures" => 10_000,
        "MeanTimeToRepair" => 1,
        "CodeCoverage" => 0.95,
        "CyclomaticComplexity" => 10,
        "MaintainabilityIndex" => 85,
        "TechnicalDebt" => 0.05,
        "BugDensity" => 0.1,
        "Redundancy" or "RedundancyLevel" => 4,
        "SafetyFactor" => 4,
        "Level" => 2,
        _ => 1
    };

    /// <summary>
    /// Measurements are fractions of a requirement, not unbounded magnitudes. Fault tolerance,
    /// TRL, availability, and cost efficiency used to be added raw, so a single value of
    /// one million carried a category — and the mission-critical predicate — through READY
    /// while every other requirement was absent. The same path threw when a count such as
    /// redundancy did not fit in an int.
    /// </summary>
    [Fact]
    public async Task MissionCriticalReadiness_CannotBeForgedByMeasurementsOutsideTheUnitInterval()
    {
        var evidence = new AuditEvidence()
            .Measure("Safety.FaultTolerance", 1_000_000)
            .Measure("Safety.SafetyFactor", 1_000_000)
            .Measure("Safety.RedundancyLevel", double.PositiveInfinity)
            .Measure("Technical.TechnologyReadinessLevel", 1_000_000)
            .Measure("Technical.FlightHeritage", double.NaN)
            .Measure("Operational.Availability", 1_000_000)
            .Measure("Operational.Maintainability", 1_000_000)
            .Measure("Operational.Supportability", double.NaN)
            .Measure("Financial.CostEfficiency", 1_000_000)
            .Measure("Financial.ReturnOnInvestment", double.PositiveInfinity);

        var subject = new AerospaceReadinessAssessment();

        var report = await subject.PerformComprehensiveAssessmentAsync(MissionLevel.Critical, evidence);

        report.ReadinessCategories.Should().OnlyContain(
            c => c.ReadinessScore >= 0.0 && c.ReadinessScore <= 1.0,
            "no category may score above fully ready");
        report.OverallReadiness.Should().BeInRange(0.0, 1.0);
        report.ReadinessStatus.Should().NotBe("READY");
        (await subject.IsReadyForMissionCriticalOperationsAsync(evidence)).Should().BeFalse();
        (await subject.IsReadyForAdvancedAerospaceAsync(MissionLevel.Critical, evidence)).Should().BeFalse();
    }

    [Fact]
    public void EvidenceKeys_MustNotBeBlank()
    {
        var evidence = new AuditEvidence();

        evidence.Invoking(e => e.Attest("  ")).Should().Throw<ArgumentException>();
        evidence.Invoking(e => e.Measured("")).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void None_IsEmpty_AndAnInstanceWithEvidenceIsNot()
    {
        AuditEvidence.None.IsEmpty.Should().BeTrue();
        new AuditEvidence().Attest("Any.Thing").IsEmpty.Should().BeFalse();
    }
}
