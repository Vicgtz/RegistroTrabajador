# Manual de Recursos Humanos — Registro de Trabajo

Guía paso a paso para quien tiene el rol **Recursos Humanos** (RR. HH.). Explica cómo crear, configurar y aprobar cada cosa que el rol puede hacer en el sistema.

> Los permisos de cada rol pueden ajustarse por empresa. Este manual describe lo que RR. HH. recibe **por defecto**. Si una pantalla o un botón no aparece en tu menú, tu empresa no te dio ese permiso: pídeselo al Administrador.

## Contenido
1. [Antes de empezar](#1-antes-de-empezar)
2. [Orden recomendado para configurar una empresa nueva](#2-orden-recomendado-para-configurar-una-empresa-nueva)
3. [Departamentos y puestos](#3-departamentos-y-puestos)
4. [Trabajadores](#4-trabajadores)
5. [Usuarios (cuentas de acceso)](#5-usuarios-cuentas-de-acceso)
6. [Supervisores](#6-supervisores)
7. [Solicitudes de cambio de perfil (aprobar)](#7-solicitudes-de-cambio-de-perfil-aprobar)
8. [Tipos de permiso y saldos](#8-tipos-de-permiso-y-saldos)
9. [Permisos y vacaciones (aprobar)](#9-permisos-y-vacaciones-aprobar)
10. [Horarios y turnos](#10-horarios-y-turnos)
11. [Asistencia: corregir registros](#11-asistencia-corregir-registros)
12. [Eventos de la empresa](#12-eventos-de-la-empresa)
13. [Proyectos y tareas](#13-proyectos-y-tareas)
14. [Reportes y exportación](#14-reportes-y-exportación)
15. [Resumen: qué puede y qué no puede hacer RR. HH.](#15-resumen-qué-puede-y-qué-no-puede-hacer-rr-hh)
16. [Problemas frecuentes](#16-problemas-frecuentes)

---

## 1. Antes de empezar

**Entrar al sistema**
1. Abre la dirección del sistema que te dio tu empresa.
2. Escribe tu **correo** y tu **contraseña** y pulsa **Iniciar sesión**. El ojo junto a la contraseña permite verla u ocultarla.
3. Si es tu primer acceso, el sistema te pide cambiar la contraseña. Debe tener **entre 10 y 128 caracteres, con mayúsculas, minúsculas y números**.
4. Si la olvidaste, usa **¿Olvidaste tu contraseña?** en la pantalla de inicio.

**Cómo se ve tu menú.** Verás solo las pantallas a las que tu rol tiene acceso: Inicio, Calendario, Proyectos, Tareas, Revisión de tareas, Reportes, Mis borradores, Eventos de la empresa, Usuarios, Trabajadores, Departamentos y puestos, Solicitudes de perfil, Aprobaciones de permisos, Historial de asistencia, Horarios, Configuración y las pantallas personales (asistencia, permisos, perfil).

**Reglas generales que verás en todas las pantallas**
- Nada se borra: los registros se **desactivan** o se dan de **baja**, y el historial se conserva.
- Los cambios importantes quedan en la **auditoría**.
- Las fechas y horas se muestran en la zona horaria de la empresa.
- Si dos personas editan lo mismo a la vez, el sistema avisa y pide recargar.

---

## 2. Orden recomendado para configurar una empresa nueva

Hay dependencias entre las pantallas. Si sigues este orden evitas bloqueos:

| Paso | Qué hacer | Dónde |
|---|---|---|
| 1 | Crear los **departamentos** | Departamentos y puestos |
| 2 | Crear los **puestos** | Departamentos y puestos |
| 3 | Crear los **turnos** y los **horarios** | Horarios |
| 4 | Registrar los **festivos** de la empresa | Horarios › Festivos |
| 5 | Crear los **tipos de permiso** y sus reglas | Configuración › Accesos › Tipos de permiso |
| 6 | Dar de alta a los **trabajadores** (con su cuenta de acceso) | Trabajadores |
| 7 | Asignar **supervisores** a cada trabajador | Expediente del trabajador |
| 8 | **Asignar horario** a cada trabajador | Horarios › Asignaciones |
| 9 | **Generar los saldos** del año | Saldos de permisos |

---

## 3. Departamentos y puestos

**Pantalla:** menú **Departamentos y puestos** (la pantalla muestra dos tablas: *Departamentos* y *Puestos*).

**Crear un departamento o un puesto**
1. En la tabla correspondiente pulsa **Nuevo**.
2. Escribe el **Nombre** (obligatorio) y la **Descripción** (opcional).
3. Pulsa **Guardar**.

**Editar**: pulsa **Editar** en la fila, cambia los datos y **Guardar**.

**Desactivar o activar**: desde la misma fila. Un departamento o puesto **inactivo** ya no se puede asignar a nadie nuevo, pero los trabajadores que ya lo tienen lo conservan. Nunca se borran.

La columna **Trabajadores** te dice cuántas personas lo usan.

---

## 4. Trabajadores

**Pantalla:** menú **Trabajadores**. Puedes buscar por nombre y filtrar por departamento, puesto y estado (*Activos*, *Inactivos*, *Baja*).

### 4.1 Dar de alta a un trabajador (con su cuenta)

Al crear un trabajador **se crea también su cuenta de acceso** en el mismo paso.

1. Pulsa **Nuevo trabajador**.
2. Llena los datos:
   - **Nombres** y **Apellidos** (obligatorios).
   - **Número de empleado**, **Fecha de ingreso**, **Teléfono**, **Correo de contacto**, **Fecha de nacimiento**, **Dirección**.
   - **Departamento** y **Puesto** (pueden quedar en «Sin departamento» / «Sin puesto» y asignarse después).
3. En la sección **Cuenta de acceso**:
   - **Correo de acceso**: con este correo iniciará sesión. Debe ser único en todo el sistema.
   - **Contraseña inicial**: debe cumplir las reglas (10 o más caracteres, mayúsculas, minúsculas y números). Entrégasela al trabajador por un medio seguro.
   - **Rol**: por defecto **Trabajador**. Solo puedes elegir roles cuyos permisos tú también tengas.
4. Pulsa **Guardar**.

El trabajador deberá **cambiar su contraseña** al entrar por primera vez.

> Si el trabajador **ya tiene una cuenta** creada en Usuarios, no se duplica: se vincula la cuenta existente.

### 4.2 Ver y editar el expediente

1. En la lista, pulsa **Expediente** en la fila del trabajador.
2. El expediente tiene estas secciones: *Información laboral*, *Datos personales*, *Cuenta de usuario*, *Supervisión*, *Contactos de emergencia* e *Historial laboral*.
3. Para modificar datos, pulsa **Editar**. Cambia lo necesario.
4. Si cambias el **puesto** o el **departamento**, llena **Cambio vigente desde** (fecha efectiva) y **Motivo del cambio**: quedan en el historial laboral.
5. Pulsa **Guardar**.

### 4.3 Contactos de emergencia

1. En el expediente, sección **Contactos de emergencia**, pulsa **Agregar**.
2. Llena **Nombre completo**, **Parentesco**, **Teléfono** y, si quieres, **Teléfono alterno**.
3. Marca **Contacto principal** si corresponde (solo puede haber uno).
4. **Guardar**. Máximo **5 contactos** por trabajador. Para eliminar uno, pulsa **Quitar**.

### 4.4 Dar de baja

1. Abre el expediente y pulsa **Dar de baja**.
2. Indica la **Fecha de baja** (entre su fecha de ingreso y hoy) y el **motivo**.
3. Pulsa **Confirmar baja**.

Qué pasa al dar de baja:
- El estado cambia a **Baja** y se desactiva su cuenta de acceso; sus sesiones abiertas se cierran.
- Se cierran sus relaciones de supervisión y se cancelan sus solicitudes de perfil pendientes.
- **No se borra nada**: el historial se conserva.
- No puedes darte de baja a ti mismo ni al último administrador activo.

### 4.5 Reingreso

1. Abre el expediente del trabajador dado de baja y pulsa **Reingresar**.
2. Confirma.

El reingreso **restablece el expediente pero no reactiva la cuenta**. Para que pueda entrar, ve a **Usuarios** y **actívala** (sección 5.4).

---

## 5. Usuarios (cuentas de acceso)

**Pantalla:** menú **Usuarios**. Permite crear cuentas, asignar roles y administrar el acceso. Para cada trabajador normalmente basta con el alta de la sección 4; esta pantalla sirve para administrar cuentas ya existentes o crear cuentas sueltas.

### 5.1 Crear un usuario
1. Pulsa **Nuevo usuario**.
2. Llena **Nombre**, **Apellido**, **Correo** y **Contraseña inicial**.
3. Deja marcada **Debe cambiar la contraseña al iniciar sesión** (recomendado).
4. Marca los **Roles** que tendrá.
5. Pulsa **Guardar**.

### 5.2 Editar o cambiar roles
- **Editar**: cambia nombre, apellido o correo.
- **Roles**: abre la lista de roles para marcar o quitar. No puedes cambiar tus propios roles y solo gestionas roles cuyos permisos tú posees.

### 5.3 Restablecer la contraseña
1. En la fila del usuario abre el menú **Más** y elige **Restablecer contraseña**.
2. Escribe la **Contraseña nueva** (con el ojo puedes verla) y pulsa **Restablecer**.

### 5.4 Más acciones (menú «Más»)
| Acción | Cuándo usarla |
|---|---|
| **Desbloquear** | La cuenta se bloqueó por demasiados intentos fallidos. |
| **Cerrar sesiones** | Quieres sacar al usuario de todos sus dispositivos. |
| **Desactivar** | El usuario ya no debe entrar. Se cierran sus sesiones. No aplica a tu propia cuenta. |
| **Activar** | Reactivar una cuenta desactivada (por ejemplo, tras un reingreso). |

> No se puede desactivar al último administrador de la empresa.

> La **Clave de registro** (para que los trabajadores creen su propia cuenta) y la gestión de **Roles y permisos** son del Administrador; RR. HH. puede consultar los roles pero no cambiarlos.

---

## 6. Supervisores

Un supervisor es quien aprueba permisos y revisa las ausencias y asistencia de su gente. Un trabajador puede tener **varios supervisores** de cualquier departamento, pero solo **uno principal** vigente.

**Asignar**
1. Abre el **Expediente** del trabajador.
2. En la sección **Supervisión** pulsa **Asignar supervisor**.
3. Elige al **Supervisor** y marca si es **Supervisor principal**. Indica desde cuándo.
4. Pulsa **Asignar**.

**Terminar una supervisión**: en la misma sección, pulsa **Finalizar** en la fila. La fecha de fin es inclusiva y el historial se conserva.

No se permite que alguien se supervise a sí mismo, duplicar una relación vigente ni crear ciclos directos.

---

## 7. Solicitudes de cambio de perfil (aprobar)

Los trabajadores no editan su expediente: **solicitan** el cambio desde **Mi perfil** y RR. HH. lo revisa.

**Pantalla:** menú **Solicitudes de perfil** (columnas: Trabajador, Cambios solicitados, Solicitada, Estado).

1. Revisa los **Cambios solicitados** de cada solicitud pendiente.
2. Pulsa **Aprobar** o **Rechazar**.
3. Escribe un **Comentario** (opcional al aprobar; conviene explicar el motivo al rechazar).
4. Pulsa **Confirmar**.

Al aprobar, los cambios se aplican al expediente y quedan en el historial laboral. El trabajador recibe una notificación.

---

## 8. Tipos de permiso y saldos

### 8.1 Crear los tipos de permiso
**Pantalla:** **Configuración › Accesos › Tipos de permiso** (o la ruta `/permisos/configuracion`).

**Opción rápida:** pulsa **Crear tipos iniciales** para generar los tipos básicos (por ejemplo vacaciones, cita médica, incapacidad, permiso personal).

**Crear un tipo propio**
1. Pulsa **Nuevo tipo**.
2. Llena:
   - **Nombre** y **Descripción**.
   - **Unidad del saldo**: días u horas.
   - **Requiere aprobación**: si se desmarca, la solicitud queda aprobada al crearse.
   - **Requiere comprobante**: solo es un aviso; el comprobante se verifica fuera del sistema.
   - **Permite solicitar por horas**.
3. Pulsa **Guardar**.

### 8.2 Agregar reglas a un tipo
Las reglas dicen cuándo y cuánto se puede pedir. En el tipo, pulsa **Agregar regla** y llena:
- **Vigente desde / hasta**.
- **Cantidad anual** (cuánto saldo recibe cada trabajador por año).
- **Límite por solicitud**.
- **Antigüedad mínima (meses)**.
- **Acumular el saldo no usado al año siguiente**.
- **Permitir saldo negativo**.
- **Regla activa**.

> Si un tipo tiene reglas y **ninguna está vigente** en la fecha pedida, ese tipo no está disponible para el trabajador. Un tipo sin cantidad anual no controla saldo.

### 8.3 Generar y ajustar saldos
**Pantalla:** **Saldos de permisos** (ruta `/permisos/saldos`).

**Generar los saldos del año**
1. Pulsa **Generar saldos del año**.
2. Confirma el año. Cada trabajador recibe su saldo según la regla vigente. Si la regla no reinicia el saldo, hereda el remanente del año anterior.

(El saldo también se crea automáticamente la primera vez que se usa.)

**Ajustar el saldo de un trabajador**
1. Elige al trabajador y pulsa **Ajustar saldo**.
2. Elige el **Tipo de permiso**, escribe la **Cantidad** (positiva suma, negativa resta; no puede ser cero) y el **Motivo**. Un ajuste a la baja no puede dejar el saldo por debajo de lo ya utilizado o reservado, salvo que la regla permita saldo negativo.
3. Pulsa **Guardar**.

Cada ajuste queda en el libro de **Movimientos** con quién y cuándo lo hizo.

---

## 9. Permisos y vacaciones (aprobar)

RR. HH. puede aprobar las solicitudes de **cualquier trabajador** (el supervisor solo las de su gente).

**Pantalla:** menú **Aprobaciones de permisos** (columnas: Trabajador, Tipo, Fechas, Duración, Estado, Acciones).

1. Abre la solicitud pendiente para ver el detalle. Como RR. HH. también ves el **Motivo** aunque sea privado.
2. Pulsa **Aprobar** o **Rechazar**.
3. Escribe un **Comentario** (opcional al aprobar).
4. Pulsa **Confirmar**.

Qué pasa con el saldo:
- Al **solicitar**, el saldo se **reserva**.
- Al **aprobar**, pasa a **utilizado**.
- Al **rechazar** o cancelar, se **libera**.
- Si se autoriza la cancelación de un permiso ya aprobado, se registra una **devolución**.

La solicitud no se puede aprobar si el saldo ya no alcanza (salvo que la regla permita saldo negativo) o si se traslapa con otro permiso.

**Tu propio permiso:** nadie puede resolver su propia solicitud; la resuelve otra persona con autoridad (otro usuario de RR. HH. o el Administrador). Como cualquier persona, puedes pedir permisos desde **Solicitar permiso** (ruta `/permisos`): elige el tipo, las fechas y el motivo, y pulsa **Enviar solicitud** (o **Guardar borrador**).

---

## 10. Horarios y turnos

**Pantalla:** menú **Horarios**, con cinco pestañas: **Turnos**, **Horarios**, **Asignaciones**, **Descansos** y **Festivos**.

### 10.1 Turnos
1. Pestaña **Turnos**.
2. **Opción rápida:** **Crear matutino, vespertino y nocturno**.
3. **Turno propio:** pulsa **Nuevo turno**, llena **Nombre**, **Inicio**, **Fin** y **Tolerancia (min)** (minutos de margen para llegar sin marcar retardo) y **Guardar**.
4. Para cambiarlo, **Editar**.

### 10.2 Horarios
Un horario define qué días se labora y a qué hora, con vigencia.
1. Pestaña **Horarios** › **Nuevo horario**.
2. Escribe el **Nombre**, **Vigente desde** y, si aplica, **Vigente hasta**.
3. Elige el **Turno** y, si el horario es solo para un grupo, el **Departamento** y/o el **Puesto** (déjalos en «—» para que aplique a toda la empresa).
4. Para cada **Día** marca si **Labora** y pon la **Entrada** y la **Salida**.
5. **Guardar**.

Para cambiar un horario sin perder el anterior, usa **Nueva versión**: el historial de versiones se conserva.

### 10.3 Asignar horario a un trabajador
1. Pestaña **Asignaciones** › **Asignar horario**.
2. Elige al **Trabajador** y el **Horario**.
3. Indica **Desde / Hasta**.
4. Si es un cambio individual para esa persona, escribe el **Motivo de excepción** (opcional).
5. **Guardar**. Para cerrar una asignación, pulsa **Terminar hoy**.

### 10.4 Descansos
1. Pestaña **Descansos** › **Registrar descanso**.
2. Elige al **Trabajador**, el rango **Desde / Hasta**, el **Tipo** (habitual o excepción) y una **Descripción**.
3. **Guardar**. **Terminar** cierra el descanso.

### 10.5 Festivos
1. Pestaña **Festivos** › **Nuevo festivo**.
2. Escribe la **Fecha** y el **Nombre**. Marca **Se repite cada año** si aplica.
3. **Guardar**. **Quitar** elimina un festivo.

Los festivos y los fines de semana no cuentan como días hábiles para los vencimientos de tareas ni para el cálculo de permisos.

---

## 11. Asistencia: corregir registros

Los trabajadores marcan **entrada** y **salida** desde **Registrar asistencia** (la hora la pone el servidor, no el trabajador). RR. HH. puede consultar y corregir los registros de **todos** los trabajadores.

**Pantalla:** **Historial de asistencia**. Filtra por **Trabajador**, **Desde**, **Hasta** y **Solo incompletas** y pulsa **Buscar**.

**Ver el detalle**: pulsa **Detalle** para ver el registro y la lista de **correcciones** anteriores.

**Corregir un registro**
1. Pulsa **Corregir** en la fila.
2. Ajusta la entrada o la salida.
3. Escribe el **Motivo (obligatorio)**.
4. Pulsa **Guardar corrección**.

**Anular un registro**: dentro de la misma ventana, pulsa **Anular registro** (también exige motivo).

**Agregar un registro faltante** (el trabajador olvidó marcar)
1. Pulsa **Agregar registro faltante**.
2. Elige al trabajador, la fecha y la entrada/salida, y escribe el motivo.
3. **Guardar**.

Reglas: la salida no puede ser anterior a la entrada, no se aceptan horas futuras, la jornada no puede pasar de 24 horas ni traslaparse con otra. **El registro original nunca se borra:** cada corrección guarda lo anterior, lo nuevo, quién lo hizo y cuándo.

---

## 12. Eventos de la empresa

Sirven para publicar juntas, capacitaciones y avisos en el calendario de todos o de un departamento.

**Pantalla:** menú **Eventos de la empresa**.

**Crear un evento**
1. Pulsa **Nuevo evento**.
2. Llena el **Título**, el **Tipo** (reunión general, capacitación, etc.) y la **Descripción**.
3. Marca **Todo el día** si no tiene hora; si no, indica **Inicio** y **Fin**.
4. En **Departamento** elige a quién va dirigido (toda la empresa o un departamento).
5. Pulsa **Guardar**.

**Editar:** **Editar** en la fila.
**Cancelar un evento:** pulsa **Cancelar**, escribe el **Motivo de la cancelación** y pulsa **Cancelar evento**. Queda registrado como cancelado, no se borra.

**Cumpleaños y aniversarios:** en la misma pantalla, sección **Fechas relevantes de los trabajadores**, marca **Mostrar cumpleaños** y/o **Mostrar aniversarios laborales** y pulsa **Guardar**.

Los eventos se ven en **Calendario**, junto con festivos, permisos aprobados y fechas relevantes.

---

## 13. Proyectos y tareas

RR. HH. puede crear proyectos y gestionar tareas.

### 13.1 Crear un proyecto
1. Menú **Proyectos** › **Nuevo proyecto**.
2. Llena **Nombre**, **Descripción**, **Inicio**, **Fin previsto** y **Responsable**.
3. Pulsa **Crear proyecto**.

### 13.2 Administrar el proyecto
Abre el proyecto para:
- **Editar** sus datos.
- **Cambiar responsable**.
- **Agregar** personas al **Equipo** (con su rol en el proyecto) y **Retirar** a quien ya no participa.
- **Cambiar el estado** (por ejemplo, Planeación, En curso, Finalizado); puedes escribir un comentario. Al pasar a un estado final se cierra el proyecto.
- Ver el **Historial de estados**.

### 13.3 Crear y asignar tareas
1. Dentro del proyecto pulsa **Nueva tarea**.
2. Llena **Título**, **Descripción**, **Fecha límite**, **Prioridad** y **Asignar a** (uno o varios del equipo).
3. Pulsa **Crear tarea**.

Cada persona actualiza su avance y comenta desde **Tareas**. Una tarea pasa por los estados *Pendiente*, *En proceso*, *En revisión*, *Completada*, *Reabierta* y *Cancelada*.

### 13.4 Revisar tareas
**Pantalla:** menú **Revisión de tareas**. Aparecen las tareas que están *En revisión*.
- **Aprobar**: la tarea queda *Completada*. Puedes añadir observaciones.
- **Solicitar correcciones**: escribe **qué debe corregirse**; la tarea se *Reabre* y se notifica al responsable.

### 13.5 Seguimiento
**Tareas › Seguimiento** (o **Panel de seguimiento**) muestra retrasos y pendientes: tareas vencidas, por vencer y sin actualización.

---

## 14. Reportes y exportación

**Pantalla:** menú **Reportes**.

1. Elige el **tipo de reporte** de las pestañas: trabajadores, movimientos, permisos, asistencia, proyectos, tareas y carga de trabajo.
2. Define el **Periodo**: *Rango de fechas*, *Mes*, *Trimestre* o *Año*.
3. Aplica los filtros que necesites (**Departamento**, **Puesto**, **Tipo de permiso**, **Estado**, **Buscar**, **Solo incompletos**, **Solo vencidas**) y pulsa **Aplicar filtros**. **Limpiar** los quita.
4. Para ordenar, haz clic en el encabezado de una columna.
5. Pulsa **Exportar Excel** o **Exportar PDF**. La exportación respeta los filtros aplicados.

> Los reportes de permisos **nunca incluyen el motivo**. La exportación está disponible para Administrador y RR. HH.; los supervisores solo consultan.

---

## 15. Resumen: qué puede y qué no puede hacer RR. HH.

| Puede | No puede |
|---|---|
| Crear, editar, dar de baja y reingresar trabajadores | Cambiar roles y permisos de la empresa (solo consultarlos) |
| Crear usuarios, restablecer contraseñas, desbloquear, activar y desactivar | Administrar la clave de registro ni la configuración de la empresa |
| Gestionar departamentos, puestos y supervisores | Ver la auditoría, los accesos, las sesiones de todos ni los errores técnicos |
| Aprobar o rechazar solicitudes de perfil | Configurar los estados de proyecto |
| Configurar tipos de permiso, ajustar y generar saldos | Pedir respaldos ni administrar empresas (eso es de la plataforma) |
| Aprobar permisos de cualquier trabajador y ver su motivo | |
| Configurar horarios, turnos, descansos y festivos | |
| Corregir, anular y agregar registros de asistencia de cualquiera | |
| Crear eventos y proyectos; revisar tareas | |
| Consultar y exportar reportes (Excel/PDF) | |

---

## 16. Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| «Credenciales inválidas.» al entrar | Revisa el correo y la contraseña. Tras varios intentos fallidos la cuenta se bloquea un tiempo: un administrador o RR. HH. puede **Desbloquear** en Usuarios. |
| Un trabajador dado de baja no puede entrar tras el reingreso | El reingreso no reactiva la cuenta: **Usuarios › Más › Activar**. |
| No puedo asignar un departamento o puesto | Está **inactivo**. Actívalo en Departamentos y puestos. |
| El correo del usuario ya existe | El correo es único en todo el sistema. Usa otro o vincula la cuenta existente. |
| La contraseña no se acepta | Debe tener entre 10 y 128 caracteres, con mayúsculas, minúsculas y números. |
| Una solicitud de permiso no se puede aprobar | Revisa el saldo, el traslape con otros permisos y que la regla del tipo esté vigente. |
| No aparece un tipo de permiso para el trabajador | Si el tipo tiene reglas, debe haber una vigente en esa fecha y el trabajador debe cumplir la antigüedad mínima. |
| «Alguien más modificó este registro» | Recarga la pantalla y vuelve a hacer el cambio. |
| No veo una pantalla del menú | Tu rol no tiene ese permiso. Pídeselo al Administrador. |
| Aparece «Ocurrió un error interno» | Anota la pantalla y la hora y avisa al Administrador: el error queda registrado con un identificador para investigarlo. |
