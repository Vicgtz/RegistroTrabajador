# Bloque 4 — Permisos, vacaciones y ausencias

Tipos de permiso configurables por empresa, solicitudes con validación de reglas, aprobación por supervisor, saldos con libro de movimientos y cancelación de permisos aprobados.

## Script de base de datos
`RegistroTrabajo.Data/Scripts/005_bloque4.sql` (idempotente y aditivo; **aplicarlo antes de levantar la API**, porque el arranque exige el catálogo de permisos completo):

```powershell
sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i RegistroTrabajo.Data\Scripts\005_bloque4.sql
```
(`-I` activa QUOTED_IDENTIFIER, necesario para los índices filtrados.) Contenido:
- `SolicitudesPermiso`: columnas `RowVersion` y `ClaveIdempotencia`; índice único filtrado `(EmpresaId, TrabajadorId, ClaveIdempotencia)`.
- `MovimientosSaldoPermiso`: índice único filtrado `(SolicitudPermisoId, TipoMovimiento)` para tipos 2 y 3 → un consumo y una devolución como máximo por solicitud.
- 8 permisos nuevos en `PermisosSistema` y su asignación a los roles de sistema existentes.

## Significado de los códigos (la BD solo limita rangos)
| Campo | Valores |
|---|---|
| `SolicitudPermiso.Estado` | 1 Pendiente · 2 Aprobada · 3 Rechazada · 4 Cancelada · 5 Concluida (reservado, no se usa) |
| `AprobacionPermiso.Decision` | 1 Aprobada · 2 Rechazada · 3 reservado |
| `SolicitudCancelacionPermiso.Estado` | 1 Pendiente · 2 Aprobada · 3 Rechazada · 4 Cancelada (sin uso todavía) |
| `TipoPermiso.UnidadBase` | 1 Días · 2 Horas |
| `MovimientoSaldoPermiso.TipoMovimiento` | 1 Asignación (+asignado) · 2 Consumo (+utilizado) · 3 Devolución (−utilizado) · 4 Ajuste a la baja (−asignado). La BD exige `Cantidad > 0`; el signo lo da el tipo. Los ajustes al alza se guardan como Asignación con descripción «Ajuste: …». |

## Permisos
| Código | Quién lo tiene por defecto | Qué permite |
|---|---|---|
| `permisos.configurar` | Admin, RH | Tipos de permiso y reglas |
| `permisos.solicitar` | Todos los roles | Solicitar, editar/cancelar lo propio, pedir cancelación |
| `permisos.consultar_todos` | Admin, RH | Historial y calendario de toda la empresa |
| `permisos.aprobar` | Admin, RH, Supervisor | Resolver solicitudes de los trabajadores que **supervisa** (vigencia en `TrabajadorSupervisor`) |
| `permisos.aprobar_todos` | Admin, RH | Resolver solicitudes de cualquier trabajador |
| `permisos.ver_motivo_privado` | Admin, RH | Ver el motivo de una solicitud ajena |
| `saldos.consultar` | Admin, RH, Supervisor (solo supervisados) | Saldos de otros trabajadores |
| `saldos.ajustar` | Admin, RH | Ajustar saldos y generar los del año |

Nadie resuelve su propia solicitud ni su propia cancelación (403). Una solicitud ajena fuera del alcance responde 404.

## Endpoints (todos filtran por la empresa del token)
- `GET api/tipos-permiso` (cualquier usuario autenticado) · `POST`, `PUT {id}`, `POST {id}/estado`, `POST {id}/reglas`, `PUT {id}/reglas/{reglaId}`, `POST iniciales` (`permisos.configurar`).
- `POST api/solicitudes-permiso/calculo` (vista previa de duración y saldo) · `POST api/solicitudes-permiso` · `GET mias` · `PUT {id}` · `POST {id}/cancelar` · `POST {id}/solicitar-cancelacion` (`permisos.solicitar`).
- `GET api/solicitudes-permiso` (historial por alcance), `GET bandeja`, `GET calendario?desde&hasta`, `GET {id}`, `POST {id}/resolver` (alcance validado en el servicio).
- `GET api/cancelaciones-permiso` · `POST {id}/resolver`.
- `GET api/saldos-permiso?trabajadorId&anio` · `GET {id}/movimientos` · `POST ajustes` · `POST generar/{anio}` (`saldos.ajustar`).

## Reglas de negocio implementadas
- **Duración:** solo días laborales según el horario asignado (`AsignacionHorario` + `HorarioDetalleDia`, 1 = lunes … 7 = domingo); sin horario se asume lunes a viernes y 8 h. Se excluyen festivos (`DiaFestivo`, incluidos los recurrentes) y descansos del trabajador. Por horas: un solo día laboral, fin posterior al inicio; solo si el tipo es de horas o permite fraccionar (en tipos de días se convierte con la jornada del día).
- **Validaciones al solicitar:** trabajador activo, tipo activo, no fechas pasadas, no cruzar de año, regla vigente (si el tipo tiene reglas y ninguna rige esa fecha, no está disponible), antigüedad mínima, límite por solicitud, traslape con permisos pendientes o aprobados, saldo suficiente salvo que la regla permita saldo negativo.
- **Saldo:** el saldo del año se crea al primer uso (o con «Generar saldos del año») con `CantidadAnual`; si la regla no reinicia, hereda el remanente del año anterior. Al crear una solicitud pendiente se **reserva**; al aprobar pasa a **utilizado** (movimiento Consumo); al rechazar o cancelar se libera; al autorizar la cancelación de un permiso aprobado se registra una **Devolución**. Un tipo sin `CantidadAnual` (y sin saldo previo) no controla saldo.
- **Reintentos:** `ClaveIdempotencia` evita duplicar solicitudes; el índice único de movimientos impide descontar o devolver dos veces; `RowVersion` en solicitudes y saldos detecta modificaciones concurrentes (409). Cada operación es un único `SaveChanges`, por lo que la decisión y el saldo se guardan juntos.
- **Tipos sin aprobación:** la solicitud queda aprobada al crearse (con registro de aprobación automática) y consume saldo si el tipo lo controla.
- **Privacidad:** el motivo solo lo ven el propio trabajador y quien tenga `permisos.ver_motivo_privado`; las notificaciones y la auditoría no incluyen motivo ni tipo; el calendario muestra solo nombre y fechas.
- **Historial:** nada se borra. Las decisiones quedan en `AprobacionesPermiso`, las cancelaciones conservan la solicitud original y los ajustes registran quién y por qué (también en `Auditoria`).
- **Notificaciones:** se escriben en `Notificaciones` para supervisores (solicitud/cancelación pendiente) y para el trabajador (resolución), respetando `PreferenciaNotificacion`. No hay aún bandeja de notificaciones en la UI.

## Pantallas (Blazor)
`/permisos` (solicitar y mis solicitudes), `/permisos/aprobaciones` (bandeja y cancelaciones), `/permisos/saldos` (saldos, movimientos, historial con filtros, ajustes), `/permisos/configuracion` (tipos y reglas).

## Límites conocidos / fuera de alcance
- Comprobantes: `RequiereComprobante` se muestra pero no hay carga de archivos.
- Aprobación de un solo nivel (la tabla admite varios niveles, no se usa).
- La fecha «hoy» para validar fechas pasadas es la fecha UTC (como el resto de la aplicación), no la zona horaria de la empresa.
- Sin vista de calendario gráfica (el endpoint `calendario` existe).
- Estado 5 de solicitud y estado 4 de cancelación quedan reservados.
