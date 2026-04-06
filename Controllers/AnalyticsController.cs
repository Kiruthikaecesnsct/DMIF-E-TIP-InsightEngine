using InsightEngine.Repositories;
using InsightEngine.Services;
using InsightEngine.Services.Orchestration;
using Microsoft.AspNetCore.Mvc;

namespace InsightEngine.Controllers
{
    public class AnalysisAskRequest
    {
        public string Question { get; set; } = string.Empty;
        public string DataSourceId { get; set; } = string.Empty;
        public bool SkipCache { get; set; } = false;
    }

    [ApiController]
    [Route("api/v1/analysis")]
    public class AnalysisController : ControllerBase
    {
        private readonly AnalysisPipelineService _pipeline;
        private readonly ResultCacheService _cache;
        private readonly IDataSourceRepository _dataSourceRepo;
        private readonly IQueryRepository _queryRepo;
        private readonly ILogger<AnalysisController> _logger;

        public AnalysisController(
            AnalysisPipelineService pipeline,
            ResultCacheService cache,
            IDataSourceRepository dataSourceRepo,
            IQueryRepository queryRepo,
            ILogger<AnalysisController> logger)
        {
            _pipeline = pipeline;
            _cache = cache;
            _dataSourceRepo = dataSourceRepo;
            _queryRepo = queryRepo;
            _logger = logger;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] AnalysisAskRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest(new { error = "Question is required." });

            if (string.IsNullOrWhiteSpace(request.DataSourceId))
                return BadRequest(new { error = "DataSourceId is required." });

            var dataSource = await _dataSourceRepo.GetByIdAsync(request.DataSourceId);
            if (dataSource == null)
                return NotFound(new { error = "Data source not found." });

            if (!request.SkipCache)
            {
                var cacheKey = _cache.GenerateCacheKey(
                    request.Question, request.DataSourceId);
                var cached = _cache.GetCachedResult(cacheKey);
                if (cached != null)
                {
                    return Ok(new
                    {
                        fromCache = true,
                        cached.Success,
                        cached.GeneratedSQL,
                        cached.RowCount,
                        cached.Statistics,
                        cached.ChartConfig,
                        data = cached.Data,
                        cached.ExecutionTimeMs,
                        trends = cached.Trends,
                        outliers = cached.Outliers,
                        anomalies = cached.Anomalies
                    });
                }
            }

            var result = await _pipeline.ExecuteAsync(request.Question, dataSource);

            if (!result.Success)
                return UnprocessableEntity(new { error = result.ErrorMessage });

            var key = _cache.GenerateCacheKey(request.Question, request.DataSourceId);
            _cache.CacheResult(key, result);

            return Ok(new
            {
                fromCache = false,
                result.Success,
                result.GeneratedSQL,
                result.RowCount,
                result.Statistics,
                result.ChartConfig,
                data = result.Data,
                result.ExecutionTimeMs,
                trends = result.Trends,
                outliers = result.Outliers,
                anomalies = result.Anomalies
            });
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var all = await _queryRepo.GetRecentQueriesAsync(page * pageSize);
            var paged = all
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new
                {
                    q.Id,
                    q.NaturalLanguageQuery,
                    q.CreatedDate,
                    q.Status
                });

            return Ok(new { page, pageSize, results = paged });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var result = await _queryRepo.GetByIdAsync(id);
            if (result == null)
                return NotFound(new { error = "Analysis not found." });
            return Ok(result);
        }
    }
}