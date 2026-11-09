# Integración con Angular + TypeScript

Guía para el equipo frontend. El backend no depende de ningún framework: todo se consume por HTTP/JSON bajo `/api/v1`.

| Recurso | Ubicación |
|---|---|
| Contrato OpenAPI 3.0 | [`docs/openapi.v1.json`](openapi.v1.json) · [`docs/openapi.v1.yaml`](openapi.v1.yaml) · en vivo: `/swagger/v1/swagger.json` y `/swagger/v1/swagger.yaml` |
| Swagger UI | `http://localhost:5221/swagger` (Development) |
| Endpoints, `operationId` y permisos | [ENDPOINTS.md](ENDPOINTS.md) |
| Roles y permisos | [ROLES_AND_PERMISSIONS.md](ROLES_AND_PERMISSIONS.md) |
| Postman | `postman/Odontia.postman_collection.json` |

> Los fragmentos de código de esta guía son orientativos (Angular 17+ con `provideHttpClient` y standalone APIs).

---

## 1. URL base y entorno de desarrollo

### Recomendado: proxy de Angular (mismo origen)

Con el proxy, el navegador ve el front y la API en el mismo origen (`http://localhost:4200`). Así:
- la cookie de sesión funciona sin configuración extra;
- la cookie CSRF es legible;
- no intervienen las reglas de CORS.

`proxy.conf.json`:

```json
{
  "/api": { "target": "http://localhost:5221", "secure": false, "changeOrigin": false }
}
```

`angular.json` → `projects.<app>.architect.serve.options.proxyConfig: "proxy.conf.json"` (o `ng serve --proxy-config proxy.conf.json`).

`src/environments/environment.ts`:

```ts
export const environment = { apiBaseUrl: '/api/v1' };   // relativo: pasa por el proxy
```

En producción usar también una ruta relativa `/api/v1` detrás de un *rewrite* del hosting (Vercel `rewrites`, nginx `location /api`).
Así se mantiene el mismo origen y las cookies no son de terceros (los navegadores las bloquean cada vez más).

### Alternativa: llamada directa a otro origen

`apiBaseUrl: 'http://localhost:5221/api/v1'`. La API ya permite `http://localhost:4200` en `Cors:AllowedOrigins`
(`appsettings.Development.json`). En otros entornos se configura con variables de entorno, sin tocar código:

```bash
Cors__AllowedOrigins__0=https://app.odontia.com
```

Requisitos de esta modalidad:
- Las llamadas a `/auth/*` deben llevar `withCredentials: true`.
- **No se podrá leer la cookie CSRF** (pertenece al dominio de la API). El token CSRF debe tomarse del cuerpo de
  `login`/`refresh`, que siempre lo incluye. Tras recargar la página no habrá token en memoria, así que el usuario deberá
  volver a iniciar sesión. Por eso se recomienda el proxy.
- Fuera de desarrollo la API **se niega a iniciar** si `Cors:AllowedOrigins` contiene `*`.

---

## 2. Generar el cliente TypeScript (opcional, recomendado)

El contrato tiene `operationId` únicos en camelCase (`patientsSearch`, `appointmentsCreate`, …) y nombres de schema simples
(`PatientDto`, `PagedResultOfPatientListItemDto`, `ApiProblem`, …). Así se generan servicios y modelos legibles:

```bash
npx ng-openapi-gen --input ../Odontia/docs/openapi.v1.json --output src/app/api
```

o con OpenAPI Generator:

```bash
npx @openapitools/openapi-generator-cli generate -i ../Odontia/docs/openapi.v1.json -g typescript-angular -o src/app/api
```

Cada operación del contrato incluye:
- su permiso (`x-permissions` y en la descripción);
- la seguridad Bearer;
- las respuestas de error (`application/problem+json` con el schema `ApiProblem`).

La descarga de documentos está declarada como binaria (`application/octet-stream`, etc.), por lo que el cliente generado usa `responseType: 'blob'`.

---

## 3. Autenticación y sesión

| Operación | Endpoint | Detalle |
|---|---|---|
| Iniciar sesión | `POST /auth/login` `{ email, password, consultorioId? }` | Devuelve `result.session.accessToken` (vence en 15 min) y `result.csrfToken`. El refresh token llega **sólo** como cookie HttpOnly `odontia_rt` |
| 2FA | `POST /auth/login/2fa` `{ challengeToken, code }` | Si el login devolvió `requiresTwoFactor: true` (desafío válido 5 min) |
| Usuario autenticado | `GET /auth/me` | Usuario, consultorio activo, `permissions` y `memberships` |
| Renovar sesión | `POST /auth/refresh` + header `X-CSRF-Token` | Rota la cookie; devuelve nuevo `accessToken` y `csrfToken` |
| Cambiar consultorio | `POST /auth/switch-consultorio` `{ consultorioId }` + `X-CSRF-Token` | |
| Cerrar sesión | `POST /auth/logout` + `X-CSRF-Token` | Revoca la sesión y borra cookies (204) |
| Recuperar contraseña | `POST /auth/forgot-password` `{ email }` → correo con enlace `/restablecer-contrasena?email=…&token=…` → `POST /auth/reset-password` `{ email, token, newPassword }` | Responde 202 siempre (no revela si el correo existe). El reset revoca todas las sesiones |
| Confirmar correo | enlace `/confirmar-correo?userId=…&token=…` → `POST /auth/confirm-email` `{ userId, token }` | Requerido antes del primer login |
| Aceptar invitación | `/aceptar-invitacion?token=…` → `GET /auth/invitations/{token}` y `POST /auth/invitations/accept` | |
| Sesiones/dispositivos | `GET /auth/sessions`, `DELETE /auth/sessions/{id}`, `POST /auth/sessions/revoke-others` | |

Reglas para el frontend:
- Guardar el **access token sólo en memoria** (un `signal` del `AuthService`). Nunca en `localStorage` ni `sessionStorage`.
- El refresh token no es accesible desde JavaScript (HttpOnly). Lo maneja el navegador.
- El "perfil" (odontólogo/secretaria/administrador) **no se elige en el cliente**: lo determinan los roles del usuario.

Respuesta de login (resumida):

```json
{
  "requiresTwoFactor": false,
  "twoFactorChallengeToken": null,
  "result": {
    "session": {
      "accessToken": "eyJhbGciOi…",
      "accessTokenExpiresAtUtc": "2026-11-09T03:55:18.7605952Z",
      "sessionId": "01a1be64-7b05-7f2e-b8a5-e432b878b2e4",
      "user": { "id": "01a1be62-…", "email": "laura.mendez@odontia.app", "displayName": "Dra. Laura Méndez", "twoFactorEnabled": false },
      "activeConsultorioId": "01a1be62-96ae-7804-bd67-5a03cda7cd83",
      "permissions": ["agenda.read", "appointments.write", "clinical.read", "…"],
      "memberships": [{ "consultorioId": "01a1be62-…", "consultorioName": "Consultorio Méndez", "roles": ["Administrador", "Odontólogo"], "isProfessional": true }]
    },
    "csrfToken": "Qm9…"
  }
}
```

### `AuthService` (orientativo)

```ts
@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private base = environment.apiBaseUrl;
  readonly session = signal<AuthSessionDto | null>(null);
  private csrf: string | null = null;
  private refreshInFlight$: Observable<AuthSessionDto> | null = null;

  get accessToken() { return this.session()?.accessToken ?? null; }
  has = (permission: string) => this.session()?.permissions.includes(permission) ?? false;

  login(email: string, password: string) {
    return this.http.post<LoginResponseDto>(`${this.base}/auth/login`, { email, password }, { withCredentials: true })
      .pipe(tap(r => r.result && this.apply(r.result)));
  }

  /** Una sola renovación en vuelo aunque fallen varias peticiones a la vez. */
  refresh(): Observable<AuthSessionDto> {
    this.refreshInFlight$ ??= this.http.post<SessionResponse>(`${this.base}/auth/refresh`, null, {
        withCredentials: true,
        headers: { 'X-CSRF-Token': this.csrf ?? readCookie('odontia_csrf') ?? '' },
      }).pipe(
        tap(r => this.apply(r)),
        map(r => r.session),
        finalize(() => (this.refreshInFlight$ = null)),
        shareReplay(1),
      );
    return this.refreshInFlight$;
  }

  logout() {
    return this.http.post<void>(`${this.base}/auth/logout`, null, {
      withCredentials: true, headers: { 'X-CSRF-Token': this.csrf ?? readCookie('odontia_csrf') ?? '' },
    }).pipe(finalize(() => { this.session.set(null); this.csrf = null; }));
  }

  private apply(r: SessionResponse) { this.session.set(r.session); this.csrf = r.csrfToken; }
}

const readCookie = (name: string) =>
  document.cookie.split('; ').find(c => c.startsWith(name + '='))?.split('=')[1] ?? null;
```

Restaurar la sesión al recargar la página (con proxy / mismo origen):

```ts
// app.config.ts
provideAppInitializer(() => inject(AuthService).refresh().pipe(catchError(() => of(null)))),
```

### XSRF integrado de Angular

`provideHttpClient()` trae protección XSRF que lee la cookie `XSRF-TOKEN` y envía `X-XSRF-TOKEN`. Odontia usa otros nombres,
así que, si se prefiere el mecanismo nativo (sólo funciona con URLs relativas, o sea con proxy), configurarlo así:

```ts
provideHttpClient(withXsrfConfiguration({ cookieName: 'odontia_csrf', headerName: 'X-CSRF-Token' }), withInterceptors([...]))
```

Los nombres también son configurables en el backend (`Auth:Cookies:CsrfCookieName`, `Auth:Cookies:CsrfHeaderName`) y CORS
acepta automáticamente el encabezado configurado. El token CSRF sólo se exige en `refresh`, `logout` y `switch-consultorio`.
El resto de la API usa Bearer, que no es vulnerable a CSRF.

---

## 4. Solicitudes autenticadas e interceptor

```ts
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const isAuthCall = /\/auth\/(login|refresh|logout|forgot-password|reset-password|confirm-email|invitations)/.test(req.url);
  const withToken = (r: HttpRequest<unknown>) =>
    auth.accessToken && !isAuthCall ? r.clone({ setHeaders: { Authorization: `Bearer ${auth.accessToken}` } }) : r;

  return next(withToken(req)).pipe(
    catchError((err: HttpErrorResponse) => {
      // 401 = token vencido (15 min) o invalidado por cambio de contraseña/2FA (code "session_invalidated").
      if (err.status !== 401 || isAuthCall) return throwError(() => err);
      return auth.refresh().pipe(
        switchMap(() => next(withToken(req))),
        catchError(e => { auth.session.set(null); router.navigate(['/login']); return throwError(() => e); }),
      );
    }),
  );
};
```

Registro: `provideHttpClient(withInterceptors([authInterceptor, errorInterceptor]))`.

### Comportamiento ante sesión vencida

| Situación | Respuesta | Acción del front |
|---|---|---|
| Access token vencido o inválido | `401` `code: "unauthorized"` | Renovar con `/auth/refresh` y reintentar |
| Contraseña o 2FA cambiados en otra pestaña o dispositivo | `401` `code: "session_invalidated"` | Renovar; si falla, ir al login |
| Refresh vencido (7 días sin uso, o 30 días de sesión) | `401` `code: "session_expired"` / `"invalid_refresh_token"` | Ir al login |
| Refresh reutilizado (posible robo) | `401` y **se revoca toda la sesión** | Ir al login |
| Usuario desactivado en el consultorio | `403` `code: "membership_inactive"` | Mostrar aviso; ofrecer otro consultorio de `memberships` o salir |
| Falta el encabezado CSRF | `403` `code: "forbidden"` en `/auth/refresh` | Volver a iniciar sesión |

Renovar proactivamente un poco antes de `accessTokenExpiresAtUtc` evita el primer 401.

---

## 5. Roles y permisos en la UI

- Mostrar u ocultar menús y botones con `auth.has('clinical.read')`, etc. La lista viene en `session.permissions` / `GET /auth/me`.
- Proteger rutas con un guard funcional:

```ts
export const permissionGuard = (permission: string): CanMatchFn => () =>
  inject(AuthService).has(permission) || inject(Router).createUrlTree(['/sin-acceso']);
// { path: 'historia-clinica/:id', canMatch: [permissionGuard('clinical.read')], loadComponent: … }
```

- **La autorización real la aplica siempre el backend** (403 aunque el front muestre la opción). La secretaria no tiene
  `clinical.*`, `odontogram.*`, `treatments.*` ni `patients.medical.read`; no deben cargarse esas pantallas para ese perfil.
- El permiso de **cada** operación está en [ENDPOINTS.md](ENDPOINTS.md) (columna "Permiso") y en `x-permissions` del OpenAPI.
  Resumen de los módulos principales:

| Módulo | Lectura | Escritura |
|---|---|---|
| Pacientes | `patients.read` | `patients.write` |
| Antecedentes médicos | `patients.medical.read` | `patients.medical.write` |
| Alertas (recepción) | `patients.alerts.read` | — |
| Agenda | `agenda.read` (+ `agenda.all_professionals` para ver todos) | `agenda.manage` |
| Turnos | `appointments.read` | `appointments.write` (+ `appointments.overbook` para sobreturno) |
| Tareas | `tasks.read` | `tasks.write` |
| Historia clínica | `clinical.read` | `clinical.write` |
| Odontograma | `odontogram.read` | `odontogram.write` |
| Tratamientos | `treatments.read` | `treatments.write` |
| Presupuestos | `budgets.read` | `budgets.write`, `budgets.approve` |
| Pagos / cuenta corriente | `finance.read` | `payments.write`, `payments.void` |
| Laboratorio | `lab.read` | `lab.write`, `lab.track` |
| Documentos | `documents.read` (+ `documents.clinical.read`) | `documents.write`, `documents.delete` |
| Usuarios / roles | `users.read` | `users.manage`, `roles.manage` |
| Dashboards | `dashboard.reception` / `dashboard.clinical` | — |

---

## 6. Errores

Todas las respuestas de error (de negocio, de validación, de autenticación, de ruteo y de rate limit) tienen el mismo formato
`application/problem+json` (schema `ApiProblem` en el OpenAPI):

```json
{ "type": "https://httpstatuses.io/400", "title": "Solicitud inválida", "status": 400,
  "detail": "La solicitud contiene datos inválidos.", "code": "validation_error",
  "traceId": "0HNPU4UT17ODI:00000001",
  "errors": { "from": ["Fecha inválida. Use ISO 8601, p. ej. 2026-10-20T13:00:00Z."] } }
```

```json
{ "type": "https://httpstatuses.io/401", "title": "No autenticado", "status": 401,
  "detail": "Se requiere un access token válido.", "code": "unauthorized", "traceId": "00-9a5e430b…-00" }
```

```json
{ "type": "https://httpstatuses.io/409", "title": "Conflicto", "status": 409,
  "detail": "El horario ya está ocupado por otro turno.", "code": "slot_taken", "traceId": "00-…" }
```

En Angular el cuerpo llega en `HttpErrorResponse.error`:

```ts
export interface ApiProblem { type: string; title: string; status: number; detail?: string; code: string; traceId: string; errors?: Record<string, string[]>; }

export const errorInterceptor: HttpInterceptorFn = (req, next) => next(req).pipe(
  catchError((err: HttpErrorResponse) => {
    const p = err.error as ApiProblem | null;
    // 400 → p.errors[campo] para mostrar junto al control (las claves son camelCase, igual que el formControlName).
    // 409/422 → mostrar p.detail (redactado para el usuario final). 0 → sin conexión / CORS. 5xx → mensaje genérico + p.traceId.
    return throwError(() => err);
  }),
);
```

Mapeo de errores de validación a un `FormGroup`:

```ts
function applyServerErrors(form: FormGroup, problem: ApiProblem) {
  for (const [field, messages] of Object.entries(problem.errors ?? {}))
    form.get(field)?.setErrors({ server: messages.join(' ') });
}
```

| Status | `code` | Uso |
|---|---|---|
| 400 | `validation_error`, `bad_request` | Errores por campo en `errors` |
| 401 | `unauthorized`, `invalid_credentials`, `email_not_confirmed`, `session_invalidated`, `session_expired`, `invalid_refresh_token` | Ver sección 4 |
| 403 | `forbidden`, `membership_inactive` | Sin permiso |
| 404 | `not_found` | No existe **o pertenece a otro consultorio** |
| 405 / 415 | `method_not_allowed`, `unsupported_media_type` | Error de programación del cliente |
| 409 | `slot_taken`, `patient_double_booking`, `concurrency_conflict`, `duplicate_patient`, `persistence_conflict` | Recargar y reintentar / elegir otro horario |
| 422 | `outside_availability`, `appointment_in_past`, `invalid_status_transition`, `tooth_absent`, `invalid_surface`, `budget_not_approved`, `amount_exceeds_outstanding`, … | Mostrar `detail` |
| 429 | `rate_limited` | Esperar y reintentar |
| 500 | `internal_error` | Mensaje genérico; informar `traceId` |

---

## 7. Paginación, filtros y ordenamiento

Parámetros comunes: `page` (desde 1), `pageSize` (1–100, por defecto 20), `sort` (campo permitido; prefijo `-` = descendente).

```ts
searchPatients(search: string, page = 1) {
  const params = new HttpParams().set('search', search).set('page', page).set('pageSize', 20).set('sort', 'name');
  return this.http.get<PagedResult<PatientListItemDto>>(`${this.base}/patients`, { params });
}
```

```json
{ "items": [ { "id": "01a1be64-7e07-7b7b-ac23-3cff024caa30", "firstName": "Elena", "lastName": "Ruiz", "documentNumber": "27660198",
    "age": 52, "phone": "+54 9 11 3120-8876", "healthInsurance": null, "insurancePlan": null, "status": "New",
    "lastVisitUtc": null, "nextAppointmentUtc": null } ],
  "page": 1, "pageSize": 10, "totalCount": 1, "totalPages": 1, "hasNext": false }
```

```ts
export interface PagedResult<T> { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number; hasNext: boolean; }
```

Encaja directo con `MatPaginator`: `length = totalCount` y `pageIndex = page - 1`.

---

## 8. Fechas, enums y otros formatos

- **Fechas/horas**: la API responde en UTC (`"2026-10-20T13:30:00Z"`). Para mostrarlas en la zona del consultorio
  (`GET /consultorio` → `timeZoneId`) usar `DatePipe` con la zona, o `Intl.DateTimeFormat`.
- **Enviar fechas** con `date.toISOString()` (formato `…Z`). También se aceptan offsets (`-03:00`). La API tolera el `+`
  que `HttpParams` no codifica en query string, pero `toISOString()` evita el problema por completo.
- **Fechas de calendario**: `"2026-10-20"`. **Horas**: `"09:00"`. Angular no convierte strings a `Date`
  automáticamente: hacerlo en el servicio si se necesita.
- **Enums como string**: `"Pending"`, `"Confirmed"`, `"InWaitingRoom"`, `"Female"`… (también como uniones de string en el
  cliente generado).
- **Concurrencia**: los turnos traen `version`. Enviarla en `PUT /appointments/{id}` y `POST /appointments/{id}/reschedule`.
  Un `409 concurrency_conflict` indica que otro usuario lo modificó.
- **Altas** responden `201` con el recurso creado en el cuerpo (y `Location`, expuesto por CORS).
- **Importes**: `number` con hasta 2 decimales. **Totales y saldos se leen siempre del backend.**

---

## 9. Ejemplos de solicitudes

Crear un turno:

```http
POST /api/v1/appointments
Authorization: Bearer eyJhbGciOi…
Content-Type: application/json

{ "patientId": "01a1be64-7e07-7b7b-ac23-3cff024caa30", "professionalUserId": "01a1be62-95ab-74af-b70c-3c4c1a911581",
  "gabineteId": null, "start": "2026-10-20T13:30:00Z", "durationMinutes": 45, "reason": "Extracción pieza 48",
  "administrativeNotes": "Traer radiografía panorámica", "isOverbooking": false, "confirmed": false }
```

`201 Created` → `AppointmentDto` (con `status`, `version`, `alerts`, …). `409 slot_taken` si el horario se ocupó mientras el
usuario completaba el formulario: la disponibilidad se revalida de forma atómica al confirmar.

Consultar disponibilidad para el asistente "Nuevo turno":

```http
GET /api/v1/agenda/availability?date=2026-10-20&professionalUserId=01a1be62-…&durationMinutes=45
```

```json
{ "date": "2026-10-20", "professionalUserId": "01a1be62-…", "durationMinutes": 45,
  "slots": [ { "startUtc": "2026-10-20T11:30:00Z", "endUtc": "2026-10-20T12:15:00Z", "localTime": "08:30" } ], "blocks": [] }
```

Subir un documento y descargarlo:

```ts
upload(patientId: string, file: File, category: DocumentCategory) {
  const fd = new FormData();
  fd.append('file', file);          // pdf, jpg, png, dcm · máx. 15 MB · se valida el contenido real
  fd.append('category', category);  // 'Radiograph' | 'Study' | 'ConsentForm' | 'Administrative' | …
  return this.http.post<DocumentDto>(`${this.base}/patients/${patientId}/documents`, fd); // sin Content-Type manual
}

download(id: string) {
  return this.http.get(`${this.base}/documents/${id}/download`, { responseType: 'blob', observe: 'response' });
  // nombre de archivo: response.headers.get('Content-Disposition'); abrir con URL.createObjectURL(response.body!)
}
```

Más ejemplos (pacientes, odontograma, presupuestos, pagos) en [FRONTEND_INTEGRATION.md](FRONTEND_INTEGRATION.md#5-pantallas-del-diseño--endpoints)
(el mapeo de pantallas a endpoints es el mismo, independientemente del framework) y en la colección de Postman.

---

## 10. Checklist para el equipo Angular

- [ ] `proxy.conf.json` en desarrollo y *rewrite* `/api` en producción (mismo origen).
- [ ] `AuthService` con el access token sólo en memoria y una única renovación en vuelo.
- [ ] Interceptor que agrega el Bearer y renueva ante un 401.
- [ ] `withCredentials: true` en las llamadas a `/auth/*` si se usa otro origen.
- [ ] Interceptor de errores basado en `code` y `errors`.
- [ ] Guards y visibilidad de menús por `permissions`.
- [ ] Cliente generado desde `docs/openapi.v1.json` y regenerado ante cambios de contrato.
- [ ] Fechas enviadas con `toISOString()` y mostradas en la zona del consultorio.
- [ ] Enviar `version` al editar o reprogramar turnos.
