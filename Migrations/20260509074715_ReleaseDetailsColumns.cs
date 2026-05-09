using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscogScrobblerMVC.Migrations
{
    /// <inheritdoc />
    public partial class ReleaseDetailsColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommunityHaveCount",
                table: "Releases",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CommunityWantCount",
                table: "Releases",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Format",
                table: "Releases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Releases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchemaVersion",
                table: "Releases",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SchemaVersion",
                table: "Labels",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SchemaVersion",
                table: "Artists",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE \"Releases\" SET \"SchemaVersion\" = 0;");
            migrationBuilder.Sql("UPDATE \"Artists\" SET \"SchemaVersion\" = 0;");
            migrationBuilder.Sql("UPDATE \"Labels\" SET \"SchemaVersion\" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "Artists");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "Labels");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "Releases");

            migrationBuilder.DropColumn(
                name: "CommunityHaveCount",
                table: "Releases");

            migrationBuilder.DropColumn(
                name: "CommunityWantCount",
                table: "Releases");

            migrationBuilder.DropColumn(
                name: "Format",
                table: "Releases");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Releases");
        }
    }
}
