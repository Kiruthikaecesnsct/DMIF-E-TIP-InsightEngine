using InsightEngine.Models.Agents;
using Microsoft.SemanticKernel;
using System.Text;
using System.Text.Json;

namespace InsightEngine.Services.Agents
{
    public class VisualizationAgent : BaseAgent
    {
        public VisualizationAgent(
            Kernel kernel,
            ILogger<VisualizationAgent> logger)
            : base(kernel, logger)
        {
            AgentName = "VisualizationAgent";
            Description = "Determines the best chart type and configuration for data";
        }

        public override async Task<AnalysisContext> ExecuteAsync(
            AnalysisContext context)
        {
            LogAgentActivity("Starting",
                $"Choosing chart for: {context.UserQuestion}");

            try
            {
                context.ChartConfig = await GenerateChartConfigAsync(context);
                context.ChartConfig = ApplyBestPractices(
                    context.ChartConfig, context.QueryResults);

                LogAgentActivity("Completed",
                    $"Selected chart type: {context.ChartConfig.ChartType}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[{AgentName}] Failed to generate chart config",
                    AgentName);

                // Fallback to table
                context.ChartConfig = new ChartConfiguration
                {
                    ChartType = "table",
                    Title = "Query Results",
                    Reasoning = "Fallback to table due to visualization error"
                };
            }

            return context;
        }

        private async Task<ChartConfiguration> GenerateChartConfigAsync(
            AnalysisContext context)
        {
            var prompt = BuildVisualizationPrompt(context);
            var response = await Kernel.InvokePromptAsync(prompt);
            var responseText = response.ToString();

            return ParseChartConfig(responseText);
        }

        private string BuildVisualizationPrompt(AnalysisContext context)
        {
            var data = context.QueryResults;
            var columns = data.Any() ? data.First().Keys.ToList() : new List<string>();

            var numericCols = new List<string>();
            var categoricalCols = new List<string>();
            var timeCols = new List<string>();

            if (data.Any())
            {
                foreach (var col in columns)
                {
                    var val = data.First()[col];
                    if (val is int or long or double or float or decimal)
                        numericCols.Add(col);
                    else if (val is DateTime)
                        timeCols.Add(col);
                    else
                        categoricalCols.Add(col);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("You are a data visualization expert.");
            sb.AppendLine("Choose the best chart type for this data.");
            sb.AppendLine();
            sb.AppendLine($"USER QUESTION: {context.UserQuestion}");
            sb.AppendLine();
            sb.AppendLine("DATA STRUCTURE:");
            sb.AppendLine($"- Columns: {string.Join(", ", columns)}");
            sb.AppendLine($"- Row count: {data.Count}");
            sb.AppendLine($"- Numeric columns: {string.Join(", ", numericCols)}");
            sb.AppendLine($"- Categorical columns: {string.Join(", ", categoricalCols)}");
            sb.AppendLine($"- Time columns: {string.Join(", ", timeCols)}");
            sb.AppendLine();
            sb.AppendLine("CHART OPTIONS:");
            sb.AppendLine("- bar_chart: Comparing categories");
            sb.AppendLine("- line_chart: Trends over time");
            sb.AppendLine("- pie_chart: Part-to-whole (max 6 slices only)");
            sb.AppendLine("- table: When no clear visualization fits");
            sb.AppendLine();
            sb.AppendLine("Return ONLY this JSON with no extra text:");
            sb.AppendLine("{");
            sb.AppendLine("  \"chart_type\": \"bar_chart\",");
            sb.AppendLine("  \"reasoning\": \"why this chart type\",");
            sb.AppendLine("  \"x_axis\": \"column_name\",");
            sb.AppendLine("  \"y_axis\": \"column_name\",");
            sb.AppendLine("  \"title\": \"Chart Title\",");
            sb.AppendLine("  \"number_format\": \"currency\"");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private ChartConfiguration ParseChartConfig(string responseText)
        {
            try
            {
                var cleaned = ExtractJsonFromResponse(responseText);
                var parsed = JsonSerializer.Deserialize<JsonElement>(cleaned);

                return new ChartConfiguration
                {
                    ChartType = parsed.GetProperty("chart_type")
                        .GetString() ?? "table",
                    Reasoning = parsed.TryGetProperty("reasoning", out var r)
                        ? r.GetString() ?? string.Empty : string.Empty,
                    XAxis = parsed.TryGetProperty("x_axis", out var x)
                        ? x.GetString() ?? string.Empty : string.Empty,
                    YAxis = parsed.TryGetProperty("y_axis", out var y)
                        ? y.GetString() ?? string.Empty : string.Empty,
                    Title = parsed.TryGetProperty("title", out var t)
                        ? t.GetString() ?? "Results" : "Results",
                    NumberFormat = parsed.TryGetProperty("number_format", out var n)
                        ? n.GetString() ?? "number" : "number"
                };
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to parse chart config, using table");
                return new ChartConfiguration
                {
                    ChartType = "table",
                    Title = "Query Results",
                    Reasoning = "Could not parse visualization config"
                };
            }
        }

        private ChartConfiguration ApplyBestPractices(
            ChartConfiguration config,
            List<Dictionary<string, object>> data)
        {
            // Rule 1: Pie chart max 6 slices
            if (config.ChartType == "pie_chart" && data.Count > 6)
            {
                config.ChartType = "bar_chart";
                config.Reasoning += " (Switched from pie: too many slices)";
            }

            // Rule 2: No chart for single row — show as stat card
            if (data.Count == 1)
            {
                config.ChartType = "stat_card";
                config.Reasoning = "Single value — displayed as metric card";
            }

            // Rule 3: Too many rows — force table
            if (data.Count > 50)
            {
                config.ChartType = "table";
                config.Reasoning = "Too many rows for chart — showing table";
            }

            // Rule 4: Detect currency columns
            var yAxis = config.YAxis?.ToLower() ?? string.Empty;
            if (yAxis.Contains("revenue") || yAxis.Contains("amount") ||
                yAxis.Contains("sales") || yAxis.Contains("price") ||
                yAxis.Contains("cost"))
            {
                config.NumberFormat = "currency";
            }

            return config;
        }
    }
}