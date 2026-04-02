using MongoDB.Driver;

namespace InsightEngine.Services
{
    public class MongoDbService
    {
        private readonly IMongoDatabase _database;

        public MongoDbService(IConfiguration configuration)
        {
            var connectionString = configuration["MongoDB:ConnectionString"]
                ?? "mongodb://localhost:27017";
            var databaseName = configuration["MongoDB:DatabaseName"]
                ?? "InsightEngineDB";

            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);
        }

        public IMongoCollection<T> GetCollection<T>(string collectionName)
            => _database.GetCollection<T>(collectionName);
    }
}
