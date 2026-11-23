import { HttpErrorResponse, HttpParams } from '@angular/common/http';
import { ApiProblem } from '../api/schema';
import { fechaEnZona, horaEnZona, zonaDelConsultorio } from './zona-horaria';

/** Ruta relativa: en desarrollo pasa por el proxy de Angular (proxy.conf.json) y en producción por un rewrite del hosting. */
export const API = '/api/v1';

export interface Paginado<T> { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number; hasNext: boolean; }

/** Arma los parámetros de consulta descartando los valores vacíos. */
export function parametros(valores: Record<string, string | number | boolean | null | undefined>): HttpParams {
  let params = new HttpParams();
  for (const [clave, valor] of Object.entries(valores)) {
    if (valor !== null && valor !== undefined && valor !== '') params = params.set(clave, String(valor));
  }
  return params;
}

/** Código (`code`) del problem+json de un error de la API, si lo trae. */
export function codigoDeError(error: unknown): string | null {
  if (!(error instanceof HttpErrorResponse)) return null;
  const problema = error.error as Partial<ApiProblem> | null;
  return problema && typeof problema === 'object' && typeof problema.code === 'string' ? problema.code : null;
}

/** Segundos de espera que indica el encabezado Retry-After (número de segundos o fecha HTTP). */
export function segundosDeEspera(error: HttpErrorResponse, ahora = Date.now()): number | null {
  const valor = error.headers?.get('Retry-After')?.trim();
  if (!valor) return null;
  if (/^\d+$/.test(valor)) return Number(valor);
  const fecha = Date.parse(valor);
  return Number.isNaN(fecha) ? null : Math.max(0, Math.ceil((fecha - ahora) / 1000));
}

function textoDeEspera(segundos: number): string {
  if (segundos <= 1) return 'un segundo';
  if (segundos < 60) return `${segundos} segundos`;
  const minutos = Math.ceil(segundos / 60);
  return minutos === 1 ? 'un minuto' : `${minutos} minutos`;
}

/** Mensaje para mostrarle al usuario a partir de un error de la API (application/problem+json). */
export function mensajeDeError(error: unknown, porDefecto = 'No se pudo completar la operación.'): string {
  if (!(error instanceof HttpErrorResponse)) return porDefecto;
  if (error.status === 0) return 'No hay conexión con el servidor. Verificá que la API esté en ejecución.';
  if (error.status === 413) return 'El contenido es demasiado grande. Los documentos pueden pesar hasta 20 MB y el resto de los datos, hasta 2 MB.';
  if (error.status === 429) {
    const segundos = segundosDeEspera(error);
    return segundos === null
      ? 'Demasiadas solicitudes. Esperá un momento y volvé a intentar.'
      : `Demasiadas solicitudes. Esperá ${textoDeEspera(segundos)} y volvé a intentar.`;
  }
  if (error.status === 401 && codigoDeError(error) === 'session_invalidated') return 'Tu sesión se cerró. Ingresá de nuevo para continuar.';
  const problema = error.error as ApiProblem | null;
  if (problema && typeof problema === 'object') {
    const detalles = Object.values(problema.errors ?? {}).flat();
    if (detalles.length) return detalles.join(' ');
    if (problema.detail) return problema.detail;
  }
  if (error.status === 403) return 'No tenés permiso para realizar esta acción.';
  if (error.status === 404) return 'El dato solicitado no existe.';
  if (error.status >= 502 && error.status <= 504) return 'No hay conexión con el servidor. Verificá que la API esté en ejecución.';
  return porDefecto;
}

/** Fecha de calendario local en formato yyyy-MM-dd (campos locales del Date, sin conversión de zona). */
export function fechaIso(fecha: Date): string {
  return `${fecha.getFullYear()}-${String(fecha.getMonth() + 1).padStart(2, '0')}-${String(fecha.getDate()).padStart(2, '0')}`;
}

/** Hora HH:mm, en la zona del consultorio, de un instante UTC devuelto por la API. */
export function horaLocal(instanteUtc: string): string {
  return horaEnZona(instanteUtc, zonaDelConsultorio());
}

/**
 * Fecha dd/MM/yyyy. Una fecha de calendario (yyyy-MM-dd) se muestra tal cual;
 * un instante UTC, como fecha del consultorio (en su zona horaria, no en la del navegador).
 */
export function fechaCorta(valor: string | null | undefined): string {
  if (!valor) return '—';
  const fecha = /^\d{4}-\d{2}-\d{2}$/.test(valor) ? valor : fechaEnZona(valor, zonaDelConsultorio());
  const [anio, mes, dia] = fecha.split('-');
  return `${dia}/${mes}/${anio}`;
}

export function iniciales(nombre: string): string {
  const partes = nombre.replace(/^(Dra?\.|Lic\.|Od\.)\s+/i, '').trim().split(/\s+/);
  return ((partes[0]?.[0] ?? '') + (partes.length > 1 ? partes[partes.length - 1][0] : '')).toUpperCase();
}

export function pesos(importe: number): string {
  return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 0 }).format(importe);
}

/**
 * 409 con `code: "concurrency_conflict"`: otra persona modificó el dato mientras se editaba.
 * Quien lo recibe muestra el `detail` (lo devuelve mensajeDeError) y vuelve a cargar los datos.
 */
export function esConflictoDeConcurrencia(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 409 && codigoDeError(error) === 'concurrency_conflict';
}

/** Plural regular en castellano: vocal + s ("turno" → "turnos"), consonante + es ("profesional" → "profesionales"). */
function pluralDe(singular: string): string {
  return /[aeiouáéíóú]$/i.test(singular) ? `${singular}s` : `${singular}es`;
}

/** Cantidad con la palabra concordada: 1 resultado, 2 resultados, 0 resultados. */
export function cantidad(numero: number, singular: string, plural = pluralDe(singular)): string {
  return `${numero} ${numero === 1 ? singular : plural}`;
}
