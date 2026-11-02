/*
  RegistroTrabajo - Cambios pendientes de base de datos: scripts 008 a 013 (Bloques 7 a 12) en un solo archivo.
  Idempotente y aditivo: no elimina tablas ni datos. Se detiene ante el primer error (:on error exit).
  Hacer respaldo antes. Aplicar con sqlcmd (-I = QUOTED_IDENTIFIER ON, necesario para los indices filtrados):
    sqlcmd -S localhost -d RegistroTrabajo -E -C -f 65001 -b -I -i consolidado_008_a_013.sql
  Contenido de cada bloque: ver el encabezado del script original (008..013) mas abajo.
*/
:on error exit
USE [RegistroTrabajo];
GO
PRINT 'Aplicando 008_bloque7...';
GO
/*
  RegistroTrabajo - Bloque 7: gestión de proyectos y tareas.
  Idempotente y aditivo: no elimina tablas ni datos. Aplicar ANTES de ejecutar la versión nueva de la API
  (la inicialización de empresas exige que el catálogo PermisosSistema esté completo).
    sqlcmd -S localhost -d RegistroTrabajo -E -f 65001 -b -I -i 008_bloque7.sql      (-I = QUOTED_IDENTIFIER ON, necesario para índices filtrados)

  Cambios:
   1. Tareas.Prioridad (1 Baja, 2 Media, 3 Alta, 4 Urgente), igual que Proyectos.Prioridad.
   2. Índices de consulta para tareas por proyecto, mis tareas y comentarios por tarea.
   3. Estados de proyecto por defecto para las empresas que aún no tienen (Planeación, En proceso, En pausa, Finalizado, Cancelado).
      Las empresas nuevas los reciben la primera vez que se consultan (EstadosProyectoService).
   4. Permisos nuevos en PermisosSistema y su asignación a los roles de sistema existentes.
  Todo lo que referencia columnas nuevas va en EXEC para que compile aunque la columna aún no exista.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
BEGIN TRANSACTION;

------------------------------------------------------------------ 1. Tareas.Prioridad
IF COL_LENGTH(N'dbo.Tareas', N'Prioridad') IS NULL
    EXEC (N'ALTER TABLE dbo.Tareas ADD Prioridad tinyint NOT NULL CONSTRAINT DF_Tareas_Prioridad DEFAULT (2)');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Tareas_Prioridad' AND parent_object_id = OBJECT_ID(N'dbo.Tareas'))
    EXEC (N'ALTER TABLE dbo.Tareas ADD CONSTRAINT CK_Tareas_Prioridad CHECK (Prioridad BETWEEN 1 AND 4)');

------------------------------------------------------------------ 2. Índices
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tareas_Empresa_Proyecto' AND object_id = OBJECT_ID(N'dbo.Tareas'))
    EXEC (N'CREATE INDEX IX_Tareas_Empresa_Proyecto ON dbo.Tareas (EmpresaId, ProyectoId) INCLUDE (Estado, PorcentajeAvance, FechaVencimiento)');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AsignacionesTarea_Trabajador' AND object_id = OBJECT_ID(N'dbo.AsignacionesTarea'))
    EXEC (N'CREATE INDEX IX_AsignacionesTarea_Trabajador ON dbo.AsignacionesTarea (EmpresaId, TrabajadorId, FechaRetiroUtc)');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Comentarios_Tarea' AND object_id = OBJECT_ID(N'dbo.Comentarios'))
    EXEC (N'CREATE INDEX IX_Comentarios_Tarea ON dbo.Comentarios (EmpresaId, TareaId, FechaCreacionUtc)');

------------------------------------------------------------------ 3. Estados de proyecto por defecto
INSERT INTO dbo.EstadosProyecto (EstadoProyectoId, EmpresaId, Nombre, Orden, EsFinal, Estado)
SELECT NEWID(), e.EmpresaId, v.Nombre, v.Orden, v.EsFinal, 1
FROM dbo.Empresas e
CROSS JOIN (VALUES
    (N'Planeación',  1, 0),
    (N'En proceso',  2, 0),
    (N'En pausa',    3, 0),
    (N'Finalizado',  4, 1),
    (N'Cancelado',   5, 1)
) AS v (Nombre, Orden, EsFinal)
WHERE NOT EXISTS (SELECT 1 FROM dbo.EstadosProyecto s WHERE s.EmpresaId = e.EmpresaId);

------------------------------------------------------------------ 4. Catálogo de permisos
INSERT INTO dbo.PermisosSistema (Codigo, Modulo, Accion, Descripcion)
SELECT v.Codigo, v.Modulo, v.Accion, v.Descripcion
FROM (VALUES
    (N'proyectos.consultar',          N'Proyectos',      N'Consultar',         N'Consultar los proyectos en los que participa (y los de sus supervisados si puede modificarlos)'),
    (N'proyectos.consultar_todos',    N'Proyectos',      N'ConsultarTodos',    N'Consultar y administrar todos los proyectos de la empresa'),
    (N'proyectos.crear',              N'Proyectos',      N'Crear',             N'Crear proyectos'),
    (N'proyectos.modificar',          N'Proyectos',      N'Modificar',         N'Modificar la información general de los proyectos que administra'),
    (N'proyectos.asignar',            N'Proyectos',      N'Asignar',           N'Asignar y retirar trabajadores de los proyectos que administra'),
    (N'proyectos.cambiar_responsable',N'Proyectos',      N'CambiarResponsable',N'Cambiar al responsable principal de un proyecto'),
    (N'proyectos.finalizar',          N'Proyectos',      N'Finalizar',         N'Finalizar o cancelar los proyectos que administra'),
    (N'proyectos.reabrir',            N'Proyectos',      N'Reabrir',           N'Reabrir un proyecto finalizado'),
    (N'proyectos.configurar_estados', N'Proyectos',      N'ConfigurarEstados', N'Configurar los estados de proyecto de la empresa'),
    (N'tareas.gestionar',             N'Tareas',         N'Gestionar',         N'Crear, editar, asignar y cancelar tareas de los proyectos que administra'),
    (N'tareas.actualizar_avance',     N'Tareas',         N'ActualizarAvance',  N'Actualizar el avance de las tareas asignadas'),
    (N'tareas.revisar',               N'Tareas',         N'Revisar',           N'Aprobar o devolver tareas en revisión y reabrir tareas cerradas'),
    (N'tareas.comentar',              N'Tareas',         N'Comentar',          N'Publicar comentarios en las tareas'),
    (N'notificaciones.consultar',     N'Notificaciones', N'Consultar',         N'Consultar la bandeja de notificaciones propia')
) AS v (Codigo, Modulo, Accion, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PermisosSistema p WHERE p.Codigo = v.Codigo);

INSERT INTO dbo.RolPermisos (EmpresaId, RolId, PermisoId, Permitido)
SELECT r.EmpresaId, r.RolId, p.PermisoId, 1
FROM dbo.Roles r
JOIN (
    SELECT N'Administrador' AS Rol, Codigo FROM (VALUES
        (N'proyectos.consultar'), (N'proyectos.consultar_todos'), (N'proyectos.crear'), (N'proyectos.modificar'), (N'proyectos.asignar'),
        (N'proyectos.cambiar_responsable'), (N'proyectos.finalizar'), (N'proyectos.reabrir'), (N'proyectos.configurar_estados'),
        (N'tareas.gestionar'), (N'tareas.actualizar_avance'), (N'tareas.revisar'), (N'tareas.comentar'), (N'notificaciones.consultar')) a (Codigo)
    UNION ALL
    SELECT N'Recursos Humanos', Codigo FROM (VALUES
        (N'proyectos.consultar'), (N'proyectos.crear'), (N'proyectos.modificar'), (N'proyectos.asignar'), (N'proyectos.finalizar'),
        (N'tareas.gestionar'), (N'tareas.actualizar_avance'), (N'tareas.revisar'), (N'tareas.comentar'), (N'notificaciones.consultar')) h (Codigo)
    UNION ALL
    SELECT N'Supervisor', Codigo FROM (VALUES
        (N'proyectos.consultar'), (N'proyectos.crear'), (N'proyectos.modificar'), (N'proyectos.asignar'), (N'proyectos.finalizar'),
        (N'tareas.gestionar'), (N'tareas.actualizar_avance'), (N'tareas.revisar'), (N'tareas.comentar'), (N'notificaciones.consultar')) s (Codigo)
    UNION ALL
    SELECT N'Trabajador', Codigo FROM (VALUES
        (N'proyectos.consultar'), (N'tareas.actualizar_avance'), (N'tareas.comentar'), (N'notificaciones.consultar')) t (Codigo)
) AS m ON m.Rol = r.Nombre
JOIN dbo.PermisosSistema p ON p.Codigo = m.Codigo
WHERE r.EsSistema = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.RolPermisos rp WHERE rp.RolId = r.RolId AND rp.PermisoId = p.PermisoId);

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
PRINT 'Aplicando 009_bloque8...';
GO
/*
  RegistroTrabajo - Bloque 8: seguimiento avanzado de tareas y actividades.
  Idempotente y aditivo: no elimina tablas ni datos. Aplicar ANTES de ejecutar la versión nueva de la API
  (la inicialización de empresas exige que el catálogo PermisosSistema esté completo). Requiere el script 008.
    sqlcmd -S localhost -d RegistroTrabajo -E -f 65001 -b -I -i 009_bloque8.sql      (-I = QUOTED_IDENTIFIER ON)

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
GO
PRINT 'Aplicando 010_bloque9...';
GO
/*
  RegistroTrabajo - Bloque 9: sistema de notificaciones.
  Idempotente y aditivo: no elimina tablas ni datos. Las tablas Notificaciones y PreferenciasNotificacion ya existían (Bloque 2).
    sqlcmd -S localhost -d RegistroTrabajo -E -f 65001 -b -I -i 010_bloque9.sql      (-I = QUOTED_IDENTIFIER ON, necesario para índices filtrados)

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
GO
PRINT 'Aplicando 011_bloque10...';
GO
/*
  RegistroTrabajo - Bloque 10: panel principal, reportes y seguimiento administrativo.
  Idempotente y aditivo: no elimina tablas ni datos. Aplicar ANTES de ejecutar la versión nueva de la API
  (la inicialización de empresas exige que el catálogo PermisosSistema esté completo).
    sqlcmd -S localhost -d RegistroTrabajo -E -f 65001 -b -I -i 011_bloque10.sql

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
GO
PRINT 'Aplicando 012_bloque11...';
GO
/*
  RegistroTrabajo - Bloque 11: respaldos, recuperación y continuidad.
  Idempotente y aditivo: no elimina tablas ni datos. Aplicar ANTES de ejecutar la versión nueva de la API
  (la inicialización de la empresa de plataforma exige que el catálogo PermisosSistema esté completo).
    sqlcmd -S localhost -d RegistroTrabajo -E -f 65001 -b -I -i 012_bloque11.sql

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
GO
PRINT 'Aplicando 013_bloque12...';
GO
/*
  RegistroTrabajo - Bloque 12: configuración de la empresa y mantenimiento.
  Idempotente y aditivo: no elimina tablas ni datos. Aplicar ANTES de ejecutar la versión nueva de la API
  (la inicialización de empresas exige que el catálogo PermisosSistema esté completo).
    sqlcmd -S localhost -d RegistroTrabajo -E -f 65001 -b -I -i 013_bloque12.sql

  Cambios:
   1. ConfiguracionEmpresa: NombreMostrado, TextoInstitucional, PrimerDiaSemana (0 domingo .. 6 sábado; por defecto 1 lunes) y Moneda.
   2. Tabla LogosEmpresa (un logotipo por empresa, máx. 512 KB) para servirlo por la API sin archivos en disco.
   3. Permisos empresa.ver_permisos_efectivos (rol Administrador de cada empresa) y plataforma.sistema.consultar
      (rol Administrador de plataforma de la empresa de plataforma).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
BEGIN TRANSACTION;

------------------------------------------------------------------ 1. ConfiguracionEmpresa
IF COL_LENGTH(N'dbo.ConfiguracionEmpresa', N'NombreMostrado') IS NULL
    EXEC (N'ALTER TABLE dbo.ConfiguracionEmpresa ADD NombreMostrado nvarchar(100) NULL');
IF COL_LENGTH(N'dbo.ConfiguracionEmpresa', N'TextoInstitucional') IS NULL
    EXEC (N'ALTER TABLE dbo.ConfiguracionEmpresa ADD TextoInstitucional nvarchar(500) NULL');
IF COL_LENGTH(N'dbo.ConfiguracionEmpresa', N'PrimerDiaSemana') IS NULL
    EXEC (N'ALTER TABLE dbo.ConfiguracionEmpresa ADD PrimerDiaSemana tinyint NOT NULL CONSTRAINT DF_ConfiguracionEmpresa_PrimerDia DEFAULT (1)');
IF COL_LENGTH(N'dbo.ConfiguracionEmpresa', N'Moneda') IS NULL
    EXEC (N'ALTER TABLE dbo.ConfiguracionEmpresa ADD Moneda char(3) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ConfiguracionEmpresa_PrimerDia' AND parent_object_id = OBJECT_ID(N'dbo.ConfiguracionEmpresa'))
    EXEC (N'ALTER TABLE dbo.ConfiguracionEmpresa ADD CONSTRAINT CK_ConfiguracionEmpresa_PrimerDia CHECK (PrimerDiaSemana BETWEEN 0 AND 6)');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ConfiguracionEmpresa_Moneda' AND parent_object_id = OBJECT_ID(N'dbo.ConfiguracionEmpresa'))
    EXEC (N'ALTER TABLE dbo.ConfiguracionEmpresa ADD CONSTRAINT CK_ConfiguracionEmpresa_Moneda CHECK (Moneda IS NULL OR Moneda LIKE ''[A-Z][A-Z][A-Z]'')');

------------------------------------------------------------------ 2. LogosEmpresa
IF OBJECT_ID(N'dbo.LogosEmpresa', N'U') IS NULL
    EXEC (N'CREATE TABLE dbo.LogosEmpresa (
        EmpresaId uniqueidentifier NOT NULL CONSTRAINT PK_LogosEmpresa PRIMARY KEY,
        Contenido varbinary(max) NOT NULL,
        TipoContenido nvarchar(50) NOT NULL,
        TamanoBytes int NOT NULL CONSTRAINT CK_LogosEmpresa_Tamano CHECK (TamanoBytes BETWEEN 1 AND 524288),
        HashSha256 char(64) NOT NULL,
        FechaActualizacionUtc datetime2(0) NOT NULL CONSTRAINT DF_LogosEmpresa_Fecha DEFAULT (sysutcdatetime()),
        CONSTRAINT FK_LogosEmpresa_Empresas FOREIGN KEY (EmpresaId) REFERENCES dbo.Empresas (EmpresaId)
    )');

------------------------------------------------------------------ 3. Permisos
INSERT INTO dbo.PermisosSistema (Codigo, Modulo, Accion, Descripcion)
SELECT v.Codigo, v.Modulo, v.Accion, v.Descripcion
FROM (VALUES
    (N'empresa.ver_permisos_efectivos', N'Empresa',    N'VerPermisosEfectivos', N'Consultar los roles y permisos efectivos de un usuario de la empresa'),
    (N'plataforma.sistema.consultar',   N'Plataforma', N'ConsultarSistema',     N'Consultar el estado técnico del sistema')
) AS v (Codigo, Modulo, Accion, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PermisosSistema p WHERE p.Codigo = v.Codigo);

INSERT INTO dbo.RolPermisos (EmpresaId, RolId, PermisoId, Permitido)
SELECT r.EmpresaId, r.RolId, p.PermisoId, 1
FROM dbo.Roles r
JOIN dbo.Empresas e ON e.EmpresaId = r.EmpresaId
JOIN dbo.PermisosSistema p ON
     (p.Codigo = N'empresa.ver_permisos_efectivos' AND r.Nombre = N'Administrador')
  OR (p.Codigo = N'plataforma.sistema.consultar' AND r.Nombre = N'Administrador de plataforma' AND e.EsPlataforma = 1)
WHERE r.EsSistema = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.RolPermisos rp WHERE rp.RolId = r.RolId AND rp.PermisoId = p.PermisoId);

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
PRINT 'Scripts 008 a 013 aplicados.';
GO
