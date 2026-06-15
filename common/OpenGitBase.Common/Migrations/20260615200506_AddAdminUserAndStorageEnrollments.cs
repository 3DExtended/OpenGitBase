using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenGitBase.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminUserAndStorageEnrollments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "FleetSecrets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DispatcherSshPublicKey = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    DispatcherSshPrivateKeyProtected = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    FleetBootstrapTokenHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FleetSecrets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorageNodeEnrollment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EnrollmentTokenHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageNodeEnrollment", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StorageNodeEnrollment_NodeId",
                table: "StorageNodeEnrollment",
                column: "NodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FleetSecrets");

            migrationBuilder.DropTable(
                name: "StorageNodeEnrollment");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Users");
        }
    }
}
