namespace EmployeeService.Models;

/// <summary>
/// Core employee record. Linked to a User in AuthService via Id.
/// </summary>
public class Employee
{
    public Guid     Id          { get; set; } = Guid.NewGuid();
    public string   FullName    { get; set; } = string.Empty;
    public string   Email       { get; set; } = string.Empty;
    public string   Department  { get; set; } = string.Empty;
    public string   Designation { get; set; } = string.Empty;

    /// <summary>The UserId of the manager responsible for approving this employee's leaves.</summary>
    public Guid     ManagerId   { get; set; }

    public DateTime JoinDate    { get; set; } = DateTime.UtcNow;
    public bool     IsActive    { get; set; } = true;
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;

    // Navigation – one employee has many leave-balance entries (one per type)
    public ICollection<LeaveBalance> LeaveBalances { get; set; } = [];
}

/// <summary>
/// Tracks the annual leave quota for one employee for one leave type.
/// Auto-allocated when an employee record is first created.
/// </summary>
public class LeaveBalance
{
    public Guid   Id         { get; set; } = Guid.NewGuid();
    public Guid   EmployeeId { get; set; }

    /// <summary>"Casual", "Sick", "Privilege"</summary>
    public string LeaveType  { get; set; } = string.Empty;

    public int TotalAllocated { get; set; } // fixed annual quota
    public int UsedDays       { get; set; } // deducted on approval
    public int RemainingDays  => TotalAllocated - UsedDays; // computed – not stored

    public int Year { get; set; } = DateTime.UtcNow.Year;

    public Employee Employee { get; set; } = null!;
}

// ── Static leave quotas per the assignment spec ───────────────────────────────
public static class LeaveQuota
{
    public const int Casual    = 12;
    public const int Sick      = 10;
    public const int Privilege = 15;

    public static readonly string[] Types = ["Casual", "Sick", "Privilege"];
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record EmployeeDto(
    Guid     Id,
    string   FullName,
    string   Email,
    string   Department,
    string   Designation,
    Guid     ManagerId,
    DateTime JoinDate);

public record LeaveBalanceDto(
    Guid   EmployeeId,
    string LeaveType,
    int    TotalAllocated,
    int    UsedDays,
    int    RemainingDays,
    int    Year);

public record DeductLeaveRequest(
    Guid   EmployeeId,
    string LeaveType,
    int    Days);

public record DeductLeaveResponse(bool Success, string Message, int NewBalance);
