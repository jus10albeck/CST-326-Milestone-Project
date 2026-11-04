
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquipmentInventoryApp.Migrations
{
    // Adds a column that records when a checkout ended because the item was lost
    public partial class AddCheckoutMarkedLost : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MarkedLost",
                table: "Checkouts",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarkedLost",
                table: "Checkouts");
        }
    }
}
