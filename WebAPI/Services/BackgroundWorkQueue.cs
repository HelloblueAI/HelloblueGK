using System.Collections.Concurrent;

namespace HB_NLP_Research_Lab.WebAPI.Services;

public interface IBackgroundWorkQueue
{
    int MaxConcurrency { get; }
    bool TryAcquire(out IBackgroundWorkSlot? slot);

    /// <summary>
    /// Cancels a previously queued work item by name (e.g. <c>simulation:12</c>).
    /// Returns true when a live registration was signalled.
    /// </summary>
    bool TryCancel(string workItemName);
}

public interface IBackgroundWorkSlot : IDisposable
{
    void Queue(
        Func<IServiceProvider, CancellationToken, Task> workItem,
        string workItemName);
}

public sealed class BackgroundWorkSlot : IBackgroundWorkSlot
{
    private readonly BoundedBackgroundWorkQueue _owner;
    private int _state;

    internal BackgroundWorkSlot(BoundedBackgroundWorkQueue owner)
    {
        _owner = owner;
    }

    public void Queue(
        Func<IServiceProvider, CancellationToken, Task> workItem,
        string workItemName)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
        {
            throw new InvalidOperationException("Background work slot has already been used.");
        }

        try
        {
            _owner.QueueReservedWork(workItem, workItemName);
        }
        catch
        {
            Interlocked.Exchange(ref _state, 0);
            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) == 0)
        {
            _owner.ReleaseSlot();
        }
    }
}

public sealed class BoundedBackgroundWorkQueue : IBackgroundWorkQueue, IDisposable
{
    private const int DefaultMaxConcurrency = 4;
    private const int HardMaxConcurrency = 100;

    private readonly SemaphoreSlim _slots;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger<BoundedBackgroundWorkQueue> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _workCancellation =
        new(StringComparer.Ordinal);

    public BoundedBackgroundWorkQueue(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime applicationLifetime,
        ILogger<BoundedBackgroundWorkQueue> logger)
    {
        _scopeFactory = scopeFactory;
        _applicationLifetime = applicationLifetime;
        _logger = logger;
        MaxConcurrency = ResolveMaxConcurrency(configuration);
        _slots = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
    }

    public int MaxConcurrency { get; }

    public bool TryAcquire(out IBackgroundWorkSlot? slot)
    {
        if (!_slots.Wait(0))
        {
            slot = null;
            return false;
        }

        slot = new BackgroundWorkSlot(this);
        return true;
    }

    public bool TryCancel(string workItemName)
    {
        if (string.IsNullOrWhiteSpace(workItemName))
        {
            return false;
        }

        if (!_workCancellation.TryGetValue(workItemName, out var cts))
        {
            return false;
        }

        try
        {
            cts.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    internal void QueueReservedWork(
        Func<IServiceProvider, CancellationToken, Task> workItem,
        string workItemName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workItemName);

        // Link ApplicationStopping so deploy/shutdown and explicit cancel share one token.
        // Ownership transfers into the Task.Run using-block (not disposed on this method's return).
        var workCts = CancellationTokenSource.CreateLinkedTokenSource(
            _applicationLifetime.ApplicationStopping);

        if (!_workCancellation.TryAdd(workItemName, workCts))
        {
            // Duplicate names should not happen (IDs are unique). Fail closed so the
            // caller can mark the job Failed instead of running uncancellable work.
            workCts.Dispose();
            throw new InvalidOperationException(
                $"Background work item '{workItemName}' is already registered.");
        }

        _ = Task.Run(async () =>
        {
            using (workCts)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    await workItem(scope.ServiceProvider, workCts.Token);
                }
                catch (OperationCanceledException) when (
                    workCts.IsCancellationRequested ||
                    _applicationLifetime.ApplicationStopping.IsCancellationRequested)
                {
                    _logger.LogInformation(
                        "Background work item {WorkItemName} cancelled",
                        workItemName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background work item {WorkItemName} failed", workItemName);
                }
                finally
                {
                    _workCancellation.TryRemove(workItemName, out _);
                    ReleaseSlot();
                }
            }
        }, CancellationToken.None);
    }

    internal void ReleaseSlot()
    {
        _slots.Release();
    }

    public void Dispose()
    {
        foreach (var key in _workCancellation.Keys.ToArray())
        {
            if (!_workCancellation.TryRemove(key, out var cts))
            {
                continue;
            }

            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException exception)
            {
                _logger.LogDebug(
                    exception,
                    "Cancellation token source for work item {WorkItemName} was already disposed during queue disposal.",
                    key);
            }

            cts.Dispose();
        }

        _slots.Dispose();
    }

    private static int ResolveMaxConcurrency(IConfiguration configuration)
    {
        var configuredLimit = configuration.GetValue<int?>("BackgroundWork:MaxConcurrentWorkItems")
            ?? configuration.GetValue<int?>("Performance:Scalability:MaxConcurrentSimulations")
            ?? DefaultMaxConcurrency;

        return Math.Clamp(configuredLimit, 1, HardMaxConcurrency);
    }
}
