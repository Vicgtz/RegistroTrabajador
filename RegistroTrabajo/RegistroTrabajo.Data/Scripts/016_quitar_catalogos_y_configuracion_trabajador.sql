/*
  RegistroTrabajo - El rol de sistema Trabajador ya no ve Departamentos y puestos ni Configuración de la empresa.
  Idempotente; no elimina filas: marca Permitido = 0 en las asignaciones empresa.consultar, departamentos.consultar y
  puestos.consultar del rol Trabajador de cada empresa. Los demás roles no cambian. Las empresas nuevas ya no las reciben (PermisosPorDefecto).
    sqlcmd -S localhost -d RegistroTrabajo -E -C -b -I -i 016_quitar_catalogos_y_configuracion_trabajador.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

UPDATE rp
SET rp.Permitido = 0
FROM dbo.RolPermisos rp
JOIN dbo.Roles r ON r.RolId = rp.RolId
JOIN dbo.PermisosSistema p ON p.PermisoId = rp.PermisoId
WHERE r.EsSistema = 1 AND r.Nombre = N'Trabajador'
  AND p.Codigo IN (N'empresa.consultar', N'departamentos.consultar', N'puestos.consultar')
  AND rp.Permitido = 1;
