using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgStorageNodeCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HostingScope",
                table: "StorageNodeEnrollment",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "MaxBytes",
                table: "StorageNodeEnrollment",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "StorageNodeEnrollment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HostingScope",
                table: "StorageNode",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "MaxBytes",
                table: "StorageNode",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerOrganizationId",
                table: "StorageNode",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UsedBytes",
                table: "StorageNode",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "PlacementPolicy",
                table: "Repository",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrganizationStorageSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefaultPlacementPolicy = table.Column<int>(type: "integer", nullable: false),
                    DefaultSelfHostPreference = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationStorageSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationStorageSettings_OrganizationId",
                table: "OrganizationStorageSettings",
                column: "OrganizationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationStorageSettings");

            migrationBuilder.DropColumn(
                name: "HostingScope",
                table: "StorageNodeEnrollment");

            migrationBuilder.DropColumn(
                name: "MaxBytes",
                table: "StorageNodeEnrollment");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "StorageNodeEnrollment");

            migrationBuilder.DropColumn(
                name: "HostingScope",
                table: "StorageNode");

            migrationBuilder.DropColumn(
                name: "MaxBytes",
                table: "StorageNode");

            migrationBuilder.DropColumn(
                name: "OwnerOrganizationId",
                table: "StorageNode");

            migrationBuilder.DropColumn(
                name: "UsedBytes",
                table: "StorageNode");

            migrationBuilder.DropColumn(
                name: "PlacementPolicy",
                table: "Repository");
        }
    }
}
