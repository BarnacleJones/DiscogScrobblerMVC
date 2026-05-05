using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscogScrobblerMVC.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeparateUserAndImageTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create new tables before migrating data out of Releases
            migrationBuilder.CreateTable(
                name: "DiscogsReleaseImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiscogsReleaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    CoverUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CoverImage = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscogsReleaseImages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscogsReleaseToUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiscogsReleaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscogsReleaseToUsers", x => x.Id);
                });

            // Migrate existing data before removing columns
            // One image row per distinct DiscogsReleaseId (pick the first)
            migrationBuilder.Sql(@"
                INSERT INTO DiscogsReleaseImages (DiscogsReleaseId, CoverUrl, CoverImage)
                SELECT DiscogsReleaseId, CoverUrl, CoverImage
                FROM Releases
                WHERE Id IN (SELECT MIN(Id) FROM Releases GROUP BY DiscogsReleaseId);
            ");

            // One user-association row per (DiscogsReleaseId, UserId) pair
            migrationBuilder.Sql(@"
                INSERT INTO DiscogsReleaseToUsers (DiscogsReleaseId, UserId, DateAdded)
                SELECT DiscogsReleaseId, UserId, DateAdded
                FROM Releases;
            ");

            // Deduplicate Releases so the unique index can be applied
            migrationBuilder.Sql(@"
                DELETE FROM Releases
                WHERE Id NOT IN (SELECT MIN(Id) FROM Releases GROUP BY DiscogsReleaseId);
            ");

            migrationBuilder.DropColumn(
                name: "CoverImage",
                table: "Releases");

            migrationBuilder.DropColumn(
                name: "CoverUrl",
                table: "Releases");

            migrationBuilder.DropColumn(
                name: "DateAdded",
                table: "Releases");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Releases");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Releases_DiscogsReleaseId",
                table: "Releases",
                column: "DiscogsReleaseId");

            // Add FK constraints now that the alternate key exists
            migrationBuilder.AddForeignKey(
                name: "FK_DiscogsReleaseImages_Releases_DiscogsReleaseId",
                table: "DiscogsReleaseImages",
                column: "DiscogsReleaseId",
                principalTable: "Releases",
                principalColumn: "DiscogsReleaseId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DiscogsReleaseToUsers_Releases_DiscogsReleaseId",
                table: "DiscogsReleaseToUsers",
                column: "DiscogsReleaseId",
                principalTable: "Releases",
                principalColumn: "DiscogsReleaseId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_Releases_DiscogsReleaseId",
                table: "Releases",
                column: "DiscogsReleaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscogsReleaseImages_DiscogsReleaseId",
                table: "DiscogsReleaseImages",
                column: "DiscogsReleaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscogsReleaseToUsers_DiscogsReleaseId_UserId",
                table: "DiscogsReleaseToUsers",
                columns: new[] { "DiscogsReleaseId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscogsReleaseImages_Releases_DiscogsReleaseId",
                table: "DiscogsReleaseImages");

            migrationBuilder.DropForeignKey(
                name: "FK_DiscogsReleaseToUsers_Releases_DiscogsReleaseId",
                table: "DiscogsReleaseToUsers");

            migrationBuilder.DropTable(
                name: "DiscogsReleaseImages");

            migrationBuilder.DropTable(
                name: "DiscogsReleaseToUsers");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Releases_DiscogsReleaseId",
                table: "Releases");

            migrationBuilder.DropIndex(
                name: "IX_Releases_DiscogsReleaseId",
                table: "Releases");

            migrationBuilder.AddColumn<byte[]>(
                name: "CoverImage",
                table: "Releases",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverUrl",
                table: "Releases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateAdded",
                table: "Releases",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Releases",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
