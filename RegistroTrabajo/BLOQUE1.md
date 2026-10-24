# Bloque 1 — Registro de Trabajo

Empresas, configuración, usuarios, roles, permisos del sistema, autenticación/sesiones, historial de acceso y auditoría.

## Arquitectura
| Proyecto | Contenido |
|---|---|
| `RegistroTrabajo.Data` | Entidades y `RegistroTrabajoDbContext` (mapeo fiel al esquema existente; sin migraciones) |
| `RegistroTrabajo.Models` | DTOs, constantes (estados, resultados, códigos de permiso, permisos por defecto de cada rol) |
| `RegistroTrabajo.Services` | Lógica de negocio, validaciones, autorización por permiso, sesiones, auditoría |
| `RegistroTrabajo.Api` | Controladores, JWT, middlewares (CorrelationId, errores), rate limiting, bootstrap |
| `RegistroTrabajo.Tests` | 31 pruebas xUnit (SQLite en memoria + 1 de esquema contra SQL Server, de solo lectura) |

## Esquema / migraciones
El esquema ya existía y **coincide con el modelo EF** (verificado por `SqlServerSchemaTests`). No hay migración inicial ni cambios en la BD.
Valores de `Estado`/`Resultado` (límites tomados de los CHECK; los significados son una convención adoptada, ajústela si usa otra):
- Empresa.Estado: 1 Activa, 2 Suspendida, 3 Inactiva · Usuario.Estado: 1 Activo, 2 Inactivo, 3 Suspendido
- HistorialAcceso.Resultado: 1 Exitoso, 2 Credenciales inválidas, 3 Cuenta bloqueada, 4 Cuenta/empresa inactiva, 5 Refresh inválido

## Endpoints (todos requieren JWT salvo login/refresh; permiso entre corchetes)
- `POST /api/auth/login`, `/api/auth/refresh` (anónimos, rate limit 10/min/IP) · `POST /api/auth/logout`, `/api/auth/cambiar-contrasena`
- `GET|PUT /api/empresa` [empresa.consultar|configurar] · `GET|PUT /api/empresa/configuracion`
- `GET|POST /api/usuarios` [usuarios.consultar|crear] · `GET|PUT /api/usuarios/{id}` [consultar|editar]
- `POST /api/usuarios/{id}/desactivar` [usuarios.desactivar] · `POST /api/usuarios/{id}/activar` [usuarios.editar]
- `GET /api/usuarios/{id}/roles` [roles.consultar] · `PUT|DELETE /api/usuarios/{id}/roles/{rolId}` [roles.configurar]
- `DELETE /api/usuarios/{id}/sesiones` [usuarios.revocar_sesiones]
- `GET /api/roles`, `GET /api/permisos`, `GET /api/roles/{id}/permisos` [roles.consultar] · `PUT /api/roles/{id}/permisos` [roles.configurar]
- `GET /api/sesiones` [sesiones.consultar] · `DELETE /api/sesiones/{id}` [sesiones.revocar]
- `GET /api/accesos` [accesos.consultar] · `GET /api/auditoria` [auditoria.consultar]

## Seguridad implementada
- Empresa siempre desde los claims del JWT (`ICurrentUser`); ningún endpoint recibe `EmpresaId`. Datos de otra empresa → 404.
- En **cada** solicitud se revalida en BD: sesión no revocada/expirada/inactiva, usuario activo, empresa activa; el permiso se consulta en BD (`[RequierePermiso]`).
- Anti-elevación: no se pueden modificar los propios roles; solo se asignan/gestionan roles cuyos permisos se poseen (Administrador solo por otro administrador); no se otorgan permisos que no se tienen; los permisos del rol Administrador son inmutables; no se desactiva a uno mismo ni al último administrador.
- Contraseñas: `PasswordHasher<T>` de ASP.NET Core Identity (PBKDF2, rehash automático) + política de 10–128 caracteres con mayúscula, minúscula y número.
- Refresh token aleatorio de 32 bytes; solo se guarda su SHA-256; se rota en cada refresh; el JWT nunca se guarda.
- Login con mensaje único ("Credenciales inválidas.") y tiempo igualado; bloqueo temporal (5 intentos / 15 min, configurable en `RegistroTrabajoApiSettings:Security`).
- Auditoría solo inserción desde la aplicación (el servicio no expone update/delete); redacta claves con contraseña/hash/token/secret; JSON validado (también hay CHECK `ISJSON` en BD).
- Errores: `ProblemDetails` con mensaje seguro + `correlationId`; excepciones internas → 500 genérico (se registran en log).

## Configuración necesaria
1. **Clave JWT**: ya no está en `appsettings.json`. Para desarrollo se guardó una clave aleatoria en *user-secrets* (`UserSecretsId` en el csproj). En otros entornos: variable `RegistroTrabajoApiSettings__JwtSettings__SecretKey` (≥32 caracteres). **La clave anterior estuvo en el archivo: considérela comprometida y no la reutilice.**
2. **Primer administrador** (la BD tiene 0 empresas; el comando falla si ya existen):
```powershell
$env:BOOTSTRAP_EMPRESA="Mi Empresa"; $env:BOOTSTRAP_ADMIN_EMAIL="admin@miempresa.com"; $env:BOOTSTRAP_ADMIN_PASSWORD="<contraseña segura>"
dotnet run --project RegistroTrabajo.Api -- bootstrap-admin
```
   El administrador queda con `DebeCambiarContrasena = 1`. (No ejecutado: escribe en su BD real.)
3. **Auditoría de solo inserción a nivel BD** (recomendado, requiere DBA): `DENY UPDATE, DELETE ON dbo.Auditoria TO <usuario_o_rol_de_la_app>;` — con autenticación integrada de Windows como db_owner no es aplicable hoy.

## Pruebas
```powershell
dotnet test RegistroTrabajo.Tests
```
SQLite no replica CHECK/triggers de SQL Server; la prueba `SqlServerSchemaTests` (solo lectura) valida el mapeo contra la BD real y se omite sin conexión (`RT_TEST_CONNECTION` para otra cadena).

## Queda para el Bloque 2 / límites conocidos
- Crear empresas adicionales (hoy solo la inicial por bootstrap) y administración de plataforma.
- UI Blazor (login, administración), CORS para el cliente WASM, almacenamiento seguro de tokens en el navegador.
- Trabajadores y FK de `Usuarios.TrabajadorId` (columna preparada, sin lógica); permisos laborales.
- Recuperación/restablecimiento de contraseña, MFA, detección de reutilización de refresh token.
- Rate limiting es en memoria por IP (no distribuido).

## Cliente Blazor (RegistroTrabajo/RegistroTrabajo)
Modelado sobre la app Finanzas (CustomAuthProvider, AuthHttpHandler, layouts), adaptado a esta API (HTTP + ProblemDetails en lugar de BaseResponse).
- **Conexión:** `wwwroot/appsettings.Development.json` -> `http://localhost:5265/`; `appsettings.json` (producción) -> cambie la URL al desplegar. CORS en la API: `RegistroTrabajoApiSettings:AllowedOrigins` (por defecto `http://localhost:5118` y `https://localhost:7097`).
- **Servicios:** `SesionStore` (sesión en localStorage), `CustomAuthProvider`, `AuthHttpHandler` (añade el JWT, renueva con el refresh token antes de vencer y cierra la sesión local ante un 401), `ApiClient`, `AuthClient`, `InicioClient`.
- **Pantallas:** `/login`, `/` (inicio: saludo, roles, empresa, usuarios, sesiones activas y accesos recientes según permisos), `/cuenta/contrasena` (obligatoria si `DebeCambiarContrasena`), página 404; menú lateral, barra superior con empresa/usuario y botón Salir.
- **Nuevo endpoint:** `GET /api/auth/me` (perfil, roles y permisos del usuario autenticado).
- **Límites:** el ocultar/mostrar por permisos en la interfaz es solo visual (la API valida todo); los tokens en localStorage son vulnerables a XSS (igual que en Finanzas; una alternativa con cookies HttpOnly/BFF queda para el Bloque 2); la API no bloquea otros endpoints mientras `DebeCambiarContrasena` sea verdadero (lo fuerza solo la interfaz).
- **Ejecutar:** API y cliente a la vez (Visual Studio: proyectos de inicio múltiples, perfiles `http`), o `dotnet run --project RegistroTrabajo.Api` y `dotnet run --project RegistroTrabajo`; abrir `http://localhost:5118`.

---

# Cierre del Bloque 1 (segunda ronda)

## Qué se agregó
- **Administrador de plataforma y alta de empresas:** `Empresas.EsPlataforma` (solo una empresa de plataforma). Sus usuarios, con los permisos `plataforma.empresas.*`, crean empresas (con sus 4 roles y su administrador), las consultan y las activan/suspenden/desactivan (al hacerlo se revocan las sesiones de la empresa). Los permisos de plataforma no funcionan en empresas normales ni siquiera si se insertan a mano en la BD.
- **Recuperación de contraseña y confirmación de correo:** enlaces de un solo uso con caducidad; solo se guarda el hash del token (`TokensRecuperacionContrasena`, `TokensConfirmacionCorreo`). Las respuestas públicas nunca revelan si la cuenta existe. Un cambio de correo exige confirmarlo otra vez. Opción `Security:RequireConfirmedEmail` (desactivada por defecto).
- **Correo:** `IEmailSender` con modo `Dev` (escribe el mensaje en `correos-dev/` junto al ejecutable y en el log) y modo `Smtp` (`RegistroTrabajoApiSettings:Email`, secretos fuera del repositorio).
- **Roles propios por empresa:** crear, editar y activar/desactivar (los de sistema están protegidos; nombres reservados).
- **Usuarios:** desbloquear cuenta, restablecer contraseña por un administrador (anti-elevación: no se puede tomar la cuenta de alguien con más permisos), vínculo con trabajador validado (misma empresa, una cuenta por trabajador).
- **Sesiones:** "mis sesiones" (listar y cerrar las propias) y listado con correo y marca de sesión actual.
- **Cambio de contraseña obligatorio forzado por la API:** con `DebeCambiarContrasena` solo se permiten `/api/auth/me`, `cambiar-contrasena` y `logout` (403 con `codigo = cambio_contrasena_requerido`).
- **Pantallas Blazor de administración:** usuarios, roles y permisos (matriz módulo × acción), sesiones, mis sesiones, accesos, auditoría (con detalle antes/después), mi empresa, plataforma → empresas, olvidé mi contraseña, restablecer contraseña y confirmar correo. El menú se filtra por permisos (solo visual; la API valida).

## Endpoints nuevos
`GET/POST /api/plataforma/empresas`, `GET /api/plataforma/empresas/{id}`, `POST …/{id}/estado` · `POST/PUT /api/roles`, `POST /api/roles/{id}/estado` · `POST /api/usuarios/{id}/desbloquear`, `…/restablecer-contrasena` · `POST /api/auth/olvide-contrasena`, `…/restablecer-contrasena`, `…/confirmar-correo`, `…/reenviar-confirmacion` (públicos, con límite de intentos) · `GET /api/sesiones/mias`, `DELETE /api/sesiones/mias/{id}`.

## Scripts SQL (en `RegistroTrabajo.Data/Scripts`, idempotentes; no se ejecutan solos)
- `003_bloque1_faltantes.sql` — **ya aplicado a la BD de desarrollo.** Agrega `Empresas.EsPlataforma`, `Usuarios.CorreoConfirmadoUtc` (los usuarios existentes quedan confirmados), la tabla de confirmación y 5 permisos nuevos (los de gestión de usuarios se asignan a los Administradores existentes). Aplicar antes de arrancar la API nueva en cualquier otro entorno.
- `001_auditoria_solo_insercion.sql` — **pendiente de aplicar (requiere un DBA).** Crea el rol `rol_app_registrotrabajo` con `DENY UPDATE, DELETE` sobre `Auditoria` e `HistorialAcceso`. Solo tiene efecto si la API se conecta con un usuario que no sea `db_owner`/`sysadmin` (hoy usa autenticación integrada con permisos amplios).

## Cómo operar
```powershell
# Primera empresa de plataforma y su administrador (una sola vez; variables de entorno o user-secrets)
$env:BOOTSTRAP_PLATFORM_EMAIL="..."; $env:BOOTSTRAP_PLATFORM_PASSWORD="..."
dotnet run --project RegistroTrabajo.Api -- bootstrap-platform
```
Después, ese administrador inicia sesión en el cliente, cambia su contraseña y da de alta empresas desde **Plataforma → Empresas**.

## Pruebas
58 pruebas (`dotnet test RegistroTrabajo.Tests`). Las de SQL Server aplican los scripts dentro de una transacción que siempre se revierte y comprueban que cada tabla y columna de la base está mapeada en el modelo (y al revés).
Verificado además contra la base real por HTTP y en el navegador: cambio de contraseña forzado (403), aislamiento de plataforma (403 para administradores de empresas normales), recuperación de contraseña con correo en modo Dev, y recorrido de todas las pantallas con el administrador de plataforma.

## Límites conocidos / siguiente
- No hay pruebas HTTP de extremo a extremo automatizadas (WebApplicationFactory); la verificación HTTP/UI fue manual.
- Reutilización del refresh token (detección), MFA y exportaciones: fuera de esta ronda.
- Los permisos de aprobar/exportar pertenecen a los módulos de los bloques siguientes; el mecanismo ya los soporta.
- `dev-credenciales.local.txt` (en `RegistroTrabajo.Api`) contiene contraseñas de desarrollo: bórralo cuando las hayas copiado.
- `appsettings.json` volvió a contener la clave JWT antigua (la había movido a user-secrets). Considérala comprometida y muévela otra vez a user-secrets o a una variable de entorno.