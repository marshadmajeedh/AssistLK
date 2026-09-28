using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssistLK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddComplaintSentimentAndCustomerComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiSentiment",
                table: "Complaints",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerComment",
                table: "Complaints",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiSentiment",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "CustomerComment",
                table: "Complaints");
        }
    }
}
