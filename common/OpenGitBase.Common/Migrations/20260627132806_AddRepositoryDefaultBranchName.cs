using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryDefaultBranchName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultBranchName",
                table: "Repository",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultBranchName",
                table: "Repository");
        }
    }
}
