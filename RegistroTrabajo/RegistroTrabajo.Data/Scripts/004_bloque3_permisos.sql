/*
  RegistroTrabajo - Bloque 3: permisos de administración de trabajadores y estructura organizacional.
  Idempotente y aditivo: no elimina ni recrea nada. Aplicar ANTES de ejecutar la versión nueva de la API
  (la inicialización de empresas exige que el catálogo PermisosSistema esté completo).
    sqlcmd -S localhost -d RegistroTrabajo -E -b -i 004_bloque3_permisos.sql

  Cambios:
   1. Permisos nuevos en PermisosSistema.
   2. Asignación a los roles de sistema que ya existen en cada empresa (Administrador, Recursos Humanos,
      Supervisor, Trabajador) según los valores por defecto de la aplicación.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
BEGIN TRANSACTION;

------------------------------------------------------------------ 1. Catálogo de permisos
INSERT INTO dbo.PermisosSistema (Codigo, Modulo, Accion, Descripcion)
SELECT v.Codigo, v.Modulo, v.Accion, v.Descripcion
FROM (VALUES
    (N'trabajadores.consultar',     N'Trabajadores',   N'Consultar',          N'Consultar el directorio de trabajadores (datos básicos)'),
    (N'trabajadores.crear',         N'Trabajadores',   N'Crear',              N'Registrar nuevos trabajadores'),
    (N'trabajadores.editar',        N'Trabajadores',   N'Editar',             N'Editar el expediente y cambiar puesto, departamento o estado'),
    (N'trabajadores.baja',          N'Trabajadores',   N'Baja',               N'Dar de baja o reingresar trabajadores'),
    (N'trabajadores.ver_sensibles', N'Trabajadores',   N'VerSensibles',       N'Ver datos personales y contactos de emergencia'),
    (N'departamentos.consultar',    N'Departamentos',  N'Consultar',          N'Consultar departamentos'),
    (N'departamentos.gestionar',    N'Departamentos',  N'Gestionar',          N'Crear, editar y desactivar departamentos'),
    (N'puestos.consultar',          N'Puestos',        N'Consultar',          N'Consultar puestos'),
    (N'puestos.gestionar',          N'Puestos',        N'Gestionar',          N'Crear, editar y desactivar puestos'),
    (N'supervisores.gestionar',     N'Supervisores',   N'Gestionar',          N'Asignar y finalizar supervisores de un trabajador'),
    (N'perfil.solicitar',           N'Perfil',         N'Solicitar',          N'Solicitar cambios en el propio perfil'),
    (N'perfil.resolver_solicitudes',N'Perfil',         N'ResolverSolicitudes',N'Aprobar o rechazar solicitudes de cambio de perfil')
) AS v (Codigo, Modulo, Accion, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PermisosSistema p WHERE p.Codigo = v.Codigo);

------------------------------------------------------------------ 2. Asignación a roles de sistema existentes
INSERT INTO dbo.RolPermisos (EmpresaId, RolId, PermisoId, Permitido)
SELECT r.EmpresaId, r.RolId, p.PermisoId, 1
FROM dbo.Roles r
JOIN (VALUES
    (N'Administrador',    N'trabajadores.consultar'),     (N'Administrador',    N'trabajadores.crear'),
    (N'Administrador',    N'trabajadores.editar'),        (N'Administrador',    N'trabajadores.baja'),
    (N'Administrador',    N'trabajadores.ver_sensibles'), (N'Administrador',    N'departamentos.consultar'),
    (N'Administrador',    N'departamentos.gestionar'),    (N'Administrador',    N'puestos.consultar'),
    (N'Administrador',    N'puestos.gestionar'),          (N'Administrador',    N'supervisores.gestionar'),
    (N'Administrador',    N'perfil.solicitar'),           (N'Administrador',    N'perfil.resolver_solicitudes'),
    (N'Recursos Humanos', N'trabajadores.consultar'),     (N'Recursos Humanos', N'trabajadores.crear'),
    (N'Recursos Humanos', N'trabajadores.editar'),        (N'Recursos Humanos', N'trabajadores.baja'),
    (N'Recursos Humanos', N'trabajadores.ver_sensibles'), (N'Recursos Humanos', N'departamentos.consultar'),
    (N'Recursos Humanos', N'departamentos.gestionar'),    (N'Recursos Humanos', N'puestos.consultar'),
    (N'Recursos Humanos', N'puestos.gestionar'),          (N'Recursos Humanos', N'supervisores.gestionar'),
    (N'Recursos Humanos', N'perfil.solicitar'),           (N'Recursos Humanos', N'perfil.resolver_solicitudes'),
    (N'Supervisor',       N'trabajadores.consultar'),     (N'Supervisor',       N'departamentos.consultar'),
    (N'Supervisor',       N'puestos.consultar'),          (N'Supervisor',       N'perfil.solicitar'),
    (N'Trabajador',       N'trabajadores.consultar'),     (N'Trabajador',       N'departamentos.consultar'),
    (N'Trabajador',       N'puestos.consultar'),          (N'Trabajador',       N'perfil.solicitar')
) AS m (Rol, Codigo) ON m.Rol = r.Nombre
JOIN dbo.PermisosSistema p ON p.Codigo = m.Codigo
WHERE r.EsSistema = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.RolPermisos rp WHERE rp.RolId = r.RolId AND rp.PermisoId = p.PermisoId);

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
