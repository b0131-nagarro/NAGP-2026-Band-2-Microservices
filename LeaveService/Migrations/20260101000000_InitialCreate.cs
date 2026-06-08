using System;
using LeaveService.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaveService.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(LeaveDbContext))]
    [Migration("20260101000000_InitialCreate")]
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeaveRequests",
                columns: table => new
                {
                    Id              = table.Column<Guid>(nullable: false),
                    EmployeeId      = table.Column<Guid>(nullable: false),
                    EmployeeName    = table.Column<string>(nullable: false),
                    LeaveType       = table.Column<string>(maxLength: 20, nullable: false),
                    StartDate       = table.Column<DateTime>(nullable: false),
                    EndDate         = table.Column<DateTime>(nullable: false),
                    NumberOfDays    = table.Column<int>(nullable: false),
                    Reason          = table.Column<string>(nullable: false),
                    ManagerId       = table.Column<Guid>(nullable: false),
                    Status          = table.Column<string>(maxLength: 20, nullable: false),
                    RejectionReason = table.Column<string>(nullable: true),
                    ApprovedBy      = table.Column<Guid>(nullable: true),
                    ApprovedAt      = table.Column<DateTime>(nullable: true),
                    CreatedAt       = table.Column<DateTime>(nullable: false),
                    UpdatedAt       = table.Column<DateTime>(nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_LeaveRequests", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_EmployeeId",
                table: "LeaveRequests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_ManagerId",
                table: "LeaveRequests",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_Status",
                table: "LeaveRequests",
                column: "Status");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LeaveRequests");
        }
    }
}
