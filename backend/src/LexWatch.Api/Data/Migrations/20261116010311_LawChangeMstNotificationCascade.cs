using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexWatch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LawChangeMstNotificationCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_GroupReviews_GroupReviewId",
                table: "Notifications");

            migrationBuilder.AddColumn<string>(
                name: "Mst",
                table: "LawChanges",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_GroupReviews_GroupReviewId",
                table: "Notifications",
                column: "GroupReviewId",
                principalTable: "GroupReviews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_GroupReviews_GroupReviewId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Mst",
                table: "LawChanges");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_GroupReviews_GroupReviewId",
                table: "Notifications",
                column: "GroupReviewId",
                principalTable: "GroupReviews",
                principalColumn: "Id");
        }
    }
}
