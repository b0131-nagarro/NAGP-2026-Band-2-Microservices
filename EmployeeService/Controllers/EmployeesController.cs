using System.Security.Claims;
using EmployeeService.Data;
using EmployeeService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmployeeService.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize] // all endpoints require a valid JWT
public class EmployeesController(
    EmployeeDbContext db,
    ILogger<EmployeesController> logger) : ControllerBase
{
    // ── Helpers to extract JWT claims ─────────────────────────────────────────
    private string UserRole       => User.FindFirstValue(ClaimTypes.Role) ?? "";
    private string UserEmployeeId => User.FindFirstValue("employeeId")    ?? "";
    private string UserId         => User.FindFirstValue("userId")         ?? "";

    // GET /api/employees
    // Managers see all employees; employees cannot list others.
    [HttpGet]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetAll()
    {
        var employees = await db.Employees
            .Where(e => e.IsActive)
            .Select(e => ToDto(e))
            .ToListAsync();

        return Ok(employees);
    }

    // GET /api/employees/{id}
    // Employees can only fetch their own record; managers can fetch any.
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> GetById(Guid id)
    {
        // Enforce: employees can only see their own profile
        if (UserRole == "Employee" && UserEmployeeId != id.ToString())
            return Forbid();

        var emp = await db.Employees.FindAsync(id);
        if (emp is null || !emp.IsActive) return NotFound(new { message = "Employee not found" });

        return Ok(ToDto(emp));
    }

    // GET /api/employees/{id}/balances
    // Returns all leave balance entries for the specified employee.
    [HttpGet("{id:guid}/balances")]
    public async Task<ActionResult<IEnumerable<LeaveBalanceDto>>> GetBalances(Guid id, [FromQuery] int? year)
    {
        // Employees can only query their own balances
        if (UserRole == "Employee" && UserEmployeeId != id.ToString())
            return Forbid();

        var targetYear = year ?? DateTime.UtcNow.Year;

        var balances = await db.LeaveBalances
            .Where(b => b.EmployeeId == id && b.Year == targetYear)
            .ToListAsync();

        if (!balances.Any())
            return NotFound(new { message = $"No leave balances found for employee {id} in year {targetYear}" });

        return Ok(balances.Select(b => new LeaveBalanceDto(
            b.EmployeeId, b.LeaveType, b.TotalAllocated, b.UsedDays, b.RemainingDays, b.Year)));
    }

    // GET /api/employees/my-team – manager views their direct reports
    [HttpGet("my-team")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<IEnumerable<EmployeeDto>>> GetMyTeam()
    {
        var managerId = Guid.Parse(UserId);

        var team = await db.Employees
            .Where(e => e.ManagerId == managerId && e.IsActive)
            .Select(e => ToDto(e))
            .ToListAsync();

        return Ok(team);
    }

    // POST /api/employees – create a new employee (manager only)
    // Automatically allocates leave balances on creation.
    [HttpPost]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<EmployeeDto>> Create([FromBody] CreateEmployeeRequest req)
    {
        if (await db.Employees.AnyAsync(e => e.Email == req.Email))
            return Conflict(new { message = "An employee with this email already exists" });

        var employee = new Employee
        {
            FullName    = req.FullName,
            Email       = req.Email,
            Department  = req.Department,
            Designation = req.Designation,
            ManagerId   = req.ManagerId,
            JoinDate    = req.JoinDate.HasValue
                ? DateTime.SpecifyKind(req.JoinDate.Value.Date, DateTimeKind.Utc)
                : DateTime.UtcNow
        };

        db.Employees.Add(employee);

        // ── Auto-allocate leave balances per assignment spec ──────────────────
        var currentYear = DateTime.UtcNow.Year;
        db.LeaveBalances.AddRange(
            new LeaveBalance { EmployeeId = employee.Id, LeaveType = "Casual",   TotalAllocated = LeaveQuota.Casual,    Year = currentYear },
            new LeaveBalance { EmployeeId = employee.Id, LeaveType = "Sick",     TotalAllocated = LeaveQuota.Sick,      Year = currentYear },
            new LeaveBalance { EmployeeId = employee.Id, LeaveType = "Privilege",TotalAllocated = LeaveQuota.Privilege, Year = currentYear }
        );

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Employee {FullName} ({Id}) created with auto-allocated leave balances",
            employee.FullName, employee.Id);

        return CreatedAtAction(nameof(GetById), new { id = employee.Id }, ToDto(employee));
    }

    // PUT /api/employees/{id}/balances/deduct
    // Internal endpoint called by LeaveService on approval.
    // Protected by role; typically called with a service-to-service JWT.
    [HttpPut("{id:guid}/balances/deduct")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<DeductLeaveResponse>> DeductBalance(Guid id, [FromBody] DeductLeaveRequest req)
    {
        var currentYear = DateTime.UtcNow.Year;

        var balance = await db.LeaveBalances
            .FirstOrDefaultAsync(b => b.EmployeeId == id
                                   && b.LeaveType == req.LeaveType
                                   && b.Year == currentYear);

        if (balance is null)
            return NotFound(new { message = $"No {req.LeaveType} balance found for employee {id}" });

        if (balance.RemainingDays < req.Days)
            return BadRequest(new DeductLeaveResponse(false,
                $"Insufficient balance. Requested {req.Days}, available {balance.RemainingDays}", balance.RemainingDays));

        balance.UsedDays += req.Days;
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Deducted {Days} {LeaveType} day(s) from employee {Id}. New balance: {Remaining}",
            req.Days, req.LeaveType, id, balance.RemainingDays);

        return Ok(new DeductLeaveResponse(true, "Balance deducted successfully", balance.RemainingDays));
    }

    // GET /api/employees/health
    [HttpGet("/health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "Healthy", service = "EmployeeService" });

    // ── Projection helper ─────────────────────────────────────────────────────
    private static EmployeeDto ToDto(Employee e) => new(
        e.Id, e.FullName, e.Email, e.Department, e.Designation, e.ManagerId, e.JoinDate);
}

// ── Request model for employee creation ──────────────────────────────────────
public record CreateEmployeeRequest(
    string    FullName,
    string    Email,
    string    Department,
    string    Designation,
    Guid      ManagerId,
    DateTime? JoinDate);
