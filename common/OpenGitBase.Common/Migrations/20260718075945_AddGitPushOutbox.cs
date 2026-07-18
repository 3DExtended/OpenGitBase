using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddGitPushOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GitPushOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ref = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AfterSha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitPushOutbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GitPushOutbox_RepositoryId_AfterSha",
                table: "GitPushOutbox",
                columns: new[] { "RepositoryId", "AfterSha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitPushOutbox_Status_CreatedAt",
                table: "GitPushOutbox",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GitPushOutbox");
        }
    }
}
