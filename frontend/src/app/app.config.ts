import { ApplicationConfig, inject, provideAppInitializer, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth.interceptor';
import { AuthService } from './core/auth.service';
import { recargarAnteNuevaVersion } from './core/recarga-por-version';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    // Recupera la sesión antes de evaluar las rutas, para que recargar la página no envíe al login.
    provideAppInitializer(() => inject(AuthService).restaurar()),
    // Tras un despliegue, una pestaña vieja puede pedir pantallas que ya no existen: se recarga sola.
    provideAppInitializer(() => recargarAnteNuevaVersion()),
    provideRouter(routes)
  ]
};
