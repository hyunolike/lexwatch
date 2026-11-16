using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LexWatch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "ChatQuotas",
                columns: table => new
                {
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    IpHash = table.Column<string>(type: "text", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatQuotas", x => new { x.Day, x.IpHash });
                });

            migrationBuilder.CreateTable(
                name: "LawChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LawId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LawName = table.Column<string>(type: "text", nullable: false),
                    PromulgationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RawJson = table.Column<string>(type: "jsonb", nullable: false),
                    RawSha256 = table.Column<string>(type: "text", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LawChanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PracticeGroups",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    InterestText = table.Column<string>(type: "text", nullable: false),
                    LawIds = table.Column<string[]>(type: "text[]", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(2048)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeGroups", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LawChangeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TraceId = table.Column<string>(type: "text", nullable: false),
                    IsReplay = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowRuns_LawChanges_LawChangeId",
                        column: x => x.LawChangeId,
                        principalTable: "LawChanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Evidence",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: true),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evidence", x => new { x.RunId, x.EvidenceId });
                    table.ForeignKey(
                        name: "FK_Evidence_WorkflowRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "WorkflowRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupCode = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupReviews_WorkflowRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "WorkflowRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StepRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupReviewId = table.Column<Guid>(type: "uuid", nullable: true),
                    Step = table.Column<string>(type: "text", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NotBefore = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InputJson = table.Column<string>(type: "jsonb", nullable: true),
                    OutputJson = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ProviderKind = table.Column<string>(type: "text", nullable: false),
                    ProviderName = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StepRuns_WorkflowRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "WorkflowRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "text", nullable: false),
                    ActorRole = table.Column<string>(type: "text", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    DiffJson = table.Column<string>(type: "jsonb", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Approvals_GroupReviews_GroupReviewId",
                        column: x => x.GroupReviewId,
                        principalTable: "GroupReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemoDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SentencesJson = table.Column<string>(type: "jsonb", nullable: false),
                    OpenQuestionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemoDrafts_GroupReviews_GroupReviewId",
                        column: x => x.GroupReviewId,
                        principalTable: "GroupReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_GroupReviews_GroupReviewId",
                        column: x => x.GroupReviewId,
                        principalTable: "GroupReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentToolCalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Seq = table.Column<int>(type: "integer", nullable: false),
                    Tool = table.Column<string>(type: "text", nullable: false),
                    ArgsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResultSummary = table.Column<string>(type: "text", nullable: false),
                    EvidenceIds = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentToolCalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentToolCalls_StepRuns_StepRunId",
                        column: x => x.StepRunId,
                        principalTable: "StepRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CriticFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemoDraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    SentenceIndex = table.Column<int>(type: "integer", nullable: false),
                    RuleId = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriticFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CriticFindings_MemoDrafts_MemoDraftId",
                        column: x => x.MemoDraftId,
                        principalTable: "MemoDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "PracticeGroups",
                columns: new[] { "Code", "Embedding", "InterestText", "LawIds", "Name" },
                values: new object[,]
                {
                    { "finance", null, "금융소비자 보호·자본시장·전자금융 관련 법령 개정에 관심이 있다.", new string[0], "금융" },
                    { "labor", null, "근로기준·임금·근로시간·산업안전 관련 법령 개정에 관심이 있다.", new string[0], "노동" },
                    { "privacy", null, "개인정보 처리·정보통신망·위치정보 관련 법령 개정에 관심이 있다.", new string[0], "개인정보" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentToolCalls_StepRunId_Seq",
                table: "AgentToolCalls",
                columns: new[] { "StepRunId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_GroupReviewId",
                table: "Approvals",
                column: "GroupReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_CriticFindings_MemoDraftId",
                table: "CriticFindings",
                column: "MemoDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupReviews_RunId_GroupCode",
                table: "GroupReviews",
                columns: new[] { "RunId", "GroupCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LawChanges_IdempotencyKey",
                table: "LawChanges",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemoDrafts_GroupReviewId_Version",
                table: "MemoDrafts",
                columns: new[] { "GroupReviewId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_GroupReviewId",
                table: "Notifications",
                column: "GroupReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_StepRuns_RunId",
                table: "StepRuns",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_StepRuns_Status_NotBefore",
                table: "StepRuns",
                columns: new[] { "Status", "NotBefore" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_LawChangeId",
                table: "WorkflowRuns",
                column: "LawChangeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentToolCalls");

            migrationBuilder.DropTable(
                name: "Approvals");

            migrationBuilder.DropTable(
                name: "ChatQuotas");

            migrationBuilder.DropTable(
                name: "CriticFindings");

            migrationBuilder.DropTable(
                name: "Evidence");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PracticeGroups");

            migrationBuilder.DropTable(
                name: "StepRuns");

            migrationBuilder.DropTable(
                name: "MemoDrafts");

            migrationBuilder.DropTable(
                name: "GroupReviews");

            migrationBuilder.DropTable(
                name: "WorkflowRuns");

            migrationBuilder.DropTable(
                name: "LawChanges");
        }
    }
}
