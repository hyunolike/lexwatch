using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace LexWatch.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ArticleChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArticleChunks",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkId = table.Column<string>(type: "text", nullable: false),
                    EvidenceId = table.Column<string>(type: "text", nullable: false),
                    Ord = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Tokens = table.Column<string[]>(type: "text[]", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1024)", nullable: true),
                    EmbedModel = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleChunks", x => new { x.RunId, x.ChunkId });
                    table.ForeignKey(
                        name: "FK_ArticleChunks_Evidence_RunId_EvidenceId",
                        columns: x => new { x.RunId, x.EvidenceId },
                        principalTable: "Evidence",
                        principalColumns: new[] { "RunId", "EvidenceId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleChunks_RunId_EvidenceId",
                table: "ArticleChunks",
                columns: new[] { "RunId", "EvidenceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleChunks");
        }
    }
}
