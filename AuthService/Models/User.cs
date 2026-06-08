namespace AuthService.Models;

/// <summary>
/// Represents a system user. Employees and Managers are both stored here.
/// Role drives authorization throughout all services.
/// </summary>
public class User
{
    public Guid   Id           { get; set; } = Guid.NewGuid();
    public string Username     { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty; // BCrypt hash – never plaintext
    public string Email        { get; set; } = string.Empty;
    public string FullName     { get; set; } = string.Empty;

    /// <summary>"Employee" or "Manager"</summary>
    public string Role { get; set; } = "Employee";

    /// <summary>
    /// The EmployeeService employee record this user maps to.
    /// Null for managers who have no leave-balance record.
    /// </summary>
    public Guid? EmployeeId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool     IsActive  { get; set; } = true;
}

// ── Request / Response DTOs ──────────────────────────────────────────────────

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string Token,
    string TokenType,
    DateTime ExpiresAt,
    UserInfo User);

public record UserInfo(
    Guid   Id,
    string Username,
    string FullName,
    string Email,
    string Role,
    Guid?  EmployeeId);
