using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AykutOnPC.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ChatController : ControllerBase
{
    private readonly IAIService _aiService;

    public ChatController(IAIService aiService)
    {
        _aiService = aiService;
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Message cannot be empty");

        // Artificial delay for realism
        await Task.Delay(500);

        var answer = await _aiService.GetAnswerAsync(request.Message);
        return Ok(new { response = answer });
    }
}

public class ChatRequest
{
    public string? Message { get; set; }
}
