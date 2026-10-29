using Microsoft.EntityFrameworkCore;
using OdontoApp.Api.Models;

namespace OdontoApp.Api.Data;

public class OdontoDbContext(DbContextOptions<OdontoDbContext> options) : DbContext(options)
{
    public DbSet<Consultorio> Consultorios => Set<Consultorio>();
    public DbSet<Invitacion> Invitaciones => Set<Invitacion>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Turno> Turnos => Set<Turno>();
    public DbSet<Odontograma> Odontogramas => Set<Odontograma>();
    public DbSet<OdontogramaDiente> OdontogramaDientes => Set<OdontogramaDiente>();
    public DbSet<PermisoRol> PermisosRol => Set<PermisoRol>();
    public DbSet<ActividadPermiso> ActividadPermisos => Set<ActividadPermiso>();
    public DbSet<HistorialClinico> HistorialesClinicos => Set<HistorialClinico>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>();
    public DbSet<TrabajoLaboratorio> TrabajosLaboratorio => Set<TrabajoLaboratorio>();
    public DbSet<DiagnosticoAtencion> DiagnosticosAtencion => Set<DiagnosticoAtencion>();
    public DbSet<PracticaAtencion> PracticasAtencion => Set<PracticaAtencion>();
    public DbSet<Tarea> Tareas => Set<Tarea>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<TipoRegistroClinico>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<TipoConsulta>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<CaracterDiagnostico>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<PrioridadTarea>().HaveConversion<string>().HaveMaxLength(10);
        builder.Properties<MedioPago>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<TipoMovimiento>().HaveConversion<string>().HaveMaxLength(10);
        builder.Properties<CategoriaMovimiento>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<EstadoTrabajoLaboratorio>().HaveConversion<string>().HaveMaxLength(20);
        // Enums como texto: legible en la BD y compatible con columnas ENUM.
        builder.Properties<RolUsuario>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<EstadoTurno>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<EstadoCara>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<EstadoGeneralDiente>().HaveConversion<string>().HaveMaxLength(30);
        builder.Properties<ModuloPermiso>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<AccionPermiso>().HaveConversion<string>().HaveMaxLength(30);
        builder.Properties<NivelPermiso>().HaveConversion<string>().HaveMaxLength(20);
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Todas las columnas datetime se guardan en UTC (DateTime.UtcNow). MySQL no guarda el "Kind":
        // al leer se marca como UTC para que el JSON salga con "Z" y el navegador lo pase a hora local.
        var utc = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var utcNulable = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
            v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
        foreach (var prop in mb.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
        {
            if (prop.ClrType == typeof(DateTime)) prop.SetValueConverter(utc);
            else if (prop.ClrType == typeof(DateTime?)) prop.SetValueConverter(utcNulable);
        }

        // Consultorio (la "empresa") 1..N usuarios, 1..N invitaciones
        mb.Entity<Consultorio>();

        mb.Entity<Usuario>(e =>
        {
            // Un correo aparece una sola vez por consultorio; una cuenta de Google, una sola vez en toda la base.
            e.HasIndex(u => new { u.ConsultorioId, u.Email }).IsUnique();
            e.HasIndex(u => u.GoogleSub).IsUnique();
            e.HasOne(u => u.Consultorio).WithMany(c => c.Usuarios)
                .HasForeignKey(u => u.ConsultorioId).OnDelete(DeleteBehavior.Restrict);
        });

        // Invitación: el token se guarda hasheado, así que se busca por hash y es único.
        mb.Entity<Invitacion>(e =>
        {
            e.HasIndex(i => i.TokenHash).IsUnique();
            e.HasIndex(i => new { i.ConsultorioId, i.Email });
            e.HasOne(i => i.Consultorio).WithMany(c => c.Invitaciones)
                .HasForeignKey(i => i.ConsultorioId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.CreadaPor).WithMany()
                .HasForeignKey(i => i.CreadaPorId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Paciente>().HasIndex(p => p.Dni).IsUnique();

        // Turno: paciente 1..N, usuario 1..N
        mb.Entity<Turno>(e =>
        {
            e.HasIndex(t => new { t.Fecha, t.Hora });
            e.HasOne(t => t.Paciente).WithMany(p => p.Turnos)
                .HasForeignKey(t => t.PacienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Usuario).WithMany(u => u.Turnos)
                .HasForeignKey(t => t.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        // Odontograma: paciente (cliente_id) 1..N, usuario 1..N, dientes 1..N
        mb.Entity<Odontograma>(e =>
        {
            e.HasIndex(o => new { o.PacienteId, o.FechaRegistro });
            e.HasOne(o => o.Paciente).WithMany(p => p.Odontogramas)
                .HasForeignKey(o => o.PacienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.Usuario).WithMany(u => u.Odontogramas)
                .HasForeignKey(o => o.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(o => o.Dientes).WithOne(d => d.Odontograma)
                .HasForeignKey(d => d.OdontogramaId).OnDelete(DeleteBehavior.Cascade);
        });

        // Una pieza aparece una sola vez por odontograma.
        mb.Entity<OdontogramaDiente>()
            .HasIndex(d => new { d.OdontogramaId, d.NumeroDiente }).IsUnique();

        // Historial clínico: paciente 1..N, usuario 1..N, turno 0..1 -> N
        mb.Entity<HistorialClinico>(e =>
        {
            e.HasIndex(h => new { h.PacienteId, h.Fecha });
            e.HasOne(h => h.Paciente).WithMany(p => p.Historiales)
                .HasForeignKey(h => h.PacienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(h => h.Usuario).WithMany(u => u.Historiales)
                .HasForeignKey(h => h.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(h => h.Turno).WithMany(t => t.Historiales)
                .HasForeignKey(h => h.TurnoId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(h => h.Diagnosticos).WithOne(d => d.Historial)
                .HasForeignKey(d => d.HistorialId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(h => h.Practicas).WithOne(p => p.Historial)
                .HasForeignKey(p => p.HistorialId).OnDelete(DeleteBehavior.Cascade);
        });

        // Tarea: usuario que la creó 1..N
        mb.Entity<Tarea>(e =>
        {
            e.HasIndex(t => new { t.Completada, t.Vence });
            e.HasOne(t => t.Usuario).WithMany()
                .HasForeignKey(t => t.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        // Pago: paciente 1..N, usuario 1..N
        mb.Entity<Pago>(e =>
        {
            e.HasIndex(p => new { p.PacienteId, p.Fecha });
            e.HasOne(p => p.Paciente).WithMany(x => x.Pagos)
                .HasForeignKey(p => p.PacienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Usuario).WithMany()
                .HasForeignKey(p => p.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        // Movimiento de caja: 0..1 pago, 0..1 trabajo de laboratorio (relaciones 1:1 opcionales)
        mb.Entity<MovimientoCaja>(e =>
        {
            e.HasIndex(m => m.Fecha);
            e.HasOne(m => m.Usuario).WithMany()
                .HasForeignKey(m => m.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Pago).WithOne(p => p.Movimiento)
                .HasForeignKey<MovimientoCaja>(m => m.PagoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.TrabajoLaboratorio).WithOne(t => t.Movimiento)
                .HasForeignKey<MovimientoCaja>(m => m.TrabajoLaboratorioId).OnDelete(DeleteBehavior.Restrict);
        });

        // Trabajo de laboratorio: paciente 1..N, usuario 1..N
        mb.Entity<TrabajoLaboratorio>(e =>
        {
            e.HasIndex(t => new { t.Estado, t.FechaEnvio });
            e.HasOne(t => t.Paciente).WithMany(p => p.TrabajosLaboratorio)
                .HasForeignKey(t => t.PacienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Usuario).WithMany()
                .HasForeignKey(t => t.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
