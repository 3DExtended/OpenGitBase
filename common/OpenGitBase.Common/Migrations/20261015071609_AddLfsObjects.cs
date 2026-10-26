using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddLfsObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LfsObject",
                columns: table => new
                {
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Oid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    KeyVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LfsObject", x => new { x.RepositoryId, x.Oid });
                    table.ForeignKey(
                        name: "FK_LfsObject_Repository_RepositoryId",
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
                name: "LfsObject");
        }
    }
}
