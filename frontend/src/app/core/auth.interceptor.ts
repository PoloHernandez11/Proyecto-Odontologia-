import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const LLAMADA_DE_AUTENTICACION = /\/auth\/(login|refresh|logout|forgot-password|reset-password|confirm-email|resend-confirmation|register|invitations)/;

/** Agrega el Bearer a cada solicitud y, ante un 401, renueva la sesión una vez y reintenta. */
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
        switchMap(() => siguiente(conToken(solicitud))),
        catchError(errorDeRenovacion => {
          auth.olvidar();
          router.navigate(['/login']);
          return throwError(() => errorDeRenovacion);
        })
      );
    })
  );
};
