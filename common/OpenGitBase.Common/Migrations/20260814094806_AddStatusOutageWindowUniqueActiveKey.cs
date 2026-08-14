using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusOutageWindowUniqueActiveKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StatusOutageWindow_ActiveKey_Group_Unique",
                table: "StatusOutageWindow",
                columns: new[] { "Scope", "ComponentGroup" },
                unique: true,
                filter: "\"EndedAt\" IS NULL AND \"InstanceId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StatusOutageWindow_ActiveKey_Instance_Unique",
                table: "StatusOutageWindow",
                columns: new[] { "Scope", "ComponentGroup", "InstanceId" },
                unique: true,
                filter: "\"EndedAt\" IS NULL AND \"InstanceId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StatusOutageWindow_ActiveKey_Group_Unique",
                table: "StatusOutageWindow");

            migrationBuilder.DropIndex(
                name: "IX_StatusOutageWindow_ActiveKey_Instance_Unique",
                table: "StatusOutageWindow");
        }
    }
}
