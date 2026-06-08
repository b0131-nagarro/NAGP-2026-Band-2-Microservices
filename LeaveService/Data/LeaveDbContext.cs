using LeaveService.Models;
using Microsoft.EntityFrameworkCore;

namespace LeaveService.Data;

public class LeaveDbContext(DbContextOptions<LeaveDbContext> options) : DbContext(options)
{
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LeaveRequest>(e =>
        {
            e.HasKey(x => x.Id);
            // Efficient queries by employee and by manager
            e.HasIndex(x => x.EmployeeId);
            e.HasIndex(x => x.ManagerId);
            e.HasIndex(x => x.Status);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.LeaveType).HasMaxLength(20);
        });
    }
}
