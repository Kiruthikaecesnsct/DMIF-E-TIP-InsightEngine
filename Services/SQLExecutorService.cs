using Dapper;
using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace InsightEngine.Services
{
    public class QueryExecutionResult
    {
        public List<Dictionary<string, object>> Rows { get; set; } = new();
        public long ExecutionTimeMs { get; set; }
        public int RowCount { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsSuccess => ErrorMessage == null;
    }

    public class SqlExecutorService
    {
        private readonly ILogger<SqlExecutorService> _logger;

        public SqlExecutorService(ILogger<SqlExecutorService> logger)
        {
            _logger = logger;
        }

        public bool ValidateSqlSafety(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return false;

            var upper = sql.ToUpperInvariant();
            var forbidden = new[]
            {
                "INSERT", "UPDATE", "DELETE", "DROP",
                "TRUNCATE", "ALTER", "CREATE", "EXEC", "EXECUTE"
            };

            return !forbidden.Any(k => upper.Contains(k));
        }

        public async Task<QueryExecutionResult> ExecuteQueryAsync(
            string sql,
            string connectionString)
        {
            var result = new QueryExecutionResult();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (!ValidateSqlSafety(sql))
                {
                    result.ErrorMessage =
                        "Query rejected: only SELECT queries are allowed.";
                    return result;
                }

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                using var cts = new CancellationTokenSource(
                    TimeSpan.FromSeconds(30));

                var rows = await connection.QueryAsync(
                    new CommandDefinition(sql,
                        cancellationToken: cts.Token));

                result.Rows = rows
                    .Select(r => (IDictionary<string, object>)r)
                    .Select(d => d.ToDictionary(k => k.Key, k => k.Value))
                    .ToList();

                result.RowCount = result.Rows.Count;

                _logger.LogInformation(
                    "Query executed successfully. Rows: {Count}", result.RowCount);
            }
            catch (OperationCanceledException)
            {
                result.ErrorMessage =
                    "Query timed out after 30 seconds.";
                _logger.LogWarning("Query execution timed out.");
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Query execution failed: {ex.Message}";
                _logger.LogError(ex, "Query execution error");
            }
            finally
            {
                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }
    }
}