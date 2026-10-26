using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AllowAnonymousChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "AuthenticationChallenges",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Open passkey sign-ins belong to nobody; they cannot survive a required user.
            migrationBuilder.Sql("DELETE FROM \"AuthenticationChallenges\" WHERE \"UserId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "AuthenticationChallenges",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
