using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DemoMVC.Migrations.Identity
{
    /// <inheritdoc />
    public partial class DynamicRoleActionPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoleActionPermissions",
                columns: table => new
                {
                    RoleId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Area = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Controller = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IsAllowed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleActionPermissions", x => new { x.RoleId, x.Area, x.Controller, x.Action });
                    table.ForeignKey(
                        name: "FK_RoleActionPermissions_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleActionPermissions");
        }
    }
}
