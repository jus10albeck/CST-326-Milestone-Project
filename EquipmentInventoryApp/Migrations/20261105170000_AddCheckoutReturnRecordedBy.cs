
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquipmentInventoryApp.Migrations
{
    // Records which administrator recorded a return on an employee's behalf
    public partial class AddCheckoutReturnRecordedBy : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReturnRecordedByUserId",
                table: "Checkouts",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReturnRecordedByUserId",
                table: "Checkouts");
        }
    }
}
