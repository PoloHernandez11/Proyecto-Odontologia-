import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, defer, finalize, map, of, shareReplay, switchMap, tap, throwError, timer } from 'rxjs';
import { AuthSessionDto, InvitationInfoDto, LoginResponseDto, MessageResponse, RegisterRequest, SessionResponse } from '../api/schema';
import { API, codigoDeError } from './api';
import { definirZonaDelConsultorio, zonaDelNavegador, zonaValida } from './zona-horaria';

/** Cookie legible (no HttpOnly) donde el backend deja el token anti-CSRF para renovar la sesión. */
export const COOKIE_CSRF = 'odontia_csrf';

function leerCookie(nombre: string): string | null {
  if (typeof document === 'undefined') return null;
  return document.cookie.split('; ').find(cookie => cookie.startsWith(nombre + '='))?.split('=')[1] ?? null;
}

/**
 * 401 `refresh_conflict`: otra pestaña (o solicitud) renovó la sesión en el mismo momento.
 * No es un cierre de sesión: se espera un instante y se renueva otra vez con la cookie que dejó la otra renovación.
 */
export const CONFLICTO_DE_RENOVACION = 'refresh_conflict';
export const ESPERA_ANTE_CONFLICTO_MS = 300;

/** La versión de demostración guardaba pacientes, turnos y pagos ficticios en localStorage: ya no se usan. */
function borrarDatosDeLaVersionDeDemostracion(): void {
  try {
    if (typeof localStorage === 'undefined') return;
    for (const clave of Object.keys(localStorage)) if (/^odontia[-_]/.test(clave)) localStorage.removeItem(clave);
  } catch { /* El navegador puede bloquear el almacenamiento: no afecta al funcionamiento. */ }
}

/**
 * Sesión del usuario. El access token vive sólo en memoria; el refresh token es una cookie HttpOnly
 * que maneja el navegador. El perfil no se elige en el cliente: lo determinan los roles y permisos del usuario.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private csrf: string | null = null;
  private renovacionEnCurso: Observable<AuthSessionDto> | null = null;

  readonly sesion = signal<AuthSessionDto | null>(null);
  readonly autenticado = computed(() => this.sesion() !== null);
  readonly nombre = computed(() => this.sesion()?.user.displayName ?? '');
  readonly membresia = computed(() => {
    const sesion = this.sesion();
    return sesion?.memberships.find(item => item.consultorioId === sesion.activeConsultorioId) ?? sesion?.memberships[0] ?? null;
  });
  readonly roles = computed(() => this.membresia()?.roles ?? []);
  readonly esProfesional = computed(() => this.membresia()?.isProfessional ?? false);
  readonly consultorio = computed(() => this.membresia()?.consultorioName ?? '');
  /** Zona horaria (IANA) del consultorio activo: con ella se muestran y se cargan todas las fechas y horas. */
  readonly zona = computed(() => zonaValida(this.membresia()?.timeZoneId) ?? zonaDelNavegador());

  constructor() {
    definirZonaDelConsultorio(() => zonaValida(this.membresia()?.timeZoneId));
    inject(DestroyRef).onDestroy(() => definirZonaDelConsultorio(() => null));
  }

  get accessToken(): string | null { return this.sesion()?.accessToken ?? null; }
  get userId(): string | null { return this.sesion()?.user.id ?? null; }

  tiene(permiso: string): boolean { return this.sesion()?.permissions.includes(permiso) ?? false; }
  tieneAlguno(permisos: string[]): boolean { return permisos.length === 0 || permisos.some(permiso => this.tiene(permiso)); }

  iniciarSesion(email: string, password: string): Observable<LoginResponseDto> {
    return this.http.post<LoginResponseDto>(`${API}/auth/login`, { email, password }, { withCredentials: true })
      .pipe(tap(respuesta => { if (!respuesta.requiresTwoFactor && respuesta.result) this.aplicar(respuesta.result); }));
  }

  /** Segundo paso del ingreso para cuentas con verificación en dos pasos. */
  completarDosFactores(challengeToken: string, code: string): Observable<SessionResponse> {
    return this.http.post<SessionResponse>(`${API}/auth/login/2fa`, { challengeToken, code }, { withCredentials: true })
      .pipe(tap(respuesta => this.aplicar(respuesta)));
  }

  /**
   * Una sola renovación en vuelo aunque fallen varias solicitudes a la vez. Si otra pestaña renovó
   * en el mismo instante (401 refresh_conflict), se reintenta una vez tras una breve espera.
   */
  renovar(): Observable<AuthSessionDto> {
    // defer: el token anti-CSRF se lee al enviar, así el reintento usa el que dejó la otra renovación.
    const pedir = defer(() => this.http.post<SessionResponse>(`${API}/auth/refresh`, null, {
      withCredentials: true,
      headers: { 'X-CSRF-Token': this.tokenCsrf() }
    }));
    this.renovacionEnCurso ??= pedir.pipe(
      catchError(error => codigoDeError(error) === CONFLICTO_DE_RENOVACION
        ? timer(ESPERA_ANTE_CONFLICTO_MS).pipe(switchMap(() => pedir))
        : throwError(() => error)),
      tap(respuesta => this.aplicar(respuesta)),
      map(respuesta => respuesta.session),
      finalize(() => (this.renovacionEnCurso = null)),
      shareReplay(1)
    );
    return this.renovacionEnCurso;
  }

  /** Al cargar la aplicación: si quedó una sesión abierta en este navegador, la recupera. */
  restaurar(): Observable<AuthSessionDto | null> {
    borrarDatosDeLaVersionDeDemostracion();
    if (!leerCookie(COOKIE_CSRF)) return of(null);
    return this.renovar().pipe(catchError(() => of(null)));
  }

  cerrarSesion(): Observable<void> {
    return this.http.post<void>(`${API}/auth/logout`, null, { withCredentials: true, headers: { 'X-CSRF-Token': this.tokenCsrf() } })
      .pipe(catchError(() => of(undefined)), finalize(() => this.olvidar()));
  }

  recuperarContrasena(email: string): Observable<unknown> {
    return this.http.post(`${API}/auth/forgot-password`, { email });
  }

  // ── Alta y recuperación de cuentas (enlaces que llegan por correo) ──────────
  registrar(solicitud: RegisterRequest): Observable<MessageResponse> { return this.http.post<MessageResponse>(`${API}/auth/register`, solicitud); }
  confirmarCorreo(userId: string, token: string): Observable<MessageResponse> { return this.http.post<MessageResponse>(`${API}/auth/confirm-email`, { userId, token }); }
  reenviarConfirmacion(email: string): Observable<MessageResponse> { return this.http.post<MessageResponse>(`${API}/auth/resend-confirmation`, { email }); }
  restablecerContrasena(email: string, token: string, newPassword: string): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${API}/auth/reset-password`, { email, token, newPassword });
  }
  invitacion(token: string): Observable<InvitationInfoDto> { return this.http.get<InvitationInfoDto>(`${API}/auth/invitations/${encodeURIComponent(token)}`); }
  aceptarInvitacion(token: string, password: string, firstName: string | null, lastName: string | null): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${API}/auth/invitations/accept`, { token, password, firstName, lastName });
  }

  olvidar(): void { this.sesion.set(null); this.csrf = null; }

  /** La cookie primero: si otra pestaña renovó la sesión, la copia en memoria quedó vieja. */
  private tokenCsrf(): string { return leerCookie(COOKIE_CSRF) ?? this.csrf ?? ''; }
  private aplicar(respuesta: SessionResponse): void { this.sesion.set(respuesta.session); this.csrf = respuesta.csrfToken; }
}
