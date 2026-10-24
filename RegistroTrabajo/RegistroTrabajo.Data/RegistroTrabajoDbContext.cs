using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data.Entities;

namespace RegistroTrabajo.Data;

public partial class RegistroTrabajoDbContext : DbContext
{
    public RegistroTrabajoDbContext(DbContextOptions<RegistroTrabajoDbContext> options) : base(options) { }

    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<ConfiguracionEmpresa> ConfiguracionEmpresa => Set<ConfiguracionEmpresa>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<PermisoSistema> PermisosSistema => Set<PermisoSistema>();
    public DbSet<UsuarioRol> UsuarioRoles => Set<UsuarioRol>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();
    public DbSet<SesionUsuario> SesionesUsuario => Set<SesionUsuario>();
    public DbSet<HistorialAcceso> HistorialAcceso => Set<HistorialAcceso>();
    public DbSet<Auditoria> Auditoria => Set<Auditoria>();
    public DbSet<TokenConfirmacionCorreo> TokensConfirmacionCorreo => Set<TokenConfirmacionCorreo>();
    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Empresa>(e =>
        {
            e.ToTable("Empresas");
            e.HasKey(x => x.EmpresaId);
            e.Property(x => x.Nombre).HasMaxLength(160);
            e.Property(x => x.NombreComercial).HasMaxLength(160);
            e.Property(x => x.RFC).HasMaxLength(13);
            e.Property(x => x.CorreoContacto).HasMaxLength(254);
            e.Property(x => x.TelefonoContacto).HasMaxLength(30);
            e.Property(x => x.LogoUrl).HasMaxLength(500);
            e.Property(x => x.ColorPrimario).HasMaxLength(7).IsFixedLength().IsUnicode(false);
            e.Property(x => x.ColorSecundario).HasMaxLength(7).IsFixedLength().IsUnicode(false);
            e.HasIndex(x => x.RFC).IsUnique().HasFilter("[RFC] IS NOT NULL").HasDatabaseName("UX_Empresas_RFC");
            e.HasIndex(x => x.EsPlataforma).IsUnique().HasFilter("[EsPlataforma] = 1").HasDatabaseName("UX_Empresas_Plataforma");
            e.HasOne(x => x.Configuracion).WithOne(c => c.Empresa)
                .HasForeignKey<ConfiguracionEmpresa>(c => c.EmpresaId)
                .HasConstraintName("FK_ConfiguracionEmpresa_Empresas");
        });

        mb.Entity<ConfiguracionEmpresa>(e =>
        {
            e.ToTable("ConfiguracionEmpresa");
            e.HasKey(x => x.EmpresaId);
            e.Property(x => x.ZonaHorariaId).HasMaxLength(100);
            e.Property(x => x.Cultura).HasMaxLength(20);
            e.Property(x => x.FormatoFecha).HasMaxLength(30);
            e.Property(x => x.FormatoHora).HasMaxLength(20);
        });

        mb.Entity<Usuario>(e =>
        {
            e.ToTable("Usuarios");
            e.HasKey(x => x.UsuarioId);
            e.Property(x => x.Correo).HasMaxLength(254);
            e.Property(x => x.CorreoNormalizado).HasMaxLength(254);
            e.Property(x => x.Nombre).HasMaxLength(100);
            e.Property(x => x.Apellido).HasMaxLength(150);
            e.Property(x => x.HashContrasena).HasMaxLength(500);
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                e.Property(x => x.RowVersion).HasDefaultValueSql("(randomblob(8))"); // SQLite no tiene rowversion (solo pruebas)
            else
                e.Property(x => x.RowVersion).IsRowVersion();
            e.HasAlternateKey(x => new { x.EmpresaId, x.UsuarioId }).HasName("UQ_Usuarios_Empresa_Usuario");
            e.HasIndex(x => x.CorreoNormalizado).IsUnique().HasDatabaseName("UX_Usuarios_CorreoNormalizado");
            e.HasIndex(x => new { x.EmpresaId, x.Estado, x.Nombre, x.Apellido, x.Correo })
                .HasDatabaseName("IX_Usuarios_Empresa_Estado");
            e.HasOne(x => x.Empresa).WithMany(x => x.Usuarios)
                .HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Usuarios_Empresas");
        });

        mb.Entity<Rol>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(x => x.RolId);
            e.Property(x => x.Nombre).HasMaxLength(80);
            e.Property(x => x.Descripcion).HasMaxLength(250);
            e.HasAlternateKey(x => new { x.EmpresaId, x.RolId }).HasName("UQ_Roles_Empresa_Rol");
            e.HasIndex(x => new { x.EmpresaId, x.Nombre }).IsUnique().HasDatabaseName("UQ_Roles_Empresa_Nombre");   // índice único: el nombre del rol se puede editar
            e.HasOne(x => x.Empresa).WithMany(x => x.Roles)
                .HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Roles_Empresas");
        });

        mb.Entity<PermisoSistema>(e =>
        {
            e.ToTable("PermisosSistema");
            e.HasKey(x => x.PermisoId);
            e.Property(x => x.Codigo).HasMaxLength(150);
            e.Property(x => x.Modulo).HasMaxLength(80);
            e.Property(x => x.Accion).HasMaxLength(80);
            e.Property(x => x.Descripcion).HasMaxLength(250);
            e.HasIndex(x => x.Codigo).IsUnique().HasDatabaseName("UQ_PermisosSistema_Codigo");
            e.HasIndex(x => new { x.Modulo, x.Accion }).IsUnique().HasDatabaseName("UQ_PermisosSistema_Modulo_Accion");
        });

        mb.Entity<UsuarioRol>(e =>
        {
            e.ToTable("UsuarioRoles");
            e.HasKey(x => new { x.UsuarioId, x.RolId }).HasName("PK_UsuarioRoles");
            e.HasIndex(x => new { x.EmpresaId, x.RolId }).HasDatabaseName("IX_UsuarioRoles_Empresa_Rol");
            e.HasOne(x => x.Usuario).WithMany(x => x.UsuarioRoles)
                .HasForeignKey(x => new { x.EmpresaId, x.UsuarioId })
                .HasPrincipalKey(x => new { x.EmpresaId, x.UsuarioId })
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_UsuarioRoles_UsuarioEmpresa");
            e.HasOne(x => x.Rol).WithMany(x => x.UsuarioRoles)
                .HasForeignKey(x => new { x.EmpresaId, x.RolId })
                .HasPrincipalKey(x => new { x.EmpresaId, x.RolId })
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_UsuarioRoles_RolEmpresa");
            e.HasOne(x => x.AsignadoPor).WithMany()
                .HasForeignKey(x => x.AsignadoPorUsuarioId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_UsuarioRoles_AsignadoPor");
        });

        mb.Entity<RolPermiso>(e =>
        {
            e.ToTable("RolPermisos");
            e.HasKey(x => new { x.RolId, x.PermisoId }).HasName("PK_RolPermisos");
            e.HasOne(x => x.Rol).WithMany(x => x.RolPermisos)
                .HasForeignKey(x => new { x.EmpresaId, x.RolId })
                .HasPrincipalKey(x => new { x.EmpresaId, x.RolId })
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RolPermisos_RolEmpresa");
            e.HasOne(x => x.Permiso).WithMany(x => x.RolPermisos)
                .HasForeignKey(x => x.PermisoId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RolPermisos_Permiso");
            e.HasOne(x => x.ActualizadoPor).WithMany()
                .HasForeignKey(x => x.ActualizadoPorUsuarioId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_RolPermisos_ActualizadoPor");
        });

        mb.Entity<SesionUsuario>(e =>
        {
            e.ToTable("SesionesUsuario");
            e.HasKey(x => x.SesionId);
            e.Property(x => x.TokenRefreshHash).HasMaxLength(32);
            e.Property(x => x.Dispositivo).HasMaxLength(200);
            e.Property(x => x.UserAgent).HasMaxLength(500);
            e.Property(x => x.DireccionIp).HasMaxLength(45).IsUnicode(false);
            e.Property(x => x.MotivoRevocacion).HasMaxLength(200);
            e.HasIndex(x => x.TokenRefreshHash).IsUnique().HasDatabaseName("UX_Sesiones_TokenRefreshHash");
            e.HasIndex(x => new { x.EmpresaId, x.UsuarioId, x.ExpiraUtc }).HasFilter("[RevocadaUtc] IS NULL").HasDatabaseName("IX_Sesiones_Usuario_Activas");
            e.HasOne(x => x.Usuario).WithMany(x => x.Sesiones)
                .HasForeignKey(x => new { x.EmpresaId, x.UsuarioId })
                .HasPrincipalKey(x => new { x.EmpresaId, x.UsuarioId })
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Sesiones_UsuarioEmpresa");
        });

        mb.Entity<HistorialAcceso>(e =>
        {
            e.ToTable("HistorialAcceso");
            e.HasKey(x => x.HistorialAccesoId);
            e.Property(x => x.CorreoIntentado).HasMaxLength(254);
            e.Property(x => x.DireccionIp).HasMaxLength(45).IsUnicode(false);
            e.Property(x => x.UserAgent).HasMaxLength(500);
            e.Property(x => x.Detalle).HasMaxLength(300);
            e.HasIndex(x => x.FechaUtc).HasDatabaseName("IX_HistorialAcceso_Fecha");
            e.HasIndex(x => new { x.EmpresaId, x.FechaUtc }).HasDatabaseName("IX_HistorialAcceso_Empresa_Fecha");
            e.HasIndex(x => new { x.UsuarioId, x.FechaUtc }).HasFilter("[UsuarioId] IS NOT NULL").HasDatabaseName("IX_HistorialAcceso_Usuario_Fecha");
            e.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_HistorialAcceso_Empresa");
            e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_HistorialAcceso_Usuario");
        });

        mb.Entity<Auditoria>(e =>
        {
            e.ToTable("Auditoria");
            e.HasKey(x => x.AuditoriaId);
            e.Property(x => x.Modulo).HasMaxLength(80);
            e.Property(x => x.Entidad).HasMaxLength(120);
            e.Property(x => x.EntidadId).HasMaxLength(100);
            e.Property(x => x.Accion).HasMaxLength(40);
            e.Property(x => x.DireccionIp).HasMaxLength(45).IsUnicode(false);
            e.HasIndex(x => new { x.EmpresaId, x.FechaUtc }).HasDatabaseName("IX_Auditoria_Empresa_Fecha");
            e.HasIndex(x => new { x.EmpresaId, x.Entidad, x.EntidadId, x.FechaUtc }).HasDatabaseName("IX_Auditoria_Entidad");
            e.HasIndex(x => new { x.UsuarioId, x.FechaUtc }).HasFilter("[UsuarioId] IS NOT NULL").HasDatabaseName("IX_Auditoria_Usuario_Fecha");
            e.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Auditoria_Empresa");
            e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Auditoria_Usuario");
        });

        mb.Entity<TokenConfirmacionCorreo>(e =>
        {
            e.ToTable("TokensConfirmacionCorreo");
            e.HasKey(x => x.TokenConfirmacionId);
            e.Property(x => x.TokenHash).HasMaxLength(32);
            e.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("UX_TokenConf_Hash");
            e.HasIndex(x => new { x.EmpresaId, x.UsuarioId }).HasDatabaseName("IX_TokenConf_Usuario");
            e.HasOne(x => x.Usuario).WithMany()
                .HasForeignKey(x => new { x.EmpresaId, x.UsuarioId })
                .HasPrincipalKey(x => new { x.EmpresaId, x.UsuarioId })
                .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TokenConf_Usuario");
        });
        ConfigurarLaboral(mb);
    }
}
