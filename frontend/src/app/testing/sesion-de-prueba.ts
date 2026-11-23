import { AuthSessionDto } from '../api/schema';

/** Zona de los datos de prueba: distinta de UTC para que se note si algo usa la zona del navegador. */
export const ZONA_DE_PRUEBA = 'America/Argentina/Buenos_Aires';

/** Sesión ficticia (sin datos reales) para las pruebas. */
export function sesionDePrueba(
  permisos: string[] = [],
  opciones: { token?: string; profesional?: boolean; zona?: string } = {}
): AuthSessionDto {
  return {
    accessToken: opciones.token ?? 'token-de-prueba', accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z', sessionId: 's1', activeConsultorioId: 'c1',
    permissions: permisos,
    memberships: [{
      consultorioId: 'c1', consultorioName: 'Consultorio de prueba', roles: ['Recepción'],
      isProfessional: opciones.profesional ?? false, timeZoneId: opciones.zona ?? ZONA_DE_PRUEBA
    }],
    user: { id: 'u1', email: 'demo@example.test', firstName: 'Ana', lastName: 'Prueba', displayName: 'Ana Prueba', twoFactorEnabled: false }
  };
}

/** Página vacía o con los elementos indicados, con la forma de Paginado<T>. */
export function pagina<T>(items: T[]): { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number; hasNext: boolean } {
  return { items, page: 1, pageSize: 50, totalCount: items.length, totalPages: 1, hasNext: false };
}
