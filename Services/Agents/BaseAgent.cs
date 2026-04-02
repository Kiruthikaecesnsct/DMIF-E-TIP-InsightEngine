using InsightEngine.Models.Agents;
using Microsoft.SemanticKernel;

namespace InsightEngine.Services.Agents
{
    public abstract class BaseAgent
    {
        protected readonly Kernel Kernel;
        protected readonly ILogger Logger;

        public string AgentName { get; protected set; } = string.Empty;
        public string Description { get; protected set; } = string.Empty;
        public string SystemPrompt { get; protected set; } = string.Empty;

        protected BaseAgent(Kernel kernel, ILogger logger)
        {
            Kernel = kernel;
            Logger = logger;
        }

        // Every agent must implement this 
        public abstract Task<AnalysisContext> ExecuteAsync(AnalysisContext context);

        // Shared logging helper used by all agents 
        protected void LogAgentActivity(string action, string details)
        {
            Logger.LogInformation("[{AgentName}] {Action}: {Details}",
                AgentName, action, details);
        }

        // Base prompt builder — agents can override 
        protected virtual string BuildPrompt(AnalysisContext context)
        {
            return SystemPrompt;
        }

        // Shared JSON extraction helper — strips markdown code fences if present 
        protected string ExtractJsonFromResponse(string response)
        {
            response = response.Trim();
            if (response.StartsWith("```json"))
                response = response[7..];
            if (response.StartsWith("```"))
                response = response[3..];
            if (response.EndsWith("```"))
                response = response[..^3];
            return response.Trim();
        }
    }
}
