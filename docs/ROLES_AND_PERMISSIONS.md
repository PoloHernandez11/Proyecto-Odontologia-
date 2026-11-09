# Roles y permisos

La autorización es **por permiso granular**, nunca por nombre de rol. Un rol es un conjunto de permisos. Un usuario puede
pertenecer a varios consultorios y tener varios roles en cada uno. Sus permisos efectivos en el consultorio activo son la unión
de los de sus roles. `GET /api/v1/auth/me` devuelve la lista para que el frontend muestre u oculte opciones (la validación
real siempre la hace el servidor).

Roles de **sistema** (sembrados por migración, no editables): `Administrador`, `Odontólogo`, `Secretaria`.
Cada consultorio puede crear **roles personalizados** (`/api/v1/roles`).

## Reglas anti-escalada

- Nadie puede otorgar (en roles, invitaciones o asignaciones) **ni quitar** permisos que no posee.
- Nadie puede modificar sus propios roles ni desactivarse a sí mismo.
- Un rol personalizado asignado al propio usuario no puede editarlo ese usuario.
- El consultorio debe conservar siempre al menos un administrador activo (`users.manage` + `roles.manage`).
- Desactivar un miembro revoca sus sesiones en ese consultorio y le quita el acceso **en la siguiente solicitud**.

## Mínimo privilegio

- `Administrador` **no** incluye acceso clínico. Quien administra y atiende (como la Dra. Méndez del diseño) tiene los roles
  `Administrador` + `Odontólogo`. Es lo que asigna el registro público cuando `isProfessional=true`.
- `Secretaria` **no** accede a historia clínica, evoluciones, diagnósticos, odontograma, tratamientos, antecedentes completos
  ni documentos clínicos. Sí ve las **alertas médicas relevantes** para la atención (`patients.alerts.read`): sólo el título
  (p. ej. "Anticoagulado", "Alergia: Penicilina"), nunca medicación ni otros antecedentes.
- Sin `agenda.all_professionals`, un profesional sólo ve y gestiona **su propia** agenda y sus turnos (los de otros responden 404).
- Registrar información clínica u odontograma exige además ser **profesional** del consultorio (`IsProfessional`).
- Corregir o anular una evolución clínica sólo lo puede hacer el **profesional autor**.

## Matriz

| Permiso | Descripción | Admin | Odontólogo | Secretaria |
|---|---|:-:|:-:|:-:|
| `consultorio.read` | Ver datos y gabinetes del consultorio | ✔ | ✔ | ✔ |
| `consultorio.manage` | Editar datos, configuración y gabinetes | ✔ | | |
| `users.read` | Ver usuarios y roles | ✔ | ✔ | ✔ |
| `users.manage` | Invitar, asignar roles, activar/desactivar | ✔ | | |
| `roles.manage` | Crear/editar roles personalizados | ✔ | | |
| `audit.read` | Consultar auditoría | ✔ | | |
| `patients.read` | Buscar y ver pacientes (datos administrativos) | ✔ | ✔ | ✔ |
| `patients.write` | Alta/edición de pacientes | | ✔ | ✔ |
| `patients.medical.read` | Antecedentes médicos completos y detalle de alertas | | ✔ | |
| `patients.medical.write` | Editar antecedentes y alertas | | ✔ | |
| `patients.alerts.read` | Títulos de alertas médicas relevantes | | ✔ | ✔ |
| `insurances.manage` | Obras sociales | ✔ | | ✔ |
| `agenda.read` | Ver agenda y disponibilidad | ✔ | ✔ | ✔ |
| `agenda.manage` | Horarios laborales, bloqueos, excepciones, feriados | ✔ | ✔ (propia) | ✔ |
| `agenda.all_professionals` | Operar sobre la agenda de todos los profesionales | ✔ | | ✔ |
| `appointments.read` | Ver turnos e historial | | ✔ | ✔ |
| `appointments.write` | Crear, modificar, reprogramar, cambiar estado | | ✔ | ✔ |
| `appointments.overbook` | Sobreturno / urgencia (permite superposición) | | ✔ | ✔ |
| `tasks.read` / `tasks.write` | Tareas | ✔ | ✔ | ✔ |
| `clinical.read` / `clinical.write` | Historia clínica, evoluciones, diagnósticos | | ✔ | |
| `odontogram.read` / `odontogram.write` | Odontograma | | ✔ | |
| `treatments.read` / `treatments.write` | Planes de tratamiento | | ✔ | |
| `catalog.manage` | Prestaciones, valores y condiciones del odontograma | ✔ | | |
| `budgets.read` | Ver presupuestos | ✔ | ✔ | ✔ |
| `budgets.write` | Crear/editar/emitir presupuestos | | ✔ | |
| `budgets.approve` | Aprobar, rechazar, cancelar presupuestos | | ✔ | |
| `finance.read` | Pagos y cuenta corriente | ✔ | ✔ | ✔ |
| `payments.write` | Registrar pagos | | ✔ | ✔ |
| `payments.void` | Anular pagos (con motivo y contraasiento) | ✔ | | |
| `lab.read` | Ver trabajos de laboratorio | | ✔ | ✔ |
| `lab.write` | Crear/editar trabajos, colocar o cancelar | | ✔ | |
| `lab.track` | Seguimiento logístico (enviado, listo, recibido) | | ✔ | ✔ |
| `laboratories.manage` | Alta de laboratorios | ✔ | | |
| `documents.read` | Ver/descargar documentos no clínicos | ✔ | ✔ | ✔ |
| `documents.clinical.read` | Ver/descargar/cargar documentos clínicos (Rx, estudios, recetas, fotos) | | ✔ | |
| `documents.write` | Cargar documentos | | ✔ | ✔ |
| `documents.delete` | Baja lógica de documentos | ✔ | ✔ | |
| `consents.read` / `consents.write` | Consentimientos informados | | ✔ | ✔ |
| `messaging.send` | Recordatorios y registro de mensajes salientes | ✔ | | ✔ |
| `dashboard.reception` | Dashboard de recepción | ✔ | | ✔ |
| `dashboard.clinical` | Dashboard del odontólogo | | ✔ | |

El catálogo completo en tiempo de ejecución está en `GET /api/v1/permissions`. Cada operación de OpenAPI lista su permiso
(campo `x-permissions` y la descripción).
