/*
  RegistroTrabajo - Bloque 14: expediente profesional del trabajador (estudios, habilidades y currículum).
  Idempotente y aditivo: no elimina tablas ni datos. Aplicar ANTES de ejecutar la versión nueva de la API
  (la inicialización de empresas exige que el catálogo PermisosSistema esté completo).
    sqlcmd -S localhost -d RegistroTrabajo -E -f 65001 -b -I -i 017_bloque14_expediente_profesional.sql

  Cambios:
   1. Habilidades: catálogo por empresa (nombre normalizado único por empresa).
   2. TrabajadoresHabilidad: habilidad de un trabajador con nivel (1 básico, 2 intermedio, 3 avanzado).
   3. TrabajadoresEstudio: historial de estudios (licenciatura, maestría, certificados...).
   4. CurriculosTrabajador: un archivo de currículum por trabajador (PDF/DOC/DOCX, máx. 5 MB) guardado en la BD.
   5. Permisos expediente.consultar|editar|aprobar|buscar y su asignación a roles de sistema.
  Estado de estudios/habilidades: 1 Aprobado, 2 Pendiente (propuesto por el trabajador), 3 Rechazado.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
BEGIN TRANSACTION;

------------------------------------------------------------------ 1. Habilidades (catálogo)
IF OBJECT_ID(N'dbo.Habilidades', N'U') IS NULL
    EXEC (N'CREATE TABLE dbo.Habilidades (
        HabilidadId uniqueidentifier NOT NULL CONSTRAINT PK_Habilidades PRIMARY KEY CONSTRAINT DF_Habilidades_Id DEFAULT (newsequentialid()),
        EmpresaId uniqueidentifier NOT NULL,
        Nombre nvarchar(80) NOT NULL,
        NombreNormalizado nvarchar(80) NOT NULL,
        Activa bit NOT NULL CONSTRAINT DF_Habilidades_Activa DEFAULT (1),
        FechaCreacionUtc datetime2(0) NOT NULL CONSTRAINT DF_Habilidades_Fecha DEFAULT (sysutcdatetime()),
        CONSTRAINT UQ_Habilidades_Empresa_Id UNIQUE (EmpresaId, HabilidadId),
        CONSTRAINT UQ_Habilidades_Empresa_Nombre UNIQUE (EmpresaId, NombreNormalizado),
        CONSTRAINT FK_Habilidades_Empresas FOREIGN KEY (EmpresaId) REFERENCES dbo.Empresas (EmpresaId)
    )');

------------------------------------------------------------------ 2. TrabajadoresHabilidad
IF OBJECT_ID(N'dbo.TrabajadoresHabilidad', N'U') IS NULL
    EXEC (N'CREATE TABLE dbo.TrabajadoresHabilidad (
        TrabajadorHabilidadId uniqueidentifier NOT NULL CONSTRAINT PK_TrabajadoresHabilidad PRIMARY KEY CONSTRAINT DF_TrabHab_Id DEFAULT (newsequentialid()),
        EmpresaId uniqueidentifier NOT NULL,
        TrabajadorId uniqueidentifier NOT NULL,
        HabilidadId uniqueidentifier NOT NULL,
        Nivel tinyint NOT NULL CONSTRAINT CK_TrabHab_Nivel CHECK (Nivel BETWEEN 1 AND 3),
        AniosExperiencia tinyint NULL CONSTRAINT CK_TrabHab_Anios CHECK (AniosExperiencia IS NULL OR AniosExperiencia <= 60),
        Notas nvarchar(300) NULL,
        Estado tinyint NOT NULL CONSTRAINT DF_TrabHab_Estado DEFAULT (1) CONSTRAINT CK_TrabHab_Estado CHECK (Estado BETWEEN 1 AND 3),
        MotivoRechazo nvarchar(300) NULL,
        ResueltoPorUsuarioId uniqueidentifier NULL,
        ResueltoUtc datetime2(0) NULL,
        FechaCreacionUtc datetime2(0) NOT NULL CONSTRAINT DF_TrabHab_Creacion DEFAULT (sysutcdatetime()),
        FechaActualizacionUtc datetime2(0) NOT NULL CONSTRAINT DF_TrabHab_Actualizacion DEFAULT (sysutcdatetime()),
        CONSTRAINT UQ_TrabHab_Trabajador_Habilidad UNIQUE (EmpresaId, TrabajadorId, HabilidadId),
        CONSTRAINT FK_TrabHab_Trabajadores FOREIGN KEY (EmpresaId, TrabajadorId) REFERENCES dbo.Trabajadores (EmpresaId, TrabajadorId),
        CONSTRAINT FK_TrabHab_Habilidades FOREIGN KEY (EmpresaId, HabilidadId) REFERENCES dbo.Habilidades (EmpresaId, HabilidadId),
        CONSTRAINT FK_TrabHab_Resolutor FOREIGN KEY (EmpresaId, ResueltoPorUsuarioId) REFERENCES dbo.Usuarios (EmpresaId, UsuarioId)
    )');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TrabHab_Busqueda' AND object_id = OBJECT_ID(N'dbo.TrabajadoresHabilidad'))
    EXEC (N'CREATE INDEX IX_TrabHab_Busqueda ON dbo.TrabajadoresHabilidad (EmpresaId, HabilidadId, Nivel) INCLUDE (TrabajadorId, Estado)');

------------------------------------------------------------------ 3. TrabajadoresEstudio
IF OBJECT_ID(N'dbo.TrabajadoresEstudio', N'U') IS NULL
    EXEC (N'CREATE TABLE dbo.TrabajadoresEstudio (
        EstudioId uniqueidentifier NOT NULL CONSTRAINT PK_TrabajadoresEstudio PRIMARY KEY CONSTRAINT DF_TrabEst_Id DEFAULT (newsequentialid()),
        EmpresaId uniqueidentifier NOT NULL,
        TrabajadorId uniqueidentifier NOT NULL,
        Tipo tinyint NOT NULL CONSTRAINT CK_TrabEst_Tipo CHECK (Tipo BETWEEN 1 AND 6),
        Titulo nvarchar(200) NOT NULL,
        Institucion nvarchar(200) NULL,
        FechaInicio date NULL,
        FechaFin date NULL,
        EnCurso bit NOT NULL CONSTRAINT DF_TrabEst_EnCurso DEFAULT (0),
        Notas nvarchar(500) NULL,
        Estado tinyint NOT NULL CONSTRAINT DF_TrabEst_Estado DEFAULT (1) CONSTRAINT CK_TrabEst_Estado CHECK (Estado BETWEEN 1 AND 3),
        MotivoRechazo nvarchar(300) NULL,
        ResueltoPorUsuarioId uniqueidentifier NULL,
        ResueltoUtc datetime2(0) NULL,
        FechaCreacionUtc datetime2(0) NOT NULL CONSTRAINT DF_TrabEst_Creacion DEFAULT (sysutcdatetime()),
        FechaActualizacionUtc datetime2(0) NOT NULL CONSTRAINT DF_TrabEst_Actualizacion DEFAULT (sysutcdatetime()),
        CONSTRAINT CK_TrabEst_Fechas CHECK (FechaFin IS NULL OR FechaInicio IS NULL OR FechaFin >= FechaInicio),
        CONSTRAINT CK_TrabEst_EnCurso CHECK (EnCurso = 0 OR FechaFin IS NULL),
        CONSTRAINT FK_TrabEst_Trabajadores FOREIGN KEY (EmpresaId, TrabajadorId) REFERENCES dbo.Trabajadores (EmpresaId, TrabajadorId),
        CONSTRAINT FK_TrabEst_Resolutor FOREIGN KEY (EmpresaId, ResueltoPorUsuarioId) REFERENCES dbo.Usuarios (EmpresaId, UsuarioId)
    )');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TrabEst_Trabajador' AND object_id = OBJECT_ID(N'dbo.TrabajadoresEstudio'))
    EXEC (N'CREATE INDEX IX_TrabEst_Trabajador ON dbo.TrabajadoresEstudio (EmpresaId, TrabajadorId, Estado)');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TrabEst_Tipo' AND object_id = OBJECT_ID(N'dbo.TrabajadoresEstudio'))
    EXEC (N'CREATE INDEX IX_TrabEst_Tipo ON dbo.TrabajadoresEstudio (EmpresaId, Tipo, Estado) INCLUDE (TrabajadorId)');

------------------------------------------------------------------ 4. CurriculosTrabajador
IF OBJECT_ID(N'dbo.CurriculosTrabajador', N'U') IS NULL
    EXEC (N'CREATE TABLE dbo.CurriculosTrabajador (
        TrabajadorId uniqueidentifier NOT NULL CONSTRAINT PK_CurriculosTrabajador PRIMARY KEY,
        EmpresaId uniqueidentifier NOT NULL,
        NombreArchivo nvarchar(200) NOT NULL,
        TipoContenido nvarchar(100) NOT NULL,
        Contenido varbinary(max) NOT NULL,
        TamanoBytes int NOT NULL CONSTRAINT CK_Curriculos_Tamano CHECK (TamanoBytes BETWEEN 1 AND 5242880),
        HashSha256 char(64) NOT NULL,
        FechaActualizacionUtc datetime2(0) NOT NULL CONSTRAINT DF_Curriculos_Fecha DEFAULT (sysutcdatetime()),
        CONSTRAINT FK_Curriculos_Trabajadores FOREIGN KEY (EmpresaId, TrabajadorId) REFERENCES dbo.Trabajadores (EmpresaId, TrabajadorId)
    )');

------------------------------------------------------------------ 5. Permisos
INSERT INTO dbo.PermisosSistema (Codigo, Modulo, Accion, Descripcion)
SELECT v.Codigo, v.Modulo, v.Accion, v.Descripcion
FROM (VALUES
    (N'expediente.consultar', N'Expediente', N'Consultar', N'Consultar estudios, habilidades y currículum de los trabajadores'),
    (N'expediente.editar',    N'Expediente', N'Editar',    N'Registrar y editar estudios, habilidades y currículum de cualquier trabajador'),
    (N'expediente.aprobar',   N'Expediente', N'Aprobar',   N'Aprobar o rechazar estudios y habilidades propuestos por los trabajadores'),
    (N'expediente.buscar',    N'Expediente', N'Buscar',    N'Buscar trabajadores por habilidades y estudios')
) AS v (Codigo, Modulo, Accion, Descripcion)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PermisosSistema p WHERE p.Codigo = v.Codigo);

INSERT INTO dbo.RolPermisos (EmpresaId, RolId, PermisoId, Permitido)
SELECT r.EmpresaId, r.RolId, p.PermisoId, 1
FROM dbo.Roles r
JOIN (VALUES
    (N'Administrador',    N'expediente.consultar'), (N'Administrador',    N'expediente.editar'),
    (N'Administrador',    N'expediente.aprobar'),   (N'Administrador',    N'expediente.buscar'),
    (N'Recursos Humanos', N'expediente.consultar'), (N'Recursos Humanos', N'expediente.editar'),
    (N'Recursos Humanos', N'expediente.aprobar'),   (N'Recursos Humanos', N'expediente.buscar'),
    (N'Supervisor',       N'expediente.consultar'), (N'Supervisor',       N'expediente.buscar')
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
