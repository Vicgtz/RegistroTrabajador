/*
  RegistroTrabajo - Auditoría e historial de acceso de solo inserción a nivel de base de datos.
  La aplicación ya no expone operaciones de modificación/borrado sobre estas tablas; este script lo hace cumplir en SQL Server.

  IMPORTANTE: solo tiene efecto si la API se conecta con un usuario que NO sea db_owner/sysadmin
  (esos principales ignoran los DENY). Hoy la API usa autenticación integrada de Windows con permisos amplios.
  Pasos recomendados:
    1) Cree un login/usuario dedicado para la API (por ejemplo [usuario_app_registrotrabajo]).
    2) Ejecute este script.
    3) Agregue ese usuario al rol:  ALTER ROLE rol_app_registrotrabajo ADD MEMBER [usuario_app_registrotrabajo];
    4) Configure DbSettings (UserId/Password, UseIntegratedSecurity = false) con ese usuario.
  Idempotente. Ejecutar:  sqlcmd -S localhost -d RegistroTrabajo -E -b -i 001_auditoria_solo_insercion.sql
*/
SET NOCOUNT ON;

IF DATABASE_PRINCIPAL_ID(N'rol_app_registrotrabajo') IS NULL
    CREATE ROLE rol_app_registrotrabajo;

-- Operación normal sobre las tablas del esquema dbo (sin permisos de DDL).
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO rol_app_registrotrabajo;

-- Registros históricos: solo se pueden leer e insertar.
DENY UPDATE, DELETE ON dbo.Auditoria       TO rol_app_registrotrabajo;
DENY UPDATE, DELETE ON dbo.HistorialAcceso TO rol_app_registrotrabajo;
