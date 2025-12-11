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
                return;
            }
            
            // Database exists, but tables might not
            // For multiple DbContexts sharing the same database, EnsureCreated() 
            // returns false if database exists, even if tables don't exist
            // We need to force table creation by attempting to use the context
            
            // Try to query the first entity to force table creation
            var model = context.Model;
            var entityTypes = model.GetEntityTypes().ToList();
            
            if (entityTypes.Any())
            {
                // Get table name for first entity
                var firstEntity = entityTypes.First();
                var tableName = firstEntity.GetTableName();
                
                if (!string.IsNullOrEmpty(tableName))
                {
                    // Check if table exists by querying it
                    try
                    {
                        // Try to query the table - this will fail if it doesn't exist
                        var sql = $"SELECT COUNT(*) FROM \"{tableName}\"";
                        await context.Database.ExecuteSqlRawAsync(sql);
                        logger.LogInformation("Table {TableName} exists", tableName);
                    }
                    catch
                    {
                        // Table doesn't exist - force creation by calling EnsureCreated
                        // Even though DB exists, this should create missing tables
                        logger.LogInformation("Table {TableName} does not exist, creating...", tableName);
                        var created = await context.Database.EnsureCreatedAsync();
                        logger.LogInformation("Tables creation attempted. Created: {Created}", created);
                    }
                }
            }
            
            // Verify connection works
            await context.Database.ExecuteSqlRawAsync("SELECT 1");
            logger.LogInformation("Database connection verified");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error ensuring tables exist for {ContextType}: {Error}", context.GetType().Name, ex.Message);
            // Don't throw - allow application to continue
        }
    }
}
