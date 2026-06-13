using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddFingerprintToSshKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicGitSshKey",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PublicSSHKey = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicGitSshKey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicGitSshKey_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicGitSshKey_Fingerprint",
                table: "PublicGitSshKey",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicGitSshKey_Fingerprint_PublicSSHKey",
                table: "PublicGitSshKey",
                columns: new[] { "Fingerprint", "PublicSSHKey" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicGitSshKey_OwnerUserId_Fingerprint",
                table: "PublicGitSshKey",
                columns: new[] { "OwnerUserId", "Fingerprint" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicGitSshKey_PublicSSHKey",
                table: "PublicGitSshKey",
                column: "PublicSSHKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicGitSshKey");
        }
    }
}
