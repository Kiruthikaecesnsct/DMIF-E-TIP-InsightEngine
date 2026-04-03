using InsightEngine.Models.Queries;
using InsightEngine.Repositories;
using InsightEngine.Services;
using InsightEngine.Services.Agents;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace InsightEngine.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class QueriesController : ControllerBase
    {
        private readonly QueryInterpreterAgent _queryAgent;
        private readonly SqlExecutorService _sqlExecutor;
        private readonly IQueryRepository _queryRepository;
        private readonly IDataSourceRepository _dataSourceRepository;
        private readonly ILogger<QueriesController> _logger;

        public QueriesController(
            QueryInterpreterAgent queryAgent,
            SqlExecutorService sqlExecutor,
            IQueryRepository queryRepository,
            IDataSourceRepository dataSourceRepository,
            ILogger<QueriesController> logger)
        {
            _queryAgent = queryAgent;
            _sqlExecutor = sqlExecutor;
            _queryRepository = queryRepository;
            _dataSourceRepository = dataSourceRepository;
            _logger = logger;
        }

        // POST /api/v1/queries/ask
        [HttpPost("queries/ask")]
        public async Task<IActionResult> AskQuestion([FromBody] AskRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest("Question cannot be empty.");

            // 1. Save the incoming query request
            var queryRequest = new QueryRequest
            {
                NaturalLanguageQuery = request.Question,
                DataSourceId = request.DataSourceId,
                Status = QueryStatus.Processing
            };
            await _queryRepository.SaveQueryAsync(queryRequest);

            // 2. Get data source and schema
            var dataSource = await _dataSourceRepository
                .GetByIdAsync(request.DataSourceId);

            if (dataSource == null)
                return NotFound($"Data source '{request.DataSourceId}' not found.");

            // 3. Call Query Interpreter Agent
            var agentResult = await _queryAgent.InterpretQueryAsync(
                request.Question,
                dataSource.SchemaMetadata);

            // 4. Validate generated SQL
            if (!_queryAgent.ValidateQuery(agentResult.GeneratedSQL))
            {
                var errorResult = new QueryResult
                {
                    QueryRequestId = queryRequest.Id,
                    ErrorMessage = "Generated SQL failed safety validation.",
                    Confidence = agentResult.Confidence
                };
                await _queryRepository.SaveResultAsync(errorResult);
                return BadRequest(errorResult);
            }

            // 5. Execute query
            var execution = await _sqlExecutor.ExecuteQueryAsync(
                agentResult.GeneratedSQL,
                dataSource.ConnectionString);

            // 6. Build and save result
            var queryResult = new QueryResult
            {
                QueryRequestId = queryRequest.Id,
                GeneratedSQL = agentResult.GeneratedSQL,
                Confidence = agentResult.Confidence,
                TablesUsed = agentResult.TablesUsed,
                Explanation = agentResult.Explanation,
                ExecutionTimeMs = execution.ExecutionTimeMs,
                RowCount = execution.RowCount,
                RawResults = JsonSerializer.Serialize(execution.Rows),
                ErrorMessage = execution.ErrorMessage
            };

            await _queryRepository.SaveResultAsync(queryResult);

            return Ok(new
            {
                queryResult.Id,
                queryResult.GeneratedSQL,
                queryResult.Confidence,
                queryResult.Explanation,
                queryResult.TablesUsed,
                queryResult.ExecutionTimeMs,
                queryResult.RowCount,
                Results = execution.Rows,
                queryResult.ErrorMessage
            });
        }

        // GET /api/v1/queries/history
        [HttpGet("queries/history")]
        public async Task<IActionResult> GetHistory()
        {
            var queries = await _queryRepository
                .GetUserQueriesAsync("default-user");
            return Ok(queries);
        }

        // GET /api/v1/queries/{id}/results
        [HttpGet("queries/{id}/results")]
        public async Task<IActionResult> GetResults(string id)
        {
            var result = await _queryRepository.GetResultByRequestIdAsync(id);
            if (result == null)
                return NotFound();
            return Ok(result);
        }

        // POST /api/v1/datasources
        [HttpPost("datasources")]
        public async Task<IActionResult> CreateDataSource(
            [FromBody] InsightEngine.Models.DataSources.DataSource dataSource)
        {
            var created = await _dataSourceRepository.CreateAsync(dataSource);
            return Ok(created);
        }
    }

    public class AskRequest
    {
        public string Question { get; set; } = string.Empty;
        public string DataSourceId { get; set; } = string.Empty;
    }
}