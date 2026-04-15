using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeUP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRolesAndUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_preferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreferredCategoriesJson = table.Column<string>(type: "jsonb", nullable: false),
                    HomeRadiusMeters = table.Column<double>(type: "double precision", nullable: false),
                    NotifyOnNewEvents = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyOnSaveReminders = table.Column<bool>(type: "boolean", nullable: false),
                    PreferredTimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastKnownMapCenterLat = table.Column<double>(type: "double precision", nullable: true),
                    LastKnownMapCenterLng = table.Column<double>(type: "double precision", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_preferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_user_preferences_user_profiles_UserId",
                        column: x => x.UserId,
                        principalTable: "user_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.UserId, x.Role });
                    table.ForeignKey(
                        name: "FK_user_roles_user_profiles_UserId",
                        column: x => x.UserId,
                        principalTable: "user_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_Role",
                table: "user_roles",
                column: "Role");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_preferences");

            migrationBuilder.DropTable(
                name: "user_roles");
        }
    }
}
