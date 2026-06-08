using System;
using EmployeeService.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmployeeService.Migrations
{
    /// <summary>
    /// Creates Employees and LeaveBalances tables.
    /// Seed data is applied at runtime by DataSeeder.SeedAsync().
    /// </summary>
    [DbContext(typeof(EmployeeDbContext))]
    [Migration("20260101000000_InitialCreate")]
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id          = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName    = table.Column<string>(type: "text", nullable: false),
                    Email       = table.Column<string>(type: "text", nullable: false),
                    Department  = table.Column<string>(type: "text", nullable: false),
                    Designation = table.Column<string>(type: "text", nullable: false),
                    ManagerId   = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinDate    = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive    = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt   = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_Employees", x => x.Id));

            migrationBuilder.CreateTable(
                name: "LeaveBalances",
                columns: table => new
                {
                    Id             = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId     = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaveType      = table.Column<string>(type: "text", nullable: false),
                    TotalAllocated = table.Column<int>(type: "integer", nullable: false),
                    UsedDays       = table.Column<int>(type: "integer", nullable: false),
                    Year           = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveBalances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_Employees_Email", "Employees", "Email", unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalances_EmployeeId_LeaveType_Year",
                table: "LeaveBalances",
                columns: new[] { "EmployeeId", "LeaveType", "Year" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LeaveBalances");
            migrationBuilder.DropTable(name: "Employees");
        }
    }
}
