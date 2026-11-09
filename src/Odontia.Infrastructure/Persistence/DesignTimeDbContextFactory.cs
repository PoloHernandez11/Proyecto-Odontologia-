using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Odontia.Application.Common;

namespace Odontia.Infrastructure.Persistence;

/// <summary>
/// Fábrica usada sólo por las herramientas de EF Core (dotnet ef). Lee la cadena de conexión de la
/// variable de entorno ODONTIA_DESIGN_CONNECTION; si no existe, usa un valor de marcador sin credenciales
/// (suficiente para generar migraciones, que no requieren conectarse a la base).
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OdontiaDbContext>
{
    public OdontiaDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ODONTIA_DESIGN_CONNECTION")
                 ?? "server=localhost;port=3306;database=odontia_design;user=design";
        var options = new DbContextOptionsBuilder<OdontiaDbContext>()
            .UseMySQL(cs, o => o.MigrationsAssembly(typeof(OdontiaDbContext).Assembly.FullName))
            .Options;
        return new OdontiaDbContext(options, new TenantContext(), TimeProvider.System);
    }
}
