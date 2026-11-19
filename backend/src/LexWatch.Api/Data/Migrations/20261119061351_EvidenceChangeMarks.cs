using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexWatch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EvidenceChangeMarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChangeMarksJson",
                table: "Evidence",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChangeMarksJson",
                table: "Evidence");
        }
    }
}
