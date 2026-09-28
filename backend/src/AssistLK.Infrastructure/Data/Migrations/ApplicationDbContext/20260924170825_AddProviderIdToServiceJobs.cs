using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssistLK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderIdToServiceJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProviderId",
                table: "ServiceJobs",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "ServiceJobs");
        }
    }
}
