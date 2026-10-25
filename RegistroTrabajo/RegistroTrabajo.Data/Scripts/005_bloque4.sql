/*
  RegistroTrabajo - Bloque 4: permisos, vacaciones y ausencias.
  Idempotente y aditivo: no elimina ni recrea nada. Aplicar ANTES de ejecutar la versión nueva de la API
  (la inicialización de empresas exige que el catálogo PermisosSistema esté completo).
    sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i 005_bloque4.sql      (-I = QUOTED_IDENTIFIER ON, necesario para índices filtrados)

  Cambios:
   1. SolicitudesPermiso: RowVersion (concurrencia) y ClaveIdempotencia (un reintento no duplica la solicitud).
   2. Índice único filtrado en SolicitudesPermiso (EmpresaId, TrabajadorId, ClaveIdempotencia).
   3. Índice único filtrado en MovimientosSaldoPermiso: un consumo y una devolución como máximo por solicitud.
   4. Permisos nuevos en PermisosSistema y su asignación a los roles de sistema existentes.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
BEGIN TRANSACTION;

------------------------------------------------------------------ 1. Columnas nuevas
IF COL_LENGTH('dbo.SolicitudesPermiso', 'ClaveIdempotencia') IS NULL
    ALTER TABLE dbo.SolicitudesPermiso ADD ClaveIdempotencia NVARCHAR(64) NULL;

IF COL_LENGTH('dbo.SolicitudesPermiso', 'RowVersion') IS NULL
    ALTER TABLE dbo.SolicitudesPermiso ADD RowVersion ROWVERSION NOT NULL;

------------------------------------------------------------------ 2 y 3. Índices únicos filtrados
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_SolicitudPermiso_Idempotencia' AND object_id = OBJECT_ID(N'dbo.SolicitudesPermiso'))
    EXEC (N'CREATE UNIQUE INDEX UX_SolicitudPermiso_Idempotencia
            ON dbo.SolicitudesPermiso (EmpresaId, TrabajadorId, ClaveIdempotencia)
            WHERE ClaveIdempotencia IS NOT NULL');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_MovimientoSaldo_Solicitud_Tipo' AND object_id = OBJECT_ID(N'dbo.MovimientosSaldoPermiso'))
    EXEC (N'CREATE UNIQUE INDEX UX_MovimientoSaldo_Solicitud_Tipo
            ON dbo.MovimientosSaldoPermiso (SolicitudPermisoId, TipoMovimiento)
            WHERE SolicitudPermisoId IS NOT NULL AND TipoMovimiento IN (2, 3)');

------------------------------------------------------------------ 4. Catálogo de permisos
INSERT INTO dbo.PermisosSistema (Codigo, Modulo, Accion, Descripcion)
SELECT v.Codigo, v.Modulo, v.Accion, v.Descripcion
FROM (VALUES
    (N'permisos.configurar',          N'Permisos', N'Configurar',        N'Configurar tipos de permiso y sus reglas'),
    (N'permisos.solicitar',           N'Permisos', N'Solicitar',         N'Solicitar permisos y ausencias propios'),
    (N'permisos.consultar_todos',     N'Permisos', N'ConsultarTodos',    N'Consultar las solicitudes de permiso de toda la empresa'),
    (N'permisos.aprobar',             N'Permisos', N'Aprobar',           N'Aprobar o rechazar permisos de los trabajadores que supervisa'),
    (N'permisos.aprobar_todos',       N'Permisos', N'AprobarTodos',      N'Aprobar o rechazar permisos de cualquier trabajador de la empresa'),
    (N'permisos.ver_motivo_privado',  N'Permisos', N'VerMotivoPrivado',  N'Ver el motivo privado (salud, familia) de una solicitud'),
    (N'saldos.consultar',             N'Saldos',   N'Consultar',         N'Consultar saldos de permisos de otros trabajadores'),
    (N'saldos.ajustar',               N'Saldos',   N'Ajustar',           N'Asignar y ajustar saldos de permisos')
) AS v (Codigo, Modulo, Accion, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PermisosSistema p WHERE p.Codigo = v.Codigo);

INSERT INTO dbo.RolPermisos (EmpresaId, RolId, PermisoId, Permitido)
SELECT r.EmpresaId, r.RolId, p.PermisoId, 1
FROM dbo.Roles r
JOIN (VALUES
    (N'Administrador',    N'permisos.configurar'),      (N'Administrador',    N'permisos.solicitar'),
    (N'Administrador',    N'permisos.consultar_todos'), (N'Administrador',    N'permisos.aprobar'),
    (N'Administrador',    N'permisos.aprobar_todos'),   (N'Administrador',    N'permisos.ver_motivo_privado'),
    (N'Administrador',    N'saldos.consultar'),         (N'Administrador',    N'saldos.ajustar'),
    (N'Recursos Humanos', N'permisos.configurar'),      (N'Recursos Humanos', N'permisos.solicitar'),
    (N'Recursos Humanos', N'permisos.consultar_todos'), (N'Recursos Humanos', N'permisos.aprobar'),
    (N'Recursos Humanos', N'permisos.aprobar_todos'),   (N'Recursos Humanos', N'permisos.ver_motivo_privado'),
    (N'Recursos Humanos', N'saldos.consultar'),         (N'Recursos Humanos', N'saldos.ajustar'),
    (N'Supervisor',       N'permisos.solicitar'),       (N'Supervisor',       N'permisos.aprobar'),
    (N'Supervisor',       N'saldos.consultar'),
    (N'Trabajador',       N'permisos.solicitar')
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
