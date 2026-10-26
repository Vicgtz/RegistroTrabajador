# Bloque 6 — Calendario laboral y eventos de la empresa

Un calendario que reúne eventos de la empresa, festivos, ausencias aprobadas y fechas relevantes (cumpleaños y aniversarios), con vista mensual, agenda del día y administración de eventos. No duplica datos: lee cada fuente original y aplica su regla de visibilidad.

## Script de base de datos
`RegistroTrabajo.Data/Scripts/007_bloque6.sql` (idempotente y aditivo; **aplicarlo antes de levantar la API**, porque el arranque exige el catálogo de permisos completo):

```powershell
sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i RegistroTrabajo.Data\Scripts\007_bloque6.sql
```
Contenido:
- `EventosEmpresa` (la tabla ya existía con 10 columnas): `Estado`, `Visibilidad`, `DepartamentoId` (FK compuesta), `ModificadoPorUsuarioId`/`FechaModificacionUtc`, `CanceladoPorUsuarioId`/`FechaCancelacionUtc`/`MotivoCancelacion` y `RowVersion`; CHECK de coherencia visibilidad↔departamento; índice `(EmpresaId, FechaInicio)`.
- **El CHECK de `Tipo` se amplía de 1..2 a 1..5** (se reemplaza la restricción original; los valores 1 y 2 siguen siendo válidos).
- `ConfiguracionEmpresa`: `MostrarCumpleanos` y `MostrarAniversarios` (apagados por defecto).
- 4 permisos nuevos en `PermisosSistema` y su asignación a los roles de sistema existentes.

La tabla del diseño usa `TipoEvento`, `Estado`… pero el esquema real ya tenía `Tipo` y no tenía `Estado`; se respetó el nombre real (`Tipo`).

## Significado de los códigos (la BD solo limita rangos)
| Campo | Valores |
|---|---|
| `EventoEmpresa.Tipo` | 1 Reunión general · 2 Capacitación · 3 Actividad interna · 4 Fecha importante · 5 Otro |
| `EventoEmpresa.Estado` | 1 Activo · 2 Cancelado |
| `EventoEmpresa.Visibilidad` | 1 Toda la empresa · 2 Un departamento (`DepartamentoId` obligatorio) |
| `CalendarioItemDto.Categoria` | 1 Evento · 2 Festivo · 3 Ausencia · 4 Cumpleaños · 5 Aniversario laboral |

## Permisos
| Código | Por defecto | Qué permite |
|---|---|---|
| `calendario.consultar` | Todos los roles | Ver el calendario y el detalle de los eventos que le corresponden |
| `calendario.gestionar_eventos` | Admin, RH | Crear, editar, cancelar y listar eventos (incluidos los cancelados) |
| `calendario.configurar` | Admin, RH | Activar/desactivar cumpleaños y aniversarios |
| `calendario.ver_fechas_relevantes` | Todos los roles | Ver cumpleaños y aniversarios (si la empresa los habilitó) |

## Endpoints (todos filtran por la empresa del token)
- `GET api/calendario?desde&hasta[&incluirCancelados]` (máx. 93 días) y `GET api/calendario/dia?fecha` — `calendario.consultar`.
- `GET api/calendario/configuracion` (`consultar`) · `PUT api/calendario/configuracion` (`configurar`).
- `GET api/eventos-empresa` (`gestionar_eventos`, paginado con filtros de rango, tipo y estado) · `GET {id}` (`consultar` + visibilidad validada en el servicio: 404 si no le corresponde) · `POST` · `PUT {id}` · `POST {id}/cancelar` (`gestionar_eventos`).

## Reglas de negocio implementadas
- **Fuentes (sin catálogos duplicados):** eventos propios; festivos de `DiasFestivo` del Bloque 5 (incluidos recurrentes; el 29 de febrero recurrente solo existe en bisiestos, igual que en el cálculo de permisos); permisos aprobados de `SolicitudesPermiso` del Bloque 4; fechas de `Trabajadores` del Bloque 3.
- **Fechas y zonas horarias:** los DTO hablan siempre en hora local de la empresa. Evento de día completo → se guarda la fecha local (sin zona), no se desplaza. Evento con hora → se guarda en UTC con `ConfiguracionEmpresa.ZonaHorariaId` y se devuelve en hora local. Una hora inexistente por cambio de horario se rechaza. Un evento con hora que termina justo a medianoche no invade el día siguiente. Los periodos de varios días funcionan entre meses y años (máx. 366 días por evento).
- **Visibilidad de eventos:** quien tiene `gestionar_eventos` ve todos (también cancelados); los demás solo los activos de toda la empresa o de su departamento.
- **Ausencias:** solo permisos aprobados (pendientes, rechazados y cancelados no aparecen; un permiso cancelado deja de mostrarse al consultar de nuevo). Cada quien ve las propias; el supervisor, las de sus supervisados vigentes (con `permisos.aprobar`); RH/admin, toda la empresa (`permisos.consultar_todos`/`aprobar_todos`). Las ausencias por horas se muestran en su día con la hora. Nunca se expone tipo ni motivo.
- **Fechas relevantes:** requieren que la empresa las habilite **y** el permiso `calendario.ver_fechas_relevantes`. Solo trabajadores activos; solo nombre y día (sin edad). Aniversarios de 1 año en adelante; 29 de febrero se celebra el 28 en años no bisiestos.
- **Cancelación e historial:** cancelar exige motivo y conserva el evento (quién, cuándo, por qué); un evento cancelado no se edita. Crear/editar/cancelar/configurar quedan en `Auditoria`. `RowVersion` evita ediciones concurrentes (409).
- **Aislamiento:** todo filtra por `EmpresaId` del token; un evento de otra empresa responde 404.

## Pantallas (Blazor)
`/calendario` (rejilla mensual lunes→domingo, agenda del día, lista del mes para pantallas pequeñas, leyenda con filtros por categoría, detalle del evento), `/calendario/eventos` (listado con filtros, crear/editar/cancelar y configuración de fechas relevantes). Los festivos se siguen administrando en `/horarios`.

## Límites conocidos / fuera de alcance
- Sin eventos recurrentes, invitaciones, recordatorios ni notificaciones por evento; sin exportación (iCal/PDF).
- La visibilidad por grupo solo admite un departamento (no listas de trabajadores ni roles).
- «Hoy» en la pantalla usa la fecha del navegador; el resto usa la zona de la empresa.
- Al abrir una ausencia, la pantalla lleva a Permisos (no hay vista de detalle individual de solicitudes).
- Pendiente de comprobar a mano en el navegador: las pruebas automáticas cubren servicios y esquema; las pantallas solo se compilaron.
