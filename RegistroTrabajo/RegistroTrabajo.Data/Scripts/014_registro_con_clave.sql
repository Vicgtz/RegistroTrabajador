/*
  RegistroTrabajo - Bloque 13: registro de cuentas con clave de registro por empresa.
  Idempotente y aditivo: no elimina tablas ni datos. Requiere los scripts anteriores (hasta el 013).
    sqlcmd -S localhost -d RegistroTrabajo -E -C -f 65001 -b -I -i 014_registro_con_clave.sql      (-I = QUOTED_IDENTIFIER ON, necesario para el índice filtrado)

  Cambios:
   1. ConfiguracionEmpresa.ClaveRegistroHash (SHA-256 de la clave; NULL = registro desactivado) y ClaveRegistroGeneradaUtc.
      La clave en claro nunca se guarda: solo se muestra una vez al generarla.
   2. Índice único filtrado sobre ClaveRegistroHash: una clave identifica una sola empresa.
  Sin permisos nuevos: administrar la clave exige empresa.configurar.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
BEGIN TRANSACTION;

------------------------------------------------------------------ 1. ConfiguracionEmpresa
IF COL_LENGTH(N'dbo.ConfiguracionEmpresa', N'ClaveRegistroHash') IS NULL
    EXEC (N'ALTER TABLE dbo.ConfiguracionEmpresa ADD ClaveRegistroHash varbinary(32) NULL');
IF COL_LENGTH(N'dbo.ConfiguracionEmpresa', N'ClaveRegistroGeneradaUtc') IS NULL
    EXEC (N'ALTER TABLE dbo.ConfiguracionEmpresa ADD ClaveRegistroGeneradaUtc datetime2(0) NULL');

------------------------------------------------------------------ 2. Índice único filtrado
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ConfigEmpresa_ClaveRegistro' AND object_id = OBJECT_ID(N'dbo.ConfiguracionEmpresa'))
    EXEC (N'CREATE UNIQUE INDEX UX_ConfigEmpresa_ClaveRegistro ON dbo.ConfiguracionEmpresa (ClaveRegistroHash) WHERE ClaveRegistroHash IS NOT NULL');

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
