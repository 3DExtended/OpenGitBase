using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddMergeRequestsDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_MergeRequest",
                table: "MergeRequest");

            migrationBuilder.RenameTable(
                name: "MergeRequest",
                newName: "merge_requests");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "merge_requests",
                newName: "TargetRef");

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "merge_requests",
                type: "character varying(16000)",
                maxLength: 16000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "merge_requests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatorUserId",
                table: "merge_requests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                table: "merge_requests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "merge_requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RepositoryId",
                table: "merge_requests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "SourceHeadSha",
                table: "merge_requests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<string>(
                name: "SourceRef",
                table: "merge_requests",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "merge_requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TargetBaseSha",
                table: "merge_requests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "merge_requests",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "merge_requests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddPrimaryKey(
                name: "PK_merge_requests",
                table: "merge_requests",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_merge_requests_RepositoryId_Number",
                table: "merge_requests",
                columns: new[] { "RepositoryId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_merge_requests_RepositoryId_SourceRef_TargetRef",
                table: "merge_requests",
                columns: new[] { "RepositoryId", "SourceRef", "TargetRef" });

            migrationBuilder.CreateIndex(
                name: "IX_merge_requests_RepositoryId_UpdatedAt",
                table: "merge_requests",
                columns: new[] { "RepositoryId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_merge_requests",
                table: "merge_requests");

            migrationBuilder.DropIndex(
                name: "IX_merge_requests_RepositoryId_Number",
                table: "merge_requests");

            migrationBuilder.DropIndex(
                name: "IX_merge_requests_RepositoryId_SourceRef_TargetRef",
                table: "merge_requests");

            migrationBuilder.DropIndex(
                name: "IX_merge_requests_RepositoryId_UpdatedAt",
                table: "merge_requests");

            migrationBuilder.DropColumn(
                name: "Body",
                table: "merge_requests");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "merge_requests");

            migrationBuilder.DropColumn(
                name: "CreatorUserId",
                table: "merge_requests");

            migrationBuilder.DropColumn(
                name: "IsDraft",
                table: "merge_requests");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "merge_requests");

            migrationBuilder.DropColumn(
                name: "RepositoryId",
                table: "merge_requests");

            migrationBuilder.DropColumn(
                name: "SourceHeadSha",
                table: "merge_requests");

            migrationBuilder.DropColumn(
                name: "SourceRef",
                table: "merge_requests");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "merge_requests");

            migrationBuilder.DropColumn(
                name: "TargetBaseSha",
                table: "merge_requests");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "merge_requests");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "merge_requests");

            migrationBuilder.RenameTable(
                name: "merge_requests",
                newName: "MergeRequest");

            migrationBuilder.RenameColumn(
                name: "TargetRef",
                table: "MergeRequest",
                newName: "Name");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MergeRequest",
                table: "MergeRequest",
                column: "Id");
        }
    }
}
