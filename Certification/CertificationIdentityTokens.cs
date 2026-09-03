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
    }
}
