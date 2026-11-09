/*
  RegistroTrabajo - Catálogo inicial de habilidades (para el autocompletado del expediente profesional).
  Idempotente y aditivo: no borra ni modifica nada. Inserta solo las habilidades que todavía no existen
  en cada empresa (la comparación es por nombre normalizado en mayúsculas, igual que hace la API).
    sqlcmd -S localhost -d RegistroTrabajo -E -f 65001 -b -I -i 018_catalogo_habilidades_inicial.sql

  Para agregar o quitar habilidades de la lista inicial, edita el bloque VALUES de abajo antes de ejecutar el script.
  Las habilidades que luego escriban los usuarios se siguen sumando solas al catálogo.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Habilidades', N'U') IS NULL
    THROW 50001, N'Falta la tabla dbo.Habilidades: ejecute primero 017_bloque14_expediente_profesional.sql.', 1;

DECLARE @Inicial TABLE (Nombre nvarchar(80) NOT NULL);
INSERT INTO @Inicial (Nombre) VALUES
    -- Tecnología
    (N'C#'), (N'.NET'), (N'ASP.NET Core'), (N'Blazor'), (N'JavaScript'), (N'HTML y CSS'), (N'Python'), (N'Java'),
    (N'SQL'), (N'SQL Server'), (N'Git'), (N'Power BI'), (N'Soporte técnico'), (N'Redes'),
    -- Ofimática
    (N'Excel'), (N'Excel avanzado'), (N'Word'), (N'PowerPoint'), (N'Microsoft 365'),
    -- Idiomas
    (N'Inglés'), (N'Francés'),
    -- Gestión y habilidades blandas
    (N'Liderazgo'), (N'Trabajo en equipo'), (N'Comunicación efectiva'), (N'Resolución de problemas'),
    (N'Gestión de proyectos'), (N'Scrum'), (N'Atención al cliente'), (N'Negociación'), (N'Análisis de datos'),
    -- Administración
    (N'Contabilidad'), (N'Nómina'), (N'Reclutamiento y selección'), (N'Compras y abastecimiento'), (N'Facturación'),
    -- Operación y mantenimiento
    (N'Mantenimiento industrial'), (N'Instalaciones eléctricas'), (N'AutoCAD'), (N'Soldadura'),
    (N'Seguridad e higiene'), (N'Manejo de montacargas'), (N'Lectura de planos');

INSERT INTO dbo.Habilidades (HabilidadId, EmpresaId, Nombre, NombreNormalizado, Activa, FechaCreacionUtc)
SELECT NEWID(), e.EmpresaId, i.Nombre, UPPER(i.Nombre), 1, SYSUTCDATETIME()
FROM dbo.Empresas e
CROSS JOIN @Inicial i
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Habilidades h
    WHERE h.EmpresaId = e.EmpresaId AND h.NombreNormalizado = UPPER(i.Nombre));

PRINT CONCAT(N'Habilidades agregadas al catálogo: ', @@ROWCOUNT);

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
