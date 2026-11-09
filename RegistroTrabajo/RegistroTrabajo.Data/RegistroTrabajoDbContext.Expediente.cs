using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data.Entities;

namespace RegistroTrabajo.Data;

// Bloque 14 (script 017): expediente profesional (habilidades, estudios y currículum).
public partial class RegistroTrabajoDbContext
{
    public DbSet<Habilidad> Habilidades => Set<Habilidad>();
    public DbSet<TrabajadorHabilidad> TrabajadoresHabilidad => Set<TrabajadorHabilidad>();
    public DbSet<TrabajadorEstudio> TrabajadoresEstudio => Set<TrabajadorEstudio>();
    public DbSet<CurriculumTrabajador> CurriculosTrabajador => Set<CurriculumTrabajador>();

    private void ConfigurarExpediente(ModelBuilder mb)
    {
        mb.Entity<Habilidad>(e =>
        {
            e.ToTable("Habilidades");
            e.HasKey(x => x.HabilidadId);
            e.Property(x => x.HabilidadId).HasDefaultValueSql("(newsequentialid())");
            e.Property(x => x.Nombre).HasMaxLength(80);
            e.Property(x => x.NombreNormalizado).HasMaxLength(80);
            e.Property(x => x.Activa).HasDefaultValue(true);
            e.Property(x => x.FechaCreacionUtc).HasPrecision(0).HasDefaultValueSql("(sysutcdatetime())");
            e.HasAlternateKey(x => new { x.EmpresaId, x.HabilidadId }).HasName("UQ_Habilidades_Empresa_Id");
            e.HasIndex(x => new { x.EmpresaId, x.NombreNormalizado }).IsUnique().HasDatabaseName("UQ_Habilidades_Empresa_Nombre");
            e.HasOne<Empresa>().WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Habilidades_Empresas");
        });

        mb.Entity<TrabajadorHabilidad>(e =>
        {
            e.ToTable("TrabajadoresHabilidad");
            e.HasKey(x => x.TrabajadorHabilidadId);
            e.Property(x => x.TrabajadorHabilidadId).HasDefaultValueSql("(newsequentialid())");
            e.Property(x => x.Notas).HasMaxLength(300);
            e.Property(x => x.MotivoRechazo).HasMaxLength(300);
            e.Property(x => x.Estado).HasDefaultValue((byte)1);
            e.Property(x => x.ResueltoUtc).HasPrecision(0);
            e.Property(x => x.FechaCreacionUtc).HasPrecision(0).HasDefaultValueSql("(sysutcdatetime())");
            e.Property(x => x.FechaActualizacionUtc).HasPrecision(0).HasDefaultValueSql("(sysutcdatetime())");
            e.HasIndex(x => new { x.EmpresaId, x.TrabajadorId, x.HabilidadId }).IsUnique().HasDatabaseName("UQ_TrabHab_Trabajador_Habilidad");
            e.HasIndex(x => new { x.EmpresaId, x.HabilidadId, x.Nivel }).HasDatabaseName("IX_TrabHab_Busqueda");
            e.HasOne(x => x.Trabajador).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.TrabajadorId })
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TrabHab_Trabajadores");
            e.HasOne(x => x.Habilidad).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.HabilidadId })
                .HasForeignKey(d => new { d.EmpresaId, d.HabilidadId })
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TrabHab_Habilidades");
            e.HasOne<Usuario>().WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.ResueltoPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TrabHab_Resolutor");
        });

        mb.Entity<TrabajadorEstudio>(e =>
        {
            e.ToTable("TrabajadoresEstudio");
            e.HasKey(x => x.EstudioId);
            e.Property(x => x.EstudioId).HasDefaultValueSql("(newsequentialid())");
            e.Property(x => x.Titulo).HasMaxLength(200);
            e.Property(x => x.Institucion).HasMaxLength(200);
            e.Property(x => x.Notas).HasMaxLength(500);
            e.Property(x => x.MotivoRechazo).HasMaxLength(300);
            e.Property(x => x.EnCurso).HasDefaultValue(false);
            e.Property(x => x.Estado).HasDefaultValue((byte)1);
            e.Property(x => x.ResueltoUtc).HasPrecision(0);
            e.Property(x => x.FechaCreacionUtc).HasPrecision(0).HasDefaultValueSql("(sysutcdatetime())");
            e.Property(x => x.FechaActualizacionUtc).HasPrecision(0).HasDefaultValueSql("(sysutcdatetime())");
            e.HasIndex(x => new { x.EmpresaId, x.TrabajadorId, x.Estado }).HasDatabaseName("IX_TrabEst_Trabajador");
            e.HasIndex(x => new { x.EmpresaId, x.Tipo, x.Estado }).HasDatabaseName("IX_TrabEst_Tipo");
            e.HasOne(x => x.Trabajador).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.TrabajadorId })
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TrabEst_Trabajadores");
            e.HasOne<Usuario>().WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.ResueltoPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TrabEst_Resolutor");
        });

        mb.Entity<CurriculumTrabajador>(e =>
        {
            e.ToTable("CurriculosTrabajador");
            e.HasKey(x => x.TrabajadorId);
            e.Property(x => x.NombreArchivo).HasMaxLength(200);
            e.Property(x => x.TipoContenido).HasMaxLength(100);
            e.Property(x => x.HashSha256).HasMaxLength(64).IsFixedLength().IsUnicode(false);
            e.Property(x => x.FechaActualizacionUtc).HasPrecision(0).HasDefaultValueSql("(sysutcdatetime())");
            e.HasOne(x => x.Trabajador).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.TrabajadorId })
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Curriculos_Trabajadores");
        });
    }
}
