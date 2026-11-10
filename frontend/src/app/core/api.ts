import { HttpErrorResponse, HttpParams } from '@angular/common/http';
import { ApiProblem } from '../api/schema';

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

/** Mensaje para mostrarle al usuario a partir de un error de la API (application/problem+json). */
export function mensajeDeError(error: unknown, porDefecto = 'No se pudo completar la operación.'): string {
  if (!(error instanceof HttpErrorResponse)) return porDefecto;
  if (error.status === 0) return 'No hay conexión con el servidor. Verificá que la API esté en ejecución.';
  const problema = error.error as ApiProblem | null;
  if (problema && typeof problema === 'object') {
    const detalles = Object.values(problema.errors ?? {}).flat();
    if (detalles.length) return detalles.join(' ');
    if (problema.detail) return problema.detail;
  }
  if (error.status === 403) return 'No tenés permiso para realizar esta acción.';
  if (error.status === 404) return 'El dato solicitado no existe.';
  if (error.status === 429) return 'Demasiadas solicitudes. Esperá un momento y volvé a intentar.';
  if (error.status >= 502 && error.status <= 504) return 'No hay conexión con el servidor. Verificá que la API esté en ejecución.';
  return porDefecto;
}

/** Fecha de calendario local en formato yyyy-MM-dd. */
export function fechaIso(fecha: Date): string {
  return `${fecha.getFullYear()}-${String(fecha.getMonth() + 1).padStart(2, '0')}-${String(fecha.getDate()).padStart(2, '0')}`;
}

/** Hora local HH:mm de un instante UTC devuelto por la API. */
export function horaLocal(instanteUtc: string): string {
  const fecha = new Date(instanteUtc);
  return `${String(fecha.getHours()).padStart(2, '0')}:${String(fecha.getMinutes()).padStart(2, '0')}`;
}

/** Fecha local dd/MM/yyyy de un instante UTC o de una fecha de calendario. */
export function fechaCorta(valor: string | null | undefined): string {
  if (!valor) return '—';
  const fecha = /^\d{4}-\d{2}-\d{2}$/.test(valor) ? new Date(`${valor}T12:00:00`) : new Date(valor);
  return new Intl.DateTimeFormat('es-AR', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(fecha);
}

export function iniciales(nombre: string): string {
  const partes = nombre.replace(/^(Dra?\.|Lic\.|Od\.)\s+/i, '').trim().split(/\s+/);
  return ((partes[0]?.[0] ?? '') + (partes.length > 1 ? partes[partes.length - 1][0] : '')).toUpperCase();
}

export function pesos(importe: number): string {
  return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 0 }).format(importe);
}
