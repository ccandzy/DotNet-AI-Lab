using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiChatClient.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyAIRoleTemperature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Model",
                table: "AIRoles");

            migrationBuilder.DropColumn(
                name: "Temperature",
                table: "AIRoles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "AIRoles",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "Temperature",
                table: "AIRoles",
                type: "REAL",
                nullable: false,
                defaultValue: 0.69999999999999996);
        }
    }
}
