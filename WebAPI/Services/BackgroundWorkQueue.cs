namespace HB_NLP_Research_Lab.WebAPI.Services;

public interface IBackgroundWorkQueue
{
    int MaxConcurrency { get; }
    bool TryAcquire(out BackgroundWorkSlot? slot);
}

public sealed class BackgroundWorkSlot : IDisposable
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

    public bool TryAcquire(out BackgroundWorkSlot? slot)
    {
        if (!_slots.Wait(0))
        {
            slot = null;
            return false;
        }

        slot = new BackgroundWorkSlot(this);
        return true;
    }

    internal void QueueReservedWork(
        Func<IServiceProvider, CancellationToken, Task> workItem,
        string workItemName)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await workItem(scope.ServiceProvider, _applicationLifetime.ApplicationStopping);
            }
            catch (OperationCanceledException) when (_applicationLifetime.ApplicationStopping.IsCancellationRequested)
            {
                _logger.LogInformation("Background work item {WorkItemName} cancelled during application shutdown", workItemName);
            }
            catch (Exception ex) when (LogBackgroundWorkFailure(ex, workItemName))
            {
            }
            finally
            {
                ReleaseSlot();
            }
        }, CancellationToken.None);
    }

    private bool LogBackgroundWorkFailure(Exception exception, string workItemName)
    {
        _logger.LogError(exception, "Background work item {WorkItemName} failed", workItemName);
        return true;
    }

    internal void ReleaseSlot()
    {
        _slots.Release();
    }

    public void Dispose()
    {
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
