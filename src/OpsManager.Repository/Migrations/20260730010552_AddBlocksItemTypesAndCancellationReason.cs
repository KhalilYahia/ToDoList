using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsManager.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddBlocksItemTypesAndCancellationReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                table: "tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "item_type",
                table: "task_template_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "main_block_title",
                table: "task_template_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "options",
                table: "task_template_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sub_block_title",
                table: "task_template_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "item_type",
                table: "task_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "main_block_title",
                table: "task_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "options",
                table: "task_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sub_block_title",
                table: "task_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "value",
                table: "task_items",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "item_type",
                table: "task_template_items");

            migrationBuilder.DropColumn(
                name: "main_block_title",
                table: "task_template_items");

            migrationBuilder.DropColumn(
                name: "options",
                table: "task_template_items");

            migrationBuilder.DropColumn(
                name: "sub_block_title",
                table: "task_template_items");

            migrationBuilder.DropColumn(
                name: "item_type",
                table: "task_items");

            migrationBuilder.DropColumn(
                name: "main_block_title",
                table: "task_items");

            migrationBuilder.DropColumn(
                name: "options",
                table: "task_items");

            migrationBuilder.DropColumn(
                name: "sub_block_title",
                table: "task_items");

            migrationBuilder.DropColumn(
                name: "value",
                table: "task_items");
        }
    }
}
