using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Odontia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinanzasConcurrenciaYRecordatorios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL no tiene DDL transaccional: cada sentencia confirma por sí sola. Por eso (1) los índices únicos van
            // PRIMERO y en una sola sentencia atómica: si existen movimientos duplicados (p. ej. anulaciones simultáneas
            // previas) la migración falla sin haber cambiado nada; y (2) todo el DDL es reejecutable (MySqlIdempotentDdl),
            // de modo que un reintento tras corregir los datos completa la migración en lugar de fallar con
            // "Duplicate column name 'Version'". Verificar antes de desplegar:
            //   SELECT PaymentId, Type, COUNT(*) FROM AccountMovements WHERE PaymentId IS NOT NULL GROUP BY PaymentId, Type HAVING COUNT(*) > 1;
            //   SELECT ReversesMovementId, COUNT(*) FROM AccountMovements WHERE ReversesMovementId IS NOT NULL GROUP BY ReversesMovementId HAVING COUNT(*) > 1;
            MySqlIdempotentDdl.AddIndexes(migrationBuilder, "AccountMovements",
                ("IX_AccountMovements_PaymentId_Type", true, ["PaymentId", "Type"]),
                ("IX_AccountMovements_ReversesMovementId", true, ["ReversesMovementId"]));

            // Versión inicial distinta por fila para los registros existentes (las filas nuevas la generan en el dominio).
            MySqlIdempotentDdl.AddVersionColumn(migrationBuilder, "Payments");
            MySqlIdempotentDdl.AddVersionColumn(migrationBuilder, "Budgets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountMovements_PaymentId_Type",
                table: "AccountMovements");

            migrationBuilder.DropIndex(
                name: "IX_AccountMovements_ReversesMovementId",
                table: "AccountMovements");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Budgets");
        }
    }
}
