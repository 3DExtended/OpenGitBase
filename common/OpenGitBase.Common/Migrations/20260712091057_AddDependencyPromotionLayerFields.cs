using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddDependencyPromotionLayerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "DependencyPromotionRequest",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "DependencyPromotionRequest",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LayerStoreObjectKey",
                table: "DependencyPromotionRequest",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "DependencyPromotionRequest");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "DependencyPromotionRequest");

            migrationBuilder.DropColumn(
                name: "LayerStoreObjectKey",
                table: "DependencyPromotionRequest");
        }
    }
}
