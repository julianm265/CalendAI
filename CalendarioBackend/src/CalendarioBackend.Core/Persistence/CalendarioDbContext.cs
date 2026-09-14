using CalendarioBackend.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CalendarioBackend.Core.Persistence;

public sealed class CalendarioDbContext(DbContextOptions<CalendarioDbContext> options) : DbContext(options)
{
    public DbSet<EquipoRow> Equipos => Set<EquipoRow>();
    public DbSet<ColaboradorRow> Colaboradores => Set<ColaboradorRow>();
    public DbSet<EventoRow> Eventos => Set<EventoRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EquipoRow>().HasKey(row => row.Id);
        modelBuilder.Entity<EquipoRow>().HasIndex(row => row.Nombre).IsUnique();
        modelBuilder.Entity<ColaboradorRow>().HasKey(row => row.Id);
        modelBuilder.Entity<ColaboradorRow>().HasIndex(row => row.Usuario).IsUnique();
        modelBuilder.Entity<ColaboradorRow>().HasOne<EquipoRow>().WithMany().HasForeignKey(row => row.EquipoId);
        modelBuilder.Entity<EventoRow>().HasKey(row => row.Id);
        modelBuilder.Entity<EventoRow>().HasOne<EquipoRow>().WithMany().HasForeignKey(row => row.EquipoId);
        modelBuilder.Entity<EventoRow>().HasIndex(row => new { row.EquipoId, row.Fecha, row.Hora }).IsUnique();
    }
}

public sealed class EquipoRow
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = "";
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