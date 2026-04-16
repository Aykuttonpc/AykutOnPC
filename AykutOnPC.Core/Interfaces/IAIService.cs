namespace AykutOnPC.Core.Interfaces;

public interface IAiService
{
    Task<string> GetAnswerAsync(string userMessage, CancellationToken cancellationToken = default);
}
