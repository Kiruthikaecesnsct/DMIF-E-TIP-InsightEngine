using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InsightEngine.Models.Queries
{
    public class QueryResult
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string QueryRequestId { get; set; } = string.Empty;
        public string GeneratedSQL { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public List<string> TablesUsed { get; set; } = new();
        public string Explanation { get; set; } = string.Empty;
        public long ExecutionTimeMs { get; set; }
        public int RowCount { get; set; }
        public string RawResults { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}