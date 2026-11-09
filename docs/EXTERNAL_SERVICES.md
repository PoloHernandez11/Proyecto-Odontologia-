# Servicios externos

Ninguna integración externa tiene credenciales configuradas en este repositorio. Todas funcionan por abstracción y,
**sin proveedor, el sistema nunca informa un mensaje como enviado**: queda registrado en `OutboundMessages` con estado
`NotSentNoProvider` y se ve en `GET /api/v1/messages`.

| Servicio | Abstracción | Estado actual | Para activarlo |
|---|---|---|---|
| Correo electrónico | `IEmailSender` | **SMTP real implementado** (`SmtpEmailSender`). Desarrollo: `DevelopmentFile` (guarda `.eml` en `storage/dev-outbox`, sin enviar). Por defecto: `None` | `Email__Provider=Smtp` + `Email__Smtp__Host/Port/EnableSsl/UserName/Password` y `Email__FromAddress` |
| WhatsApp | `IWhatsAppSender` | Sin proveedor: `NoWhatsAppSender` registra el intento y devuelve "no enviado" | Implementar `IWhatsAppSender` con el proveedor elegido (ver abajo) |
| Almacenamiento de archivos | `IFileStorage` | `LocalFileStorage` privado (fuera del directorio web) | Para la nube, implementar `IFileStorage` (S3/Azure Blob/GCS con bucket privado) |
| Pasarela de pagos | — | **No implementada** (no hay configuración real disponible). Los pagos se registran manualmente | Fuera de alcance; requiere definir proveedor y conciliación |
| Firma digital certificada | — | **No implementada**. Se registra aceptación en el sistema o el documento firmado en papel | Requiere un prestador de firma digital certificado |

## Correo (SMTP)

`SmtpEmailSender` usa `System.Net.Mail` con TLS. Sólo marca `Sent` cuando el servidor SMTP acepta el mensaje; ante un error
queda `Failed` con el código SMTP. Flujos que envían correo: confirmación de cuenta, recuperación de contraseña, cambio de
correo, invitaciones, aviso de intento de registro duplicado y recordatorios de turno por email.

Los mensajes que contienen enlaces de seguridad (reset, confirmación, invitación) **no guardan su cuerpo** en la base.

Si se prefiere un proveedor con API HTTP (SendGrid, Amazon SES, Mailgun), basta con implementar `IEmailSender`, devolver
`SendResult(ProviderConfigured: true, Accepted: <confirmación del proveedor>, ...)` y registrarlo en
`Odontia.Infrastructure/DependencyInjection.cs`.

## WhatsApp

El recordatorio se arma en `NotificationService` y se envía al teléfono normalizado del paciente en formato E.164
(`+5491155238810`). Para integrar, por ejemplo, WhatsApp Business Cloud API (Meta) o Twilio:

1. Crear `XxxWhatsAppSender : IWhatsAppSender` en `Odontia.Infrastructure/Messaging`, con `HttpClient` tipado y opciones
   (`WhatsApp:Xxx:Token`, `PhoneNumberId`, nombre de **plantilla aprobada**; los mensajes salientes fuera de la ventana de
   24 h requieren plantillas).
2. Devolver `Accepted=true` sólo si la API responde éxito, y guardar el id del mensaje del proveedor.
3. Registrar el sender según `WhatsApp:Provider` en `DependencyInjection.cs`.
4. (Opcional) Exponer un webhook para actualizar el estado a entregado/leído.

## Trabajos en segundo plano

`BackgroundJobs:Enabled=true` activa:

- **Recordatorios** (cada 15 min): turnos pendientes/confirmados dentro de `ReminderHoursBefore` horas (configurable por
  consultorio; 0 desactiva), por WhatsApp si hay teléfono, si no por correo. Un turno se recuerda una sola vez y la
  reprogramación reinicia el recordatorio.
- **Mantenimiento** (cada 60 min): vencimiento de presupuestos emitidos, avisos de tareas por vencer o vencidas y avisos
  (una vez al día) de trabajos de laboratorio atrasados.

Cada consultorio se procesa en su propio scope con su contexto, así que el aislamiento es el mismo que en HTTP. En un
despliegue con varias instancias conviene habilitar los jobs en una sola (o mover la planificación a un worker dedicado).
