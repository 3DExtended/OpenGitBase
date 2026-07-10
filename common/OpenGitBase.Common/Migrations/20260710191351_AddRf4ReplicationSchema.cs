using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddRf4ReplicationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ArtifactWatermark",
                table: "RepositoryReplica",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReadReplicaStorageNodeId",
                table: "Repository",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RepositoryKey",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyCiphertext = table.Column<string>(type: "text", nullable: false),
                    KeyVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryKey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryKey_Repository_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repository",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryKey_RepositoryId",
                table: "RepositoryKey",
                column: "RepositoryId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryKey");

            migrationBuilder.DropColumn(
                name: "ArtifactWatermark",
                table: "RepositoryReplica");

            migrationBuilder.DropColumn(
                name: "ReadReplicaStorageNodeId",
                table: "Repository");
        }
    }
}
