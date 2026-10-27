# Bloque 8 — Seguimiento avanzado de tareas y actividades

Complementa el Bloque 7 (que organiza proyectos y tareas) con trazabilidad: línea de tiempo de cada tarea, revisiones del responsable, detección de retrasos y pendientes, y los eventos que usará el Bloque 9 para notificar. **No duplica** lo existente: el avance y los cambios de estado siguen en `HistorialAvanceTarea`; los comentarios y menciones, en `Comentarios` / `MencionesComentario`; la bandeja, en `Notificaciones`.

## Base de datos

`Data/Scripts/009_bloque8.sql` (idempotente y aditivo; requiere el 008). Aplicar **antes** de levantar la API:

```
sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i RegistroTrabajo.Data\Scripts\009_bloque8.sql
```

| Objeto | Función |
|---|---|
| `EventosSeguimientoTarea` | Línea de tiempo de operaciones: tipo, usuario, fecha, valor anterior/nuevo (JSON de un texto) y detalle. Solo se inserta. |
| `RevisionesTarea` | Cada revisión: revisor, resultado (1 Aprobada, 2 Devuelta), observación y avance al revisar. |
| `ConfiguracionEmpresa.DiasAlertaVencimiento` | Días hábiles previos al vencimiento para "próxima a vencer" (0..30, por defecto 3). |
| `ConfiguracionEmpresa.DiasSinActualizacion` | Días hábiles sin cambios para alertar (1..365; `NULL` = desactivado). |
| Permiso `tareas.ver_seguimiento` | Panel del equipo. Por defecto: Administrador, Recursos Humanos, Supervisor. |

Tipos de evento (`TipoEventoTarea`): fecha límite y prioridad cambiadas, asignación agregada/retirada (también al retirar a alguien del proyecto), enviada a revisión, revisión aprobada/devuelta, reabierta, cancelada y comentario.

## Reglas

- **Transiciones** (`TransicionesTarea`, `SeguimientoServices.cs`): la API valida cada cambio de estado con una matriz explícita. Por avance se llega a Pendiente, En proceso o En revisión; **Completada** solo la fija quien revisa (aprobar). Desde En revisión solo el revisor aprueba o devuelve (Reabierta); una tarea cerrada solo se mueve reabriéndola. `TareaDetalleDto.TransicionesPermitidas` lista lo que el usuario puede hacer (la interfaz solo muestra esas acciones).
- **Línea de tiempo**: mezcla `EventosSeguimientoTarea` y `HistorialAvanceTarea`. Si un evento comparte operación con una fila de avance (envío a revisión, revisión, reapertura, cancelación) se fusionan en una sola entrada que lleva porcentajes y estados. Se conserva aunque cambien los asignados o se reabra la tarea.
- **Revisión**: aprobar o devolver crea una `RevisionTarea`, el evento y el cambio de estado en un único `SaveChanges` (consistencia). Devolver exige observación.
- **Días hábiles**: sábado, domingo y festivos de la empresa (Bloque 5, incluidos los recurrentes). Simplificación: no se consulta el horario individual de cada trabajador.
  - Una tarea está **vencida** si su fecha límite es de un día hábil anterior a hoy (zona horaria de la empresa). Si la fecha límite cae en día no hábil, vence hasta pasar el siguiente día hábil.
  - **Próxima a vencer**: faltan `DiasAlertaVencimiento` días hábiles o menos.
  - **Sin actualización**: días hábiles desde `FechaActualizacionUtc` (o la creación) ≥ `DiasSinActualizacion`; los comentarios no cuentan como actualización.
  - Vencer nunca cambia el estado ni elimina la tarea: solo se marca. Los filtros de vencidas del Bloque 7 y los conteos de proyectos usan la misma regla.
- **Alcance del panel**: con `tareas.ver_seguimiento` abarca los proyectos visibles (el filtro `trabajadorId` da el seguimiento por trabajador); sin él, solo las tareas asignadas al propio usuario. Lo que no se ve responde 404 y todo está aislado por empresa.
- **Notificaciones** nuevas (además de las del Bloque 7): `tarea.fecha_limite` (cambio de fecha) y `tarea.retirada`. Textos genéricos, respetan `PreferenciaNotificacion`.

## Endpoints

- `GET api/tareas/{id}/linea-de-tiempo`, `GET api/tareas/{id}/revisiones`
- `GET api/seguimiento/panel`, `GET api/seguimiento/revisiones`, `GET api/seguimiento/trabajador/{id}` (`tareas.ver_seguimiento`)
- `GET/PUT api/seguimiento/configuracion` (consultar con `empresa.consultar`, guardar con `empresa.configurar`)
- Aprobar, devolver y reabrir siguen en `POST api/tareas/{id}/revision` y `/reabrir` (Bloque 7).

## Pantallas (Blazor)

| Ruta | Contenido |
|---|---|
| `/seguimiento` | Contadores y listas: vencidas, próximas a vencer, pendientes de revisión, reabiertas, sin actualización |
| `/revision-tareas` | Trabajos pendientes con Aprobar / Solicitar correcciones (observación obligatoria al devolver) |
| `/tareas/{id}` | Línea de tiempo filtrable y revisiones, además del detalle del Bloque 7 |

## Límites

Fuera de la primera versión: archivos y evidencias, firmas electrónicas, chat en tiempo real, estimación automática, calendarios externos y métricas de productividad. No hay pantalla para editar `DiasAlertaVencimiento`/`DiasSinActualizacion` (solo el endpoint). El historial es de solo inserción desde la aplicación; la protección a nivel de BD (permisos o triggers) depende del script 001. Las pantallas nuevas solo se compilaron, no tienen pruebas de interfaz.
