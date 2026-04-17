using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeUP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventVersioningSemantics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AggregateVersion",
                table: "events",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ChangeHistoryJson",
                table: "events",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyToken",
                table: "events",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE events
                SET "AggregateVersion" = 1
                WHERE "AggregateVersion" IS NULL OR "AggregateVersion" < 1;
                """);

            migrationBuilder.Sql("""
                UPDATE events
                SET "ConcurrencyToken" = COALESCE(NULLIF("PublicId", ''), 'event') || ':v' || "AggregateVersion"
                WHERE "ConcurrencyToken" = '' OR "ConcurrencyToken" IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE events
                SET "ChangeHistoryJson" = '[]'::jsonb
                WHERE "ChangeHistoryJson" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_events_PublicId_AggregateVersion",
                table: "events",
                columns: new[] { "PublicId", "AggregateVersion" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_events_PublicId_AggregateVersion",
                table: "events");

            migrationBuilder.DropColumn(
                name: "AggregateVersion",
                table: "events");

            migrationBuilder.DropColumn(
                name: "ChangeHistoryJson",
                table: "events");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "events");
        }
    }
}
