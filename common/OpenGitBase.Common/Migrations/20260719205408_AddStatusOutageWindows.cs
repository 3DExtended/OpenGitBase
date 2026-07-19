using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusOutageWindows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StatusOutageWindow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    ComponentGroup = table.Column<int>(type: "integer", nullable: false),
                    InstanceId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UnhealthySince = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BecamePublicAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastNonUnhealthyAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsPartial = table.Column<bool>(type: "boolean", nullable: false),
                    Suppressed = table.Column<bool>(type: "boolean", nullable: false),
                    Annotation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusOutageWindow", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StatusOutageWindow_ActiveKey",
                table: "StatusOutageWindow",
                columns: new[] { "Scope", "ComponentGroup", "InstanceId", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StatusOutageWindow_PublicList",
                table: "StatusOutageWindow",
                columns: new[] { "BecamePublicAt", "Suppressed", "EndedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StatusOutageWindow_UnhealthySince",
                table: "StatusOutageWindow",
                column: "UnhealthySince");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StatusOutageWindow");
        }
    }
}
