# Bloque 11 — Respaldos, recuperación y continuidad

Respaldo manual de la base de datos SQL Server, historial de operaciones y procedimiento documentado de restauración.
El respaldo cubre **toda** la base (todas las empresas), por eso solo lo administra la empresa de plataforma.

## Base de datos y paquetes
Sin paquetes nuevos. Aplicar el script **antes** de levantar la API (el bootstrap exige el catálogo de permisos completo):

```powershell
sqlcmd -S localhost -d RegistroTrabajo -E -b -I -i RegistroTrabajo.Data\Scripts\012_bloque11.sql
```

`012_bloque11.sql` (idempotente): permisos `plataforma.respaldos.consultar` y `plataforma.respaldos.solicitar` (solo rol *Administrador de plataforma*),
columna `OperacionesRespaldo.HashArchivo`, CHECK de `TipoOperacion` ampliado a 1..4 e índice por fecha.

Significado de códigos (ver `DtosBloque11.cs`): `TipoOperacion` 1 Completo / 2 Diferencial / 3 Restauración / 4 Verificación;
`Estado` 1 Iniciado / 2 Completado / 3 Fallido.

## Configuración (`RegistroTrabajoApiSettings:Respaldos`)
| Clave | Valor por defecto | Uso |
|---|---|---|
| `Carpeta` | `C:\RegistroTrabajoRespaldos` | Carpeta **del servidor SQL** donde se escriben los `.bak`. |
| `Comprimir` | `true` | `WITH COMPRESSION`. |
| `MinutosOperacionVigente` | `360` | Una operación "Iniciado" más antigua se considera abandonada y deja de bloquear. |

## Endpoints (`api/respaldos`, solo plataforma)
| Método | Ruta | Permiso |
|---|---|---|
| GET | `/` (filtros tipo, estado, desde, hasta, paginado) | `plataforma.respaldos.consultar` |
| GET | `/{id}` | `plataforma.respaldos.consultar` |
| POST | `/` (`{ "tipo": 1 }` completo, `2` diferencial) | `plataforma.respaldos.solicitar` |
| POST | `/{id}/verificar` | `plataforma.respaldos.solicitar` |

No existe descarga del archivo ni restauración por HTTP. Pantalla: **Respaldos** (`/plataforma/respaldos`, menú Plataforma).

## Reglas
- Cada solicitud crea una fila *Iniciado* antes de ejecutar `BACKUP DATABASE ... WITH CHECKSUM`; luego se ejecuta `RESTORE VERIFYONLY ... WITH CHECKSUM`, se compara el SHA-256 y solo entonces pasa a *Completado*. Cualquier error la deja *Fallido*; una operación fallida nunca se registra como exitosa.
- Mensajes de error resumidos y sin el texto de la excepción (rutas, cadenas de conexión). El DTO expone solo el nombre del archivo, no la ruta del servidor.
- Un diferencial exige un completo previo *Completado*. Solo una operación en curso a la vez (409).
- Verificar un respaldo registra una operación tipo 4; falla si la huella cambió respecto a la guardada. La ruta se revalida dentro de `Carpeta`.
- Se audita (módulo `Respaldos`) inicio, resultado y verificación, sin la huella ni contenido.
- `SqlServerRespaldoEjecutor` implementa `IRespaldoEjecutor`; las pruebas usan `FakeRespaldoEjecutor`.

## Seguridad de los archivos (obligatorio antes de usar)
- La cuenta del **servicio SQL Server** necesita escritura en `Carpeta`; la cuenta con la que la API se conecta a SQL necesita `db_backupoperator` (o `BACKUP DATABASE`/`VIEW SERVER STATE`). El rol `rol_app_registrotrabajo` (script 001, pendiente) no los tiene: conceder solo lo necesario.
- La API lee el archivo para calcular el hash, así que su identidad necesita lectura en `Carpeta` (si la API y SQL están en máquinas distintas, usar una ruta UNC accesible a ambas).
- La carpeta debe estar **fuera** de `C:\inetpub` y de cualquier sitio web; permisos NTFS solo para SQL Server, la cuenta de la API (lectura) y los administradores técnicos. Cifrar el volumen o los respaldos (`WITH ENCRYPTION`) cuando haya datos reales.
- No guardar contraseñas ni tokens en logs. Guardar por separado los respaldos de BD y la configuración de la aplicación (`appsettings`, variables de entorno, clave JWT).

## Procedimiento de restauración (controlado, fuera de la aplicación)
Lo ejecuta un administrador técnico/DBA; la aplicación no restaura producción.
1. **Elegir** el respaldo en la pantalla Respaldos: estado *Completado*, y verificación reciente. Anotar nombre, tamaño y hash.
2. **Verificar** el archivo: `Get-FileHash <archivo> -Algorithm SHA256` debe coincidir con el hash del historial; `RESTORE VERIFYONLY FROM DISK = N'<archivo>' WITH CHECKSUM;`.
3. **Restaurar en un entorno controlado** (nunca primero sobre producción):
   ```sql
   RESTORE FILELISTONLY FROM DISK = N'<archivo>';
   RESTORE DATABASE RegistroTrabajo_Restauracion FROM DISK = N'<archivo>'
     WITH MOVE '<nombre lógico datos>' TO '<ruta>\RegistroTrabajo_Restauracion.mdf',
          MOVE '<nombre lógico log>'   TO '<ruta>\RegistroTrabajo_Restauracion.ldf', CHECKSUM, STATS = 10;
   -- si hay un completo y diferenciales: restaurar el completo WITH NORECOVERY y luego el diferencial WITH RECOVERY
   ```
4. **Validar**: `DBCC CHECKDB (RegistroTrabajo_Restauracion)`, conteos de tablas clave (Empresas, Usuarios, Trabajadores, RegistrosAsistencia, SolicitudesPermiso, Proyectos, Tareas, Auditoria) frente a producción y arranque de una API de prueba contra la copia.
5. **Producción** (solo con aprobación y ventana de mantenimiento): detener la API, hacer un respaldo previo del estado actual, `ALTER DATABASE RegistroTrabajo SET SINGLE_USER WITH ROLLBACK IMMEDIATE`, restaurar `WITH REPLACE`, `SET MULTI_USER`, validar y levantar la API.
6. **Registrar**: insertar una fila en `OperacionesRespaldo` (`TipoOperacion = 3`, `Estado` según el resultado, `Mensaje` resumido, `EjecutadaPorUsuarioId` de un administrador de plataforma) y una entrada en `Auditoria` si es posible.
7. **Pruebas periódicas**: restaurar un respaldo reciente en la BD alterna al menos cada mes y documentar errores y soluciones.

## Límites de la primera versión
- Sin respaldos programados, sin copia externa/nube, sin políticas automáticas de conservación (no se borra ningún `.bak`; definir cuántos y por cuánto tiempo), sin alertas de fallo y sin restauración a un punto en el tiempo (la BD está en modelo FULL, pero los respaldos de registro de transacciones no se implementan).
- **Antes de producción**: copiar los `.bak` a otro dispositivo o ubicación independiente; si el servidor falla se perderían base y respaldos.
- La prueba automática no ejecuta `BACKUP` real (un `BACKUP` no puede correr dentro de la transacción de las pruebas de esquema): `IRespaldoEjecutor` está simulado; probar manualmente en el servidor.
- Siguiente: Bloque 12 (configuración general de la empresa y parámetros del sistema).
