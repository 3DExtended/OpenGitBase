using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryGitBytesUsed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "GitBytesUsed",
                table: "Repository",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // StorageBytesUsed = GitBytesUsed + everything else counted (Git LFS objects). Git
            // pushes never wrote StorageBytesUsed, so without Git LFS whatever it holds is stale
            // git accounting: marking it as the git part lets the first measurement replace it.
            // Once LfsObject exists (Git LFS migrated first), the counter holds LFS bytes and
            // the git part starts at 0.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF to_regclass('"LfsObject"') IS NULL THEN
                        UPDATE "Repository" SET "GitBytesUsed" = "StorageBytesUsed";
                    END IF;
                END $$;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GitBytesUsed",
                table: "Repository");
        }
    }
}
