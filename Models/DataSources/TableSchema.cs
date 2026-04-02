namespace InsightEngine.Models.DataSources
{
    public class ColumnSchema
    {
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsForeignKey { get; set; }
        public string? Description { get; set; }
    }

    public class TableSchema
    {
        public string TableName { get; set; } = string.Empty;
        public List<ColumnSchema> Columns { get; set; } = new();
        public string? Description { get; set; }
        public int RowCount { get; set; }
        public List<Dictionary<string, object>> SampleData { get; set; } = new();
    }
}
