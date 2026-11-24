using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexWatch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BillRadar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "WatchLawNames",
                table: "PracticeGroups",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.CreateTable(
                name: "BillCollectLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Ok = table.Column<bool>(type: "boolean", nullable: false),
                    NewBills = table.Column<int>(type: "integer", nullable: false),
                    StageChanges = table.Column<int>(type: "integer", nullable: false),
                    Refreshed = table.Column<int>(type: "integer", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    FailuresJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillCollectLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BillNo = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BillName = table.Column<string>(type: "text", nullable: false),
                    LawName = table.Column<string>(type: "text", nullable: false),
                    Stage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProposedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Proposer = table.Column<string>(type: "text", nullable: false),
                    LeadProposer = table.Column<string>(type: "text", nullable: true),
                    Committee = table.Column<string>(type: "text", nullable: true),
                    ProcResult = table.Column<string>(type: "text", nullable: true),
                    ProcOn = table.Column<DateOnly>(type: "date", nullable: true),
                    DetailUrl = table.Column<string>(type: "text", nullable: false),
                    RawJson = table.Column<string>(type: "jsonb", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastEventAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BillEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BillId = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FromStage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ToStage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillEvents_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "PracticeGroups",
                keyColumn: "Code",
                keyValue: "finance",
                column: "WatchLawNames",
                value: new[] { "금융소비자 보호에 관한 법률", "자본시장과 금융투자업에 관한 법률", "전자금융거래법", "은행법", "보험업법" });

            migrationBuilder.UpdateData(
                table: "PracticeGroups",
                keyColumn: "Code",
                keyValue: "labor",
                column: "WatchLawNames",
                value: new[] { "근로기준법", "최저임금법", "산업안전보건법", "중대재해 처벌 등에 관한 법률", "근로자퇴직급여 보장법", "노동조합 및 노동관계조정법" });

            migrationBuilder.UpdateData(
                table: "PracticeGroups",
                keyColumn: "Code",
                keyValue: "privacy",
                column: "WatchLawNames",
                value: new[] { "개인정보 보호법", "정보통신망 이용촉진 및 정보보호 등에 관한 법률", "위치정보의 보호 및 이용 등에 관한 법률", "신용정보의 이용 및 보호에 관한 법률" });

            migrationBuilder.CreateIndex(
                name: "IX_BillCollectLogs_StartedAt",
                table: "BillCollectLogs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BillEvents_BillId_At",
                table: "BillEvents",
                columns: new[] { "BillId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_LastEventAt",
                table: "Bills",
                column: "LastEventAt");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_LawName",
                table: "Bills",
                column: "LawName");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_Source_ExternalId",
                table: "Bills",
                columns: new[] { "Source", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillCollectLogs");

            migrationBuilder.DropTable(
                name: "BillEvents");

            migrationBuilder.DropTable(
                name: "Bills");

            migrationBuilder.DropColumn(
                name: "WatchLawNames",
                table: "PracticeGroups");
        }
    }
}
