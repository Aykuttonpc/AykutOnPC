using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AykutOnPC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatLogInboxFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminNote",
                table: "ChatLogs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReviewed",
                table: "ChatLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LinkedKnowledgeEntryId",
                table: "ChatLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatLogs_Reviewed_CreatedAt",
                table: "ChatLogs",
                columns: new[] { "IsReviewed", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatLogs_Reviewed_CreatedAt",
                table: "ChatLogs");

            migrationBuilder.DropColumn(
                name: "AdminNote",
                table: "ChatLogs");

            migrationBuilder.DropColumn(
                name: "IsReviewed",
                table: "ChatLogs");

            migrationBuilder.DropColumn(
                name: "LinkedKnowledgeEntryId",
                table: "ChatLogs");
        }
    }
}
