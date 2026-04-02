using InsightEngine.Models.Queries;
using InsightEngine.Services;
using MongoDB.Driver;

namespace InsightEngine.Repositories
{
    public class QueryRepository : IQueryRepository
    {
        private readonly IMongoCollection<QueryRequest> _requests;
        private readonly IMongoCollection<QueryResult> _results;

        public QueryRepository(MongoDbService mongoDb)
        {
            _requests = mongoDb.GetCollection<QueryRequest>("QueryRequests");
            _results = mongoDb.GetCollection<QueryResult>("QueryResults");
        }

        public async Task<QueryRequest> SaveQueryAsync(QueryRequest request)
        {
            await _requests.InsertOneAsync(request);
            return request;
        }

        public async Task<QueryResult> SaveResultAsync(QueryResult result)
        {
            await _results.InsertOneAsync(result);
            return result;
        }

        public async Task<List<QueryRequest>> GetUserQueriesAsync(string userId)
            => await _requests
                .Find(q => q.UserId == userId)
                .SortByDescending(q => q.CreatedDate)
                .Limit(50)
                .ToListAsync();

        public async Task<QueryResult?> GetResultByRequestIdAsync(string requestId)
            => await _results
                .Find(r => r.QueryRequestId == requestId)
                .FirstOrDefaultAsync();
    }
}
