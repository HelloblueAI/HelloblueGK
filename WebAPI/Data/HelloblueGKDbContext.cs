using Microsoft.EntityFrameworkCore;
using HB_NLP_Research_Lab.WebAPI.Data.Models;
using HB_NLP_Research_Lab.Certification;

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

    // AI and Optimization entities
    public DbSet<AIOptimizationRun> AIOptimizationRuns { get; set; }
    public DbSet<DigitalTwin> DigitalTwins { get; set; }

    // User and Authentication entities
    public DbSet<User> Users { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }

    // Flight Software Certification entities
    public DbSet<Requirement> Requirements { get; set; }
    public DbSet<RequirementDesignLink> RequirementDesignLinks { get; set; }
    public DbSet<RequirementCodeLink> RequirementCodeLinks { get; set; }
    public DbSet<RequirementTestLink> RequirementTestLinks { get; set; }
    public DbSet<ProblemReport> ProblemReports { get; set; }
    public DbSet<ProblemReportStatusChange> ProblemReportStatusChanges { get; set; }
    public DbSet<ProblemReportRequirementLink> ProblemReportRequirementLinks { get; set; }
    public DbSet<ProblemReportTestLink> ProblemReportTestLinks { get; set; }
    public DbSet<SoftwareBaseline> SoftwareBaselines { get; set; }
    public DbSet<ConfigurationItem> ConfigurationItems { get; set; }
    public DbSet<BaselineConfigurationItem> BaselineConfigurationItems { get; set; }
    public DbSet<ChangeRequest> ChangeRequests { get; set; }
    public DbSet<ChangeRequestApproval> ChangeRequestApprovals { get; set; }
    public DbSet<ChangeRequestItemLink> ChangeRequestItemLinks { get; set; }
    public DbSet<CodeCoverage> CodeCoverage { get; set; }
    public DbSet<CoverageTestCaseLink> CoverageTestCaseLinks { get; set; }
    public DbSet<CodeReview> CodeReviews { get; set; }
    public DbSet<CodeReviewAssignment> CodeReviewAssignments { get; set; }
    public DbSet<ReviewFinding> ReviewFindings { get; set; }

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

        // Certification: Requirements
        modelBuilder.Entity<Requirement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RequirementNumber).IsUnique();
            entity.HasMany(e => e.DesignLinks).WithOne().HasForeignKey("RequirementId").OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.CodeLinks).WithOne().HasForeignKey("RequirementId").OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.TestLinks).WithOne().HasForeignKey("RequirementId").OnDelete(DeleteBehavior.Cascade);
        });

        // Certification: Problem Reports
        modelBuilder.Entity<ProblemReport>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ReportNumber).IsUnique();
            entity.HasMany(e => e.StatusChanges).WithOne().HasForeignKey("ProblemReportId").OnDelete(DeleteBehavior.Cascade);
        });

        // Certification: Configuration Management
        modelBuilder.Entity<BaselineConfigurationItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Baseline).WithMany(b => b.ConfigurationItems).HasForeignKey(e => e.BaselineId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ConfigurationItem).WithMany().HasForeignKey(e => e.ConfigurationItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChangeRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RequestNumber).IsUnique();
        });

        // Certification: Test Coverage
        modelBuilder.Entity<CodeCoverage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FilePath).IsUnique();
            entity.HasMany(e => e.TestCaseLinks).WithOne().HasForeignKey("CodeCoverageId").OnDelete(DeleteBehavior.Cascade);
        });

        // Certification: Code Reviews
        modelBuilder.Entity<CodeReview>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ReviewNumber).IsUnique();
            entity.HasMany(e => e.Assignments).WithOne().HasForeignKey("ReviewId").OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Findings).WithOne().HasForeignKey("ReviewId").OnDelete(DeleteBehavior.Cascade);
        });
    }
}

