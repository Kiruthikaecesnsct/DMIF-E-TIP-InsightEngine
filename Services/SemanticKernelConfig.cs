using Microsoft.SemanticKernel;

namespace InsightEngine.Services
{
    public class SemanticKernelConfig
    {
        private readonly IConfiguration _configuration;

        public SemanticKernelConfig(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Kernel CreateKernel()
        {
            // ❌ We are NOT using OpenAI anymore
            // Just return empty kernel (to avoid breaking DI)

            return Kernel.CreateBuilder().Build();
        }
    }
}