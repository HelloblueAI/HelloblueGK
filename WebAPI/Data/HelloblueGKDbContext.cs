using Microsoft.EntityFrameworkCore;
using HB_NLP_Research_Lab.WebAPI.Data.Models;

namespace HB_NLP_Research_Lab.WebAPI.Data;

/// <summary>
/// Main database context for HelloblueGK application
/// Manages all database entities and relationships
/// </summary>
public class HelloblueGKDbContext : DbContext
{
    public HelloblueGKDbContext(DbContextOptions<HelloblueGKDbContext> options)
        : base(options)
    {
    }

    // Engine-related entities
    public DbSet<Engine> Engines { get; set; }
    public DbSet<EngineSimulation> EngineSimulations { get; set; }
    public DbSet<EngineTelemetry> EngineTelemetry { get; set; }
    public DbSet<EngineConfiguration> EngineConfigurations { get; set; }
    public DbSet<Launch> Launches { get; set; }

    // AI and Optimization entities
    public DbSet<AIOptimizationRun> AIOptimizationRuns { get; set; }
    public DbSet<DigitalTwin> DigitalTwins { get; set; }

    // User and Authentication entities
    public DbSet<User> Users { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }

    // Note: Flight Software Certification entities are managed by separate DbContexts:
    // - RequirementsDbContext (Requirements, RequirementDesignLinks, RequirementCodeLinks, RequirementTestLinks)
    // - ProblemReportDbContext (ProblemReports, ProblemReportStatusChanges, etc.)
    // - ConfigurationDbContext (SoftwareBaselines, ConfigurationItems, ChangeRequests, etc.)
    // - TestCoverageDbContext (CodeCoverage, CoverageTestCaseLinks)
    // - CodeReviewDbContext (CodeReviews, CodeReviewAssignments, ReviewFindings)
    // This separation ensures proper isolation and follows certification best practices.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Engine configuration
        modelBuilder.Entity<Engine>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.EngineType).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Engine Simulation
        modelBuilder.Entity<EngineSimulation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Engine)
                  .WithMany()
                  .HasForeignKey(e => e.EngineId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.EngineId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Engine Telemetry
        modelBuilder.Entity<EngineTelemetry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Simulation)
                  .WithMany()
                  .HasForeignKey(e => e.SimulationId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.SimulationId);
            entity.HasIndex(e => e.Timestamp);
        });

        // AI Optimization Run
        modelBuilder.Entity<AIOptimizationRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Engine)
                  .WithMany()
                  .HasForeignKey(e => e.EngineId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.EngineId);
            entity.HasIndex(e => e.Status);
        });

        // Digital Twin
        modelBuilder.Entity<DigitalTwin>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Engine)
                  .WithMany()
                  .HasForeignKey(e => e.EngineId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.EngineId);
        });

        // Launch
        modelBuilder.Entity<Launch>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MissionName).IsRequired().HasMaxLength(200);
            entity.HasOne(e => e.Engine)
                  .WithMany()
                  .HasForeignKey(e => e.EngineId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.EngineId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ScheduledAt);
            entity.HasIndex(e => e.CreatedAt);
        });

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // API Key
        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.KeyHash).IsRequired().HasMaxLength(512);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.KeyHash);
            entity.HasIndex(e => e.UserId);
        });
    }
}

