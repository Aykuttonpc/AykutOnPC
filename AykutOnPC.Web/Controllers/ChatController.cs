using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;

namespace AykutOnPC.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting("ChatApiPolicy")]
public class ChatController(IAiService aiService) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Non-streaming chat. Kept for compatibility (curl/cron callers).
    /// Returns the full response in a single JSON payload.
    /// </summary>
    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var answer = await aiService.GetAnswerAsync(request.Message, cancellationToken);
        return Ok(new { response = answer });
    }

    /// <summary>
    /// Streaming chat (Server-Sent Events). Each token is pushed as it arrives from the model.
    /// Wire format: lines of "data: {\"t\":\"<chunk>\"}\n\n" followed by a final "event: done\ndata: {}\n\n".
    /// Frontend reads via fetch + ReadableStream (EventSource is GET-only).
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

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl    = "no-cache, no-transform";
        Response.Headers.Pragma          = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // disable nginx response buffering
        Response.Headers.Connection      = "keep-alive";

        await Response.Body.FlushAsync(cancellationToken);

        try
        {
            await foreach (var chunk in aiService.GetStreamingAnswerAsync(request.Message, cancellationToken))
            {
                if (string.IsNullOrEmpty(chunk)) continue;

                var line = $"data: {JsonSerializer.Serialize(new { t = chunk }, JsonOpts)}\n\n";
                await Response.WriteAsync(line, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — nothing more to send.
            return;
        }

        await Response.WriteAsync("event: done\ndata: {}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
