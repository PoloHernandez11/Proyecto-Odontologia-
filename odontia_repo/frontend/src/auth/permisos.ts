import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { AccionPermiso, ModuloPermiso, NivelPermiso, PermisoItemDto } from '../types/permisos';
import { useAuth } from './AuthContext';

/** Permisos del usuario actual. El odontólogo siempre tiene acceso; la secretaria, según la matriz. */
export function usePermisos() {
  const { usuario } = useAuth();
  const [items, setItems] = useState<PermisoItemDto[] | null>(null);

  useEffect(() => {
    let vigente = true;
    api<PermisoItemDto[]>('/permisos/mios')
      .then((datos) => {
        if (vigente) setItems(datos);
      })
      .catch(() => {
        if (vigente) setItems([]);
      });
    return () => {
      vigente = false;
    };
  }, [usuario?.id]);

  const nivel = (modulo: ModuloPermiso, accion: AccionPermiso): NivelPermiso =>
    items?.find((i) => i.modulo === modulo && i.accion === accion)?.nivel ?? 'SinAcceso';

  return {
    cargado: items !== null,
    nivel,
    puede: (modulo: ModuloPermiso, accion: AccionPermiso) => nivel(modulo, accion) !== 'SinAcceso',
  };
}
