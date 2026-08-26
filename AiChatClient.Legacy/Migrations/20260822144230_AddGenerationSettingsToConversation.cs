using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiChatClient.Migrations
{
    /// <inheritdoc />
    public partial class AddGenerationSettingsToConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxTokens",
                table: "Conversations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Temperature",
                table: "Conversations",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TopP",
                table: "Conversations",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxTokens",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Temperature",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "TopP",
                table: "Conversations");
        }
    }
}
