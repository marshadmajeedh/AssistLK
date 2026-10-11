using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssistLK.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchingExecutionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "MatchingExecutions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "MatchingExecutions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThreadId",
                table: "MatchingExecutions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MatchingExecutions_ThreadId",
                table: "MatchingExecutions",
                column: "ThreadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MatchingExecutions_ThreadId",
                table: "MatchingExecutions");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "MatchingExecutions");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "MatchingExecutions");

            migrationBuilder.DropColumn(
                name: "ThreadId",
                table: "MatchingExecutions");
        }
    }
}
