using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexWatch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApprovalAcknowledgedFindingCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AcknowledgedFindingCount",
                table: "Approvals",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcknowledgedFindingCount",
                table: "Approvals");
        }
    }
}
