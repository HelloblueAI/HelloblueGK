using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using HB_NLP_Research_Lab.Physics;
using Xunit;

namespace HelloblueGK.Tests.Unit.Aerospace;

/// <summary>
/// Cross-checks the concept engine's declared parameters against the ideal-rocket relations in
/// <see cref="IdealRocketNozzle"/>, which are validated against Merlin 1D, Raptor, and RS-25.
///
/// A declaration can be internally inconsistent in ways no type system catches: an expansion ratio
/// that disagrees with the declared diameters, a thrust the declared throat cannot pass, a specific
/// impulse the propellant cannot reach. Those numbers are read by the API, the design exports, and
/// the Blender visualization, so an error in them propagates into everything downstream.
///
/// The declaration used to disagree with itself in three places: the expansion ratio was a diameter
/// ratio stored in an area-ratio field, the 3.5 MN thrust needed a larger throat than the one
/// declared, and 420 s of specific impulse sat above what methane/LOX can deliver at that expansion.
/// Those were reconciled by sizing the throat and exit to the declared thrust, chamber pressure, and
/// area ratio, and by setting specific impulse to the ideal value. <see cref="KnownDeviations"/> is
/// now empty. It stays as a waiver register under configuration control, and it still cuts three ways:
///
///   - a new inconsistency, or one in another quantity, fails the build;
///   - a registered deviation whose magnitude drifts fails the build;
///   - reconciling a deviation also fails the build, until its entry is deleted.
///
/// So the numbers cannot quietly get worse, and they cannot quietly get better either.
/// </summary>
public class EngineDesignConsistencyTests
{
    /// <summary>
    /// Literature values for a methane/LOX combustion product mixture, the same ones the Raptor case
    /// in <c>IdealRocketNozzleTests</c> is validated against. They are not tuned to this engine.
    ///
    /// The engine design declares no chamber temperature, so 3600 K is an assumption here. It does
    /// not affect the geometry or thrust checks — thrust follows from the thrust coefficient, which
    /// depends only on the specific heat ratio and the area ratio — but the specific impulse and
    /// ceiling checks do depend on it, and their tolerances are set accordingly.
    /// </summary>
    private const double SpecificHeatRatio = 1.20;
    private const double MolarMass = 0.0206;
    private const double AssumedChamberTemperature = 3600.0;
    private const double StandardGravity = 9.80665;

    private sealed record Deviation(string Quantity, double Declared, double FirstPrinciples, double Tolerance, string Note);

    private static readonly Deviation[] KnownDeviations = [];

    private static async Task<HB_NLP_Research_Lab.Aerospace.HB_NLP_EngineDesign> DeclaredDesignAsync()
    {
        var engine = new HB_NLP_Research_Lab.Aerospace.HB_NLP_RevolutionaryEngine();
        await engine.InitializeAsync();
        return await engine.CreateRevolutionaryEngineAsync();
    }

    private static double ThroatArea(double throatDiameter) => Math.PI / 4.0 * throatDiameter * throatDiameter;

    private static NozzleSolution SolveFromDeclaredGeometry(
        HB_NLP_Research_Lab.Aerospace.HB_NLP_EngineDesign design,
        double expansionRatio) =>
        IdealRocketNozzle.Solve(new EngineOperatingPoint
        {
            // Declared chamber pressure is in bar; the solver works in pascals.
            ChamberPressure = design.ChamberPressure * 1e5,
            ChamberTemperature = AssumedChamberTemperature,
            SpecificHeatRatio = SpecificHeatRatio,
            MolarMass = MolarMass,
            ThroatArea = ThroatArea(design.ThroatDiameter),
            ExpansionRatio = expansionRatio,
            // Vacuum is the most generous case, so a shortfall here is a shortfall everywhere.
            AmbientPressure = 0.0
        });

    /// <summary>
    /// Pure geometry, independent of any propellant assumption: an expansion ratio is an area ratio,
    /// so it must equal the square of the diameter ratio.
    /// </summary>
    [Fact]
    public async Task DeclaredExpansionRatio_MatchesTheAreaRatioImpliedByTheDeclaredDiameters()
    {
        var design = await DeclaredDesignAsync();

        var diameterRatio = design.ExitDiameter / design.ThroatDiameter;
        var impliedAreaRatio = diameterRatio * diameterRatio;

        AssertConsistentOrRegistered("ExpansionRatio", design.ExpansionRatio, impliedAreaRatio, 0.02);
    }

    /// <summary>
    /// Thrust follows from the thrust coefficient, chamber pressure, and throat area. The thrust
    /// coefficient depends only on the specific heat ratio and the area ratio, so this check does
    /// not rest on the assumed chamber temperature.
    /// </summary>
    [Fact]
    public async Task DeclaredThrust_MatchesTheIdealThrustOfTheDeclaredGeometry()
    {
        var design = await DeclaredDesignAsync();

        var ideal = SolveFromDeclaredGeometry(design, design.ExpansionRatio);

        AssertConsistentOrRegistered("Thrust_kN", design.Thrust / 1e3, ideal.Thrust / 1e3, 0.05);
    }

    [Fact]
    public async Task DeclaredSpecificImpulse_MatchesTheIdealValueAtTheDeclaredExpansionRatio()
    {
        var design = await DeclaredDesignAsync();

        var ideal = SolveFromDeclaredGeometry(design, design.ExpansionRatio);

        AssertConsistentOrRegistered("SpecificImpulse_s", design.SpecificImpulse, ideal.SpecificImpulse, 0.05);
    }

    /// <summary>
    /// The hardest bound available. Expanding to zero pressure converts all thermal energy to kinetic
    /// energy, so no nozzle, geometry, or efficiency can exceed this. A declared value above it is
    /// not optimistic, it is impossible.
    /// </summary>
    [Fact]
    public async Task DeclaredSpecificImpulse_StaysBelowThePropellantThermodynamicCeiling()
    {
        var design = await DeclaredDesignAsync();

        var r = IdealRocketNozzle.SpecificGasConstant(MolarMass);
        var limitingExhaustVelocity = Math.Sqrt(
            2.0 * SpecificHeatRatio / (SpecificHeatRatio - 1.0) * r * AssumedChamberTemperature);
        var ceiling = limitingExhaustVelocity / StandardGravity;

        design.SpecificImpulse.Should().BeLessThan(
            ceiling,
            "no expansion can exceed the thermodynamic limit for this propellant "
            + $"({ceiling:F1} s at {AssumedChamberTemperature:F0} K)");
    }

    /// <summary>
    /// The design package is generated separately from the engine class. This fails if
    /// <c>Docs/Designs/HB-NLP-REV-001/design.json</c> again declares a different thrust, specific
    /// impulse, chamber pressure, expansion ratio, or geometry for the same model.
    /// </summary>
    [Fact]
    public async Task DesignPackage_DeclaresTheSameParametersAsTheEngine()
    {
        var design = await DeclaredDesignAsync();
        using var document = JsonDocument.Parse(File.ReadAllText(DesignPackagePath()));
        var root = document.RootElement;

        root.GetProperty("model_id").GetString().Should().Be("HB-NLP-REV-001");

        var specifications = root.GetProperty("specifications");
        specifications.GetProperty("thrust").GetDouble().Should().Be(design.Thrust);
        specifications.GetProperty("specific_impulse").GetDouble().Should().Be(design.SpecificImpulse);
        specifications.GetProperty("chamber_pressure").GetDouble().Should().Be(design.ChamberPressure);
        specifications.GetProperty("expansion_ratio").GetDouble().Should().Be(design.ExpansionRatio);
        specifications.TryGetProperty("technology_readiness_level", out _).Should().BeFalse();
        specifications.TryGetProperty("efficiency", out _).Should().BeFalse();

        var geometry = root.GetProperty("geometry");
        geometry.GetProperty("chamber_diameter").GetDouble().Should().Be(design.ChamberDiameter);
        geometry.GetProperty("chamber_length").GetDouble().Should().Be(design.ChamberLength);
        geometry.GetProperty("throat_diameter").GetDouble().Should().Be(design.ThroatDiameter);
        geometry.GetProperty("exit_diameter").GetDouble().Should().Be(design.ExitDiameter);
        geometry.GetProperty("nozzle_length").GetDouble().Should().Be(design.NozzleLength);

        root.TryGetProperty("performance_metrics", out _).Should().BeFalse(
            "solver convergence, power, and hardware figures in the old package were constants, not measurements");
    }

    // Path.Join concatenates these parts. Path.Combine would discard the walked
    // directory if any later part were rooted, which is what cs/path-combine flags.
    private static readonly string RelativeDesignPath = Path.Join(
        "Docs", "Designs", "HB-NLP-REV-001", "design.json");

    private static string DesignPackagePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Join(directory.FullName, RelativeDesignPath);
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not find Docs/Designs/HB-NLP-REV-001/design.json by walking up from the test output directory.");
    }

    /// <summary>
    /// Guards the register itself. Every entry must name a quantity one of the checks above
    /// evaluates, otherwise a stale waiver could sit here forever describing nothing.
    /// </summary>
    [Fact]
    public void KnownDeviations_OnlyNameQuantitiesThatAreActuallyChecked()
    {
        var checkedQuantities = new[] { "ExpansionRatio", "Thrust_kN", "SpecificImpulse_s" };

        KnownDeviations.Select(d => d.Quantity).Should().OnlyHaveUniqueItems();
        KnownDeviations.Select(d => d.Quantity).Should().BeSubsetOf(checkedQuantities);
        KnownDeviations.Should().OnlyContain(d => !string.IsNullOrWhiteSpace(d.Note));
    }

    private static void AssertConsistentOrRegistered(
        string quantity,
        double declared,
        double firstPrinciples,
        double relativeTolerance)
    {
        var registered = KnownDeviations.SingleOrDefault(d => d.Quantity == quantity);

        if (Math.Abs(declared - firstPrinciples) <= relativeTolerance * Math.Abs(firstPrinciples))
        {
            registered.Should().BeNull(
                $"{quantity} now agrees with first principles ({declared:F1} against {firstPrinciples:F1}), "
                + "so delete its entry from KnownDeviations — leaving it would misrepresent the design "
                + "as still inconsistent");
            return;
        }

        registered.Should().NotBeNull(
            $"{quantity} disagrees with first principles: declared {declared:F1}, ideal theory gives "
            + $"{firstPrinciples:F1}. Either correct the engine declaration or, if the discrepancy is "
            + "intended, add it to KnownDeviations with the reason");

        declared.Should().BeApproximately(
            registered!.Declared,
            registered.Tolerance,
            $"the declared {quantity} changed from the registered deviation, so re-check the register");

        firstPrinciples.Should().BeApproximately(
            registered.FirstPrinciples,
            registered.Tolerance,
            $"the first-principles {quantity} moved, so the recorded magnitude of this deviation is stale");
    }
}
