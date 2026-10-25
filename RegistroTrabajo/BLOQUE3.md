# Bloque 3 — Administración de trabajadores y estructura organizacional

Todas las rutas requieren JWT. La empresa sale siempre del token: ninguna ruta ni cuerpo recibe `EmpresaId`, y los datos de otra empresa responden 404 (o 400 si se intenta referenciarlos desde un alta o edición).

## Aplicar antes de usar
```powershell
sqlcmd -S localhost -d RegistroTrabajo -E -b -i RegistroTrabajo.Data\Scripts\004_bloque3_permisos.sql
```
Idempotente. Inserta 12 permisos y los asigna a los roles de sistema existentes. Sin él, `bootstrap-*` falla ("catálogo incompleto").

## Permisos
| Código | Roles por defecto |
|---|---|
| `trabajadores.consultar` (datos básicos) | Administrador, RRHH, Supervisor, Trabajador |
| `trabajadores.crear`, `.editar`, `.baja`, `.ver_sensibles` | Administrador, RRHH |
| `departamentos.consultar`, `puestos.consultar` | todos |
| `departamentos.gestionar`, `puestos.gestionar`, `supervisores.gestionar` | Administrador, RRHH |
| `perfil.solicitar` | todos |
| `perfil.resolver_solicitudes` | Administrador, RRHH |

Visibilidad: sin `trabajadores.ver_sensibles` solo se ve nombre, número, puesto, departamento, estado y supervisores. Datos personales, fecha de ingreso/baja, contactos de emergencia e historial solo para quien tiene el permiso **o el propio trabajador**. Editar el expediente exige el permiso (la edición reemplaza todo el expediente).

## Endpoints
- `GET/POST /api/departamentos`, `PUT /{id}`, `POST /{id}/estado` — igual para `/api/puestos`. Se desactivan, nunca se borran; un inactivo no se asigna a nadie nuevo pero los trabajadores actuales lo conservan.
- `GET /api/trabajadores` (búsqueda, `departamentoId`, `puestoId`, `estadoLaboral`, paginado), `GET /{id}`, `GET /mi-expediente`, `POST`, `PUT /{id}` (con `RowVersion`), `POST /{id}/estado` (activo/inactivo), `POST /{id}/baja`, `POST /{id}/reingreso`, `GET /{id}/historial`.
- `GET/POST /api/trabajadores/{id}/supervisores`, `POST .../{supervisionId}/finalizar`, `GET /{id}/supervisados`.
- `GET/POST /api/trabajadores/{id}/contactos-emergencia`, `PUT/DELETE .../{contactoId}` (máximo 5, un solo principal).
- `POST /api/solicitudes-perfil`, `GET /mias`, `POST /{id}/cancelar` (trabajador); `GET /api/solicitudes-perfil`, `POST /{id}/resolver` (revisor).

## Reglas
- **Duplicados**: por empresa, mismo número de empleado, mismo correo de contacto, o mismo nombre + fecha de nacimiento → 409.
- **Cambios de puesto/departamento**: escriben `HistorialLaboral` (`Puesto`/`Departamento`, antes y después con nombres, `FechaEfectiva`, motivo, quién). También `Ingreso`, `Estado`, `Supervisor`, `Baja`, `Reingreso` y `Perfil`.
- **Baja**: fecha entre ingreso y hoy + motivo; estado Baja; desactiva la cuenta vinculada (por `Trabajadores.UsuarioId` o `Usuarios.TrabajadorId`) y revoca sus sesiones; cierra supervisiones vigentes (como supervisado y como supervisor); cancela solicitudes de perfil pendientes. No borra nada. No se puede dar de baja uno mismo ni al último administrador activo. El reingreso restablece el expediente pero **no** reactiva la cuenta (se hace en Usuarios).
- **Supervisores**: varios por trabajador, de cualquier departamento; sin autosupervisión, duplicados vigentes ni ciclos directos; un solo principal vigente. `FechaFin` inclusiva; el historial de relaciones se conserva.
- **Solicitudes de perfil**: solo correo de contacto, teléfono y dirección. Una pendiente a la vez; el expediente no cambia hasta la aprobación; se registra quién y cuándo resolvió; nadie resuelve la suya; rechazar exige motivo. Política fija por ahora: siempre con aprobación.
- **Concurrencia**: `PUT /trabajadores/{id}` compara `RowVersion` (base64) → 409 si cambió.

## Fuera de alcance (decisiones abiertas)
- Visibilidad por equipo (un supervisor viendo datos sensibles solo de sus supervisados).
- Política de aprobación configurable por empresa y cambios de contactos de emergencia vía solicitud.
- Organigrama por departamento, importación masiva de trabajadores.
