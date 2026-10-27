# Bloque 9 — Sistema de notificaciones

Centro personal de avisos dentro de la aplicación. Reutiliza lo que ya existía (tablas `Notificaciones` y `PreferenciasNotificacion`, `NotificacionesHelper`, el emisor de permisos y la bandeja del Bloque 7) y lo completa: categorías, filtros, historial, apertura segura del recurso, preferencias, prioridad y deduplicación. Las notificaciones son internas (sin correo, SMS, push ni tiempo real) y no sustituyen la auditoría del Bloque 1.

## Base de datos

`Data/Scripts/010_bloque9.sql` (idempotente y aditivo):

```
sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i RegistroTrabajo.Data\Scripts\010_bloque9.sql
```

- `Notificaciones.ClaveDeduplicacion` + índice único filtrado `(EmpresaId, UsuarioDestinoId, ClaveDeduplicacion)`.
- `PreferenciasNotificacion.Prioritaria`.
- Índice `(EmpresaId, UsuarioDestinoId, Tipo, CreadaUtc)` para el filtro por categoría.
- Sin permisos nuevos: `notificaciones.consultar` (todos los roles) basta porque la bandeja es siempre propia.

## Categorías y tipos (`CatalogoNotificaciones`, `Constantes.cs`)

Lista fija en código; no hay reglas por empresa en la primera versión. Las **obligatorias** no se pueden desactivar.

| Categoría | Tipo | Obligatoria |
|---|---|---|
| Permisos y ausencias | `permiso.solicitud_pendiente`, `permiso.resuelta`, `permiso.cancelacion_pendiente`, `permiso.cancelacion_resuelta` | Sí (todas) |
| Proyectos | `proyecto.asignado`, `proyecto.responsable` | Sí |
| Proyectos | `proyecto.estado`, `proyecto.fecha` | No |
| Tareas | `tarea.asignada`, `tarea.retirada`, `tarea.fecha_limite`, `tarea.revision_pendiente`, `tarea.revisada` | Sí |
| Tareas | `tarea.comentario` | No |
| Menciones | `tarea.mencion` | No |

Un tipo desconocido se agrupa por su prefijo y se trata como obligatorio. La categoría no se guarda: se deriva del tipo.

## Reglas

- **Consistencia**: los avisos se agregan al contexto de la operación y se guardan con su mismo `SaveChanges`; si la operación falla no queda aviso.
- **Deduplicación**: `NotificacionesHelper.NotificarAsync(..., clave)` omite el aviso si ese usuario ya tiene uno con la misma clave (en BD o pendiente en el contexto). Claves: `sp:` solicitud de permiso, `sr:` resolución, `cp:`/`cr:` cancelación, `c:` comentario (aplica a comentario y mención).
- **Preferencias**: por categoría (`PreferenciasNotificacion.TipoNotificacion` = código de categoría). `Activa` silencia solo los tipos opcionales; las categorías con todo obligatorio (Permisos) no se pueden desactivar. `Prioritaria` marca los avisos de la categoría como prioritarios en la bandeja y en la campana. Las preferencias no alteran permisos.
- **Apertura segura** (`GET api/notificaciones/{id}/destino`): solo entiende `Tarea`, `Proyecto` y `SolicitudPermiso`, vuelve a comprobar el acceso actual y devuelve la ruta (`tareas/{id}`, `proyectos/{id}`, `permisos` para el solicitante o `permisos/aprobaciones` para quien revisa). Sin acceso responde 404 y la notificación sigue visible.
- **Datos sensibles**: los textos son genéricos; no incluyen motivos, comentarios ni datos médicos.
- **Archivar** oculta de la bandeja principal, marca como leída y nunca elimina ni toca el recurso de origen; **desarchivar** la devuelve.
- **Eventos nuevos**: cambio de responsable (al nuevo responsable y al resto del equipo, incluido el anterior) y cambio de fecha de término del proyecto (al equipo). Quien hace el cambio no se avisa a sí mismo.

## Endpoints

- `GET api/notificaciones` con filtros `estado` (0 bandeja, 1 no leídas, 2 leídas, 3 archivadas, 4 todas), `categoria`, `desde`/`hasta` (fecha local de la empresa), `buscar`, `page`, `pageSize`; se conservan `soloNoLeidas` e `incluirArchivadas` del Bloque 7.
- `GET api/notificaciones/no-leidas`, `GET api/notificaciones/resumen` (total, prioritarias y por categoría)
- `POST api/notificaciones/{id}/leida`, `/leidas`, `/{id}/archivar`, `/{id}/desarchivar`
- `GET api/notificaciones/{id}/destino`
- `GET/PUT api/notificaciones/preferencias`

## Pantallas (Blazor)

| Ruta / componente | Contenido |
|---|---|
| Campana (`CampanaNotificaciones`) | Contador de no leídas; se renueva al navegar y cada 60 s. Panel con los 5 avisos más recientes sin leer |
| `/notificaciones` | Pestañas Bandeja / No leídas / Archivadas (historial) / Todas, chips de categoría con contador, búsqueda y rango de fechas, acciones de lectura y archivo |
| `/notificaciones/preferencias` | Interruptores por categoría, prioridad y lista de avisos obligatorios y opcionales |

## Límites

Fuera de la primera versión: push del navegador, correo, SMS, SignalR, automatizaciones, resúmenes programados, integraciones y reglas de obligatoriedad por empresa. La campana no actualiza en tiempo real (sondeo de 60 s). Las pantallas solo se compilaron: no hay pruebas de interfaz ni HTTP de extremo a extremo.
