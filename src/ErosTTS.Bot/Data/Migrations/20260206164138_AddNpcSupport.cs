using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErosTTS.Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNpcSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Create new tables first (before dropping old ones) for data migration
            migrationBuilder.CreateTable(
                name: "Npcs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Personality = table.Column<string>(type: "TEXT", nullable: false),
                    VoiceId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Npcs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuildNpcSettings",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    ActiveNpcId = table.Column<int>(type: "INTEGER", nullable: true),
                    AutoSwitchEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SharedHistory = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildNpcSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildNpcSettings_Npcs_ActiveNpcId",
                        column: x => x.ActiveNpcId,
                        principalTable: "Npcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NpcConversationMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    NpcId = table.Column<int>(type: "INTEGER", nullable: true),
                    NpcName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcConversationMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcConversationMessages_Npcs_NpcId",
                        column: x => x.NpcId,
                        principalTable: "Npcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildNpcSettings_ActiveNpcId",
                table: "GuildNpcSettings",
                column: "ActiveNpcId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcConversationMessages_GuildId",
                table: "NpcConversationMessages",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcConversationMessages_NpcId",
                table: "NpcConversationMessages",
                column: "NpcId");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_GuildId_Name",
                table: "Npcs",
                columns: new[] { "GuildId", "Name" },
                unique: true);

            // Step 2: Migrate existing character state data to NPC tables
            migrationBuilder.Sql("""
                INSERT INTO Npcs (GuildId, Name, Personality, CreatedAt, UpdatedAt)
                SELECT GuildId, 'Character', Context, UpdatedAt, UpdatedAt
                FROM GuildCharacterStates
                WHERE Context IS NOT NULL AND Context != '';
                """);

            migrationBuilder.Sql("""
                INSERT INTO GuildNpcSettings (GuildId, ActiveNpcId, AutoSwitchEnabled, SharedHistory, UpdatedAt)
                SELECT n.GuildId, n.Id, 0, 0, n.UpdatedAt
                FROM Npcs n
                INNER JOIN GuildCharacterStates g ON g.GuildId = n.GuildId;
                """);

            migrationBuilder.Sql("""
                INSERT INTO NpcConversationMessages (GuildId, NpcId, NpcName, Role, Content, Timestamp)
                SELECT cm.GuildId, n.Id, n.Name, cm.Role, cm.Content, cm.Timestamp
                FROM ConversationMessages cm
                INNER JOIN Npcs n ON n.GuildId = cm.GuildId;
                """);

            // Step 3: Drop old tables
            migrationBuilder.DropTable(
                name: "ConversationMessages");

            migrationBuilder.DropTable(
                name: "GuildCharacterStates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildNpcSettings");

            migrationBuilder.DropTable(
                name: "NpcConversationMessages");

            migrationBuilder.DropTable(
                name: "Npcs");

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
                name: "ConversationMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
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
    }
}
