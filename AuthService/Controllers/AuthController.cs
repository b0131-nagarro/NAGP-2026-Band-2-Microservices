using AuthService.Data;
using AuthService.Models;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AuthDbContext   db,
    ITokenService   tokenService,
    ILogger<AuthController> logger) : ControllerBase
{
    // POST /api/auth/login
    // Public – no JWT required. Returns a signed JWT on success.
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        logger.LogInformation("Login attempt for username: {Username}", request.Username);

        // Find user by username (case-insensitive)
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower()
                                   && u.IsActive);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for username: {Username}", request.Username);
            // Return 401 with a vague message – don't reveal which part was wrong
            return Unauthorized(new { message = "Invalid username or password" });
        }

        var (token, expiresAt) = tokenService.GenerateToken(user);

        logger.LogInformation("Successful login for {Username} with role {Role}", user.Username, user.Role);

        return Ok(new LoginResponse(
            Token:     token,
            TokenType: "Bearer",
            ExpiresAt: expiresAt,
            User: new UserInfo(
                Id:         user.Id,
                Username:   user.Username,
                FullName:   user.FullName,
                Email:      user.Email,
                Role:       user.Role,
                EmployeeId: user.EmployeeId)));
    }

    // GET /api/auth/me – returns claims from the current JWT (useful for debugging)
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value });
        return Ok(claims);
    }

    // GET /api/auth/health
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "Healthy", service = "AuthService" });
}
