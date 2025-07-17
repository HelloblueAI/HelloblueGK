using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using HelloblueGK.Enterprise.API.Services;
using HelloblueGK.Enterprise.API.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HelloblueGK Enterprise API",
        Version = "v1",
        Description = "Advanced aerospace engine simulation platform with AI-driven design, digital twins, and quantum computing",
        Contact = new OpenApiContact
        {
            Name = "Helloblue, Inc. HB-NLP Research Lab",
            Email = "api@helloblue.com"
        }
    });

    // Add JWT Authentication
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
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
            new string[] {}
        }
    });
});

// Add CORS for enterprise deployment
builder.Services.AddCors(options =>
{
    options.AddPolicy("EnterprisePolicy", policy =>
    {
        policy.WithOrigins("https://app.helloblue.com", "https://enterprise.helloblue.com")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add Authentication
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = "https://auth.helloblue.com";
        options.Audience = "https://api.helloblue.com";
        options.RequireHttpsMetadata = true;
    });

// Add Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("EnterpriseAccess", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", "hellobluegk.api"));
});

// Register services
builder.Services.AddScoped<IEngineService, EngineService>();

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.AddEventSourceLogger();
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("api", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API is healthy"));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "HelloblueGK Enterprise API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

app.UseHttpsRedirection();
app.UseCors("EnterprisePolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Root endpoint
app.MapGet("/", () => new
{
    Message = "HelloblueGK Enterprise API",
    Version = "1.0.0",
    Status = "Running",
    Features = new
    {
        AIDrivenDesign = true,
        DigitalTwin = true,
        QuantumComputing = true,
        MultiPhysics = true,
        EnterpriseReady = true
    },
    Timestamp = DateTime.UtcNow
});

app.Run(); 