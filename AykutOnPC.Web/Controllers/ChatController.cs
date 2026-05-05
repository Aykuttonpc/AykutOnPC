using AykutOnPC.Core.Configuration;
using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AykutOnPC.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting("ChatApiPolicy")]
public class ChatController(
    IAiService aiService,
    IChatLogService chatLogs,
    IOptions<AiSettings> aiOptions) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // Pre-flight rules: cheap, deterministic checks that catch garbage before the model is called.
    // Anything matching here returns Validation kind + suggestion chips, model is never invoked.
    private static readonly Regex OnlyNonWordRx = new(@"^[\W_]+$", RegexOptions.Compiled);
    private static readonly Regex SingleCharRepeatRx = new(@"^(.)\1{4,}$", RegexOptions.Compiled);

    private AiSettings Settings => aiOptions.Value;

    /// <summary>Non-streaming chat. Used by curl/cron callers and as the SSE fallback in the JS widget.</summary>
    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var conversationId = request.ConversationId is { } id && id != Guid.Empty ? id : Guid.NewGuid();
        var (hashedIp, userAgent) = ResolveCallerIdentity();

        // ── Pre-flight ────────────────────────────────────────────
        if (TryPreFlight(request.Message) is { } preflightKind)
        {
            var preflightMessage = MapKindToMessage(preflightKind);
            await LogAsync(conversationId, request.Message, preflightMessage, preflightKind, latencyMs: 0,
                modelId: Settings.ModelId, shortCircuited: true, hashedIp, userAgent, cancellationToken);

            return Ok(new ChatResponseDto
            {
                Response       = preflightMessage,
                Kind           = preflightKind.ToString(),
                ConversationId = conversationId,
                Suggestions    = MapSuggestions()
            });
        }

        // ── Memory + model call ───────────────────────────────────
        var history = await chatLogs.GetRecentTurnsAsync(conversationId, Settings.ConversationMemoryTurns, cancellationToken);

        var sw     = Stopwatch.StartNew();
        var result = await aiService.AnswerAsync(request.Message, history, cancellationToken);
        sw.Stop();

        await LogAsync(conversationId, request.Message, result.Content, result.Kind, (int)sw.ElapsedMilliseconds,
            result.ModelId, shortCircuited: false, hashedIp, userAgent, cancellationToken);

        return Ok(new ChatResponseDto
        {
            Response       = result.Content,
            Kind           = result.Kind.ToString(),
            ConversationId = conversationId,
            Suggestions    = ShouldOfferChips(result.Kind) ? MapSuggestions() : null
        });
    }

    /// <summary>
    /// Streaming chat (SSE). Each token is "data: {\"t\":\"<chunk>\"}\n\n".
    /// The terminal frame is "event: done\ndata: {\"kind\":\"...\",\"conversationId\":\"...\",\"suggestions\":[...]}\n\n".
    /// </summary>
    [HttpPost("stream")]
    public async Task Stream([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync(JsonSerializer.Serialize(ModelState), cancellationToken);
            return;
        }

        var conversationId = request.ConversationId is { } id && id != Guid.Empty ? id : Guid.NewGuid();
        var (hashedIp, userAgent) = ResolveCallerIdentity();

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl    = "no-cache, no-transform";
        Response.Headers.Pragma          = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.Headers.Connection      = "keep-alive";

        await Response.Body.FlushAsync(cancellationToken);

        // ── Pre-flight short-circuit ──────────────────────────────
        if (TryPreFlight(request.Message) is { } preflightKind)
        {
            var preflightMessage = MapKindToMessage(preflightKind);
            await WriteTokenAsync(preflightMessage, cancellationToken);
            await WriteDoneAsync(preflightKind, conversationId, MapSuggestions(), cancellationToken);
            await LogAsync(conversationId, request.Message, preflightMessage, preflightKind, latencyMs: 0,
                modelId: Settings.ModelId, shortCircuited: true, hashedIp, userAgent, CancellationToken.None);
            return;
        }

        // ── Real model stream ─────────────────────────────────────
        var history     = await chatLogs.GetRecentTurnsAsync(conversationId, Settings.ConversationMemoryTurns, cancellationToken);
        var sw          = Stopwatch.StartNew();
        var fullContent = string.Empty;
        var finalKind   = ChatErrorKind.Ok;
        var modelId     = Settings.ModelId;
        var clientGone  = false;

        try
        {
            await foreach (var chunk in aiService.StreamAnswerAsync(request.Message, history, cancellationToken))
            {
                if (chunk.FinalKind is { } kind)
                {
                    finalKind   = kind;
                    fullContent = chunk.FullContent ?? fullContent;
                    modelId     = chunk.ModelId    ?? modelId;
                    break;
                }

                if (!string.IsNullOrEmpty(chunk.Token))
                {
                    fullContent += chunk.Token;
                    await WriteTokenAsync(chunk.Token, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            clientGone = true;
        }

        sw.Stop();

        // The done event is best-effort: if the client disconnected mid-stream, skip writing
        // (no point) but still record the log so admin telemetry shows abandoned turns.
        if (!clientGone)
        {
            await WriteDoneAsync(finalKind, conversationId,
                ShouldOfferChips(finalKind) ? MapSuggestions() : null, cancellationToken);
        }

        // Logging always runs (use a fresh token so a disconnected client doesn't suppress the write).
        await LogAsync(conversationId, request.Message, fullContent, finalKind, (int)sw.ElapsedMilliseconds,
            modelId, shortCircuited: false, hashedIp, userAgent, CancellationToken.None);
    }

    /// <summary>Static suggestion chips for the welcome state and recovery from empty/safety/validation outcomes.</summary>
    [HttpGet("suggestions")]
    public IActionResult GetSuggestions() => Ok(MapSuggestions() ?? new List<SuggestionDto>());

    // ── Helpers ──────────────────────────────────────────────────────

    private static ChatErrorKind? TryPreFlight(string message)
    {
        var trimmed = message?.Trim() ?? string.Empty;
        if (trimmed.Length < 2)              return ChatErrorKind.Validation;
        if (OnlyNonWordRx.IsMatch(trimmed))  return ChatErrorKind.Validation;
        if (SingleCharRepeatRx.IsMatch(trimmed)) return ChatErrorKind.Validation;
        return null;
    }

    private static bool ShouldOfferChips(ChatErrorKind kind) =>
        kind is ChatErrorKind.Empty or ChatErrorKind.Safety or ChatErrorKind.Validation;

    private string MapKindToMessage(ChatErrorKind kind) => kind switch
    {
        ChatErrorKind.NotConfigured => Settings.ErrorMessages.ApiNotConfigured,
        ChatErrorKind.RateLimit     => Settings.ErrorMessages.RateLimitHit,
        ChatErrorKind.Empty         => Settings.ErrorMessages.EmptyResponse,
        ChatErrorKind.Safety        => Settings.ErrorMessages.SafetyBlocked,
        ChatErrorKind.Network       => Settings.ErrorMessages.NetworkError,
        ChatErrorKind.Validation    => Settings.ErrorMessages.ValidationError,
        _                           => Settings.ErrorMessages.GeneralError
    };

    private List<SuggestionDto>? MapSuggestions()
    {
        if (Settings.Suggestions is null || Settings.Suggestions.Count == 0) return null;
        return Settings.Suggestions
            .Select(s => new SuggestionDto { Label = s.Label, Prompt = s.Prompt })
            .ToList();
    }

    private async Task WriteTokenAsync(string token, CancellationToken ct)
    {
        var line = $"data: {JsonSerializer.Serialize(new { t = token }, JsonOpts)}\n\n";
        await Response.WriteAsync(line, ct);
        await Response.Body.FlushAsync(ct);
    }

    private async Task WriteDoneAsync(ChatErrorKind kind, Guid conversationId, List<SuggestionDto>? suggestions, CancellationToken ct)
    {
        var payload = new
        {
            kind           = kind.ToString(),
            conversationId = conversationId.ToString(),
            suggestions
        };
        var line = $"event: done\ndata: {JsonSerializer.Serialize(payload, JsonOpts)}\n\n";
        try
        {
            await Response.WriteAsync(line, ct);
            await Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException) { /* client gone, ignore */ }
    }

    private (string HashedIp, string? UserAgent) ResolveCallerIdentity()
    {
        var ipRaw     = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                     ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";
        var dailySalt = DateTime.UtcNow.ToString("yyyyMMdd");
        var hash      = SHA256.HashData(Encoding.UTF8.GetBytes($"{ipRaw}:{dailySalt}"));
        var hashedIp  = Convert.ToHexString(hash).ToLowerInvariant();

        var ua = HttpContext.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(ua)) ua = null;
        else if (ua.Length > 512)          ua = ua[..512];

        return (hashedIp, ua);
    }

    private async Task LogAsync(
        Guid conversationId,
        string userMessage,
        string botResponse,
        ChatErrorKind kind,
        int latencyMs,
        string modelId,
        bool shortCircuited,
        string hashedIp,
        string? userAgent,
        CancellationToken ct)
    {
        var turnIndex = await chatLogs.GetNextTurnIndexAsync(conversationId, ct);
        var truncatedResponse = botResponse?.Length > 8000 ? botResponse[..8000] : botResponse;

        await chatLogs.RecordAsync(new ChatLog
        {
            ConversationId = conversationId,
            TurnIndex      = turnIndex,
            UserMessage    = userMessage.Length > 2000 ? userMessage[..2000] : userMessage,
            BotResponse    = truncatedResponse,
            Kind           = kind.ToString(),
            LatencyMs      = latencyMs,
            ModelId        = modelId,
            ShortCircuited = shortCircuited,
            HashedIp       = hashedIp,
            UserAgent      = userAgent
        }, ct);
    }
}
