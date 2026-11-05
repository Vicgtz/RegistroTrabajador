# Manual del Administrador — Registro de Trabajo

Guía paso a paso para quien tiene el rol **Administrador** de una empresa. El Administrador tiene **todos los permisos de la empresa**: hace todo lo que hace Recursos Humanos y además controla la seguridad, los roles, la configuración y la auditoría.

> Este manual se centra en lo que **solo el Administrador** puede hacer. Para las tareas diarias de personal (altas de trabajadores, permisos, horarios, asistencia, eventos, proyectos, reportes) consulta el **Manual de Recursos Humanos**; para las de supervisión, el **Manual del Supervisor**. Aquí solo se resumen.

## Contenido
1. [Responsabilidades del Administrador](#1-responsabilidades-del-administrador)
2. [Primeros pasos de una empresa nueva](#2-primeros-pasos-de-una-empresa-nueva)
3. [Configuración de la empresa](#3-configuración-de-la-empresa)
4. [Roles y permisos](#4-roles-y-permisos)
5. [Usuarios y cuentas](#5-usuarios-y-cuentas)
6. [Clave de registro](#6-clave-de-registro)
7. [Sesiones activas](#7-sesiones-activas)
8. [Historial de accesos](#8-historial-de-accesos)
9. [Auditoría](#9-auditoría)
10. [Errores técnicos](#10-errores-técnicos)
11. [Estados de proyecto](#11-estados-de-proyecto)
12. [Lo que también puede hacer (resumen)](#12-lo-que-también-puede-hacer-resumen)
13. [Reglas de seguridad que debes conocer](#13-reglas-de-seguridad-que-debes-conocer)
14. [Lo que NO es del Administrador](#14-lo-que-no-es-del-administrador)
15. [Problemas frecuentes](#15-problemas-frecuentes)

---

## 1. Responsabilidades del Administrador

- Mantener la **configuración** de la empresa (datos, apariencia, formato regional, seguridad de sesiones).
- Administrar **roles y permisos**: quién puede hacer qué.
- Administrar **cuentas de usuario** y su seguridad (restablecer contraseñas, bloqueos, sesiones).
- Vigilar la **auditoría**, los **accesos** y los **errores técnicos**.
- Definir los **estados de proyecto** que usa la empresa.
- Respaldar a Recursos Humanos y a los supervisores en cualquier tarea de personal.

Tu menú incluye, además de las pantallas de RR. HH.: **Roles y permisos**, **Sesiones**, **Accesos**, **Auditoría**, **Errores técnicos** y **Estados de proyecto**; y **Configuración** con edición completa.

---

## 2. Primeros pasos de una empresa nueva

La empresa y su primer Administrador los crea la **plataforma** (ver sección 14). Cuando recibas tu acceso:

1. **Inicia sesión** con el correo y la contraseña inicial y **cámbiala** en el primer acceso (10 a 128 caracteres, con mayúsculas, minúsculas y números).
2. **Configuración › Datos generales:** completa razón social, nombre comercial, RFC, teléfono y correo (sección 3.1).
3. **Configuración › Apariencia** y **Regional:** logotipo, colores, zona horaria y formatos (secciones 3.2 y 3.3).
4. **Configuración › Parámetros:** revisa el cierre por inactividad y la duración de sesión (sección 3.4).
5. **Roles y permisos:** revisa lo que recibe cada rol y crea los roles propios que necesites (sección 4).
6. Crea los **departamentos y puestos**, **horarios**, **tipos de permiso** y da de alta a los **trabajadores** (o delega en Recursos Humanos). El orden recomendado está en el *Manual de Recursos Humanos*, sección 2.
7. Decide si activarás la **clave de registro** para que los trabajadores creen su cuenta (sección 6).

---

## 3. Configuración de la empresa

**Pantalla:** **Configuración** (título «Configuración de la empresa»). Tiene cinco pestañas. Los cambios se **auditan**; la personalización **no altera** la seguridad ni los controles obligatorios.

### 3.1 Datos generales
1. Pestaña **Datos generales**.
2. Edita **Razón social**, **Nombre comercial**, **RFC**, **Teléfono** y **Correo de contacto**.
3. Pulsa **Guardar datos**.

### 3.2 Apariencia
1. Pestaña **Apariencia**.
2. **Nombre mostrado en la aplicación** y **Texto institucional** (si dejas el nombre vacío se usa la razón social).
3. **Color principal** y **Color secundario**: elige un color; **Predeterminado** restaura el original.
4. **Logotipo** (PNG, JPEG o WebP, **máximo 512 KB**): súbelo; **Quitar logotipo actual** lo elimina. El sistema valida que el archivo sea realmente una imagen.
5. Revisa la **Vista previa** (los colores ya se aplican en pantalla pero **no se guardan** hasta que pulses el botón).
6. Pulsa **Guardar apariencia**, o **Descartar cambios** para volver a lo guardado.

### 3.3 Regional
1. Pestaña **Regional**.
2. Elige la **Zona horaria** (las fechas de asistencia, permisos, eventos y auditoría se interpretan con ella), la **Cultura** (por ejemplo `es-MX`), la **Moneda** (opcional, por ejemplo `MXN`), el **Formato de fecha**, el **Formato de hora** y el **Primer día de la semana**.
3. Pulsa **Guardar configuración regional**.

### 3.4 Parámetros
1. Pestaña **Parámetros**.
2. **Cierre por inactividad (minutos):** entre 5 y 1440.
3. **Duración máxima de sesión (horas):** entre 1 y 168.
4. **Permitir validación por GPS en la asistencia:** si lo marcas, los trabajadores deben compartir su ubicación al marcar entrada y salida.
5. Pulsa **Guardar parámetros**.

### 3.5 Accesos
La pestaña **Accesos** es un menú de atajos a las pantallas de administración: Departamentos y puestos, Tipos de permiso, Horarios y turnos, Estados de proyecto, Calendario y eventos, Seguimiento de tareas, Usuarios, Roles y permisos, Sesiones activas y Auditoría. Solo aparecen las que tu rol puede usar.

---

## 4. Roles y permisos

**Pantalla:** **Roles y permisos**. Cada empresa tiene cuatro **roles de sistema** (Administrador, Recursos Humanos, Supervisor, Trabajador) y puede crear **roles propios**.

### 4.1 Ver los permisos de un rol
1. Elige un rol de la lista.
2. A la derecha ves los permisos agrupados, con casillas marcadas para los que el rol tiene.

### 4.2 Crear un rol propio
1. Pulsa **Nuevo rol**.
2. Escribe el **Nombre** y la **Descripción**.
3. Pulsa **Guardar**.
4. Selecciona el rol nuevo, marca los permisos que tendrá y pulsa **Guardar permisos**.

### 4.3 Cambiar los permisos de un rol
1. Selecciona el rol.
2. Marca o desmarca los permisos.
3. Pulsa **Guardar permisos** (el botón se habilita cuando hay cambios).

### 4.4 Editar, desactivar o activar un rol propio
Con el rol seleccionado pulsa **Editar** para cambiar nombre o descripción, y **Desactivar** / **Activar** para cambiar su estado. Los roles de **sistema** no se pueden editar ni desactivar.

### 4.5 Asignar roles a una persona
Se hace desde **Usuarios › Roles** (sección 5.3), no desde esta pantalla.

### Reglas importantes
- Los **permisos del rol Administrador son inmutables**: no se pueden quitar.
- **Solo puedes dar o quitar permisos que tú mismo tienes** (por eso un rol personalizado nunca supera al Administrador).
- **No puedes cambiar tus propios roles.**
- Ajustar los permisos de un rol **afecta a todas las personas que lo tienen**, de inmediato: el sistema revisa el permiso en cada solicitud.
- Antes de cambiar, usa **Permisos efectivos** (sección 5.4) para ver el resultado real de una persona.

---

## 5. Usuarios y cuentas

**Pantalla:** **Usuarios**. Los pasos de crear, editar, asignar roles, restablecer contraseña, desbloquear, cerrar sesiones, activar y desactivar están explicados en el *Manual de Recursos Humanos*, sección 5. Resumen:

### 5.1 Crear un usuario
**Nuevo usuario** › Nombre, Apellido, Correo, Contraseña inicial › deja marcada **Debe cambiar la contraseña al iniciar sesión** › elige los **Roles** › **Guardar**.

> Para dar de alta a un **trabajador** conviene usar **Trabajadores › Nuevo trabajador**, que crea el expediente y la cuenta juntos.

### 5.2 Acciones por usuario
- **Editar**, **Roles**, y en el menú **Más**: **Restablecer contraseña**, **Desbloquear**, **Cerrar sesiones**, **Activar** y **Desactivar**.
- No puedes **desactivarte a ti mismo** ni desactivar al **último administrador activo** de la empresa.

### 5.3 Asignar o quitar roles
1. En la fila del usuario pulsa **Roles**.
2. Marca o desmarca los roles.
3. Guarda.

Solo puedes administrar roles cuyos permisos tú posees y no los tuyos.

### 5.4 Permisos efectivos de un usuario
En la fila del usuario pulsa **Permisos**: se abre una ventana «Permisos efectivos de…» con sus **roles** y la lista final de **permisos**. Úsala para entender por qué alguien puede (o no) hacer algo.

---

## 6. Clave de registro

Permite que los trabajadores **creen su propia cuenta** desde **Crear cuenta con clave de registro** en la pantalla de inicio, sin intervención de nadie. La cuenta que se crea tiene el rol **Trabajador**.

**Pantalla:** **Usuarios**, panel **Clave de registro** (arriba).

**Activar**
1. Pulsa **Activar registro**.
2. El sistema genera una clave y te la **muestra una sola vez**. Cópiala en ese momento: **no se puede volver a ver**.
3. Compártela solo con las personas que deban registrarse.

**Cambiar la clave:** pulsa **Generar clave nueva**. La anterior deja de funcionar al instante.

**Desactivar:** pulsa **Desactivar**. Nadie podrá registrarse por su cuenta hasta que actives de nuevo.

Seguridad:
- Solo se guarda un **resumen cifrado** (hash) de la clave, nunca la clave en claro.
- Cada clave identifica **una sola empresa**.
- Si la clave se filtró, genérala de nuevo o desactiva el registro.
- Después del registro, **Recursos Humanos** debe vincular la cuenta con el expediente del trabajador si aún no existe.

---

## 7. Sesiones activas

**Pantalla:** **Sesiones** («Sesiones activas»). Muestra las sesiones abiertas de **todos** los usuarios de tu empresa: usuario, dispositivo, IP, inicio, última actividad y cuándo expira.

**Cerrar una sesión**
1. Localiza la sesión sospechosa o que quieres terminar.
2. Pulsa **Cerrar** (o **Salir** si es la tuya).

El usuario tendrá que iniciar sesión de nuevo. Para cerrar **todas** las sesiones de una persona, usa **Usuarios › Más › Cerrar sesiones**.

---

## 8. Historial de accesos

**Pantalla:** **Accesos** («Historial de accesos»). Lista cada intento de inicio de sesión, **exitoso o fallido**: Fecha, Correo, Resultado y Detalle.

1. Elige **Desde** y **Hasta**.
2. Pulsa **Filtrar**.

Cómo usarlo:
- Varios **fallos** seguidos para un mismo correo indican intentos de adivinar la contraseña o un usuario que la olvidó.
- Si un usuario legítimo quedó **bloqueado**, desbloquéalo desde **Usuarios › Más › Desbloquear**.

---

## 9. Auditoría

**Pantalla:** **Auditoría**. Es el registro de los cambios importantes (altas, bajas, cambios de roles, aprobaciones, correcciones, configuración…). **Solo se agrega información; nada se modifica ni se borra desde la aplicación.**

1. Filtra por **Desde**, **Hasta** y **Entidad** (por ejemplo, Usuario, Trabajador, Rol).
2. Pulsa **Filtrar**. Cada fila muestra **Fecha**, **Módulo**, **Acción** y quién la hizo.
3. Pulsa **Detalle** para ver la operación completa («Detalle de la operación»).

La auditoría **redacta** automáticamente contraseñas, hashes y tokens: nunca los guarda. Los motivos privados de permisos tampoco aparecen.

---

## 10. Errores técnicos

**Pantalla:** **Errores técnicos**. Cuando algo falla de forma inesperada, la persona ve «Ocurrió un error interno» con un código de seguimiento, y el sistema guarda el error aquí.

1. Filtra por **Código de correlación** (el que reporta la persona), **Desde**, **Hasta** o texto en **Buscar**.
2. Pulsa **Buscar**.
3. Cada fila muestra Fecha, Nivel, Tipo, **Ruta** (sin parámetros sensibles) y **Correlación**.

Cuando un usuario reporte un error, pídele la hora y la pantalla, o el código de correlación, y búscalo aquí. No se guardan contraseñas, tokens ni mensajes internos con datos personales.

> Además, el servidor conserva un registro en archivos (carpeta `logs` de la API) con los errores de la API y los que reporta el navegador. Eso lo consulta quien administra el servidor.

---

## 11. Estados de proyecto

**Pantalla:** **Estados de proyecto** (desde **Proyectos › Estados** o **Configuración › Accesos**). Define los estados por los que pasa un proyecto (por ejemplo Planeación, En curso, Pausado, Finalizado).

**Crear un estado**
1. Pulsa **Nuevo estado**.
2. Escribe el **Nombre** y el **Orden** (posición en la lista).
3. Marca **Estado final (cierra el proyecto)** si al llegar a él el proyecto se considera terminado o cancelado.
4. Deja marcado **Activo**.
5. Pulsa **Guardar**.

**Editar:** pulsa **Editar** en la fila. La columna **Proyectos** muestra cuántos proyectos usan ese estado.

Notas:
- Un proyecto en estado **final** ya no admite cambios ni tareas nuevas, pero conserva su historial; salir de un estado final lo **reabre**.
- «Pausado», en los reportes, es cualquier estado cuyo nombre contenga «paus».
- Desactivar un estado impide usarlo en proyectos nuevos; no borra nada.

---

## 12. Lo que también puede hacer (resumen)

El Administrador puede realizar **todas** las tareas descritas en los otros manuales:

| Área | Qué puede hacer | Dónde está explicado |
|---|---|---|
| Trabajadores | Alta con cuenta, expediente, contactos, baja, reingreso | Manual RR. HH., secciones 4 y 6 |
| Estructura | Departamentos, puestos, supervisores | Manual RR. HH., secciones 3 y 6 |
| Perfil | Aprobar o rechazar solicitudes de cambio de perfil | Manual RR. HH., sección 7 |
| Permisos | Tipos y reglas, saldos, aprobar permisos de cualquiera | Manual RR. HH., secciones 8 y 9 |
| Tiempo | Turnos, horarios, descansos, festivos, correcciones de asistencia | Manual RR. HH., secciones 10 y 11 |
| Calendario | Eventos de la empresa, cumpleaños y aniversarios | Manual RR. HH., sección 12 |
| Proyectos y tareas | Crear, administrar, **cambiar al responsable**, revisar tareas | Manual Supervisor, secciones 6 a 9 |
| Reportes | Consultar de toda la empresa y **exportar a Excel y PDF** | Manual RR. HH., sección 14 |
| Cuenta propia | Asistencia, permisos, perfil, contraseña y sesiones | Manual del Trabajador |

Dos puntos extra del Administrador en proyectos: **cambiar al responsable** de un proyecto (el supervisor y RR. HH. no pueden) y ver **todos** los proyectos de la empresa.

---

## 13. Reglas de seguridad que debes conocer

- **Multiempresa:** solo ves y administras datos de **tu empresa**. Lo de otra empresa responde como si no existiera.
- **Permisos en cada solicitud:** el sistema revisa en la base de datos tu sesión, tu usuario, tu empresa y tu permiso en **cada** acción. Ocultar un botón es solo visual; el control real está en el servidor.
- **Contraseñas:** 10 a 128 caracteres, con mayúsculas, minúsculas y números. Se guardan cifradas; nadie (ni el Administrador) puede verlas. Solo se pueden **restablecer**.
- **Bloqueo por intentos fallidos:** tras varios intentos la cuenta se bloquea un tiempo. El mensaje de error siempre es el mismo («Credenciales inválidas.») para no revelar si el correo existe.
- **Sin borrados:** trabajadores, usuarios, departamentos, puestos, roles y proyectos se desactivan o se dan de baja; el historial se conserva.
- **No eliminar al último administrador:** el sistema impide desactivar o dar de baja al último administrador activo.
- **Nadie resuelve lo propio:** no puedes aprobar tu propio permiso ni darte de baja.
- **Tu propia seguridad:** no compartas tu cuenta, cierra sesión en equipos compartidos y revisa de vez en cuando **Sesiones** y **Accesos**.

---

## 14. Lo que NO es del Administrador

Algunas funciones son **solo de la plataforma** (la cuenta de «Administrador de plataforma» que opera el sistema completo), no de una empresa:

| Función | Quién la hace |
|---|---|
| **Crear empresas** y su primer administrador, y activarlas, suspenderlas o desactivarlas | Administrador de plataforma (pantalla **Empresas**) |
| **Estado del sistema** | Administrador de plataforma |
| **Respaldos** de la base de datos | Administrador de plataforma |
| Aplicar scripts de base de datos, publicar el sistema, configurar correo y claves del servidor | Quien administra el servidor |

Los permisos `plataforma.*` no valen dentro de una empresa normal, ni siquiera para el Administrador.

---

## 15. Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| Un usuario no puede entrar | Revisa en **Accesos** el resultado. Si está bloqueado, **Desbloquear**; si está inactivo, **Activar**; si olvidó la contraseña, **Restablecer contraseña**. |
| Un trabajador dado de baja no puede entrar tras reingresar | El reingreso no reactiva la cuenta: **Usuarios › Más › Activar**. |
| Alguien dice que no ve una pantalla | Revisa sus **Permisos efectivos** (Usuarios › Permisos) y los permisos de su rol en **Roles y permisos**. |
| No puedo quitarle un permiso a un rol | Los permisos del rol Administrador no se pueden quitar, o es un permiso que tú no tienes. |
| No puedo cambiar mis propios roles | Es una protección del sistema. Pídele a otro administrador. |
| No puedo desactivar un usuario | Es tu propia cuenta o el último administrador activo. |
| Perdí la clave de registro | No se puede recuperar: pulsa **Generar clave nueva** y compártela. |
| Un usuario reporta «Ocurrió un error interno» | Búscalo en **Errores técnicos** por hora o por **código de correlación**. |
| Sospecho que una cuenta fue comprometida | **Usuarios › Más › Cerrar sesiones**, luego **Restablecer contraseña**, y revisa **Accesos** y **Auditoría**. |
| «Alguien más modificó este registro» | Recarga la pantalla y repite el cambio. |
| Un dato aparece con caracteres raros (acentos) | Avísale a quien administra la base de datos; suele deberse a un script aplicado con la codificación incorrecta (usar `sqlcmd -f 65001`). |
