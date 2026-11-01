/*
  RegistroTrabajo - El rol de sistema Trabajador ya no consulta reportes.
  Idempotente; no elimina filas: marca Permitido = 0 en la asignación reportes.consultar del rol Trabajador de cada empresa
  (el script 011 la había concedido). Los demás roles no cambian. Las empresas nuevas ya no reciben el permiso (PermisosPorDefecto).
    sqlcmd -S localhost -d RegistroTrabajo -E -C -b -I -i 015_quitar_reportes_trabajador.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

UPDATE rp
SET rp.Permitido = 0
FROM dbo.RolPermisos rp
JOIN dbo.Roles r ON r.RolId = rp.RolId
JOIN dbo.PermisosSistema p ON p.PermisoId = rp.PermisoId
WHERE r.EsSistema = 1 AND r.Nombre = N'Trabajador' AND p.Codigo = N'reportes.consultar' AND rp.Permitido = 1;
