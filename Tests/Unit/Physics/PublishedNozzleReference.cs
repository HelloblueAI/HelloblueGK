using HB_NLP_Research_Lab.Physics;

namespace HelloblueGK.Tests.Unit.Physics;

/// <summary>
/// Published operating points for <see cref="IdealRocketNozzle"/>.
///
/// Every performance number sits next to the public source it was taken from. Propellant
/// gamma, molar mass, and chamber temperature are literature assumptions shared by every
/// point that burns that propellant. They are not measurements from the engine, and they
/// are not adjusted so that one point lands closer to its published specific impulse.
///
/// A point is omitted, and the omission is listed in
/// <c>Docs/Technical/VALIDATION_AND_BENCHMARKS.md</c>, when the only way to accept it would
/// be to retune those propellant assumptions or to combine figures from different engines.
/// </summary>
public static class PublishedNozzleReference
{
    public const double RelativeBand = 0.03;

    public const double SeaLevelPressurePascals = 101325.0;

    /// <summary>Exact pound-force per square inch to pascal, so a published psi figure is not pre-rounded.</summary>
    private const double PascalsPerPsi = 6894.757293168361;

    public static readonly SpecificImpulsePoint[] SpecificImpulse =
    [
        new(
            Id: "merlin-1d-sea-level",
            Label: "Merlin 1D, sea level",
            ChamberPressurePascals: 9.7e6,
            ChamberTemperatureKelvin: 3500,
            SpecificHeatRatio: 1.24,
            MolarMassKgPerMol: 0.0223,
            ExpansionRatio: 16,
            AmbientPressurePascals: SeaLevelPressurePascals,
            PublishedSpecificImpulseSeconds: 282.0,
            PerformanceSource: "https://en.wikipedia.org/wiki/SpaceX_Merlin . The Merlin 1D section states a "
                + "chamber pressure of 9.7 MPa and an expansion ratio of 16. The article infobox lists "
                + "sea-level specific impulse 282 s and sea-level thrust 845 kN beside that chamber pressure. "
                + "The same infobox lists a vacuum specific impulse of 311 s, flagged as needing an update, "
                + "which is not the Merlin 1D Vacuum figure of 348 s at an expansion ratio of 165. Neither "
                + "vacuum figure is used. No throat area is published with the 282 s figure, so thrust is not checked."),
        new(
            Id: "raptor-sea-level",
            Label: "Raptor, sea level",
            ChamberPressurePascals: 30e6,
            ChamberTemperatureKelvin: 3600,
            SpecificHeatRatio: 1.20,
            MolarMassKgPerMol: 0.0206,
            ExpansionRatio: 34,
            AmbientPressurePascals: SeaLevelPressurePascals,
            PublishedSpecificImpulseSeconds: 330.0,
            PerformanceSource: "https://en.wikipedia.org/wiki/SpaceX_Raptor records a mid-2018 public expectation "
                + "of 330 s sea-level specific impulse, and a separate statement aiming for roughly 300 bar. "
                + "The area ratio of 34 is the round figure this comparison has used; it is not in that 2018 "
                + "sentence. A 2019 plume study cites 34.34 at about 253 bar, and the current infobox mixes "
                + "later generations. Those later rows are not substituted in. No throat area is paired with "
                + "the 330 s figure, so thrust is not checked."),
        new(
            Id: "rs-25-vacuum",
            Label: "RS-25, vacuum, 109 percent",
            ChamberPressurePascals: 20.64e6,
            ChamberTemperatureKelvin: 3588,
            SpecificHeatRatio: 1.19,
            MolarMassKgPerMol: 0.0136,
            ExpansionRatio: 69,
            AmbientPressurePascals: 0,
            PublishedSpecificImpulseSeconds: 452.3,
            PerformanceSource: "L3Harris RS-25 specification sheet, July 2024, "
                + "https://www.l3harris.com/sites/default/files/2024-07/l3harris-ar-rs-25-spec-sheet.pdf : "
                + "at 109 percent power, vacuum specific impulse 452.3 s, chamber pressure 2,994 psia (20.64 MPa), "
                + "exit-to-throat area ratio 69. Vacuum thrust 512,300 lbf is on the same sheet."),
        new(
            Id: "rs-25-sea-level",
            Label: "RS-25, sea level, 109 percent",
            ChamberPressurePascals: 20.64e6,
            ChamberTemperatureKelvin: 3588,
            SpecificHeatRatio: 1.19,
            MolarMassKgPerMol: 0.0136,
            ExpansionRatio: 69,
            AmbientPressurePascals: SeaLevelPressurePascals,
            PublishedSpecificImpulseSeconds: 366.0,
            PerformanceSource: "L3Harris RS-25 product page, same 109 percent power level, "
                + "https://www.l3harris.com/all-capabilities/rs-25-engine : sea-level specific impulse 366 s. "
                + "The July 2024 specification sheet does not list a sea-level specific impulse. It does list "
                + "the same area ratio 69 and chamber pressure 2,994 psia, which this point uses. "
                + "Sea level ambient pressure is the standard atmosphere, 101325 Pa, not a figure on either page."),
        new(
            Id: "rl10a-4-2-vacuum",
            Label: "RL10A-4-2, vacuum",
            ChamberPressurePascals: 610.0 * PascalsPerPsi,
            ChamberTemperatureKelvin: 3588,
            SpecificHeatRatio: 1.19,
            MolarMassKgPerMol: 0.0136,
            ExpansionRatio: 84,
            AmbientPressurePascals: 0,
            PublishedSpecificImpulseSeconds: 451.0,
            PerformanceSource: "National Academies of Sciences, Engineering, and Medicine, "
                + "A Review of United States Air Force and Department of Defense Aerospace Propulsion Needs (2006), "
                + "Appendix D, https://www.nationalacademies.org/read/11780/chapter/13 . "
                + "Table D-4 lists nozzle area ratio 84:1, vacuum specific impulse 451 s, and vacuum thrust "
                + "22,300 lb for the RL10A-4-2. The paragraph introducing that table states a chamber pressure "
                + "of 610 psi. No throat area is on the table, so thrust is not checked. Hydrogen gamma, molar "
                + "mass, and chamber temperature are the same literature assumptions as the RS-25 points, not "
                + "measurements from this table."),
        new(
            Id: "rl10b-2-vacuum",
            Label: "RL10B-2, vacuum",
            ChamberPressurePascals: 644.0 * PascalsPerPsi,
            ChamberTemperatureKelvin: 3588,
            SpecificHeatRatio: 1.19,
            MolarMassKgPerMol: 0.0136,
            ExpansionRatio: 285,
            AmbientPressurePascals: 0,
            PublishedSpecificImpulseSeconds: 466.5,
            PerformanceSource: "Same National Academies appendix, "
                + "https://www.nationalacademies.org/read/11780/chapter/13 . "
                + "Table D-2's RL-10 B-2 column lists vacuum thrust 24,750 lb, chamber pressure 644 psia, "
                + "expansion ratio 285:1, and specific impulse 466.5 s together. The prose on that page states "
                + "633 psi and 465.5 s; those figures are not this column and are not mixed in. No throat area "
                + "is in the column, so thrust is not checked. Hydrogen gamma, molar mass, and chamber "
                + "temperature are the same literature assumptions as the RS-25 points.")
    ];

    public static readonly ThrustRatioPoint Rs25ThrustRatio = new(
        Vacuum: SpecificImpulseById("rs-25-vacuum"),
        SeaLevel: SpecificImpulseById("rs-25-sea-level"),
        PublishedVacuumThrustPounds: 512_300,
        PublishedSeaLevelThrustPounds: 418_000,
        Source: "L3Harris RS-25 specification sheet, July 2024: vacuum thrust 512,300 lb and sea-level thrust "
            + "418,000 lb at 109 percent power. NASA's SLS reference guide records the same pair "
            + "(https://www.nasa.gov/wp-content/uploads/2022/03/sls-reference-guide-2022-v2-508-0.pdf). "
            + "The ratio does not need a throat area. No published throat diameter from these sheets is "
            + "used, because a commonly repeated 10.3 inch throat makes ideal thrust fall below the "
            + "published thrust and is not adopted here.");

    public static SpecificImpulsePoint SpecificImpulseById(string id) =>
        SpecificImpulse.Single(point => point.Id == id);

    public static EngineOperatingPoint OperatingPoint(SpecificImpulsePoint point, double throatArea) => new()
    {
        ChamberPressure = point.ChamberPressurePascals,
        ChamberTemperature = point.ChamberTemperatureKelvin,
        SpecificHeatRatio = point.SpecificHeatRatio,
        MolarMass = point.MolarMassKgPerMol,
        ExpansionRatio = point.ExpansionRatio,
        ThroatArea = throatArea,
        AmbientPressure = point.AmbientPressurePascals
    };

    public sealed record SpecificImpulsePoint(
        string Id,
        string Label,
        double ChamberPressurePascals,
        double ChamberTemperatureKelvin,
        double SpecificHeatRatio,
        double MolarMassKgPerMol,
        double ExpansionRatio,
        double AmbientPressurePascals,
        double PublishedSpecificImpulseSeconds,
        string PerformanceSource);

    public sealed record ThrustRatioPoint(
        SpecificImpulsePoint Vacuum,
        SpecificImpulsePoint SeaLevel,
        double PublishedVacuumThrustPounds,
        double PublishedSeaLevelThrustPounds,
        string Source)
    {
        public double PublishedRatio => PublishedVacuumThrustPounds / PublishedSeaLevelThrustPounds;
    }
}
