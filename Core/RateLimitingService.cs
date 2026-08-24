using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace HB_NLP_Research_Lab.Core
{
    /// <summary>
    /// Advanced rate limiting service for API protection
    /// Implements sliding window and token bucket algorithms
    /// </summary>
    public class RateLimitingService : IDisposable
    {
        private const int DefaultMaxTrackedIdentifiers = 10000;
        private const int DefaultMaxTestIdentifiers = 64;
        private const string TestBucketPrefix = "test:";
        private const string AuthBucketPrefix = "Auth:";
        private const string AuthUsernameBucketPrefix = "AuthUsername:";
        private const string PreAuthBucketPrefix = "PreAuth:";

        private readonly ILogger<RateLimitingService> _logger;
        private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets;
        private readonly ConcurrentDictionary<string, RateLimitBucket> _authBuckets;
        private readonly ConcurrentDictionary<string, RateLimitBucket> _testBuckets;
        private readonly Timer _cleanupTimer;
        private readonly object _bucketCreationLock = new();
        private readonly object _authBucketCreationLock = new();
        private readonly object _testBucketCreationLock = new();
        private readonly int _maxTrackedIdentifiers;
        private readonly int _maxAuthTrackedIdentifiers;
        private readonly int _maxTestIdentifiers;

        public RateLimitingService(ILogger<RateLimitingService> logger)
            : this(logger, DefaultMaxTrackedIdentifiers)
        {
        }

        public RateLimitingService(ILogger<RateLimitingService> logger, int maxTrackedIdentifiers)
            : this(logger, maxTrackedIdentifiers, DefaultMaxTestIdentifiers)
        {
        }

        public RateLimitingService(ILogger<RateLimitingService> logger, int maxTrackedIdentifiers, int maxTestIdentifiers)
            : this(logger, maxTrackedIdentifiers, maxTestIdentifiers, maxTrackedIdentifiers)
        {
        }

        public RateLimitingService(
            ILogger<RateLimitingService> logger,
            int maxTrackedIdentifiers,
            int maxTestIdentifiers,
            int maxAuthTrackedIdentifiers)
        {
            _logger = logger;
            _maxTrackedIdentifiers = maxTrackedIdentifiers > 0
                ? maxTrackedIdentifiers
                : throw new ArgumentOutOfRangeException(nameof(maxTrackedIdentifiers), "Maximum tracked identifiers must be greater than zero.");
            _maxTestIdentifiers = maxTestIdentifiers > 0
                ? maxTestIdentifiers
                : throw new ArgumentOutOfRangeException(nameof(maxTestIdentifiers), "Maximum test identifiers must be greater than zero.");
            _maxAuthTrackedIdentifiers = maxAuthTrackedIdentifiers > 0
                ? maxAuthTrackedIdentifiers
                : throw new ArgumentOutOfRangeException(nameof(maxAuthTrackedIdentifiers), "Maximum auth tracked identifiers must be greater than zero.");
            _buckets = new ConcurrentDictionary<string, RateLimitBucket>();
            _authBuckets = new ConcurrentDictionary<string, RateLimitBucket>();
            _testBuckets = new ConcurrentDictionary<string, RateLimitBucket>();
            
            // Clean up expired buckets every minute
            _cleanupTimer = new Timer(CleanupExpiredBuckets, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public virtual async Task<RateLimitResult> CheckRateLimitAsync(string identifier, RateLimitPolicy policy)
        {
            var now = DateTime.UtcNow;
            var isAuthIdentifier = IsAuthRateLimitIdentifier(identifier);
            var bucket = GetOrCreateBucket(identifier, policy, now);
            if (bucket == null)
            {
                _logger.LogWarning(
                    "Rate limit bucket capacity reached. Blocking new identifier {Identifier}. Capacity: {Capacity} Pool: {Pool}",
                    LogSanitizer.SanitizeIdentifier(identifier),
                    isAuthIdentifier ? _maxAuthTrackedIdentifiers : _maxTrackedIdentifiers,
                    isAuthIdentifier ? "auth" : "api");

                return await Task.FromResult(new RateLimitResult
                {
                    IsAllowed = false,
                    RemainingRequests = 0,
                    ResetTime = now.Add(policy.WindowSize),
                    TotalRequests = policy.RequestsPerWindow,
                    Message = "Rate limit capacity reached"
                });
            }

            var result = bucket.CheckLimit(now);
            
            var sanitizedIdentifier = LogSanitizer.SanitizeIdentifier(identifier);
            if (result.IsAllowed)
            {
                _logger.LogDebug("Rate limit check passed for {Identifier}", sanitizedIdentifier);
            }
            else
            {
                _logger.LogWarning("Rate limit exceeded for {Identifier}. Limit: {Limit}, Remaining: {Remaining}, ResetAt: {ResetAt}", 
                    sanitizedIdentifier, policy.RequestsPerWindow, result.RemainingRequests, result.ResetTime);
            }

            return await Task.FromResult(result);
        }

        public virtual async Task<RateLimitResult> CheckRateLimitAsync(string identifier, int maxRequests, TimeSpan window)
        {
            var policy = new RateLimitPolicy
            {
                RequestsPerWindow = maxRequests,
                WindowSize = window,
                Algorithm = RateLimitAlgorithm.SlidingWindow
            };

            return await CheckRateLimitAsync(identifier, policy);
        }

        /// <summary>
        /// Admin/test-only rate-limit check that uses an isolated bucket store.
        /// Never creates or mutates production auth/API buckets, so /RateLimit/test
        /// cannot exhaust the global identifier capacity or pollute live client state.
        /// </summary>
        public virtual Task<RateLimitResult> CheckTestRateLimitAsync(string identifier, RateLimitPolicy policy)
        {
            ArgumentNullException.ThrowIfNull(policy);
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("Identifier is required.", nameof(identifier));
            }

            var now = DateTime.UtcNow;
            var testKey = TestBucketPrefix + identifier.Trim();
            var bucket = GetOrCreateTestBucket(testKey, policy, now);
            if (bucket == null)
            {
                _logger.LogWarning(
                    "Test rate limit bucket capacity reached. Blocking test identifier {Identifier}. Capacity: {Capacity}",
                    LogSanitizer.SanitizeIdentifier(identifier),
                    _maxTestIdentifiers);

                return Task.FromResult(new RateLimitResult
                {
                    IsAllowed = false,
                    RemainingRequests = 0,
                    ResetTime = now.Add(policy.WindowSize),
                    TotalRequests = policy.RequestsPerWindow,
                    Message = "Test rate limit capacity reached"
                });
            }

            return Task.FromResult(bucket.CheckLimit(now));
        }

        public Task<RateLimitStatus> GetRateLimitStatusAsync(string identifier)
        {
            if (!TryGetTrackedBucket(identifier, out var bucket) || bucket == null)
            {
                return Task.FromResult(new RateLimitStatus
                {
                    Identifier = identifier,
                    IsActive = false,
                    RemainingRequests = 0,
                    ResetTime = DateTime.UtcNow
                });
            }

            var now = DateTime.UtcNow;
            var result = bucket.PeekLimit(now);

            return Task.FromResult(new RateLimitStatus
            {
                Identifier = identifier,
                IsActive = true,
                RemainingRequests = result.RemainingRequests,
                ResetTime = result.ResetTime,
                TotalRequests = result.TotalRequests,
                Policy = bucket.Policy
            });
        }

        public async Task<RateLimitReport> GenerateReportAsync()
        {
            var now = DateTime.UtcNow;
            var activeBuckets = _buckets.Values
                .Concat(_authBuckets.Values)
                .Where(b => b.IsActive(now))
                .ToList();

            var report = new RateLimitReport
            {
                GeneratedAt = now,
                TotalActiveBuckets = activeBuckets.Count,
                TotalBuckets = _buckets.Count + _authBuckets.Count,
                BlockedRequests = activeBuckets.Sum(b => b.BlockedRequests),
                AllowedRequests = activeBuckets.Sum(b => b.AllowedRequests),
                TopBlockedIdentifiers = activeBuckets
                    .OrderByDescending(b => b.BlockedRequests)
                    .Take(10)
                    .Select(b => new RateLimitIdentifier
                    {
                        Identifier = b.Identifier,
                        BlockedRequests = b.BlockedRequests,
                        AllowedRequests = b.AllowedRequests,
                        LastActivity = b.LastActivity
                    })
                    .ToList()
            };

            return await Task.FromResult(report);
        }

        public async Task ResetRateLimitAsync(string identifier)
        {
            var removed = _buckets.TryRemove(identifier, out _)
                || _authBuckets.TryRemove(identifier, out _);
            if (removed)
            {
                var sanitizedIdentifier = LogSanitizer.SanitizeIdentifier(identifier);
                _logger.LogInformation("Rate limit reset for {Identifier}", sanitizedIdentifier);
            }

            await Task.CompletedTask;
        }

        public async Task ResetAllRateLimitsAsync()
        {
            _buckets.Clear();
            _authBuckets.Clear();
            _testBuckets.Clear();
            _logger.LogInformation("All rate limits have been reset");
            await Task.CompletedTask;
        }

        public static bool IsAuthRateLimitIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return false;
            }

            return identifier.StartsWith(AuthBucketPrefix, StringComparison.OrdinalIgnoreCase)
                || identifier.StartsWith(AuthUsernameBucketPrefix, StringComparison.OrdinalIgnoreCase)
                || identifier.StartsWith(PreAuthBucketPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private bool TryGetTrackedBucket(string identifier, out RateLimitBucket? bucket)
        {
            if (IsAuthRateLimitIdentifier(identifier))
            {
                return _authBuckets.TryGetValue(identifier, out bucket);
            }

            return _buckets.TryGetValue(identifier, out bucket);
        }

        private RateLimitBucket? GetOrCreateBucket(string identifier, RateLimitPolicy policy, DateTime now)
        {
            if (IsAuthRateLimitIdentifier(identifier))
            {
                return GetOrCreateAuthBucket(identifier, policy, now);
            }

            if (_buckets.TryGetValue(identifier, out var existingBucket))
            {
                return existingBucket;
            }

            lock (_bucketCreationLock)
            {
                if (_buckets.TryGetValue(identifier, out existingBucket))
                {
                    return existingBucket;
                }

                if (_buckets.Count >= _maxTrackedIdentifiers)
                {
                    CleanupExpiredApiBuckets(now);
                    if (_buckets.Count >= _maxTrackedIdentifiers)
                    {
                        return null;
                    }
                }

                var bucket = new RateLimitBucket(identifier, policy);
                return _buckets.TryAdd(identifier, bucket) ? bucket : _buckets[identifier];
            }
        }

        private RateLimitBucket? GetOrCreateAuthBucket(string identifier, RateLimitPolicy policy, DateTime now)
        {
            if (_authBuckets.TryGetValue(identifier, out var existingBucket))
            {
                return existingBucket;
            }

            lock (_authBucketCreationLock)
            {
                if (_authBuckets.TryGetValue(identifier, out existingBucket))
                {
                    return existingBucket;
                }

                if (_authBuckets.Count >= _maxAuthTrackedIdentifiers)
                {
                    CleanupExpiredAuthBuckets(now);
                    if (_authBuckets.Count >= _maxAuthTrackedIdentifiers)
                    {
                        return null;
                    }
                }

                var bucket = new RateLimitBucket(identifier, policy);
                return _authBuckets.TryAdd(identifier, bucket) ? bucket : _authBuckets[identifier];
            }
        }

        private RateLimitBucket? GetOrCreateTestBucket(string identifier, RateLimitPolicy policy, DateTime now)
        {
            if (_testBuckets.TryGetValue(identifier, out var existingBucket))
            {
                return existingBucket;
            }

            lock (_testBucketCreationLock)
            {
                if (_testBuckets.TryGetValue(identifier, out existingBucket))
                {
                    return existingBucket;
                }

                if (_testBuckets.Count >= _maxTestIdentifiers)
                {
                    CleanupExpiredTestBuckets(now);
                    if (_testBuckets.Count >= _maxTestIdentifiers)
                    {
                        return null;
                    }
                }

                var bucket = new RateLimitBucket(identifier, policy);
                return _testBuckets.TryAdd(identifier, bucket) ? bucket : _testBuckets[identifier];
            }
        }

        private void CleanupExpiredBuckets(object? state)
        {
            var now = state is DateTime cleanupTime ? cleanupTime : DateTime.UtcNow;
            var expiredApi = CleanupExpiredApiBuckets(now);
            var expiredAuth = CleanupExpiredAuthBuckets(now);
            CleanupExpiredTestBuckets(now);

            var expiredCount = expiredApi + expiredAuth;
            if (expiredCount > 0)
            {
                _logger.LogDebug("Cleaned up {Count} expired rate limit buckets", expiredCount);
            }
        }

        private int CleanupExpiredApiBuckets(DateTime now)
        {
            var expiredKeys = _buckets
                .Where(kvp => !kvp.Value.IsActive(now))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _buckets.TryRemove(key, out _);
            }

            return expiredKeys.Count;
        }

        private int CleanupExpiredAuthBuckets(DateTime now)
        {
            var expiredKeys = _authBuckets
                .Where(kvp => !kvp.Value.IsActive(now))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _authBuckets.TryRemove(key, out _);
            }

            return expiredKeys.Count;
        }

        private void CleanupExpiredTestBuckets(DateTime now)
        {
            var expiredKeys = _testBuckets
                .Where(kvp => !kvp.Value.IsActive(now))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _testBuckets.TryRemove(key, out _);
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }

    public class RateLimitBucket
    {
        public string Identifier { get; private set; }
        public RateLimitPolicy Policy { get; private set; }
        public long AllowedRequests { get; private set; }
        public long BlockedRequests { get; private set; }
        public DateTime LastActivity { get; private set; }

        private readonly Queue<DateTime> _requestTimes;
        private readonly object _lock = new object();

        public RateLimitBucket(string identifier, RateLimitPolicy policy)
        {
            Policy = policy;
            Identifier = identifier;
            _requestTimes = new Queue<DateTime>();
            LastActivity = DateTime.UtcNow;
        }

        public RateLimitResult CheckLimit(DateTime now)
        {
            lock (_lock)
            {
                LastActivity = now;

                // Clean up old requests outside the window
                CleanupOldRequests(now);

                // Check if we can allow the request
                if (_requestTimes.Count < Policy.RequestsPerWindow)
                {
                    _requestTimes.Enqueue(now);
                    AllowedRequests++;
                    
                    return new RateLimitResult
                    {
                        IsAllowed = true,
                        RemainingRequests = Policy.RequestsPerWindow - _requestTimes.Count,
                        ResetTime = CalculateResetTime(now),
                        TotalRequests = _requestTimes.Count
                    };
                }
                else
                {
                    BlockedRequests++;
                    
                    return new RateLimitResult
                    {
                        IsAllowed = false,
                        RemainingRequests = 0,
                        ResetTime = CalculateResetTime(now),
                        TotalRequests = _requestTimes.Count
                    };
                }
            }
        }

        public RateLimitResult PeekLimit(DateTime now)
        {
            lock (_lock)
            {
                CleanupOldRequests(now);
                var totalRequests = _requestTimes.Count;
                var remainingRequests = Math.Max(Policy.RequestsPerWindow - totalRequests, 0);

                return new RateLimitResult
                {
                    IsAllowed = totalRequests < Policy.RequestsPerWindow,
                    RemainingRequests = remainingRequests,
                    ResetTime = CalculateResetTime(now),
                    TotalRequests = totalRequests
                };
            }
        }

        public bool IsActive(DateTime now)
        {
            lock (_lock)
            {
                return now - LastActivity < Policy.WindowSize.Add(TimeSpan.FromMinutes(5));
            }
        }

        private void CleanupOldRequests(DateTime now)
        {
            var cutoff = now - Policy.WindowSize;
            while (_requestTimes.Count > 0 && _requestTimes.Peek() < cutoff)
            {
                _requestTimes.Dequeue();
            }
        }

        private DateTime CalculateResetTime(DateTime now)
        {
            if (_requestTimes.Count == 0)
            {
                return now.Add(Policy.WindowSize);
            }

            var oldestRequest = _requestTimes.Peek();
            return oldestRequest.Add(Policy.WindowSize);
        }
    }

    public class RateLimitPolicy
    {
        public int RequestsPerWindow { get; set; } = 100;
        public TimeSpan WindowSize { get; set; } = TimeSpan.FromMinutes(1);
        public RateLimitAlgorithm Algorithm { get; set; } = RateLimitAlgorithm.SlidingWindow;
        public bool BlockOnLimit { get; set; } = true;
        public TimeSpan? BlockDuration { get; set; }
    }

    public enum RateLimitAlgorithm
    {
        SlidingWindow,
        TokenBucket,
        FixedWindow
    }

    public class RateLimitResult
    {
        public bool IsAllowed { get; set; }
        public int RemainingRequests { get; set; }
        public DateTime ResetTime { get; set; }
        public int TotalRequests { get; set; }
        public string? Message { get; set; }
    }

    public class RateLimitStatus
    {
        public string Identifier { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int RemainingRequests { get; set; }
        public DateTime ResetTime { get; set; }
        public int TotalRequests { get; set; }
        public RateLimitPolicy? Policy { get; set; }
    }

    public class RateLimitReport
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalActiveBuckets { get; set; }
        public int TotalBuckets { get; set; }
        public long BlockedRequests { get; set; }
        public long AllowedRequests { get; set; }
        public List<RateLimitIdentifier> TopBlockedIdentifiers { get; set; } = new();
    }

    public class RateLimitIdentifier
    {
        public string Identifier { get; set; } = string.Empty;
        public long BlockedRequests { get; set; }
        public long AllowedRequests { get; set; }
        public DateTime LastActivity { get; set; }
    }
}
