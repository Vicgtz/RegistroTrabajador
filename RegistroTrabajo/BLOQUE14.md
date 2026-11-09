# Bloque 14 — Expediente profesional (estudios, habilidades y currículum)

Permite registrar el historial de **estudios** (técnico, licenciatura, maestría, doctorado, certificado, curso), las **habilidades** (C#, Python, …) con nivel y el **currículum** de cada trabajador, y **buscar trabajadores por perfil** sin tener que preguntar.

## Base de datos (script `017_bloque14_expediente_profesional.sql`, idempotente y aditivo)
Aplicar ANTES de levantar la API (el arranque exige el catálogo de permisos completo):
```powershell
sqlcmd -S localhost -d RegistroTrabajo -E -f 65001 -b -I -i RegistroTrabajo.Data\Scripts\017_bloque14_expediente_profesional.sql
```
- `Habilidades`: catálogo por empresa; `NombreNormalizado` (mayúsculas, espacios colapsados) único por empresa, así «C#», «c#» y « c# » son la misma.
- `TrabajadoresHabilidad`: nivel 1 Básico / 2 Intermedio / 3 Avanzado, años de experiencia opcionales. Única por (trabajador, habilidad).
- `TrabajadoresEstudio`: `Tipo` 1 Técnico, 2 Licenciatura, 3 Maestría, 4 Doctorado, 5 Certificado, 6 Curso; fechas opcionales; `EnCurso` excluye `FechaFin`.
- `CurriculosTrabajador`: un archivo por trabajador (PDF, DOC o DOCX, máx. 5 MB) en `varbinary(max)` con SHA-256, como el logotipo.
- `Estado` de estudios y habilidades: 1 Aprobado, 2 Pendiente, 3 Rechazado (`EstadoExpediente`); guardan quién y cuándo resolvió y el motivo del rechazo.
- Permisos: `expediente.consultar|editar|aprobar|buscar`. Administrador y Recursos Humanos: los cuatro; Supervisor: consultar y buscar; Trabajador: ninguno (su propio expediente no necesita permiso).

## Reglas
- **RH edita, el trabajador propone.** Con `expediente.editar` lo registrado queda *Aprobado*. El propio trabajador agrega/edita y queda *Pendiente* hasta que alguien con `expediente.aprobar` lo apruebe o rechace (el rechazo exige motivo). Editar un registro propio ya aprobado lo devuelve a Pendiente. Un trabajador dado de baja no puede proponer.
- **Visibilidad:** el dueño y quienes editan/aprueban ven todo; quien solo consulta ve únicamente lo aprobado. **La búsqueda solo considera registros aprobados.**
- **Catálogo + alta libre:** al guardar una habilidad se reutiliza la del catálogo o se crea. Una habilidad desactivada no la puede proponer un trabajador (RH sí). RH puede renombrar/desactivar (`PUT /api/habilidades/{id}`).
- **Currículum:** el tipo se deduce de los bytes (firma `%PDF-`, OLE de `.doc`, zip con `word/document.xml` para `.docx`), no del nombre; el nombre guardado pierde rutas y toma la extensión real. Lo suben/reemplazan/eliminan RH o el propio trabajador; lo descargan RH/consultores o el propio trabajador. Se audita el hecho y el tamaño, nunca el contenido.
- Multiempresa: la empresa sale siempre del token; datos de otra empresa responden 404.

## Endpoints
| Ruta | Descripción |
|---|---|
| `GET api/trabajadores/{id}/expediente-profesional` · `GET api/trabajadores/mi-expediente-profesional` | Estudios, habilidades y datos del currículum |
| `POST api/trabajadores/{id}/habilidades` · `DELETE …/habilidades/{thId}` | Agregar/actualizar y quitar habilidad |
| `POST api/trabajadores/{id}/estudios` · `PUT/DELETE …/estudios/{estudioId}` | Estudios |
| `GET/POST/DELETE api/trabajadores/{id}/curriculum` | Descargar, subir (multipart `archivo`) y quitar |
| `GET api/expediente/pendientes` · `POST api/expediente/{habilidades\|estudios}/{id}/resolver` | Aprobación (`expediente.aprobar`) |
| `GET api/habilidades?q=` · `PUT api/habilidades/{id}` | Catálogo y autocompletado |
| `GET api/trabajadores/buscar-perfil` | Búsqueda (`expediente.buscar`): `habilidadIds` (repetido, máx. 10), `todasLasHabilidades`, `nivelMinimo`, `tipoEstudio`, `estudio` (texto en título/institución), `soloActivos` |

## Código
- Data: `Habilidad`, `TrabajadorHabilidad`, `TrabajadorEstudio`, `CurriculumTrabajador` y `RegistroTrabajoDbContext.Expediente.cs`.
- Models: `DtosBloque14.cs` (DTO, tipos, niveles y límites) y permisos en `Constantes.cs`.
- Services: `ExpedienteProfesionalService.cs`, `CurriculumService.cs`. Api: `ExpedienteProfesionalController.cs`.
- Cliente: `ExpedienteClient.cs`, componente `ExpedienteProfesionalPanel.razor` (usado en `TrabajadorExpediente` y `MiPerfil`) y página `BuscarPerfil.razor` (búsqueda y pestaña de pendientes).
- Pruebas: `Bloque14Tests.cs`; `SqlServerSchemaTests` ahora incluye el script 017.

## Pendiente / ideas
- Notificar a RH cuando hay propuestas pendientes y al trabajador cuando se resuelven (hoy se ve en la pestaña «Pendientes» y en su propio perfil).
- Fusionar habilidades equivalentes del catálogo («c sharp» vs «C#»); hoy solo se normaliza mayúsculas y espacios.
- Probar la interfaz Blazor manualmente (esta entrega se verificó con compilación y pruebas de servicios).
