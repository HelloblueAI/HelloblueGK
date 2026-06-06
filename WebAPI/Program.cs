using System.Text;
using HB_NLP_Research_Lab.Core;
using HB_NLP_Research_Lab.Certification;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Data.Repositories;
using HB_NLP_Research_Lab.WebAPI.Middleware;
using HB_NLP_Research_Lab.WebAPI.Services;
using HB_NLP_Research_Lab.WebAPI.Scripts;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Prometheus;
using FluentValidation;
using FluentValidation.AspNetCore;
using HB_NLP_Research_Lab.WebAPI.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using HB_NLP_Research_Lab.WebAPI.Authorization;
using HB_NLP_Research_Lab.WebAPI.Configuration;
using HB_NLP_Research_Lab.WebAPI.Extensions;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Render/Railway terminate TLS at the edge and forward HTTP to the container.
// Trust X-Forwarded-Proto so OIDC redirect URIs use https:// (required by Azure AD).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.Configure<DocumentationOptions>(
    builder.Configuration.GetSection(DocumentationOptions.SectionName));
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, SwaggerDevAuthorizationHandler>();

// Add services to the container
var mvcBuilder = builder.Services.AddControllers();
mvcBuilder.PartManager.ApplicationParts.Clear();
mvcBuilder.AddApplicationPart(typeof(Program).Assembly);

// Add API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

builder.Services.AddVersionedApiExplorer(setup =>
{
    setup.GroupNameFormat = "'v'VVV";
    setup.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();

// Configure Swagger/OpenAPI with versioning
builder.Services.AddSwaggerGen(c =>
{
    // Configure Swagger to discover endpoints from both versioned and non-versioned controllers
    // Include all endpoints in v1 document (both versioned and non-versioned)
    c.DocInclusionPredicate((docName, apiDesc) =>
    {
        // If endpoint has a group name (versioned), include it if it matches the doc name
        if (!string.IsNullOrEmpty(apiDesc.GroupName))
        {
            return apiDesc.GroupName == docName;
        }
        // For non-versioned endpoints, include them in v1
        return docName == "v1";
    });
    
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HelloblueGK Aerospace Engine Simulation API",
        Version = "v1",
        Description = "Advanced aerospace engine simulation platform with multi-physics coupling, AI optimization, and enterprise-grade compliance.",
        Contact = new OpenApiContact
        {
            Name = "HB-NLP Research Lab",
            Url = new Uri("https://github.com/HelloblueAI/HelloblueGK")
        },
        License = new OpenApiLicense
        {
            Name = "Apache 2.0",
            Url = new Uri("https://www.apache.org/licenses/LICENSE-2.0.html")
        }
    });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    c.ConfigureSwaggerOAuth(builder.Configuration);

    // Include XML comments if available
    var assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
    if (!string.IsNullOrEmpty(assemblyName))
    {
        var xmlFile = $"{assemblyName}.xml";
        // Sanitize filename to prevent path traversal attacks
        xmlFile = xmlFile.Replace(Path.DirectorySeparatorChar, '_')
                        .Replace(Path.AltDirectorySeparatorChar, '_')
                        .Replace("..", string.Empty)
                        .Replace("/", "_")
                        .Replace("\\", "_");

        // Additional validation: ensure filename contains only safe characters
        if (!Path.IsPathRooted(xmlFile) && 
            !xmlFile.Contains(Path.DirectorySeparatorChar) &&
            !xmlFile.Contains(Path.AltDirectorySeparatorChar) &&
            !xmlFile.Contains("..") &&
            xmlFile.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
        {
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            
            // Security: Ensure the resolved path is within the base directory (prevent path traversal)
            var baseDir = Path.GetFullPath(AppContext.BaseDirectory);
            var resolvedPath = Path.GetFullPath(xmlPath);
            
            if (resolvedPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase) && 
                File.Exists(resolvedPath))
            {
                c.IncludeXmlComments(resolvedPath);
            }
        }
    }

    c.CustomSchemaIds(type => type.FullName);
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
});

// Add FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var configuredOrigins = builder.Configuration["Cors:Origins"]?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? Array.Empty<string>();

        if (configuredOrigins.Length > 0)
        {
            policy.WithOrigins(configuredOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

// Configure Entity Framework Core
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    // Fallback: Use SQLite for development, but require configuration in production
    if (builder.Environment.IsDevelopment())
    {
        connectionString = "Data Source=hellobluegk.db"; // SQLite for development
    }
    else
    {
        // In production, require connection string, but allow Railway/Render to set it
        // Railway provides DATABASE_URL, Render expects ConnectionStrings__DefaultConnection
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            // Convert Railway DATABASE_URL format to .NET connection string
            // Format: postgresql://user:pass@host:port/db or postgresql://user@host:port/db (passwordless)
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':');
            var username = userInfo.Length > 0 ? userInfo[0] : string.Empty;
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
            
            // Build connection string - include password only if provided
            if (!string.IsNullOrEmpty(password))
            {
                connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.LocalPath.TrimStart('/')};Username={username};Password={password}";
            }
            else
            {
                connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.LocalPath.TrimStart('/')};Username={username}";
            }
        }
        else
        {
            throw new InvalidOperationException(
                "DefaultConnection string must be configured in production. " +
                "Please set ConnectionStrings:DefaultConnection or DATABASE_URL in your configuration. " +
                "For SQL Server, use: Server=your-server;Database=HelloblueGK;...");
        }
    }
}

// Auto-detect database provider from connection string format
// Check for PostgreSQL-specific keywords first (highest priority)
bool hasPostgresKeywords = connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
                            connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) && 
                            (connectionString.Contains("Port=", StringComparison.OrdinalIgnoreCase) ||
                             connectionString.Contains("Username=", StringComparison.OrdinalIgnoreCase) ||
                             connectionString.Contains("User Id=", StringComparison.OrdinalIgnoreCase));

// Check for SQL Server-specific keywords
bool hasSqlServerKeywords = connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase) ||
                            connectionString.Contains("Integrated Security=", StringComparison.OrdinalIgnoreCase) ||
                            connectionString.Contains("Trusted_Connection=", StringComparison.OrdinalIgnoreCase) ||
                            (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) && 
                             !hasPostgresKeywords);

// Check for SQLite-specific indicators
bool hasSqliteFileExtension = connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase);
bool hasSqliteFilenameKeyword = connectionString.StartsWith("Filename=", StringComparison.OrdinalIgnoreCase);
bool isDataSourceWithDbFile = connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) &&
                              connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase);
bool useSqlite = (hasSqliteFileExtension || hasSqliteFilenameKeyword || isDataSourceWithDbFile) && 
                 !hasSqlServerKeywords && !hasPostgresKeywords;

// Select database provider based on connection string format
// For PostgreSQL, add ApplicationName to connection string to help identify connections in metrics
Action<DbContextOptionsBuilder> configureDbContext = hasPostgresKeywords
    ? options =>
    {
        // Add ApplicationName to connection string to help identify connections without exposing full connection details
        // Note: Npgsql still uses the full connection string as pool identifier in metrics, but ApplicationName
        // helps identify the application in PostgreSQL server logs
        var connectionStringWithAppName = connectionString;
        if (!connectionString.Contains("ApplicationName=", StringComparison.OrdinalIgnoreCase))
        {
            connectionStringWithAppName = $"{connectionString};ApplicationName=HelloblueGK-API";
        }
        
        // Use NpgsqlDataSourceBuilder to create a data source
        // This allows better connection pooling and management
        var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionStringWithAppName);
        var dataSource = dataSourceBuilder.Build();
        options.UseNpgsql(dataSource);
    }
    : useSqlite
        ? options => options.UseSqlite(connectionString)
        : options => options.UseSqlServer(connectionString);

// Main database context
builder.Services.AddDbContext<HelloblueGKDbContext>(configureDbContext);

// Flight Software Certification database contexts (use same connection)
builder.Services.AddDbContext<RequirementsDbContext>(configureDbContext);
builder.Services.AddDbContext<ProblemReportDbContext>(configureDbContext);
builder.Services.AddDbContext<ConfigurationDbContext>(configureDbContext);
builder.Services.AddDbContext<TestCoverageDbContext>(configureDbContext);
builder.Services.AddDbContext<CodeReviewDbContext>(configureDbContext);

// Flight Software Certification services
builder.Services.AddScoped<RequirementsTraceabilitySystem>();
builder.Services.AddScoped<ProblemReportingSystem>();
builder.Services.AddScoped<ConfigurationManagementSystem>();
builder.Services.AddScoped<TestCoverageSystem>();
builder.Services.AddScoped<FormalCodeReviewSystem>();

// Add JWT Authentication
// Default key is only allowed for local development; deployed environments must configure a secure key.
const string defaultJwtKey = "your-super-secret-jwt-key-change-in-production-min-32-chars";
const int minimumJwtKeyBytes = 32;

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    if (builder.Environment.IsDevelopment())
    {
        jwtKey = defaultJwtKey;
    }
    else
    {
        throw new InvalidOperationException(
            "SECURITY ERROR: JWT key is not configured. " +
            "Please set a secure JWT:Key in configuration or environment variables.");
    }
}

var isKnownInsecureJwtKey = jwtKey == defaultJwtKey || jwtKey == "change-me-in-production";
if (!builder.Environment.IsDevelopment() && isKnownInsecureJwtKey)
{
    throw new InvalidOperationException(
        "SECURITY ERROR: Insecure JWT key detected outside development. " +
        "Please set a secure JWT:Key in configuration or environment variables.");
}

if (Encoding.UTF8.GetByteCount(jwtKey) < minimumJwtKeyBytes)
{
    throw new InvalidOperationException(
        $"SECURITY ERROR: JWT key must contain at least {minimumJwtKeyBytes} bytes of entropy.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "hellobluegk";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "hellobluegk-api";
var openIdConnectEnabled = builder.AddHelloblueGKAuthentication(jwtKey, jwtIssuer, jwtAudience);
var documentationOptions = builder.Configuration
    .GetSection(DocumentationOptions.SectionName)
    .Get<DocumentationOptions>() ?? new DocumentationOptions();

builder.Services.AddAuthorization(options =>
{
    var authenticatedUserPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.DefaultPolicy = authenticatedUserPolicy;

    // Internal-only documentation: Swagger and other unannotated routes require authentication in production.
    if (!builder.Environment.IsDevelopment() || documentationOptions.InternalOnly)
    {
        options.FallbackPolicy = authenticatedUserPolicy;
    }
});

// Configure Data Protection
// For containerized stateless APIs, ephemeral keys are acceptable (regenerated on restart)
// The warnings about key storage are expected and harmless for stateless containerized applications.
// Data Protection keys are only needed for features like anti-forgery tokens, which this API doesn't use.
// If key persistence is needed in the future, add Microsoft.AspNetCore.DataProtection.FileSystem package
// and configure: .PersistKeysToFileSystem(new DirectoryInfo("/app/data/keys"))

// Add core services
// PerformanceMonitoringService implements IHostedService and should be registered as such
// in a hosted application (WebAPI). It will be available as a singleton for injection.
builder.Services.AddHostedService<PerformanceMonitoringService>();
builder.Services.AddSingleton<RateLimitingService>();
builder.Services.AddSingleton<StructuredLoggingService>();
builder.Services.AddSingleton<ConfigurationValidationService>();
builder.Services.AddSingleton<AdvancedHealthCheckService>();
builder.Services.AddSingleton<HelloblueGKEngine>();
builder.Services.AddSingleton<AdvancedAIOptimizationEngine>();
builder.Services.AddSingleton<DigitalTwinEngine>();

// Add application services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IEngineRepository, EngineRepository>();

// Add logging
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    loggingBuilder.SetMinimumLevel(LogLevel.Information);
    
    // Suppress Data Protection warnings for containerized stateless APIs
    // These warnings are expected and harmless - keys are ephemeral by design
    loggingBuilder.AddFilter("Microsoft.AspNetCore.DataProtection", LogLevel.Warning);
});

// Configure port for Render/deployment
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// Ensure database is created and initialized
// This runs in both Development and Production to ensure database tables exist
    using (var scope = app.Services.CreateScope())
    {
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<HelloblueGKDbContext>();
        var requirementsContext = scope.ServiceProvider.GetRequiredService<RequirementsDbContext>();
        var problemReportContext = scope.ServiceProvider.GetRequiredService<ProblemReportDbContext>();
        var configurationContext = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        var testCoverageContext = scope.ServiceProvider.GetRequiredService<TestCoverageDbContext>();
        var codeReviewContext = scope.ServiceProvider.GetRequiredService<CodeReviewDbContext>();
    
    try
    {
        // Ensure database exists first
        var dbExists = dbContext.Database.CanConnect();
        if (!dbExists)
        {
            logger.LogInformation("Database does not exist. Creating database...");
            var created = dbContext.Database.EnsureCreated();
            logger.LogInformation("Database created: {Created}", created);
        }
        else
        {
            logger.LogInformation("Database already exists");
        }
        
        // For multiple DbContexts sharing the same database, we need to ensure tables exist
        // EnsureCreated() only works if database doesn't exist, so we use a different approach
        // We'll try to ensure tables exist by attempting to query them, which will create them if missing
        
        // Initialize certification contexts using improved initializer
        // This handles the case where multiple DbContexts share the same database
        try
        {
            await CertificationDatabaseInitializer.InitializeAllAsync(
                requirementsContext,
                problemReportContext,
                configurationContext,
                testCoverageContext,
                codeReviewContext,
                logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize certification databases: {Error}", ex.Message);
            // Continue - individual contexts will handle errors
        }
        
        logger.LogInformation("Flight Software Certification systems initialization completed");
        
        // Ensure all tables exist (including new ones like Launch)
        // This handles the case where new tables are added after initial database creation
        try
        {
            logger.LogInformation("Ensuring all database tables exist...");
            // Try to query each table to ensure it exists
            _ = await dbContext.Engines.CountAsync();
            _ = await dbContext.EngineSimulations.CountAsync();
            _ = await dbContext.AIOptimizationRuns.CountAsync();
            _ = await dbContext.DigitalTwins.CountAsync();

            if (hasPostgresKeywords)
            {
                await dbContext.Database.ExecuteSqlRawAsync(@"
                    ALTER TABLE ""AIOptimizationRuns""
                    ADD COLUMN IF NOT EXISTS ""CreatedBy"" VARCHAR(255);

                    ALTER TABLE ""DigitalTwins""
                    ADD COLUMN IF NOT EXISTS ""CreatedBy"" VARCHAR(255);

                    CREATE INDEX IF NOT EXISTS ""IX_AIOptimizationRuns_CreatedBy""
                    ON ""AIOptimizationRuns""(""CreatedBy"");

                    CREATE INDEX IF NOT EXISTS ""IX_DigitalTwins_CreatedBy""
                    ON ""DigitalTwins""(""CreatedBy"");
                ");
            }
            
            // Ensure Launch table exists - try to query it, create if missing
            try
            {
                _ = await dbContext.Launches.CountAsync();
                logger.LogInformation("Launch table exists");
            }
            catch
            {
                // Table doesn't exist - try to create it using EF Core model
                logger.LogInformation("Launch table does not exist, attempting to create...");
                try
                {
                    // Use GenerateCreateScript to get SQL for Launch table only
                    var createScript = dbContext.Database.GenerateCreateScript();
                    // Extract just the Launch table creation SQL
                    if (createScript.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
                    {
                        // For PostgreSQL, we can try to create just the Launch table
                        // Since we can't easily extract just one table, we'll let EF handle it on first use
                        // or create it manually with a simple SQL statement
                        // Only execute PostgreSQL-specific SQL if we're using PostgreSQL
                        if (hasPostgresKeywords)
                        {
                            await dbContext.Database.ExecuteSqlRawAsync(@"
                            CREATE TABLE IF NOT EXISTS ""Launches"" (
                                ""Id"" SERIAL PRIMARY KEY,
                                ""MissionName"" VARCHAR(200) NOT NULL,
                                ""Description"" VARCHAR(500),
                                ""EngineId"" INTEGER NOT NULL,
                                ""EngineCount"" INTEGER NOT NULL DEFAULT 1,
                                ""Status"" VARCHAR(50) DEFAULT 'Scheduled',
                                ""LaunchParametersJson"" TEXT,
                                ""ResultsJson"" TEXT,
                                ""MissionDurationSeconds"" DOUBLE PRECISION,
                                ""MaxAltitude"" DOUBLE PRECISION,
                                ""MaxVelocity"" DOUBLE PRECISION,
                                ""MissionSuccess"" BOOLEAN,
                                ""ErrorMessage"" TEXT,
                                ""ScheduledAt"" TIMESTAMP NOT NULL,
                                ""LaunchedAt"" TIMESTAMP,
                                ""CompletedAt"" TIMESTAMP,
                                ""CreatedBy"" VARCHAR(255),
                                ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                            );
                        ");
                        // Create indexes
                        await dbContext.Database.ExecuteSqlRawAsync(@"
                            CREATE INDEX IF NOT EXISTS ""IX_Launches_EngineId"" ON ""Launches""(""EngineId"");
                            CREATE INDEX IF NOT EXISTS ""IX_Launches_Status"" ON ""Launches""(""Status"");
                            CREATE INDEX IF NOT EXISTS ""IX_Launches_ScheduledAt"" ON ""Launches""(""ScheduledAt"");
                            CREATE INDEX IF NOT EXISTS ""IX_Launches_CreatedAt"" ON ""Launches""(""CreatedAt"");
                        ");
                        // Add foreign key constraint
                        await dbContext.Database.ExecuteSqlRawAsync(@"
                            DO $$
                            BEGIN
                                IF NOT EXISTS (
                                    SELECT 1 FROM pg_constraint 
                                    WHERE conname = 'FK_Launches_Engines_EngineId'
                                ) THEN
                                    ALTER TABLE ""Launches""
                                    ADD CONSTRAINT ""FK_Launches_Engines_EngineId""
                                    FOREIGN KEY (""EngineId"") REFERENCES ""Engines""(""Id"") ON DELETE RESTRICT;
                                END IF;
                            END $$;
                        ");
                            logger.LogInformation("Launch table created successfully");
                        }
                        else
                        {
                            logger.LogWarning("Launch table creation skipped - PostgreSQL-specific SQL only works with PostgreSQL. Table will be created via EF Core migrations or EnsureCreated on first use.");
                        }
                    }
                }
                catch (Exception createEx)
                {
                    logger.LogWarning(createEx, "Could not create Launch table automatically: {Error}. It will be created on first use.", createEx.Message);
                }
            }
            logger.LogInformation("All database tables verified");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Warning while verifying tables: {Error}. Tables will be created on first use.", ex.Message);
        }
        
        // Seed initial engines if database is empty
        try
        {
            await EngineSeeder.SeedEnginesAsync(dbContext, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed engines: {Error}", ex.Message);
            // Continue - engines can be added manually via API
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize database: {Error}. The application will continue, but database operations may fail.", ex.Message);
        // Don't throw - allow application to start and fail gracefully if database operations are attempted
    }
    }

// Configure the HTTP request pipeline

app.UseForwardedHeaders();

// Security headers middleware (must be early in pipeline)
app.Use(async (context, next) =>
{
    // Add security headers to all responses
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");
    
    await next();
});

// Global exception handler (must be early in pipeline)
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// Prometheus HTTP metrics tracking (tracks HTTP requests)
app.UseHttpMetrics();

app.UseCors();

// Authentication and Authorization
app.UseAuthentication();
if (builder.Configuration.GetValue("EnableRateLimiting", true))
{
    app.UseRateLimiting();
}
app.UseAuthorization();

// Swagger/OpenAPI documentation — internal-only in production (aerospace-style).
// Production: SSO via /api/v1/Account/login when OpenIdConnect is enabled, otherwise JWT Bearer.
// Development: public when Documentation:AllowPublicInDevelopment is true.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "HelloblueGK API v1");
    c.RoutePrefix = app.Environment.IsDevelopment() ? string.Empty : "swagger";
    c.DocumentTitle = "HelloblueGK API Documentation (Internal)";

    c.EnableDeepLinking();
    c.EnableFilter();
    c.DisplayRequestDuration();
    c.EnableValidator();

    c.DefaultModelsExpandDepth(2);
    c.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Example);

    c.ConfigObject.AdditionalItems.Add("tryItOutEnabled", true);
    c.ConfigObject.AdditionalItems.Add("supportedSubmitMethods", new[] { "get", "post", "put", "patch", "delete" });

    if (openIdConnectEnabled)
    {
        var clientId = app.Configuration["Authentication:OpenIdConnect:ClientId"] ?? string.Empty;
        var apiScope = app.Configuration["Authentication:OpenIdConnect:ApiScope"]
            ?? $"api://{clientId}/.default";

        c.OAuthClientId(clientId);
        c.OAuthUsePkce();
        c.OAuthScopes("openid", "profile", apiScope);
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();

// Serve static files for demo
app.UseStaticFiles();

// Root endpoint - redirect to Swagger in development, show info in production
app.MapGet("/", () =>
{
    if (app.Environment.IsDevelopment())
    {
        return Results.Redirect("/swagger");
    }
    else
    {
        return Results.Json(new
        {
            service = "HelloblueGK Aerospace Engine Simulation API",
            version = "v1",
            status = "operational",
            documentation = openIdConnectEnabled
                ? "Internal API documentation requires corporate SSO at /api/v1/Account/login"
                : "Internal API documentation requires authentication at /swagger",
            internalDocsLogin = openIdConnectEnabled ? "/api/v1/Account/login" : (string?)null,
            health = "/Health",
            metrics = "/metrics"
        });
    }
}).AllowAnonymous().ExcludeFromDescription();

// Map Prometheus metrics endpoint - must be after MapControllers for endpoint routing
app.MapMetrics().RequireAuthorization(); // Exposes /metrics endpoint in Prometheus format

Console.WriteLine("🚀 HelloblueGK Web API Server Starting...");
Console.WriteLine("📚 API Documentation: http://localhost:5000/swagger");
Console.WriteLine("🏥 Health Check: http://localhost:5000/Health");
Console.WriteLine("📊 Prometheus Metrics: http://localhost:5000/metrics");
Console.WriteLine("🔐 Authentication: http://localhost:5000/swagger -> Auth endpoints");

app.Run();

public partial class Program
{
}
