using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventRecommender.Migrations
{
    /// <inheritdoc />
    public partial class AddEventClick : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventClicks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    EventId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClickedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DwellMs = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventClicks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventClicks_UserId_EventId_ClickedAt",
                table: "EventClicks",
                columns: new[] { "UserId", "EventId", "ClickedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventClicks");
        }
    }
}
