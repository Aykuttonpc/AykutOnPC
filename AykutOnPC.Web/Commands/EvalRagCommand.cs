using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AykutOnPC.Web.Commands;

/// <summary>
/// Run-and-exit CLI: <c>dotnet AykutOnPC.Web.dll --eval-rag</c>.
/// Reads <c>.claudeteam/EVAL_SET/rag-eval.json</c>, asks each question through the
/// real chat path (so it exercises retrieval + generation end-to-end), and checks
/// whether the answer mentions every keyword in <c>mustMention</c>. Produces a
/// pass/fail tally + per-item detail. Keyword-check is a coarse first filter — a
/// future iteration can add LLM-as-judge for nuance, but keyword-pass is enough
/// to catch "RAG retrieved nothing" and "model hallucinated something off-topic"
/// regressions.
/// </summary>
public static class EvalRagCommand
{
    public const string ArgFlag = "--eval-rag";
    private const string EvalSetPath = ".claudeteam/EVAL_SET/rag-eval.json";

    public static async Task<int> RunAsync(IServiceProvider services, ILogger logger)
    {
        if (!File.Exists(EvalSetPath))
        {
            logger.LogError("Eval set not found at {Path} — run from repo root.", EvalSetPath);
            return 2;
        }

        var json = await File.ReadAllTextAsync(EvalSetPath);
        var evalSet = JsonSerializer.Deserialize<EvalSet>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (evalSet?.Items is null || evalSet.Items.Count == 0)
        {
            logger.LogError("Eval set parsed but contains no items.");
            return 2;
        }

        using var scope = services.CreateScope();
        var ai = scope.ServiceProvider.GetRequiredService<IAiService>();

        logger.LogInformation("Eval start — {Count} items against model {Model}",
            evalSet.Items.Count, evalSet.Model);

        var passed = 0;
        var failed = 0;
        var results = new List<string>();

        foreach (var item in evalSet.Items)
        {
            var emptyHistory = Array.Empty<ChatLog>();
            var result = await ai.AnswerAsync(item.Question, emptyHistory);

            if (result.Kind != ChatErrorKind.Ok)
            {
                failed++;
                results.Add($"  ❌ {item.Id}: error={result.Kind} q=\"{Truncate(item.Question, 60)}\"");
                continue;
            }

            var answer = result.Content ?? string.Empty;
            var missing = item.MustMention
                .Where(kw => answer.IndexOf(kw, StringComparison.OrdinalIgnoreCase) < 0)
                .ToList();

            if (missing.Count == 0)
            {
                passed++;
                results.Add($"  ✅ {item.Id}: {Truncate(item.Question, 60)}");
            }
            else
            {
                failed++;
                results.Add($"  ❌ {item.Id}: missing=[{string.Join(", ", missing)}] q=\"{Truncate(item.Question, 60)}\"");
            }
        }

        logger.LogInformation("Eval done — passed={Passed} failed={Failed} score={Score}%",
            passed, failed, passed * 100 / evalSet.Items.Count);
        foreach (var line in results) logger.LogInformation("{Line}", line);

        // Exit non-zero only if more than half fail — small misses are expected on
        // first run, will tune. CI can flip this to "any failure = exit 1".
        return failed > evalSet.Items.Count / 2 ? 1 : 0;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private sealed class EvalSet
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("items")] public List<EvalItem> Items { get; set; } = new();
    }
    private sealed class EvalItem
    {
        [JsonPropertyName("id")]          public string Id          { get; set; } = string.Empty;
        [JsonPropertyName("question")]    public string Question    { get; set; } = string.Empty;
        [JsonPropertyName("mustMention")] public List<string> MustMention { get; set; } = new();
        [JsonPropertyName("category")]    public string Category    { get; set; } = string.Empty;
    }
}
