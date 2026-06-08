namespace LeaveService.Models;

/// <summary>
/// The central aggregate for this service. Tracks a single leave application
/// from submission through approval/rejection.
/// </summary>
public class LeaveRequest
{
    public Guid     Id          { get; set; } = Guid.NewGuid();
    public Guid     EmployeeId  { get; set; }
    public string   EmployeeName{ get; set; } = string.Empty;

    /// <summary>"Casual" | "Sick" | "Privilege"</summary>
    public string   LeaveType   { get; set; } = string.Empty;

    public DateTime StartDate   { get; set; }
    public DateTime EndDate     { get; set; }
    public int      NumberOfDays{ get; set; }
    public string   Reason      { get; set; } = string.Empty;

    /// <summary>The UserId of the manager the employee reports to.</summary>
    public Guid     ManagerId   { get; set; }

    /// <summary>"Pending" | "Approved" | "Rejected" | "Cancelled"</summary>
    public string   Status      { get; set; } = LeaveStatus.Pending;

    public string?  RejectionReason { get; set; }
    public Guid?    ApprovedBy      { get; set; }
    public DateTime? ApprovedAt     { get; set; }

    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt   { get; set; } = DateTime.UtcNow;
}

/// <summary>Strongly-typed status constants to avoid magic strings.</summary>
public static class LeaveStatus
{
    public const string Pending   = "Pending";
    public const string Approved  = "Approved";
    public const string Rejected  = "Rejected";
    public const string Cancelled = "Cancelled";
}

/// <summary>Valid leave type values.</summary>
public static class LeaveTypes
{
    public static readonly HashSet<string> Valid =
        ["Casual", "Sick", "Privilege"];
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record ApplyLeaveRequest(
    string   LeaveType,
    DateTime StartDate,
    DateTime EndDate,
    string   Reason,
    Guid     ManagerId);

public record ApprovalRequest(
    string  Action,           // "Approve" or "Reject"
    string? RejectionReason); // required when Action == "Reject"

public record LeaveRequestDto(
    Guid      Id,
    Guid      EmployeeId,
    string    EmployeeName,
    string    LeaveType,
    DateTime  StartDate,
    DateTime  EndDate,
    int       NumberOfDays,
    string    Reason,
    Guid      ManagerId,
    string    Status,
    string?   RejectionReason,
    DateTime  CreatedAt,
    DateTime  UpdatedAt);

public record PaginatedResult<T>(
    IEnumerable<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
