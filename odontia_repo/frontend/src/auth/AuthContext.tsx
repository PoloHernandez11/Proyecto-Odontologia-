import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { api, leerSesion, SESSION_KEY, type SesionGuardada } from '../api/client';
import type { GoogleLoginRequest, LoginResponse, RegistroDuenoDto, AceptarInvitacionDto, RolUsuario } from '../types/api';

export const ROLES = { SECRETARIA: 'Secretaria', ODONTOLOGO: 'Odontologo' } as const satisfies Record<string, RolUsuario>;

export type UsuarioSesion = SesionGuardada['usuario'];

/** Pantalla inicial de cada rol. */
export const rutaInicio = (rol: RolUsuario): string => (rol === ROLES.ODONTOLOGO ? '/odontologo' : '/secretaria');

interface AuthValue {
  usuario: UsuarioSesion | null;
  esOdontologo: boolean;
  /** Ingreso con Google. El backend decide si la cuenta está autorizada y con qué rol. */
  loginGoogle: (credential: string) => Promise<UsuarioSesion>;
  /** Alta del odontólogo dueño con su cuenta de Google. Solo funciona sin usuarios previos. */
  registrarDueno: (dto: RegistroDuenoDto) => Promise<UsuarioSesion>;
  /** Alta de la secretaria con el token de la invitación y su cuenta de Google. */
  aceptarInvitacion: (dto: AceptarInvitacionDto) => Promise<UsuarioSesion>;
  logout: () => void;
}

const AuthContext = createContext<AuthValue | null>(null);

/** Guarda la respuesta como sesión y devuelve el usuario. Solo se guarda el token de la API, nunca credenciales. */
function guardarSesion(data: LoginResponse): UsuarioSesion {
  const nueva: SesionGuardada = {
    token: data.token,
    expira: data.expira,
    usuario: {
      id: data.id,
      nombre: data.nombre,
      email: data.email,
      rol: data.rol,
      consultorioId: data.consultorioId,
      consultorioNombre: data.consultorioNombre,
    },
  };
  sessionStorage.setItem(SESSION_KEY, JSON.stringify(nueva));
  return nueva.usuario;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [sesion, setSesion] = useState<SesionGuardada | null>(leerSesion);

  const loginGoogle = useCallback(async (credential: string) => {
    const body: GoogleLoginRequest = { credential };
    const data = await api<LoginResponse>('/auth/google', { method: 'POST', body });
    const u = guardarSesion(data);
    setSesion(leerSesion());
    return u;
  }, []);

  const registrarDueno = useCallback(async (dto: RegistroDuenoDto) => {
    const data = await api<LoginResponse>('/auth/registro-dueno', { method: 'POST', body: dto });
    const u = guardarSesion(data);
    setSesion(leerSesion());
    return u;
  }, []);

  const aceptarInvitacion = useCallback(async (dto: AceptarInvitacionDto) => {
    const data = await api<LoginResponse>('/auth/invitaciones/aceptar', { method: 'POST', body: dto });
    const u = guardarSesion(data);
    setSesion(leerSesion());
    return u;
  }, []);

  const logout = useCallback(() => {
    sessionStorage.removeItem(SESSION_KEY);
    setSesion(null);
  }, []);

  const value = useMemo<AuthValue>(() => {
    const usuario = sesion?.usuario ?? null;
    return {
      usuario,
      esOdontologo: usuario?.rol === ROLES.ODONTOLOGO,
      loginGoogle,
      registrarDueno,
      aceptarInvitacion,
      logout,
    };
  }, [sesion, loginGoogle, registrarDueno, aceptarInvitacion, logout]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth debe usarse dentro de <AuthProvider>.');
  return ctx;
}

/** Protege una ruta por rol. Es solo UX: la seguridad real la aplica el backend. */
export function RutaProtegida({ roles, children }: { roles?: RolUsuario[]; children: ReactNode }) {
  const { usuario } = useAuth();
  if (!usuario) return <Navigate to="/login" replace />;
  if (roles && !roles.includes(usuario.rol)) return <Navigate to={rutaInicio(usuario.rol)} replace />;
  return <>{children}</>;
}
