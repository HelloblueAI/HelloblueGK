using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Data.Common;
using System.Linq;
using HB_NLP_Research_Lab.Certification;

namespace HB_NLP_Research_Lab.WebAPI.Scripts;

/// <summary>
/// Script to ensure all certification database tables are created
/// This is needed because EnsureCreated() doesn't work reliably with multiple DbContexts
/// </summary>
public static class CertificationDatabaseInitializer
{
    public static async Task InitializeAllAsync(
        RequirementsDbContext requirementsContext,
        ProblemReportDbContext problemReportContext,
        ConfigurationDbContext configurationContext,
        TestCoverageDbContext testCoverageContext,
        CodeReviewDbContext codeReviewContext,
        ILogger logger)
    {
        try
        {
            // For each context, ensure tables exist by trying to query them
            // This will create tables if they don't exist
            
            logger.LogInformation("Initializing RequirementsDbContext tables...");
            await EnsureTablesExistAsync(requirementsContext, logger);
            
            logger.LogInformation("Initializing ProblemReportDbContext tables...");
            await EnsureTablesExistAsync(problemReportContext, logger);
            
            logger.LogInformation("Initializing ConfigurationDbContext tables...");
            await EnsureTablesExistAsync(configurationContext, logger);
            
            logger.LogInformation("Initializing TestCoverageDbContext tables...");
            await EnsureTablesExistAsync(testCoverageContext, logger);
            
            logger.LogInformation("Initializing CodeReviewDbContext tables...");
            await EnsureTablesExistAsync(codeReviewContext, logger);
            await EnsureReviewFindingDispositionColumnsAsync(codeReviewContext, logger);
            
            logger.LogInformation("All certification database tables initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize certification databases");
            throw;
        }
    }
    
    private static async Task EnsureTablesExistAsync(DbContext context, ILogger logger)
    {
        var contextName = context.GetType().Name;

        try
        {
            // First ensure database exists
            var canConnect = await context.Database.CanConnectAsync();
            if (!canConnect)
            {
                logger.LogWarning("Cannot connect to database. Attempting to create...");
                var created = await context.Database.EnsureCreatedAsync();
                logger.LogInformation("Database created: {Created}", created);
                return;
            }

            // Every table in the model is probed, not just the first one. Probing a single table
            // hid tables added to a context after its schema was first created: the probe passed
            // on the pre-existing table, so the missing ones were never created and every call
            // that touched them failed at runtime.
            var missing = new List<TableName>();
            foreach (var table in ModelTables(context))
            {
                if (!await TableExistsAsync(context, table))
                {
                    missing.Add(table);
                }
            }

            if (missing.Count == 0)
            {
                logger.LogInformation("All {Count} tables for {ContextType} exist", ModelTables(context).Count, contextName);
            }
            else
            {
                logger.LogInformation(
                    "Missing tables for {ContextType}: {Tables}. Creating...",
                    contextName,
                    string.Join(", ", missing));

                // EnsureCreated returns false when the database already exists, even if this
                // context's tables are absent, which is the normal case when several contexts
                // share one database.
                if (await context.Database.EnsureCreatedAsync())
                {
                    logger.LogInformation("Schema for {ContextType} created", contextName);
                }
                else
                {
                    await CreateMissingTablesAsync(context, logger);
                }

                var stillMissing = new List<TableName>();
                foreach (var table in missing)
                {
                    if (!await TableExistsAsync(context, table))
                    {
                        stillMissing.Add(table);
                    }
                }

                if (stillMissing.Count > 0)
                {
                    logger.LogError(
                        "Tables still missing for {ContextType} after creation: {Tables}",
                        contextName,
                        string.Join(", ", stillMissing));
                }
                else
                {
                    logger.LogInformation("Created {Count} missing tables for {ContextType}", missing.Count, contextName);
                }
            }

            // Verify connection works
            await context.Database.ExecuteSqlRawAsync("SELECT 1");
            logger.LogInformation("Database connection verified");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ensuring tables exist for {ContextType}: {Error}", contextName, ex.Message);
            // Don't throw - allow application to continue
        }
    }

    /// <summary>
    /// The generated create script covers the whole context, so it necessarily includes tables
    /// that are already present. PostgreSQL aborts an entire batch on the first duplicate, which
    /// is why running the script as one statement created nothing. Each statement therefore runs
    /// on its own, and one that reports an object already exists is skipped rather than fatal.
    /// </summary>
    private static async Task CreateMissingTablesAsync(DbContext context, ILogger logger)
    {
        var script = context.Database.GenerateCreateScript();
        if (string.IsNullOrWhiteSpace(script))
        {
            return;
        }

        var applied = 0;
        var alreadyPresent = 0;

        foreach (var statement in SplitStatements(script))
        {
            try
            {
                await context.Database.ExecuteSqlRawAsync(statement);
                applied++;
            }
            catch (Exception ex) when (DescribesExistingObject(ex))
            {
                alreadyPresent++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "DDL statement failed for {ContextType}: {Statement}",
                    context.GetType().Name,
                    Excerpt(statement));
            }
        }

        logger.LogInformation(
            "Applied {Applied} DDL statements for {ContextType}; {AlreadyPresent} objects already existed",
            applied,
            context.GetType().Name,
            alreadyPresent);
    }

    private static List<TableName> ModelTables(DbContext context) =>
        context.Model.GetEntityTypes()
            .Select(entity => new TableName(entity.GetSchema(), entity.GetTableName()))
            .Where(table => !string.IsNullOrEmpty(table.Table))
            .Distinct()
            .ToList();

    private static async Task<bool> TableExistsAsync(DbContext context, TableName table)
    {
        // A table identifier cannot be parameterised. The name comes from the EF model rather than
        // from a request, and Quoted() escapes embedded quotes.
        var sql = "SELECT COUNT(*) FROM " + table.Quoted();

        try
        {
            await context.Database.ExecuteSqlRawAsync(sql);
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    private static IEnumerable<string> SplitStatements(string script)
    {
        foreach (var candidate in System.Text.RegularExpressions.Regex.Split(script, @";[ \t]*(?:\r?\n|$)"))
        {
            var statement = candidate.Trim();
            if (statement.Length > 0)
            {
                yield return statement;
            }
        }
    }

    private static bool DescribesExistingObject(Exception exception)
    {
        for (Exception? ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Excerpt(string statement)
    {
        var singleLine = statement.ReplaceLineEndings(" ");
        return singleLine.Length <= 120 ? singleLine : singleLine[..120] + "...";
    }

    private readonly record struct TableName(string? Schema, string? Table)
    {
        public string Quoted() =>
            Schema is null
                ? $"\"{Escape(Table!)}\""
                : $"\"{Escape(Schema)}\".\"{Escape(Table!)}\"";

        public override string ToString() => Schema is null ? Table! : $"{Schema}.{Table}";

        private static string Escape(string identifier) => identifier.Replace("\"", "\"\"", StringComparison.Ordinal);
    }

    /// <summary>
    /// Additive columns for finding disposition notes on legacy EnsureCreated schemas.
    /// </summary>
    private static async Task EnsureReviewFindingDispositionColumnsAsync(
        CodeReviewDbContext context,
        ILogger logger)
    {
        try
        {
            if (context.Database.IsSqlite())
            {
                if (!await SqliteColumnExistsAsync(context, "ReviewFindings", "ResolvedBy"))
                {
                    await context.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "ReviewFindings" ADD COLUMN "ResolvedBy" TEXT NULL""");
                    logger.LogInformation("Added ReviewFindings.ResolvedBy column.");
                }

                if (!await SqliteColumnExistsAsync(context, "ReviewFindings", "Resolution"))
                {
                    await context.Database.ExecuteSqlRawAsync(
                        """ALTER TABLE "ReviewFindings" ADD COLUMN "Resolution" TEXT NULL""");
                    logger.LogInformation("Added ReviewFindings.Resolution column.");
                }

                return;
            }

            if (context.Database.IsNpgsql())
            {
                await context.Database.ExecuteSqlRawAsync(
                    """
                    ALTER TABLE "ReviewFindings" ADD COLUMN IF NOT EXISTS "ResolvedBy" character varying(256) NULL;
                    ALTER TABLE "ReviewFindings" ADD COLUMN IF NOT EXISTS "Resolution" text NULL;
                    """);
                logger.LogInformation("Ensured ReviewFindings disposition columns exist (PostgreSQL).");
            }
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Could not patch ReviewFindings disposition columns.");
        }
        catch (DbException ex)
        {
            logger.LogWarning(ex, "Could not patch ReviewFindings disposition columns.");
        }
    }

    private static async Task<bool> SqliteColumnExistsAsync(
        DbContext context,
        string tableName,
        string columnName)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        if (command.Connection!.State != System.Data.ConnectionState.Open)
        {
            await command.Connection.OpenAsync();
        }

        command.CommandText = $"PRAGMA table_info(\"{tableName}\")";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
