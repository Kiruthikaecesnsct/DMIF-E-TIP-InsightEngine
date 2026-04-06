using InsightEngine.Models.Queries;

namespace InsightEngine.Repositories
{
    public interface IQueryRepository
    {
        Task<QueryRequest> SaveQueryAsync(QueryRequest request);
        Task<QueryResult> SaveResultAsync(QueryResult result);
        Task<List<QueryRequest>> GetUserQueriesAsync(string userId);
        Task<QueryResult?> GetResultByRequestIdAsync(string requestId);
        Task<List<QueryRequest>> GetRecentQueriesAsync(int limit);
        Task<QueryRequest?> GetByIdAsync(string id);
    }
}
