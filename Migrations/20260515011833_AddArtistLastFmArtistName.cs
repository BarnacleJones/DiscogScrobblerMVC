using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscogScrobblerMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddArtistLastFmArtistName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastFmArtistName",
                table: "Artists",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastFmArtistName",
                table: "Artists");
        }
    }
}
