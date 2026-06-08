using System;
using AuthService.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthService.Migrations
{
    /// <summary>
    /// Creates the Users table. Seed data is handled by DataSeeder.SeedAsync()
    /// at runtime to allow BCrypt password hashing (not possible inside migrations).
    /// </summary>
    [DbContext(typeof(AuthDbContext))]
    [Migration("20260101000000_InitialCreate")]
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id           = table.Column<Guid>(type: "uuid", nullable: false),
                    Username     = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Email        = table.Column<string>(type: "text", nullable: false),
                    FullName     = table.Column<string>(type: "text", nullable: false),
                    Role         = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EmployeeId   = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt    = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive     = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_Users", x => x.Id));

            migrationBuilder.CreateIndex("IX_Users_Username", "Users", "Username", unique: true);
            migrationBuilder.CreateIndex("IX_Users_Email",    "Users", "Email",    unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Users");
        }
    }
}
