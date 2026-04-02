namespace InsightEngine.Models.Agents
{
    public class AnalysisContext
    {
        public string UserQuestion { get; set; } = string.Empty;
        public string DataSourceId { get; set; } = string.Empty;
        public string GeneratedSQL { get; set; } = string.Empty;
        public string SchemaDescription { get; set; } = string.Empty;
        public List<Dictionary<string, object>> QueryResults { get; set; } = new();
        public Dictionary<string, object> Statistics { get; set; } = new();
        public string ChartType { get; set; } = string.Empty;
        public List<string> Insights { get; set; } = new();
        public string ExecutiveSummary { get; set; } = string.Empty;
        public List<string> ErrorMessages { get; set; } = new();
        public bool HasError => ErrorMessages.Any();
    }
}
