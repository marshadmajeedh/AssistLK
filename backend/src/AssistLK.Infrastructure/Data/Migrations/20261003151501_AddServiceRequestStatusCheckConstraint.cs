using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssistLK.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRequestStatusCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_ServiceRequests_Status_Valid",
                table: "ServiceRequests",
                sql: "\"Status\" IN ('Created', 'Analyzing', 'AwaitingInformation', 'Analyzed', 'ReadyForMatching', 'Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ServiceRequests_Status_Valid",
                table: "ServiceRequests");
        }
    }
}
