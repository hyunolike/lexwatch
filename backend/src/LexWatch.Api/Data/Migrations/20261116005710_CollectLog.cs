using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexWatch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CollectLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_GroupReviews_GroupReviewId",
                table: "Notifications");

            migrationBuilder.AlterColumn<Guid>(
                name: "GroupReviewId",
                table: "Notifications",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "CollectLogs",
                columns: table => new
                {
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    ItemCount = table.Column<int>(type: "integer", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectLogs", x => x.Day);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_GroupReviews_GroupReviewId",
                table: "Notifications",
                column: "GroupReviewId",
                principalTable: "GroupReviews",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_GroupReviews_GroupReviewId",
                table: "Notifications");

            migrationBuilder.DropTable(
                name: "CollectLogs");

            migrationBuilder.AlterColumn<Guid>(
                name: "GroupReviewId",
                table: "Notifications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_GroupReviews_GroupReviewId",
                table: "Notifications",
                column: "GroupReviewId",
                principalTable: "GroupReviews",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
