using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeUP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationAuditHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DistrictCode",
                table: "events",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarketCode",
                table: "events",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NeighborhoodCode",
                table: "events",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "event_merge_provenance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CanonicalEventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ResolutionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MergeActor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MergeReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    EntryJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_merge_provenance", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "moderation_history_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    QueueItemId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CandidateId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ReviewerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NewStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActionTimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReasonComment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderation_history_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_events_DistrictCode",
                table: "events",
                column: "DistrictCode");

            migrationBuilder.CreateIndex(
                name: "IX_events_MarketCode",
                table: "events",
                column: "MarketCode");

            migrationBuilder.CreateIndex(
                name: "IX_events_MarketCode_DistrictCode_NeighborhoodCode",
                table: "events",
                columns: new[] { "MarketCode", "DistrictCode", "NeighborhoodCode" });

            migrationBuilder.CreateIndex(
                name: "IX_event_merge_provenance_CanonicalEventId_SequenceNumber",
                table: "event_merge_provenance",
                columns: new[] { "CanonicalEventId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_merge_provenance_EntryId",
                table: "event_merge_provenance",
                column: "EntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_merge_provenance_RecordedAtUtc",
                table: "event_merge_provenance",
                column: "RecordedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_moderation_history_entries_CandidateId_ActionTimestampUtc",
                table: "moderation_history_entries",
                columns: new[] { "CandidateId", "ActionTimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_moderation_history_entries_EntryId",
                table: "moderation_history_entries",
                column: "EntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_moderation_history_entries_EventId_ActionTimestampUtc",
                table: "moderation_history_entries",
                columns: new[] { "EventId", "ActionTimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_moderation_history_entries_QueueItemId_ActionTimestampUtc",
                table: "moderation_history_entries",
                columns: new[] { "QueueItemId", "ActionTimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_moderation_history_entries_ReviewerId_ActionTimestampUtc",
                table: "moderation_history_entries",
                columns: new[] { "ReviewerId", "ActionTimestampUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_merge_provenance");

            migrationBuilder.DropTable(
                name: "moderation_history_entries");

            migrationBuilder.DropIndex(
                name: "IX_events_DistrictCode",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_MarketCode",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_MarketCode_DistrictCode_NeighborhoodCode",
                table: "events");

            migrationBuilder.DropColumn(
                name: "DistrictCode",
                table: "events");

            migrationBuilder.DropColumn(
                name: "MarketCode",
                table: "events");

            migrationBuilder.DropColumn(
                name: "NeighborhoodCode",
                table: "events");
        }
    }
}
