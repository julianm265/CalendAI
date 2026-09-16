using CalendarioBackend.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CalendarioBackend.Core.Persistence;

public sealed class CalendarioDbContext(DbContextOptions<CalendarioDbContext> options) : DbContext(options)
{
    public DbSet<EquipoRow> Equipos => Set<EquipoRow>();
    public DbSet<CalendarioRow> Calendarios => Set<CalendarioRow>();
    public DbSet<ColaboradorRow> Colaboradores => Set<ColaboradorRow>();
    public DbSet<EventoRow> Eventos => Set<EventoRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EquipoRow>().ToTable("equipos");
        modelBuilder.Entity<EquipoRow>().HasKey(row => row.Id);
        modelBuilder.Entity<EquipoRow>().Property(row => row.Id).HasColumnName("id");
        modelBuilder.Entity<EquipoRow>().Property(row => row.Nombre).HasColumnName("nombre");
        modelBuilder.Entity<EquipoRow>().HasIndex(row => row.Nombre).IsUnique();
        modelBuilder.Entity<CalendarioRow>().ToTable("calendarios");
        modelBuilder.Entity<CalendarioRow>().HasKey(row => row.Id);
        modelBuilder.Entity<CalendarioRow>().Property(row => row.Id).HasColumnName("id");
        modelBuilder.Entity<CalendarioRow>().Property(row => row.EquipoId).HasColumnName("equipo_id");
        modelBuilder.Entity<CalendarioRow>().HasOne<EquipoRow>().WithMany().HasForeignKey(row => row.EquipoId).IsRequired();
        modelBuilder.Entity<ColaboradorRow>().ToTable("colaboradores");
        modelBuilder.Entity<ColaboradorRow>().HasKey(row => row.Id);
        modelBuilder.Entity<ColaboradorRow>().Property(row => row.Id).HasColumnName("id");
        modelBuilder.Entity<ColaboradorRow>().Property(row => row.Usuario).HasColumnName("usuario");
        modelBuilder.Entity<ColaboradorRow>().Property(row => row.ContraseñaHash).HasColumnName("contrasena_hash");
        modelBuilder.Entity<ColaboradorRow>().Property(row => row.EquipoId).HasColumnName("equipo_id");
        modelBuilder.Entity<ColaboradorRow>().HasIndex(row => row.Usuario);
        modelBuilder.Entity<ColaboradorRow>().HasOne<EquipoRow>().WithMany().HasForeignKey(row => row.EquipoId).IsRequired(false);
        modelBuilder.Entity<EventoRow>().ToTable("eventos");
        modelBuilder.Entity<EventoRow>().HasKey(row => row.Id);
        modelBuilder.Entity<EventoRow>().Property(row => row.Id).HasColumnName("id");
        modelBuilder.Entity<EventoRow>().Property(row => row.EquipoId).HasColumnName("equipo_id");
        modelBuilder.Entity<EventoRow>().Property(row => row.Fecha).HasColumnName("fecha");
        modelBuilder.Entity<EventoRow>().Property(row => row.Hora).HasColumnName("hora");
        modelBuilder.Entity<EventoRow>().Property(row => row.Nombre).HasColumnName("nombre");
        modelBuilder.Entity<EventoRow>().Property(row => row.Lugar).HasColumnName("lugar");
        modelBuilder.Entity<EventoRow>().Property(row => row.Descripcion).HasColumnName("descripcion");
        modelBuilder.Entity<EventoRow>().Property(row => row.ColaboradorOrganizadorId).HasColumnName("colaborador_organizador_id");
        modelBuilder.Entity<EventoRow>().HasOne<EquipoRow>().WithMany().HasForeignKey(row => row.EquipoId);
        modelBuilder.Entity<EventoRow>().HasOne<ColaboradorRow>().WithMany().HasForeignKey(row => row.ColaboradorOrganizadorId).IsRequired(false);
        modelBuilder.Entity<EventoRow>().HasIndex(row => new { row.EquipoId, row.Fecha, row.Hora }).IsUnique();
    }
}

public sealed class EquipoRow
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = "";
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
    public Guid? EquipoId { get; set; }
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