using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dike.Infrastructure.Migrations.Finance
{
    /// <inheritdoc />
    public partial class InitialPostgresFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dike_finance");

            migrationBuilder.CreateTable(
                name: "AccountMappings",
                schema: "dike_finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Company = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Subsidiary = table.Column<string>(type: "text", nullable: false),
                    AccountingBook = table.Column<string>(type: "text", nullable: false),
                    GlAccount = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Connection = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalAccount = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                schema: "dike_finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MappingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchGroups",
                schema: "dike_finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MappingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Preparer = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    RuleVersion = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncJobs",
                schema: "dike_finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MappingId = table.Column<Guid>(type: "uuid", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    To = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LeaseGeneration = table.Column<long>(type: "bigint", nullable: false),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Movements",
                schema: "dike_finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MappingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Side = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SourceVersion = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,2)", precision: 19, scale: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    Booked = table.Column<bool>(type: "boolean", nullable: false),
                    ImportRunId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Movements_AccountMappings_MappingId",
                        column: x => x.MappingId,
                        principalSchema: "dike_finance",
                        principalTable: "AccountMappings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatchMembers",
                schema: "dike_finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceVersion = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchMembers_MatchGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "dike_finance",
                        principalTable: "MatchGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchMembers_Movements_MovementId",
                        column: x => x.MovementId,
                        principalSchema: "dike_finance",
                        principalTable: "Movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_Company_Connection_ExternalAccount",
                schema: "dike_finance",
                table: "AccountMappings",
                columns: new[] { "Company", "Connection", "ExternalAccount" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_MappingId_IdempotencyKey",
                schema: "dike_finance",
                table: "AuditEvents",
                columns: new[] { "MappingId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchMembers_GroupId",
                schema: "dike_finance",
                table: "MatchMembers",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchMembers_MovementId",
                schema: "dike_finance",
                table: "MatchMembers",
                column: "MovementId",
                unique: true,
                filter: "\"Active\"");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_MappingId_Side_SourceId",
                schema: "dike_finance",
                table: "Movements",
                columns: new[] { "MappingId", "Side", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncJobs_Status_NextRunAt",
                schema: "dike_finance",
                table: "SyncJobs",
                columns: new[] { "Status", "NextRunAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents",
                schema: "dike_finance");

            migrationBuilder.DropTable(
                name: "MatchMembers",
                schema: "dike_finance");

            migrationBuilder.DropTable(
                name: "SyncJobs",
                schema: "dike_finance");

            migrationBuilder.DropTable(
                name: "MatchGroups",
                schema: "dike_finance");

            migrationBuilder.DropTable(
                name: "Movements",
                schema: "dike_finance");

            migrationBuilder.DropTable(
                name: "AccountMappings",
                schema: "dike_finance");
        }
    }
}
