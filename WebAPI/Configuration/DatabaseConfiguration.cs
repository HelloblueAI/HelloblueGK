using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace HB_NLP_Research_Lab.WebAPI.Configuration;

public enum DatabaseProvider
{
    PostgreSql,
    SqlServer,
    Sqlite
}

public sealed record DatabaseConnectionSettings(string ConnectionString, DatabaseProvider Provider);

public static class DatabaseConfiguration
{
    public static DatabaseConnectionSettings Resolve(
        IConfiguration configuration,
        IHostEnvironment environment,
        Func<string, string?>? getEnvironmentVariable = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (environment.IsDevelopment())
            {
                connectionString = "Data Source=hellobluegk.db";
            }
            else
            {
                var databaseUrl = getEnvironmentVariable("DATABASE_URL");
                if (string.IsNullOrWhiteSpace(databaseUrl))
                {
                    throw new InvalidOperationException(
                        "DefaultConnection string must be configured in production. " +
                        "Please set ConnectionStrings:DefaultConnection or DATABASE_URL in your configuration. " +
                        "For SQL Server, use: Server=your-server;Database=HelloblueGK;...");
                }

                connectionString = ConvertDatabaseUrlToConnectionString(databaseUrl);
            }
        }

        var configuredProvider = configuration["Database:Provider"];
        return new DatabaseConnectionSettings(connectionString, DetectProvider(connectionString, configuredProvider));
    }

    public static DatabaseProvider DetectProvider(string connectionString, string? configuredProvider = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredProvider))
        {
            return configuredProvider.Trim().ToLowerInvariant() switch
            {
                "postgres" or "postgresql" or "npgsql" => DatabaseProvider.PostgreSql,
                "sqlserver" or "mssql" or "microsoftsqlserver" => DatabaseProvider.SqlServer,
                "sqlite" => DatabaseProvider.Sqlite,
                _ => throw new InvalidOperationException(
                    "Unsupported Database:Provider value. Use PostgreSql, SqlServer, or Sqlite.")
            };
        }

        var keywords = ParseConnectionStringKeywords(connectionString);

        if (IsSqliteConnectionString(connectionString, keywords))
        {
            return DatabaseProvider.Sqlite;
        }

        if (HasAnyKeyword(keywords, "Host", "Username", "Search Path", "SSL Mode", "Include Error Detail"))
        {
            return DatabaseProvider.PostgreSql;
        }

        if (HasAnyKeyword(
            keywords,
            "Server",
            "Data Source",
            "Address",
            "Addr",
            "Network Address",
            "Initial Catalog",
            "Integrated Security",
            "Trusted_Connection",
            "Trust Server Certificate",
            "MultipleActiveResultSets",
            "Encrypt"))
        {
            return DatabaseProvider.SqlServer;
        }

        return DatabaseProvider.SqlServer;
    }

    public static string ConvertDatabaseUrlToConnectionString(string databaseUrl)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            throw new InvalidOperationException("DATABASE_URL is empty.");
        }

        var schemeSeparatorIndex = databaseUrl.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparatorIndex <= 0)
        {
            throw new InvalidOperationException("DATABASE_URL must include a PostgreSQL URI scheme.");
        }

        var scheme = databaseUrl[..schemeSeparatorIndex].ToLowerInvariant();
        if (scheme is not ("postgres" or "postgresql"))
        {
            throw new InvalidOperationException("DATABASE_URL must use the postgres:// or postgresql:// scheme.");
        }

        var remainder = databaseUrl[(schemeSeparatorIndex + 3)..];
        var authorityEndIndex = IndexOfAny(remainder, '/', '?', '#');
        var authority = authorityEndIndex >= 0 ? remainder[..authorityEndIndex] : remainder;
        var pathAndQuery = authorityEndIndex >= 0 ? remainder[authorityEndIndex..] : string.Empty;

        var userInfoSeparatorIndex = authority.LastIndexOf('@');
        if (userInfoSeparatorIndex <= 0 || userInfoSeparatorIndex == authority.Length - 1)
        {
            throw new InvalidOperationException("DATABASE_URL must include user info and host.");
        }

        var userInfo = authority[..userInfoSeparatorIndex];
        var hostAndPort = authority[(userInfoSeparatorIndex + 1)..];
        var passwordSeparatorIndex = userInfo.IndexOf(':');
        var username = Uri.UnescapeDataString(passwordSeparatorIndex >= 0 ? userInfo[..passwordSeparatorIndex] : userInfo);
        var password = passwordSeparatorIndex >= 0
            ? Uri.UnescapeDataString(userInfo[(passwordSeparatorIndex + 1)..])
            : string.Empty;

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException("DATABASE_URL must include a username.");
        }

        var sanitizedUri = new Uri($"{scheme}://{hostAndPort}{pathAndQuery}");
        var database = Uri.UnescapeDataString(sanitizedUri.AbsolutePath.TrimStart('/'));
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new InvalidOperationException("DATABASE_URL must include a database name.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = sanitizedUri.Host,
            Database = database,
            Username = username
        };

        if (sanitizedUri.Port > 0)
        {
            builder.Port = sanitizedUri.Port;
        }

        if (!string.IsNullOrEmpty(password))
        {
            builder.Password = password;
        }

        ApplyPostgresQueryParameters(builder, sanitizedUri.Query);

        return builder.ConnectionString;
    }

    public static string AddPostgresApplicationName(string connectionString, string applicationName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.ApplicationName))
        {
            builder.ApplicationName = applicationName;
        }

        return builder.ConnectionString;
    }

    private static Dictionary<string, string?> ParseConnectionStringKeywords(string connectionString)
    {
        var builder = new DbConnectionStringBuilder();
        builder.ConnectionString = connectionString;

        return builder
            .Cast<KeyValuePair<string, object?>>()
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value?.ToString(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsSqliteConnectionString(
        string connectionString,
        IReadOnlyDictionary<string, string?> keywords)
    {
        if (connectionString.StartsWith("Filename=", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!keywords.TryGetValue("Data Source", out var dataSource) &&
            !keywords.TryGetValue("Filename", out dataSource))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(dataSource) &&
               (dataSource.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
                dataSource.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase) ||
                dataSource.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasAnyKeyword(IReadOnlyDictionary<string, string?> keywords, params string[] keys)
    {
        return keys.Any(keywords.ContainsKey);
    }

    private static int IndexOfAny(string value, params char[] candidates)
    {
        var indexes = candidates
            .Select(candidate => value.IndexOf(candidate))
            .Where(index => index >= 0)
            .ToArray();

        return indexes.Length == 0 ? -1 : indexes.Min();
    }

    private static void ApplyPostgresQueryParameters(NpgsqlConnectionStringBuilder builder, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        foreach (var parameter in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = parameter.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]).Replace("_", " ", StringComparison.Ordinal);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;

            if (!string.IsNullOrWhiteSpace(key))
            {
                builder[key] = value;
            }
        }
    }
}
