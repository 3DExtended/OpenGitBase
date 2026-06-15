using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageNodeEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StorageNode",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    InternalHost = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    InternalSshPort = table.Column<int>(type: "integer", nullable: false),
                    InternalHttpPort = table.Column<int>(type: "integer", nullable: false),
                    ApiTokenHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FreeBytesAvailable = table.Column<long>(type: "bigint", nullable: false),
                    TotalBytesAvailable = table.Column<long>(type: "bigint", nullable: false),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsHealthy = table.Column<bool>(type: "boolean", nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageNode", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMember_UserId",
                table: "OrganizationMember",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Organization_OwnerUserId",
                table: "Organization",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageNode_NodeId",
                table: "StorageNode",
                column: "NodeId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Organization_Users_OwnerUserId",
                table: "Organization",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationMember_Users_UserId",
                table: "OrganizationMember",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Organization_Users_OwnerUserId",
                table: "Organization");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationMember_Users_UserId",
                table: "OrganizationMember");

            migrationBuilder.DropTable(
                name: "StorageNode");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationMember_UserId",
                table: "OrganizationMember");

            migrationBuilder.DropIndex(
                name: "IX_Organization_OwnerUserId",
                table: "Organization");
        }
    }
}
