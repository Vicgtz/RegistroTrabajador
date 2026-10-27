/*
  RegistroTrabajo - Bloque 10: panel principal, reportes y seguimiento administrativo.
  Idempotente y aditivo: no elimina tablas ni datos. Aplicar ANTES de ejecutar la versión nueva de la API
  (la inicialización de empresas exige que el catálogo PermisosSistema esté completo).
    sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i 011_bloque10.sql

  Cambios:
   1. Permisos reportes.consultar (todos los roles de sistema), reportes.exportar (Administrador y Recursos Humanos)
      y errores.consultar (Administrador), y su asignación a los roles de sistema existentes.
   2. Índices de consulta para Borradores (por creador y módulo) y ErroresTecnicos (por empresa y fecha).
  El Dashboard y los reportes no necesitan tablas propias; DefinicionesReporte no se usa en esta versión.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
BEGIN TRANSACTION;

------------------------------------------------------------------ 1. Permisos
INSERT INTO dbo.PermisosSistema (Codigo, Modulo, Accion, Descripcion)
SELECT v.Codigo, v.Modulo, v.Accion, v.Descripcion
FROM (VALUES
    (N'reportes.consultar', N'Reportes', N'Consultar', N'Consultar reportes dentro de su alcance (propio, equipo o empresa)'),
    (N'reportes.exportar',  N'Reportes', N'Exportar',  N'Exportar reportes a Excel y PDF'),
    (N'errores.consultar',  N'Errores',  N'Consultar', N'Consultar el registro de errores técnicos de la API')
) AS v (Codigo, Modulo, Accion, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PermisosSistema p WHERE p.Codigo = v.Codigo);

INSERT INTO dbo.RolPermisos (EmpresaId, RolId, PermisoId, Permitido)
SELECT r.EmpresaId, r.RolId, p.PermisoId, 1
FROM dbo.Roles r
JOIN (
    SELECT N'Administrador' AS Rol, Codigo FROM (VALUES (N'reportes.consultar'), (N'reportes.exportar'), (N'errores.consultar')) a (Codigo)
    UNION ALL
    SELECT N'Recursos Humanos', Codigo FROM (VALUES (N'reportes.consultar'), (N'reportes.exportar')) h (Codigo)
    UNION ALL
    SELECT N'Supervisor', Codigo FROM (VALUES (N'reportes.consultar')) s (Codigo)
    UNION ALL
    SELECT N'Trabajador', Codigo FROM (VALUES (N'reportes.consultar')) t (Codigo)
) AS m ON m.Rol = r.Nombre
JOIN dbo.PermisosSistema p ON p.Codigo = m.Codigo
WHERE r.EsSistema = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.RolPermisos rp WHERE rp.RolId = r.RolId AND rp.PermisoId = p.PermisoId);

------------------------------------------------------------------ 2. Índices
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Borradores_Usuario' AND object_id = OBJECT_ID(N'dbo.Borradores'))
    EXEC (N'CREATE INDEX IX_Borradores_Usuario ON dbo.Borradores (EmpresaId, UsuarioCreadorId, Modulo, FechaActualizacionUtc)');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ErroresTecnicos_Fecha' AND object_id = OBJECT_ID(N'dbo.ErroresTecnicos'))
    EXEC (N'CREATE INDEX IX_ErroresTecnicos_Fecha ON dbo.ErroresTecnicos (EmpresaId, FechaUtc)');

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
