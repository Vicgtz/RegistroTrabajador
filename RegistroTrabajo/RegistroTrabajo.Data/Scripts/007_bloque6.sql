/*
  RegistroTrabajo - Bloque 6: calendario laboral y eventos de la empresa.
  Idempotente y aditivo: no elimina tablas ni datos. Aplicar ANTES de ejecutar la versión nueva de la API
  (la inicialización de empresas exige que el catálogo PermisosSistema esté completo).
    sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i 007_bloque6.sql      (-I = QUOTED_IDENTIFIER ON, necesario para índices filtrados)

  Cambios:
   1. EventosEmpresa: Estado (activo/cancelado), visibilidad (empresa o departamento), datos de modificación y cancelación
      (historial básico) y RowVersion. El CHECK de Tipo se amplía de 1..2 a 1..5 (la tabla no tenía datos de otro uso).
   2. ConfiguracionEmpresa: interruptores para mostrar cumpleaños y aniversarios (apagados por defecto).
   3. Permisos nuevos en PermisosSistema y su asignación a los roles de sistema existentes.
  Todo lo que referencia columnas nuevas va en EXEC para que compile aunque la columna aún no exista.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
BEGIN TRANSACTION;

------------------------------------------------------------------ 1. EventosEmpresa
IF COL_LENGTH(N'dbo.EventosEmpresa', N'Estado') IS NULL
    EXEC (N'ALTER TABLE dbo.EventosEmpresa ADD Estado tinyint NOT NULL CONSTRAINT DF_EventosEmpresa_Estado DEFAULT (1)');
IF COL_LENGTH(N'dbo.EventosEmpresa', N'Visibilidad') IS NULL
    EXEC (N'ALTER TABLE dbo.EventosEmpresa ADD Visibilidad tinyint NOT NULL CONSTRAINT DF_EventosEmpresa_Visibilidad DEFAULT (1)');
IF COL_LENGTH(N'dbo.EventosEmpresa', N'DepartamentoId') IS NULL
    EXEC (N'ALTER TABLE dbo.EventosEmpresa ADD DepartamentoId uniqueidentifier NULL');
IF COL_LENGTH(N'dbo.EventosEmpresa', N'ModificadoPorUsuarioId') IS NULL
    EXEC (N'ALTER TABLE dbo.EventosEmpresa ADD ModificadoPorUsuarioId uniqueidentifier NULL');
IF COL_LENGTH(N'dbo.EventosEmpresa', N'FechaModificacionUtc') IS NULL
    EXEC (N'ALTER TABLE dbo.EventosEmpresa ADD FechaModificacionUtc datetime2(0) NULL');
IF COL_LENGTH(N'dbo.EventosEmpresa', N'CanceladoPorUsuarioId') IS NULL
    EXEC (N'ALTER TABLE dbo.EventosEmpresa ADD CanceladoPorUsuarioId uniqueidentifier NULL');
IF COL_LENGTH(N'dbo.EventosEmpresa', N'FechaCancelacionUtc') IS NULL
    EXEC (N'ALTER TABLE dbo.EventosEmpresa ADD FechaCancelacionUtc datetime2(0) NULL');
IF COL_LENGTH(N'dbo.EventosEmpresa', N'MotivoCancelacion') IS NULL
    EXEC (N'ALTER TABLE dbo.EventosEmpresa ADD MotivoCancelacion nvarchar(300) NULL');
IF COL_LENGTH(N'dbo.EventosEmpresa', N'RowVersion') IS NULL
    EXEC (N'ALTER TABLE dbo.EventosEmpresa ADD RowVersion rowversion NOT NULL');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_EventosEmpresa_Estado' AND parent_object_id = OBJECT_ID(N'dbo.EventosEmpresa'))
    EXEC (N'ALTER TABLE dbo.EventosEmpresa ADD CONSTRAINT CK_EventosEmpresa_Estado CHECK (Estado IN (1, 2))');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_EventosEmpresa_Visibilidad' AND parent_object_id = OBJECT_ID(N'dbo.EventosEmpresa'))
    EXEC (N'ALTER TABLE dbo.EventosEmpresa ADD CONSTRAINT CK_EventosEmpresa_Visibilidad CHECK (Visibilidad IN (1, 2))');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_EventosEmpresa_Grupo' AND parent_object_id = OBJECT_ID(N'dbo.EventosEmpresa'))
    EXEC (N'ALTER TABLE dbo.EventosEmpresa ADD CONSTRAINT CK_EventosEmpresa_Grupo
            CHECK ((Visibilidad = 1 AND DepartamentoId IS NULL) OR (Visibilidad = 2 AND DepartamentoId IS NOT NULL))');

-- Tipo: 1 Reunión general, 2 Capacitación, 3 Actividad interna, 4 Fecha importante, 5 Otro.
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_EventosEmpresa_Tipo' AND parent_object_id = OBJECT_ID(N'dbo.EventosEmpresa'))
BEGIN
    DECLARE @ckTipo sysname =
        (SELECT cc.name FROM sys.check_constraints cc
         JOIN sys.columns c ON c.object_id = cc.parent_object_id AND c.column_id = cc.parent_column_id
         WHERE cc.parent_object_id = OBJECT_ID(N'dbo.EventosEmpresa') AND c.name = N'Tipo');
    IF @ckTipo IS NOT NULL EXEC (N'ALTER TABLE dbo.EventosEmpresa DROP CONSTRAINT ' + N'[' + @ckTipo + N']');
    EXEC (N'ALTER TABLE dbo.EventosEmpresa ADD CONSTRAINT CK_EventosEmpresa_Tipo CHECK (Tipo BETWEEN 1 AND 5)');
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EventosEmpresa_Departamento')
    EXEC (N'ALTER TABLE dbo.EventosEmpresa ADD CONSTRAINT FK_EventosEmpresa_Departamento
            FOREIGN KEY (EmpresaId, DepartamentoId) REFERENCES dbo.Departamentos (EmpresaId, DepartamentoId)');
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EventosEmpresa_ModificadoPor')
    EXEC (N'ALTER TABLE dbo.EventosEmpresa ADD CONSTRAINT FK_EventosEmpresa_ModificadoPor
            FOREIGN KEY (EmpresaId, ModificadoPorUsuarioId) REFERENCES dbo.Usuarios (EmpresaId, UsuarioId)');
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EventosEmpresa_CanceladoPor')
    EXEC (N'ALTER TABLE dbo.EventosEmpresa ADD CONSTRAINT FK_EventosEmpresa_CanceladoPor
            FOREIGN KEY (EmpresaId, CanceladoPorUsuarioId) REFERENCES dbo.Usuarios (EmpresaId, UsuarioId)');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EventosEmpresa_Empresa_Fechas' AND object_id = OBJECT_ID(N'dbo.EventosEmpresa'))
    EXEC (N'CREATE INDEX IX_EventosEmpresa_Empresa_Fechas ON dbo.EventosEmpresa (EmpresaId, FechaInicio) INCLUDE (FechaFin, Estado)');

------------------------------------------------------------------ 2. ConfiguracionEmpresa
IF COL_LENGTH(N'dbo.ConfiguracionEmpresa', N'MostrarCumpleanos') IS NULL
    EXEC (N'ALTER TABLE dbo.ConfiguracionEmpresa ADD MostrarCumpleanos bit NOT NULL CONSTRAINT DF_ConfiguracionEmpresa_MostrarCumpleanos DEFAULT (0)');
IF COL_LENGTH(N'dbo.ConfiguracionEmpresa', N'MostrarAniversarios') IS NULL
    EXEC (N'ALTER TABLE dbo.ConfiguracionEmpresa ADD MostrarAniversarios bit NOT NULL CONSTRAINT DF_ConfiguracionEmpresa_MostrarAniversarios DEFAULT (0)');

------------------------------------------------------------------ 3. Catálogo de permisos
INSERT INTO dbo.PermisosSistema (Codigo, Modulo, Accion, Descripcion)
SELECT v.Codigo, v.Modulo, v.Accion, v.Descripcion
FROM (VALUES
    (N'calendario.consultar',             N'Calendario', N'Consultar',          N'Consultar el calendario de la empresa'),
    (N'calendario.gestionar_eventos',     N'Calendario', N'GestionarEventos',   N'Crear, editar y cancelar eventos de la empresa'),
    (N'calendario.configurar',            N'Calendario', N'Configurar',         N'Configurar qué fechas relevantes muestra el calendario'),
    (N'calendario.ver_fechas_relevantes', N'Calendario', N'VerFechasRelevantes', N'Ver cumpleaños y aniversarios laborales en el calendario')
) AS v (Codigo, Modulo, Accion, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PermisosSistema p WHERE p.Codigo = v.Codigo);

INSERT INTO dbo.RolPermisos (EmpresaId, RolId, PermisoId, Permitido)
SELECT r.EmpresaId, r.RolId, p.PermisoId, 1
FROM dbo.Roles r
JOIN (VALUES
    (N'Administrador',    N'calendario.consultar'), (N'Administrador',    N'calendario.gestionar_eventos'),
    (N'Administrador',    N'calendario.configurar'), (N'Administrador',   N'calendario.ver_fechas_relevantes'),
    (N'Recursos Humanos', N'calendario.consultar'), (N'Recursos Humanos', N'calendario.gestionar_eventos'),
    (N'Recursos Humanos', N'calendario.configurar'), (N'Recursos Humanos', N'calendario.ver_fechas_relevantes'),
    (N'Supervisor',       N'calendario.consultar'), (N'Supervisor',       N'calendario.ver_fechas_relevantes'),
    (N'Trabajador',       N'calendario.consultar'), (N'Trabajador',       N'calendario.ver_fechas_relevantes')
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
