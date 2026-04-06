using InsightEngine.Services.Orchestration;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;

namespace InsightEngine.Services
{
    public class ResultCacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<ResultCacheService> _logger;

        public ResultCacheService(IMemoryCache cache, ILogger<ResultCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public PipelineResult? GetCachedResult(string cacheKey)
        {
            if (_cache.TryGetValue(cacheKey, out PipelineResult? result))
            {
                _logger.LogInformation("Cache hit for key: {Key}", cacheKey);
                return result;
            }
            _logger.LogInformation("Cache miss for key: {Key}", cacheKey);
            return null;
        }

        public void CacheResult(string cacheKey, PipelineResult result,
            TimeSpan? duration = null)
        {
            var expiry = duration ?? TimeSpan.FromMinutes(10);

            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry,
                SlidingExpiration = TimeSpan.FromMinutes(2)
            };

            _cache.Set(cacheKey, result, options);
            _logger.LogInformation(
                "Cached result for key: {Key}, expires in {Minutes} min",
                cacheKey, expiry.TotalMinutes);
        }

        public string GenerateCacheKey(string question, string dataSourceId)
        {
            var raw = $"{question.Trim().ToLowerInvariant()}|{dataSourceId}|{DateTime.UtcNow:yyyy-MM-dd}";
            var hash = Convert.ToHexString(
                MD5.HashData(Encoding.UTF8.GetBytes(raw)))[..8];
            return $"pipeline_{hash}_{dataSourceId}";
        }

        public TimeSpan GetCacheDuration(string dataSourceRefreshFrequency)
        {
            return dataSourceRefreshFrequency?.ToLower() switch
            {
                "realtime" => TimeSpan.FromMinutes(1),
                "hourly" => TimeSpan.FromMinutes(10),
                "daily" => TimeSpan.FromHours(1),
                "historical" => TimeSpan.FromHours(24),
                _ => TimeSpan.FromMinutes(10)
            };
        }
    }
}