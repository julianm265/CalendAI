using Microsoft.EntityFrameworkCore;

namespace CalendarioBackend.Core.Persistence;

public sealed class CalendaiDbContext : DbContext
{
    public CalendaiDbContext(DbContextOptions<CalendaiDbContext> options) : base(options)
    {
    }

    public DbSet<EquipoRow> Equipos => Set<EquipoRow>();
    public DbSet<CalendarioRow> Calendarios => Set<CalendarioRow>();
    public DbSet<ColaboradorRow> Colaboradores => Set<ColaboradorRow>();
    public DbSet<EventoRow> Eventos => Set<EventoRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EquipoRow>(entity =>
        {
            entity.ToTable("equipos");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.Nombre).HasColumnName("nombre").HasMaxLength(120).IsRequired();
            entity.HasIndex(row => row.Nombre).IsUnique();
        });

        modelBuilder.Entity<CalendarioRow>(entity =>
        {
            entity.ToTable("calendarios");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.EquipoId).HasColumnName("equipo_id");
            entity.HasIndex(row => row.EquipoId).IsUnique();
            entity.HasOne<EquipoRow>().WithOne().HasForeignKey<CalendarioRow>(row => row.EquipoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ColaboradorRow>(entity =>
        {
            entity.ToTable("colaboradores");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.EquipoId).HasColumnName("equipo_id");
            entity.Property(row => row.Usuario).HasColumnName("usuario").HasMaxLength(100).IsRequired();
            entity.Property(row => row.ContrasenaHash).HasColumnName("contrasena_hash").HasMaxLength(255).IsRequired();
            entity.HasIndex(row => new { row.EquipoId, row.Usuario }).IsUnique();
            entity.HasOne<EquipoRow>().WithMany().HasForeignKey(row => row.EquipoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventoRow>(entity =>
        {
            entity.ToTable("eventos");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.EquipoId).HasColumnName("equipo_id");
            entity.Property(row => row.ColaboradorOrganizadorId).HasColumnName("colaborador_organizador_id");
            entity.Property(row => row.Fecha).HasColumnName("fecha").HasColumnType("date");
            entity.Property(row => row.Nombre).HasColumnName("nombre").HasMaxLength(160).IsRequired();
            entity.Property(row => row.Hora).HasColumnName("hora").HasColumnType("time");
            entity.Property(row => row.Lugar).HasColumnName("lugar").HasMaxLength(200);
            entity.Property(row => row.Descripcion).HasColumnName("descripcion");
            entity.HasIndex(row => new { row.EquipoId, row.Fecha, row.Hora }).IsUnique();
            entity.HasOne<EquipoRow>().WithMany().HasForeignKey(row => row.EquipoId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ColaboradorRow>().WithMany().HasForeignKey(row => row.ColaboradorOrganizadorId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}

public sealed class EquipoRow
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public sealed class CalendarioRow
{
    public Guid Id { get; set; }
    public Guid EquipoId { get; set; }
}

public sealed class ColaboradorRow
{
    public Guid Id { get; set; }
    public Guid EquipoId { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string ContrasenaHash { get; set; } = string.Empty;
}

public sealed class EventoRow
{
    public Guid Id { get; set; }
    public Guid EquipoId { get; set; }
    public Guid? ColaboradorOrganizadorId { get; set; }
    public DateOnly Fecha { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public TimeOnly Hora { get; set; }
    public string? Lugar { get; set; }
    public string? Descripcion { get; set; }
}
