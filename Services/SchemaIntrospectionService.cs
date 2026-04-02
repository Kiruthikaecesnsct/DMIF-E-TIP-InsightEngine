using Dapper;
using InsightEngine.Models.DataSources;
using Microsoft.Data.SqlClient;
using System.Text;

namespace InsightEngine.Services
{
    public class SchemaIntrospectionService
    {
        private readonly ILogger<SchemaIntrospectionService> _logger;

        public SchemaIntrospectionService(
            ILogger<SchemaIntrospectionService> logger)
        {
            _logger = logger;
        }

        public async Task<List<TableSchema>> IntrospectSqlServerAsync(
            string connectionString)
        {
            var tables = new List<TableSchema>();

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var query = @" 
                    SELECT 
                        t.TABLE_NAME, 
                        c.COLUMN_NAME, 
                        c.DATA_TYPE, 
                        c.IS_NULLABLE, 
                        CASE WHEN pk.COLUMN_NAME IS NOT NULL  
                             THEN 1 ELSE 0 END AS IS_PRIMARY_KEY, 
                        CASE WHEN fk.COLUMN_NAME IS NOT NULL  
                             THEN 1 ELSE 0 END AS IS_FOREIGN_KEY 
                    FROM INFORMATION_SCHEMA.TABLES t 
                    JOIN INFORMATION_SCHEMA.COLUMNS c  
                        ON t.TABLE_NAME = c.TABLE_NAME 
                    LEFT JOIN ( 
                        SELECT ku.TABLE_NAME, ku.COLUMN_NAME 
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc 
                        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku 
                            ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME 
                        WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY' 
                    ) pk ON c.TABLE_NAME = pk.TABLE_NAME  
                         AND c.COLUMN_NAME = pk.COLUMN_NAME 
                    LEFT JOIN ( 
                        SELECT ku.TABLE_NAME, ku.COLUMN_NAME 
                        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc 
                        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku 
                            ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME 
                        WHERE tc.CONSTRAINT_TYPE = 'FOREIGN KEY' 
                    ) fk ON c.TABLE_NAME = fk.TABLE_NAME  
                         AND c.COLUMN_NAME = fk.COLUMN_NAME 
                    WHERE t.TABLE_TYPE = 'BASE TABLE' 
                    ORDER BY t.TABLE_NAME, c.ORDINAL_POSITION";

                var rows = await connection.QueryAsync(query);
                var grouped = rows.GroupBy(r => (string)r.TABLE_NAME);

                foreach (var group in grouped)
                {
                    var schema = new TableSchema
                    {
                        TableName = group.Key,
                        Columns = group.Select(r => new ColumnSchema
                        {
                            ColumnName = (string)r.COLUMN_NAME,
                            DataType = (string)r.DATA_TYPE,
                            IsNullable = (string)r.IS_NULLABLE == "YES",
                            IsPrimaryKey = (int)r.IS_PRIMARY_KEY == 1,
                            IsForeignKey = (int)r.IS_FOREIGN_KEY == 1
                        }).ToList()
                    };

                    // Get row count 
                    try
                    {
                        schema.RowCount = await connection.ExecuteScalarAsync<int>(
                            $"SELECT COUNT(*) FROM [{group.Key}]");
                    }
                    catch { schema.RowCount = -1; }

                    // Get 5 sample rows for agent context 
                    try
                    {
                        var sampleRows = await connection.QueryAsync(
                            $"SELECT TOP 5 * FROM [{group.Key}]");
                        schema.SampleData = sampleRows
                            .Select(r => (IDictionary<string, object>)r)
                            .Select(d => d.ToDictionary(k => k.Key, k => k.Value))
                            .ToList();
                    }
                    catch { /* Sample data is optional */ }

                    tables.Add(schema);
                }

                _logger.LogInformation(
                    "Introspected {Count} tables from database", tables.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to introspect SQL Server schema");
                throw;
            }

            return tables;
        }

        public string GenerateSchemaDescription(List<TableSchema> schemas)
        {
            var sb = new StringBuilder();

            foreach (var table in schemas)
            {
                sb.AppendLine($"Table: {table.TableName}");

                if (!string.IsNullOrEmpty(table.Description))
                    sb.AppendLine($"  Purpose: {table.Description}");

                sb.AppendLine($"  Rows: {(table.RowCount >= 0 ? table.RowCount.ToString() : "unknown")}");
                sb.AppendLine("  Columns:");

                foreach (var col in table.Columns)
                {
                    var flags = new List<string>();
                    if (col.IsPrimaryKey) flags.Add("PK");
                    if (col.IsForeignKey) flags.Add("FK");
                    if (!col.IsNullable) flags.Add("NOT NULL");

                    var flagStr = flags.Any()
                        ? $" [{string.Join(", ", flags)}]"
                        : string.Empty;

                    var desc = !string.IsNullOrEmpty(col.Description)
                        ? $" — {col.Description}"
                        : string.Empty;

                    sb.AppendLine(
                        $"    - {col.ColumnName} ({col.DataType}){flagStr}{desc}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
