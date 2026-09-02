using System;
using System.Linq;

namespace HB_NLP_Research_Lab.Certification
{
    internal enum RepositoryEvidenceKind
    {
        Design,
        Code,
        Test
    }

    /// <summary>
    /// Shared repository-tree prefixes for RTM, problem-report, and leftover verify gates.
    /// </summary>
    internal static class RepositoryEvidencePaths
    {
        public static readonly string[] DesignPrefixes = ["Docs/"];
        public static readonly string[] CodePrefixes =
        [
            "Core/", "WebAPI/", "Certification/", "Physics/", "AI/", "Models/", "Aerospace/", "Scripts/"
        ];
        public static readonly string[] TestPrefixes = ["Tests/"];

        public static string[] PrefixesFor(RepositoryEvidenceKind kind) => kind switch
        {
            RepositoryEvidenceKind.Design => DesignPrefixes,
            RepositoryEvidenceKind.Code => CodePrefixes,
            RepositoryEvidenceKind.Test => TestPrefixes,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        public static bool HasAllowedPrefix(string? path, RepositoryEvidenceKind kind)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var prefixes = PrefixesFor(kind);
            return prefixes.Any(prefix =>
                path.StartsWith(prefix, StringComparison.Ordinal) &&
                path.Length > prefix.Length);
        }

        /// <summary>
        /// Leftover verify: reject traversal/absolute paths and require an allowed tree prefix.
        /// </summary>
        public static bool HasSafeRepositoryPath(string? path, RepositoryEvidenceKind kind)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var normalized = path.Trim().Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal)
                || normalized.StartsWith("//", StringComparison.Ordinal)
                || normalized.Contains("://", StringComparison.Ordinal)
                || normalized.Contains(':', StringComparison.Ordinal))
            {
                return false;
            }

            var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2 || segments.Any(segment => segment is "." or ".."))
            {
                return false;
            }

            var canonical = string.Join("/", segments);
            return HasAllowedPrefix(canonical, kind);
        }
    }
}
