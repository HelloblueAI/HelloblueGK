using System.Text.RegularExpressions;
using System.Collections.Generic;

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

        /// <summary>
        /// Sanitizes a connection string for safe use in metrics/logging
        /// Removes sensitive information like passwords and usernames
        /// </summary>
        /// <param name="connectionString">The connection string to sanitize</param>
        /// <returns>Sanitized connection string safe for metrics</returns>
        public static string SanitizeConnectionStringForMetrics(string? connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                return "unknown";
            }

            // Remove password and sensitive information from connection string
            var sanitized = connectionString;
            
            // Remove Password=... (case insensitive)
            sanitized = Regex.Replace(sanitized, @"(?i)Password\s*=\s*[^;]+", "Password=***", RegexOptions.IgnoreCase);
            
            // Remove Pwd=... (case insensitive)
            sanitized = Regex.Replace(sanitized, @"(?i)Pwd\s*=\s*[^;]+", "Pwd=***", RegexOptions.IgnoreCase);
            
            // Remove User Id=... or Username=... (case insensitive) - keep only database and host info
            sanitized = Regex.Replace(sanitized, @"(?i)(User\s*Id|Username|UID)\s*=\s*[^;]+", "$1=***", RegexOptions.IgnoreCase);
            
            // For PostgreSQL connection strings, extract only Host, Port, Database
            if (sanitized.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            {
                var parts = sanitized.Split(';');
                var safeParts = new List<string>();
                foreach (var part in parts)
                {
                    var keyValue = part.Split('=', 2);
                    if (keyValue.Length == 2)
                    {
                        var key = keyValue[0].Trim();
                        if (key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("Port", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("Database", StringComparison.OrdinalIgnoreCase))
                        {
                            safeParts.Add(part);
                        }
                    }
                }
                sanitized = string.Join(";", safeParts);
            }
            
            // For SQL Server connection strings, extract only Server and Database
            if (sanitized.Contains("Server=", StringComparison.OrdinalIgnoreCase) && 
                !sanitized.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            {
                var parts = sanitized.Split(';');
                var safeParts = new List<string>();
                foreach (var part in parts)
                {
                    var keyValue = part.Split('=', 2);
                    if (keyValue.Length == 2)
                    {
                        var key = keyValue[0].Trim();
                        if (key.Equals("Server", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("Database", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase))
                        {
                            safeParts.Add(part);
                        }
                    }
                }
                sanitized = string.Join(";", safeParts);
            }
            
            return string.IsNullOrEmpty(sanitized) ? "unknown" : sanitized;
        }
    }
}

