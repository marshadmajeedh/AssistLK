using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssistLK.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRequestAttachmentsAndEvidenceRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "EvidenceRevision",
                table: "ServiceRequests",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "EvidenceRevision",
                table: "ProblemAnalyses",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "ServiceRequestAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRequestAttachments", x => x.Id);
                    table.CheckConstraint("CK_Attachments_Dimensions", "\"Width\" > 0 AND \"Height\" > 0");
                    table.CheckConstraint("CK_Attachments_Size", "\"FileSizeBytes\" > 0");
                    table.CheckConstraint("CK_Attachments_Slot", "\"Slot\" BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_ServiceRequestAttachments_ServiceRequests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "ServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServiceRequests_EvidenceRevision",
                table: "ServiceRequests",
                sql: "\"EvidenceRevision\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProblemAnalyses_EvidenceRevision",
                table: "ProblemAnalyses",
                sql: "\"EvidenceRevision\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequestAttachments_ServiceRequestId_Slot",
                table: "ServiceRequestAttachments",
                columns: new[] { "ServiceRequestId", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequestAttachments_StorageKey",
                table: "ServiceRequestAttachments",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceRequestAttachments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ServiceRequests_EvidenceRevision",
                table: "ServiceRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProblemAnalyses_EvidenceRevision",
                table: "ProblemAnalyses");

            migrationBuilder.DropColumn(
                name: "EvidenceRevision",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "EvidenceRevision",
                table: "ProblemAnalyses");
        }
    }
}
