using HB_NLP_Research_Lab.Core;
using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger/OpenAPI
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
    
    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
    
    // Ignore type conflicts - use the types from this assembly
    c.CustomSchemaIds(type => type.FullName);
    
    // Resolve conflicting actions (same route from different assemblies)
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
});

// Add CORS for demo purposes
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add core services
builder.Services.AddSingleton<PerformanceMonitoringService>();
builder.Services.AddSingleton<RateLimitingService>();
builder.Services.AddSingleton<StructuredLoggingService>();
builder.Services.AddSingleton<ConfigurationValidationService>();
builder.Services.AddSingleton<AdvancedHealthCheckService>();
builder.Services.AddSingleton<HelloblueGKEngine>();

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

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "HelloblueGK API v1");
    c.RoutePrefix = app.Environment.IsDevelopment() ? string.Empty : "swagger";
    c.DocumentTitle = "HelloblueGK API Documentation";
    
    // Enable interactive features - Try it out buttons should appear by default
    // but ensure they're visible in all environments
    c.EnableDeepLinking();
    c.EnableFilter();
    c.DisplayRequestDuration();
    c.EnableValidator();
    
    // Show request/response examples
    c.DefaultModelsExpandDepth(2);
    c.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Example);
    
    // Ensure Swagger UI is in interactive mode (not read-only)
    c.ConfigObject.AdditionalItems.Add("tryItOutEnabled", true);
    c.ConfigObject.AdditionalItems.Add("supportedSubmitMethods", new[] { "get", "post", "put", "patch", "delete" });
});

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Serve static files for demo
app.UseStaticFiles();

// Health check endpoint
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

Console.WriteLine("🚀 HelloblueGK Web API Server Starting...");
Console.WriteLine("📚 API Documentation: http://localhost:5000/swagger");
Console.WriteLine("🏥 Health Check: http://localhost:5000/Health");

app.Run();

