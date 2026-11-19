using System;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odontia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Antes, "Extracción" registrada como existente significaba "pieza ya extraída". Desde
    /// LabWorkTurnoYOdontogramaSimplificado la condición pasó a ser "Extraer" (a extraer, la pieza sigue presente),
    /// así que los hallazgos vigentes y existentes de Extracción se convierten en "Ausente" para que la pieza
    /// se siga viendo como ausente. Las extracciones planificadas quedan como "Extraer".
    /// </summary>
    public partial class ExtraccionesExistentesAAusente : Migration
    {
        private const string Extraccion = "6c1d3e0a-7b2f-4e11-8a55-000000000006";
        private const string Ausente = "6c1d3e0a-7b2f-4e11-8a55-000000000007";
        private const int Existente = 1; // FindingStatus.Existing

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var ahora = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            // 1) Si la pieza ya tiene un "Ausente" vigente, la extracción vieja se cierra para no duplicar la marca.
            //    (La subconsulta va envuelta en una tabla derivada porque MySQL no permite leer la tabla que actualiza.)
            migrationBuilder.Sql($@"
UPDATE ToothFindings
SET IsCurrent = 0, ClosedAtUtc = '{ahora}', ClosedReason = 'Convertido: la pieza ya figuraba como ausente'
WHERE ConditionId = '{Extraccion}' AND Status = {Existente} AND IsCurrent = 1
  AND EXISTS (
    SELECT 1 FROM (SELECT OdontogramId, ToothCode FROM ToothFindings WHERE ConditionId = '{Ausente}' AND IsCurrent = 1) AS m
    WHERE m.OdontogramId = ToothFindings.OdontogramId AND m.ToothCode = ToothFindings.ToothCode);");

            // 2) El resto de las extracciones existentes y vigentes pasan a "Ausente".
            migrationBuilder.Sql($@"
UPDATE ToothFindings
SET ConditionId = '{Ausente}'
WHERE ConditionId = '{Extraccion}' AND Status = {Existente} AND IsCurrent = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Conversión de datos sin vuelta atrás exacta: no se puede distinguir un "Ausente" convertido de uno
            // cargado como tal. Se deja sin cambios a propósito.
        }
    }
}
