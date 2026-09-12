namespace HB_NLP_Research_Lab.Certification
{
    /// <summary>
    /// Shared placeholder-token checks for certification identity fields (function names,
    /// configuration metadata, etc.). Empty/whitespace is handled separately at each gate.
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

        public static bool HasRealIdentity(string? value) =>
            !string.IsNullOrWhiteSpace(value) && !IsPlaceholder(value);

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
