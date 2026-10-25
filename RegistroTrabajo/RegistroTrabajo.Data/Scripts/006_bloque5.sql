/*
  RegistroTrabajo - Bloque 5: asistencia, horarios y turnos.
  Idempotente y aditivo: no elimina ni recrea nada. Aplicar ANTES de ejecutar la versión nueva de la API
  (la inicialización de empresas exige que el catálogo PermisosSistema esté completo).
    sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i 006_bloque5.sql      (-I = QUOTED_IDENTIFIER ON, necesario para índices filtrados)

  Cambios:
   1. Índice único filtrado en RegistrosAsistencia: un trabajador solo puede tener una jornada abierta
      (Estado = 1 y sin salida) a la vez. Es la última defensa contra entradas duplicadas concurrentes.
   2. Permisos nuevos en PermisosSistema y su asignación a los roles de sistema existentes.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
BEGIN TRANSACTION;

------------------------------------------------------------------ 1. Una sola jornada abierta por trabajador
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Asistencia_JornadaAbierta' AND object_id = OBJECT_ID(N'dbo.RegistrosAsistencia'))
    EXEC (N'CREATE UNIQUE INDEX UX_Asistencia_JornadaAbierta
            ON dbo.RegistrosAsistencia (EmpresaId, TrabajadorId)
            WHERE Estado = 1 AND SalidaUtc IS NULL');

------------------------------------------------------------------ 2. Catálogo de permisos
INSERT INTO dbo.PermisosSistema (Codigo, Modulo, Accion, Descripcion)
SELECT v.Codigo, v.Modulo, v.Accion, v.Descripcion
FROM (VALUES
    (N'asistencia.registrar',        N'Asistencia', N'Registrar',       N'Registrar la propia entrada y salida'),
    (N'asistencia.consultar_propia', N'Asistencia', N'ConsultarPropia', N'Consultar el propio historial de asistencia'),
    (N'asistencia.consultar',        N'Asistencia', N'Consultar',       N'Consultar la asistencia de los trabajadores que supervisa'),
    (N'asistencia.consultar_todos',  N'Asistencia', N'ConsultarTodos',  N'Consultar la asistencia de toda la empresa'),
    (N'asistencia.corregir',         N'Asistencia', N'Corregir',        N'Corregir registros de los trabajadores que supervisa'),
    (N'asistencia.corregir_todos',   N'Asistencia', N'CorregirTodos',   N'Corregir registros de cualquier trabajador de la empresa'),
    (N'horarios.consultar',          N'Horarios',   N'Consultar',       N'Consultar turnos, horarios, descansos y festivos'),
    (N'horarios.configurar',         N'Horarios',   N'Configurar',      N'Configurar turnos, horarios, asignaciones, descansos y festivos')
) AS v (Codigo, Modulo, Accion, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PermisosSistema p WHERE p.Codigo = v.Codigo);

INSERT INTO dbo.RolPermisos (EmpresaId, RolId, PermisoId, Permitido)
SELECT r.EmpresaId, r.RolId, p.PermisoId, 1
FROM dbo.Roles r
JOIN (VALUES
    (N'Administrador',    N'asistencia.registrar'),       (N'Administrador',    N'asistencia.consultar_propia'),
    (N'Administrador',    N'asistencia.consultar'),       (N'Administrador',    N'asistencia.consultar_todos'),
    (N'Administrador',    N'asistencia.corregir'),        (N'Administrador',    N'asistencia.corregir_todos'),
    (N'Administrador',    N'horarios.consultar'),         (N'Administrador',    N'horarios.configurar'),
    (N'Recursos Humanos', N'asistencia.registrar'),       (N'Recursos Humanos', N'asistencia.consultar_propia'),
    (N'Recursos Humanos', N'asistencia.consultar'),       (N'Recursos Humanos', N'asistencia.consultar_todos'),
    (N'Recursos Humanos', N'asistencia.corregir'),        (N'Recursos Humanos', N'asistencia.corregir_todos'),
    (N'Recursos Humanos', N'horarios.consultar'),         (N'Recursos Humanos', N'horarios.configurar'),
    (N'Supervisor',       N'asistencia.registrar'),       (N'Supervisor',       N'asistencia.consultar_propia'),
    (N'Supervisor',       N'asistencia.consultar'),       (N'Supervisor',       N'asistencia.corregir'),
    (N'Supervisor',       N'horarios.consultar'),
    (N'Trabajador',       N'asistencia.registrar'),       (N'Trabajador',       N'asistencia.consultar_propia')
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
