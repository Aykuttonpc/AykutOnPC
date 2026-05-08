using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AykutOnPC.Web.Commands;

/// <summary>
/// Run-and-exit CLI: <c>dotnet AykutOnPC.Web.dll --backfill-embeddings</c>.
/// Computes embeddings for KB entries that have <c>Embedding IS NULL</c>. Used after
/// the AddKnowledgeEmbedding migration ships and once after admin imports without an
/// embedding service available. Idempotent — re-running only touches still-null rows.
/// </summary>
public static class BackfillEmbeddingsCommand
{
    public const string ArgFlag = "--backfill-embeddings";

    public static async Task<int> RunAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var context = sp.GetRequiredService<AppDbContext>();
        var kb = sp.GetRequiredService<IKnowledgeBaseService>();

        try
        {
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Migration failed before backfill — aborting.");
            return 2;
        }

        var (ok, fail) = await kb.BackfillMissingEmbeddingsAsync();
        logger.LogInformation("Backfill complete — succeeded={Succeeded} failed={Failed}", ok, fail);
        return fail == 0 ? 0 : 1;
    }
}
