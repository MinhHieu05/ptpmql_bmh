using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DemoMVC.Migrations
{
    /// <inheritdoc />
    public partial class Create_Table_Student : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    StudentCode = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Age = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: true) //trong su dung nullable de cho phep truong Address co the null do de dau ?
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.StudentCode);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) //phuong thuc Down de xoa bang Student khi rollback
        {
            migrationBuilder.DropTable(
                name: "Students");
        }
    }
}
