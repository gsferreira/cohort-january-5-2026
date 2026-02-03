namespace BudgetTracker.Api.Features.Intelligence.Tools;

public interface IAgentContext
{
    string UserId { get; }
}

public class AgentContext : IAgentContext
{
    public string UserId { get; set; } = string.Empty;
}
