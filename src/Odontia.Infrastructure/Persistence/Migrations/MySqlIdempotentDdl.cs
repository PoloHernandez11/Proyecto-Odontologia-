using Microsoft.EntityFrameworkCore.Migrations;

namespace Odontia.Infrastructure.Persistence.Migrations;

/// <summary>
/// DDL reejecutable para MySQL. En MySQL cada sentencia DDL hace COMMIT implícito: si una migración falla a mitad de
/// camino, los cambios previos quedan aplicados pero la migración no se registra, y el reintento fallaría para siempre
/// ("Duplicate column name"). Estas operaciones sólo se ejecutan si el objeto todavía no existe, de modo que un
/// reintento completa lo que faltó. Usan sentencias preparadas porque MySQL no admite ADD COLUMN IF NOT EXISTS.
/// </summary>
internal static class MySqlIdempotentDdl
{
    private const string EmptyGuid = "00000000-0000-0000-0000-000000000000";

    /// <summary>Ejecuta <paramref name="ddl"/> sólo si <paramref name="existsQuery"/> devuelve 0.</summary>
    private static void RunUnless(MigrationBuilder mb, string existsQuery, string ddl)
    {
        var escaped = ddl.Replace("'", "''");
        mb.Sql($"SET @`odontia_ddl` := IF(({existsQuery}) = 0, '{escaped}', 'DO 0');");
        mb.Sql("PREPARE odontia_stmt FROM @`odontia_ddl`;");
        mb.Sql("EXECUTE odontia_stmt;");
        mb.Sql("DEALLOCATE PREPARE odontia_stmt;");
    }

    /// <summary>
    /// Agrega la columna de concurrencia <c>Version</c> (char(36), NOT NULL) si falta y asigna una versión distinta a
    /// cada fila existente que aún tenga la versión vacía (las filas nuevas la generan en el dominio).
    /// </summary>
    public static void AddVersionColumn(MigrationBuilder mb, string table)
    {
        RunUnless(mb,
            $"SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{table}' AND COLUMN_NAME = 'Version'",
            $"ALTER TABLE `{table}` ADD `Version` char(36) NOT NULL DEFAULT '{EmptyGuid}'");
        mb.Sql($"UPDATE `{table}` SET `Version` = UUID() WHERE `Version` = '{EmptyGuid}';");
    }

    /// <summary>
    /// Crea en UNA sola sentencia ALTER TABLE (atómica) los índices indicados, si todavía no existe el primero.
    /// Si hay datos que violan un índice único, falla sin haber creado ninguno.
    /// </summary>
    public static void AddIndexes(MigrationBuilder mb, string table, params (string Name, bool Unique, string[] Columns)[] indexes)
    {
        var clauses = string.Join(", ", indexes.Select(i =>
            $"ADD {(i.Unique ? "UNIQUE " : "")}INDEX `{i.Name}` ({string.Join(", ", i.Columns.Select(c => $"`{c}`"))})"));
        RunUnless(mb,
            $"SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{table}' AND INDEX_NAME = '{indexes[0].Name}'",
            $"ALTER TABLE `{table}` {clauses}");
    }
}
