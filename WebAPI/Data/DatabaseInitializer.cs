using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace HB_NLP_Research_Lab.WebAPI.Data;

/// <summary>
/// Ensures the main application schema exists from the EF Core model.
/// Prefers Migrate() when migrations are present; otherwise creates missing tables
/// from the current model (safe replacement for EnsureCreated + ad-hoc SQL).
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
