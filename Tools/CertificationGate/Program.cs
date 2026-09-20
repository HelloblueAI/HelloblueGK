using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using HB_NLP_Research_Lab.Certification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HelloblueGK.Tools.CertificationGate;

/// <summary>
/// Runs the DO-178C Level A coverage gates against the declared certification boundary
/// using real measured coverage, and fails the build when the boundary stops being met.
///
/// The certification systems are otherwise exercised only by unit tests with synthetic
/// fixtures, which proves the gates work but never evaluates this project against them.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        string? coveragePath = null;
        string? boundaryPath = null;

        for (var i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--coverage":
                    coveragePath = args[i + 1];
                    break;
                case "--boundary":
                    boundaryPath = args[i + 1];
                    break;
                default:
                    break;
            }
        }

        if (coveragePath is null || boundaryPath is null)
        {
            Console.Error.WriteLine(
                "usage: CertificationGate --coverage <coverage.cobertura.xml> --boundary <certification-boundary.json>");
            return 2;
        }

        if (!File.Exists(coveragePath))
        {
            Console.Error.WriteLine($"Coverage report not found: {coveragePath}");
            return 2;
        }

        if (!File.Exists(boundaryPath))
        {
            Console.Error.WriteLine($"Boundary artifact not found: {boundaryPath}");
            return 2;
        }

        var boundary = ReadBoundary(boundaryPath);
        if (boundary is null || boundary.Units.Count == 0)
        {
            Console.Error.WriteLine("Boundary artifact declares no units. Refusing to report compliance.");
            return 1;
        }

        var measured = ReadCobertura(coveragePath);

        Console.WriteLine($"Certification boundary: {boundary.BoundaryName}");
        Console.WriteLine($"Declared units: {boundary.Units.Count}");
        Console.WriteLine($"Files in coverage report: {measured.Count}");
        Console.WriteLine();

        var options = new DbContextOptionsBuilder<TestCoverageDbContext>()
            .UseSqlite($"Data Source=file:certgate-{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;

        await using var context = new TestCoverageDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();

        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        foreach (var unit in boundary.Units)
        {
            if (!measured.TryGetValue(unit.FilePath, out var file))
            {
                Console.Error.WriteLine(
                    $"FAIL {unit.FilePath}: declared in the boundary but absent from the coverage report.");
                return 1;
            }

            var pairs = unit.McdcAnalysis?.Pairs ?? new List<BoundaryPair>();

            // Unreachable outcomes leave the denominator with a recorded justification
            // instead of being counted as demonstrated.
            var totalPairs = pairs.Count(p => !IsInfeasible(p));
            var demonstratedPairs = pairs.Count(IsDemonstrated);
            var infeasiblePairs = pairs.Count(IsInfeasible);

            if (pairs.Any(p => IsInfeasible(p) && (p.Justification is null || p.Justification.Count == 0)))
            {
                Console.Error.WriteLine(
                    $"FAIL {unit.FilePath}: an infeasible MC/DC outcome is excluded without a justification.");
                return 1;
            }

            await system.RegisterRequiredFileAsync(unit.FilePath, unit.IsSafetyCritical, "certification-gate");

            await system.RecordCoverageAsync(unit.FilePath, new CoverageMetrics
            {
                TotalStatements = file.TotalLines,
                CoveredStatements = file.CoveredLines,
                TotalBranches = file.TotalDecisions,
                CoveredBranches = file.CoveredDecisions,
                TotalConditions = file.TotalOutcomes,
                CoveredConditions = file.CoveredOutcomes,
                TotalMcdcPairs = totalPairs,
                CoveredMcdcPairs = demonstratedPairs
            });

            await system.MarkAsSafetyCriticalAsync(unit.FilePath, unit.IsSafetyCritical);

            foreach (var evidence in unit.TestEvidence)
            {
                if (!Enum.TryParse<CoverageType>(evidence.CoverageType, ignoreCase: true, out var coverageType))
                {
                    Console.Error.WriteLine(
                        $"FAIL {unit.FilePath}: unknown coverage type '{evidence.CoverageType}'.");
                    return 1;
                }

                await system.LinkTestCaseAsync(
                    unit.FilePath, evidence.TestCaseId, evidence.TestFile, coverageType);
            }

            Console.WriteLine($"{unit.FilePath}{(unit.IsSafetyCritical ? " [safety-critical]" : string.Empty)}");
            Console.WriteLine(
                $"  statements {file.CoveredLines}/{file.TotalLines}" +
                $"   decisions {file.CoveredDecisions}/{file.TotalDecisions}" +
                $"   outcomes {file.CoveredOutcomes}/{file.TotalOutcomes}");
            Console.WriteLine(
                $"  MC/DC pairs {demonstratedPairs}/{totalPairs} demonstrated" +
                $"   ({infeasiblePairs} excluded as infeasible with justification)");
        }

        Console.WriteLine();

        var check = await system.VerifyComplianceAsync();

        Console.WriteLine("DO-178C Level A verification");
        Console.WriteLine($"  files in scope                  {check.TotalFiles}");
        Console.WriteLine($"  safety-critical files           {check.SafetyCriticalFiles}");
        Console.WriteLine($"  100% statement coverage         {check.FilesWith100PercentStatementCoverage}/{check.TotalFiles}");
        Console.WriteLine($"  100% branch coverage            {check.FilesWith100PercentBranchCoverage}/{check.TotalFiles}");
        Console.WriteLine($"  MC/DC on safety-critical files  {check.SafetyCriticalFilesWithMCDC}/{check.SafetyCriticalFiles}");
        Console.WriteLine($"  files with test evidence        {check.FilesWithTestEvidence}/{check.TotalFiles}");
        Console.WriteLine($"  MC/DC test evidence             {check.SafetyCriticalFilesWithMcdcTestEvidence}/{check.SafetyCriticalFiles}");
        Console.WriteLine();

        if (check.Issues.Count > 0)
        {
            Console.WriteLine("Issues:");
            foreach (var issue in check.Issues)
            {
                Console.WriteLine($"  - {issue}");
            }

            Console.WriteLine();
        }

        if (!check.IsCompliant)
        {
            Console.Error.WriteLine(
                "FAIL: the declared certification boundary no longer meets DO-178C Level A.");
            return 1;
        }

        Console.WriteLine("PASS: every unit in the declared certification boundary meets DO-178C Level A.");
        return 0;
    }

    private static bool IsDemonstrated(BoundaryPair pair) =>
        string.Equals(pair.Status, "demonstrated", StringComparison.OrdinalIgnoreCase);

    private static bool IsInfeasible(BoundaryPair pair) =>
        string.Equals(pair.Status, "infeasible", StringComparison.OrdinalIgnoreCase);

    private static Boundary? ReadBoundary(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Boundary>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        });
    }

    /// <summary>
    /// Aggregates a cobertura report per source file. A file can appear under several
    /// classes, and the same line can appear more than once, so lines are merged by
    /// number taking the best result — otherwise a partially covered duplicate would
    /// deflate the totals and a duplicate would inflate them.
    /// </summary>
    private static Dictionary<string, MeasuredFile> ReadCobertura(string path)
    {
        var lines = new Dictionary<string, Dictionary<int, (int Hits, int Covered, int Total)>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var cls in XDocument.Load(path).Descendants("class"))
        {
            var fileName = ((string?)cls.Attribute("filename"))?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            if (!lines.TryGetValue(fileName, out var byNumber))
            {
                byNumber = new Dictionary<int, (int, int, int)>();
                lines[fileName] = byNumber;
            }

            foreach (var line in cls.Descendants("line"))
            {
                if (!int.TryParse((string?)line.Attribute("number"), CultureInfo.InvariantCulture, out var number))
                    continue;

                _ = int.TryParse((string?)line.Attribute("hits"), CultureInfo.InvariantCulture, out var hits);
                var (covered, total) = ParseConditionCoverage((string?)line.Attribute("condition-coverage"));

                if (byNumber.TryGetValue(number, out var existing))
                {
                    byNumber[number] = (
                        Math.Max(existing.Hits, hits),
                        Math.Max(existing.Covered, covered),
                        Math.Max(existing.Total, total));
                }
                else
                {
                    byNumber[number] = (hits, covered, total);
                }
            }
        }

        var result = new Dictionary<string, MeasuredFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var (fileName, byNumber) in lines)
        {
            var measured = new MeasuredFile
            {
                TotalLines = byNumber.Count,
                CoveredLines = byNumber.Values.Count(v => v.Hits > 0)
            };

            foreach (var value in byNumber.Values.Where(v => v.Total > 0))
            {
                measured.TotalDecisions++;
                measured.TotalOutcomes += value.Total;
                measured.CoveredOutcomes += value.Covered;
                if (value.Covered >= value.Total)
                    measured.CoveredDecisions++;
            }

            result[fileName] = measured;
        }

        return result;
    }

    private static (int Covered, int Total) ParseConditionCoverage(string? value)
    {
        // Cobertura formats this as "100% (2/2)".
        if (string.IsNullOrWhiteSpace(value))
            return (0, 0);

        var open = value.IndexOf('(', StringComparison.Ordinal);
        var close = value.IndexOf(')', StringComparison.Ordinal);
        if (open < 0 || close <= open)
            return (0, 0);

        var parts = value[(open + 1)..close].Split('/');
        if (parts.Length != 2)
            return (0, 0);

        if (!int.TryParse(parts[0], CultureInfo.InvariantCulture, out var covered)
            || !int.TryParse(parts[1], CultureInfo.InvariantCulture, out var total))
        {
            return (0, 0);
        }

        return (covered, total);
    }

    private sealed class MeasuredFile
    {
        public int TotalLines { get; set; }
        public int CoveredLines { get; set; }
        public int TotalDecisions { get; set; }
        public int CoveredDecisions { get; set; }
        public int TotalOutcomes { get; set; }
        public int CoveredOutcomes { get; set; }
    }

    private sealed class Boundary
    {
        public string BoundaryName { get; set; } = string.Empty;
        public List<BoundaryUnit> Units { get; set; } = new();
    }

    private sealed class BoundaryUnit
    {
        public string FilePath { get; set; } = string.Empty;
        public bool IsSafetyCritical { get; set; }
        public List<BoundaryEvidence> TestEvidence { get; set; } = new();
        public BoundaryMcdcAnalysis? McdcAnalysis { get; set; }
    }

    private sealed class BoundaryEvidence
    {
        public string TestCaseId { get; set; } = string.Empty;
        public string TestFile { get; set; } = string.Empty;
        public string CoverageType { get; set; } = string.Empty;
    }

    private sealed class BoundaryMcdcAnalysis
    {
        public List<BoundaryPair> Pairs { get; set; } = new();
    }

    private sealed class BoundaryPair
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<string>? Justification { get; set; }
    }
}
