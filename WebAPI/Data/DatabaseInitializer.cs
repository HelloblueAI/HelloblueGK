using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace HB_NLP_Research_Lab.WebAPI.Data;

/// <summary>
/// Ensures the main application schema exists from the EF Core model.
/// Prefers Migrate() when migrations are present; otherwise creates missing tables
/// from the current model (safe replacement for EnsureCreated + ad-hoc SQL).
/// Also applies additive compatibility patches for legacy EnsureCreated databases.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        HelloblueGKDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var migrationsAssembly = dbContext.Database.GetMigrations().ToList();
        if (migrationsAssembly.Count > 0)
        {
            logger.LogInformation(
                "Applying EF Core migrations ({Count} migration(s) in assembly)...",
                migrationsAssembly.Count);
            await dbContext.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Database migrations applied successfully.");
            return;
        }

        logger.LogInformation("No EF migrations found; ensuring relational schema from the current model...");

        var creator = dbContext.GetService<IRelationalDatabaseCreator>();
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            await creator.CreateAsync(cancellationToken);
            await creator.CreateTablesAsync(cancellationToken);
            logger.LogInformation("Created database and tables from EF model.");
            return;
        }

        if (!await creator.HasTablesAsync(cancellationToken))
        {
            await creator.CreateTablesAsync(cancellationToken);
            logger.LogInformation("Created missing tables from EF model.");
            return;
        }

        // Legacy EnsureCreated databases: verify core tables; create any that are missing
        // by attempting model-driven creation when Engines is absent.
        if (!await TableSeemsPresentAsync(dbContext, cancellationToken))
        {
            try
            {
                await creator.CreateTablesAsync(cancellationToken);
                logger.LogInformation("Created tables on existing empty/partial database.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Could not create all tables automatically. Some objects may already exist.");
            }
        }
        else
        {
            logger.LogInformation("Database schema already present.");
        }

        // Additive patches for columns/indexes introduced after the initial EnsureCreated schema.
        await EnsureSchemaCompatibilityAsync(dbContext, logger, cancellationToken);
    }

    /// <summary>
    /// Adds refresh-token columns and uniqueness indexes when missing on legacy databases.
    /// Safe to call repeatedly; no-ops when objects already exist.
    /// </summary>
    public static async Task EnsureSchemaCompatibilityAsync(
        HelloblueGKDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        try
        {
            if (dbContext.Database.IsSqlite())
            {
                await EnsureSqliteCompatibilityAsync(dbContext, logger, cancellationToken);
            }
            else if (dbContext.Database.IsNpgsql())
            {
                await EnsurePostgresCompatibilityAsync(dbContext, logger, cancellationToken);
            }
            else
            {
                await EnsureSqlServerCompatibilityAsync(dbContext, logger, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbException ex)
        {
            logger.LogWarning(
                ex,
                "Schema compatibility patch failed. Manual migration may be required for refresh tokens / twin uniqueness.");
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(
                ex,
                "Schema compatibility patch failed. Manual migration may be required for refresh tokens / twin uniqueness.");
        }
    }

    private static async Task EnsureSqliteCompatibilityAsync(
        HelloblueGKDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!await SqliteColumnExistsAsync(dbContext, "Users", "RefreshTokenHash", cancellationToken))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "Users" ADD COLUMN "RefreshTokenHash" TEXT NULL""",
                cancellationToken);
            logger.LogInformation("Added Users.RefreshTokenHash column.");
        }

        if (!await SqliteColumnExistsAsync(dbContext, "Users", "RefreshTokenExpiresAt", cancellationToken))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "Users" ADD COLUMN "RefreshTokenExpiresAt" TEXT NULL""",
                cancellationToken);
            logger.LogInformation("Added Users.RefreshTokenExpiresAt column.");
        }

        if (!await SqliteColumnExistsAsync(dbContext, "Users", "AccessTokenVersion", cancellationToken))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "Users" ADD COLUMN "AccessTokenVersion" INTEGER NOT NULL DEFAULT 0""",
                cancellationToken);
            logger.LogInformation("Added Users.AccessTokenVersion column.");
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_RefreshTokenHash"
            ON "Users" ("RefreshTokenHash")
            WHERE "RefreshTokenHash" IS NOT NULL
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_DigitalTwins_EngineId_CreatedBy_Active"
            ON "DigitalTwins" ("EngineId", "CreatedBy")
            WHERE "IsActive" = 1 AND "CreatedBy" IS NOT NULL
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Engines_Name_Unique"
            ON "Engines" ("Name")
            """,
            cancellationToken);
    }

    private static async Task EnsurePostgresCompatibilityAsync(
        HelloblueGKDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "RefreshTokenHash" character varying(128) NULL;
            ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "RefreshTokenExpiresAt" timestamp with time zone NULL;
            ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "AccessTokenVersion" integer NOT NULL DEFAULT 0;
            """,
            cancellationToken);
        logger.LogInformation("Ensured Users refresh/access-token columns exist (PostgreSQL).");

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_RefreshTokenHash"
            ON "Users" ("RefreshTokenHash")
            WHERE "RefreshTokenHash" IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_DigitalTwins_EngineId_CreatedBy_Active"
            ON "DigitalTwins" ("EngineId", "CreatedBy")
            WHERE "IsActive" = TRUE AND "CreatedBy" IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Engines_Name_Unique"
            ON "Engines" ("Name");
            """,
            cancellationToken);
    }

    private static async Task EnsureSqlServerCompatibilityAsync(
        HelloblueGKDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH('Users', 'RefreshTokenHash') IS NULL
                ALTER TABLE [Users] ADD [RefreshTokenHash] nvarchar(128) NULL;
            IF COL_LENGTH('Users', 'RefreshTokenExpiresAt') IS NULL
                ALTER TABLE [Users] ADD [RefreshTokenExpiresAt] datetime2 NULL;
            IF COL_LENGTH('Users', 'AccessTokenVersion') IS NULL
                ALTER TABLE [Users] ADD [AccessTokenVersion] int NOT NULL CONSTRAINT [DF_Users_AccessTokenVersion] DEFAULT (0);
            """,
            cancellationToken);
        logger.LogInformation("Ensured Users refresh/access-token columns exist (SQL Server).");

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Users_RefreshTokenHash' AND object_id = OBJECT_ID(N'Users'))
            BEGIN
                CREATE UNIQUE INDEX [IX_Users_RefreshTokenHash]
                ON [Users] ([RefreshTokenHash])
                WHERE [RefreshTokenHash] IS NOT NULL;
            END

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_DigitalTwins_EngineId_CreatedBy_Active' AND object_id = OBJECT_ID(N'DigitalTwins'))
            BEGIN
                CREATE UNIQUE INDEX [IX_DigitalTwins_EngineId_CreatedBy_Active]
                ON [DigitalTwins] ([EngineId], [CreatedBy])
                WHERE [IsActive] = 1 AND [CreatedBy] IS NOT NULL;
            END

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Engines_Name_Unique' AND object_id = OBJECT_ID(N'Engines'))
            BEGIN
                CREATE UNIQUE INDEX [IX_Engines_Name_Unique]
                ON [Engines] ([Name]);
            END
            """,
            cancellationToken);
    }

    private static async Task<bool> SqliteColumnExistsAsync(
        HelloblueGKDbContext dbContext,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        if (command.Connection!.State != System.Data.ConnectionState.Open)
        {
            await command.Connection.OpenAsync(cancellationToken);
        }

        command.CommandText = $"PRAGMA table_info(\"{tableName}\")";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            // PRAGMA table_info columns: cid, name, type, notnull, dflt_value, pk
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> TableSeemsPresentAsync(
        HelloblueGKDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await dbContext.Engines.AsNoTracking().Take(1).CountAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
