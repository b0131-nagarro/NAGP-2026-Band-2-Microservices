using EmployeeService.Data;
using EmployeeService.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeService.Services;

public static class DataSeeder
{
    public static async Task SeedAsync(EmployeeDbContext db)
    {
        var mgr1 = new Guid("11111111-1111-1111-1111-111111111111");
        var mgr2 = new Guid("22222222-2222-2222-2222-222222222222");
        var seed = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var year = DateTime.UtcNow.Year;

        var employees = new[]
        {
            new Employee { Id = new Guid("aaaaaaa1-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), FullName = "Charlie Brown", Email = "employee1@company.com", Department = "Engineering", Designation = "Software Engineer",  ManagerId = mgr1, JoinDate = seed, CreatedAt = seed },
            new Employee { Id = new Guid("aaaaaaa2-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), FullName = "Diana Prince",  Email = "employee2@company.com", Department = "Engineering", Designation = "Senior Engineer",    ManagerId = mgr1, JoinDate = seed, CreatedAt = seed },
            new Employee { Id = new Guid("aaaaaaa3-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), FullName = "Eve Adams",     Email = "employee3@company.com", Department = "HR",          Designation = "HR Specialist",      ManagerId = mgr2, JoinDate = seed, CreatedAt = seed }
        };

        foreach (var emp in employees)
        {
            if (await db.Employees.AnyAsync(e => e.Id == emp.Id))
                continue;

            db.Employees.Add(emp);
            AddBalances(db, emp.Id, year);
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // replica might seed at same time
            foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).ToList())
                entry.State = EntityState.Detached;
        }

        await EnsureCurrentYearBalancesAsync(db);
    }

    public static async Task EnsureCurrentYearBalancesAsync(EmployeeDbContext db)
    {
        var year = DateTime.UtcNow.Year;
        var employees = await db.Employees.Where(e => e.IsActive).ToListAsync();

        foreach (var emp in employees)
        {
            if (await db.LeaveBalances.AnyAsync(b => b.EmployeeId == emp.Id && b.Year == year))
                continue;

            AddBalances(db, emp.Id, year);
        }

        await db.SaveChangesAsync();
    }

    private static void AddBalances(EmployeeDbContext db, Guid employeeId, int year)
    {
        db.LeaveBalances.AddRange(
            new LeaveBalance { EmployeeId = employeeId, LeaveType = "Casual",    TotalAllocated = LeaveQuota.Casual,    UsedDays = 0, Year = year },
            new LeaveBalance { EmployeeId = employeeId, LeaveType = "Sick",      TotalAllocated = LeaveQuota.Sick,      UsedDays = 0, Year = year },
            new LeaveBalance { EmployeeId = employeeId, LeaveType = "Privilege", TotalAllocated = LeaveQuota.Privilege, UsedDays = 0, Year = year }
        );
    }
}
