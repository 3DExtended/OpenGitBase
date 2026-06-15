using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailVerificationToUserCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailVerificationTokenExpireDate",
                table: "UserCredentials",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "EmailVerificationTokenHash",
                table: "UserCredentials",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                table: "UserCredentials",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<long>(
                name: "StorageBytesUsed",
                table: "Repository",
                type: "bigint",
                nullable: false,
                defaultValue: 0L
            );

            migrationBuilder.CreateTable(
                name: "Organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(
                        type: "character varying(256)",
                        maxLength: 256,
                        nullable: false
                    ),
                    Slug = table.Column<string>(
                        type: "character varying(256)",
                        maxLength: 256,
                        nullable: false
                    ),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organization", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "OrganizationMember",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationMember", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationMember_Organization_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organization",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Organization_Slug",
                table: "Organization",
                column: "Slug",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMember_OrganizationId_UserId",
                table: "OrganizationMember",
                columns: new[] { "OrganizationId", "UserId" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "OrganizationMember");

            migrationBuilder.DropTable(name: "Organization");

            migrationBuilder.DropColumn(
                name: "EmailVerificationTokenExpireDate",
                table: "UserCredentials"
            );

            migrationBuilder.DropColumn(
                name: "EmailVerificationTokenHash",
                table: "UserCredentials"
            );

            migrationBuilder.DropColumn(name: "EmailVerified", table: "UserCredentials");

            migrationBuilder.DropColumn(name: "StorageBytesUsed", table: "Repository");
        }
    }
}
