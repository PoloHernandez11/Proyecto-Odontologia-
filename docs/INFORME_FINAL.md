# Informe final — Backend Odontia

Fecha: 09/11/2026 · Ubicación: `Proyecto Odontologia/Odontia`

## 1. Arquitectura final

Solución .NET 10 en 4 capas más 2 proyectos de pruebas: `Odontia.Domain`, `Odontia.Application`, `Odontia.Infrastructure`,
`Odontia.Api`, `Odontia.UnitTests` y `Odontia.IntegrationTests`. Los controllers son delgados, la lógica vive en los servicios
de aplicación y en el dominio, la persistencia usa EF Core 10 con MySQL, y no hay repositorios genéricos.
Detalle en [ARCHITECTURE.md](ARCHITECTURE.md).

## 2. Módulos implementados

Consultorios y configuración (gabinetes, zona horaria, duración de turno, intervalo de agenda, recordatorios) · Usuarios,
roles personalizados, permisos granulares e invitaciones · Perfil y preferencias de notificación · Pacientes (búsqueda por
nombre/DNI/teléfono, filtros, resumen, historial administrativo, antecedentes restringidos, alertas) · Obras sociales ·
Agenda (día/semana/mes, resumen del día, disponibilidad, horarios laborales, bloqueos, días no laborables, excepciones) ·
Turnos (alta, modificación, reprogramación, máquina de estados completa, sobreturno, historial, concurrencia) · Tareas ·
Historia clínica (evoluciones versionadas, anulación, diagnósticos, auditoría de acceso) · Odontograma FDI normalizado con
historial · Catálogo de prestaciones y planes de tratamiento con progreso · Presupuestos con valores históricos · Pagos y
cuenta corriente con anulación por contraasiento · Laboratorios y trabajos con seguimiento de entregas · Documentos privados
y consentimientos · Notificaciones internas, mensajería saliente y recordatorios · Dashboards de recepción y clínico ·
Auditoría · Health checks.

## 3. Entidades, tablas y relaciones

53 tablas de aplicación (+ `__EFMigrationsHistory`) y 134 claves foráneas, muchas compuestas por consultorio. Ver
[DATABASE.md](DATABASE.md) (diagrama ER, restricciones e índices).

## 4. Endpoints

168 operaciones REST bajo `/api/v1` en 22 grupos, todas documentadas en OpenAPI con su permiso. Ver [ENDPOINTS.md](ENDPOINTS.md)
y la colección `postman/Odontia.postman_collection.json`.

## 5. Autenticación

Identity + JWT de 15 minutos. Refresh token rotativo en cookie HttpOnly/Secure/SameSite, guardado sólo como hash, con
detección de reuso. Incluye CSRF de doble envío, lockout, anti-enumeración, 2FA TOTP con códigos de recuperación,
confirmación de correo, recuperación y cambio de contraseña, invitaciones, gestión y revocación de sesiones, e invalidación
inmediata por security stamp.

## 6. Roles y permisos

46 permisos y 3 roles de sistema (Administrador 22, Odontólogo 35, Secretaria 25), más roles personalizados por consultorio
con reglas anti-escalada. Ver [ROLES_AND_PERMISSIONS.md](ROLES_AND_PERMISSIONS.md).

## 7. Seguridad

Ver [SECURITY.md](SECURITY.md): aislamiento por consultorio en tres niveles (claim firmado + filtro global + FK compuestas),
validación de archivos por firma binaria, rate limiting, CORS restringido, cabeceras de seguridad, logs sin datos sensibles,
auditoría, política de retención y checklist de producción.

## 8. Migraciones

`InitialCreate`, aplicada y verificada en MySQL 9.1 (`odontia_dev`). Incluye las semillas de roles, permisos, 52 piezas FDI,
7 superficies y 11 condiciones. `has-pending-model-changes`: sin cambios pendientes.

## 9. Pruebas ejecutadas

| Suite | Motor | Resultado |
|---|---|---|
| Unitarias | — / SQLite (persistencia) | **107/107** |
| Integración | SQLite en memoria | **47/47** (+1 omitida a propósito: la de concurrencia requiere MySQL) |
| Integración | **MySQL 9.1 real** (base temporal por clase, migraciones reales) | **48/48**, incluye 6 reservas simultáneas del mismo horario → exactamente 1 creada |

Escenarios negativos cubiertos, entre otros: la secretaria intenta acceder a historia clínica, odontograma, tratamientos,
antecedentes y documentos clínicos (403); otro consultorio intenta leer o modificar pacientes, turnos, pagos y documentos
(404) o reservar con un paciente o profesional ajeno (400); horario ocupado (409); fuera de disponibilidad, bloqueado o en el
pasado (422); doble turno del mismo paciente (409); usuario sin `appointments.write` modifica un turno (403); pagos con
importe 0, negativo, con 3 decimales, método inválido, fecha futura o que excede el saldo; anulación sin permiso; archivo
falsificado o extensión prohibida; descarga anónima; escalada de privilegios; reuso de refresh token; refresh sin CSRF;
login concurrente.

Errores reales encontrados y corregidos gracias a las pruebas:
- `EnableRetryOnFailure` del proveedor MySQL es incompatible con transacciones explícitas.
- El proveedor de Oracle no lee `DateOnly`/`TimeOnly` (se agregaron conversores).
- Inicios de sesión simultáneos chocaban por el `ConcurrencyStamp` de Identity.
- Un Bearer vencido bloqueaba los endpoints públicos (`/auth/login`, `/auth/refresh`).
- Las cabeceras de seguridad se perdían en las respuestas de error.
- Faltaba el permiso `documents.delete` en los roles.
- Los errores de deserialización exponían nombres de tipos internos.

## 10. Estado de compilación

`dotnet build Odontia.slnx --no-incremental`: **0 errores, 0 advertencias**.

## 11. Integraciones pendientes por falta de credenciales

- **Correo**: SMTP implementado; falta configurar el host, usuario y contraseña del proveedor. En desarrollo los correos se
  guardan como `.eml` en `storage/dev-outbox` sin enviarse.
- **WhatsApp**: sólo abstracción y registro "no enviado"; falta elegir el proveedor e implementar `IWhatsAppSender`.
- **Almacenamiento en la nube**: implementación local privada; para la nube falta implementar `IFileStorage`.
- **Pasarela de pagos y firma digital certificada**: no implementadas (fuera de alcance sin proveedor real).

Ver [EXTERNAL_SERVICES.md](EXTERNAL_SERVICES.md).

## 12. Configuraciones a completar manualmente

Ver la checklist en [SECURITY.md](SECURITY.md#checklist-antes-de-producción): clave JWT, usuario MySQL dedicado con TLS,
carpeta persistente de claves de Data Protection, carpeta privada de documentos con backup, orígenes CORS, SMTP, HTTPS/proxy,
desactivar Swagger y el registro público en producción, y revisión legal.

## 13. Pasos para ejecutar

Ver [README.md](../README.md#ejecución). En resumen: `dotnet tool restore` → configurar User Secrets (conexión y clave JWT) →
`dotnet run --project src/Odontia.Api --launch-profile http` → `http://localhost:5221/swagger`.

## 14. Recomendaciones para React

Ver [FRONTEND_INTEGRATION.md](FRONTEND_INTEGRATION.md): proxy de Vite, access token sólo en memoria, una única renovación
en vuelo, manejo de ProblemDetails por `code`, fechas en UTC con la zona del consultorio, envío de `version` en turnos,
tipos generados desde OpenAPI y menús según `permissions` sin confiar en ellos para la seguridad.
