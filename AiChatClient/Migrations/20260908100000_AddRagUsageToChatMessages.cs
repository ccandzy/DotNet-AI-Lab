using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiChatClient.Migrations
{
    /// <inheritdoc />
    public partial class AddRagUsageToChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RagWasEnabled",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RagWasEnabled",
                table: "ChatMessages");
        }
    }
}
