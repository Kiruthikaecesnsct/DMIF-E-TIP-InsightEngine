using Dapper;
using InsightEngine.Models.Agents;
using Microsoft.Data.SqlClient;
using Microsoft.SemanticKernel;
using System.Diagnostics;

namespace InsightEngine.Services.Agents
{
    public class DataAnalystAgent : BaseAgent
    {
        public DataAnalystAgent(
            Kernel kernel,
            ILogger<DataAnalystAgent> logger)
            : base(kernel, logger)
        {
            AgentName = "DataAnalystAgent";
            Description = "Executes SQL queries and calculates statistics on results";
        }

        public override async Task<AnalysisContext> ExecuteAsync(
            AnalysisContext context)
        {
            LogAgentActivity("Starting",
                $"Executing SQL for question: {context.UserQuestion}");

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Step 1: Execute the SQL
                context.QueryResults = await ExecuteQueryAsync(
                    context.GeneratedSQL,
                    context.ConnectionString);

                // Step 2: Calculate statistics
                context.Statistics = CalculateStatistics(context.QueryResults);

                // Step 3: Detect trends
                context.Trends = DetectTrends(context.QueryResults);

                // Step 4: Detect outliers
                context.Outliers = DetectOutliers(context.QueryResults);

                stopwatch.Stop();
                context.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

                LogAgentActivity("Completed",
                    $"Returned {context.QueryResults.Count} rows " +
                    $"in {context.ExecutionTimeMs}ms");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.LogError(ex, "[{AgentName}] Failed to analyze data",
                    AgentName);
                context.ErrorMessages.Add($"Data analysis failed: {ex.Message}");
            }

            return context;
        }

        private async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(
            string sql, string connectionString)
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var rows = await connection.QueryAsync(
                new CommandDefinition(sql, cancellationToken: cts.Token));

            return rows
                .Select(r => (IDictionary<string, object>)r)
                .Select(d => d.ToDictionary(k => k.Key, k => k.Value))
                .ToList();
        }

        public Dictionary<string, object> CalculateStatistics(
            List<Dictionary<string, object>> data)
        {
            var stats = new Dictionary<string, object>();

            if (!data.Any()) return stats;

            stats["row_count"] = data.Count;

            // Find numeric columns
            var firstRow = data.First();
            var numericColumns = firstRow
                .Where(kv => kv.Value != null &&
                    IsNumeric(kv.Value))
                .Select(kv => kv.Key)
                .ToList();

            foreach (var column in numericColumns)
            {
                var values = data
                    .Where(r => r.ContainsKey(column) && r[column] != null)
                    .Select(r => Convert.ToDouble(r[column]))
                    .ToList();

                if (!values.Any()) continue;

                stats[$"{column}_total"] = Math.Round(values.Sum(), 2);
                stats[$"{column}_average"] = Math.Round(values.Average(), 2);
                stats[$"{column}_max"] = values.Max();
                stats[$"{column}_min"] = values.Min();
                stats[$"{column}_count"] = values.Count;

                // Growth rate if 2+ rows
                if (values.Count >= 2)
                {
                    var first = values.First();
                    var last = values.Last();
                    if (first != 0)
                    {
                        var growth = ((last - first) / Math.Abs(first)) * 100;
                        stats[$"{column}_growth_percent"] =
                            Math.Round(growth, 2);
                    }
                }
            }

            return stats;
        }

        public List<Trend> DetectTrends(
            List<Dictionary<string, object>> data)
        {
            var trends = new List<Trend>();

            if (data.Count < 3) return trends;

            var firstRow = data.First();
            var numericColumns = firstRow
                .Where(kv => kv.Value != null && IsNumeric(kv.Value))
                .Select(kv => kv.Key)
                .ToList();

            foreach (var column in numericColumns)
            {
                var values = data
                    .Where(r => r.ContainsKey(column) && r[column] != null)
                    .Select(r => Convert.ToDouble(r[column]))
                    .ToList();

                if (values.Count < 3) continue;

                // Simple trend: compare first half average to second half average
                var mid = values.Count / 2;
                var firstHalf = values.Take(mid).Average();
                var secondHalf = values.Skip(mid).Average();

                if (firstHalf == 0) continue;

                var change = ((secondHalf - firstHalf) / Math.Abs(firstHalf)) * 100;

                var trend = new Trend
                {
                    Column = column,
                    Magnitude = Math.Round(Math.Abs(change), 2)
                };

                if (change > 5)
                    trend.Type = "increasing";
                else if (change < -5)
                    trend.Type = "decreasing";
                else
                    trend.Type = "stable";

                trends.Add(trend);
            }

            return trends;
        }

        public List<Outlier> DetectOutliers(
            List<Dictionary<string, object>> data)
        {
            var outliers = new List<Outlier>();

            if (data.Count < 4) return outliers;

            var firstRow = data.First();
            var numericColumns = firstRow
                .Where(kv => kv.Value != null && IsNumeric(kv.Value))
                .Select(kv => kv.Key)
                .ToList();

            foreach (var column in numericColumns)
            {
                var values = data
                    .Where(r => r.ContainsKey(column) && r[column] != null)
                    .Select(r => Convert.ToDouble(r[column]))
                    .ToList();

                if (values.Count < 4) continue;

                var average = values.Average();
                var stdDev = CalculateStdDev(values, average);

                if (stdDev == 0) continue;

                foreach (var value in values)
                {
                    if (Math.Abs(value - average) > 2 * stdDev)
                    {
                        outliers.Add(new Outlier
                        {
                            Value = value,
                            Column = column,
                            Type = value > average ? "High" : "Low"
                        });
                    }
                }
            }

            return outliers;
        }

        private double CalculateStdDev(List<double> values, double average)
        {
            var sumOfSquares = values.Sum(v => Math.Pow(v - average, 2));
            return Math.Sqrt(sumOfSquares / values.Count);
        }

        private bool IsNumeric(object value)
        {
            return value is int or long or double or float or decimal
                or short or byte;
        }
    }
}