using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddLfsObjectCopies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LfsObjectCopy",
                columns: table => new
                {
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Oid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StorageNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    StoredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LfsObjectCopy", x => new { x.RepositoryId, x.Oid, x.StorageNodeId });
                    table.ForeignKey(
                        name: "FK_LfsObjectCopy_LfsObject_RepositoryId_Oid",
                        columns: x => new { x.RepositoryId, x.Oid },
                        principalTable: "LfsObject",
                        principalColumns: new[] { "RepositoryId", "Oid" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LfsObjectCopy_StorageNodeId",
                table: "LfsObjectCopy",
                column: "StorageNodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LfsObjectCopy");
        }
    }
}
