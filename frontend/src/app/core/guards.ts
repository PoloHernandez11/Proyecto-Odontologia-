import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Sólo deja pasar a usuarios con sesión iniciada; el resto va al login. */
export const sesionGuard: CanActivateFn = (_ruta, estado) => {
  const auth = inject(AuthService);
  return auth.autenticado() || inject(Router).createUrlTree(['/login'], { queryParams: { volver: estado.url } });
};

/** Si ya hay sesión, el login redirige al inicio. */
export const invitadoGuard: CanActivateFn = () =>
  !inject(AuthService).autenticado() || inject(Router).createUrlTree(['/dashboard']);

/** Exige al menos uno de los permisos indicados. La autorización real la aplica siempre el backend. */
export const permisoGuard = (...permisos: string[]): CanActivateFn => () =>
  inject(AuthService).tieneAlguno(permisos) || inject(Router).createUrlTree(['/sin-acceso']);
