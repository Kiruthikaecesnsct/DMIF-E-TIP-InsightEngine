namespace InsightEngine.Models.Agents
{
    public class Trend
    {
        public string Type { get; set; } = string.Empty; // increasing, decreasing, stable
        public string Column { get; set; } = string.Empty;
        public double Magnitude { get; set; }
    }

    public class Outlier
    {
        public double Value { get; set; }
        public string Type { get; set; } = string.Empty; // High, Low
        public string Column { get; set; } = string.Empty;
    }

    public class ChartConfiguration
    {
        public string ChartType { get; set; } = string.Empty; // bar_chart, line_chart, pie_chart, table
        public string Title { get; set; } = string.Empty;
        public string XAxis { get; set; } = string.Empty;
        public string YAxis { get; set; } = string.Empty;
        public string SortBy { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
        public string NumberFormat { get; set; } = string.Empty; // currency, percent, number
        public bool ShowLegend { get; set; } = true;
    }

    public class AnalysisContext
    {
        public string UserQuestion { get; set; } = string.Empty;
        public string DataSourceId { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public string GeneratedSQL { get; set; } = string.Empty;
        public string SchemaDescription { get; set; } = string.Empty;
        public List<Dictionary<string, object>> QueryResults { get; set; } = new();
        public Dictionary<string, object> Statistics { get; set; } = new();
        public List<Trend> Trends { get; set; } = new();
        public List<Outlier> Outliers { get; set; } = new();
        public ChartConfiguration? ChartConfig { get; set; }
        public List<string> Insights { get; set; } = new();
        public string ExecutiveSummary { get; set; } = string.Empty;
        public List<string> ErrorMessages { get; set; } = new();
        public long ExecutionTimeMs { get; set; }
        public bool HasError => ErrorMessages.Any();
    }
}