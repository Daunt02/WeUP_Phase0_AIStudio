using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeUP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationQueueItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "moderation_queue_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CandidateJson = table.Column<string>(type: "jsonb", nullable: true),
                    ProvenanceJson = table.Column<string>(type: "jsonb", nullable: false),
                    ConfidenceJson = table.Column<string>(type: "jsonb", nullable: false),
                    DedupeMatchJson = table.Column<string>(type: "jsonb", nullable: true),
                    IngestionJobJson = table.Column<string>(type: "jsonb", nullable: true),
                    ReviewReasonsJson = table.Column<string>(type: "jsonb", nullable: false),
                    HistoryJson = table.Column<string>(type: "jsonb", nullable: false),
                    AssignedReviewerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LinkedEventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderation_queue_items", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_moderation_queue_items_AssignedReviewerId",
                table: "moderation_queue_items",
                column: "AssignedReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_moderation_queue_items_CreatedAt",
                table: "moderation_queue_items",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_moderation_queue_items_ItemId",
                table: "moderation_queue_items",
                column: "ItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_moderation_queue_items_Kind",
                table: "moderation_queue_items",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_moderation_queue_items_Status",
                table: "moderation_queue_items",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "moderation_queue_items");
        }
    }
}
