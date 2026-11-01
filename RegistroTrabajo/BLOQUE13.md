# Bloque 13 — Registro de cuentas con clave de registro

Permite que un trabajador cree su propia cuenta si conoce la **clave de registro de su empresa**. No envía correos.

## Flujo
1. Un usuario con `empresa.configurar` abre **Usuarios** y pulsa *Activar registro* / *Generar clave nueva*. La clave (`XXXX-XXXX-XXXX-XXXX`, ≈79 bits, sin caracteres ambiguos) se muestra **una sola vez**.
2. El trabajador entra a **Crear cuenta** (`/registro`, enlace en el login) y escribe la clave, nombre, apellido, correo y contraseña.
3. La clave identifica la empresa. Se crea un usuario activo con el rol de sistema **Trabajador** (nunca otro), sin obligación de cambiar la contraseña.
4. Una clave nueva invalida la anterior; *Desactivar* deja el registro cerrado.

## API
| Endpoint | Acceso |
|---|---|
| `POST /api/auth/registro` | Anónimo, límite `registro` (5/min por IP) |
| `GET/POST/DELETE /api/empresa/clave-registro` | `empresa.configurar` (estado / generar / desactivar) |

## Seguridad
- Solo se guarda el SHA-256 de la clave (`ConfiguracionEmpresa.ClaveRegistroHash`, índice único filtrado); se ignoran guiones, espacios y mayúsculas al compararla. Ni la clave ni su hash se auditan.
- Error único («No se pudo completar el registro…») para clave inválida, correo existente, empresa inactiva/de plataforma o falta del rol Trabajador. Solo la política de contraseña se informa por separado.
- No aplica a la empresa de plataforma ni a empresas no activas.
- Se audita `AutoRegistro` (empresa y usuario creado), `GenerarClaveRegistro` y `DesactivarClaveRegistro`.
- Si `Security:RequireConfirmedEmail` está activo, se envía el enlace de confirmación; si no, no se envía correo.

## Base de datos
Script `014_registro_con_clave.sql` (idempotente; aplicar con `-I`): `ClaveRegistroHash varbinary(32) NULL`, `ClaveRegistroGeneradaUtc datetime2(0) NULL` e índice `UX_ConfigEmpresa_ClaveRegistro`. Sin permisos nuevos.

## Código
`Bloque13Services.cs` (`ClaveRegistroService`, `RegistroConClaveService`), `DtosBloque13.cs`, `AuthController.Registro`, `ClaveRegistroController` (en `Bloque12Controllers.cs`), cliente `AuthClient.RegistrarAsync` / `ConfiguracionClient.*ClaveRegistro*`, pantallas `Registro.razor` y componente `ClaveRegistroPanel.razor`. Pruebas en `Bloque13Tests.cs`.
