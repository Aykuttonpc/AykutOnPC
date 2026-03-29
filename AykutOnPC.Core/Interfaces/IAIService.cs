namespace AykutOnPC.Core.Interfaces;

public interface IAIService
{
    Task<string> GetAnswerAsync(string userMessage, CancellationToken cancellationToken = default);
}
