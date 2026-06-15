using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryStorageNodeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiTokenProtected",
                table: "StorageNode",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<Guid>(
                name: "StorageNodeId",
                table: "Repository",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiTokenProtected",
                table: "StorageNode");

            migrationBuilder.DropColumn(
                name: "StorageNodeId",
                table: "Repository");
        }
    }
}
