using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventRecommender.Migrations
{
    /// <inheritdoc />
    public partial class AddUserEventInteraction_StatusRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InteractionType",
                table: "UserEventInteractions");

            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "UserEventInteractions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "UserEventInteractions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rating",
                table: "UserEventInteractions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "UserEventInteractions");

            migrationBuilder.AddColumn<string>(
                name: "InteractionType",
                table: "UserEventInteractions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
