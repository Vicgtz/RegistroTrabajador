/*
  RegistroTrabajo - Bloque 12: configuración de la empresa y mantenimiento.
  Idempotente y aditivo: no elimina tablas ni datos. Aplicar ANTES de ejecutar la versión nueva de la API
  (la inicialización de empresas exige que el catálogo PermisosSistema esté completo).
    sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i 013_bloque12.sql

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
