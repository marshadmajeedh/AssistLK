using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssistLK.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRequestMatchingExpiryTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MatchingExpiresAtUtc",
                table: "ServiceRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadyForMatchingAtUtc",
                table: "ServiceRequests",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MatchingExpiresAtUtc",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "ReadyForMatchingAtUtc",
                table: "ServiceRequests");
        }
    }
}
