import { inject } from '@angular/core';
import { NavigationError, Router } from '@angular/router';
import { filter } from 'rxjs';

const CLAVE = 'odentix:recarga-por-version';
/** No recarga más de una vez por minuto, para no entrar en bucle si el archivo falta de verdad. */
const ESPERA_ENTRE_RECARGAS_MS = 60_000;

/**
 * Indica si el error es el de un fragmento de JavaScript que ya no existe en el servidor. Pasa cuando
 * una pestaña abierta antes de un despliegue navega a una pantalla de carga diferida.
 */
export function esFalloDeCargaDeFragmento(error: unknown): boolean {
  const texto = error instanceof Error ? `${error.name} ${error.message}` : String(error ?? '');
  return /ChunkLoadError|Loading chunk [\w-]+ failed|Failed to fetch dynamically imported module|error loading dynamically imported module|Importing a module script failed/i.test(texto);
}

/**
 * Si falla la carga de una pantalla porque se desplegó una versión nueva, recarga la página en la URL
 * a la que se quería ir, así el navegador baja la versión actual.
 */
export function recargarAnteNuevaVersion(): void {
  const router = inject(Router);
  router.events.pipe(filter((evento): evento is NavigationError => evento instanceof NavigationError)).subscribe(evento => {
    if (!esFalloDeCargaDeFragmento(evento.error)) return;
    let ultima = 0;
    try { ultima = Number(sessionStorage.getItem(CLAVE) ?? 0); } catch { /* almacenamiento no disponible */ }
    if (Date.now() - ultima < ESPERA_ENTRE_RECARGAS_MS) return;
    try { sessionStorage.setItem(CLAVE, String(Date.now())); } catch { /* almacenamiento no disponible */ }
    window.location.assign(evento.url);
  });
}
