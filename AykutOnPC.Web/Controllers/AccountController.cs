using AykutOnPC.Core.DTOs;
using AykutOnPC.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AykutOnPC.Web.Controllers;

public class AccountController(IAuthService authService) : Controller
{
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginDto dto, CancellationToken cancellationToken)
    {
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
