using InsightEngine.Models.Agents;
using InsightEngine.Models.DataSources;
using InsightEngine.Services.Agents;

namespace InsightEngine.Services.Orchestration
{
    public class PipelineResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string GeneratedSQL { get; set; } = string.Empty;
        public List<Dictionary<string, object>> Data { get; set; } = new();
        public Dictionary<string, object> Statistics { get; set; } = new();
        public List<Trend> Trends { get; set; } = new();
        public List<Outlier> Outliers { get; set; } = new();
        public ChartConfiguration? ChartConfig { get; set; }
        public double Confidence { get; set; }
        public string Explanation { get; set; } = string.Empty;
        public long ExecutionTimeMs { get; set; }
        public int RowCount { get; set; }

        public static PipelineResult Error(string message) => new()
        {
            Success = false,
            ErrorMessage = message
        };
    }

    public class AnalysisPipelineService
    {
        private readonly QueryInterpreterAgent _interpreter;
        private readonly DataAnalystAgent _dataAnalyst;
        private readonly VisualizationAgent _visualizer;
        private readonly ILogger<AnalysisPipelineService> _logger;

        public AnalysisPipelineService(
            QueryInterpreterAgent interpreter,
            DataAnalystAgent dataAnalyst,
            VisualizationAgent visualizer,
            ILogger<AnalysisPipelineService> logger)
        {
            _interpreter = interpreter;
            _dataAnalyst = dataAnalyst;
            _visualizer = visualizer;
            _logger = logger;
        }

        public async Task<PipelineResult> ExecuteAsync(
            string userQuestion,
            DataSource dataSource)
        {
            _logger.LogInformation(
                "Pipeline starting for question: {Question}", userQuestion);

            var context = new AnalysisContext
            {
                UserQuestion = userQuestion,
                DataSourceId = dataSource.Id,
                ConnectionString = dataSource.ConnectionString,
                SchemaDescription = dataSource.SchemaMetadata
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));

            try
            {
                // Stage 1: Query Interpreter
                _logger.LogInformation("Stage 1: Query Interpretation");
                context = await RunWithTimeout(
                    () => _interpreter.ExecuteAsync(context),
                    10, "Query Interpreter");

                if (context.HasError || string.IsNullOrEmpty(context.GeneratedSQL))
                    return PipelineResult.Error(
                        context.ErrorMessages.FirstOrDefault()
                        ?? "Could not generate SQL. Please rephrase your question.");

                // Stage 2: Data Analyst
                _logger.LogInformation("Stage 2: Data Analysis");
                context = await RunWithTimeout(
                    () => _dataAnalyst.ExecuteAsync(context),
                    30, "Data Analyst");

                if (context.HasError)
                    return PipelineResult.Error(
                        context.ErrorMessages.FirstOrDefault()
                        ?? "Data analysis failed. Check database connection.");

                // Stage 3: Visualization
                _logger.LogInformation("Stage 3: Visualization");
                context = await RunWithTimeout(
                    () => _visualizer.ExecuteAsync(context),
                    5, "Visualization");

                // Visualization failure is non-fatal — fallback to table
                if (context.ChartConfig == null)
                {
                    context.ChartConfig = new ChartConfiguration
                    {
                        ChartType = "table",
                        Title = "Query Results"
                    };
                }

                _logger.LogInformation(
                    "Pipeline completed successfully. Rows: {Count}",
                    context.QueryResults.Count);

                return new PipelineResult
                {
                    Success = true,
                    GeneratedSQL = context.GeneratedSQL,
                    Data = context.QueryResults,
                    Statistics = context.Statistics,
                    Trends = context.Trends,
                    Outliers = context.Outliers,
                    ChartConfig = context.ChartConfig,
                    ExecutionTimeMs = context.ExecutionTimeMs,
                    RowCount = context.QueryResults.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pipeline failed unexpectedly");
                return PipelineResult.Error($"Pipeline error: {ex.Message}");
            }
        }

        private async Task<AnalysisContext> RunWithTimeout(
            Func<Task<AnalysisContext>> agentTask,
            int timeoutSeconds,
            string agentName)
        {
            using var cts = new CancellationTokenSource(
                TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                return await agentTask();
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "{AgentName} timed out after {Seconds}s",
                    agentName, timeoutSeconds);
                throw new TimeoutException(
                    $"{agentName} timed out after {timeoutSeconds} seconds.");
            }
        }
    }
}