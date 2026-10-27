/*
  RegistroTrabajo - Bloque 9: sistema de notificaciones.
  Idempotente y aditivo: no elimina tablas ni datos. Las tablas Notificaciones y PreferenciasNotificacion ya existían (Bloque 2).
    sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i 010_bloque9.sql      (-I = QUOTED_IDENTIFIER ON, necesario para índices filtrados)

  Cambios:
   1. Notificaciones.ClaveDeduplicacion + índice único filtrado: un mismo evento no genera dos avisos para el mismo usuario.
   2. PreferenciasNotificacion.Prioritaria: el usuario puede marcar una categoría o tipo como prioritario.
   3. Índice de consulta por tipo (filtro de categorías) en la bandeja.
  Sin permisos nuevos: notificaciones.consultar (Bloque 7) basta, porque la bandeja es siempre propia.
  Todo lo que referencia columnas nuevas va en EXEC para que compile aunque la columna aún no exista.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
BEGIN TRANSACTION;

------------------------------------------------------------------ 1. Deduplicación
IF COL_LENGTH(N'dbo.Notificaciones', N'ClaveDeduplicacion') IS NULL
    EXEC (N'ALTER TABLE dbo.Notificaciones ADD ClaveDeduplicacion nvarchar(200) NULL');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Notificaciones_Clave' AND object_id = OBJECT_ID(N'dbo.Notificaciones'))
    EXEC (N'CREATE UNIQUE INDEX UX_Notificaciones_Clave ON dbo.Notificaciones (EmpresaId, UsuarioDestinoId, ClaveDeduplicacion) WHERE ClaveDeduplicacion IS NOT NULL');

------------------------------------------------------------------ 2. Prioridad en preferencias
IF COL_LENGTH(N'dbo.PreferenciasNotificacion', N'Prioritaria') IS NULL
    EXEC (N'ALTER TABLE dbo.PreferenciasNotificacion ADD Prioritaria bit NOT NULL CONSTRAINT DF_PreferenciasNotificacion_Prioritaria DEFAULT (0)');

------------------------------------------------------------------ 3. Índice por tipo
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notificaciones_Tipo' AND object_id = OBJECT_ID(N'dbo.Notificaciones'))
    EXEC (N'CREATE INDEX IX_Notificaciones_Tipo ON dbo.Notificaciones (EmpresaId, UsuarioDestinoId, Tipo, CreadaUtc)');

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
