using EmployeeService.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeService.Data;

public class EmployeeDbContext(DbContextOptions<EmployeeDbContext> options) : DbContext(options)
{
    public DbSet<Employee>     Employees     => Set<Employee>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Employee>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasMany(x => x.LeaveBalances)
             .WithOne(b => b.Employee)
             .HasForeignKey(b => b.EmployeeId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeaveBalance>(b =>
        {
            b.HasKey(x => x.Id);
            // Unique per employee + type + year prevents duplicate allocations
            b.HasIndex(x => new { x.EmployeeId, x.LeaveType, x.Year }).IsUnique();
            // RemainingDays is a computed property – do not map to a column
            b.Ignore(x => x.RemainingDays);
        });
        // Seed data is handled by DataSeeder.SeedAsync() at startup.
    }
}
