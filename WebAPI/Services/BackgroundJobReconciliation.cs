using HB_NLP_Research_Lab.WebAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace HB_NLP_Research_Lab.WebAPI.Services;

/// <summary>
/// Fails closed in-flight background workloads left behind by a previous process.
/// Simulations/optimizations/launches are executed via in-process queue workers only, so a
/// crash or restart would otherwise leave Pending/Running/InProgress rows stranded forever.
/// </summary>
/// <remarks>
/// Default <paramref name="minimumAge"/> is <see cref="TimeSpan.Zero"/> so a single-instance
/// restart immediately fail-closes work that can never resume. Multi-replica deployments that
/// share a database must set <c>BackgroundWork:InterruptedJobMinimumAge</c> (e.g. 30 minutes)
/// so a rolling deploy cannot kill jobs still executing on peer replicas.
/// </remarks>
public static class BackgroundJobReconciliation
{
    public const string InterruptedMessage = "Interrupted by process restart";

    /// <summary>
    /// Recommended age gate when multiple WebAPI instances share one database.
    /// </summary>
    public static readonly TimeSpan SharedDatabaseInterruptedJobMinimumAge = TimeSpan.FromMinutes(30);

    public static async Task<BackgroundJobReconciliationResult> ReconcileInterruptedJobsAsync(
        HelloblueGKDbContext context,
        ILogger logger,
        TimeSpan? minimumAge = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        if (minimumAge is { } negativeAge && negativeAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumAge), minimumAge, "minimumAge cannot be negative.");
        }

        // Zero: single-instance immediate fail-close (in-process work never resumes).
        // Non-zero: protect peer replicas during rolling deploys / shared DB.
        var ageGate = minimumAge ?? TimeSpan.Zero;
        var cutoff = DateTime.UtcNow - ageGate;
        var completedAt = DateTime.UtcNow;

        var simulations = await context.EngineSimulations
            .Where(s =>
                (s.Status == "Pending" || s.Status == "Running") &&
                (s.StartedAt ?? s.CreatedAt) <= cutoff)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.Status, "Failed")
                    .SetProperty(s => s.CompletedAt, completedAt)
                    .SetProperty(s => s.ErrorMessage, InterruptedMessage)
                    .SetProperty(s => s.StackTrace, (string?)null),
                cancellationToken);

        var optimizations = await context.AIOptimizationRuns
            .Where(o =>
                (o.Status == "Pending" || o.Status == "Running") &&
                (o.StartedAt ?? o.CreatedAt) <= cutoff)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(o => o.Status, "Failed")
                    .SetProperty(o => o.CompletedAt, completedAt)
                    .SetProperty(o => o.ErrorMessage, InterruptedMessage),
                cancellationToken);

        // Scheduled launches were never claimed by a worker and remain executable.
        var launches = await context.Launches
            .Where(l =>
                l.Status == "InProgress" &&
                (l.LaunchedAt ?? l.CreatedAt) <= cutoff)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(l => l.Status, "Failed")
                    .SetProperty(l => l.CompletedAt, completedAt)
                    .SetProperty(l => l.MissionSuccess, false)
                    .SetProperty(l => l.ErrorMessage, InterruptedMessage),
                cancellationToken);

        var result = new BackgroundJobReconciliationResult(simulations, optimizations, launches);
        if (result.Total > 0)
        {
            logger.LogWarning(
                "Reconciled {Total} interrupted background job(s) older than {MinimumAge} after process restart: {Simulations} simulation(s), {Optimizations} optimization(s), {Launches} launch(es)",
                result.Total,
                ageGate,
                result.Simulations,
                result.Optimizations,
                result.Launches);
        }
        else
        {
            logger.LogInformation(
                "No interrupted background jobs older than {MinimumAge} found during startup reconciliation",
                ageGate);
        }

        return result;
    }
}

public readonly record struct BackgroundJobReconciliationResult(
    int Simulations,
    int Optimizations,
    int Launches)
{
    public int Total => Simulations + Optimizations + Launches;
}
