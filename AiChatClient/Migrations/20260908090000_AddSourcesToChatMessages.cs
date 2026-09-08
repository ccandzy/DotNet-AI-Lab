using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiChatClient.Migrations
{
    /// <inheritdoc />
    public partial class AddSourcesToChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourcesJson",
                table: "ChatMessages",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourcesJson",
                table: "ChatMessages");
        }
    }
}
