using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AykutOnPC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitorIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PageViews",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Referrer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    HashedIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DeviceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    VisitedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageViews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PageViews_HashedIp",
                table: "PageViews",
                column: "HashedIp");

            migrationBuilder.CreateIndex(
                name: "IX_PageViews_VisitedAtUtc",
                table: "PageViews",
                column: "VisitedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PageViews_VisitedAtUtc_Path",
                table: "PageViews",
                columns: new[] { "VisitedAtUtc", "Path" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageViews");
        }
    }
}
