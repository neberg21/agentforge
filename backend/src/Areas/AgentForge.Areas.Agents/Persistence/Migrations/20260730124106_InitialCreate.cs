using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentForge.Areas.Agents.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agents_agent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    SystemPrompt = table.Column<string>(type: "TEXT", nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Temperature = table.Column<double>(type: "REAL", nullable: false),
                    MaxOutputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxTurns = table.Column<int>(type: "INTEGER", nullable: false),
                    AllowedTools = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents_agent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agents_conversation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents_conversation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agents_run",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AgentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    Objective = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    PromptTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    CompletionTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    CostEstimate = table.Column<decimal>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents_run", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agents_run_agents_agent_AgentId",
                        column: x => x.AgentId,
                        principalTable: "agents_agent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agents_conversation_message",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: true),
                    ToolCallsJson = table.Column<string>(type: "TEXT", nullable: true),
                    ToolCallId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SenderAgentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SenderName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    MentionsJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents_conversation_message", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agents_conversation_message_agents_conversation_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "agents_conversation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agents_conversation_participant",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents_conversation_participant", x => new { x.ConversationId, x.AgentId });
                    table.ForeignKey(
                        name: "FK_agents_conversation_participant_agents_agent_AgentId",
                        column: x => x.AgentId,
                        principalTable: "agents_agent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agents_conversation_participant_agents_conversation_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "agents_conversation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agents_run_message",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: true),
                    ToolCallsJson = table.Column<string>(type: "TEXT", nullable: true),
                    ToolCallId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents_run_message", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agents_run_message_agents_run_RunId",
                        column: x => x.RunId,
                        principalTable: "agents_run",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agents_agent_OwnerId",
                table: "agents_agent",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_agents_agent_OwnerId_Name",
                table: "agents_agent",
                columns: new[] { "OwnerId", "Name" },
                unique: true,
                filter: "\"ArchivedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_agents_conversation_OwnerId",
                table: "agents_conversation",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_agents_conversation_message_ConversationId_Sequence",
                table: "agents_conversation_message",
                columns: new[] { "ConversationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agents_conversation_participant_AgentId",
                table: "agents_conversation_participant",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_agents_run_AgentId",
                table: "agents_run",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_agents_run_OwnerId_AgentId",
                table: "agents_run",
                columns: new[] { "OwnerId", "AgentId" });

            migrationBuilder.CreateIndex(
                name: "IX_agents_run_message_RunId_Sequence",
                table: "agents_run_message",
                columns: new[] { "RunId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agents_conversation_message");

            migrationBuilder.DropTable(
                name: "agents_conversation_participant");

            migrationBuilder.DropTable(
                name: "agents_run_message");

            migrationBuilder.DropTable(
                name: "agents_conversation");

            migrationBuilder.DropTable(
                name: "agents_run");

            migrationBuilder.DropTable(
                name: "agents_agent");
        }
    }
}
