using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DemoMVC.Migrations.Identity
{
    /// <inheritdoc />
    public partial class StudentApprovalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Students"
                ADD COLUMN "IsApproved" INTEGER NOT NULL DEFAULT 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Students" DROP COLUMN "IsApproved";
                """);
        }
    }
}
