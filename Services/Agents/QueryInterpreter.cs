using InsightEngine.Models.Agents;
using InsightEngine.Models.Queries;
using Microsoft.SemanticKernel;
using System.Text;
using System.Text.Json;

namespace InsightEngine.Services.Agents
{
    public class QueryInterpreterAgent : BaseAgent
    {
        private readonly IConfiguration _config;

        public QueryInterpreterAgent(
            Kernel kernel,
            ILogger<QueryInterpreterAgent> logger,
            IConfiguration config)
            : base(kernel, logger)
        {
            _config = config;
        }

        public override async Task<AnalysisContext> ExecuteAsync(AnalysisContext context)
        {
            LogAgentActivity("Starting",
                $"Processing question: {context.UserQuestion}");

            try
            {
                var result = await InterpretQueryAsync(
                    context.UserQuestion,
                    context.SchemaDescription);

                context.GeneratedSQL = result.GeneratedSQL;

                LogAgentActivity("Completed",
                    $"SQL generated with confidence {result.Confidence:P0}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[{AgentName}] Failed to interpret query",
                    AgentName);
                context.ErrorMessages.Add(
                    $"Query interpretation failed: {ex.Message}");
            }

            return context;
        }

        public async Task<QueryResult> InterpretQueryAsync(
     string naturalLanguageQuery,
     string schemaContext)
        {
            var prompt = BuildQueryPrompt(naturalLanguageQuery, schemaContext);
      

            var apiKey = _config["SemanticKernel:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new Exception("Gemini API key missing in appsettings.json");

            using var httpClient = new HttpClient();

            var url =
                $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
            new
            {
                parts = new[]
                {
                    new { text = prompt }
                }
            }
        }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await httpClient.PostAsync(url, content);
            var responseTextRaw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Gemini Error: {responseTextRaw}");

            using var doc = JsonDocument.Parse(responseTextRaw);

            var responseText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            // ✅ Clean markdown if Gemini adds it
            responseText = responseText?
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            return ParseQueryResponse(responseText ?? "");
        }
        private string BuildQueryPrompt(string question, string schema)
        {
            return $$"""
        You are a SQL query generation expert. Convert natural language questions
        into valid SQL queries based on the provided database schema.

        Available Tables and Columns:
        {{schema}}

        Rules:
        1. Generate SELECT queries only (no INSERT, UPDATE, DELETE, DROP)
        2. Use proper JOIN syntax when multiple tables are needed
        3. Apply appropriate WHERE clauses for filters
        4. Use GROUP BY for aggregations
        5. Return valid SQL Server syntax
        6. Handle date ranges intelligently using GETDATE() and DATEADD()
        7. Always add TOP 1000 if no row limit is specified

        Return ONLY this exact JSON format with no extra text or explanation:
        {
          "sql_query": "SELECT...",
          "confidence": 0.85,
          "tables_used": ["TableName1", "TableName2"],
          "explanation": "Brief explanation of what the query returns"
        }

        User Question: {{question}}
        """;
        }

        private QueryResult ParseQueryResponse(string responseText)
        {
            try
            {
                var cleaned = ExtractJsonFromResponse(responseText);
                var parsed = JsonSerializer.Deserialize<JsonElement>(cleaned);

                return new QueryResult
                {
                    GeneratedSQL = parsed.GetProperty("sql_query")
                        .GetString() ?? string.Empty,
                    Confidence = parsed.GetProperty("confidence").GetDouble(),
                    TablesUsed = parsed.GetProperty("tables_used")
                        .EnumerateArray()
                        .Select(t => t.GetString() ?? string.Empty)
                        .ToList(),
                    Explanation = parsed.GetProperty("explanation")
                        .GetString() ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to parse agent JSON response");
                return new QueryResult
                {
                    ErrorMessage = "Failed to parse agent response",
                    Confidence = 0.0
                };
            }
        }

        public bool ValidateQuery(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return false;

            var upperSql = sql.ToUpperInvariant();
            var forbiddenKeywords = new[]
            {
                "INSERT", "UPDATE", "DELETE", "DROP",
                "TRUNCATE", "ALTER", "CREATE", "EXEC"
            };

            return !forbiddenKeywords.Any(k => upperSql.Contains(k));
        }

        public List<string> ExtractTablesUsed(string sql)
        {
            var tables = new List<string>();
            var upperSql = sql.ToUpperInvariant();
            var keywords = new[] { "FROM ", "JOIN " };

            foreach (var keyword in keywords)
            {
                var index = 0;
                while ((index = upperSql.IndexOf(keyword, index)) != -1)
                {
                    index += keyword.Length;
                    var end = upperSql.IndexOfAny(
                        new[] { ' ', '\n', '\r', ',' }, index);
                    if (end == -1) end = upperSql.Length;
                    var tableName = sql.Substring(index, end - index).Trim();
                    if (!string.IsNullOrEmpty(tableName))
                        tables.Add(tableName);
                }
            }

            return tables.Distinct().ToList();
        }
    }
}
