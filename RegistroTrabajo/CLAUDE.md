# Registro de Trabajo — contexto del proyecto

Aplicación web multiempresa para administrar trabajadores, permisos, asistencia, proyectos y tareas. Se construye por bloques; este archivo resume lo hecho hasta ahora (Bloque 1 cerrado) y las reglas para seguir.

## Stack y estructura
- .NET 9, ASP.NET Core Web API, EF Core 9 (SQL Server), Blazor WebAssembly. Solución: `RegistroTrabajo.sln`.
- Proyectos (arquitectura en capas; no reemplazarla):
  - `RegistroTrabajo.Data`: entidades (`Entities/`), `RegistroTrabajoDbContext` (+ `.Laboral.cs` parcial) y `Scripts/` SQL.
  - `RegistroTrabajo.Models`: DTOs (`Dtos.cs`) y constantes (`Constantes.cs`: estados, resultados, códigos de permiso, permisos por defecto). Sin dependencias.
  - `RegistroTrabajo.Services`: lógica de negocio, validaciones, autorización, sesiones, auditoría, correo. Interfaces + implementaciones, DI con `AddRegistroTrabajoServices()`.
  - `RegistroTrabajo.Api`: controladores, JWT, middlewares, límite de intentos, comandos de inicialización.
  - `RegistroTrabajo` (cliente Blazor WASM): login, inicio y pantallas de administración.
  - `RegistroTrabajo.Tests`: xUnit (143 pruebas).
- Base de datos: SQL Server local `RegistroTrabajo` (autenticación integrada). Sección de configuración de la API: `RegistroTrabajoApiSettings` (`DbSettings`, `JwtSettings`, `Security`, `Email`, `AllowedOrigins`).

## Bloque 1 (hecho): seguridad y administración
- Tablas: Empresas, ConfiguracionEmpresa, Usuarios, Roles, PermisosSistema, UsuarioRoles, RolPermisos, SesionesUsuario, HistorialAcceso, Auditoria, TokensRecuperacionContrasena, TokensConfirmacionCorreo.
- Multiempresa: la empresa sale **siempre** del JWT (`ICurrentUser`), nunca del cliente; datos de otra empresa responden 404. Llaves foráneas compuestas con `EmpresaId`; sin eliminaciones en cascada (NO ACTION); instantes técnicos en UTC.
- Autenticación: login por correo (único global, normalizado a MAYÚSCULAS) con JWT + refresh token rotativo (solo se guarda SHA-256). Hash con `PasswordHasher<Usuario>` de Identity. Bloqueo temporal por intentos fallidos; mensaje de error único ("Credenciales inválidas.").
- En **cada** solicitud se revalida en BD: sesión no revocada/expirada/inactiva, usuario y empresa activos; el permiso se consulta en BD (`[RequierePermiso("codigo")]`). Con `DebeCambiarContrasena` la API solo permite `/api/auth/me`, `cambiar-contrasena` y `logout`.
- Anti-elevación: no se modifican los propios roles; solo se gestionan roles cuyos permisos se poseen; los permisos del rol Administrador son inmutables; no se desactiva al último administrador.
- Plataforma: una única empresa con `EsPlataforma = 1`; sus administradores (permisos `plataforma.empresas.*`) crean empresas y las activan/suspenden/desactivan. Los permisos `plataforma.*` nunca valen en empresas normales.
- Roles de sistema por empresa: Administrador, Recursos Humanos, Supervisor, Trabajador (+ roles propios creables). Catálogo de permisos global en `PermisosSistema` (códigos estables como `usuarios.consultar`; los significados de Estado/Resultado están en `Constantes.cs`).
- Recuperación de contraseña y confirmación de correo con enlaces de un solo uso (solo hash del token); respuestas públicas que no revelan si la cuenta existe. Correo: `IEmailSender` modo `Dev` (archivos en `correos-dev/`) o `Smtp`.
- Auditoría de solo inserción desde la aplicación (redacta contraseñas/hashes/tokens; JSON validado). `HistorialAcceso` registra éxitos y fallos.
- Cliente Blazor: sesión en localStorage (`SesionStore`), `AuthHttpHandler` (JWT + renovación), `ApiClient` (ProblemDetails → `ApiResult`), clientes por área (`AdminClients.cs`), componentes `Modal`, `Paginador`, `Alerta`, `SoloConPermiso`; menú filtrado por permisos (solo visual).

## Bloque 2 (solo en Data, sin Service/API/App todavía)
El usuario creó 40 tablas laborales en la BD (Trabajadores, Departamentos, Puestos, Proyectos, Tareas, Horarios, RegistrosAsistencia, SolicitudesPermiso, SaldosPermiso, Notificaciones, etc.). Ya están como entidades en `Data/Entities/*.cs` y configuradas en `RegistroTrabajoDbContext.Laboral.cs` (generadas con EF scaffold y adaptadas: nombres en singular, NO ACTION, navegaciones inversas a Empresa/Usuario omitidas). Los servicios, endpoints y pantallas irán por etapas. Antes de empezar un módulo, leer su tabla real (columnas/CHECK/FK) en la BD.

## Bloque 3 (hecho; script 004 ya aplicado a la BD de desarrollo)
- Administración de trabajadores y estructura organizacional: departamentos, puestos, expediente, supervisores con vigencia, contactos de emergencia, historial laboral, baja/reingreso y solicitudes de cambio de perfil. Detalle y endpoints en `BLOQUE3.md`.
- Servicios nuevos: `DepartamentoService`/`PuestoService` (`CatalogoServices.cs`), `TrabajadorService`, `SupervisionService`, `ContactoEmergenciaService`, `SolicitudCambioPerfilService`. Controladores en `Bloque3Controllers.cs`; pantallas Blazor: Trabajadores, TrabajadorExpediente, Catalogos, SolicitudesPerfil, MiPerfil.
- Significado de estados (la BD solo limita rangos): `EstadoLaboral` 1 Activo/2 Inactivo/3 Baja; `SolicitudCambioPerfil.Estado` 1 Pendiente/2 Aprobada/3 Rechazada/4 Cancelada. Ver `Constantes.cs`.
- `TrabajadorSupervisor`: el scaffold tenía las navegaciones al revés; ahora `Supervisor` = quien supervisa (`SupervisorTrabajadorId`) y `Supervisado` = quien es supervisado (`TrabajadorId`). `FechaFin` es inclusiva.
- Permisos nuevos (script `004_bloque3_permisos.sql`, ya aplicado; los scripts de permisos hay que aplicarlos ANTES de levantar la API: el bootstrap exige el catálogo completo). Los datos personales/contactos/historial requieren `trabajadores.ver_sensibles` (o ser el propio trabajador); editar el expediente exige ese mismo permiso.

## Bloque 4 (hecho en código; falta aplicar el script 005 a la BD de desarrollo)
- Permisos, vacaciones y ausencias: tipos y reglas por empresa, solicitudes (días u horas) con validación de reglas, aprobación por supervisor vigente o `permisos.aprobar_todos`, saldos con libro de movimientos, cancelación de permisos aprobados, calendario de ausencias y notificaciones mínimas. Detalle, endpoints, significado de estados y límites en `BLOQUE4.md`.
- Servicios: `TiposPermisoService`, `CalculoDuracionService`, `SolicitudesPermisoService`, `AprobacionesPermisoService`, `SaldosPermisoService` (+ helpers en `PermisosCore.cs`). Controladores en `Bloque4Controllers.cs`; pantallas Blazor: SolicitarPermiso, AprobacionesPermisos, SaldosPermisos, ConfigPermisos.
- Significado de códigos numéricos (Estado 1..5, Decision 1..3, TipoMovimiento 1..4, UnidadBase 1..2) en `Constantes.cs`. La BD exige `Cantidad > 0` en movimientos: el signo lo da el tipo.

## Bloque 5 (hecho en código; falta aplicar el script 006 a la BD de desarrollo)
- Asistencia, horarios y turnos: entrada/salida con hora del servidor, turnos, horarios con vigencia y versiones, asignaciones y excepciones individuales, descansos, festivos, GPS opcional por empresa y correcciones que conservan el original. Detalle, endpoints, significado de estados y límites en `BLOQUE5.md`.
- Servicios: `TurnosService`, `HorariosService`, `CalendarioLaboralService` (`HorariosServices.cs`), `AsistenciaService`, `CorreccionesAsistenciaService` (`AsistenciaServices.cs`). Controladores en `Bloque5Controllers.cs`; pantallas Blazor: Asistencia, HistorialAsistencia, Horarios.
- Significado: `RegistroAsistencia.Estado` 1 Abierta/2 Completa/3 Anulada; `DescansoTrabajador.Tipo` 1 Habitual/2 Excepción (ver `Constantes.cs`). La fecha laboral usa la zona horaria de la empresa.

## Bloque 6 (hecho en código; falta aplicar el script 007 a la BD de desarrollo)
- Calendario laboral y eventos de la empresa: vista mensual, agenda del día y administración de eventos. Reúne eventos propios, festivos (Bloque 5), permisos aprobados (Bloque 4) y cumpleaños/aniversarios (Bloque 3) sin duplicar catálogos. Detalle, endpoints, permisos y límites en `BLOQUE6.md`.
- Servicios: `EventosEmpresaService` y `CalendarioService` (`CalendarioServices.cs`). Controladores en `Bloque6Controllers.cs`; pantallas Blazor: Calendario, EventosEmpresa.
- Fechas: los DTO usan siempre hora local de la empresa; día completo se guarda como fecha local sin zona, con hora se guarda en UTC. `EventoEmpresa.Estado` 1 Activo/2 Cancelado; `Visibilidad` 1 Empresa/2 Departamento; `Tipo` 1..5 (el script 007 amplía ese CHECK).
## Comandos útiles
```powershell
dotnet build RegistroTrabajo.sln
dotnet test RegistroTrabajo.Tests                       # incluye pruebas contra SQL Server local (se omiten sin conexión; RT_TEST_CONNECTION)
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project RegistroTrabajo.Api --no-launch-profile --urls http://localhost:5265
dotnet run --project RegistroTrabajo --no-launch-profile --urls http://localhost:5118   # cliente
dotnet run --project RegistroTrabajo.Api -- bootstrap-admin      # primera empresa (BOOTSTRAP_EMPRESA/ADMIN_EMAIL/ADMIN_PASSWORD)
dotnet run --project RegistroTrabajo.Api -- bootstrap-platform   # empresa de plataforma (BOOTSTRAP_PLATFORM_*)
dotnet run --project RegistroTrabajo.Api -- seed-dev-users       # solo Development; SEED_USERS_PASSWORD (+SEED_RESET_PASSWORD=true)
```
Puertos de desarrollo: API `http://localhost:5265`, cliente `http://localhost:5118` (CORS en `AllowedOrigins`). En desarrollo la API no redirige a HTTPS (rompería el preflight de CORS).

## Base de datos y scripts
- El esquema NO usa migraciones de EF: se gestiona con scripts SQL idempotentes en `RegistroTrabajo.Data/Scripts` (nunca se ejecutan solos).
  - `003_bloque1_faltantes.sql`: **aplicado** a la BD de desarrollo (EsPlataforma, CorreoConfirmadoUtc, tokens de confirmación, 5 permisos).
  - `004_bloque3_permisos.sql`: **aplicado** (12 permisos del Bloque 3 + asignación a roles de sistema existentes).
  - `005_bloque4.sql`: **pendiente de aplicar** a la BD de desarrollo (RowVersion y ClaveIdempotencia en SolicitudesPermiso, 2 índices únicos filtrados, 8 permisos del Bloque 4 + asignación a roles). Aplicar con `sqlcmd ... -I -i` (ver `BLOQUE4.md`). Las pruebas de SQL Server lo validan dentro de una transacción revertida.
- `006_bloque5.sql`: **pendiente de aplicar** a la BD de desarrollo (índice único de jornada abierta + 8 permisos del Bloque 5 con su asignación a roles). Aplicar con `sqlcmd ... -I -i` (ver `BLOQUE5.md`).
- `007_bloque6.sql`: **pendiente de aplicar** a la BD de desarrollo (estado, visibilidad, cancelación y RowVersion en EventosEmpresa, interruptores de fechas relevantes en ConfiguracionEmpresa, 4 permisos del Bloque 6 con su asignación a roles). Aplicar con `sqlcmd ... -I -i` (ver `BLOQUE6.md`).
- `001_auditoria_solo_insercion.sql`: **pendiente** (requiere DBA y un usuario de la API que no sea db_owner).
- Las pruebas de SQL Server aplican los scripts dentro de una transacción que siempre se revierte y comprueban que cada tabla/columna de la BD esté mapeada (y al revés): mantenerlas verdes al cambiar el modelo.
- Para regenerar entidades desde la BD se usó `dotnet ef dbcontext scaffold` en una carpeta temporal (fuera del repo) y un script de adaptación; no hay herramienta EF instalada globalmente.

## Convenciones
- Código en español (nombres de dominio), mensajes de error en español sin filtrar detalles internos. Errores de negocio con `ServiceException` (Validation/Unauthorized/Forbidden/NotFound/Conflict) → ProblemDetails en la API.
- Servicios async con `CancellationToken`; DTOs en las respuestas (nunca entidades); validar en el servidor.
- Auditar operaciones importantes con `IAuditoriaService.Registrar` (se guarda con el mismo SaveChanges).
- Sin credenciales por defecto en el repositorio; no registrar contraseñas, hashes ni tokens.
- No cambiar paquetes/versiones sin justificar. No eliminar tablas ni datos; cambios de esquema solo con script SQL idempotente y aditivo.
- Al editar archivos con PowerShell 5.1 usar `[IO.File]::ReadAllText(..., UTF8)`/`WriteAllText` (un `Get-Content`/`Set-Content` sin codificación corrompe los acentos).

## Pendiente / decisiones abiertas
- Script 001 (auditoría de solo inserción a nivel BD) y usuario de BD dedicado para la API.
- `appsettings.json` volvió a contener la clave JWT antigua: considerarla comprometida y moverla a user-secrets o variable de entorno (`RegistroTrabajoApiSettings__JwtSettings__SecretKey`). Los user-secrets solo se cargan en `Development`.
- `RegistroTrabajo.Api/dev-credenciales.local.txt` guarda contraseñas de desarrollo: borrar cuando se copien.
- Pruebas HTTP de extremo a extremo (WebApplicationFactory), detección de reutilización del refresh token, MFA y exportaciones: fuera del Bloque 1.
- Siguiente: resto de módulos del Bloque 2 (Proyectos, Tareas...) en Service → API → App, por etapas; usarán las relaciones de supervisión del Bloque 3 para aprobaciones.

Más detalle del Bloque 1 (endpoints, scripts, límites): `BLOQUE1.md`.
