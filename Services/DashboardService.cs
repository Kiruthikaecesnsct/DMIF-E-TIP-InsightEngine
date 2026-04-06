using InsightEngine.Models.Agents;
using InsightEngine.Models.DataSources;
using InsightEngine.Services.Orchestration;

namespace InsightEngine.Services
{
    public class DashboardPanel
    {
        public string Title { get; set; } = string.Empty;
        public string SubQuestion { get; set; } = string.Empty;
        public PipelineResult? Result { get; set; }
        public int Order { get; set; }
    }

    public class DashboardResult
    {
        public string OverallQuestion { get; set; } = string.Empty;
        public List<DashboardPanel> Panels { get; set; } = new();
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class DashboardService
    {
        private readonly AnalysisPipelineService _pipeline;
        private readonly ILogger<DashboardService> _logger;

        public DashboardService(
            AnalysisPipelineService pipeline,
            ILogger<DashboardService> logger)
        {
            _pipeline = pipeline;
            _logger = logger;
        }

        public async Task<DashboardResult> GenerateDashboardAsync(
            string userQuestion, DataSource dataSource)
        {
            _logger.LogInformation(
                "Generating dashboard for: {Question}", userQuestion);

            var subQuestions = DecomposeQuestion(userQuestion);

            var panels = new List<DashboardPanel>();
            int order = 0;

            foreach (var sq in subQuestions)
            {
                try
                {
                    var result = await _pipeline.ExecuteAsync(sq.Question, dataSource);
                    panels.Add(new DashboardPanel
                    {
                        Title = sq.Title,
                        SubQuestion = sq.Question,
                        Result = result,
                        Order = order++
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Dashboard sub-panel failed: {Q}", sq.Question);
                    panels.Add(new DashboardPanel
                    {
                        Title = sq.Title,
                        SubQuestion = sq.Question,
                        Result = PipelineResult.Error("Panel failed to load"),
                        Order = order++
                    });
                }
            }

            return new DashboardResult
            {
                OverallQuestion = userQuestion,
                Panels = panels,
                Success = panels.Any(p => p.Result?.Success == true)
            };
        }

        private List<(string Title, string Question)> DecomposeQuestion(
            string question)
        {
            var q = question.ToLower();

            // Performance overview pattern
            if (q.Contains("performance") || q.Contains("overview") ||
                q.Contains("summary"))
            {
                return new List<(string, string)>
                {
                    ("Total Revenue", "What is the total revenue?"),
                    ("Revenue by Product", "What is revenue by product category?"),
                    ("Monthly Trend", "What is the monthly revenue trend?"),
                    ("Top Customers", "Who are the top 10 customers by revenue?")
                };
            }

            // Sales pattern
            if (q.Contains("sales"))
            {
                return new List<(string, string)>
                {
                    ("Total Sales", "What is the total sales amount?"),
                    ("Sales by Product", "What are the top 5 products by sales?"),
                    ("Sales Trend", "What is the monthly sales trend?"),
                    ("Sales by Region", "What is sales broken down by region?")
                };
            }

            // Default: treat as single question
            return new List<(string, string)>
            {
                (question, question)
            };
        }
    }
}