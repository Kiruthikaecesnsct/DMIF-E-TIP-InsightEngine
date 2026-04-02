using InsightEngine.Models.DataSources;

namespace InsightEngine.Repositories
{
    public interface IDataSourceRepository
    {
        Task<DataSource> CreateAsync(DataSource dataSource);
        Task<DataSource?> GetByIdAsync(string id);
        Task<List<DataSource>> GetAllActiveAsync();
        Task UpdateSchemaAsync(string id, string schemaMetadata);
        Task DeleteAsync(string id);
    }
}
