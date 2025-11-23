using System.Text;
using HB_NLP_Research_Lab.Core;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Data.Repositories;
using HB_NLP_Research_Lab.WebAPI.Middleware;
using HB_NLP_Research_Lab.WebAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
        xmlFile = xmlFile.Replace(Path.DirectorySeparatorChar, '_')
                        .Replace(Path.AltDirectorySeparatorChar, '_')
                        .Replace("..", string.Empty)
                        .Replace("/", "_")
                        .Replace("\\", "_");

        if (!Path.IsPathRooted(xmlFile) && !xmlFile.Contains(Path.DirectorySeparatorChar) &&
            !xmlFile.Contains(Path.AltDirectorySeparatorChar))
        {
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
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
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=hellobluegk.db"; // Fallback to SQLite for development

// Use SQLite for development, SQL Server for production
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<HelloblueGKDbContext>(options =>
        options.UseSqlite(connectionString));
}
else
{
    builder.Services.AddDbContext<HelloblueGKDbContext>(options =>
        options.UseSqlServer(connectionString));
}

// Add JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "your-super-secret-jwt-key-change-in-production-min-32-chars";
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

// Ensure database is created (for development)
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<HelloblueGKDbContext>();
        dbContext.Database.EnsureCreated();
    }
}

// Configure the HTTP request pipeline

// Prometheus metrics
app.UseMetricServer(); // Exposes /metrics endpoint
app.UseHttpMetrics(); // Tracks HTTP metrics

// Global exception handler (must be early in pipeline)
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

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

    c.ConfigObject.AdditionalItems.Add("tryItOutEnabled", true);
    c.ConfigObject.AdditionalItems.Add("supportedSubmitMethods", new[] { "get", "post", "put", "patch", "delete" });
});

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Serve static files for demo
app.UseStaticFiles();

// Health check endpoint (for Render and monitoring)
app.MapGet("/Health", () => Results.Ok(new { 
    status = "Healthy", 
    timestamp = DateTime.UtcNow,
    service = "HelloblueGK WebAPI",
    version = "1.0.0"
})).AllowAnonymous();

// Root redirect to Swagger
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// Prometheus metrics endpoint (alternative route)
app.MapGet("/metrics", async context =>
{
    using var stream = new MemoryStream();
    await Prometheus.Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream, context.RequestAborted);
    stream.Position = 0;
    context.Response.ContentType = "text/plain; version=0.0.4";
    await stream.CopyToAsync(context.Response.Body);
}).AllowAnonymous();

Console.WriteLine("🚀 HelloblueGK Web API Server Starting...");
Console.WriteLine("📚 API Documentation: http://localhost:5000/swagger");
Console.WriteLine("🏥 Health Check: http://localhost:5000/Health");
Console.WriteLine("📊 Prometheus Metrics: http://localhost:5000/metrics");
Console.WriteLine("🔐 Authentication: http://localhost:5000/swagger -> Auth endpoints");

app.Run();
