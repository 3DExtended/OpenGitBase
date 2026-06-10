using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    NormalizedUsername = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "UserCredentials",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    PasswordHash = table.Column<string>(
                        type: "character varying(512)",
                        maxLength: 512,
                        nullable: true
                    ),
                    SignInProvider = table.Column<bool>(type: "boolean", nullable: false),
                    InternalId = table.Column<string>(
                        type: "character varying(256)",
                        maxLength: 256,
                        nullable: true
                    ),
                    EmailCiphertext = table.Column<string>(
                        type: "character varying(1024)",
                        maxLength: 1024,
                        nullable: true
                    ),
                    EmailLookupHash = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: true
                    ),
                    PasswordResetTokenHash = table.Column<string>(
                        type: "character varying(512)",
                        maxLength: 512,
                        nullable: true
                    ),
                    PasswordResetTokenExpireDate = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCredentials", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserCredentials_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_UserCredentials_EmailLookupHash",
                table: "UserCredentials",
                column: "EmailLookupHash",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedUsername",
                table: "Users",
                column: "NormalizedUsername",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UserCredentials");

            migrationBuilder.DropTable(name: "Users");
        }
    }
}
