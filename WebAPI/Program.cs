using System.Text;
using HB_NLP_Research_Lab.Core;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Data.Repositories;
using HB_NLP_Research_Lab.WebAPI.Middleware;
using HB_NLP_Research_Lab.WebAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using FluentValidation;
using FluentValidation.AspNetCore;
using HB_NLP_Research_Lab.WebAPI.Validators;

var builder = WebApplication.CreateBuilder(args);

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
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
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
        throw new InvalidOperationException(
            "DefaultConnection string must be configured in production. " +
            "Please set ConnectionStrings:DefaultConnection in your configuration. " +
            "For SQL Server, use: Server=your-server;Database=HelloblueGK;...");
    }
}

// Auto-detect database provider from connection string format
// Check for SQL Server-specific keywords first (highest priority)
bool hasSqlServerKeywords = connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                            connectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase) ||
                            connectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase) ||
                            connectionString.Contains("Integrated Security=", StringComparison.OrdinalIgnoreCase) ||
                            connectionString.Contains("Trusted_Connection=", StringComparison.OrdinalIgnoreCase) ||
                            connectionString.Contains("User Id=", StringComparison.OrdinalIgnoreCase) ||
                            connectionString.Contains("Password=", StringComparison.OrdinalIgnoreCase);

// Check for SQLite-specific indicators
bool hasSqliteFileExtension = connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase);
bool hasSqliteFilenameKeyword = connectionString.StartsWith("Filename=", StringComparison.OrdinalIgnoreCase);

// For "Data Source=" strings, only treat as SQLite if it clearly points to a file (.db extension)
// SQL Server can use "Data Source=server" without other keywords, so we must be careful
bool isDataSourceWithDbFile = connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) &&
                              connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase);

// Use SQLite only if clear SQLite indicators exist and no SQL Server keywords
// Default to SQL Server if ambiguous (e.g., "Data Source=.\sqlexpress" will use SQL Server)
bool useSqlite = (hasSqliteFileExtension || hasSqliteFilenameKeyword || isDataSourceWithDbFile) && 
                 !hasSqlServerKeywords;

if (useSqlite)
{
    builder.Services.AddDbContext<HelloblueGKDbContext>(options =>
        options.UseSqlite(connectionString));
}
else
{
    // SQL Server or other providers - default to SQL Server if ambiguous
    builder.Services.AddDbContext<HelloblueGKDbContext>(options =>
        options.UseSqlServer(connectionString));
}

// Add JWT Authentication
// Use same default key as JwtService to maintain consistency
// Default key should only be used in development - production must configure a secure key
const string defaultJwtKey = "your-super-secret-jwt-key-change-in-production-min-32-chars";
var jwtKey = builder.Configuration["Jwt:Key"] ?? defaultJwtKey;
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "hellobluegk";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "hellobluegk-api";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Add core services
builder.Services.AddSingleton<PerformanceMonitoringService>();
builder.Services.AddSingleton<RateLimitingService>();
builder.Services.AddSingleton<StructuredLoggingService>();
builder.Services.AddSingleton<ConfigurationValidationService>();
builder.Services.AddSingleton<AdvancedHealthCheckService>();
builder.Services.AddSingleton<HelloblueGKEngine>();

// Add application services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IEngineRepository, EngineRepository>();

// Add logging
builder.Services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
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
    
    try
    {
        // Attempt to ensure database and tables are created
        // In Production, consider using migrations (Database.Migrate()) instead
        // EnsureCreated() is simpler but doesn't track schema changes like migrations do
        var databaseCreated = dbContext.Database.EnsureCreated();
        
        if (databaseCreated)
        {
            logger.LogInformation("Database and tables created successfully");
        }
        else
        {
            logger.LogInformation("Database already exists - skipping creation");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize database. The application will continue, but database operations may fail.");
        // Don't throw - allow application to start and fail gracefully if database operations are attempted
    }
}

// Configure the HTTP request pipeline

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

// Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

// Swagger/OpenAPI documentation
// Industry-standard approach (like SpaceX, GitHub, Stripe, AWS):
// - Swagger UI is PUBLICLY ACCESSIBLE - anyone can view the documentation
// - Swagger JSON spec is publicly accessible (for API discovery and tooling)
// - API endpoints themselves require authentication (handled by [Authorize] attributes)
// - Users can browse docs freely, but need to login to use "Try it out" feature
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "HelloblueGK API v1");
    c.RoutePrefix = app.Environment.IsDevelopment() ? string.Empty : "swagger";
    c.DocumentTitle = "HelloblueGK API Documentation";

    c.EnableDeepLinking();
    c.EnableFilter();
    c.DisplayRequestDuration();
    c.EnableValidator();

    c.DefaultModelsExpandDepth(2);
    c.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Example);

    // Enable "Try it out" - users can test endpoints, but will need to authenticate
    c.ConfigObject.AdditionalItems.Add("tryItOutEnabled", true);
    c.ConfigObject.AdditionalItems.Add("supportedSubmitMethods", new[] { "get", "post", "put", "patch", "delete" });
});

app.UseCors();

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
            documentation = "API documentation is available in development mode only",
            health = "/Health",
            metrics = "/metrics"
        });
    }
}).ExcludeFromDescription();

// Map Prometheus metrics endpoint - must be after MapControllers for endpoint routing
app.MapMetrics(); // Exposes /metrics endpoint in Prometheus format

Console.WriteLine("🚀 HelloblueGK Web API Server Starting...");
Console.WriteLine("📚 API Documentation: http://localhost:5000/swagger");
Console.WriteLine("🏥 Health Check: http://localhost:5000/Health");
Console.WriteLine("📊 Prometheus Metrics: http://localhost:5000/metrics");
Console.WriteLine("🔐 Authentication: http://localhost:5000/swagger -> Auth endpoints");

app.Run();
