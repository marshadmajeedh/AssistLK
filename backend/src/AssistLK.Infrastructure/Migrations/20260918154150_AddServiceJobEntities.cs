using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssistLK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceJobEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "OldStatus",
                table: "ServiceStatusHistories",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "ChangedByUserId",
                table: "ServiceStatusHistories",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "ServiceJobs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceRequestId",
                table: "ServiceJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Complaints",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Complaints",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "ServiceRequestId",
                table: "ServiceJobs");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Complaints");

            migrationBuilder.AlterColumn<string>(
                name: "OldStatus",
                table: "ServiceStatusHistories",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ChangedByUserId",
                table: "ServiceStatusHistories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
