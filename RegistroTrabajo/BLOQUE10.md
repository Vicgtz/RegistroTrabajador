# Bloque 10 — Panel principal, reportes y seguimiento administrativo

Dashboard por rol, reportes consultables y exportables (Excel y PDF), borradores de formularios y registro de errores técnicos. El dashboard y los reportes **no tienen tablas propias**: se calculan sobre las existentes y reutilizan las reglas de alcance de los bloques anteriores, de modo que un reporte nunca muestra algo que el módulo de origen no mostraría. `DefinicionesReporte` no se usa en esta versión.

## Base de datos y paquetes

`Data/Scripts/011_bloque10.sql` (idempotente y aditivo):

```
sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i RegistroTrabajo.Data\Scripts\011_bloque10.sql
```

- Permisos: `reportes.consultar` (todos los roles), `reportes.exportar` (Administrador y Recursos Humanos), `errores.consultar` (Administrador).
- Índices `IX_Borradores_Usuario` e `IX_ErroresTecnicos_Fecha`. Las tablas `Borradores` y `ErroresTecnicos` ya existían (Bloque 2).
- Paquetes nuevos en `RegistroTrabajo.Services`: `DocumentFormat.OpenXml` 3.1.1 (.xlsx) y `PDFsharp-MigraDoc` 6.2.1 (.pdf), ambos MIT. El restore normal falla por el origen de Telerik: usar `dotnet restore RegistroTrabajo.sln --source "$USERPROFILE/.nuget/packages"` (están en la caché local). En Windows el PDF usa las fuentes del sistema; en otros sistemas habría que registrar un `FontResolver`.

## Dashboard (`GET api/dashboard`)

Una llamada devuelve secciones opcionales según el perfil (derivado de permisos: `empresa.configurar` → Administrador, `trabajadores.editar` → Recursos Humanos, `permisos.aprobar`/`asistencia.consultar` → Supervisor, el resto → Trabajador):

| Indicador | Alcance |
|---|---|
| Trabajadores activos/inactivos/baja y altas en 30 días | Empresa (Admin, RR. HH.) o su equipo (Supervisor); no se muestra al trabajador |
| Permisos por revisar, propios pendientes y aprobados por comenzar (14 días) | Según el alcance del módulo de permisos |
| Proyectos activos/pausados/finalizados | Proyectos visibles. "Pausado" = estado cuyo nombre contiene «paus»; "finalizado" = estado final |
| Tareas por estado, vencidas y próximas a vencer | Trabajador: las asignadas; los demás: las de los proyectos visibles. Vencidas con días hábiles (Bloque 8) |
| Asistencia de hoy y jornadas anteriores sin salida | Según permisos de asistencia |
| Usuarios activos | Solo Administrador |
| Notificaciones sin leer y prioritarias | Propias |
| Actividad reciente y accesos rápidos | Eventos de tareas visibles; accesos filtrados por permiso |

Los indicadores no dan acceso a registros: cada enlace abre el módulo, que vuelve a validar la autorización.

## Reportes (`GET api/reportes/{tipo}`)

Pantalla y exportación pasan por el **mismo código** (`ReporteService`), así que comparten filtros, alcance y exclusión de datos sensibles. Siempre exigen `reportes.consultar` más el permiso del módulo de origen.

| Tipo | Contenido y alcance |
|---|---|
| `trabajadores` | Estado, departamento, puesto, supervisor, ingreso/baja, antigüedad, altas y bajas del periodo. Admin/RR. HH. (`trabajadores.editar`): toda la empresa; Supervisor: su equipo y él mismo; Trabajador: solo él. Las bajas se conservan. No muestra datos personales ni el motivo de baja |
| `movimientos` | Cambios de ingreso, puesto, departamento, supervisor, estado, baja y reingreso desde `HistorialLaboral` (los cambios de perfil personal se excluyen). El motivo solo con `trabajadores.ver_sensibles` |
| `permisos` | Trabajador, departamento, tipo, estado, fechas, quién resolvió; totales de solicitudes y de días/horas autorizados y pendientes. **Nunca incluye motivo ni comprobantes** |
| `asistencia` | Entradas/salidas, incompletas, correcciones, horario asignado y excepción. Sin nómina, horas extra ni sanciones |
| `proyectos` | Estado, responsable, avance (promedio simple de las tareas no canceladas), vencidas, en revisión y "requiere atención" |
| `tareas` | Estado, avance, prioridad, fecha límite, vencidas (días hábiles), asignados |
| `carga-trabajo` | Tareas abiertas, vencidas y completadas por trabajador. No es una medida de productividad |

Filtros: rango de fechas o periodo (`mes`/`trimestre`/`anio` + `anio` + `numero`), trabajador, departamento, puesto, supervisor, tipo de permiso, proyecto, estado, búsqueda, `ordenarPor` (lista blanca por reporte) y paginación (máx. 200). Un trabajador fuera del alcance responde 404. Los totales de cantidades se suman en memoria con tope de 50 000 filas.

## Exportación (`GET api/reportes/{tipo}/exportar?formato=xlsx|pdf`)

- Exige `reportes.exportar` en el controlador **y** en el servicio (ocultar el botón en Blazor no basta); Supervisor y Trabajador reciben 403 aunque pueden consultar.
- Tope de 10 000 filas (Excel) y 2 000 (PDF); si se excede, 400 pidiendo acotar los filtros.
- Excel: encabezado con empresa, título, fecha y filtros; tabla con filtro automático, fechas reales; hoja «Resumen». Los textos van como cadenas en línea (no se interpretan como fórmulas). PDF: A4 horizontal, encabezado de empresa, filtros, resumen y «Página x de y».
- Se audita `Reportes/Exportar` con tipo, formato, número de filas y filtros, nunca el contenido.

## Borradores (`api/borradores`)

Solo el creador los consulta, edita o elimina (los ajenos y los de la administración responden 404). Se admiten los módulos `Permisos`, `Trabajadores`, `Proyectos` y `Tareas`, cada uno con el permiso de crear correspondiente. El JSON debe ser un objeto válido de hasta 20 000 caracteres y sin claves tipo contraseña, token o secreto. Máximo 50 por usuario; vencen a los 30 días sin cambios. Un borrador no es una solicitud: ningún otro servicio lo lee, así que no aparece en permisos, saldos, aprobaciones, calendarios ni notificaciones. Al enviar el formulario, el cliente llama al endpoint normal (que valida y crea el registro) y luego elimina el borrador.

Interfaz: `/borradores` (lista, continuar, descartar) y «Guardar borrador» en la solicitud de permisos. Proyectos, tareas y trabajadores ya tienen servicio y API, pero sus formularios aún no incluyen el botón.

## Errores técnicos (`GET api/errores-tecnicos`, `errores.consultar`)

`ErrorHandlingMiddleware` registra cada excepción no controlada con un alcance propio y su propio try/catch (un fallo al registrar nunca cambia la respuesta 500 genérica). Se guarda fecha, nivel, tipo de excepción, método y ruta **sin parámetros ni segmentos que parezcan tokens**, `CorrelationId` (el mismo que recibe el usuario), empresa/usuario del token y la pila de métodos. No se guarda el mensaje de la excepción, contraseñas, tokens, cabeceras ni cuerpos. Cada empresa ve sus errores; los que no pudieron asociarse a una empresa los ve la de plataforma. Es independiente de la auditoría de negocio.

## Pantallas

`/` (dashboard por rol, con barras y dona en SVG), `/reportes` (pestañas, filtros, orden, paginación y exportación), `/borradores`, `/errores-tecnicos`; entradas nuevas en el menú.

## Límites

Fuera de la primera versión: indicadores predictivos, reportes configurables, nómina y horas extra, análisis de productividad, envíos programados por correo, monitoreo externo y flujos de aprobación de borradores. Los reportes de permisos no admiten filtrar por aprobador. Las pantallas solo se compilaron: no hay pruebas de interfaz ni HTTP de extremo a extremo. El Bloque 11 (respaldos) queda pendiente.
