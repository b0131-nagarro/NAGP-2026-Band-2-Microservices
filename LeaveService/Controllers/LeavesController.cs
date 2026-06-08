using System.Security.Claims;
using LeaveService.Data;
using LeaveService.Models;
using LeaveService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveService.Controllers;

[ApiController]
[Route("api/leaves")]
[Authorize]
public class LeavesController(
    LeaveDbContext          db,
    IEmployeeServiceClient  employeeClient,
    IEventPublisher         publisher,
    ILogger<LeavesController> logger,
    IHttpContextAccessor    httpContextAccessor) : ControllerBase
{
    private string UserRole       => User.FindFirstValue(ClaimTypes.Role) ?? "";
    private string UserEmployeeId => User.FindFirstValue("employeeId")    ?? "";
    private string UserId         => User.FindFirstValue("userId")         ?? "";
    private string FullName       => User.FindFirstValue("fullName")       ?? "";

    private string BearerToken =>
        httpContextAccessor.HttpContext?.Request.Headers["Authorization"]
            .ToString().Replace("Bearer ", "") ?? "";

    // TODO: pagination on pending list could be nicer
    [HttpPost("apply")]
    [Authorize(Roles = "Employee")]
    public async Task<ActionResult<LeaveRequestDto>> Apply([FromBody] ApplyLeaveRequest req)
    {
        var employeeId = Guid.Parse(UserEmployeeId);
        var startDate  = ToUtc(req.StartDate);
        var endDate    = ToUtc(req.EndDate);

        // ── Validation 1: valid leave type ────────────────────────────────────
        if (!LeaveTypes.Valid.Contains(req.LeaveType))
            return BadRequest(new { message = $"Invalid leave type. Valid values: {string.Join(", ", LeaveTypes.Valid)}" });

        // ── Validation 2: date range ──────────────────────────────────────────
        if (startDate.Date < DateTime.UtcNow.Date)
            return BadRequest(new { message = "Start date cannot be in the past" });

        if (endDate < startDate)
            return BadRequest(new { message = "End date must be on or after start date" });

        // ── Validation 3: calculate working days (simple – excludes weekends) ─
        var days = CountWorkingDays(startDate, endDate);
        if (days == 0)
            return BadRequest(new { message = "The selected date range contains no working days" });

        // ── Validation 4: check for overlapping requests ──────────────────────
        var hasOverlap = await db.LeaveRequests.AnyAsync(r =>
            r.EmployeeId == employeeId
            && r.Status != LeaveStatus.Rejected
            && r.Status != LeaveStatus.Cancelled
            && r.StartDate <= endDate
            && r.EndDate   >= startDate);

        if (hasOverlap)
            return Conflict(new { message = "You already have a leave request overlapping these dates" });

        // ── Validation 5: sufficient balance (calls EmployeeService) ──────────
        var balance = await employeeClient.GetBalanceAsync(employeeId, req.LeaveType, BearerToken);
        if (balance is null)
        {
            logger.LogError("System error: could not verify leave balance for employee {EmpId} (EmployeeService unavailable)", employeeId);
            return StatusCode(503, new { message = "Unable to verify leave balance. Please try again." });
        }

        if (balance.RemainingDays < days)
            return BadRequest(new
            {
                message = $"Insufficient {req.LeaveType} leave balance. " +
                          $"Requested: {days} day(s), Available: {balance.RemainingDays}"
            });

        var leave = new LeaveRequest
        {
            EmployeeId   = employeeId,
            EmployeeName = FullName,
            LeaveType    = req.LeaveType,
            StartDate    = startDate,
            EndDate      = endDate,
            NumberOfDays = days,
            Reason       = req.Reason,
            ManagerId    = req.ManagerId,
            Status       = LeaveStatus.Pending,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow
        };

        db.LeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Leave request {Id} submitted by employee {EmpId} for {Days} {Type} day(s) starting {Start}",
            leave.Id, employeeId, days, req.LeaveType, req.StartDate.ToString("yyyy-MM-dd"));

        // ── Publish notification event (async) ────────────────────────────────
        await publisher.PublishAsync("LeaveApplied", new
        {
            LeaveRequestId = leave.Id,
            EmployeeId     = leave.EmployeeId,
            EmployeeName   = leave.EmployeeName,
            LeaveType      = leave.LeaveType,
            StartDate      = leave.StartDate,
            EndDate        = leave.EndDate,
            NumberOfDays   = leave.NumberOfDays,
            ManagerId      = leave.ManagerId
        });

        return CreatedAtAction(nameof(GetById), new { id = leave.Id }, ToDto(leave));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/leaves/{id}
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LeaveRequestDto>> GetById(Guid id)
    {
        var leave = await db.LeaveRequests.FindAsync(id);
        if (leave is null) return NotFound(new { message = "Leave request not found" });

        // Employees can only view their own requests
        if (UserRole == "Employee" && leave.EmployeeId.ToString() != UserEmployeeId)
            return Forbid();

        return Ok(ToDto(leave));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/leaves/my-history
    // Employee views their own leave history with pagination + status filter.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet("my-history")]
    [Authorize(Roles = "Employee")]
    public async Task<ActionResult<PaginatedResult<LeaveRequestDto>>> MyHistory(
        [FromQuery] string? status,
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 10)
    {
        var employeeId = Guid.Parse(UserEmployeeId);
        var query      = db.LeaveRequests.Where(r => r.EmployeeId == employeeId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        return Ok(await Paginate(query, page, pageSize));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/leaves/pending-approvals
    // Manager views all pending requests for their team, with filters.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpGet("pending-approvals")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<PaginatedResult<LeaveRequestDto>>> PendingApprovals(
        [FromQuery] string?   status,
        [FromQuery] Guid?     employeeId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int       page     = 1,
        [FromQuery] int       pageSize = 10)
    {
        var managerId = Guid.Parse(UserId);
        var query = db.LeaveRequests.Where(r => r.ManagerId == managerId);

        // Apply optional filters
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);
        else
            query = query.Where(r => r.Status == LeaveStatus.Pending); // default to pending

        if (employeeId.HasValue)
            query = query.Where(r => r.EmployeeId == employeeId.Value);

        if (from.HasValue)
            query = query.Where(r => r.StartDate >= DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc));

        if (to.HasValue)
            query = query.Where(r => r.EndDate <= DateTime.SpecifyKind(to.Value.Date, DateTimeKind.Utc));

        return Ok(await Paginate(query, page, pageSize));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUT /api/leaves/{id}/approve
    // Manager approves or rejects a pending leave request.
    // On approval: calls EmployeeService to deduct balance.
    // Both outcomes publish a notification event.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPut("{id:guid}/approve")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<LeaveRequestDto>> ProcessApproval(
        Guid id, [FromBody] ApprovalRequest req)
    {
        if (req.Action != "Approve" && req.Action != "Reject")
            return BadRequest(new { message = "Action must be 'Approve' or 'Reject'" });

        if (req.Action == "Reject" && string.IsNullOrWhiteSpace(req.RejectionReason))
            return BadRequest(new { message = "Rejection reason is required when rejecting a request" });

        var leave = await db.LeaveRequests.FindAsync(id);
        if (leave is null) return NotFound(new { message = "Leave request not found" });

        // Ensure this manager owns the request (RBAC enforcement)
        var managerId = Guid.Parse(UserId);
        if (leave.ManagerId != managerId)
            return Forbid();

        if (leave.Status != LeaveStatus.Pending)
            return Conflict(new { message = $"Cannot process a request that is already {leave.Status}" });

        if (req.Action == "Approve")
        {
            // deduct balance on employee service
            var deducted = await employeeClient.DeductBalanceAsync(
                leave.EmployeeId, leave.LeaveType, leave.NumberOfDays, BearerToken);

            if (!deducted)
                return StatusCode(503, new
                {
                    message = "Could not deduct leave balance from EmployeeService. Approval aborted."
                });

            leave.Status     = LeaveStatus.Approved;
            leave.ApprovedBy = managerId;
            leave.ApprovedAt = DateTime.UtcNow;

            logger.LogInformation("Leave {Id} approved by manager {ManagerId}", id, managerId);

            await publisher.PublishAsync("LeaveApproved", new
            {
                LeaveRequestId = leave.Id,
                EmployeeId     = leave.EmployeeId,
                EmployeeName   = leave.EmployeeName,
                LeaveType      = leave.LeaveType,
                NumberOfDays   = leave.NumberOfDays,
                ApprovedBy     = managerId
            });
        }
        else // Reject
        {
            leave.Status          = LeaveStatus.Rejected;
            leave.RejectionReason = req.RejectionReason;

            logger.LogInformation("Leave {Id} rejected by manager {ManagerId}: {Reason}",
                id, managerId, req.RejectionReason);

            await publisher.PublishAsync("LeaveRejected", new
            {
                LeaveRequestId  = leave.Id,
                EmployeeId      = leave.EmployeeId,
                EmployeeName    = leave.EmployeeName,
                LeaveType       = leave.LeaveType,
                RejectionReason = leave.RejectionReason,
                RejectedBy      = managerId
            });
        }

        leave.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(ToDto(leave));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUT /api/leaves/{id}/cancel
    // Employee cancels their own pending request.
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPut("{id:guid}/cancel")]
    [Authorize(Roles = "Employee")]
    public async Task<ActionResult<LeaveRequestDto>> Cancel(Guid id)
    {
        var leave = await db.LeaveRequests.FindAsync(id);
        if (leave is null) return NotFound(new { message = "Leave request not found" });

        if (leave.EmployeeId.ToString() != UserEmployeeId)
            return Forbid();

        if (leave.Status != LeaveStatus.Pending)
            return Conflict(new { message = "Only pending requests can be cancelled" });

        leave.Status    = LeaveStatus.Cancelled;
        leave.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        logger.LogInformation("Leave request {Id} cancelled by employee {EmpId}", id, UserEmployeeId);

        return Ok(ToDto(leave));
    }

    // GET /api/leaves/health
    [HttpGet("/health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "Healthy", service = "LeaveService" });

    // ── Helpers ───────────────────────────────────────────────────────────────

    // postgres timestamptz needs UTC
    private static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    private static int CountWorkingDays(DateTime start, DateTime end)
    {
        int count = 0;
        for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
        {
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                count++;
        }
        return count;
    }

    private static async Task<PaginatedResult<LeaveRequestDto>> Paginate(
        IQueryable<LeaveRequest> query, int page, int pageSize)
    {
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => ToDto(r))
            .ToListAsync();

        return new PaginatedResult<LeaveRequestDto>(
            items, page, pageSize, total, (int)Math.Ceiling((double)total / pageSize));
    }

    private static LeaveRequestDto ToDto(LeaveRequest r) => new(
        r.Id, r.EmployeeId, r.EmployeeName, r.LeaveType,
        r.StartDate, r.EndDate, r.NumberOfDays, r.Reason,
        r.ManagerId, r.Status, r.RejectionReason, r.CreatedAt, r.UpdatedAt);
}
