using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssistLK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddComponent4TrackingEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Complaints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComplainantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Complaints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Complaints_ServiceJobs_ServiceJobId",
                        column: x => x.ServiceJobId,
                        principalTable: "ServiceJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompletionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkSummary = table.Column<string>(type: "text", nullable: false),
                    ProofOfWorkImageUrl = table.Column<string>(type: "text", nullable: true),
                    AdditionalCost = table.Column<decimal>(type: "numeric", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompletionRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompletionRecords_ServiceJobs_ServiceJobId",
                        column: x => x.ServiceJobId,
                        principalTable: "ServiceJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Feedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Feedbacks_ServiceJobs_ServiceJobId",
                        column: x => x.ServiceJobId,
                        principalTable: "ServiceJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldStatus = table.Column<int>(type: "integer", nullable: false),
                    NewStatus = table.Column<int>(type: "integer", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceStatusHistories_ServiceJobs_ServiceJobId",
                        column: x => x.ServiceJobId,
                        principalTable: "ServiceJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_ServiceJobId",
                table: "Complaints",
                column: "ServiceJobId");

            migrationBuilder.CreateIndex(
                name: "IX_CompletionRecords_ServiceJobId",
                table: "CompletionRecords",
                column: "ServiceJobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_ServiceJobId",
                table: "Feedbacks",
                column: "ServiceJobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStatusHistories_ServiceJobId",
                table: "ServiceStatusHistories",
                column: "ServiceJobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Complaints");

            migrationBuilder.DropTable(
                name: "CompletionRecords");

            migrationBuilder.DropTable(
                name: "Feedbacks");

            migrationBuilder.DropTable(
                name: "ServiceStatusHistories");

            migrationBuilder.DropTable(
                name: "ServiceJobs");
        }
    }
}
