/*
  RegistroTrabajo - Bloque 11: respaldos, recuperación y continuidad.
  Idempotente y aditivo: no elimina tablas ni datos. Aplicar ANTES de ejecutar la versión nueva de la API
  (la inicialización de la empresa de plataforma exige que el catálogo PermisosSistema esté completo).
    sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i 012_bloque11.sql

  Cambios:
   1. Permisos plataforma.respaldos.consultar y plataforma.respaldos.solicitar y su asignación a los roles de la empresa
      de plataforma (EsPlataforma = 1). Ninguna empresa normal los recibe.
   2. OperacionesRespaldo: columna HashArchivo y CHECK de TipoOperacion ampliado a 1..4
      (1 Completo, 2 Diferencial, 3 Restauración, 4 Verificación). Estado sigue en 1..3 (1 Iniciado, 2 Completado, 3 Fallido).
   3. Índice de consulta por fecha de inicio.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
BEGIN TRANSACTION;

------------------------------------------------------------------ 1. Permisos de plataforma
INSERT INTO dbo.PermisosSistema (Codigo, Modulo, Accion, Descripcion)
SELECT v.Codigo, v.Modulo, v.Accion, v.Descripcion
FROM (VALUES
    (N'plataforma.respaldos.consultar', N'Plataforma', N'ConsultarRespaldos', N'Consultar el historial de respaldos de la base de datos'),
    (N'plataforma.respaldos.solicitar', N'Plataforma', N'SolicitarRespaldos', N'Solicitar y verificar respaldos de la base de datos')
) AS v (Codigo, Modulo, Accion, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PermisosSistema p WHERE p.Codigo = v.Codigo);

INSERT INTO dbo.RolPermisos (EmpresaId, RolId, PermisoId, Permitido)
SELECT r.EmpresaId, r.RolId, p.PermisoId, 1
FROM dbo.Roles r
JOIN dbo.Empresas e ON e.EmpresaId = r.EmpresaId AND e.EsPlataforma = 1
JOIN dbo.PermisosSistema p ON p.Codigo IN (N'plataforma.respaldos.consultar', N'plataforma.respaldos.solicitar')
WHERE r.EsSistema = 1 AND r.Nombre = N'Administrador de plataforma'
  AND NOT EXISTS (SELECT 1 FROM dbo.RolPermisos rp WHERE rp.RolId = r.RolId AND rp.PermisoId = p.PermisoId);

------------------------------------------------------------------ 2. OperacionesRespaldo
IF COL_LENGTH(N'dbo.OperacionesRespaldo', N'HashArchivo') IS NULL
    EXEC (N'ALTER TABLE dbo.OperacionesRespaldo ADD HashArchivo nvarchar(128) NULL');

-- El CHECK original limita TipoOperacion a 1..2; se reemplaza por 1..4 (se localiza por su definición, no por su nombre).
DECLARE @ck sysname = (
    SELECT TOP (1) cc.name FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID(N'dbo.OperacionesRespaldo')
      AND cc.definition LIKE N'%TipoOperacion%' AND cc.name <> N'CK_OperacionesRespaldo_Tipo');
IF @ck IS NOT NULL EXEC (N'ALTER TABLE dbo.OperacionesRespaldo DROP CONSTRAINT ' + @ck);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_OperacionesRespaldo_Tipo' AND parent_object_id = OBJECT_ID(N'dbo.OperacionesRespaldo'))
    EXEC (N'ALTER TABLE dbo.OperacionesRespaldo ADD CONSTRAINT CK_OperacionesRespaldo_Tipo CHECK (TipoOperacion BETWEEN 1 AND 4)');

------------------------------------------------------------------ 3. Índices
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OperacionesRespaldo_Fecha' AND object_id = OBJECT_ID(N'dbo.OperacionesRespaldo'))
    EXEC (N'CREATE INDEX IX_OperacionesRespaldo_Fecha ON dbo.OperacionesRespaldo (FechaInicioUtc)');

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
