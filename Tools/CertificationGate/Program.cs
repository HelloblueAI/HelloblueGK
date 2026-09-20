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
        string? testResultsPath = null;
        var repoRoot = Directory.GetCurrentDirectory();

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
                case "--test-results":
                    testResultsPath = args[i + 1];
                    break;
                case "--repo-root":
                    repoRoot = args[i + 1];
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
            if (unit.Limitations.Count > 0)
            {
                // Surfaced on every run so a passing gate is never read as a broader claim
                // than it makes.
                Console.WriteLine("  recorded limitations:");
                foreach (var limitation in unit.Limitations)
                {
                    Console.WriteLine($"    {limitation}");
                }
            }

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

        var traceabilityOk = await VerifyRequirementsAsync(boundary, repoRoot, testResultsPath);

        if (!check.IsCompliant)
        {
            Console.Error.WriteLine(
                "FAIL: the declared certification boundary no longer meets DO-178C Level A coverage objectives.");
        }

        if (!check.IsCompliant || !traceabilityOk)
        {
            return 1;
        }

        Console.WriteLine(
            "PASS: every unit in the declared boundary meets the DO-178C Level A coverage and");
        Console.WriteLine(
            "traceability objectives. This is not a validation claim — see claimScope and the");
        Console.WriteLine("recorded limitations in the boundary artifact for what is out of scope.");
        return 0;
    }

    /// <summary>
    /// Runs the requirements traceability gates over the declared requirements.
    ///
    /// A link is only marked verified when this tool can confirm the evidence exists:
    /// the design document contains the design element, the code file contains the named
    /// function within the recorded line range, and the test method is recorded as passed
    /// in this run's test results. Rubber-stamping every link would make the gate vacuous,
    /// which is the failure mode the whole boundary exists to avoid.
    /// </summary>
    private static async Task<bool> VerifyRequirementsAsync(
        Boundary boundary,
        string repoRoot,
        string? testResultsPath)
    {
        var requirements = boundary.Units.SelectMany(u => u.Requirements).ToList();
        if (requirements.Count == 0)
        {
            return true;
        }

        Console.WriteLine("Requirements traceability");

        if (testResultsPath is null || !File.Exists(testResultsPath))
        {
            Console.Error.WriteLine(
                "  FAIL: requirements are declared but no test results were supplied, so test links cannot be verified.");
            return false;
        }

        var passedTests = ReadPassedTests(testResultsPath);
        Console.WriteLine($"  passing tests in this run: {passedTests.Count}");

        var options = new DbContextOptionsBuilder<RequirementsDbContext>()
            .UseSqlite($"Data Source=file:certgate-rtm-{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;

        await using var context = new RequirementsDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();

        var system = new RequirementsTraceabilitySystem(
            context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var evidenceMissing = false;

        foreach (var declared in requirements)
        {
            if (!Enum.TryParse<RequirementPriority>(declared.Priority, ignoreCase: true, out var priority))
            {
                Console.Error.WriteLine(
                    $"  FAIL {declared.RequirementNumber}: unknown priority '{declared.Priority}'.");
                return false;
            }

            if (!Enum.TryParse<TestCoverageType>(declared.CoverageType, ignoreCase: true, out var coverageType))
            {
                Console.Error.WriteLine(
                    $"  FAIL {declared.RequirementNumber}: unknown coverage type '{declared.CoverageType}'.");
                return false;
            }

            Requirement requirement;
            RequirementDesignLink designLink;
            RequirementCodeLink codeLink;
            RequirementTestLink testLink;

            // The traceability system rejects placeholder identities and evidence paths
            // outside the allowed prefixes. Report that as a gate failure rather than an
            // unhandled exception, since a malformed artifact is a finding, not a crash.
            try
            {
                requirement = await system.CreateRequirementAsync(new Requirement
                {
                    RequirementNumber = declared.RequirementNumber,
                    Title = declared.Title,
                    Description = declared.Description,
                    Priority = priority,
                    Status = RequirementStatus.Implemented,
                    CreatedBy = "certification-gate"
                });

                designLink = await system.LinkToDesignAsync(
                    requirement.Id, declared.DesignElementId, declared.DesignDocument);
                codeLink = await system.LinkToCodeAsync(
                    requirement.Id, declared.CodeFile, declared.LineStart, declared.LineEnd, declared.FunctionName);
                testLink = await system.LinkToTestAsync(
                    requirement.Id, declared.TestCaseId, declared.TestFile, coverageType);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(
                    $"  FAIL {declared.RequirementNumber}: rejected by the traceability system — {ex.Message}");
                return false;
            }

            var designPresent = FileContains(repoRoot, declared.DesignDocument, declared.DesignElementId);
            var codePresent = CodeEvidencePresent(repoRoot, declared);
            var testPresent = passedTests.Contains(declared.TestMethod)
                              && FileContains(repoRoot, declared.TestFile, declared.TestMethod);

            if (designPresent)
                await system.VerifyLinkAsync(requirement.Id, designLink.Id, RequirementLinkKind.Design);
            if (codePresent)
                await system.VerifyLinkAsync(requirement.Id, codeLink.Id, RequirementLinkKind.Code);
            if (testPresent)
            {
                // The result has to be recorded first: the system refuses to verify a test
                // link that has not passed, which is the ordering a real process implies.
                await system.RecordTestResultAsync(requirement.Id, testLink.Id, TestResult.Passed);
                await system.VerifyLinkAsync(requirement.Id, testLink.Id, RequirementLinkKind.Test);
            }

            Console.WriteLine(
                $"  {declared.RequirementNumber}  design {Mark(designPresent)}" +
                $"  code {Mark(codePresent)}  test {Mark(testPresent)}");

            if (!designPresent)
                Console.Error.WriteLine(
                    $"    {declared.DesignElementId} not found in {declared.DesignDocument}");
            if (!codePresent)
                Console.Error.WriteLine(
                    $"    {declared.FunctionName} not found in {declared.CodeFile} within lines {declared.LineStart}-{declared.LineEnd}");
            if (!testPresent)
                Console.Error.WriteLine(
                    $"    {declared.TestMethod} did not pass in this run, or is absent from {declared.TestFile}");

            evidenceMissing |= !designPresent || !codePresent || !testPresent;
        }

        var report = await system.VerifyTraceabilityAsync();

        Console.WriteLine($"  requirements          {report.TotalRequirements}");
        Console.WriteLine($"  issues                {report.IssuesFound} ({report.CriticalIssues} critical)");
        Console.WriteLine();

        foreach (var issue in report.Issues)
        {
            Console.WriteLine($"  - [{issue.Severity}] {issue.RequirementNumber}: {issue.Description}");
        }

        if (!report.IsCompliant || evidenceMissing)
        {
            Console.Error.WriteLine("FAIL: declared requirements are not fully traced to verified evidence.");
            return false;
        }

        return true;
    }

    private static string Mark(bool ok) => ok ? "ok" : "MISSING";

    /// <summary>
    /// Confirms the named function appears inside the recorded line range. A line range
    /// that has drifted away from the function it claims to describe is stale trace data,
    /// so it is treated as missing evidence rather than quietly accepted.
    /// </summary>
    private static bool CodeEvidencePresent(string repoRoot, BoundaryRequirement declared)
    {
        var path = Path.Combine(repoRoot, declared.CodeFile);
        if (!File.Exists(path))
            return false;

        var lines = File.ReadAllLines(path);
        if (declared.LineStart < 1 || declared.LineEnd > lines.Length || declared.LineStart > declared.LineEnd)
            return false;

        for (var i = declared.LineStart - 1; i < declared.LineEnd; i++)
        {
            if (lines[i].Contains(declared.FunctionName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool FileContains(string repoRoot, string relativePath, string needle)
    {
        var path = Path.Combine(repoRoot, relativePath);
        return File.Exists(path)
               && File.ReadAllText(path).Contains(needle, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reads the passing test method names from a VSTest trx file. Test links are only
    /// verified against tests that actually ran and passed in this build.
    /// </summary>
    private static HashSet<string> ReadPassedTests(string trxPath)
    {
        var passed = new HashSet<string>(StringComparer.Ordinal);
        var ns = XNamespace.Get("http://microsoft.com/schemas/VisualStudio/TeamTest/2010");

        foreach (var result in XDocument.Load(trxPath).Descendants(ns + "UnitTestResult"))
        {
            if (!string.Equals((string?)result.Attribute("outcome"), "Passed", StringComparison.Ordinal))
                continue;

            var testName = (string?)result.Attribute("testName");
            if (string.IsNullOrWhiteSpace(testName))
                continue;

            // trx records the fully qualified name, sometimes with a data-driven suffix.
            var withoutArguments = testName.Split('(')[0];
            passed.Add(withoutArguments[(withoutArguments.LastIndexOf('.') + 1)..]);
        }

        return passed;
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
        public List<string> Limitations { get; set; } = new();
        public List<BoundaryRequirement> Requirements { get; set; } = new();
    }

    private sealed class BoundaryRequirement
    {
        public string RequirementNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string DesignElementId { get; set; } = string.Empty;
        public string DesignDocument { get; set; } = string.Empty;
        public string CodeFile { get; set; } = string.Empty;
        public string FunctionName { get; set; } = string.Empty;
        public int LineStart { get; set; }
        public int LineEnd { get; set; }
        public string TestCaseId { get; set; } = string.Empty;
        public string TestFile { get; set; } = string.Empty;
        public string TestMethod { get; set; } = string.Empty;
        public string CoverageType { get; set; } = string.Empty;
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
