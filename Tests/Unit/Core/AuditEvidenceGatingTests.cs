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
