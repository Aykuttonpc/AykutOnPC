using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace AykutOnPC.Web.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AccountController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string username, string password)
    {
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == username);
        
        if (user != null && AykutOnPC.Infrastructure.Data.DbInitializer.VerifyPassword(password, user.PasswordHash)) 
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, "Admin")
            };

            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(double.Parse(jwtSettings["DurationInMinutes"]!)),
                signingCredentials: creds
            );

            var jwtString = new JwtSecurityTokenHandler().WriteToken(token);

            Response.Cookies.Append("AykutOnPC.AuthToken", jwtString, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.Now.AddHours(2)
            });

            return RedirectToAction("Index", "Admin");
        }

        ViewBag.Error = "Invalid credentials";
        return View();
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(string username, string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Username and password are required.";
            return View();
        }

        if (password != confirmPassword)
        {
            ViewBag.Error = "Passwords do not match.";
            return View();
        }

        // Check if username already exists
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (existingUser != null)
        {
            ViewBag.Error = "Username already exists.";
            return View();
        }

        // Create new user with hashed password
        var newUser = new AykutOnPC.Core.Entities.User
        {
            Username = username,
            PasswordHash = AykutOnPC.Infrastructure.Data.DbInitializer.HashPassword(password)
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        // Auto-login after registration
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, newUser.Username),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.Now.AddMinutes(double.Parse(jwtSettings["DurationInMinutes"]!)),
            signingCredentials: creds
        );

        var jwtString = new JwtSecurityTokenHandler().WriteToken(token);

        Response.Cookies.Append("AykutOnPC.AuthToken", jwtString, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.Now.AddHours(2)
        });

        return RedirectToAction("Index", "Admin");
    }

    public IActionResult Logout()
    {
        Response.Cookies.Delete("AykutOnPC.AuthToken");
        return RedirectToAction("Index", "Home");
    }
}
