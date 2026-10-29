// Espejo de los DTOs de backend/OdontoApp.Api/Dtos/Dtos.cs (sección Auth e Invitaciones).
// Si cambia un record de C#, cambiá su interfaz acá con el mismo nombre y los mismos campos.
// Los nombres de campo quedan en camelCase porque así los serializa ASP.NET Core.

/** Enum RolUsuario (se serializa como texto). */
export type RolUsuario = 'Odontologo' | 'Secretaria';

/** Estado calculado por el backend para una invitación. */
export type EstadoInvitacion = 'Pendiente' | 'Aceptada' | 'Vencida' | 'Revocada';

/** GoogleLoginRequest: credential = ID token de Firebase (proveedor Google). */
export interface GoogleLoginRequest {
  credential: string;
}

/** LoginResponse: token de sesión propio de la API y datos del usuario. */
export interface LoginResponse {
  token: string;
  expira: string;
  id: number;
  nombre: string;
  email: string;
  rol: RolUsuario;
  consultorioId: number;
  consultorioNombre: string;
}

/** EstadoConsultorioDto */
export interface EstadoConsultorioDto {
  registrado: boolean;
  googleConfigurado: boolean;
}

/** RegistroDuenoDto */
export interface RegistroDuenoDto {
  credential: string;
  nombreConsultorio: string;
}

/** InvitacionPublicaDto */
export interface InvitacionPublicaDto {
  email: string;
  rol: RolUsuario;
  consultorioNombre: string;
  expiraEn: string;
}

/** AceptarInvitacionDto */
export interface AceptarInvitacionDto {
  token: string;
  credential: string;
}

/** UsuarioDto: miembro del consultorio, sin credenciales. */
export interface UsuarioDto {
  id: number;
  nombre: string;
  email: string;
  rol: RolUsuario;
  activo: boolean;
}

/** ActivarUsuarioDto */
export interface ActivarUsuarioDto {
  activo: boolean;
}

/** InvitacionDto */
export interface InvitacionDto {
  id: number;
  email: string;
  rol: RolUsuario;
  estado: EstadoInvitacion;
  creadaEn: string;
  expiraEn: string;
  usadaEn: string | null;
  creadaPorNombre: string;
}

/** InvitacionCreadaDto: el token llega una sola vez, para armar el enlace. */
export interface InvitacionCreadaDto {
  invitacion: InvitacionDto;
  token: string;
}

/** CrearInvitacionDto */
export interface CrearInvitacionDto {
  email: string;
}
