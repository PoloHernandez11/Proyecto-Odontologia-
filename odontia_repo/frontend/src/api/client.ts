const API_URL: string = import.meta.env.VITE_API_URL ?? 'http://localhost:5000/api';

export const SESSION_KEY = 'odontoapp.sesion';

/** Sesión guardada en sessionStorage. Se reemplaza el JSON completo al iniciar sesión. */
export interface SesionGuardada {
  token: string;
  expira: string;
  usuario: {
    id: number;
    nombre: string;
    rol: import('../types/api').RolUsuario;
    consultorioId: number;
    consultorioNombre: string;
  };
}

export function leerSesion(): SesionGuardada | null {
  try {
    return JSON.parse(sessionStorage.getItem(SESSION_KEY) ?? 'null');
  } catch {
    return null;
  }
}

export type MetodoHttp = 'GET' | 'POST' | 'PUT' | 'DELETE';

interface OpcionesApi {
  method?: MetodoHttp;
  body?: unknown;
  signal?: AbortSignal;
}

/** Error con el mensaje del backend ({ mensaje } o ProblemDetails). */
export class ApiError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message);
  }
}

/**
 * fetch con el JWT de la sesión. Lanza ApiError con el mensaje del backend.
 * Un 401 cierra la sesión y vuelve al login.
 */
export async function api<T = unknown>(path: string, { method = 'GET', body, signal }: OpcionesApi = {}): Promise<T> {
  const sesion = leerSesion();
  const res = await fetch(`${API_URL}${path}`, {
    method,
    signal,
    headers: {
      'Content-Type': 'application/json',
      ...(sesion?.token ? { Authorization: `Bearer ${sesion.token}` } : {}),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  // Los 401 de /auth/* son errores de inicio de sesión (no de sesión vencida): se muestran en la pantalla.
  if (res.status === 401 && !path.startsWith('/auth/')) {
    sessionStorage.removeItem(SESSION_KEY);
    window.location.assign('/login');
    throw new ApiError('La sesión expiró.', 401);
  }
  if (!res.ok) {
    const err = (await res.json().catch(() => ({}))) as {
      mensaje?: string;
      title?: string;
      errors?: Record<string, string[]>;
    };
    const validacion = err.errors ? Object.values(err.errors).flat().join(' ') : undefined;
    const porDefecto = res.status === 403 ? 'No tenés permisos para esta acción.' : `Error ${res.status}`;
    throw new ApiError(err.mensaje ?? validacion ?? err.title ?? porDefecto, res.status);
  }
  return res.status === 204 ? (null as T) : ((await res.json()) as T);
}
