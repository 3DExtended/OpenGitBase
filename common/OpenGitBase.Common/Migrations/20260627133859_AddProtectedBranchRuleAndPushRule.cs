using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddProtectedBranchRuleAndPushRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tables are created by AddProtectedBranchRules (20260627133716).
            // This migration only adds the repository FK when missing.
            migrationBuilder.Sql(
                """
                DO $$ BEGIN
                  IF NOT EXISTS (
                    SELECT 1
                    FROM pg_constraint
                    WHERE conname = 'FK_ProtectedBranchRule_Repository_RepositoryId'
                  ) THEN
                    ALTER TABLE "ProtectedBranchRule"
                    ADD CONSTRAINT "FK_ProtectedBranchRule_Repository_RepositoryId"
                    FOREIGN KEY ("RepositoryId") REFERENCES "Repository" ("Id") ON DELETE CASCADE;
                  END IF;
                END $$;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProtectedBranchRule_Repository_RepositoryId",
                table: "ProtectedBranchRule");
        }
    }
}
