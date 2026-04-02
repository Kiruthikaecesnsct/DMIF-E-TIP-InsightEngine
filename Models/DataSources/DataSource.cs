using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InsightEngine.Models.DataSources
{
    public enum DataSourceType { SqlServer, PostgreSQL, MySQL }
    public enum DataSourceStatus { Active, Inactive, Error }

    public class DataSource
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public DataSourceType Type { get; set; }
        public string ConnectionString { get; set; } = string.Empty;
        public string SchemaMetadata { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastSyncDate { get; set; }
        public DataSourceStatus Status { get; set; } = DataSourceStatus.Active;
    }
}