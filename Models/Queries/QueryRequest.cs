using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InsightEngine.Models.Queries
{
    public enum QueryStatus { Pending, Processing, Completed, Failed }

    public class QueryRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = "default-user";
        public string NaturalLanguageQuery { get; set; } = string.Empty;
        public string DataSourceId { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public QueryStatus Status { get; set; } = QueryStatus.Pending;
    }
}