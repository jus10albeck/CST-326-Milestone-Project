
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquipmentInventoryApp.Migrations
{
    // Adds a note explaining why an item is Under Repair
    public partial class AddEquipmentRepairNote : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RepairNote",
                table: "Equipment",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RepairNote",
                table: "Equipment");
        }
    }
}
