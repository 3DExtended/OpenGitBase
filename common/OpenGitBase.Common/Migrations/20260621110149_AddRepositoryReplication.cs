using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryReplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryStorageNodeId",
                table: "Repository",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PrimaryWatermark",
                table: "Repository",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "ReplicationEpoch",
                table: "Repository",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "ReplicationState",
                table: "Repository",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "RepositoryReplica",
                columns: table => new
                {
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    AppliedWatermark = table.Column<long>(type: "bigint", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryReplica", x => new { x.RepositoryId, x.StorageNodeId });
                    table.ForeignKey(
                        name: "FK_RepositoryReplica_Repository_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repository",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryReplica");

            migrationBuilder.DropColumn(
                name: "PrimaryStorageNodeId",
                table: "Repository");

            migrationBuilder.DropColumn(
                name: "PrimaryWatermark",
                table: "Repository");

            migrationBuilder.DropColumn(
                name: "ReplicationEpoch",
                table: "Repository");

            migrationBuilder.DropColumn(
                name: "ReplicationState",
                table: "Repository");
        }
    }
}
