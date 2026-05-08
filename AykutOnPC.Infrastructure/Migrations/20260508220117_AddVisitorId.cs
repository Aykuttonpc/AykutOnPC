using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AykutOnPC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitorId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VisitorId",
                table: "PageViews",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PageViews_VisitorId",
                table: "PageViews",
                column: "VisitorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PageViews_VisitorId",
                table: "PageViews");

            migrationBuilder.DropColumn(
                name: "VisitorId",
                table: "PageViews");
        }
    }
}
