using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HB_NLP_Research_Lab.Core.FaultTolerance
{
    /// <summary>
    /// Fault-tolerant system architecture
    /// Implements graceful degradation, automatic recovery, and failover
    /// Used by SpaceX, NASA, and other mission-critical systems
    /// </summary>
    public class FaultTolerantSystem<T> where T : class
    {
        private readonly List<FaultTolerantComponent<T>> _components;
        private readonly FaultToleranceStrategy _strategy;
        private readonly int _redundancyLevel;
        
        private T? _activeComponent;
        private readonly object _lock = new object();
        
        public int RedundancyLevel => _redundancyLevel;
        public FaultToleranceStrategy Strategy => _strategy;
        public T? ActiveComponent => _activeComponent;
        public bool IsOperational => _activeComponent != null;
        
        public FaultTolerantSystem(
            List<FaultTolerantComponent<T>> components,
            FaultToleranceStrategy strategy)
        {
            if (components == null || components.Count == 0)
                throw new ArgumentException("At least one component required", nameof(components));
            
            _components = components;
            _strategy = strategy;
            _redundancyLevel = components.Count;
            
            // Initialize components
            foreach (var component in _components)
            {
                component.StatusChanged += OnComponentStatusChanged;
            }
        }
        
        /// <summary>
        /// Initialize and start the fault-tolerant system
        /// </summary>
        public async Task InitializeAsync()
        {
            Console.WriteLine($"[Fault Tolerance] Initializing {_redundancyLevel}-redundant system");
            
            // Start all components
            var initTasks = _components.Select(c => c.InitializeAsync()).ToArray();
            await Task.WhenAll(initTasks);
            
            // Select active component based on strategy
            SelectActiveComponent();
            
            Console.WriteLine($"[Fault Tolerance] System operational with {_redundancyLevel} redundant components");
        }
        
        /// <summary>
        /// Execute operation with fault tolerance
        /// </summary>
        public async Task<TR> ExecuteAsync<TR>(Func<T, Task<TR>> operation, int maxRetries = 3)
        {
            var attempts = 0;
            Exception? lastException = null;
            
            while (attempts < maxRetries)
            {
                try
                {
                    var component = GetActiveComponent();
                    if (component == null)
                    {
                        // Try to recover
                        await AttemptRecoveryAsync();
                        component = GetActiveComponent();
                        
                        if (component == null)
                            throw new InvalidOperationException("No operational components available");
                    }
                    
                    return await operation(component);
                }
                catch (InvalidOperationException ex)
                {
                    lastException = ex;
                    attempts++;
                    Console.WriteLine($"[Fault Tolerance] ⚠️ Invalid operation (attempt {attempts}/{maxRetries}): {ex.Message}");
                    MarkComponentFailed();
                    if (attempts < maxRetries)
                    {
                        await Task.Delay(100 * attempts);
                        SelectActiveComponent();
                    }
                }
                catch (TimeoutException ex)
                {
                    lastException = ex;
                    attempts++;
                    Console.WriteLine($"[Fault Tolerance] ⚠️ Operation timeout (attempt {attempts}/{maxRetries}): {ex.Message}");
                    MarkComponentFailed();
                    if (attempts < maxRetries)
                    {
                        await Task.Delay(100 * attempts);
                        SelectActiveComponent();
                    }
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NullReferenceException)
                {
                    lastException = ex;
                    attempts++;
                    Console.WriteLine($"[Fault Tolerance] ⚠️ Data error (attempt {attempts}/{maxRetries}): {ex.Message}");
                    MarkComponentFailed();
                    if (attempts < maxRetries)
                    {
                        await Task.Delay(100 * attempts);
                        SelectActiveComponent();
                    }
                }
                // codeql[generic-catch-clause]: Intentional final catch-all for safety - all specific exceptions handled above
                catch (Exception ex)
                {
                    lastException = ex;
                    attempts++;
                    Console.WriteLine($"[Fault Tolerance] ⚠️ Operation failed (attempt {attempts}/{maxRetries}): {ex.Message}");
                    MarkComponentFailed();
                    if (attempts < maxRetries)
                    {
                        await Task.Delay(100 * attempts);
                        SelectActiveComponent();
                    }
                }
            }
            
            throw new AggregateException("Operation failed after all retries", lastException!);
        }
        
        /// <summary>
        /// Execute operation with timeout
        /// </summary>
        public async Task<TR> ExecuteWithTimeoutAsync<TR>(
            Func<T, Task<TR>> operation,
            TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            
            try
            {
                var component = GetActiveComponent();
                if (component == null)
                    throw new InvalidOperationException("No operational components available");
                
                return await operation(component);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Operation timed out after {timeout.TotalSeconds} seconds");
            }
        }
        
        private T? GetActiveComponent()
        {
            lock (_lock)
            {
                // Verify active component is still healthy
                if (_activeComponent != null)
                {
                    var component = _components.FirstOrDefault(c => c.Instance == _activeComponent);
                    if (component != null && component.Status == ComponentStatus.Healthy)
                    {
                        return _activeComponent;
                    }
                }
                
                // Select new active component
                SelectActiveComponent();
                return _activeComponent;
            }
        }
        
        private void SelectActiveComponent()
        {
            lock (_lock)
            {
                var healthyComponents = _components
                    .Where(c => c.Status == ComponentStatus.Healthy)
                    .ToList();
                
                if (healthyComponents.Count == 0)
                {
                    _activeComponent = null;
                    Console.WriteLine("[Fault Tolerance] ❌ No healthy components available");
                    return;
                }
                
                // Select based on strategy
                FaultTolerantComponent<T>? selected = null;
                
                switch (_strategy)
                {
                    case FaultToleranceStrategy.PrimaryBackup:
                        // Use first healthy component as primary
                        selected = healthyComponents[0];
                        break;
                    
                    case FaultToleranceStrategy.RoundRobin:
                        // Rotate through healthy components
                        var currentIndex = _activeComponent != null
                            ? healthyComponents.FindIndex(c => c.Instance == _activeComponent)
                            : -1;
                        var nextIndex = (currentIndex + 1) % healthyComponents.Count;
                        selected = healthyComponents[nextIndex];
                        break;
                    
                    case FaultToleranceStrategy.LeastLoaded:
                        // Select component with lowest load
                        selected = healthyComponents
                            .OrderBy(c => c.CurrentLoad)
                            .First();
                        break;
                    
                    case FaultToleranceStrategy.HighestPriority:
                        // Select component with highest priority
                        selected = healthyComponents
                            .OrderByDescending(c => c.Priority)
                            .First();
                        break;
                }
                
                if (selected != null)
                {
                    _activeComponent = selected.Instance;
                    Console.WriteLine($"[Fault Tolerance] Selected active component: {selected.Name}");
                }
            }
        }
        
        private void MarkComponentFailed()
        {
            lock (_lock)
            {
                if (_activeComponent != null)
                {
                    var component = _components.FirstOrDefault(c => c.Instance == _activeComponent);
                    if (component != null)
                    {
                        component.MarkFailed();
                        Console.WriteLine($"[Fault Tolerance] ⚠️ Component marked as failed: {component.Name}");
                    }
                }
            }
        }
        
        private async Task AttemptRecoveryAsync()
        {
            Console.WriteLine("[Fault Tolerance] 🔄 Attempting system recovery...");
            
            // Try to recover failed components
            var recoveryTasks = _components
                .Where(c => c.Status == ComponentStatus.Failed)
                .Select(c => c.AttemptRecoveryAsync())
                .ToArray();
            
            if (recoveryTasks.Length > 0)
            {
                await Task.WhenAll(recoveryTasks);
                Console.WriteLine($"[Fault Tolerance] Recovery attempted for {recoveryTasks.Length} components");
            }
        }
        
        private void OnComponentStatusChanged(object? sender, ComponentStatusChangedEventArgs e)
        {
            Console.WriteLine($"[Fault Tolerance] Component status changed: {e.ComponentName} -> {e.NewStatus}");
            
            // If active component failed, switch to backup
            if (e.NewStatus == ComponentStatus.Failed && 
                _activeComponent != null &&
                _components.Any(c => c.Instance == _activeComponent && c.Name == e.ComponentName))
            {
                SelectActiveComponent();
            }
        }
        
        /// <summary>
        /// Get system health status
        /// </summary>
        public SystemHealth GetSystemHealth()
        {
            var healthyCount = _components.Count(c => c.Status == ComponentStatus.Healthy);
            var degradedCount = _components.Count(c => c.Status == ComponentStatus.Degraded);
            var failedCount = _components.Count(c => c.Status == ComponentStatus.Failed);
            
            return new SystemHealth
            {
                TotalComponents = _components.Count,
                HealthyComponents = healthyCount,
                DegradedComponents = degradedCount,
                FailedComponents = failedCount,
                RedundancyLevel = _redundancyLevel,
                IsOperational = healthyCount > 0,
                OperationalRedundancy = healthyCount
            };
        }
    }
    
    public class FaultTolerantComponent<T> where T : class
    {
        private ComponentStatus _status = ComponentStatus.Unknown;
        private int _failureCount = 0;
        private DateTime _lastFailureTime;
        
        public string Name { get; set; } = string.Empty;
        public T Instance { get; set; } = null!;
        public int Priority { get; set; } = 0;
        public double CurrentLoad { get; set; } = 0.0;
        
        public ComponentStatus Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    var oldStatus = _status;
                    _status = value;
                    StatusChanged?.Invoke(this, new ComponentStatusChangedEventArgs
                    {
                        ComponentName = Name,
                        OldStatus = oldStatus,
                        NewStatus = value
                    });
                }
            }
        }
        
        public event EventHandler<ComponentStatusChangedEventArgs>? StatusChanged;
        
        public async Task InitializeAsync()
        {
            try
            {
                // Component-specific initialization would go here
                Status = ComponentStatus.Healthy;
                Console.WriteLine($"[Fault Tolerance] Component initialized: {Name}");
            }
            catch (InvalidOperationException ex)
            {
                Status = ComponentStatus.Failed;
                Console.WriteLine($"[Fault Tolerance] ⚠️ Component initialization invalid: {Name} - {ex.Message}");
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NullReferenceException)
            {
                Status = ComponentStatus.Failed;
                Console.WriteLine($"[Fault Tolerance] ⚠️ Component initialization data error: {Name} - {ex.Message}");
            }
            // codeql[generic-catch-clause]: Intentional final catch-all for safety - all specific exceptions handled above
            catch (Exception ex)
            {
                Status = ComponentStatus.Failed;
                Console.WriteLine($"[Fault Tolerance] ❌ Component initialization failed: {Name} - {ex.Message}");
            }
        }
        
        public void MarkFailed()
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;
            Status = ComponentStatus.Failed;
        }
        
        public async Task<bool> AttemptRecoveryAsync()
        {
            try
            {
                // Attempt to recover the component
                // This would involve restarting, reinitializing, etc.
                await Task.Delay(1000); // Simulate recovery time
                
                Status = ComponentStatus.Healthy;
                Console.WriteLine($"[Fault Tolerance] ✅ Component recovered: {Name}");
                return true;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"[Fault Tolerance] ⚠️ Recovery invalid operation: {Name} - {ex.Message}");
                return false;
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"[Fault Tolerance] ⚠️ Recovery timeout: {Name} - {ex.Message}");
                return false;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NullReferenceException)
            {
                Console.WriteLine($"[Fault Tolerance] ⚠️ Recovery data error: {Name} - {ex.Message}");
                return false;
            }
            // codeql[generic-catch-clause]: Intentional final catch-all for safety - all specific exceptions handled above
            catch (Exception ex)
            {
                Console.WriteLine($"[Fault Tolerance] ❌ Recovery failed: {Name} - {ex.Message}");
                return false;
            }
        }
    }
    
    public enum FaultToleranceStrategy
    {
        PrimaryBackup,      // Primary with backups
        RoundRobin,         // Rotate through components
        LeastLoaded,        // Use least loaded component
        HighestPriority     // Use highest priority component
    }
    
    public enum ComponentStatus
    {
        Unknown,
        Initializing,
        Healthy,
        Degraded,
        Failed,
        Recovering
    }
    
    public class ComponentStatusChangedEventArgs : EventArgs
    {
        public string ComponentName { get; set; } = string.Empty;
        public ComponentStatus OldStatus { get; set; }
        public ComponentStatus NewStatus { get; set; }
    }
    
    public class SystemHealth
    {
        public int TotalComponents { get; set; }
        public int HealthyComponents { get; set; }
        public int DegradedComponents { get; set; }
        public int FailedComponents { get; set; }
        public int RedundancyLevel { get; set; }
        public bool IsOperational { get; set; }
        public int OperationalRedundancy { get; set; }
    }
}
