using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSessionsValidAfter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SessionsValidAfter",
                table: "UserCredentials",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SessionsValidAfter",
                table: "UserCredentials");
        }
    }
}
