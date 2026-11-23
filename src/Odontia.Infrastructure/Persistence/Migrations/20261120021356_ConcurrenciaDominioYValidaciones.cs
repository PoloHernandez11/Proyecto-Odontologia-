using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odontia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConcurrenciaDominioYValidaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DDL reejecutable (MySQL no tiene DDL transaccional): si la migración se interrumpe a mitad de camino, el
            // reintento agrega sólo las columnas que falten. Cada tabla recibe una versión inicial distinta por fila para
            // los registros existentes (las filas nuevas la generan en el dominio).
            MySqlIdempotentDdl.AddVersionColumn(migrationBuilder, "TreatmentPlans");
            MySqlIdempotentDdl.AddVersionColumn(migrationBuilder, "TreatmentPlanItems");
            MySqlIdempotentDdl.AddVersionColumn(migrationBuilder, "Patients");
            MySqlIdempotentDdl.AddVersionColumn(migrationBuilder, "Odontograms");
            MySqlIdempotentDdl.AddVersionColumn(migrationBuilder, "MedicalHistories");
            MySqlIdempotentDdl.AddVersionColumn(migrationBuilder, "LabWorks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "TreatmentPlans");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "TreatmentPlanItems");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Odontograms");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "MedicalHistories");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "LabWorks");
        }
    }
}
