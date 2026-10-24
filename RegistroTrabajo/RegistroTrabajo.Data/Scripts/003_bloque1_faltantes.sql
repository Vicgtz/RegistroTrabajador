/*
  RegistroTrabajo - Bloque 1: faltantes (administrador de plataforma, confirmación de correo, nuevos permisos).
  Idempotente y aditivo: no elimina ni recrea nada. Aplicar ANTES de ejecutar la versión nueva de la API.
    sqlcmd -S localhost -d RegistroTrabajo -E -b -i 003_bloque1_faltantes.sql

  Cambios:
   1. Empresas.EsPlataforma (bit): marca la única empresa de plataforma, cuyos administradores dan de alta empresas.
   2. Usuarios.CorreoConfirmadoUtc (datetime2 NULL). Los usuarios que ya existen se marcan como confirmados.
   3. Tabla TokensConfirmacionCorreo (espejo de TokensRecuperacionContrasena).
   4. Permisos nuevos en PermisosSistema + asignación a los roles Administrador existentes (los permisos de
      plataforma NO se asignan a ningún rol de empresa).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
BEGIN TRANSACTION;

------------------------------------------------------------------ 1. Empresas.EsPlataforma
IF COL_LENGTH(N'dbo.Empresas', N'EsPlataforma') IS NULL
    EXEC (N'ALTER TABLE dbo.Empresas ADD EsPlataforma bit NOT NULL CONSTRAINT DF_Empresas_EsPlataforma DEFAULT (0);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Empresas_Plataforma' AND object_id = OBJECT_ID(N'dbo.Empresas'))
    EXEC (N'CREATE UNIQUE INDEX UX_Empresas_Plataforma ON dbo.Empresas (EsPlataforma) WHERE EsPlataforma = 1;');

------------------------------------------------------------------ 2. Usuarios.CorreoConfirmadoUtc
IF COL_LENGTH(N'dbo.Usuarios', N'CorreoConfirmadoUtc') IS NULL
BEGIN
    EXEC (N'ALTER TABLE dbo.Usuarios ADD CorreoConfirmadoUtc datetime2(0) NULL;');
    EXEC (N'UPDATE dbo.Usuarios SET CorreoConfirmadoUtc = SYSUTCDATETIME() WHERE CorreoConfirmadoUtc IS NULL;');
END;

------------------------------------------------------------------ 3. TokensConfirmacionCorreo
IF OBJECT_ID(N'dbo.TokensConfirmacionCorreo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TokensConfirmacionCorreo (
        TokenConfirmacionId uniqueidentifier NOT NULL CONSTRAINT DF_TokenConf_Id DEFAULT NEWSEQUENTIALID(),
        EmpresaId           uniqueidentifier NOT NULL,
        UsuarioId           uniqueidentifier NOT NULL,
        TokenHash           varbinary(32)    NOT NULL,
        CreadoUtc           datetime2(0)     NOT NULL CONSTRAINT DF_TokenConf_CreadoUtc DEFAULT SYSUTCDATETIME(),
        ExpiraUtc           datetime2(0)     NOT NULL,
        UsadoUtc            datetime2(0)     NULL,
        RevocadoUtc         datetime2(0)     NULL,
        CONSTRAINT PK_TokensConfirmacionCorreo PRIMARY KEY (TokenConfirmacionId),
        CONSTRAINT UX_TokenConf_Hash UNIQUE (TokenHash),
        CONSTRAINT CK_TokenConf_Fechas CHECK (ExpiraUtc > CreadoUtc),
        CONSTRAINT FK_TokenConf_Usuario FOREIGN KEY (EmpresaId, UsuarioId) REFERENCES dbo.Usuarios (EmpresaId, UsuarioId)
    );
    CREATE INDEX IX_TokenConf_Usuario ON dbo.TokensConfirmacionCorreo (EmpresaId, UsuarioId);
END;

------------------------------------------------------------------ 4. Permisos nuevos
INSERT INTO dbo.PermisosSistema (Codigo, Modulo, Accion, Descripcion)
SELECT v.Codigo, v.Modulo, v.Accion, v.Descripcion
FROM (VALUES
    (N'plataforma.empresas.consultar',      N'Plataforma', N'ConsultarEmpresas',     N'Consultar todas las empresas de la plataforma'),
    (N'plataforma.empresas.crear',          N'Plataforma', N'CrearEmpresas',         N'Dar de alta empresas y su administrador'),
    (N'plataforma.empresas.cambiar_estado', N'Plataforma', N'CambiarEstadoEmpresas', N'Activar, suspender o desactivar empresas'),
    (N'usuarios.restablecer_contrasena',    N'Usuarios',   N'RestablecerContrasena', N'Restablecer la contraseña de otro usuario'),
    (N'usuarios.desbloquear',               N'Usuarios',   N'Desbloquear',           N'Quitar el bloqueo temporal de una cuenta')
) AS v (Codigo, Modulo, Accion, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PermisosSistema p WHERE p.Codigo = v.Codigo);

-- Los administradores existentes reciben los permisos de gestión de usuarios (no los de plataforma).
INSERT INTO dbo.RolPermisos (EmpresaId, RolId, PermisoId, Permitido)
SELECT r.EmpresaId, r.RolId, p.PermisoId, 1
FROM dbo.Roles r
CROSS JOIN dbo.PermisosSistema p
WHERE r.EsSistema = 1 AND r.Nombre = N'Administrador'
  AND p.Codigo IN (N'usuarios.restablecer_contrasena', N'usuarios.desbloquear')
  AND NOT EXISTS (SELECT 1 FROM dbo.RolPermisos rp WHERE rp.RolId = r.RolId AND rp.PermisoId = p.PermisoId);

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
