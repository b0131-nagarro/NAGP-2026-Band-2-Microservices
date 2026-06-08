using AuthService.Data;
using AuthService.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

/// <summary>
/// Seeds predefined users after migrations run.
/// Using a runtime seeder (vs migration InsertData) allows us to call BCrypt
/// at application startup, avoiding the issue of BCrypt in migration files.
/// Idempotent – checks before inserting.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(AuthDbContext db)
    {
        // If any user already exists, seeding has already run – skip
        if (await db.Users.AnyAsync()) return;

        var mgr1  = new Guid("11111111-1111-1111-1111-111111111111");
        var mgr2  = new Guid("22222222-2222-2222-2222-222222222222");
        var emp1u = new Guid("33333333-3333-3333-3333-333333333333");
        var emp2u = new Guid("44444444-4444-4444-4444-444444444444");
        var emp3u = new Guid("55555555-5555-5555-5555-555555555555");
        var emp1  = new Guid("aaaaaaa1-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var emp2  = new Guid("aaaaaaa2-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var emp3  = new Guid("aaaaaaa3-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var seed  = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // BCrypt hashes are computed at startup – safe and correct
        var mgrHash = BCrypt.Net.BCrypt.HashPassword("Manager@123");
        var empHash = BCrypt.Net.BCrypt.HashPassword("Employee@123");

        db.Users.AddRange(
            new User { Id = mgr1,  Username = "manager1",  PasswordHash = mgrHash, Email = "manager1@company.com",  FullName = "Alice Johnson",  Role = "Manager",  EmployeeId = null, CreatedAt = seed },
            new User { Id = mgr2,  Username = "manager2",  PasswordHash = mgrHash, Email = "manager2@company.com",  FullName = "Bob Williams",   Role = "Manager",  EmployeeId = null, CreatedAt = seed },
            new User { Id = emp1u, Username = "employee1", PasswordHash = empHash, Email = "employee1@company.com", FullName = "Charlie Brown",  Role = "Employee", EmployeeId = emp1, CreatedAt = seed },
            new User { Id = emp2u, Username = "employee2", PasswordHash = empHash, Email = "employee2@company.com", FullName = "Diana Prince",   Role = "Employee", EmployeeId = emp2, CreatedAt = seed },
            new User { Id = emp3u, Username = "employee3", PasswordHash = empHash, Email = "employee3@company.com", FullName = "Eve Adams",      Role = "Employee", EmployeeId = emp3, CreatedAt = seed }
        );

        await db.SaveChangesAsync();
    }
}
