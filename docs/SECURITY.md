# Seguridad y protección de datos

Odontia maneja datos personales, administrativos y clínicos. Este documento describe las medidas implementadas y lo que
debe completarse antes de producción. **No constituye una certificación ni garantiza por sí solo el cumplimiento de
normativas** (p. ej. Ley 25.326 de Protección de Datos Personales y Ley 26.529 de Derechos del Paciente en Argentina). La
adecuación legal debe revisarla un profesional especializado.

## Autenticación

| Medida | Implementación |
|---|---|
| Contraseñas | ASP.NET Core Identity (PBKDF2 con sal). Mínimo 10 caracteres, mayúscula, minúscula, dígito, 4 caracteres distintos |
| Bloqueo | 5 intentos fallidos → 15 minutos de bloqueo (también cuentan los códigos 2FA erróneos) |
| Anti-enumeración | Login con mensaje único para usuario inexistente, contraseña incorrecta, bloqueo o usuario inactivo (y costo de hash igualado). `forgot-password`, `resend-confirmation` y `register` responden igual exista o no la cuenta |
| Access token | JWT HS256, **15 minutos**, emisor/audiencia/firma/vencimiento validados, clock skew de 30 s. Claims: `sub`, `sid` (sesión), `cid` (consultorio), `sst` (huella del security stamp). **No contiene permisos ni datos personales** |
| Invalidación inmediata | Cambio/restablecimiento de contraseña, cambio de 2FA o de correo → cambia el security stamp → todo access token previo responde `401 session_invalidated` |
| Refresh token | Opaco, 48 bytes aleatorios; en base **sólo el hash SHA-256**. Rotación en cada uso, vigencia deslizante de 7 días y máximo absoluto de 30 días por sesión. **Detección de reuso**: presentar un token ya rotado revoca toda la sesión |
| Cookies | Refresh en cookie `HttpOnly`, `Secure`, `SameSite` (Strict por defecto), `Path=/api/v1/auth`. Nunca en `localStorage` ni en el cuerpo de la respuesta |
| CSRF | Doble envío: los endpoints que usan la cookie (`refresh`, `logout`, `switch-consultorio`) exigen el encabezado `X-CSRF-Token` igual a la cookie `odontia_csrf` (comparación en tiempo constante). El resto de la API usa `Authorization: Bearer`, que no es vulnerable a CSRF |
| Sesiones | Listado y revocación por dispositivo; "cerrar las demás sesiones" |
| 2FA | TOTP (apps autenticadoras) con 10 códigos de recuperación; el login devuelve un desafío de 5 minutos |
| Correo | Confirmación obligatoria (`Auth:RequireConfirmedEmail`); tokens de Identity con vigencia de 2 h |
| Invitaciones | Token aleatorio, sólo hash en base, 7 días, un solo uso, revocables |

## Autorización y aislamiento

- Todo endpoint requiere autenticación salvo `[AllowAnonymous]` explícito (filtro global). Cada endpoint declara su permiso
  y el servicio lo vuelve a verificar.
- El consultorio activo se determina en el servidor (claim firmado + membresía activa revalidada en cada solicitud).
- Filtro global de EF Core por `ConsultorioId`, imposición del consultorio activo en escrituras y **FK compuestas** que
  impiden referencias cruzadas a nivel de base (ver [ARCHITECTURE.md](ARCHITECTURE.md#multi-consultorio-aislamiento)).
- Recursos de otro consultorio responden **404** (no revelan su existencia).
- Reglas anti-escalada de privilegios: [ROLES_AND_PERMISSIONS.md](ROLES_AND_PERMISSIONS.md).

## Protección de entradas y salidas

- **Validación** con FluentValidation en todos los DTOs de entrada + invariantes en el dominio.
- **Asignación masiva**: los controllers reciben DTOs específicos; nunca se enlazan entidades. Campos como `ConsultorioId`,
  autor, fechas de auditoría, estado o totales nunca se toman del cliente.
- **SQL injection**: sólo consultas LINQ parametrizadas de EF Core (no hay SQL crudo con entrada del usuario).
- **Respuestas**: siempre DTOs; nunca entidades, hashes, tokens ni security stamps. Los datos clínicos sólo se incluyen si
  el usuario tiene el permiso (p. ej. el resumen del paciente omite tratamiento y evolución a la recepción).
- **Errores**: ProblemDetails con `code` y `traceId`; sin stack traces ni mensajes internos (incluso los errores del
  deserializador JSON se reemplazan por mensajes genéricos).
- **Cabeceras**: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`,
  `Permissions-Policy`, `Content-Security-Policy: default-src 'none'` y `Cache-Control: no-store` en `/api`. HSTS y redirección
  HTTPS fuera de desarrollo.
- **CORS**: lista blanca configurable (`Cors:AllowedOrigins`), con credenciales; la API **no arranca** si se configura `*`
  fuera de desarrollo.
- **Rate limiting**: 10/min por IP en login, registro, recuperación, 2FA e invitaciones; 30/min en refresh; 600/min global
  por usuario/IP. Respuesta `429` en ProblemDetails.

## Documentos

- Extensiones permitidas: `.pdf`, `.jpg/.jpeg`, `.png`, `.dcm`. **El tipo real se valida por firma binaria** (magic numbers);
  el `Content-Type` del cliente se ignora.
- Tamaño máximo configurable (15 MB por defecto), controlado al leer el flujo (no se confía en `Content-Length`).
- Nombre saneado (sin rutas, caracteres de control ni reservados). El archivo se guarda con un nombre aleatorio fuera
  del directorio web. La clave de almacenamiento se valida contra *path traversal*.
- Hash SHA-256 guardado. Descarga sólo vía endpoint autorizado y auditada. Los documentos clínicos exigen `documents.clinical.read`.
  No existen URLs públicas.

## Logs y auditoría

- Logs sin contraseñas, tokens, cuerpos de solicitud ni contenido clínico. Correos y teléfonos se enmascaran (`m***@mail.com`, `***8810`).
- `AuditLogs`: altas, modificaciones y bajas de toda entidad de consultorio (sólo **nombres** de campos modificados, nunca
  valores), inicios de sesión (exitosos y fallidos), cierres y revocaciones de sesión, cambios de contraseña y 2FA, cambios de
  roles, activaciones, invitaciones, accesos a historia clínica, correcciones y anulaciones clínicas, descargas de documentos,
  accesos denegados a documentos clínicos y anulaciones de pagos.
- Historiales funcionales inmutables: turnos, odontograma, tratamientos, presupuestos, laboratorio y revisiones clínicas.
- El registro de auditoría se escribe en una unidad de trabajo separada, de modo que persiste aunque la operación falle.

## Integridad clínica y financiera

- Las evoluciones clínicas no se sobrescriben: corregir exige un motivo y guarda la versión anterior. Sólo corrige o anula
  el autor. No se eliminan: se anulan.
- Los pagos no se eliminan: se anulan con motivo y se registra un contraasiento. Los saldos se calculan siempre en el servidor.
- Los presupuestos conservan el valor histórico de cada línea.
- Los consentimientos distinguen la **aceptación registrada en el sistema** (constancia interna) del **documento firmado en
  papel adjunto**. **Odontia no emite firmas digitales certificadas.**

## Política de retención de datos

Propuesta técnica (a validar con asesoría legal del consultorio):

| Dato | Criterio |
|---|---|
| Historia clínica, odontograma, diagnósticos, tratamientos, consentimientos y documentos clínicos | Conservar como mínimo **10 años desde la última atención** (plazo usual de la Ley 26.529, art. 18). Sin borrado físico desde la API; la baja de documentos es lógica |
| Datos administrativos y financieros (pagos, presupuestos, movimientos) | Conservar según normativa fiscal/contable (típicamente 10 años) |
| Refresh tokens | Pueden purgarse una vez vencidos o revocados (se recomienda una tarea mensual sobre `RefreshTokens` con más de 60 días vencidos) |
| `OutboundMessages` | Sin secretos (los mensajes con enlaces de seguridad no guardan el cuerpo). Purgar a los 12 meses |
| `AuditLogs` | Conservar 5 años como mínimo (o lo que exija la política del consultorio) |
| Notificaciones internas | Purgar las leídas a los 6 meses |
| Usuarios dados de baja | Se desactivan (no se eliminan) para preservar la autoría de los registros clínicos |

El ejercicio de derechos del titular (acceso, rectificación, supresión) sobre datos clínicos debe evaluarse caso por caso:
la supresión puede estar limitada por la obligación legal de conservar la historia clínica.

## Checklist antes de producción

- [ ] `Jwt__SigningKey` aleatoria (≥ 32 bytes) en un gestor de secretos; rotación planificada.
- [ ] Usuario de MySQL dedicado (sin `root`) con conexión TLS (`SslMode=Required` en la cadena de conexión).
- [ ] `DataProtection__KeysPath` en un volumen persistente y protegido (sin esto, los enlaces de reset/confirmación se invalidan al reiniciar).
- [ ] `Storage__Local__RootPath` en un volumen privado con backup cifrado, o implementar `IFileStorage` sobre S3/Azure Blob privado.
- [ ] `Cors__AllowedOrigins` con el dominio real del frontend; cookies `Secure=true` y `SameSite` según el despliegue.
- [ ] HTTPS terminado en el proxy con HSTS; `ReverseProxy__Enabled=true` y `KnownProxies` configurados.
- [ ] `Auth__AllowPublicRegistration=false` salvo que el alta pública de consultorios sea deseada.
- [ ] Proveedor de correo real (`Email__Provider=Smtp`); SPF/DKIM/DMARC en el dominio remitente.
- [ ] `Swagger__Enabled=false` (o protegido) en producción.
- [ ] Aplicar migraciones con el script idempotente revisado; backups automáticos de MySQL con prueba de restauración.
- [ ] Centralizar logs (sin datos sensibles) y alertas sobre picos de 401/403/429 y errores 500.
- [ ] Revisión legal de términos, consentimientos y política de retención.
- [ ] Prueba de penetración externa antes de la salida a producción.
