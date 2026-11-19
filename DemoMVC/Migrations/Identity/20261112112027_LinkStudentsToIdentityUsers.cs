using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DemoMVC.Migrations.Identity
{
    /// <inheritdoc />
    public partial class LinkStudentsToIdentityUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Students"
                ADD COLUMN "UserId" TEXT NULL
                    REFERENCES "AspNetUsers" ("Id") ON DELETE SET NULL;

                CREATE INDEX "IX_Students_UserId" ON "Students" ("UserId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX "IX_Students_UserId";
                ALTER TABLE "Students" DROP COLUMN "UserId";
                """);
        }
    }
}
