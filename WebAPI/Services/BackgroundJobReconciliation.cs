using HB_NLP_Research_Lab.WebAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace HB_NLP_Research_Lab.WebAPI.Services;

/// <summary>
/// Fails closed any in-flight background workloads left behind by a previous process.
/// Simulations/optimizations/launches are executed via in-process queue workers only, so a
/// crash or restart would otherwise leave Pending/Running/InProgress rows stranded forever.
/// </summary>
public static class BackgroundJobReconciliation
{
    public const string InterruptedMessage = "Interrupted by process restart";

    public static async Task<BackgroundJobReconciliationResult> ReconcileInterruptedJobsAsync(
        HelloblueGKDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        var completedAt = DateTime.UtcNow;

        var simulations = await context.EngineSimulations
            .Where(s => s.Status == "Pending" || s.Status == "Running")
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.Status, "Failed")
                    .SetProperty(s => s.CompletedAt, completedAt)
                    .SetProperty(s => s.ErrorMessage, InterruptedMessage)
                    .SetProperty(s => s.StackTrace, (string?)null),
                cancellationToken);

        var optimizations = await context.AIOptimizationRuns
            .Where(o => o.Status == "Pending" || o.Status == "Running")
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(o => o.Status, "Failed")
                    .SetProperty(o => o.CompletedAt, completedAt)
                    .SetProperty(o => o.ErrorMessage, InterruptedMessage),
                cancellationToken);

        // Scheduled launches were never claimed by a worker and remain executable.
        var launches = await context.Launches
            .Where(l => l.Status == "InProgress")
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
                "Reconciled {Total} interrupted background job(s) after process restart: {Simulations} simulation(s), {Optimizations} optimization(s), {Launches} launch(es)",
                result.Total,
                result.Simulations,
                result.Optimizations,
                result.Launches);
        }
        else
        {
            logger.LogInformation("No interrupted background jobs found during startup reconciliation");
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
