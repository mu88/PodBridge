using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PodBridge.Persistence.Migrations
{
    /// <inheritdoc />
    // EF Core-generated migration: excluded from coverage since it's tool-generated, not authored/tested code.
    [ExcludeFromCodeCoverage]
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Podcasts",
                columns: table => new
                {
                    PodcastId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ShowId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Podcasts", x => x.PodcastId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Podcasts_ShowId",
                table: "Podcasts",
                column: "ShowId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Podcasts");
        }
    }
}
