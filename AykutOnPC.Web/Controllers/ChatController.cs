using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AykutOnPC.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting("ChatApiPolicy")]
public class ChatController(IAIService aiService) : ControllerBase
{
    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var answer = await aiService.GetAnswerAsync(request.Message, cancellationToken);
        return Ok(new { response = answer });
    }
}
