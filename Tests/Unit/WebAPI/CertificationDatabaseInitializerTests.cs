using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using HB_NLP_Research_Lab.Certification;
using HB_NLP_Research_Lab.WebAPI.Scripts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HelloblueGK.Tests.Unit.WebAPI;

/// <summary>
/// The five certification contexts share one database, so schema initialisation has to cope with a
/// database that already holds some of the tables it is about to declare. Production ran for a week
/// logging 'relation "CodeReviews" already exists' on every boot while CertifiedReviewers and
/// RequiredReviewFiles stayed missing, because the initialiser probed only the first table of each
/// context and then submitted the whole create script as a single batch.
///
/// These tests use SQLite over one shared connection, which gives the property that matters: a
/// database that exists, is reachable, and is partially populated.
///
/// One difference is worth knowing when reading them. PostgreSQL runs the batch in an implicit
/// transaction, so the conflict rolls back every table in the script. SQLite applies statements up
/// to the failure, so only tables ordered after the conflicting one are lost. The tests therefore
/// pin the order-dependent loss rather than the total loss seen in production, but the cause they
/// exercise is the same: one script covering tables that already exist, submitted as one batch.
/// </summary>
public sealed class CertificationDatabaseInitializerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public CertificationDatabaseInitializerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task InitializeAllAsync_CreatesEveryTableOfEveryContext_OnAnEmptyDatabase()
    {
        await RunInitializerAsync();

        var tables = ExistingTables();

        foreach (var expected in AllExpectedTables())
        {
            tables.Should().Contain(expected, $"{expected} is declared by a certification context");
        }
    }

    /// <summary>
    /// The production failure, reproduced: tables added to a context after its schema was first
    /// created must still be created. Without the fix the initialiser aborts on the first table
    /// that already exists and leaves these two absent.
    /// </summary>
    [Theory]
    [InlineData("CertifiedReviewers")]
    [InlineData("RequiredReviewFiles")]
    public async Task InitializeAllAsync_CreatesTablesAddedAfterTheSchemaWasFirstBuilt(string droppedTable)
    {
        await RunInitializerAsync();
        Execute($"DROP TABLE \"{droppedTable}\"");
        ExistingTables().Should().NotContain(droppedTable);

        await RunInitializerAsync();

        ExistingTables().Should().Contain(droppedTable);
    }

    /// <summary>
    /// A missing table must not cost the tables that are already there, and the rows in them.
    /// </summary>
    [Fact]
    public async Task InitializeAllAsync_RepairingOneTablePreservesExistingTablesAndData()
    {
        await RunInitializerAsync();

        await using (var seed = Create<CodeReviewDbContext>())
        {
            seed.CodeReviews.Add(new CodeReview { ReviewNumber = "CR-001" });
            await seed.SaveChangesAsync();
        }

        Execute("DROP TABLE \"CertifiedReviewers\"");

        await RunInitializerAsync();

        ExistingTables().Should().Contain("CertifiedReviewers");
        Scalar("SELECT COUNT(*) FROM \"CodeReviews\"").Should().Be(1);
    }

    /// <summary>
    /// Startup runs on every deploy, so a complete schema has to be a quiet no-op rather than a
    /// source of errors in the log.
    /// </summary>
    [Fact]
    public async Task InitializeAllAsync_IsIdempotent()
    {
        await RunInitializerAsync();
        var before = ExistingTables();

        await RunInitializerAsync();
        await RunInitializerAsync();

        ExistingTables().Should().BeEquivalentTo(before);
    }

    private async Task RunInitializerAsync()
    {
        await using var requirements = Create<RequirementsDbContext>();
        await using var problemReports = Create<ProblemReportDbContext>();
        await using var configuration = Create<ConfigurationDbContext>();
        await using var testCoverage = Create<TestCoverageDbContext>();
        await using var codeReviews = Create<CodeReviewDbContext>();

        await CertificationDatabaseInitializer.InitializeAllAsync(
            requirements,
            problemReports,
            configuration,
            testCoverage,
            codeReviews,
            NullLogger.Instance);
    }

    private IEnumerable<string> AllExpectedTables()
    {
        using var requirements = Create<RequirementsDbContext>();
        using var problemReports = Create<ProblemReportDbContext>();
        using var configuration = Create<ConfigurationDbContext>();
        using var testCoverage = Create<TestCoverageDbContext>();
        using var codeReviews = Create<CodeReviewDbContext>();

        return new DbContext[] { requirements, problemReports, configuration, testCoverage, codeReviews }
            .SelectMany(context => context.Model.GetEntityTypes())
            .Select(entity => entity.GetTableName())
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .Distinct()
            .ToList();
    }

    private TContext Create<TContext>()
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(_connection)
            .Options;

        return (TContext)Activator.CreateInstance(typeof(TContext), options)!;
    }

    private List<string> ExistingTables()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";

        var tables = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private void Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private long Scalar(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }
}
