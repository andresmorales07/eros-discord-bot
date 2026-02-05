using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErosTTS.Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuildCharacterStates",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    Context = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildCharacterStates", x => x.GuildId);
                });

            migrationBuilder.CreateTable(
                name: "GuildConfigurations",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    TextChannelId = table.Column<long>(type: "INTEGER", nullable: true),
                    VoiceChannelId = table.Column<long>(type: "INTEGER", nullable: true),
                    VoiceId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildConfigurations", x => x.GuildId);
                });

            migrationBuilder.CreateTable(
                name: "ConversationMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationMessages_GuildCharacterStates_GuildId",
                        column: x => x.GuildId,
                        principalTable: "GuildCharacterStates",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_GuildId",
                table: "ConversationMessages",
                column: "GuildId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationMessages");

            migrationBuilder.DropTable(
                name: "GuildConfigurations");

            migrationBuilder.DropTable(
                name: "GuildCharacterStates");
        }
    }
}
