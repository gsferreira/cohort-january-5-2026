using System.Text.Json;
using Microsoft.Extensions.AI;

namespace BudgetTracker.Api.Features.Intelligence.Tools;

public interface IToolRegistry
{
    IReadOnlyList<IAgentTool> GetAllTools();
    IAgentTool? GetTool(string toolName);
    IList<AITool> ToAITools();
}

public class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, IAgentTool> _tools;

    public ToolRegistry(IEnumerable<IAgentTool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, t => t);
    }

    public IReadOnlyList<IAgentTool> GetAllTools()
    {
        return _tools.Values.ToList();
    }

    public IAgentTool? GetTool(string toolName)
    {
        return _tools.TryGetValue(toolName, out var tool) ? tool : null;
    }

    public IList<AITool> ToAITools()
    {
        return _tools.Values.Select(tool =>
            AIFunctionFactory.Create(
                method: (string userId, JsonElement arguments) => tool.ExecuteAsync(userId, arguments),
                name: tool.Name,
                description: tool.Description,
                serializerOptions: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            )
        ).Cast<AITool>().ToList();
    }
}
