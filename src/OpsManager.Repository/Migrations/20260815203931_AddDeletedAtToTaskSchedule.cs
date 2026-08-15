using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsManager.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedAtToTaskSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "task_schedules",
                type: "timestamptz",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "task_schedules");
        }
    }
}
