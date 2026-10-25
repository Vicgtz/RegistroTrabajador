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
        PermisosVerMotivoPrivado, SaldosConsultar, SaldosAjustar
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
            Permisos.SaldosConsultar, Permisos.SaldosAjustar
        },
        [NombresRol.Supervisor] = new[]
        {
            Permisos.EmpresaConsultar, Permisos.UsuariosConsultar, Permisos.TrabajadoresConsultar,
            Permisos.DepartamentosConsultar, Permisos.PuestosConsultar, Permisos.PerfilSolicitar,
            Permisos.PermisosSolicitar, Permisos.PermisosAprobar, Permisos.SaldosConsultar
        },
        [NombresRol.Trabajador] = new[]
        {
            Permisos.EmpresaConsultar, Permisos.TrabajadoresConsultar, Permisos.DepartamentosConsultar,
            Permisos.PuestosConsultar, Permisos.PerfilSolicitar, Permisos.PermisosSolicitar
        }
    };
}
