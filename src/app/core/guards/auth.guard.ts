import { inject, PLATFORM_ID } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { routes } from '../../app.routes';
import { isPlatformBrowser } from '@angular/common';
import { platform } from 'os';

/*export const authGuard: CanActivateFn = (route, state) => {
  const router = inject(Router);
  const token = localStorage.getItem('token');

  if (token) {
    return true; // Puede pasar
  }

  // No hay token, pal lobby (al login)
  router.navigate(['/login']);
  return false;
};
*/
export const authGuard: CanActivateFn = () => {
  const router = inject(Router);

  //En el servidor ssr no existe localstorage: dejemos que decida el navegador
  if(!isPlatformBrowser(inject(PLATFORM_ID))){
    return true;
  }
  //CON TOKEN puede pasar; sin token se lo manda al login

  return localStorage.getItem('token') ? true : router.createUrlTree(['/login']);
}