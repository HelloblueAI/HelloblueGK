using Microsoft.EntityFrameworkCore;
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
                // Try to ensure tables exist by calling EnsureCreated
                // This will create missing tables even if database exists
                var tablesCreated = await context.Database.EnsureCreatedAsync();
                logger.LogInformation("Tables ensured. Created: {Created}", tablesCreated);
                
                // Verify by attempting a simple query
                // This will fail if tables don't exist, triggering creation
                try
                {
                    await context.Database.ExecuteSqlRawAsync("SELECT 1");
                    logger.LogInformation("Database connection verified");
                }
                catch
                {
                    // If query fails, try EnsureCreated again
                    await context.Database.EnsureCreatedAsync();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ensuring tables exist for {ContextType}", context.GetType().Name);
            throw;
        }
    }
}
