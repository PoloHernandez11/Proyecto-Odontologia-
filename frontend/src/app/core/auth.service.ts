import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import { AuthSessionDto, InvitationInfoDto, LoginResponseDto, MessageResponse, RegisterRequest, SessionResponse } from '../api/schema';
import { API } from './api';

function leerCookie(nombre: string): string | null {
  if (typeof document === 'undefined') return null;
  return document.cookie.split('; ').find(cookie => cookie.startsWith(nombre + '='))?.split('=')[1] ?? null;
}

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

  /** Una sola renovación en vuelo aunque fallen varias solicitudes a la vez. */
  renovar(): Observable<AuthSessionDto> {
    this.renovacionEnCurso ??= this.http.post<SessionResponse>(`${API}/auth/refresh`, null, {
      withCredentials: true,
      headers: { 'X-CSRF-Token': this.tokenCsrf() }
    }).pipe(
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
    if (!leerCookie('odontia_csrf')) return of(null);
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

  private tokenCsrf(): string { return this.csrf ?? leerCookie('odontia_csrf') ?? ''; }
  private aplicar(respuesta: SessionResponse): void { this.sesion.set(respuesta.session); this.csrf = respuesta.csrfToken; }
}
