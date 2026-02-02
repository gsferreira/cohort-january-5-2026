using System.Text.Json;

namespace BudgetTracker.Api.Features.Intelligence.Tools;

public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    BinaryData ParametersSchema { get; }
    Task<string> ExecuteAsync(string userId, JsonElement arguments);
}

public class ToolExecutionResult
{
    public required string ToolName { get; init; }
    public required string Result { get; init; }
    public required TimeSpan ExecutionTime { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}
