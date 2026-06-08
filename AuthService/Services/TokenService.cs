using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthService.Models;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Services;

/// <summary>
/// Generates signed JWT tokens containing userId, role, and employeeId claims.
/// The token is validated by the API Gateway and by each downstream service.
/// </summary>
public interface ITokenService
{
    (string Token, DateTime ExpiresAt) GenerateToken(User user);
}

public class TokenService(IConfiguration config) : ITokenService
{
    public (string Token, DateTime ExpiresAt) GenerateToken(User user)
    {
        var jwtConfig   = config.GetSection("Jwt");
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig["Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryMins  = int.Parse(jwtConfig["ExpiryMinutes"] ?? "60");
        var expiresAt   = DateTime.UtcNow.AddMinutes(expiryMins);

        // ── Claims embedded in the token ─────────────────────────────────────
        // Downstream services extract these without a DB round-trip.
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("userId",     user.Id.ToString()),
            new Claim("role",       user.Role),
            new Claim("fullName",   user.FullName),
            // employeeId allows leave/employee services to scope data without
            // an extra lookup – empty string for managers
            new Claim("employeeId", user.EmployeeId?.ToString() ?? string.Empty),
            // Standard ASP.NET role claim for [Authorize(Roles="Manager")] etc.
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer:             jwtConfig["Issuer"],
            audience:           jwtConfig["Audience"],
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
