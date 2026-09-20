using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using HB_NLP_Research_Lab.Aerospace;
using Xunit;

namespace HelloblueGK.Tests.Unit.Aerospace;

/// <summary>
/// Verification of the compliance checks that decide whether the project may claim DO-178C,
/// NPR 7150.2, ITAR, FIPS 140, mission-critical, environmental, and export-control standing.
///
/// Each check is a long conjunction of requirement flags. The failure mode that matters is
/// not a wrong answer but a silently ignored requirement: drop one clause from the
/// conjunction and the check still returns true for a system that no longer satisfies it,
/// which is precisely the sort of omission a reviewer cannot see by reading a passing test
/// suite. So rather than assert a handful of cases, these tests enumerate every requirement
/// flag by reflection and prove each one can independently veto the claim. Adding a new flag
/// to a check without wiring it into IsCompliant() fails here automatically.
/// </summary>
public class AerospaceComplianceCheckTests
{
    /// <summary>
    /// Sets every boolean requirement to satisfied, and every numeric threshold to a value
    /// that clears the bar, giving a check that should be fully compliant.
    /// </summary>
    private static T FullySatisfied<T>() where T : new()
    {
        var check = new T();

        foreach (var property in WritableBooleans<T>())
        {
            property.SetValue(check, true);
        }

        // Numeric thresholds have no "true" — MissionCriticalComplianceCheck is the only
        // check that gates on them, so its bars are set explicitly.
        if (check is MissionCriticalComplianceCheck mission)
        {
            mission.RedundancyLevel = 3;
            mission.FaultTolerance = 0.9999;
            mission.MeanTimeBetweenFailures = 10000;
            mission.MeanTimeToRepair = 1;
            mission.SafetyFactor = 2.5;
        }

        return check;
    }

    private static IEnumerable<PropertyInfo> WritableBooleans<T>() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(bool) && p.CanWrite);

    private static bool IsCompliant(object check) =>
        (bool)check.GetType().GetMethod("IsCompliant")!.Invoke(check, null)!;

    public static TheoryData<Type> CheckTypes => new()
    {
        typeof(DO178CComplianceCheck),
        typeof(NASANPR7150ComplianceCheck),
        typeof(ITARComplianceCheck),
        typeof(FIPS140ComplianceCheck),
        typeof(MissionCriticalComplianceCheck),
        typeof(EnvironmentalComplianceCheck),
        typeof(ExportControlComplianceCheck)
    };

    /// <summary>
    /// A fully satisfied check must actually report compliance. Without this, a check whose
    /// conjunction can never be true would pass the veto test below for the wrong reason.
    /// </summary>
    [Theory]
    [MemberData(nameof(CheckTypes))]
    public void FullySatisfiedCheck_ReportsCompliant(Type checkType)
    {
        var check = typeof(AerospaceComplianceCheckTests)
            .GetMethod(nameof(FullySatisfied), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(checkType)
            .Invoke(null, null)!;

        IsCompliant(check).Should().BeTrue(
            "{0} with every requirement satisfied must be able to claim compliance",
            checkType.Name);
    }

    /// <summary>
    /// The core property: every requirement flag must be load-bearing. If any single flag can
    /// be false while the check still claims compliance, that requirement is not being
    /// enforced and the standard is being asserted without evidence.
    /// </summary>
    [Theory]
    [MemberData(nameof(CheckTypes))]
    public void EveryRequirementFlag_CanIndependentlyVetoCompliance(Type checkType)
    {
        var satisfiedFactory = typeof(AerospaceComplianceCheckTests)
            .GetMethod(nameof(FullySatisfied), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(checkType);

        var flags = checkType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(bool) && p.CanWrite)
            .ToList();

        flags.Should().NotBeEmpty("{0} must gate on at least one requirement", checkType.Name);

        var ignored = new List<string>();

        foreach (var flag in flags)
        {
            var check = satisfiedFactory.Invoke(null, null)!;
            flag.SetValue(check, false);

            if (IsCompliant(check))
            {
                ignored.Add(flag.Name);
            }
        }

        ignored.Should().BeEmpty(
            "these {0} requirements are declared but not enforced by IsCompliant(), so the " +
            "standard would be claimed without them: {1}",
            checkType.Name,
            string.Join(", ", ignored));
    }

    // ---------------------------------------------------------------------------------
    // Mission-critical numeric thresholds
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// The mission-critical bars are the only quantitative gates in the compliance system,
    /// so their boundaries are pinned exactly. Each case sits one step on the failing side of
    /// a single threshold with everything else satisfied, which is where an inequality typo
    /// (&gt; for &gt;=, or a reversed comparison on repair time) would hide.
    /// </summary>
    [Theory]
    // Redundancy: triple redundancy is the floor.
    [InlineData("RedundancyLevel", 2.0, false)]
    [InlineData("RedundancyLevel", 3.0, true)]
    // Fault tolerance: four nines.
    [InlineData("FaultTolerance", 0.9998, false)]
    [InlineData("FaultTolerance", 0.9999, true)]
    // MTBF in hours.
    [InlineData("MeanTimeBetweenFailures", 9999.0, false)]
    [InlineData("MeanTimeBetweenFailures", 10000.0, true)]
    // Safety factor.
    [InlineData("SafetyFactor", 2.49, false)]
    [InlineData("SafetyFactor", 2.5, true)]
    public void MissionCritical_NumericThresholdsHoldAtTheirBoundary(
        string propertyName,
        double value,
        bool expectedCompliant)
    {
        var check = FullySatisfied<MissionCriticalComplianceCheck>();
        var property = typeof(MissionCriticalComplianceCheck).GetProperty(propertyName)!;

        property.SetValue(check, Convert.ChangeType(value, property.PropertyType));

        check.IsCompliant().Should().Be(expectedCompliant);
    }

    /// <summary>
    /// Repair time is the one threshold where smaller is better, so it is asserted separately:
    /// a copy-paste of the "greater than or equal" pattern used by every other bar would make
    /// a slow-to-repair system look compliant.
    /// </summary>
    [Theory]
    [InlineData(0.5, true)]
    [InlineData(1.0, true)]
    [InlineData(1.01, false)]
    [InlineData(24.0, false)]
    public void MissionCritical_RepairTimeIsAnUpperBoundNotALowerOne(double hours, bool expected)
    {
        var check = FullySatisfied<MissionCriticalComplianceCheck>();

        check.MeanTimeToRepair = hours;

        check.IsCompliant().Should().Be(expected);
    }

    /// <summary>
    /// A default-constructed check must not claim compliance. Anything that asks "are we
    /// compliant?" before evidence has been recorded has to hear no.
    /// </summary>
    [Theory]
    [MemberData(nameof(CheckTypes))]
    public void DefaultConstructedCheck_IsNotCompliant(Type checkType)
    {
        var check = Activator.CreateInstance(checkType)!;

        IsCompliant(check).Should().BeFalse(
            "{0} must fail closed before any requirement has been recorded",
            checkType.Name);
    }
}
