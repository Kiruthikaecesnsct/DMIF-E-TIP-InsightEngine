using Microsoft.SemanticKernel;

namespace InsightEngine.Services
{
    public class SemanticKernelConfig
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SemanticKernelConfig> _logger;

        public SemanticKernelConfig(
            IConfiguration configuration,
            ILogger<SemanticKernelConfig> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public Kernel CreateKernel()
        {
            var apiKey = _configuration["SemanticKernel:ApiKey"]
                ?? throw new InvalidOperationException(
                    "Semantic Kernel API key not configured.");

            var modelId = _configuration["SemanticKernel:ModelId"]
                ?? "gemini-1.5-flash";

            _logger.LogInformation(
                "Creating Kernel with model: {ModelId}", modelId);

            var builder = Kernel.CreateBuilder();

            builder.AddOpenAIChatCompletion(
                modelId: modelId,
                apiKey: apiKey,
                endpoint: new Uri(
                    "https://generativelanguage.googleapis.com/v1beta/openai/")
            );

            var kernel = builder.Build();

            _logger.LogInformation("Kernel created successfully.");

            return kernel;
        }
    }
}