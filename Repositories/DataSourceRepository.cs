using InsightEngine.Models.DataSources;
using InsightEngine.Services;
using MongoDB.Driver;

namespace InsightEngine.Repositories
{
    public class DataSourceRepository : IDataSourceRepository
    {
        private readonly IMongoCollection<DataSource> _collection;

        public DataSourceRepository(MongoDbService mongoDb)
        {
            _collection = mongoDb.GetCollection<DataSource>("DataSources");
        }

        public async Task<DataSource> CreateAsync(DataSource dataSource)
        {
            await _collection.InsertOneAsync(dataSource);
            return dataSource;
        }

        public async Task<DataSource?> GetByIdAsync(string id)
            => await _collection
                .Find(d => d.Id == id)
                .FirstOrDefaultAsync();

        public async Task<List<DataSource>> GetAllActiveAsync()
            => await _collection
                .Find(d => d.IsActive)
                .ToListAsync();

        public async Task UpdateSchemaAsync(string id, string schemaMetadata)
        {
            var update = Builders<DataSource>.Update
                .Set(d => d.SchemaMetadata, schemaMetadata)
                .Set(d => d.LastSyncDate, DateTime.UtcNow);
            await _collection.UpdateOneAsync(d => d.Id == id, update);
        }

        public async Task DeleteAsync(string id)
            => await _collection.DeleteOneAsync(d => d.Id == id);
    }
}
