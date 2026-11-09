# Integración con el frontend React + TypeScript

La API es independiente del frontend. Todo se consume por HTTP/JSON bajo `/api/v1`. El contrato completo está en
[openapi.v1.json](openapi.v1.json) (se pueden generar tipos TS con `npx openapi-typescript docs/openapi.v1.json -o src/api/schema.d.ts`)
y la tabla de endpoints y permisos en [ENDPOINTS.md](ENDPOINTS.md).

## 1. Configuración local recomendada

Usar el **proxy de Vite** para que el navegador vea front y API en el mismo origen (cookies simples, sin CORS en desarrollo):

```ts
// vite.config.ts
export default defineConfig({
  server: { proxy: { '/api': { target: 'http://localhost:5221', changeOrigin: false } } },
});
```

Sin proxy, la API ya acepta `http://localhost:5173` y `http://localhost:3000` (`Cors:AllowedOrigins` en
`appsettings.Development.json`). En ese caso usar `credentials: 'include'` en las llamadas a `/auth/*`.

## 2. Autenticación

| Paso | Endpoint | Notas |
|---|---|---|
| Login | `POST /auth/login` `{ email, password, consultorioId? }` | Devuelve `result.session.accessToken` (15 min) y `result.csrfToken`. El refresh token llega como cookie **HttpOnly** (`odontia_rt`) |
| 2FA (si `requiresTwoFactor`) | `POST /auth/login/2fa` `{ challengeToken, code }` o `{ challengeToken, recoveryCode }` | El desafío vence a los 5 min |
| Requests | `Authorization: Bearer <accessToken>` | Guardar el access token **sólo en memoria** (estado de React), nunca en `localStorage` |
| Renovar | `POST /auth/refresh` + encabezado `X-CSRF-Token: <csrfToken>` | Rota la cookie y devuelve un nuevo `accessToken` y `csrfToken` |
| Recargar página | `POST /auth/refresh` con `X-CSRF-Token` = valor de la cookie `odontia_csrf` | La cookie CSRF es legible por JS (no es secreta por sí sola); con proxy/mismo sitio se lee de `document.cookie` |
| Cambiar consultorio | `POST /auth/switch-consultorio` `{ consultorioId }` + `X-CSRF-Token` | Las membresías vienen en `session.memberships` |
| Logout | `POST /auth/logout` + `X-CSRF-Token` | Revoca la sesión y borra las cookies |
| Perfil y permisos | `GET /auth/me` | `permissions` del consultorio activo, para mostrar/ocultar opciones |

Ejemplo de respuesta de login:

```json
{
  "requiresTwoFactor": false,
  "twoFactorChallengeToken": null,
  "result": {
    "session": {
      "accessToken": "eyJhbGciOi…",
      "accessTokenExpiresAtUtc": "2026-11-09T03:55:18.7605952Z",
      "sessionId": "01a1be64-7b05-7f2e-b8a5-e432b878b2e4",
      "user": {
        "id": "01a1be62-95ab-74af-b70c-3c4c1a911581", "email": "laura.mendez@odontia.app",
        "firstName": "Laura", "lastName": "Méndez", "displayName": "Dra. Laura Méndez", "title": "Dra.",
        "specialty": "Endodoncia", "licenseNumber": "M.P. 4521", "twoFactorEnabled": false
      },
      "activeConsultorioId": "01a1be62-96ae-7804-bd67-5a03cda7cd83",
      "permissions": ["agenda.all_professionals", "agenda.manage", "agenda.read", "appointments.read", "…"],
      "memberships": [
        { "consultorioId": "01a1be62-96ae-7804-bd67-5a03cda7cd83", "consultorioName": "Consultorio Méndez",
          "roles": ["Administrador", "Odontólogo"], "isProfessional": true }
      ]
    },
    "csrfToken": "Qm9…"
  }
}
```

### Cliente HTTP con renovación automática (una sola renovación en vuelo)

```ts
let accessToken: string | null = null;
let csrfToken: string | null = null;
let refreshing: Promise<boolean> | null = null;

const readCookie = (n: string) => document.cookie.split('; ').find(c => c.startsWith(n + '='))?.split('=')[1] ?? null;

async function refresh(): Promise<boolean> {
  refreshing ??= (async () => {
    const r = await fetch('/api/v1/auth/refresh', {
      method: 'POST', credentials: 'include',
      headers: { 'X-CSRF-Token': csrfToken ?? readCookie('odontia_csrf') ?? '' },
    });
    if (!r.ok) { accessToken = null; return false; }
    const body = await r.json();
    accessToken = body.session.accessToken;
    csrfToken = body.csrfToken;
    return true;
  })().finally(() => { refreshing = null; });
  return refreshing;
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const doFetch = () => fetch(`/api/v1${path}`, {
    ...init,
    credentials: 'include',
    headers: {
      ...(init.body && !(init.body instanceof FormData) ? { 'Content-Type': 'application/json' } : {}),
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      ...init.headers,
    },
  });
  let r = await doFetch();
  if (r.status === 401 && (await refresh())) r = await doFetch();   // vencido o "session_invalidated"
  if (!r.ok) throw await r.json();                                    // ProblemDetails
  return r.status === 204 ? (undefined as T) : r.json();
}
```

Renovar proactivamente unos segundos antes de `accessTokenExpiresAtUtc` evita el primer 401.

## 3. Formato de errores (ProblemDetails)

```json
{
  "type": "https://httpstatuses.io/400", "title": "Solicitud inválida", "status": 400,
  "detail": "La solicitud contiene datos inválidos.", "code": "validation_error",
  "traceId": "00-848cfd3f4c007144bc7d06d72f253ce3-8c370a47d86421b3-00",
  "errors": { "firstName": ["El nombre es obligatorio."], "documentNumber": ["El DNI es obligatorio."] }
}
```

| Status | `code` habituales | Qué hacer en la UI |
|---|---|---|
| 400 | `validation_error` | Mostrar `errors[campo]` junto a cada input (claves en camelCase) |
| 401 | `invalid_credentials`, `email_not_confirmed`, `session_invalidated`, `invalid_refresh_token`, `session_expired` | Renovar sesión o volver al login |
| 403 | `forbidden`, `membership_inactive` | Ocultar la acción / avisar falta de permiso |
| 404 | `not_found` | El recurso no existe **o es de otro consultorio** |
| 409 | `slot_taken`, `patient_double_booking`, `concurrency_conflict`, `duplicate_patient`, `persistence_conflict` | Refrescar datos y reintentar / elegir otro horario |
| 422 | `outside_availability`, `appointment_in_past`, `invalid_status_transition`, `tooth_absent`, `invalid_surface`, `budget_not_approved`, `amount_exceeds_outstanding`, … | Mostrar `detail` (mensaje en español pensado para el usuario) |
| 429 | `rate_limited` | Esperar y reintentar |

Ejemplo 409 al crear un paciente con DNI existente:

```json
{ "type": "https://httpstatuses.io/409", "title": "Conflicto", "status": 409,
  "detail": "Ya existe un paciente con ese DNI en el consultorio.", "code": "duplicate_patient", "traceId": "00-…" }
```

## 4. Convenciones

- **JSON camelCase**, enums como **string** (`"Confirmed"`, `"InWaitingRoom"`, `"Female"`).
- **Fechas/horas**: enviar `DateTimeOffset` ISO 8601 con offset (`"2026-10-20T10:30:00-03:00"`). La API responde en UTC
  (`"2026-10-20T13:30:00Z"`); convertir con la zona del consultorio (`GET /consultorio` → `timeZoneId`).
  Fechas de calendario: `"2026-10-20"`. Horas: `"09:00"`.
- **Paginación**: `?page=1&pageSize=20&sort=-start` → `{ items, page, pageSize, totalCount, totalPages, hasNext }` (máx. 100 por página).
  `sort` acepta los campos indicados en cada endpoint; el prefijo `-` ordena descendente.
- **Concurrencia**: los turnos devuelven `version`. Enviarla en `PUT /appointments/{id}` y `/reschedule`; si llega 409
  `concurrency_conflict`, recargar el turno.
- **Importes**: números con hasta 2 decimales. Los saldos y totales **siempre** se leen del backend.

Ejemplo de lista paginada (`GET /patients?search=ruiz`):

```json
{ "items": [ { "id": "01a1be64-7e07-7b7b-ac23-3cff024caa30", "firstName": "Elena", "lastName": "Ruiz",
    "documentNumber": "27660198", "age": 52, "phone": "+54 9 11 3120-8876", "healthInsurance": "Swiss Medical",
    "insurancePlan": "SMG 30", "status": "New", "lastVisitUtc": null, "nextAppointmentUtc": "2026-10-20T12:00:00Z" } ],
  "page": 1, "pageSize": 10, "totalCount": 1, "totalPages": 1, "hasNext": false }
```

## 5. Pantallas del diseño → endpoints

| Pantalla | Endpoints |
|---|---|
| Login (perfil Odontólogo/Secretaria) | `POST /auth/login`. El "perfil" no se elige en el cliente: lo definen los roles del usuario (`memberships[].roles`, `permissions`) |
| ¿Olvidaste tu contraseña? | `POST /auth/forgot-password`, `POST /auth/reset-password` (`email` y `token` vienen en el enlace `/restablecer-contrasena?email=…&token=…`) |
| Inicio secretaria | `GET /dashboard/reception` (turnos del día, confirmaciones, sala de espera, tareas, laboratorio); "Enviar recordatorio por WhatsApp" → `POST /messages/reminders` |
| Inicio odontólogo | `GET /dashboard/clinical` (pacientes del día, próximo turno, paciente en sala con alertas, tratamientos, laboratorio, actividad semanal) |
| Agenda día / semana | `GET /agenda?from=2026-10-19&to=2026-10-24&professionalUserId=…`, `GET /agenda/day-summary?date=…`, `GET /consultorio/professionals` |
| Agenda mes | `GET /agenda/month?year=2026&month=10` |
| Nuevo turno (asistente de 4 pasos) | `GET /patients?search=…` → `GET /agenda/availability?date=…&professionalUserId=…&durationMinutes=45` → `POST /appointments` (`isOverbooking` = "Marcar como urgencia (sobreturno)") |
| Turno seleccionado | `GET /appointments/{id}` (incluye `alerts`), `POST /appointments/{id}/reschedule`, `/attend`, `/cancel`, `/check-in`, `/waiting-room`, `/start` |
| Turnos y tareas | `GET /appointments?from=…&to=…&status=Pending`, `GET /appointments/status-counts`, `GET /tasks?view=today`, `POST /tasks`, `PUT /tasks/{id}/status` |
| Pacientes / Nuevo paciente | `GET /patients`, `GET /health-insurances`, `POST /patients` (con `medicalHistory` sólo si el usuario tiene `patients.medical.write`), `POST /patients/{id}/consents` |
| Ficha del paciente · Resumen | `GET /patients/{id}/summary`, `GET /patients/{id}/treatment-plans`, `GET /patients/{id}/account` |
| Odontograma | `GET /odontogram/catalog` (una vez), `GET /patients/{id}/odontogram`, `POST /patients/{id}/odontogram/findings` ("Aplicar estado"), `POST …/teeth/{pieza}/notes`, `GET …/odontogram/history?toothCode=36` |
| Historia clínica | `GET /patients/{id}/clinical-record`, `GET …/clinical-record/entries?type=Evolution`, `POST …/entries`, adjuntos con `POST /patients/{id}/documents` (`clinicalEntryId`) |
| Laboratorio | `GET /lab-works/board`, `GET /lab-works?view=overdue`, `POST /lab-works`, `POST /lab-works/{id}/status` |
| Perfil y configuración | `GET/PUT /profile`, `PUT /profile/professional`, `POST /auth/change-password`, `GET /auth/2fa/setup`, `POST /auth/2fa/enable`, `GET /users`, `POST /invitations`, `PUT /consultorio`, `PUT /consultorio/settings` |

## 6. Archivos

Carga (multipart):

```ts
const fd = new FormData();
fd.append('file', file);                 // pdf, jpg, png, dcm (máx. 15 MB)
fd.append('category', 'Radiograph');     // Radiograph | Study | Prescription | ClinicalPhoto | ConsentForm | Administrative | …
fd.append('clinicalEntryId', entryId);   // opcional
await api(`/patients/${patientId}/documents`, { method: 'POST', body: fd });
```

Descarga: la URL requiere el Bearer, por eso no puede usarse directamente en `<a href>`. Pedirla con `fetch` y abrir el blob:

```ts
const r = await fetch(`/api/v1/documents/${id}/download`, { headers: { Authorization: `Bearer ${accessToken}` } });
const url = URL.createObjectURL(await r.blob());
window.open(url); // revocar luego con URL.revokeObjectURL(url)
```

## 7. Recomendaciones

- Generar los tipos desde OpenAPI en CI para detectar cambios de contrato.
- Usar TanStack Query (o similar) e invalidar las consultas de agenda tras crear/reprogramar/cancelar turnos.
- Mostrar u ocultar menús según `permissions`, pero manejar igualmente 403 (la autorización real es del servidor).
- No cachear respuestas clínicas en almacenamiento persistente del navegador (la API envía `Cache-Control: no-store`).
- Mostrar `detail` de los 409/422 tal cual: están redactados para el usuario final.
- Para la secretaria, no intentar cargar historia clínica/odontograma: no tiene permiso y la API responde 403.
