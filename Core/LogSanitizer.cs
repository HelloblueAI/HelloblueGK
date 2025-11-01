using System.Text.RegularExpressions;

namespace HB_NLP_Research_Lab.Core
{
    /// <summary>
    /// Utility class for sanitizing user input before logging to prevent log injection attacks
    /// </summary>
    public static class LogSanitizer
    {
        // Remove or escape potentially dangerous characters for log injection
        private static readonly Regex DangerousCharsRegex = new Regex(@"[\r\n\t\x00-\x1F\x7F-\x9F]", RegexOptions.Compiled);
        private const int MaxLogLength = 500; // Maximum length for logged values

        /// <summary>
        /// Sanitizes a string value for safe logging
        /// </summary>
        /// <param name="value">The value to sanitize</param>
        /// <returns>Sanitized string safe for logging</returns>
        public static string Sanitize(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            // Remove dangerous control characters and newlines
            var sanitized = DangerousCharsRegex.Replace(value, string.Empty);
            
            // Truncate if too long
            if (sanitized.Length > MaxLogLength)
            {
                sanitized = sanitized.Substring(0, MaxLogLength) + "...[truncated]";
            }

            return sanitized;
        }

        /// <summary>
        /// Sanitizes a path for safe logging
        /// </summary>
        /// <param name="path">The path to sanitize</param>
        /// <returns>Sanitized path safe for logging</returns>
        public static string SanitizePath(PathString path)
        {
            return Sanitize(path.Value);
        }

        /// <summary>
        /// Sanitizes an identifier for safe logging
        /// </summary>
        /// <param name="identifier">The identifier to sanitize</param>
        /// <returns>Sanitized identifier safe for logging</returns>
        public static string SanitizeIdentifier(string? identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return "unknown";
            }

            // More strict sanitization for identifiers (alphanumeric, dash, underscore only)
            var sanitized = Regex.Replace(identifier, @"[^a-zA-Z0-9\-_]", string.Empty);
            
            if (sanitized.Length > MaxLogLength)
            {
                sanitized = sanitized.Substring(0, MaxLogLength) + "...[truncated]";
            }

            return string.IsNullOrEmpty(sanitized) ? "invalid" : sanitized;
        }
    }
}

