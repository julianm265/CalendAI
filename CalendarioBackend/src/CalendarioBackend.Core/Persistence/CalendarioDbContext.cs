using Microsoft.EntityFrameworkCore;

namespace CalendarioBackend.Core.Persistence;

public sealed class CalendarioDbContext(DbContextOptions<CalendarioDbContext> options) : DbContext(options)
{
    public DbSet<EquipoRow> Equipos => Set<EquipoRow>();
    public DbSet<CalendarioRow> Calendarios => Set<CalendarioRow>();
    public DbSet<ColaboradorRow> Colaboradores => Set<ColaboradorRow>();
    public DbSet<MembresiaRow> Membresias => Set<MembresiaRow>();
    public DbSet<InvitacionRow> Invitaciones => Set<InvitacionRow>();
    public DbSet<EventoRow> Eventos => Set<EventoRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EquipoRow>(e =>
        {
            e.ToTable("equipos");
            e.HasKey(row => row.Id);
            e.Property(row => row.Id).HasColumnName("id");
            e.Property(row => row.Nombre).HasColumnName("nombre").HasMaxLength(120);
            e.Property(row => row.EsPersonal).HasColumnName("es_personal");
            e.Property(row => row.CreadoPorId).HasColumnName("creado_por");
            e.Property(row => row.CreadoEn).HasColumnName("creado_en");
        });

        modelBuilder.Entity<CalendarioRow>(e =>
        {
            e.ToTable("calendarios");
            e.HasKey(row => row.Id);
            e.Property(row => row.Id).HasColumnName("id");
            e.Property(row => row.EquipoId).HasColumnName("equipo_id");
            e.HasOne<EquipoRow>().WithMany().HasForeignKey(row => row.EquipoId).IsRequired();
        });

        modelBuilder.Entity<ColaboradorRow>(e =>
        {
            e.ToTable("colaboradores");
            e.HasKey(row => row.Id);
            e.Property(row => row.Id).HasColumnName("id");
            e.Property(row => row.Usuario).HasColumnName("usuario").HasMaxLength(100);
            e.Property(row => row.ContraseñaHash).HasColumnName("contrasena_hash").HasMaxLength(255);
        });

        // Un usuario puede pertenecer a muchos equipos; el rol vive en la membresía.
        modelBuilder.Entity<MembresiaRow>(e =>
        {
            e.ToTable("membresias");
            e.HasKey(row => new { row.EquipoId, row.ColaboradorId });
            e.Property(row => row.EquipoId).HasColumnName("equipo_id");
            e.Property(row => row.ColaboradorId).HasColumnName("colaborador_id");
            e.Property(row => row.Rol).HasColumnName("rol").HasMaxLength(20);
            e.Property(row => row.UnidoEn).HasColumnName("unido_en");
            e.HasOne<EquipoRow>().WithMany().HasForeignKey(row => row.EquipoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ColaboradorRow>().WithMany().HasForeignKey(row => row.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(row => row.ColaboradorId);
        });

        modelBuilder.Entity<InvitacionRow>(e =>
        {
            e.ToTable("invitaciones");
            e.HasKey(row => row.Id);
            e.Property(row => row.Id).HasColumnName("id");
            e.Property(row => row.EquipoId).HasColumnName("equipo_id");
            e.Property(row => row.InvitadoId).HasColumnName("invitado_id");
            e.Property(row => row.InvitadoPorId).HasColumnName("invitado_por_id");
            e.Property(row => row.Estado).HasColumnName("estado").HasMaxLength(20);
            e.Property(row => row.CreadaEn).HasColumnName("creada_en");
            e.Property(row => row.RespondidaEn).HasColumnName("respondida_en");
            e.HasOne<EquipoRow>().WithMany().HasForeignKey(row => row.EquipoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ColaboradorRow>().WithMany().HasForeignKey(row => row.InvitadoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ColaboradorRow>().WithMany().HasForeignKey(row => row.InvitadoPorId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(row => new { row.InvitadoId, row.Estado });
        });

        modelBuilder.Entity<EventoRow>(e =>
        {
            e.ToTable("eventos");
            e.HasKey(row => row.Id);
            e.Property(row => row.Id).HasColumnName("id");
            e.Property(row => row.EquipoId).HasColumnName("equipo_id");
            e.Property(row => row.Fecha).HasColumnName("fecha");
            e.Property(row => row.Hora).HasColumnName("hora");
            e.Property(row => row.Nombre).HasColumnName("nombre").HasMaxLength(160);
            e.Property(row => row.Lugar).HasColumnName("lugar").HasMaxLength(200);
            e.Property(row => row.Descripcion).HasColumnName("descripcion").HasMaxLength(500);
            e.Property(row => row.ColaboradorOrganizadorId).HasColumnName("colaborador_organizador_id");
            e.HasOne<EquipoRow>().WithMany().HasForeignKey(row => row.EquipoId);
            e.HasOne<ColaboradorRow>().WithMany().HasForeignKey(row => row.ColaboradorOrganizadorId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(row => new { row.EquipoId, row.Fecha, row.Hora }).IsUnique();
        });
    }
}

public sealed class EquipoRow
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = "";
    public bool EsPersonal { get; set; }
    public Guid? CreadoPorId { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

public sealed class CalendarioRow
{
    public Guid Id { get; set; }
    public Guid EquipoId { get; set; }
}

public sealed class ColaboradorRow
{
    public Guid Id { get; set; }
    public string Usuario { get; set; } = "";
    public string ContraseñaHash { get; set; } = "";
}

public sealed class MembresiaRow
{
    public Guid EquipoId { get; set; }
    public Guid ColaboradorId { get; set; }
    public string Rol { get; set; } = "miembro";
    public DateTime UnidoEn { get; set; } = DateTime.UtcNow;
}

public sealed class InvitacionRow
{
    public Guid Id { get; set; }
    public Guid EquipoId { get; set; }
    public Guid InvitadoId { get; set; }
    public Guid InvitadoPorId { get; set; }
    public string Estado { get; set; } = "pendiente";
    public DateTime CreadaEn { get; set; } = DateTime.UtcNow;
    public DateTime? RespondidaEn { get; set; }
}

public sealed class EventoRow
{
    public Guid Id { get; set; }
    public Guid EquipoId { get; set; }
    public DateOnly Fecha { get; set; }
    public TimeOnly Hora { get; set; }
    public string Nombre { get; set; } = "";
    public string? Lugar { get; set; }
    public string? Descripcion { get; set; }
    public Guid? ColaboradorOrganizadorId { get; set; }
}
