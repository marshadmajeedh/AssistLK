using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssistLK.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProblemAnalysisVisualEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VisualEvidence",
                table: "ProblemAnalyses",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{\"visionStatus\":\"not_requested\",\"attachmentIdsUsed\":[],\"observations\":[],\"limitations\":[]}'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VisualEvidence",
                table: "ProblemAnalyses");
        }
    }
}
