using AykutOnPC.Core.Configuration;
using AykutOnPC.Core.Entities;
using AykutOnPC.Core.Interfaces;
using AykutOnPC.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AykutOnPC.Infrastructure.Services;

public class AuthService(AppDbContext context, IOptions<JwtSettings> jwtOptions, IOptions<SecuritySettings> securityOptions) : IAuthService
{
    private readonly JwtSettings _jwtSettings = jwtOptions.Value;
    private readonly SecuritySettings _securitySettings = securityOptions.Value;

    public async Task<User?> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Username == username, cancellationToken);
        if (user is null)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        return user;
    }

    public async Task<User> RegisterUserAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var isFirstUser = !await context.Users.AnyAsync(cancellationToken);
        var userRole = isFirstUser ? "Admin" : "User";

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: _securitySettings.BCryptWorkFactor),
            Role = userRole
        };

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public string GenerateJwtToken(User user, string? role = null)
    {
        var finalRole = role ?? user.Role;
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, finalRole)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<bool> UserExistsAsync(string username, CancellationToken cancellationToken = default)
        => await context.Users.AnyAsync(u => u.Username == username, cancellationToken);
}
