import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { codigoDeError } from './api';
import { AuthService, CONFLICTO_DE_RENOVACION } from './auth.service';

const LLAMADA_DE_AUTENTICACION = /\/auth\/(login|refresh|logout|forgot-password|reset-password|confirm-email|resend-confirmation|register|invitations)/;

/** Motivo que el login muestra al llegar por un cierre de sesión forzado (?sesion=…). */
export type MotivoDeCierre = 'cerrada' | 'vencida';

/**
 * Agrega el Bearer a cada solicitud y, ante un 401 (incluido `session_invalidated`: la sesión se
 * revocó en otro lado), renueva la sesión una vez y reintenta. Si la renovación falla, lleva al login
 * con el motivo. Un 401 `refresh_conflict` que persiste tras el reintento no cierra la sesión.
 */
export const authInterceptor: HttpInterceptorFn = (solicitud, siguiente) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const esDeAutenticacion = LLAMADA_DE_AUTENTICACION.test(solicitud.url);
  const conToken = (original: HttpRequest<unknown>) =>
    auth.accessToken && !esDeAutenticacion
      ? original.clone({ setHeaders: { Authorization: `Bearer ${auth.accessToken}` } })
      : original;

  return siguiente(conToken(solicitud)).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== 401 || esDeAutenticacion) return throwError(() => error);
      return auth.renovar().pipe(
        // Sólo los errores de la renovación: si falla el reintento, ese error llega tal cual a quien llamó.
        catchError(errorDeRenovacion => {
          if (codigoDeError(errorDeRenovacion) === CONFLICTO_DE_RENOVACION) return throwError(() => error);
          const cerradaEnOtroLado = codigoDeError(error) === 'session_invalidated' || codigoDeError(errorDeRenovacion) === 'session_invalidated';
          const motivo: MotivoDeCierre = cerradaEnOtroLado ? 'cerrada' : 'vencida';
          const volver = router.url;
          auth.olvidar();
          router.navigate(['/login'], {
            queryParams: { sesion: motivo, volver: volver && volver !== '/' && !volver.startsWith('/login') ? volver : null }
          });
          return throwError(() => errorDeRenovacion);
        }),
        switchMap(() => siguiente(conToken(solicitud)))
      );
    })
  );
};
