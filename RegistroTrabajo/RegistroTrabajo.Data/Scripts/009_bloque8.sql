/*
  RegistroTrabajo - Bloque 8: seguimiento avanzado de tareas y actividades.
  Idempotente y aditivo: no elimina tablas ni datos. Aplicar ANTES de ejecutar la versión nueva de la API
  (la inicialización de empresas exige que el catálogo PermisosSistema esté completo). Requiere el script 008.
    sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i 009_bloque8.sql      (-I = QUOTED_IDENTIFIER ON)

  Cambios:
   1. EventosSeguimientoTarea: línea de tiempo de operaciones (fecha límite, asignaciones, revisión, reapertura...) con valores anterior/nuevo.
      El avance y los cambios de estado siguen en HistorialAvanceTarea (Bloque 7): no se duplican.
   2. RevisionesTarea: cada revisión del responsable (aprobada/devuelta) con su observación.
   3. ConfiguracionEmpresa.DiasAlertaVencimiento y DiasSinActualizacion (criterios de alerta por empresa).
   4. Índice de consulta de tareas por estado y vencimiento.
   5. Permiso tareas.ver_seguimiento y su asignación a los roles de sistema.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
BEGIN TRANSACTION;

------------------------------------------------------------------ 1. EventosSeguimientoTarea
IF OBJECT_ID(N'dbo.EventosSeguimientoTarea', N'U') IS NULL
BEGIN
    EXEC (N'
    CREATE TABLE dbo.EventosSeguimientoTarea
    (
        EventoSeguimientoTareaId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_EventosSeguimientoTarea PRIMARY KEY,
        EmpresaId         uniqueidentifier NOT NULL,
        TareaId           uniqueidentifier NOT NULL,
        Tipo              varchar(40)      NOT NULL,
        UsuarioId         uniqueidentifier NOT NULL,
        FechaUtc          datetime2(0)     NOT NULL CONSTRAINT DF_EventosSeguimientoTarea_Fecha DEFAULT (sysutcdatetime()),
        ValorAnteriorJson nvarchar(2000)   NULL,
        ValorNuevoJson    nvarchar(2000)   NULL,
        Detalle           nvarchar(1000)   NULL,
        CONSTRAINT FK_EventosSeguimientoTarea_Tarea   FOREIGN KEY (EmpresaId, TareaId)   REFERENCES dbo.Tareas (EmpresaId, TareaId),
        CONSTRAINT FK_EventosSeguimientoTarea_Usuario FOREIGN KEY (EmpresaId, UsuarioId) REFERENCES dbo.Usuarios (EmpresaId, UsuarioId)
    )');
    EXEC (N'CREATE INDEX IX_EventosSeguimientoTarea_Tarea ON dbo.EventosSeguimientoTarea (EmpresaId, TareaId, FechaUtc)');
END;

------------------------------------------------------------------ 2. RevisionesTarea
IF OBJECT_ID(N'dbo.RevisionesTarea', N'U') IS NULL
BEGIN
    EXEC (N'
    CREATE TABLE dbo.RevisionesTarea
    (
        RevisionTareaId     bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RevisionesTarea PRIMARY KEY,
        EmpresaId           uniqueidentifier NOT NULL,
        TareaId             uniqueidentifier NOT NULL,
        RevisorUsuarioId    uniqueidentifier NOT NULL,
        Resultado           tinyint          NOT NULL CONSTRAINT CK_RevisionesTarea_Resultado CHECK (Resultado BETWEEN 1 AND 2),
        Observacion         nvarchar(2000)   NULL,
        PorcentajeAlRevisar tinyint          NOT NULL CONSTRAINT CK_RevisionesTarea_Porcentaje CHECK (PorcentajeAlRevisar BETWEEN 0 AND 100),
        FechaUtc            datetime2(0)     NOT NULL CONSTRAINT DF_RevisionesTarea_Fecha DEFAULT (sysutcdatetime()),
        CONSTRAINT FK_RevisionesTarea_Tarea   FOREIGN KEY (EmpresaId, TareaId)         REFERENCES dbo.Tareas (EmpresaId, TareaId),
        CONSTRAINT FK_RevisionesTarea_Usuario FOREIGN KEY (EmpresaId, RevisorUsuarioId) REFERENCES dbo.Usuarios (EmpresaId, UsuarioId)
    )');
    EXEC (N'CREATE INDEX IX_RevisionesTarea_Tarea ON dbo.RevisionesTarea (EmpresaId, TareaId, FechaUtc)');
END;

------------------------------------------------------------------ 3. Criterios de alerta por empresa
IF COL_LENGTH(N'dbo.ConfiguracionEmpresa', N'DiasAlertaVencimiento') IS NULL
    EXEC (N'ALTER TABLE dbo.ConfiguracionEmpresa ADD DiasAlertaVencimiento tinyint NOT NULL CONSTRAINT DF_ConfiguracionEmpresa_DiasAlerta DEFAULT (3)');
IF COL_LENGTH(N'dbo.ConfiguracionEmpresa', N'DiasSinActualizacion') IS NULL
    EXEC (N'ALTER TABLE dbo.ConfiguracionEmpresa ADD DiasSinActualizacion smallint NULL');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ConfiguracionEmpresa_DiasAlerta' AND parent_object_id = OBJECT_ID(N'dbo.ConfiguracionEmpresa'))
    EXEC (N'ALTER TABLE dbo.ConfiguracionEmpresa ADD CONSTRAINT CK_ConfiguracionEmpresa_DiasAlerta CHECK (DiasAlertaVencimiento BETWEEN 0 AND 30)');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ConfiguracionEmpresa_DiasSinAct' AND parent_object_id = OBJECT_ID(N'dbo.ConfiguracionEmpresa'))
    EXEC (N'ALTER TABLE dbo.ConfiguracionEmpresa ADD CONSTRAINT CK_ConfiguracionEmpresa_DiasSinAct CHECK (DiasSinActualizacion IS NULL OR DiasSinActualizacion BETWEEN 1 AND 365)');

------------------------------------------------------------------ 4. Índice
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tareas_Empresa_Estado_Vence' AND object_id = OBJECT_ID(N'dbo.Tareas'))
    EXEC (N'CREATE INDEX IX_Tareas_Empresa_Estado_Vence ON dbo.Tareas (EmpresaId, Estado, FechaVencimiento)');

------------------------------------------------------------------ 5. Permiso
INSERT INTO dbo.PermisosSistema (Codigo, Modulo, Accion, Descripcion)
SELECT v.Codigo, v.Modulo, v.Accion, v.Descripcion
FROM (VALUES
    (N'tareas.ver_seguimiento', N'Tareas', N'VerSeguimiento', N'Consultar el panel de seguimiento (retrasos, revisiones pendientes) de las tareas que administra')
) AS v (Codigo, Modulo, Accion, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PermisosSistema p WHERE p.Codigo = v.Codigo);

INSERT INTO dbo.RolPermisos (EmpresaId, RolId, PermisoId, Permitido)
SELECT r.EmpresaId, r.RolId, p.PermisoId, 1
FROM dbo.Roles r
JOIN dbo.PermisosSistema p ON p.Codigo = N'tareas.ver_seguimiento'
WHERE r.EsSistema = 1 AND r.Nombre IN (N'Administrador', N'Recursos Humanos', N'Supervisor')
  AND NOT EXISTS (SELECT 1 FROM dbo.RolPermisos rp WHERE rp.RolId = r.RolId AND rp.PermisoId = p.PermisoId);

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
