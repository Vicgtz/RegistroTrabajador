namespace RegistroTrabajo.Models;

// Valores permitidos por los CHECK de la base de datos (Estado 1..3, Resultado 1..5).
public static class EstadoEmpresa
{
    public const byte Activa = 1;
    public const byte Suspendida = 2;
    public const byte Inactiva = 3;
}

public static class EstadoUsuario
{
    public const byte Activo = 1;
    public const byte Inactivo = 2;
    public const byte Suspendido = 3;
}

// Bloque 3. La BD solo limita EstadoLaboral a 1..3 y el estado de solicitud a 1..4; el significado es de la aplicación.
public static class EstadoLaboral
{
    public const byte Activo = 1;
    public const byte Inactivo = 2;
    public const byte Baja = 3;
}

public static class EstadoSolicitudPerfil
{
    public const byte Pendiente = 1;
    public const byte Aprobada = 2;
    public const byte Rechazada = 3;
    public const byte Cancelada = 4;
}

// Bloque 4. La BD solo limita rangos (Estado 1..5, Decision 1..3, TipoMovimiento 1..4, UnidadBase 1..2, cancelación 1..4);
// el significado es de la aplicación.
public static class EstadoSolicitudPermiso
{
    public const byte Pendiente = 1;
    public const byte Aprobada = 2;
    public const byte Rechazada = 3;
    public const byte Cancelada = 4;
    /// <summary>Reservado para uso futuro (la BD admite 1..5); hoy no se asigna.</summary>
    public const byte Concluida = 5;
}

public static class DecisionPermiso
{
    public const byte Aprobada = 1;
    public const byte Rechazada = 2;
    /// <summary>Reservado: las decisiones sobre cancelaciones viven en SolicitudesCancelacionPermiso.</summary>
    public const byte Cancelada = 3;
}

public static class EstadoCancelacionPermiso
{
    public const byte Pendiente = 1;
    public const byte Aprobada = 2;
    public const byte Rechazada = 3;
    public const byte Cancelada = 4;
}

public static class UnidadPermiso
{
    public const byte Dias = 1;
    public const byte Horas = 2;
}

/// <summary>
/// Libro mayor de saldos. La BD exige Cantidad &gt; 0, así que el signo lo da el tipo:
/// Asignación suma a lo asignado, Consumo suma a lo utilizado, Devolución resta de lo utilizado y
/// Ajuste (a la baja) resta de lo asignado. Los ajustes al alza se registran como Asignación con descripción "Ajuste: ...".
/// </summary>
public static class TipoMovimientoSaldo
{
    public const byte Asignacion = 1;
    public const byte Consumo = 2;
    public const byte Devolucion = 3;
    public const byte Ajuste = 4;
}

// Bloque 5. La BD solo limita RegistroAsistencia.Estado a 1..3 y DescansoTrabajador.Tipo a 1..2; el significado es de la aplicación.
public static class EstadoAsistencia
{
    /// <summary>Entrada registrada, todavía sin salida (jornada incompleta).</summary>
    public const byte Abierta = 1;
    public const byte Completa = 2;
    /// <summary>Invalidada por un supervisor con motivo; no cuenta como jornada.</summary>
    public const byte Anulada = 3;
}

public static class TipoDescanso
{
    /// <summary>Descanso semanal o habitual del trabajador.</summary>
    public const byte Habitual = 1;
    /// <summary>Excepción temporal (cambio de descanso en un periodo).</summary>
    public const byte Excepcion = 2;
}

// Bloque 6. La BD limita EstadoEvento a 1..2, Visibilidad a 1..2 y Tipo a 1..5 (script 007); el significado es de la aplicación.
public static class EstadoEventoEmpresa
{
    public const byte Activo = 1;
    /// <summary>Se conserva para el historial pero no aparece en el calendario habitual.</summary>
    public const byte Cancelado = 2;
}

public static class TipoEventoEmpresa
{
    public const byte ReunionGeneral = 1;
    public const byte Capacitacion = 2;
    public const byte ActividadInterna = 3;
    public const byte FechaImportante = 4;
    public const byte Otro = 5;
}

public static class VisibilidadEvento
{
    public const byte Empresa = 1;
    /// <summary>Solo los trabajadores del departamento indicado (y quienes administran eventos).</summary>
    public const byte Departamento = 2;
}

/// <summary>Origen de cada elemento que el calendario reúne en una misma vista.</summary>
public static class CategoriaCalendario
{
    public const byte Evento = 1;
    public const byte Festivo = 2;
    public const byte Ausencia = 3;
    public const byte Cumpleanos = 4;
    public const byte Aniversario = 5;
}

// Bloque 7. La BD limita Tareas.Estado a 1..6 y Prioridad (de proyectos y tareas, script 008) a 1..4; el significado es de la aplicación.
public static class EstadoTarea
{
    public const byte Pendiente = 1;
    public const byte EnProceso = 2;
    /// <summary>El trabajador terminó (100 %) y espera la revisión del responsable.</summary>
    public const byte EnRevision = 3;
    public const byte Completada = 4;
    /// <summary>Volvió a abrirse tras una revisión o un cierre; conserva sus actualizaciones anteriores.</summary>
    public const byte Reabierta = 5;
    /// <summary>Se conserva para el historial; las tareas no se eliminan físicamente.</summary>
    public const byte Cancelada = 6;

    public static bool EsCerrada(byte e) => e is Completada or Cancelada;
}

/// <summary>Tipos de <c>EventosSeguimientoTarea.Tipo</c> (Bloque 8). El avance y los cambios de estado viven en HistorialAvanceTarea.</summary>
public static class TipoEventoTarea
{
    public const string FechaLimiteCambiada = "fecha_limite_cambiada";
    public const string PrioridadCambiada = "prioridad_cambiada";
    public const string AsignacionAgregada = "asignacion_agregada";
    public const string AsignacionRetirada = "asignacion_retirada";
    public const string EnviadaARevision = "enviada_a_revision";
    public const string RevisionAprobada = "revision_aprobada";
    public const string RevisionDevuelta = "revision_devuelta";
    public const string Reabierta = "reabierta";
    public const string Cancelada = "cancelada";
    public const string Comentario = "comentario";
    /// <summary>Evento sintético: proviene de HistorialAvanceTarea (no se guarda en EventosSeguimientoTarea).</summary>
    public const string AvanceActualizado = "avance_actualizado";
}

public static class ResultadoRevision
{
    public const byte Aprobada = 1;
    public const byte Devuelta = 2;
}

public static class PrioridadTrabajo
{
    public const byte Baja = 1;
    public const byte Media = 2;
    public const byte Alta = 3;
    public const byte Urgente = 4;
}

/// <summary>Tipos de notificación del Bloque 7 (Notificaciones.Tipo).</summary>
public static class TipoNotificacionTrabajo
{
    public const string ProyectoAsignado = "proyecto.asignado";
    public const string ProyectoEstado = "proyecto.estado";
    public const string ProyectoResponsable = "proyecto.responsable";
    public const string ProyectoFecha = "proyecto.fecha";
    public const string TareaAsignada = "tarea.asignada";
    public const string TareaRevision = "tarea.revision_pendiente";
    public const string TareaRevisada = "tarea.revisada";
    public const string TareaMencion = "tarea.mencion";
    public const string TareaComentario = "tarea.comentario";
    public const string TareaFechaLimite = "tarea.fecha_limite";
    public const string TareaRetirada = "tarea.retirada";
}

/// <summary>Tipos de notificación de permisos y ausencias (Bloque 4).</summary>
public static class TipoNotificacionPermiso
{
    public const string SolicitudPendiente = "permiso.solicitud_pendiente";
    public const string Resuelta = "permiso.resuelta";
    public const string CancelacionPendiente = "permiso.cancelacion_pendiente";
    public const string CancelacionResuelta = "permiso.cancelacion_resuelta";
}

/// <summary>Categorías del centro de notificaciones (Bloque 9).</summary>
public static class CategoriaNotificacion
{
    public const string Permisos = "permisos";
    public const string Proyectos = "proyectos";
    public const string Tareas = "tareas";
    public const string Menciones = "menciones";

    public static readonly string[] Todas = { Permisos, Proyectos, Tareas, Menciones };

    public static string Nombre(string categoria) => categoria switch
    {
        Permisos => "Permisos y ausencias",
        Proyectos => "Proyectos",
        Tareas => "Tareas",
        Menciones => "Menciones",
        _ => categoria
    };

    public static string Descripcion(string categoria) => categoria switch
    {
        Permisos => "Solicitudes, aprobaciones, rechazos y cancelaciones.",
        Proyectos => "Incorporación a proyectos, cambios de responsable y modificaciones relevantes.",
        Tareas => "Nuevas asignaciones, fechas límite, revisiones y correcciones.",
        Menciones => "Avisos cuando otro usuario te menciona en un comentario.",
        _ => ""
    };
}

public sealed record InfoTipoNotificacion(string Tipo, string Categoria, bool Obligatoria, string Descripcion);

/// <summary>
/// Catálogo de tipos de notificación. Las obligatorias corresponden a operaciones importantes y no se pueden desactivar;
/// las opcionales sí. Es una lista fija en código: no hay reglas por empresa en la primera versión.
/// </summary>
public static class CatalogoNotificaciones
{
    public static readonly IReadOnlyList<InfoTipoNotificacion> Tipos = new List<InfoTipoNotificacion>
    {
        new(TipoNotificacionPermiso.SolicitudPendiente, CategoriaNotificacion.Permisos, true, "Solicitud de permiso pendiente de revisión"),
        new(TipoNotificacionPermiso.Resuelta, CategoriaNotificacion.Permisos, true, "Resultado de tu solicitud de permiso"),
        new(TipoNotificacionPermiso.CancelacionPendiente, CategoriaNotificacion.Permisos, true, "Solicitud de cancelación pendiente de revisión"),
        new(TipoNotificacionPermiso.CancelacionResuelta, CategoriaNotificacion.Permisos, true, "Resultado de tu solicitud de cancelación"),
        new(TipoNotificacionTrabajo.ProyectoAsignado, CategoriaNotificacion.Proyectos, true, "Te incorporaron a un proyecto"),
        new(TipoNotificacionTrabajo.ProyectoResponsable, CategoriaNotificacion.Proyectos, true, "Cambió el responsable de un proyecto"),
        new(TipoNotificacionTrabajo.ProyectoEstado, CategoriaNotificacion.Proyectos, false, "Cambió el estado de un proyecto"),
        new(TipoNotificacionTrabajo.ProyectoFecha, CategoriaNotificacion.Proyectos, false, "Cambió la fecha de término de un proyecto"),
        new(TipoNotificacionTrabajo.TareaAsignada, CategoriaNotificacion.Tareas, true, "Te asignaron una tarea"),
        new(TipoNotificacionTrabajo.TareaRetirada, CategoriaNotificacion.Tareas, true, "Te retiraron de una tarea"),
        new(TipoNotificacionTrabajo.TareaFechaLimite, CategoriaNotificacion.Tareas, true, "Cambió la fecha límite de una tarea"),
        new(TipoNotificacionTrabajo.TareaRevision, CategoriaNotificacion.Tareas, true, "Una tarea espera tu revisión"),
        new(TipoNotificacionTrabajo.TareaRevisada, CategoriaNotificacion.Tareas, true, "Aprobaron, devolvieron, reabrieron o cancelaron una tarea"),
        new(TipoNotificacionTrabajo.TareaComentario, CategoriaNotificacion.Tareas, false, "Comentario nuevo en una tarea"),
        new(TipoNotificacionTrabajo.TareaMencion, CategoriaNotificacion.Menciones, false, "Te mencionaron en un comentario")
    };

    private static readonly Dictionary<string, InfoTipoNotificacion> PorTipo = Tipos.ToDictionary(t => t.Tipo);

    public static InfoTipoNotificacion? Info(string tipo) => PorTipo.GetValueOrDefault(tipo);

    /// <summary>Categoría del tipo; los tipos desconocidos se agrupan por su prefijo.</summary>
    public static string CategoriaDe(string tipo) => Info(tipo)?.Categoria ?? tipo switch
    {
        _ when tipo.StartsWith("permiso.", StringComparison.Ordinal) => CategoriaNotificacion.Permisos,
        _ when tipo.StartsWith("proyecto.", StringComparison.Ordinal) => CategoriaNotificacion.Proyectos,
        _ => CategoriaNotificacion.Tareas
    };

    /// <summary>Los tipos desconocidos se tratan como obligatorios: ante la duda, no se silencia un aviso.</summary>
    public static bool EsObligatoria(string tipo) => Info(tipo)?.Obligatoria ?? true;

    public static IEnumerable<string> TiposDe(string categoria) => Tipos.Where(t => t.Categoria == categoria).Select(t => t.Tipo);
}

public static class TipoCambioLaboral
{
    public const string Ingreso = "Ingreso";
    public const string Puesto = "Puesto";
    public const string Departamento = "Departamento";
    public const string Supervisor = "Supervisor";
    public const string Estado = "Estado";
    public const string Baja = "Baja";
    public const string Reingreso = "Reingreso";
    public const string Perfil = "Perfil";
}

public static class ResultadoAcceso
{
    public const byte Exitoso = 1;
    public const byte CredencialesInvalidas = 2;
    public const byte CuentaBloqueada = 3;
    public const byte CuentaInactiva = 4;
    public const byte RefreshInvalido = 5;
}

public static class NombresRol
{
    public const string Administrador = "Administrador";
    public const string RecursosHumanos = "Recursos Humanos";
    public const string Supervisor = "Supervisor";
    public const string Trabajador = "Trabajador";
    /// <summary>Rol exclusivo de la empresa de plataforma; no forma parte de los roles iniciales de cada empresa.</summary>
    public const string AdministradorPlataforma = "Administrador de plataforma";

    public static readonly string[] Iniciales = { Administrador, RecursosHumanos, Supervisor, Trabajador };
}

// Códigos estables del catálogo global PermisosSistema.
public static class Permisos
{
    public const string EmpresaConsultar = "empresa.consultar";
    public const string EmpresaConfigurar = "empresa.configurar";
    public const string UsuariosConsultar = "usuarios.consultar";
    public const string UsuariosCrear = "usuarios.crear";
    public const string UsuariosEditar = "usuarios.editar";
    public const string UsuariosDesactivar = "usuarios.desactivar";
    public const string UsuariosRevocarSesiones = "usuarios.revocar_sesiones";
    public const string UsuariosRestablecerContrasena = "usuarios.restablecer_contrasena";
    public const string UsuariosDesbloquear = "usuarios.desbloquear";
    public const string PlataformaEmpresasConsultar = "plataforma.empresas.consultar";
    public const string PlataformaEmpresasCrear = "plataforma.empresas.crear";
    public const string PlataformaEmpresasCambiarEstado = "plataforma.empresas.cambiar_estado";
    public const string RolesConsultar = "roles.consultar";
    public const string RolesConfigurar = "roles.configurar";
    public const string AuditoriaConsultar = "auditoria.consultar";
    public const string AccesosConsultar = "accesos.consultar";
    public const string SesionesConsultar = "sesiones.consultar";
    public const string SesionesRevocar = "sesiones.revocar";

    // Bloque 3
    public const string TrabajadoresConsultar = "trabajadores.consultar";
    public const string TrabajadoresCrear = "trabajadores.crear";
    public const string TrabajadoresEditar = "trabajadores.editar";
    public const string TrabajadoresBaja = "trabajadores.baja";
    public const string TrabajadoresVerSensibles = "trabajadores.ver_sensibles";
    public const string DepartamentosConsultar = "departamentos.consultar";
    public const string DepartamentosGestionar = "departamentos.gestionar";
    public const string PuestosConsultar = "puestos.consultar";
    public const string PuestosGestionar = "puestos.gestionar";
    public const string SupervisoresGestionar = "supervisores.gestionar";
    public const string PerfilSolicitar = "perfil.solicitar";
    public const string PerfilResolverSolicitudes = "perfil.resolver_solicitudes";

    // Bloque 4
    public const string PermisosConfigurar = "permisos.configurar";
    public const string PermisosSolicitar = "permisos.solicitar";
    public const string PermisosConsultarTodos = "permisos.consultar_todos";
    public const string PermisosAprobar = "permisos.aprobar";
    public const string PermisosAprobarTodos = "permisos.aprobar_todos";
    public const string PermisosVerMotivoPrivado = "permisos.ver_motivo_privado";
    public const string SaldosConsultar = "saldos.consultar";
    public const string SaldosAjustar = "saldos.ajustar";

    // Bloque 5
    public const string AsistenciaRegistrar = "asistencia.registrar";
    public const string AsistenciaConsultarPropia = "asistencia.consultar_propia";
    public const string AsistenciaConsultar = "asistencia.consultar";
    public const string AsistenciaConsultarTodos = "asistencia.consultar_todos";
    public const string AsistenciaCorregir = "asistencia.corregir";
    public const string AsistenciaCorregirTodos = "asistencia.corregir_todos";
    public const string HorariosConsultar = "horarios.consultar";
    public const string HorariosConfigurar = "horarios.configurar";

    // Bloque 6
    public const string CalendarioConsultar = "calendario.consultar";
    public const string CalendarioGestionarEventos = "calendario.gestionar_eventos";
    public const string CalendarioConfigurar = "calendario.configurar";
    public const string CalendarioVerFechasRelevantes = "calendario.ver_fechas_relevantes";
    public const string CalendarioVerAusenciasTodos = "calendario.ver_ausencias_todos";

    // Bloque 7
    public const string ProyectosConsultar = "proyectos.consultar";
    public const string ProyectosConsultarTodos = "proyectos.consultar_todos";
    public const string ProyectosCrear = "proyectos.crear";
    public const string ProyectosModificar = "proyectos.modificar";
    public const string ProyectosAsignar = "proyectos.asignar";
    public const string ProyectosCambiarResponsable = "proyectos.cambiar_responsable";
    public const string ProyectosFinalizar = "proyectos.finalizar";
    public const string ProyectosReabrir = "proyectos.reabrir";
    public const string ProyectosConfigurarEstados = "proyectos.configurar_estados";
    public const string TareasGestionar = "tareas.gestionar";
    public const string TareasActualizarAvance = "tareas.actualizar_avance";
    public const string TareasRevisar = "tareas.revisar";
    public const string TareasComentar = "tareas.comentar";
    public const string NotificacionesConsultar = "notificaciones.consultar";

    // Bloque 8
    public const string TareasVerSeguimiento = "tareas.ver_seguimiento";

    // Bloque 10
    public const string ReportesConsultar = "reportes.consultar";
    public const string ReportesExportar = "reportes.exportar";
    public const string ErroresConsultar = "errores.consultar";

    /// <summary>Permisos del Bloque 10 (catálogo insertado por el script 011).</summary>
    public static readonly string[] Bloque10 = { ReportesConsultar, ReportesExportar, ErroresConsultar };

    /// <summary>Permisos del Bloque 8 (catálogo insertado por el script 009).</summary>
    public static readonly string[] Bloque8 = { TareasVerSeguimiento };

    /// <summary>Permisos del Bloque 7 (catálogo insertado por el script 008).</summary>
    public static readonly string[] Bloque7 =
    {
        ProyectosConsultar, ProyectosConsultarTodos, ProyectosCrear, ProyectosModificar, ProyectosAsignar, ProyectosCambiarResponsable,
        ProyectosFinalizar, ProyectosReabrir, ProyectosConfigurarEstados, TareasGestionar, TareasActualizarAvance, TareasRevisar,
        TareasComentar, NotificacionesConsultar
    };

    /// <summary>Permisos del Bloque 6 (catálogo insertado por el script 007).</summary>
    public static readonly string[] Bloque6 =
    {
        CalendarioConsultar, CalendarioGestionarEventos, CalendarioConfigurar, CalendarioVerFechasRelevantes, CalendarioVerAusenciasTodos
    };

    /// <summary>Permisos del Bloque 5 (catálogo insertado por el script 006).</summary>
    public static readonly string[] Bloque5 =
    {
        AsistenciaRegistrar, AsistenciaConsultarPropia, AsistenciaConsultar, AsistenciaConsultarTodos,
        AsistenciaCorregir, AsistenciaCorregirTodos, HorariosConsultar, HorariosConfigurar
    };

    /// <summary>Permisos del Bloque 4 (catálogo insertado por el script 005).</summary>
    public static readonly string[] Bloque4 =
    {
        PermisosConfigurar, PermisosSolicitar, PermisosConsultarTodos, PermisosAprobar, PermisosAprobarTodos,
        PermisosVerMotivoPrivado, SaldosConsultar, SaldosAjustar
    };

    /// <summary>Permisos del Bloque 3 (catálogo insertado por el script 004).</summary>
    public static readonly string[] Bloque3 =
    {
        TrabajadoresConsultar, TrabajadoresCrear, TrabajadoresEditar, TrabajadoresBaja, TrabajadoresVerSensibles,
        DepartamentosConsultar, DepartamentosGestionar, PuestosConsultar, PuestosGestionar,
        SupervisoresGestionar, PerfilSolicitar, PerfilResolverSolicitudes
    };

    public static readonly string[] Todos =
    {
        EmpresaConsultar, EmpresaConfigurar, UsuariosConsultar, UsuariosCrear, UsuariosEditar,
        UsuariosDesactivar, UsuariosRevocarSesiones, UsuariosRestablecerContrasena, UsuariosDesbloquear,
        RolesConsultar, RolesConfigurar, AuditoriaConsultar, AccesosConsultar, SesionesConsultar, SesionesRevocar,
        TrabajadoresConsultar, TrabajadoresCrear, TrabajadoresEditar, TrabajadoresBaja, TrabajadoresVerSensibles,
        DepartamentosConsultar, DepartamentosGestionar, PuestosConsultar, PuestosGestionar,
        SupervisoresGestionar, PerfilSolicitar, PerfilResolverSolicitudes,
        PermisosConfigurar, PermisosSolicitar, PermisosConsultarTodos, PermisosAprobar, PermisosAprobarTodos,
        PermisosVerMotivoPrivado, SaldosConsultar, SaldosAjustar,
        AsistenciaRegistrar, AsistenciaConsultarPropia, AsistenciaConsultar, AsistenciaConsultarTodos,
        AsistenciaCorregir, AsistenciaCorregirTodos, HorariosConsultar, HorariosConfigurar,
        CalendarioConsultar, CalendarioGestionarEventos, CalendarioConfigurar, CalendarioVerFechasRelevantes, CalendarioVerAusenciasTodos,
        ProyectosConsultar, ProyectosConsultarTodos, ProyectosCrear, ProyectosModificar, ProyectosAsignar, ProyectosCambiarResponsable,
        ProyectosFinalizar, ProyectosReabrir, ProyectosConfigurarEstados, TareasGestionar, TareasActualizarAvance, TareasRevisar,
        TareasComentar, NotificacionesConsultar, TareasVerSeguimiento, ReportesConsultar, ReportesExportar, ErroresConsultar
    };
}
/// <summary>Permisos exclusivos de la empresa de plataforma (nunca se asignan a roles de empresas normales).</summary>
public static class PermisosPlataforma
{
    public static readonly string[] Todos =
    {
        Permisos.PlataformaEmpresasConsultar, Permisos.PlataformaEmpresasCrear, Permisos.PlataformaEmpresasCambiarEstado
    };
}

// Permisos que recibe cada rol inicial al crear una empresa.
public static class PermisosPorDefecto
{
    public static IReadOnlyDictionary<string, string[]> PorRol { get; } = new Dictionary<string, string[]>
    {
        [NombresRol.Administrador] = Permisos.Todos,
        [NombresRol.RecursosHumanos] = new[]
        {
            Permisos.EmpresaConsultar, Permisos.UsuariosConsultar, Permisos.UsuariosCrear,
            Permisos.UsuariosEditar, Permisos.UsuariosDesactivar, Permisos.RolesConsultar,
            Permisos.TrabajadoresConsultar, Permisos.TrabajadoresCrear, Permisos.TrabajadoresEditar,
            Permisos.TrabajadoresBaja, Permisos.TrabajadoresVerSensibles,
            Permisos.DepartamentosConsultar, Permisos.DepartamentosGestionar,
            Permisos.PuestosConsultar, Permisos.PuestosGestionar,
            Permisos.SupervisoresGestionar, Permisos.PerfilSolicitar, Permisos.PerfilResolverSolicitudes,
            Permisos.PermisosConfigurar, Permisos.PermisosSolicitar, Permisos.PermisosConsultarTodos,
            Permisos.PermisosAprobar, Permisos.PermisosAprobarTodos, Permisos.PermisosVerMotivoPrivado,
            Permisos.SaldosConsultar, Permisos.SaldosAjustar,
            Permisos.AsistenciaRegistrar, Permisos.AsistenciaConsultarPropia, Permisos.AsistenciaConsultar,
            Permisos.AsistenciaConsultarTodos, Permisos.AsistenciaCorregir, Permisos.AsistenciaCorregirTodos,
            Permisos.HorariosConsultar, Permisos.HorariosConfigurar,
            Permisos.CalendarioConsultar, Permisos.CalendarioGestionarEventos, Permisos.CalendarioConfigurar, Permisos.CalendarioVerFechasRelevantes, Permisos.CalendarioVerAusenciasTodos,
            Permisos.ProyectosConsultar, Permisos.ProyectosCrear, Permisos.ProyectosModificar, Permisos.ProyectosAsignar, Permisos.ProyectosFinalizar,
            Permisos.TareasGestionar, Permisos.TareasActualizarAvance, Permisos.TareasRevisar, Permisos.TareasComentar, Permisos.NotificacionesConsultar, Permisos.TareasVerSeguimiento,
            Permisos.ReportesConsultar, Permisos.ReportesExportar
        },
        [NombresRol.Supervisor] = new[]
        {
            Permisos.EmpresaConsultar, Permisos.UsuariosConsultar, Permisos.TrabajadoresConsultar,
            Permisos.DepartamentosConsultar, Permisos.PuestosConsultar, Permisos.PerfilSolicitar,
            Permisos.PermisosSolicitar, Permisos.PermisosAprobar, Permisos.SaldosConsultar,
            Permisos.AsistenciaRegistrar, Permisos.AsistenciaConsultarPropia, Permisos.AsistenciaConsultar,
            Permisos.AsistenciaCorregir, Permisos.HorariosConsultar,
            Permisos.CalendarioConsultar, Permisos.CalendarioVerFechasRelevantes, Permisos.CalendarioVerAusenciasTodos,
            Permisos.ProyectosConsultar, Permisos.ProyectosCrear, Permisos.ProyectosModificar, Permisos.ProyectosAsignar, Permisos.ProyectosFinalizar,
            Permisos.TareasGestionar, Permisos.TareasActualizarAvance, Permisos.TareasRevisar, Permisos.TareasComentar, Permisos.NotificacionesConsultar, Permisos.TareasVerSeguimiento,
            Permisos.ReportesConsultar
        },
        [NombresRol.Trabajador] = new[]
        {
            Permisos.EmpresaConsultar, Permisos.TrabajadoresConsultar, Permisos.DepartamentosConsultar,
            Permisos.PuestosConsultar, Permisos.PerfilSolicitar, Permisos.PermisosSolicitar,
            Permisos.AsistenciaRegistrar, Permisos.AsistenciaConsultarPropia,
            Permisos.CalendarioConsultar, Permisos.CalendarioVerFechasRelevantes, Permisos.CalendarioVerAusenciasTodos,
            Permisos.ProyectosConsultar, Permisos.TareasActualizarAvance, Permisos.TareasComentar, Permisos.NotificacionesConsultar, Permisos.ReportesConsultar
        }
    };
}
