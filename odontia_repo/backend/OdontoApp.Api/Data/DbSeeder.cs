using Microsoft.EntityFrameworkCore;

namespace OdontoApp.Api.Data;

/// <summary>
/// Solo en desarrollo: crea o migra el esquema. No crea usuarios: el primer ingreso es el alta
/// del odontólogo dueño desde la pantalla /registro, y el resto del equipo entra por invitación.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OdontoDbContext>();
        // Con migraciones (dotnet ef migrations add ...) las aplica; sin ellas crea las tablas directamente.
        if (db.Database.GetMigrations().Any())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();
    }
}
