using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErosTTS.Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTtsProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TtsProvider",
                table: "GuildConfigurations",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TtsProvider",
                table: "GuildConfigurations");
        }
    }
}
