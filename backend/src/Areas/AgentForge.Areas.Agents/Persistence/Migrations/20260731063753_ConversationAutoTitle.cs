using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentForge.Areas.Agents.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConversationAutoTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompletedTurnCount",
                table: "agents_conversation",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TitleGeneratedAtTurn",
                table: "agents_conversation",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleMode",
                table: "agents_conversation",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedTurnCount",
                table: "agents_conversation");

            migrationBuilder.DropColumn(
                name: "TitleGeneratedAtTurn",
                table: "agents_conversation");

            migrationBuilder.DropColumn(
                name: "TitleMode",
                table: "agents_conversation");
        }
    }
}
