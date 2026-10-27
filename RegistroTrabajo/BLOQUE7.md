# Bloque 7 — Gestión de proyectos y tareas

Organiza proyectos, distribuye el trabajo entre los trabajadores, da seguimiento al avance de las tareas y mantiene los comentarios y el historial en un solo lugar. Se apoya en los trabajadores y la supervisión (Bloque 3), la auditoría (Bloque 1) y las notificaciones internas (que existían de forma mínima desde el Bloque 4 y aquí ganan bandeja).

## Base de datos

Las tablas ya existían desde el Bloque 2. El script `Data/Scripts/008_bloque7.sql` es idempotente y aditivo:

```
sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i RegistroTrabajo.Data\Scripts\008_bloque7.sql
```

Aplicarlo **antes** de levantar la API (el bootstrap exige el catálogo de permisos completo). Hace cuatro cosas:

1. `Tareas.Prioridad` (1..4, por defecto 2): la tabla no la tenía y la especificación la pide para las tareas.
2. Tres índices de consulta (tareas por proyecto, asignaciones por trabajador, comentarios por tarea).
3. Siembra los cinco estados de proyecto habituales en las empresas que no tengan ninguno. Las empresas nuevas los reciben la primera vez que se consultan (`EstadosProyectoService.AsegurarAsync`), así que no hay que tocar el alta de empresas.
4. 14 permisos nuevos y su asignación a los roles de sistema.

## Significado de los códigos

La BD solo limita rangos; el significado es de la aplicación (`Models/Constantes.cs`).

| Campo | Valores |
|---|---|
| `Tareas.Estado` (1..6) | 1 Pendiente · 2 En proceso · 3 En revisión · 4 Completada · 5 Reabierta · 6 Cancelada |
| `Proyectos.Prioridad` y `Tareas.Prioridad` (1..4) | 1 Baja · 2 Media · 3 Alta · 4 Urgente |
| `EstadosProyecto` | Catálogo por empresa con `Orden`, `EsFinal` (cierra el proyecto) y `Estado` (activo) |

## Permisos

| Código | Por defecto | Qué permite |
|---|---|---|
| `proyectos.consultar` | todos | Ver los proyectos en que participa (y las tareas de ellos) |
| `proyectos.consultar_todos` | Administrador | Ver y administrar todos los proyectos de la empresa |
| `proyectos.crear` | Admin, RR. HH., Supervisor | Crear proyectos |
| `proyectos.modificar` | Admin, RR. HH., Supervisor | Editar información general y cambiar de estado (no final) |
| `proyectos.asignar` | Admin, RR. HH., Supervisor | Agregar y retirar trabajadores del proyecto |
| `proyectos.cambiar_responsable` | Administrador | Cambiar al responsable principal |
| `proyectos.finalizar` | Admin, RR. HH., Supervisor | Finalizar o cancelar (solo el responsable o quien ve todo) |
| `proyectos.reabrir` | Administrador | Reabrir un proyecto finalizado |
| `proyectos.configurar_estados` | Administrador | Administrar el catálogo de estados |
| `tareas.gestionar` | Admin, RR. HH., Supervisor | Crear, editar, asignar y cancelar tareas |
| `tareas.actualizar_avance` | todos | Actualizar el avance de las tareas asignadas |
| `tareas.revisar` | Admin, RR. HH., Supervisor | Aprobar o devolver tareas en revisión y reabrir tareas cerradas |
| `tareas.comentar` | todos | Publicar comentarios |
| `notificaciones.consultar` | todos | Bandeja propia |

Un permiso no basta: además hay que **administrar el proyecto** (ser su responsable, supervisar al responsable —solo con `proyectos.modificar`— o tener `consultar_todos`).

## Endpoints

Ninguno recibe `EmpresaId`; sale del token. Lo que el usuario no puede ver responde 404.

- `GET/POST api/estados-proyecto`, `PUT api/estados-proyecto/{id}`
- `GET api/proyectos`, `GET api/proyectos/resumen`, `GET/PUT api/proyectos/{id}`, `POST api/proyectos`
- `POST api/proyectos/{id}/trabajadores`, `DELETE api/proyectos/{id}/trabajadores/{trabajadorId}`
- `POST api/proyectos/{id}/responsable`, `POST api/proyectos/{id}/estado`, `GET api/proyectos/{id}/historial`
- `GET/POST api/proyectos/{id}/tareas`
- `GET api/tareas/mis-tareas`, `GET/PUT api/tareas/{id}`, `GET api/tareas/{id}/historial`
- `POST api/tareas/{id}/asignados`, `DELETE api/tareas/{id}/asignados/{trabajadorId}`
- `POST api/tareas/{id}/avance`, `/revision`, `/reabrir`, `/cancelar`
- `GET/POST api/tareas/{id}/comentarios`, `GET api/tareas/{id}/mencionables` (no hay PUT ni DELETE de comentarios)
- `GET api/notificaciones`, `GET api/notificaciones/no-leidas`, `POST api/notificaciones/{id}/leida`, `/leidas`, `/{id}/archivar`

## Reglas de negocio

- **Visibilidad**: participantes y responsable ven el proyecto; un supervisor con `proyectos.modificar` ve y administra los de su gente; `consultar_todos` ve todo.
- **Estados de proyecto**: pasar a un estado final finaliza o cancela (`proyectos.finalizar`, solo el responsable o quien ve todo) y fija `FechaCierreUtc`; salir de un estado final reabre (`proyectos.reabrir`). Un proyecto cerrado no admite cambios ni tareas nuevas, pero conserva todo. Cada cambio queda en `HistorialEstadoProyecto`.
- **Equipo**: el responsable siempre forma parte del equipo; no se le puede retirar. Al retirar a alguien se liberan sus tareas abiertas; las completadas conservan su historial. Solo se asigna una tarea a quien participa en el proyecto.
- **Avance de tareas**: el porcentaje deduce el estado (0 pendiente, 1–99 en proceso, 100 en revisión). Quien no revisa nunca cierra una tarea: pasa a **En revisión** y el responsable la aprueba (Completada) o la devuelve con observaciones (Reabierta). Una tarea cerrada solo se mueve reabriéndola, y la reapertura conserva las actualizaciones anteriores. Cada cambio escribe `HistorialAvanceTarea`.
- **Vencidas**: fecha límite anterior a hoy (zona horaria de la empresa) y tarea no cerrada.
- **Avance del proyecto**: promedio simple del porcentaje de las tareas no canceladas. Es un indicador; la ponderación por tamaño o importancia queda para una versión posterior.
- **Comentarios**: los publican los asignados y quien administra el proyecto; conservan autor y fecha; son inmutables. Las menciones solo pueden ser a participantes del proyecto.
- **Notificaciones**: asignación a proyecto o tarea, tarea en revisión, resultado de la revisión, cambios de estado del proyecto, menciones y comentarios nuevos. Los textos son genéricos (no copian descripciones ni comentarios) y respetan `PreferenciaNotificacion`. Cada usuario solo ve y modifica su bandeja.
- **Concurrencia**: proyectos y tareas llevan `RowVersion`; una edición con una versión vieja responde 409.
- **Auditoría**: módulos `Proyectos` y `Tareas` en `IAuditoriaService`. Las tareas no se eliminan: se cancelan.

## Pantallas (Blazor)

| Ruta | Contenido |
|---|---|
| `/proyectos` | Resumen por estado, próximos a vencer y vencidos; tarjetas con progreso; alta de proyecto |
| `/proyectos/{id}` | Información, equipo, tareas con filtros, cambio de estado, responsable e historial |
| `/mis-tareas` | Tareas del trabajador agrupadas en vencidas, en curso y cerradas, con su proyecto |
| `/tareas/{id}` | Detalle, actualización de avance, revisión, asignados, historial y comentarios con menciones |
| `/proyectos/estados` | Catálogo de estados de proyecto |
| `/notificaciones` | Bandeja; la campana del encabezado muestra los avisos sin leer |

## Límites y fuera de alcance

Quedan para una versión posterior: dependencias entre tareas y diagramas, distribución automática de cargas, automatizaciones, tableros personalizables (Kanban/Gantt), adjuntos, alertas avanzadas y escalamiento, integraciones externas y ponderación del avance. La integración con el Calendario (vencimientos) queda preparada por los datos, pero no está conectada. `Tareas.ResponsableTrabajadorId` existe en la BD y no se usa: la responsabilidad se expresa con las asignaciones. Los comentarios son de tarea (la tabla admite también comentarios de proyecto, sin pantalla). La bandeja actualiza su contador al navegar, no por sondeo. No hay pruebas HTTP de extremo a extremo ni de la interfaz: las pantallas solo se compilaron.
