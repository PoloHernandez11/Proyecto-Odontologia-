using Microsoft.EntityFrameworkCore;
using OdontoApp.Api.Data;
using OdontoApp.Api.Dtos;
using OdontoApp.Api.Models;

namespace OdontoApp.Api.Auth;

/// <summary>
/// Permisos configurables por rol. El odontólogo (administrador) siempre tiene acceso total.
/// Si el consultorio todavía no guardó su matriz, se usan los valores iniciales de la pantalla.
/// </summary>
public class PermisosService(OdontoDbContext db)
{
    /// <summary>Valores iniciales de la matriz (los de la pantalla de Perfil y configuración).</summary>
    public static NivelPermiso Inicial(ModuloPermiso modulo, AccionPermiso accion) => (modulo, accion) switch
    {
        (ModuloPermiso.PagosPacientes, AccionPermiso.Registrar) => NivelPermiso.Permitido,
        (ModuloPermiso.PagosPacientes, AccionPermiso.EditarCorregir) => NivelPermiso.ConAprobacion,
        (ModuloPermiso.PagosPacientes, AccionPermiso.VerResumenes) => NivelPermiso.Permitido,
        (ModuloPermiso.LaboratorioTrabajos, AccionPermiso.Registrar) => NivelPermiso.Permitido,
        (ModuloPermiso.LaboratorioTrabajos, AccionPermiso.EditarCorregir) => NivelPermiso.Permitido,
        (ModuloPermiso.LaboratorioPagos, AccionPermiso.Registrar) => NivelPermiso.Permitido,
        (ModuloPermiso.LaboratorioPagos, AccionPermiso.EditarCorregir) => NivelPermiso.ConAprobacion,
        _ => NivelPermiso.SinAcceso,
    };

    /// <summary>Todas las combinaciones módulo × acción de la matriz.</summary>
    public static IEnumerable<(ModuloPermiso Modulo, AccionPermiso Accion)> Combinaciones() =>
        from m in Enum.GetValues<ModuloPermiso>()
        from a in Enum.GetValues<AccionPermiso>()
        select (m, a);

    public async Task<NivelPermiso> NivelAsync(int consultorioId, RolUsuario rol, ModuloPermiso modulo, AccionPermiso accion)
    {
        if (rol == RolUsuario.Odontologo) return NivelPermiso.Permitido;

        var guardado = await db.PermisosRol.AsNoTracking()
            .Where(p => p.ConsultorioId == consultorioId && p.Rol == rol && p.Modulo == modulo && p.Accion == accion)
            .Select(p => (NivelPermiso?)p.Nivel)
            .FirstOrDefaultAsync();

        return guardado ?? Inicial(modulo, accion);
    }

    public async Task<List<PermisoItemDto>> MatrizAsync(int consultorioId, RolUsuario rol)
    {
        if (rol == RolUsuario.Odontologo)
            return Combinaciones().Select(c => new PermisoItemDto(c.Modulo, c.Accion, NivelPermiso.Permitido)).ToList();

        var guardados = await db.PermisosRol.AsNoTracking()
            .Where(p => p.ConsultorioId == consultorioId && p.Rol == rol)
            .ToListAsync();

        return Combinaciones()
            .Select(c => new PermisoItemDto(c.Modulo, c.Accion,
                guardados.FirstOrDefault(g => g.Modulo == c.Modulo && g.Accion == c.Accion)?.Nivel
                ?? Inicial(c.Modulo, c.Accion)))
            .ToList();
    }

    /// <summary>Reemplaza la matriz del rol. Ignora combinaciones desconocidas y duplicadas.</summary>
    public async Task GuardarAsync(int consultorioId, RolUsuario rol, IEnumerable<PermisoItemDto> items)
    {
        var validas = Combinaciones().ToHashSet();
        var limpias = items
            .Where(i => validas.Contains((i.Modulo, i.Accion)))
            .DistinctBy(i => (i.Modulo, i.Accion))
            .Select(i => new PermisoRol
            {
                ConsultorioId = consultorioId,
                Rol = rol,
                Modulo = i.Modulo,
                Accion = i.Accion,
                Nivel = i.Nivel,
            })
            .ToList();

        await using var transaccion = await db.Database.BeginTransactionAsync();
        await db.PermisosRol.Where(p => p.ConsultorioId == consultorioId && p.Rol == rol).ExecuteDeleteAsync();
        db.PermisosRol.AddRange(limpias);
        await db.SaveChangesAsync();
        await transaccion.CommitAsync();
    }

    /// <summary>Guarda en el historial una acción hecha con permiso (Con acceso o Con registro).</summary>
    public async Task RegistrarActividadAsync(int consultorioId, int usuarioId, ModuloPermiso modulo, AccionPermiso accion, NivelPermiso nivel)
    {
        db.ActividadPermisos.Add(new ActividadPermiso
        {
            ConsultorioId = consultorioId,
            UsuarioId = usuarioId,
            Modulo = modulo,
            Accion = accion,
            Nivel = nivel,
            CreadoEn = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Últimas acciones de la secretaria, de la más reciente a la más antigua.</summary>
    public async Task<List<ActividadPermisoDto>> ActividadAsync(int consultorioId, int limite)
    {
        return await db.ActividadPermisos.AsNoTracking()
            .Include(a => a.Usuario)
            .Where(a => a.ConsultorioId == consultorioId)
            .OrderByDescending(a => a.CreadoEn)
            .Take(limite)
            .Select(a => new ActividadPermisoDto(a.Id, a.Usuario.Nombre, a.Usuario.Email,
                a.Modulo, a.Accion, a.Nivel, a.CreadoEn))
            .ToListAsync();
    }
}
