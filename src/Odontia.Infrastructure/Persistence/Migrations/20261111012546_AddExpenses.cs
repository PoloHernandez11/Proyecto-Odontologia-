using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odontia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Description = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Supplier = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    Reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    LaboratoryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    RegisteredByUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    VoidReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConsultorioId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Expenses_ConsultorioMembers_ConsultorioId_RegisteredByUserId",
                        columns: x => new { x.ConsultorioId, x.RegisteredByUserId },
                        principalTable: "ConsultorioMembers",
                        principalColumns: new[] { "ConsultorioId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_Consultorios_ConsultorioId",
                        column: x => x.ConsultorioId,
                        principalTable: "Consultorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_Laboratories_ConsultorioId_LaboratoryId",
                        columns: x => new { x.ConsultorioId, x.LaboratoryId },
                        principalTable: "Laboratories",
                        principalColumns: new[] { "ConsultorioId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ConsultorioId_Category_PaidAtUtc",
                table: "Expenses",
                columns: new[] { "ConsultorioId", "Category", "PaidAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ConsultorioId_LaboratoryId",
                table: "Expenses",
                columns: new[] { "ConsultorioId", "LaboratoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ConsultorioId_PaidAtUtc",
                table: "Expenses",
                columns: new[] { "ConsultorioId", "PaidAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ConsultorioId_RegisteredByUserId",
                table: "Expenses",
                columns: new[] { "ConsultorioId", "RegisteredByUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Expenses");
        }
    }
}
