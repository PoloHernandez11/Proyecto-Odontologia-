using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odontia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LabWorkTurnoYOdontogramaSimplificado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentId",
                table: "LabWorks",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToothCodes",
                table: "LabWorks",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Trabajos existentes de una sola pieza: ToothCodes = ToothCode.
            migrationBuilder.Sql("UPDATE LabWorks SET ToothCodes = CAST(ToothCode AS CHAR) WHERE ToothCode IS NOT NULL;");

            migrationBuilder.UpdateData(
                table: "OdontogramConditions",
                keyColumn: "Id",
                keyValue: new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000002"),
                column: "Name",
                value: "Arreglo");

            migrationBuilder.UpdateData(
                table: "OdontogramConditions",
                keyColumn: "Id",
                keyValue: new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000003"),
                column: "IsActive",
                value: false);

            migrationBuilder.UpdateData(
                table: "OdontogramConditions",
                keyColumn: "Id",
                keyValue: new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000005"),
                column: "IsActive",
                value: false);

            migrationBuilder.UpdateData(
                table: "OdontogramConditions",
                keyColumn: "Id",
                keyValue: new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000006"),
                columns: new[] { "MarksToothAbsent", "Name" },
                values: new object[] { false, "Extraer" });

            migrationBuilder.UpdateData(
                table: "OdontogramConditions",
                keyColumn: "Id",
                keyValue: new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000009"),
                column: "IsActive",
                value: false);

            migrationBuilder.UpdateData(
                table: "OdontogramConditions",
                keyColumn: "Id",
                keyValue: new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000010"),
                column: "IsActive",
                value: false);

            migrationBuilder.UpdateData(
                table: "OdontogramConditions",
                keyColumn: "Id",
                keyValue: new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000011"),
                column: "IsActive",
                value: false);

            migrationBuilder.CreateIndex(
                name: "IX_LabWorks_ConsultorioId_AppointmentId",
                table: "LabWorks",
                columns: new[] { "ConsultorioId", "AppointmentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_LabWorks_Appointments_ConsultorioId_AppointmentId",
                table: "LabWorks",
                columns: new[] { "ConsultorioId", "AppointmentId" },
                principalTable: "Appointments",
                principalColumns: new[] { "ConsultorioId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabWorks_Appointments_ConsultorioId_AppointmentId",
                table: "LabWorks");

            migrationBuilder.DropIndex(
                name: "IX_LabWorks_ConsultorioId_AppointmentId",
                table: "LabWorks");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "LabWorks");

            migrationBuilder.DropColumn(
                name: "ToothCodes",
                table: "LabWorks");

            migrationBuilder.UpdateData(
                table: "OdontogramConditions",
                keyColumn: "Id",
                keyValue: new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000002"),
                column: "Name",
                value: "Obturación");

            migrationBuilder.UpdateData(
                table: "OdontogramConditions",
                keyColumn: "Id",
                keyValue: new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000003"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "OdontogramConditions",
                keyColumn: "Id",
                keyValue: new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000005"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "OdontogramConditions",
                keyColumn: "Id",
                keyValue: new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000006"),
                columns: new[] { "MarksToothAbsent", "Name" },
                values: new object[] { true, "Extracción" });

            migrationBuilder.UpdateData(
                table: "OdontogramConditions",
                keyColumn: "Id",
                keyValue: new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000009"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "OdontogramConditions",
                keyColumn: "Id",
                keyValue: new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000010"),
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "OdontogramConditions",
                keyColumn: "Id",
                keyValue: new Guid("6c1d3e0a-7b2f-4e11-8a55-000000000011"),
                column: "IsActive",
                value: true);
        }
    }
}
