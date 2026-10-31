
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquipmentInventoryApp.Migrations
{
    public partial class AddUserSecurityStamp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Users",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@"
                UPDATE `Users`
                SET `SecurityStamp` =
                    REPLACE(UUID(), '-', '')
                WHERE `SecurityStamp` = '';
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE `Users`
                ALTER COLUMN `SecurityStamp` DROP DEFAULT;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Users");
        }
    }
}
