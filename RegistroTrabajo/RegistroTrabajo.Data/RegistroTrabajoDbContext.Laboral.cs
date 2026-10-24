using Microsoft.EntityFrameworkCore;
using RegistroTrabajo.Data.Entities;

namespace RegistroTrabajo.Data;

// Bloque 2: tablas laborales (trabajadores, asistencia, horarios, permisos laborales, proyectos y tareas, etc.).
// Generado a partir del esquema real de SQL Server (EF Core scaffold) y adaptado a las convenciones de este proyecto:
// entidades en singular, sin eliminaciones en cascada (NO ACTION) y sin navegaciones inversas hacia las entidades del Bloque 1.
public partial class RegistroTrabajoDbContext
{
    public DbSet<AprobacionPermiso> AprobacionesPermiso => Set<AprobacionPermiso>();
    public DbSet<AsignacionHorario> AsignacionesHorario => Set<AsignacionHorario>();
    public DbSet<AsignacionProyecto> AsignacionesProyecto => Set<AsignacionProyecto>();
    public DbSet<AsignacionTarea> AsignacionesTarea => Set<AsignacionTarea>();
    public DbSet<Borrador> Borradores => Set<Borrador>();
    public DbSet<Comentario> Comentarios => Set<Comentario>();
    public DbSet<ContactoEmergencia> ContactosEmergencia => Set<ContactoEmergencia>();
    public DbSet<CorreccionAsistencia> CorreccionesAsistencia => Set<CorreccionAsistencia>();
    public DbSet<DefinicionReporte> DefinicionesReporte => Set<DefinicionReporte>();
    public DbSet<Departamento> Departamentos => Set<Departamento>();
    public DbSet<DescansoTrabajador> DescansosTrabajador => Set<DescansoTrabajador>();
    public DbSet<DiaFestivo> DiasFestivos => Set<DiaFestivo>();
    public DbSet<ErrorTecnico> ErroresTecnicos => Set<ErrorTecnico>();
    public DbSet<EstadoProyecto> EstadosProyecto => Set<EstadoProyecto>();
    public DbSet<EventoEmpresa> EventosEmpresa => Set<EventoEmpresa>();
    public DbSet<HistorialAvanceTarea> HistorialAvanceTarea => Set<HistorialAvanceTarea>();
    public DbSet<HistorialEstadoProyecto> HistorialEstadoProyecto => Set<HistorialEstadoProyecto>();
    public DbSet<HistorialLaboral> HistorialLaboral => Set<HistorialLaboral>();
    public DbSet<HorarioDetalleDia> HorarioDetalleDia => Set<HorarioDetalleDia>();
    public DbSet<HorarioLaboral> HorariosLaborales => Set<HorarioLaboral>();
    public DbSet<MencionComentario> MencionesComentario => Set<MencionComentario>();
    public DbSet<MovimientoSaldoPermiso> MovimientosSaldoPermiso => Set<MovimientoSaldoPermiso>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<OperacionRespaldo> OperacionesRespaldo => Set<OperacionRespaldo>();
    public DbSet<PreferenciaNotificacion> PreferenciasNotificacion => Set<PreferenciaNotificacion>();
    public DbSet<PreferenciaUsuario> PreferenciasUsuario => Set<PreferenciaUsuario>();
    public DbSet<Proyecto> Proyectos => Set<Proyecto>();
    public DbSet<Puesto> Puestos => Set<Puesto>();
    public DbSet<RegistroAsistencia> RegistrosAsistencia => Set<RegistroAsistencia>();
    public DbSet<ReglaPermiso> ReglasPermiso => Set<ReglaPermiso>();
    public DbSet<SaldoPermiso> SaldosPermiso => Set<SaldoPermiso>();
    public DbSet<SolicitudCambioPerfil> SolicitudesCambioPerfil => Set<SolicitudCambioPerfil>();
    public DbSet<SolicitudCancelacionPermiso> SolicitudesCancelacionPermiso => Set<SolicitudCancelacionPermiso>();
    public DbSet<SolicitudPermiso> SolicitudesPermiso => Set<SolicitudPermiso>();
    public DbSet<Tarea> Tareas => Set<Tarea>();
    public DbSet<TipoPermiso> TiposPermiso => Set<TipoPermiso>();
    public DbSet<TokenRecuperacionContrasena> TokensRecuperacionContrasena => Set<TokenRecuperacionContrasena>();
    public DbSet<Trabajador> Trabajadores => Set<Trabajador>();
    public DbSet<TrabajadorSupervisor> TrabajadorSupervisores => Set<TrabajadorSupervisor>();
    public DbSet<Turno> Turnos => Set<Turno>();

    private void ConfigurarLaboral(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AprobacionPermiso>(entity =>
        {
            entity.ToTable("AprobacionesPermiso");

            entity.HasKey(e => e.AprobacionPermisoId).HasName("PK__Aprobaci__A0BA9F8EE449FC98");

            entity.Property(e => e.Comentario).HasMaxLength(1000);
            entity.Property(e => e.FechaDecisionUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Nivel).HasDefaultValue((short)1);

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.RevisorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__AprobacionesPerm__54CB950F");

            entity.HasOne(d => d.SolicitudPermiso).WithMany(p => p.AprobacionesPermiso)
                .HasPrincipalKey(p => new { p.EmpresaId, p.SolicitudPermisoId })
                .HasForeignKey(d => new { d.EmpresaId, d.SolicitudPermisoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__AprobacionesPerm__53D770D6");
        });

        modelBuilder.Entity<AsignacionHorario>(entity =>
        {
            entity.ToTable("AsignacionesHorario");

            entity.HasKey(e => e.AsignacionHorarioId).HasName("PK__Asignaci__37B2CBC4FCC4B7B9");

            entity.Property(e => e.AsignacionHorarioId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.MotivoExcepcion).HasMaxLength(300);

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.AsignadoPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__AsignacionesHora__047AA831");

            entity.HasOne(d => d.HorarioLaboral).WithMany(p => p.AsignacionesHorario)
                .HasPrincipalKey(p => new { p.EmpresaId, p.HorarioLaboralId })
                .HasForeignKey(d => new { d.EmpresaId, d.HorarioLaboralId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__AsignacionesHora__038683F8");

            entity.HasOne(d => d.Trabajador).WithMany(p => p.AsignacionesHorario)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.TrabajadorId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__AsignacionesHora__02925FBF");
        });

        modelBuilder.Entity<AsignacionProyecto>(entity =>
        {
            entity.ToTable("AsignacionesProyecto");

            entity.HasKey(e => e.AsignacionProyectoId).HasName("PK__Asignaci__DB1CF26975085E0B");

            entity.HasIndex(e => new { e.EmpresaId, e.ProyectoId, e.TrabajadorId }, "UX_AsignacionProyecto_Activa")
                .IsUnique()
                .HasFilter("([FechaSalida] IS NULL)");

            entity.Property(e => e.AsignacionProyectoId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.FechaAsignacion).HasDefaultValueSql("(CONVERT([date],sysutcdatetime()))");
            entity.Property(e => e.RolEnProyecto).HasMaxLength(100);

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.AsignadoPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__AsignacionesProy__45544755");

            entity.HasOne(d => d.Proyecto).WithMany(p => p.AsignacionesProyecto)
                .HasPrincipalKey(p => new { p.EmpresaId, p.ProyectoId })
                .HasForeignKey(d => new { d.EmpresaId, d.ProyectoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__AsignacionesProy__436BFEE3");

            entity.HasOne(d => d.Trabajador).WithMany(p => p.AsignacionesProyecto)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.TrabajadorId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__AsignacionesProy__4460231C");
        });

        modelBuilder.Entity<AsignacionTarea>(entity =>
        {
            entity.ToTable("AsignacionesTarea");

            entity.HasKey(e => e.AsignacionTareaId).HasName("PK__Asignaci__60C0A51A6150BE7F");

            entity.HasIndex(e => new { e.EmpresaId, e.TareaId, e.TrabajadorId }, "UX_AsignacionTarea_Activa")
                .IsUnique()
                .HasFilter("([FechaRetiroUtc] IS NULL)");

            entity.Property(e => e.AsignacionTareaId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.FechaAsignacionUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FechaRetiroUtc).HasPrecision(0);

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.AsignadoPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__AsignacionesTare__60083D91");

            entity.HasOne(d => d.Tarea).WithMany(p => p.AsignacionesTarea)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TareaId })
                .HasForeignKey(d => new { d.EmpresaId, d.TareaId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__AsignacionesTare__5E1FF51F");

            entity.HasOne(d => d.Trabajador).WithMany(p => p.AsignacionesTarea)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.TrabajadorId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__AsignacionesTare__5F141958");
        });

        modelBuilder.Entity<Borrador>(entity =>
        {
            entity.ToTable("Borradores");

            entity.HasKey(e => e.BorradorId).HasName("PK__Borrador__B9A88936D2DCDE63");

            entity.Property(e => e.BorradorId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.ExpiraUtc).HasPrecision(0);
            entity.Property(e => e.FechaActualizacionUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FechaCreacionUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Modulo).HasMaxLength(80);
            entity.Property(e => e.TipoFormulario).HasMaxLength(100);

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.UsuarioCreadorId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Borradores__5CA1C101");
        });

        modelBuilder.Entity<Comentario>(entity =>
        {
            entity.ToTable("Comentarios");

            entity.HasKey(e => e.ComentarioId).HasName("PK__Comentar__F1844938C6EFB26A");

            entity.HasIndex(e => new { e.EmpresaId, e.ComentarioId }, "UQ_Comentario_Empresa_Id").IsUnique();

            entity.Property(e => e.ComentarioId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.FechaCreacionUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.ComentariosNavigation).WithMany(p => p.InverseComentariosNavigation)
                .HasPrincipalKey(p => new { p.EmpresaId, p.ComentarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.ComentarioPadreId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Comentarios__6F4A8121");

            entity.HasOne(d => d.Proyecto).WithMany(p => p.Comentarios)
                .HasPrincipalKey(p => new { p.EmpresaId, p.ProyectoId })
                .HasForeignKey(d => new { d.EmpresaId, d.ProyectoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Comentarios__6C6E1476");

            entity.HasOne(d => d.Tarea).WithMany(p => p.Comentarios)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TareaId })
                .HasForeignKey(d => new { d.EmpresaId, d.TareaId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Comentarios__6D6238AF");

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.UsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Comentarios__6E565CE8");
        });

        modelBuilder.Entity<ContactoEmergencia>(entity =>
        {
            entity.ToTable("ContactosEmergencia");

            entity.HasKey(e => e.ContactoEmergenciaId).HasName("PK__Contacto__9FF076AC66A9B6B6");

            entity.Property(e => e.ContactoEmergenciaId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.NombreCompleto).HasMaxLength(180);
            entity.Property(e => e.Parentesco).HasMaxLength(80);
            entity.Property(e => e.Telefono).HasMaxLength(30);
            entity.Property(e => e.TelefonoAlternativo).HasMaxLength(30);

            entity.HasOne(d => d.Trabajador).WithMany(p => p.ContactosEmergencia)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.TrabajadorId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__ContactosEmergen__25518C17");
        });

        modelBuilder.Entity<CorreccionAsistencia>(entity =>
        {
            entity.ToTable("CorreccionesAsistencia");

            entity.HasKey(e => e.CorreccionAsistenciaId).HasName("PK__Correcci__F1B60F13D1951BFD");

            entity.Property(e => e.EntradaAnteriorUtc).HasPrecision(0);
            entity.Property(e => e.EntradaNuevaUtc).HasPrecision(0);
            entity.Property(e => e.FechaCorreccionUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Motivo).HasMaxLength(1000);
            entity.Property(e => e.SalidaAnteriorUtc).HasPrecision(0);
            entity.Property(e => e.SalidaNuevaUtc).HasPrecision(0);

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.CorregidoPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__CorreccionesAsis__2116E6DF");

            entity.HasOne(d => d.RegistroAsistencia).WithMany(p => p.CorreccionesAsistencia)
                .HasPrincipalKey(p => new { p.EmpresaId, p.RegistroAsistenciaId })
                .HasForeignKey(d => new { d.EmpresaId, d.RegistroAsistenciaId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__CorreccionesAsis__2022C2A6");
        });

        modelBuilder.Entity<DefinicionReporte>(entity =>
        {
            entity.ToTable("DefinicionesReporte");

            entity.HasKey(e => e.DefinicionReporteId).HasName("PK__Definici__625703D4B2A52DA2");

            entity.HasIndex(e => new { e.EmpresaId, e.Nombre }, "UQ_DefinicionReporte_Nombre").IsUnique();

            entity.Property(e => e.DefinicionReporteId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Descripcion).HasMaxLength(500);
            entity.Property(e => e.Estado).HasDefaultValue(true);
            entity.Property(e => e.FechaCreacionUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Nombre).HasMaxLength(150);
            entity.Property(e => e.TipoReporte).HasMaxLength(100);

            entity.HasOne(d => d.Empresa).WithMany()
                .HasForeignKey(d => d.EmpresaId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Definicio__Empre__6442E2C9");

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.CreadoPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__DefinicionesRepo__65370702");
        });

        modelBuilder.Entity<Departamento>(entity =>
        {
            entity.ToTable("Departamentos");

            entity.HasKey(e => e.DepartamentoId).HasName("PK__Departam__66BB0E3E21D93FA5");

            entity.HasIndex(e => new { e.EmpresaId, e.DepartamentoId }, "UQ_Departamento_Empresa_Id").IsUnique();

            entity.HasIndex(e => new { e.EmpresaId, e.Nombre }, "UQ_Departamento_Nombre").IsUnique();

            entity.Property(e => e.DepartamentoId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Descripcion).HasMaxLength(300);
            entity.Property(e => e.Estado).HasDefaultValue(true);
            entity.Property(e => e.Nombre).HasMaxLength(120);

            entity.HasOne(d => d.Empresa).WithMany()
                .HasForeignKey(d => d.EmpresaId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Departamento_Empresa");
        });

        modelBuilder.Entity<DescansoTrabajador>(entity =>
        {
            entity.ToTable("DescansosTrabajador");

            entity.HasKey(e => e.DescansoTrabajadorId).HasName("PK__Descanso__F80E1535147FE387");

            entity.Property(e => e.DescansoTrabajadorId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Descripcion).HasMaxLength(300);

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.RegistradoPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__DescansosTrabaja__0A338187");

            entity.HasOne(d => d.Trabajador).WithMany(p => p.DescansosTrabajador)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.TrabajadorId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__DescansosTrabaja__093F5D4E");
        });

        modelBuilder.Entity<DiaFestivo>(entity =>
        {
            entity.ToTable("DiasFestivos");

            entity.HasKey(e => e.DiaFestivoId).HasName("PK__DiasFest__F1C5DF1D15973BAF");

            entity.HasIndex(e => new { e.EmpresaId, e.Fecha, e.Nombre }, "UQ_DiaFestivo").IsUnique();

            entity.Property(e => e.DiaFestivoId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Nombre).HasMaxLength(150);

            entity.HasOne(d => d.Empresa).WithMany()
                .HasForeignKey(d => d.EmpresaId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__DiasFesti__Empre__11D4A34F");
        });

        modelBuilder.Entity<ErrorTecnico>(entity =>
        {
            entity.ToTable("ErroresTecnicos");

            entity.HasKey(e => e.ErrorTecnicoId).HasName("PK__ErroresT__10B495F4E0105265");

            entity.Property(e => e.Codigo).HasMaxLength(100);
            entity.Property(e => e.FechaUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Mensaje).HasMaxLength(1000);
            entity.Property(e => e.Nivel).HasMaxLength(20);
            entity.Property(e => e.Ruta).HasMaxLength(500);

            entity.HasOne(d => d.Empresa).WithMany()
                .HasForeignKey(d => d.EmpresaId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__ErroresTe__Empre__69FBBC1F");

            entity.HasOne(d => d.Usuario).WithMany()
                .HasForeignKey(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__ErroresTe__Usuar__6AEFE058");
        });

        modelBuilder.Entity<EstadoProyecto>(entity =>
        {
            entity.ToTable("EstadosProyecto");

            entity.HasKey(e => e.EstadoProyectoId).HasName("PK__EstadosP__EC7FA7E3F0C4A29D");

            entity.HasIndex(e => new { e.EmpresaId, e.EstadoProyectoId }, "UQ_EstadoProyecto_Empresa_Id").IsUnique();

            entity.HasIndex(e => new { e.EmpresaId, e.Nombre }, "UQ_EstadoProyecto_Nombre").IsUnique();

            entity.Property(e => e.EstadoProyectoId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Estado).HasDefaultValue(true);
            entity.Property(e => e.Nombre).HasMaxLength(80);

            entity.HasOne(d => d.Empresa).WithMany()
                .HasForeignKey(d => d.EmpresaId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__EstadosPr__Empre__3335971A");
        });

        modelBuilder.Entity<EventoEmpresa>(entity =>
        {
            entity.ToTable("EventosEmpresa");

            entity.HasKey(e => e.EventoEmpresaId).HasName("PK__EventosE__ECD93F902AE8250C");

            entity.Property(e => e.EventoEmpresaId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Descripcion).HasMaxLength(1000);
            entity.Property(e => e.FechaCreacionUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FechaFin).HasPrecision(0);
            entity.Property(e => e.FechaInicio).HasPrecision(0);
            entity.Property(e => e.Tipo).HasDefaultValue((byte)1);
            entity.Property(e => e.Titulo).HasMaxLength(180);

            entity.HasOne(d => d.Empresa).WithMany()
                .HasForeignKey(d => d.EmpresaId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__EventosEm__Empre__27C3E46E");

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.CreadoPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__EventosEmpresa__28B808A7");
        });

        modelBuilder.Entity<HistorialAvanceTarea>(entity =>
        {
            entity.ToTable("HistorialAvanceTarea");

            entity.HasKey(e => e.HistorialAvanceTareaId).HasName("PK__Historia__009BC8D4EE5D82DE");

            entity.Property(e => e.Comentario).HasMaxLength(1000);
            entity.Property(e => e.FechaUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.ActualizadoPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__HistorialAvanceT__65C116E7");

            entity.HasOne(d => d.Tarea).WithMany(p => p.HistorialAvanceTarea)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TareaId })
                .HasForeignKey(d => new { d.EmpresaId, d.TareaId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__HistorialAvanceT__64CCF2AE");
        });

        modelBuilder.Entity<HistorialEstadoProyecto>(entity =>
        {
            entity.ToTable("HistorialEstadoProyecto");

            entity.HasKey(e => e.HistorialEstadoProyectoId).HasName("PK__Historia__3BC35F489CE42D1B");

            entity.Property(e => e.Comentario).HasMaxLength(500);
            entity.Property(e => e.FechaUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.CambiadoPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__HistorialEstadoP__4CF5691D");

            entity.HasOne(d => d.EstadoProyecto).WithMany(p => p.HistorialEstadoProyectoEstadosProyecto)
                .HasPrincipalKey(p => new { p.EmpresaId, p.EstadoProyectoId })
                .HasForeignKey(d => new { d.EmpresaId, d.EstadoAnteriorId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__HistorialEstadoP__4B0D20AB");

            entity.HasOne(d => d.EstadosProyectoNavigation).WithMany(p => p.HistorialEstadoProyectoEstadosProyectoNavigation)
                .HasPrincipalKey(p => new { p.EmpresaId, p.EstadoProyectoId })
                .HasForeignKey(d => new { d.EmpresaId, d.EstadoNuevoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__HistorialEstadoP__4C0144E4");

            entity.HasOne(d => d.Proyecto).WithMany(p => p.HistorialEstadoProyecto)
                .HasPrincipalKey(p => new { p.EmpresaId, p.ProyectoId })
                .HasForeignKey(d => new { d.EmpresaId, d.ProyectoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__HistorialEstadoP__4A18FC72");
        });

        modelBuilder.Entity<HistorialLaboral>(entity =>
        {
            entity.ToTable("HistorialLaboral");

            entity.HasKey(e => e.HistorialLaboralId).HasName("PK__Historia__2EB05FCDAE5BB20F");

            entity.Property(e => e.Motivo).HasMaxLength(500);
            entity.Property(e => e.RegistradoUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.TipoCambio).HasMaxLength(80);

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.RegistradoPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__HistorialLaboral__31B762FC");

            entity.HasOne(d => d.Trabajador).WithMany(p => p.HistorialLaboral)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.TrabajadorId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__HistorialLaboral__30C33EC3");
        });

        modelBuilder.Entity<HorarioDetalleDia>(entity =>
        {
            entity.ToTable("HorarioDetalleDia");

            entity.HasKey(e => e.HorarioDetalleDiaId).HasName("PK__HorarioD__FBB901FAE5B6EAAB");

            entity.HasIndex(e => new { e.HorarioLaboralId, e.DiaSemana }, "UQ_Horario_Dia").IsUnique();

            entity.Property(e => e.EsDiaLaboral).HasDefaultValue(true);
            entity.Property(e => e.FinDescanso).HasPrecision(0);
            entity.Property(e => e.HoraEntrada).HasPrecision(0);
            entity.Property(e => e.HoraSalida).HasPrecision(0);
            entity.Property(e => e.InicioDescanso).HasPrecision(0);

            entity.HasOne(d => d.HorarioLaboral).WithMany(p => p.HorarioDetalleDia)
                .HasPrincipalKey(p => new { p.EmpresaId, p.HorarioLaboralId })
                .HasForeignKey(d => new { d.EmpresaId, d.HorarioLaboralId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__HorarioDetalleDi__7DCDAAA2");
        });

        modelBuilder.Entity<HorarioLaboral>(entity =>
        {
            entity.ToTable("HorariosLaborales");

            entity.HasKey(e => e.HorarioLaboralId).HasName("PK__Horarios__AAA969017977C9A2");

            entity.HasIndex(e => new { e.EmpresaId, e.HorarioLaboralId }, "UQ_Horario_Empresa_Id").IsUnique();

            entity.Property(e => e.HorarioLaboralId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Estado).HasDefaultValue(true);
            entity.Property(e => e.Nombre).HasMaxLength(120);

            entity.HasOne(d => d.Empresa).WithMany()
                .HasForeignKey(d => d.EmpresaId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__HorariosL__Empre__753864A1");

            entity.HasOne(d => d.Departamento).WithMany(p => p.HorariosLaborales)
                .HasPrincipalKey(p => new { p.EmpresaId, p.DepartamentoId })
                .HasForeignKey(d => new { d.EmpresaId, d.DepartamentoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__HorariosLaborale__762C88DA");

            entity.HasOne(d => d.Puesto).WithMany(p => p.HorariosLaborales)
                .HasPrincipalKey(p => new { p.EmpresaId, p.PuestoId })
                .HasForeignKey(d => new { d.EmpresaId, d.PuestoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__HorariosLaborale__7720AD13");

            entity.HasOne(d => d.Turno).WithMany(p => p.HorariosLaborales)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TurnoId })
                .HasForeignKey(d => new { d.EmpresaId, d.TurnoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__HorariosLaborale__7814D14C");
        });

        modelBuilder.Entity<MencionComentario>(entity =>
        {
            entity.ToTable("MencionesComentario");

            entity.HasKey(e => e.MencionComentarioId).HasName("PK__Mencione__BC67026C35265008");

            entity.HasIndex(e => new { e.ComentarioId, e.UsuarioMencionadoId }, "UQ_Mencion_Comentario_Usuario").IsUnique();

            entity.Property(e => e.FechaCreacionUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Comentario).WithMany(p => p.MencionesComentario)
                .HasPrincipalKey(p => new { p.EmpresaId, p.ComentarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.ComentarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__MencionesComenta__75035A77");

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.UsuarioMencionadoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__MencionesComenta__75F77EB0");
        });

        modelBuilder.Entity<MovimientoSaldoPermiso>(entity =>
        {
            entity.ToTable("MovimientosSaldoPermiso");

            entity.HasKey(e => e.MovimientoSaldoPermisoId).HasName("PK__Movimien__322AAEDDA6F59FC2");

            entity.Property(e => e.Cantidad).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Descripcion).HasMaxLength(300);
            entity.Property(e => e.FechaUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.SaldoPermiso).WithMany(p => p.MovimientosSaldoPermiso)
                .HasForeignKey(d => d.SaldoPermisoId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Movimient__Saldo__59904A2C");

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.RegistradoPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__MovimientosSaldo__5B78929E");

            entity.HasOne(d => d.SolicitudPermiso).WithMany(p => p.MovimientosSaldoPermiso)
                .HasPrincipalKey(p => new { p.EmpresaId, p.SolicitudPermisoId })
                .HasForeignKey(d => new { d.EmpresaId, d.SolicitudPermisoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__MovimientosSaldo__5A846E65");
        });

        modelBuilder.Entity<Notificacion>(entity =>
        {
            entity.ToTable("Notificaciones");

            entity.HasKey(e => e.NotificacionId).HasName("PK__Notifica__BCC12024519843B0");

            entity.HasIndex(e => new { e.EmpresaId, e.UsuarioDestinoId, e.LeidaUtc, e.ArchivadaUtc, e.CreadaUtc }, "IX_Notificaciones_Inbox").IsDescending(false, false, false, false, true);

            entity.Property(e => e.NotificacionId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.ArchivadaUtc).HasPrecision(0);
            entity.Property(e => e.CreadaUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.EntidadId).HasMaxLength(100);
            entity.Property(e => e.EntidadTipo).HasMaxLength(80);
            entity.Property(e => e.LeidaUtc).HasPrecision(0);
            entity.Property(e => e.Mensaje).HasMaxLength(1000);
            entity.Property(e => e.Tipo).HasMaxLength(80);
            entity.Property(e => e.Titulo).HasMaxLength(180);

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.UsuarioDestinoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Notificaciones__4B7734FF");
        });

        modelBuilder.Entity<OperacionRespaldo>(entity =>
        {
            entity.ToTable("OperacionesRespaldo");

            entity.HasKey(e => e.OperacionRespaldoId).HasName("PK__Operacio__7CE50164D945C6C0");

            entity.Property(e => e.OperacionRespaldoId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.FechaFinUtc).HasPrecision(0);
            entity.Property(e => e.FechaInicioUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Mensaje).HasMaxLength(1000);
            entity.Property(e => e.UbicacionArchivo).HasMaxLength(1000);

            entity.HasOne(d => d.EjecutadaPorUsuario).WithMany()
                .HasForeignKey(d => d.EjecutadaPorUsuarioId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Operacion__Ejecu__719CDDE7");

            entity.HasOne(d => d.Empresa).WithMany()
                .HasForeignKey(d => d.EmpresaId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Operacion__Empre__70A8B9AE");
        });

        modelBuilder.Entity<PreferenciaNotificacion>(entity =>
        {
            entity.ToTable("PreferenciasNotificacion");

            entity.HasKey(e => e.PreferenciaNotificacionId).HasName("PK__Preferen__C5E17EE90F55BC21");

            entity.HasIndex(e => new { e.EmpresaId, e.UsuarioId, e.TipoNotificacion }, "UQ_PreferenciaNotificacion").IsUnique();

            entity.Property(e => e.Habilitada).HasDefaultValue(true);
            entity.Property(e => e.TipoNotificacion).HasMaxLength(80);

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.UsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__PreferenciasNoti__503BEA1C");
        });

        modelBuilder.Entity<PreferenciaUsuario>(entity =>
        {
            entity.ToTable("PreferenciasUsuario");

            entity.HasKey(e => new { e.EmpresaId, e.UsuarioId });

            entity.Property(e => e.Cultura).HasMaxLength(20);
            entity.Property(e => e.FechaActualizacionUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Tema)
                .HasMaxLength(20)
                .HasDefaultValue("sistema");
            entity.Property(e => e.ZonaHorariaId).HasMaxLength(100);

            entity.HasOne(d => d.Usuario).WithOne()
                .HasPrincipalKey<Usuario>(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey<PreferenciaUsuario>(d => new { d.EmpresaId, d.UsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__PreferenciasUsua__55009F39");
        });

        modelBuilder.Entity<Proyecto>(entity =>
        {
            entity.ToTable("Proyectos");

            entity.HasKey(e => e.ProyectoId).HasName("PK__Proyecto__CF241D6513E2945C");

            entity.HasIndex(e => new { e.EmpresaId, e.ProyectoId }, "UQ_Proyecto_Empresa_Id").IsUnique();

            entity.Property(e => e.ProyectoId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.FechaActualizacionUtc).HasPrecision(0);
            entity.Property(e => e.FechaCierreUtc).HasPrecision(0);
            entity.Property(e => e.FechaCreacionUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Nombre).HasMaxLength(180);
            entity.Property(e => e.Prioridad).HasDefaultValue((byte)2);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.Empresa).WithMany()
                .HasForeignKey(d => d.EmpresaId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Proyectos__Empre__39E294A9");

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.CreadoPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Proyectos__3CBF0154");

            entity.HasOne(d => d.EstadoProyecto).WithMany(p => p.Proyectos)
                .HasPrincipalKey(p => new { p.EmpresaId, p.EstadoProyectoId })
                .HasForeignKey(d => new { d.EmpresaId, d.EstadoProyectoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Proyectos__3AD6B8E2");

            entity.HasOne(d => d.Trabajador).WithMany(p => p.Proyectos)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.ResponsableTrabajadorId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Proyectos__3BCADD1B");
        });

        modelBuilder.Entity<Puesto>(entity =>
        {
            entity.ToTable("Puestos");

            entity.HasKey(e => e.PuestoId).HasName("PK__Puestos__F7F6C604B5E13412");

            entity.HasIndex(e => new { e.EmpresaId, e.PuestoId }, "UQ_Puesto_Empresa_Id").IsUnique();

            entity.HasIndex(e => new { e.EmpresaId, e.Nombre }, "UQ_Puesto_Nombre").IsUnique();

            entity.Property(e => e.PuestoId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Descripcion).HasMaxLength(300);
            entity.Property(e => e.Estado).HasDefaultValue(true);
            entity.Property(e => e.Nombre).HasMaxLength(120);

            entity.HasOne(d => d.Empresa).WithMany()
                .HasForeignKey(d => d.EmpresaId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Puesto_Empresa");
        });

        modelBuilder.Entity<RegistroAsistencia>(entity =>
        {
            entity.ToTable("RegistrosAsistencia");

            entity.HasKey(e => e.RegistroAsistenciaId).HasName("PK__Registro__07817236303BE79E");

            entity.HasIndex(e => new { e.EmpresaId, e.TrabajadorId, e.FechaLaboral }, "IX_Asistencia_TrabajadorFecha").IsDescending(false, false, true);

            entity.HasIndex(e => new { e.EmpresaId, e.RegistroAsistenciaId }, "UQ_Asistencia_Empresa_Id").IsUnique();

            entity.Property(e => e.RegistroAsistenciaId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.EntradaLatitud).HasColumnType("decimal(9, 6)");
            entity.Property(e => e.EntradaLongitud).HasColumnType("decimal(9, 6)");
            entity.Property(e => e.EntradaUtc).HasPrecision(0);
            entity.Property(e => e.Estado).HasDefaultValue((byte)1);
            entity.Property(e => e.FechaCreacionUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Observaciones).HasMaxLength(500);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.SalidaLatitud).HasColumnType("decimal(9, 6)");
            entity.Property(e => e.SalidaLongitud).HasColumnType("decimal(9, 6)");
            entity.Property(e => e.SalidaUtc).HasPrecision(0);

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.EntradaRegistradaPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__RegistrosAsisten__1975C517");

            entity.HasOne(d => d.UsuariosNavigation).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.SalidaRegistradaPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__RegistrosAsisten__1A69E950");

            entity.HasOne(d => d.Trabajador).WithMany(p => p.RegistrosAsistencia)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.TrabajadorId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__RegistrosAsisten__1881A0DE");
        });

        modelBuilder.Entity<ReglaPermiso>(entity =>
        {
            entity.ToTable("ReglasPermiso");

            entity.HasKey(e => e.ReglaPermisoId).HasName("PK__ReglasPe__F3982145AD935AF2");

            entity.Property(e => e.ReglaPermisoId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CantidadAnual).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Estado).HasDefaultValue(true);
            entity.Property(e => e.LimitePorSolicitud).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.ReiniciaAnualmente).HasDefaultValue(true);

            entity.HasOne(d => d.TipoPermiso).WithMany(p => p.ReglasPermiso)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TipoPermisoId })
                .HasForeignKey(d => new { d.EmpresaId, d.TipoPermisoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__ReglasPermiso__36470DEF");
        });

        modelBuilder.Entity<SaldoPermiso>(entity =>
        {
            entity.ToTable("SaldosPermiso");

            entity.HasKey(e => e.SaldoPermisoId).HasName("PK__SaldosPe__1CD0423286EB5FBA");

            entity.HasIndex(e => new { e.EmpresaId, e.TrabajadorId, e.TipoPermisoId, e.PeriodoAnio }, "UQ_SaldoPermiso").IsUnique();

            entity.Property(e => e.SaldoPermisoId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CantidadAsignada).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CantidadReservada).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CantidadUtilizada).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.FechaActualizacionUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.TipoPermiso).WithMany(p => p.SaldosPermiso)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TipoPermisoId })
                .HasForeignKey(d => new { d.EmpresaId, d.TipoPermisoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__SaldosPermiso__42ACE4D4");

            entity.HasOne(d => d.Trabajador).WithMany(p => p.SaldosPermiso)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.TrabajadorId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__SaldosPermiso__41B8C09B");
        });

        modelBuilder.Entity<SolicitudCambioPerfil>(entity =>
        {
            entity.ToTable("SolicitudesCambioPerfil");

            entity.HasKey(e => e.SolicitudCambioPerfilId).HasName("PK__Solicitu__C58643E22C8AB86C");

            entity.Property(e => e.SolicitudCambioPerfilId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.ComentarioRevision).HasMaxLength(500);
            entity.Property(e => e.Estado).HasDefaultValue((byte)1);
            entity.Property(e => e.RevisadaUtc).HasPrecision(0);
            entity.Property(e => e.SolicitadaUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.RevisadaPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__SolicitudesCambi__22401542");

            entity.HasOne(d => d.Trabajador).WithMany(p => p.SolicitudesCambioPerfil)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.TrabajadorId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__SolicitudesCambi__214BF109");
        });

        modelBuilder.Entity<SolicitudCancelacionPermiso>(entity =>
        {
            entity.ToTable("SolicitudesCancelacionPermiso");

            entity.HasKey(e => e.SolicitudCancelacionId).HasName("PK__Solicitu__A249942F75D820EC");

            entity.Property(e => e.SolicitudCancelacionId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.ComentarioRevision).HasMaxLength(500);
            entity.Property(e => e.Estado).HasDefaultValue((byte)1);
            entity.Property(e => e.Motivo).HasMaxLength(1000);
            entity.Property(e => e.RevisadaUtc).HasPrecision(0);
            entity.Property(e => e.SolicitadaUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.RevisadaPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__SolicitudesCance__6501FCD8");

            entity.HasOne(d => d.UsuariosNavigation).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.SolicitadaPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__SolicitudesCance__640DD89F");

            entity.HasOne(d => d.SolicitudPermiso).WithMany(p => p.SolicitudesCancelacionPermiso)
                .HasPrincipalKey(p => new { p.EmpresaId, p.SolicitudPermisoId })
                .HasForeignKey(d => new { d.EmpresaId, d.SolicitudPermisoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__SolicitudesCance__6319B466");
        });

        modelBuilder.Entity<SolicitudPermiso>(entity =>
        {
            entity.ToTable("SolicitudesPermiso");

            entity.HasKey(e => e.SolicitudPermisoId).HasName("PK__Solicitu__8AE45F345CB6BE74");

            entity.HasIndex(e => new { e.EmpresaId, e.SolicitudPermisoId }, "UQ_SolicitudPermiso_Empresa_Id").IsUnique();

            entity.Property(e => e.SolicitudPermisoId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CantidadSolicitada).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Estado).HasDefaultValue((byte)1);
            entity.Property(e => e.FechaFin).HasPrecision(0);
            entity.Property(e => e.FechaInicio).HasPrecision(0);
            entity.Property(e => e.ModificadaUtc).HasPrecision(0);
            entity.Property(e => e.MotivoPrivado).HasMaxLength(1000);
            entity.Property(e => e.SolicitadaUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.TipoPermiso).WithMany(p => p.SolicitudesPermiso)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TipoPermisoId })
                .HasForeignKey(d => new { d.EmpresaId, d.TipoPermisoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__SolicitudesPermi__4C364F0E");

            entity.HasOne(d => d.Trabajador).WithMany(p => p.SolicitudesPermiso)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.TrabajadorId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__SolicitudesPermi__4B422AD5");
        });

        modelBuilder.Entity<Tarea>(entity =>
        {
            entity.ToTable("Tareas");

            entity.HasKey(e => e.TareaId).HasName("PK__Tareas__5CD839918EE44231");

            entity.HasIndex(e => new { e.EmpresaId, e.TareaId }, "UQ_Tarea_Empresa_Id").IsUnique();

            entity.Property(e => e.TareaId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Estado).HasDefaultValue((byte)1);
            entity.Property(e => e.FechaActualizacionUtc).HasPrecision(0);
            entity.Property(e => e.FechaCreacionUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FechaFinalizacionUtc).HasPrecision(0);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Titulo).HasMaxLength(180);

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.CreadaPorUsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Tareas__558AAF1E");

            entity.HasOne(d => d.Proyecto).WithMany(p => p.Tareas)
                .HasPrincipalKey(p => new { p.EmpresaId, p.ProyectoId })
                .HasForeignKey(d => new { d.EmpresaId, d.ProyectoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Tareas__54968AE5");

            entity.HasOne(d => d.Trabajador).WithMany(p => p.Tareas)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.ResponsableTrabajadorId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Tareas__567ED357");
        });

        modelBuilder.Entity<TipoPermiso>(entity =>
        {
            entity.ToTable("TiposPermiso");

            entity.HasKey(e => e.TipoPermisoId).HasName("PK__TiposPer__6A7B92D2B83EFDC5");

            entity.HasIndex(e => new { e.EmpresaId, e.TipoPermisoId }, "UQ_TipoPermiso_Empresa_Id").IsUnique();

            entity.HasIndex(e => new { e.EmpresaId, e.Nombre }, "UQ_TipoPermiso_Nombre").IsUnique();

            entity.Property(e => e.TipoPermisoId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Descripcion).HasMaxLength(300);
            entity.Property(e => e.Estado).HasDefaultValue(true);
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.PermiteFraccionar).HasDefaultValue(true);
            entity.Property(e => e.RequiereAprobacion).HasDefaultValue(true);
            entity.Property(e => e.UnidadBase).HasDefaultValue((byte)1);

            entity.HasOne(d => d.Empresa).WithMany()
                .HasForeignKey(d => d.EmpresaId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__TiposPerm__Empre__2EA5EC27");
        });

        modelBuilder.Entity<TokenRecuperacionContrasena>(entity =>
        {
            entity.ToTable("TokensRecuperacionContrasena");

            entity.HasKey(e => e.TokenRecuperacionId).HasName("PK__TokensRe__E9CC978B46D6A862");

            entity.HasIndex(e => e.TokenHash, "UX_TokenRec_Hash").IsUnique();

            entity.Property(e => e.TokenRecuperacionId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreadoUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ExpiraUtc).HasPrecision(0);
            entity.Property(e => e.RevocadoUtc).HasPrecision(0);
            entity.Property(e => e.TokenHash).HasMaxLength(32);
            entity.Property(e => e.UsadoUtc).HasPrecision(0);

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.UsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TokenRec_Usuario");
        });

        modelBuilder.Entity<Trabajador>(entity =>
        {
            entity.ToTable("Trabajadores");

            entity.HasKey(e => e.TrabajadorId).HasName("PK__Trabajad__C5EF3A2168D98D88");

            entity.HasIndex(e => new { e.EmpresaId, e.TrabajadorId }, "UQ_Trabajador_Empresa_Id").IsUnique();

            entity.HasIndex(e => new { e.EmpresaId, e.NumeroEmpleado }, "UX_Trabajador_Numero")
                .IsUnique()
                .HasFilter("([NumeroEmpleado] IS NOT NULL)");

            entity.Property(e => e.TrabajadorId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Apellidos).HasMaxLength(160);
            entity.Property(e => e.CorreoContacto).HasMaxLength(254);
            entity.Property(e => e.Direccion).HasMaxLength(300);
            entity.Property(e => e.EstadoLaboral).HasDefaultValue((byte)1);
            entity.Property(e => e.FechaActualizacionUtc).HasPrecision(0);
            entity.Property(e => e.FechaCreacionUtc)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.MotivoBaja).HasMaxLength(500);
            entity.Property(e => e.Nombres).HasMaxLength(120);
            entity.Property(e => e.NumeroEmpleado).HasMaxLength(40);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Telefono).HasMaxLength(30);

            entity.HasOne(d => d.Empresa).WithMany()
                .HasForeignKey(d => d.EmpresaId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Trabajador_Empresa");

            entity.HasOne(d => d.Departamento).WithMany(p => p.Trabajadores)
                .HasPrincipalKey(p => new { p.EmpresaId, p.DepartamentoId })
                .HasForeignKey(d => new { d.EmpresaId, d.DepartamentoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Trabajador_Dep");

            entity.HasOne(d => d.Puesto).WithMany(p => p.Trabajadores)
                .HasPrincipalKey(p => new { p.EmpresaId, p.PuestoId })
                .HasForeignKey(d => new { d.EmpresaId, d.PuestoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Trabajador_Puesto");

            entity.HasOne(d => d.Usuario).WithMany()
                .HasPrincipalKey(p => new { p.EmpresaId, p.UsuarioId })
                .HasForeignKey(d => new { d.EmpresaId, d.UsuarioId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Trabajador_Usuario");
        });

        modelBuilder.Entity<TrabajadorSupervisor>(entity =>
        {
            entity.ToTable("TrabajadorSupervisores");

            entity.HasKey(e => e.TrabajadorSupervisorId).HasName("PK__Trabajad__577F30EE95478521");

            entity.HasOne(d => d.Departamento).WithMany(p => p.TrabajadorSupervisores)
                .HasPrincipalKey(p => new { p.EmpresaId, p.DepartamentoId })
                .HasForeignKey(d => new { d.EmpresaId, d.DepartamentoId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__TrabajadorSuperv__2B0A656D");

            entity.HasOne(d => d.Trabajador).WithMany(p => p.TrabajadorSupervisoresTrabajadores)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.SupervisorTrabajadorId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__TrabajadorSuperv__2A164134");

            entity.HasOne(d => d.TrabajadoresNavigation).WithMany(p => p.TrabajadorSupervisoresTrabajadoresNavigation)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.TrabajadorId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__TrabajadorSuperv__29221CFB");
        });

        modelBuilder.Entity<Turno>(entity =>
        {
            entity.ToTable("Turnos");

            entity.HasKey(e => e.TurnoId).HasName("PK__Turnos__AD3E2E94E9C1D94F");

            entity.HasIndex(e => new { e.EmpresaId, e.TurnoId }, "UQ_Turno_Empresa_Id").IsUnique();

            entity.HasIndex(e => new { e.EmpresaId, e.Nombre }, "UQ_Turno_Nombre").IsUnique();

            entity.Property(e => e.TurnoId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.Estado).HasDefaultValue(true);
            entity.Property(e => e.HoraFin).HasPrecision(0);
            entity.Property(e => e.HoraInicio).HasPrecision(0);
            entity.Property(e => e.Nombre).HasMaxLength(100);

            entity.HasOne(d => d.Empresa).WithMany()
                .HasForeignKey(d => d.EmpresaId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK__Turnos__EmpresaI__6E8B6712");
        });


        // Usuarios.TrabajadorId (columna del Bloque 1) -> Trabajadores
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasOne(d => d.Trabajador).WithMany(p => p.Usuarios)
                .HasPrincipalKey(p => new { p.EmpresaId, p.TrabajadorId })
                .HasForeignKey(d => new { d.EmpresaId, d.TrabajadorId })
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Usuarios_Trabajadores");
        });

        AjustarParaSqlite(modelBuilder);
    }

    // SQLite (solo pruebas) no tiene newsequentialid/sysutcdatetime ni rowversion: los valores se generan en la aplicaciÃ³n.
    private void AjustarParaSqlite(ModelBuilder modelBuilder)
    {
        if (Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite") return;
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
        {
            if (property.GetDefaultValueSql() is not null) property.SetDefaultValueSql(null);
            if (property.ClrType == typeof(byte[]) && property.Name == "RowVersion")
                property.SetDefaultValueSql("(randomblob(8))");
        }
    }}