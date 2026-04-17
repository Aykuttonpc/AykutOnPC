using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AykutOnPC.Web.Controllers;

public class AccountController(IAuthService authService, ILogger<AccountController> logger) : Controller
{
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<IActionResult> Login(LoginDto dto, CancellationToken cancellationToken)
    {
        // Honeypot: real users never see/fill the hidden 'Website' field.
        // Bots auto-fill every input — silently reject without leaking that we caught them.
        if (!string.IsNullOrWhiteSpace(dto.Website))
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            logger.LogWarning("Login honeypot triggered. IP={IP} Username={Username}", ip, dto.Username);

            // Mimic the timing of a real BCrypt verification so bots can't time-distinguish.
            await Task.Delay(Random.Shared.Next(800, 1800), cancellationToken);

            ViewBag.Error = "Invalid credentials";
            return View();
        }

        if (!ModelState.IsValid)
            return View();

        var user = await authService.ValidateCredentialsAsync(dto.Username, dto.Password, cancellationToken);

        if (user is null)
        {
            ViewBag.Error = "Invalid credentials";
            return View();
        }

        var jwtString = authService.GenerateJwtToken(user, user.Role);

        Response.Cookies.Append("AykutOnPC.AuthToken", jwtString, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(2)
        });

        return RedirectToAction("Index", "Admin");
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("AykutOnPC.AuthToken");
        return RedirectToAction("Index", "Home");
    }
}
