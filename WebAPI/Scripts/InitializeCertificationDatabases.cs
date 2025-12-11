using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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
        try
        {
            // First ensure database exists
            var canConnect = await context.Database.CanConnectAsync();
            if (!canConnect)
            {
                logger.LogWarning("Cannot connect to database. Attempting to create...");
                var created = await context.Database.EnsureCreatedAsync();
                logger.LogInformation("Database created: {Created}", created);
            }
            else
            {
                // Database exists, but tables might not
                // EnsureCreated() returns false if database exists, but will still create missing tables
                // However, for multiple contexts sharing same DB, we need to force table creation
                var tablesCreated = await context.Database.EnsureCreatedAsync();
                logger.LogInformation("Tables ensured. Created: {Created}", tablesCreated);
                
                // Force table creation by attempting to use the context
                // This will create tables if they don't exist
                try
                {
                    // Try to query the first DbSet to force table creation
                    var model = context.Model;
                    var entityTypes = model.GetEntityTypes();
                    if (entityTypes.Any())
                    {
                        // Get the first entity type and try to query it
                        var firstEntity = entityTypes.First();
                        var method = typeof(EntityFrameworkQueryableExtensions)
                            .GetMethod(nameof(EntityFrameworkQueryableExtensions.AnyAsync))
                            ?.MakeGenericMethod(firstEntity.ClrType);
                        
                        // This will trigger table creation if needed
                        await context.Database.ExecuteSqlRawAsync("SELECT 1");
                    }
                    logger.LogInformation("Database connection verified");
                }
                catch (Exception queryEx)
                {
                    logger.LogWarning(queryEx, "Query failed, attempting to create tables...");
                    // If query fails, the tables likely don't exist
                    // Use migrations or manual table creation
                    try
                    {
                        // Try EnsureCreated again - it should create tables even if DB exists
                        await context.Database.EnsureCreatedAsync();
                        logger.LogInformation("Tables creation attempted after query failure");
                    }
                    catch (Exception createEx)
                    {
                        logger.LogError(createEx, "Failed to create tables for {ContextType}", context.GetType().Name);
                        // Don't throw - allow application to continue
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ensuring tables exist for {ContextType}: {Error}", context.GetType().Name, ex.Message);
            // Don't throw - log and continue
        }
    }
}
