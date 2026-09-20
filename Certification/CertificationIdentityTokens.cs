namespace HB_NLP_Research_Lab.Certification
{
    /// <summary>
    /// Shared placeholder-token checks for certification identity fields (function names,
    /// configuration metadata, etc.). Empty/whitespace is handled separately at each gate.
    /// New leftover-identity gates should compose these predicates rather than redefining
    /// equivalent one-liners next to their own call sites.
    /// </summary>
    internal static class CertificationIdentityTokens
    {
        public static bool IsPlaceholder(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim().ToLowerInvariant();
            return normalized is
                "n/a" or "na" or "none" or "todo" or "tbd" or
                "unknown" or "pending" or "placeholder" or
                "null" or "undefined" or "system" or "anonymous";
        }

        /// <summary>
        /// Named implementation identity must contain a letter. Leftover
        /// punctuation-only / digit-only values ("...", "___", "123") previously
        /// satisfied HasRealIdentity after placeholder tokens were rejected.
        /// Matching leftover ReadCalibratedPressure / ValidateSensor still qualify.
        /// </summary>
        public static bool HasRealIdentity(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            return trimmed.Any(char.IsLetter) && !IsPlaceholder(trimmed);
        }

        /// <summary>
        /// Design/test evidence IDs must contain at least one letter.
        /// Leftover "..." / "___" / "123" previously counted as recorded
        /// inventory and stamped Level A. Matching leftover TC-SAFE / DE-SAFE
        /// still comply. Distinct from HasRealIdentity (function names) and
        /// from placeholder token lists (n/a, none, todo).
        /// </summary>
        public static bool HasAlphabeticIdentity(string? value) =>
            !string.IsNullOrWhiteSpace(value) && value.Any(char.IsLetter);

        /// <summary>
        /// SoD actor identity must contain a letter or digit. Leftover
        /// punctuation-only values ("...", "___", "---") previously satisfied
        /// leftover creator / author / approver gates after placeholder tokens
        /// were rejected. Matching leftover alice / bob / admin still qualify.
        /// Do not use this for versions (1.0.0) or hex checksums, and do not
        /// letter-gate actors — leftover alice must remain a real identity.
        /// </summary>
        public static bool HasLetterOrDigitIdentity(string? value) =>
            !string.IsNullOrWhiteSpace(value) && value.Any(char.IsLetterOrDigit);

        /// <summary>
        /// Non-empty, non-placeholder design/test identity with a letter.
        /// Function-name leftover letter gates stay on HasRealIdentity.
        /// </summary>
        public static bool HasRealEvidenceId(string? value) =>
            HasRealIdentity(value) && HasAlphabeticIdentity(value);

        /// <summary>
        /// Leftover evidence paths whose filename or directory identity is a
        /// placeholder token ("n/a", "none", "todo") — including slash-containing
        /// tokens such as Tests/n/a and Core/n/a.cs — are not repository evidence.
        /// Matching leftover Core/Sensors.cs still has real path identity.
        /// Distinct from HasAlphabeticPathIdentity (punctuation / digit-only stems).
        /// </summary>
        public static bool HasPlaceholderPathIdentity(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var normalized = path.Trim().Replace('\\', '/');
            var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return false;
            }

            for (var start = 0; start < segments.Length; start++)
            {
                for (var length = 1; start + length <= segments.Length; length++)
                {
                    var slice = string.Join('/', segments, start, length);
                    if (IsPlaceholder(slice) || IsPlaceholder(FileStem(slice)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Evidence path file identity must contain a letter. Leftover
        /// punctuation-only / digit-only file stems ("Core/....cs",
        /// "Core/___.cs", "Tests/123.cs") previously satisfied
        /// HasSafeRepositoryPath after placeholder path tokens were rejected.
        /// The allowed prefix (Core/, Tests/) always has letters and is not
        /// path identity — only the filename stem is checked. Matching leftover
        /// Core/Sensors.cs still qualifies. Leftover n/a stays on
        /// HasPlaceholderPathIdentity (n/a has letters). Do not use this for
        /// versions (1.0.0) or hex checksums.
        /// </summary>
        public static bool HasAlphabeticPathIdentity(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var normalized = path.Trim().Replace('\\', '/');
            var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return false;
            }

            return FileStem(segments[^1]).Any(char.IsLetter);
        }

        private static string FileStem(string pathSlice)
        {
            var lastDot = pathSlice.LastIndexOf('.');
            var lastSlash = pathSlice.LastIndexOf('/');
            if (lastDot <= lastSlash)
            {
                return pathSlice;
            }

            return pathSlice[..lastDot];
        }
    }
}
