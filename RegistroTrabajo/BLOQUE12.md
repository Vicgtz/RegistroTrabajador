# Bloque 12 — Configuración de la empresa y mantenimiento

Cada empresa administra sus datos, apariencia, formatos regionales y parámetros sin tocar código. La empresa sale siempre del token; la apariencia la lee cualquier usuario de la empresa y cambiarla exige `empresa.configurar`.

## Base de datos
Aplicar **antes** de levantar la API (el bootstrap exige el catálogo de permisos completo):

```powershell
sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i RegistroTrabajo.Data\Scripts\013_bloque12.sql
```

`013_bloque12.sql` (idempotente): `ConfiguracionEmpresa.NombreMostrado`, `TextoInstitucional`, `PrimerDiaSemana` (0..6, por defecto 1) y `Moneda`; tabla `LogosEmpresa` (un logotipo por empresa, máx. 512 KB); permisos `empresa.ver_permisos_efectivos` (rol Administrador) y `plataforma.sistema.consultar` (Administrador de plataforma).

## Endpoints
| Método | Ruta | Acceso |
|---|---|---|
| GET | `api/empresa/apariencia` | cualquier usuario de la empresa |
| GET / POST / DELETE | `api/empresa/logo` | GET: cualquier usuario; POST (multipart, campo `archivo`) y DELETE: `empresa.configurar` |
| PUT | `api/empresa`, `api/empresa/configuracion` | `empresa.configurar` (ya existían; ampliados) |
| GET | `api/usuarios/{id}/permisos-efectivos` | `empresa.ver_permisos_efectivos` + `usuarios.consultar` |
| GET | `api/sistema/estado` | `plataforma.sistema.consultar` |
| GET | `/health` (anónimo) | solo `ok` / `degradado` (200/503), sin detalles |

## Reglas
- **Logotipo**: PNG, JPEG o WebP detectados por la firma real del archivo (no por extensión ni encabezado); SVG no se admite; máx. 512 KB. Se guarda en BD y se sirve autenticado (`private`, `nosniff`); el cliente lo muestra como data URL. La auditoría registra el hecho y el tamaño, nunca el contenido. `LogoUrl` de `PUT api/empresa` se ignora.
- **Colores**: `#RRGGBB`; el color principal debe tener contraste ≥ 4.5:1 con texto blanco (los botones lo usan). Se aplican como variables CSS (`--rt-azul`, `--rt-azul-oscuro`, `--rt-secundario`) desde `wwwroot/js/tema.js`; no afectan autorización ni controles. Hay vista previa antes de guardar.
- **Regional**: formatos de fecha/hora de una lista cerrada (`FormatosRegionales`), zona y cultura validadas, moneda de 3 letras opcional. El cliente aplica formato y zona a las pantallas que usan `FormatoAsistencia` y `FormatoProyectos.FechaHora` mediante `FormatoEmpresa`; si el navegador no resuelve la zona, usa la hora local del equipo. Otras pantallas que formatean por su cuenta se migran gradualmente.
- **Permisos efectivos**: el Administrador ve roles y permisos (agrupados por módulo) de un usuario de su empresa; otra empresa responde 404.
- **Estado del sistema**: versión, entorno, BD disponible, último respaldo completado y errores técnicos de 24 h. Nunca cadenas de conexión ni credenciales.
- **Catálogos**: departamentos, puestos, tipos de permiso y estados de proyecto se desactivan, no se borran (ya implementado en sus bloques). La pestaña «Accesos» de Configuración reúne los enlaces a cada pantalla sin duplicar sus reglas.

## Pantallas
Configuración (`/configuracion`, antes «Mi empresa»; `/empresa` sigue funcionando): pestañas Datos, Apariencia, Regional, Parámetros y Accesos. Estado del sistema (`/plataforma/sistema`). Botón «Permisos» en Usuarios.

## Límites de la primera versión
- Sin módulos habilitados por empresa, sin modo de mantenimiento desde el panel, sin catálogo configurable de tipos de evento, sin tabla genérica de parámetros ni `HistorialConfiguracion` (la auditoría cubre los cambios).
- Los estados de proyecto se siguen activando/desactivando con el `PUT` existente (no hay endpoint `/estado` aparte).
- No se validan dimensiones del logotipo, solo formato y tamaño.
- Siguiente paso sugerido: matriz maestra de implementación de los 12 bloques.
