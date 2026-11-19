using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DemoMVC.Migrations.Identity
{
    /// <inheritdoc />
    public partial class StudentSubmissionApprovalTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Students"
                SET "IsApproved" = 0
                WHERE "UserId" IS NOT NULL;

                CREATE TRIGGER "TR_Students_UserSubmissionsPending"
                AFTER INSERT ON "Students"
                FOR EACH ROW
                WHEN NEW."UserId" IS NOT NULL AND NEW."IsApproved" = 1
                BEGIN
                    UPDATE "Students"
                    SET "IsApproved" = 0
                    WHERE "StudentCode" = NEW."StudentCode";
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER "TR_Students_UserSubmissionsPending";
                """);
        }
    }
}
