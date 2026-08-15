using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsManager.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxAttachmentsToTaskItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "max_attachments",
                table: "task_template_items",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<int>(
                name: "max_attachments",
                table: "task_items",
                type: "integer",
                nullable: false,
                defaultValue: 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "max_attachments",
                table: "task_template_items");

            migrationBuilder.DropColumn(
                name: "max_attachments",
                table: "task_items");
        }
    }
}
