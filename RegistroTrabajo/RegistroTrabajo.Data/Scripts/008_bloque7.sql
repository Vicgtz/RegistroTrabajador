/*
  RegistroTrabajo - Bloque 7: gestión de proyectos y tareas.
  Idempotente y aditivo: no elimina tablas ni datos. Aplicar ANTES de ejecutar la versión nueva de la API
  (la inicialización de empresas exige que el catálogo PermisosSistema esté completo).
    sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i 008_bloque7.sql      (-I = QUOTED_IDENTIFIER ON, necesario para índices filtrados)

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
