# Bloque 5 — Asistencia, horarios y turnos

Registro de entrada y salida con hora del servidor, turnos y horarios con vigencia, asignaciones y excepciones individuales, descansos, festivos, ubicación GPS opcional por empresa y correcciones que conservan el registro original.

## Script de base de datos
`RegistroTrabajo.Data/Scripts/006_bloque5.sql` (idempotente y aditivo; **aplicarlo antes de levantar la API**, porque el arranque exige el catálogo de permisos completo). Las 8 tablas del bloque ya existían:

```powershell
sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i RegistroTrabajo.Data\Scripts\006_bloque5.sql
```
Contenido:
- Índice único filtrado `UX_Asistencia_JornadaAbierta` en `RegistrosAsistencia (EmpresaId, TrabajadorId) WHERE Estado = 1 AND SalidaUtc IS NULL`: un trabajador solo puede tener una jornada abierta (defensa final contra entradas duplicadas concurrentes).
- 8 permisos nuevos en `PermisosSistema` y su asignación a los roles de sistema existentes.

## Significado de los códigos (la BD solo limita rangos)
| Campo | Valores |
|---|---|
| `RegistroAsistencia.Estado` | 1 Abierta (entrada sin salida = jornada incompleta) · 2 Completa · 3 Anulada |
| `DescansoTrabajador.Tipo` | 1 Habitual · 2 Excepción temporal |
| `HorarioDetalleDia.DiaSemana` | 1 lunes … 7 domingo |
| `AsignacionHorario.MotivoExcepcion` | con valor = excepción individual temporal; nulo = asignación habitual |

## Permisos
| Código | Por defecto | Qué permite |
|---|---|---|
| `asistencia.registrar` | Todos los roles | Registrar la propia entrada y salida |
| `asistencia.consultar_propia` | Todos | Ver el propio historial |
| `asistencia.consultar` | Admin, RH, Supervisor | Ver la asistencia de los trabajadores que **supervisa** (vigencia en `TrabajadorSupervisor`) |
| `asistencia.consultar_todos` | Admin, RH | Ver la asistencia de toda la empresa |
| `asistencia.corregir` | Admin, RH, Supervisor | Corregir/anular/agregar registros de los supervisados (últimos 31 días) |
| `asistencia.corregir_todos` | Admin, RH | Corregir cualquier trabajador y periodo |
| `horarios.consultar` | Admin, RH, Supervisor | Ver turnos, horarios, asignaciones, descansos y festivos |
| `horarios.configurar` | Admin, RH | Configurar todo lo anterior |

Nadie corrige sus propios registros (403). Un registro ajeno fuera del alcance responde 404.

## Endpoints (todos filtran por la empresa del token)
- Asistencia: `GET api/asistencia/estado`, `POST entrada`, `POST salida` (`asistencia.registrar`); `GET mias` (`consultar_propia`); `GET api/asistencia` y `GET {id}` (alcance validado en el servicio); `POST {id}/corregir`, `POST {id}/anular`, `POST manual` (alcance de corrección validado en el servicio).
- Turnos: `GET api/turnos`; `POST`, `PUT {id}`, `POST {id}/estado`, `POST iniciales` (`horarios.configurar`).
- Horarios: `GET api/horarios`, `GET {id}`, `GET aplicable?trabajadorId&fecha`, `GET asignaciones`; `POST`, `PUT {id}`, `POST {id}/nueva-version`, `POST {id}/estado`, `POST asignaciones`, `POST asignaciones/{id}/cerrar` (`horarios.configurar`).
- Calendario: `GET api/calendario-laboral/descansos|festivos`; `POST`/`PUT` de ambos, `POST descansos/{id}/terminar`, `DELETE festivos/{id}` (`horarios.configurar`).

## Reglas de negocio implementadas
- **Hora oficial del servidor** (`TimeProvider`): el cliente solo envía ubicación, nunca la hora.
- **Secuencia:** entrada solo sin jornada abierta (409); salida solo con jornada abierta (409). La salida cierra la jornada abierta aunque cruce la medianoche y **conserva la `FechaLaboral` de la entrada**. Solo trabajadores activos registran.
- **Fecha laboral** según `ConfiguracionEmpresa.ZonaHorariaId` (si no se reconoce, UTC). El Bloque 4 sigue usando fecha UTC.
- **GPS:** si `PermitirValidacionGps` está activo, la ubicación es obligatoria y válida (400 con mensaje claro si falta); si no, las coordenadas recibidas se descartan. La ubicación no prueba presencia física.
- **Horario aplicable** a una fecha: excepción individual → asignación habitual → horario del puesto → horario del departamento (todos con vigencia y activos), junto con festivo (incluidos recurrentes) y descanso del trabajador.
- **Asignaciones:** sin traslape del mismo tipo; la excepción exige fecha final y no sobrescribe la habitual. `CalculoDuracionService` (Bloque 4) también da prioridad a la excepción.
- **Cambios de horario sin efecto retroactivo:** un horario que ya rige para alguien no se edita; «nueva versión» cierra el anterior el día previo y traslada las asignaciones vigentes al nuevo.
- **Correcciones:** motivo obligatorio; se inserta una fila en `CorreccionesAsistencia` con valores anteriores y nuevos, responsable y fecha, en el mismo `SaveChanges` que el cambio; el original nunca se borra. Valida salida ≥ entrada, no futuro, ≤ 24 h y sin traslape con otra jornada. Anular (Estado 3) y «registro manual» también dejan fila de corrección y auditoría.
- **Concurrencia:** `RowVersion` en `RegistrosAsistencia` (409 al chocar).
- **Aislamiento:** todo filtra por `EmpresaId` del token; supervisores solo ven/corrigen a sus supervisados vigentes.

## Pantallas (Blazor)
`/asistencia` (registrar entrada/salida, estado y horario de hoy, mis registros), `/asistencia/historial` (filtros, jornadas incompletas, detalle con correcciones, corregir/anular/agregar faltante), `/horarios` (turnos, horarios con versiones, asignaciones y excepciones, descansos, festivos). La ubicación se pide con `navigator.geolocation` (`wwwroot/js/geolocalizacion.js`) solo si la empresa la exige y en contexto seguro (HTTPS o localhost).

## Límites conocidos / fuera de alcance
- No se calculan retardos, faltas injustificadas ni horas extra; `Turno.ToleranciaMinutos` se guarda pero aún no se usa. Los permisos aprobados solo se marcan como contexto en el historial.
- No hay detección automática de jornadas olvidadas: queda abierta hasta que el supervisor corrija; mientras tanto el trabajador no puede registrar otra entrada.
- `CalculoDuracionService` solo considera asignaciones directas (no el horario por puesto/departamento).
- Un día laborable con entrada cercana a medianoche podría tomar la fecha laboral del día calendario de la entrada.
- No hay exportaciones ni reportes; los festivos y descansos no se editan en pantalla (alta, baja/terminar).
