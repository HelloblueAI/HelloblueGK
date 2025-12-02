using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace HB_NLP_Research_Lab.Core
{
    /// <summary>
    /// Advanced rate limiting service for API protection
    /// Implements sliding window and token bucket algorithms
    /// </summary>
    public class RateLimitingService
    {
        private readonly ILogger<RateLimitingService> _logger;
        private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets;
        private readonly Timer _cleanupTimer;

        public RateLimitingService(ILogger<RateLimitingService> logger)
        {
            _logger = logger;
            _buckets = new ConcurrentDictionary<string, RateLimitBucket>();
            
            // Clean up expired buckets every minute
            _cleanupTimer = new Timer(CleanupExpiredBuckets, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public async Task<RateLimitResult> CheckRateLimitAsync(string identifier, RateLimitPolicy policy)
        {
            var bucket = _buckets.GetOrAdd(identifier, _ => new RateLimitBucket(policy));
            
            var now = DateTime.UtcNow;
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

        public async Task<RateLimitResult> CheckRateLimitAsync(string identifier, int maxRequests, TimeSpan window)
        {
            var policy = new RateLimitPolicy
            {
                RequestsPerWindow = maxRequests,
                WindowSize = window,
                Algorithm = RateLimitAlgorithm.SlidingWindow
            };

            return await CheckRateLimitAsync(identifier, policy);
        }

        public Task<RateLimitStatus> GetRateLimitStatusAsync(string identifier)
        {
            if (!_buckets.TryGetValue(identifier, out var bucket))
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
            var result = bucket.CheckLimit(now);

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
            var activeBuckets = _buckets.Values.Where(b => b.IsActive(now)).ToList();

            var report = new RateLimitReport
            {
                GeneratedAt = now,
                TotalActiveBuckets = activeBuckets.Count,
                TotalBuckets = _buckets.Count,
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
            if (_buckets.TryRemove(identifier, out var bucket))
            {
                var sanitizedIdentifier = LogSanitizer.SanitizeIdentifier(identifier);
                _logger.LogInformation("Rate limit reset for {Identifier}", sanitizedIdentifier);
            }

            await Task.CompletedTask;
        }

        public async Task ResetAllRateLimitsAsync()
        {
            _buckets.Clear();
            _logger.LogInformation("All rate limits have been reset");
            await Task.CompletedTask;
        }

        private void CleanupExpiredBuckets(object? state)
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _buckets
                .Where(kvp => !kvp.Value.IsActive(now))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _buckets.TryRemove(key, out _);
            }

            if (expiredKeys.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} expired rate limit buckets", expiredKeys.Count);
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

        public RateLimitBucket(RateLimitPolicy policy)
        {
            Policy = policy;
            Identifier = Guid.NewGuid().ToString();
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
