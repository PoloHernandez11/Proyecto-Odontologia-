// Formatos y etiquetas compartidas (es-AR).

const moneda = new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' });
export const fmtMoneda = (n) => moneda.format(n ?? 0);

/** "2026-10-15" (DateOnly del backend) -> "15/10/2026" sin corrimiento de zona horaria. */
export const fmtFecha = (isoDate) => (isoDate ? new Date(`${isoDate.slice(0, 10)}T00:00:00`).toLocaleDateString('es-AR') : '');

/** DateTime UTC del backend -> fecha y hora local. */
export const fmtFechaHora = (iso) => new Date(iso).toLocaleString('es-AR', { dateStyle: 'short', timeStyle: 'short' });

/** "09:30:00" -> "09:30" */
export const fmtHora = (hora) => hora?.slice(0, 5) ?? '';

/** Hoy en hora local como YYYY-MM-DD (formato de <input type="date"> y de DateOnly). */
export const hoyIso = () => new Date().toLocaleDateString('en-CA');

export const primerDiaDelMesIso = () => `${hoyIso().slice(0, 8)}01`;

/** Suma días a una fecha YYYY-MM-DD. */
export function sumarDias(isoDate, dias) {
  const d = new Date(`${isoDate}T00:00:00`);
  d.setDate(d.getDate() + dias);
  return d.toLocaleDateString('en-CA');
}

/** Lunes de la semana de la fecha dada. */
export function lunesDe(isoDate) {
  const d = new Date(`${isoDate}T00:00:00`);
  const dia = (d.getDay() + 6) % 7; // lunes = 0
  return sumarDias(isoDate, -dia);
}

/** "2026-10-19" -> "lunes 19 de octubre de 2026" */
export const fmtFechaLarga = (isoDate) =>
  new Date(`${isoDate}T00:00:00`).toLocaleDateString('es-AR', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });

export const capitalizar = (s) => (s ? s[0].toUpperCase() + s.slice(1) : s);

/** "Rodríguez, Martín" o "Martín Rodríguez" -> "MR" */
export function iniciales(nombre) {
  if (!nombre) return '';
  const partes = nombre.includes(',') ? nombre.split(',').reverse() : nombre.split(' ');
  return partes
    .map((p) => p.trim().replace(/^(Dr|Dra)\.?\s*/i, '')[0])
    .filter(Boolean)
    .slice(0, 2)
    .join('')
    .toUpperCase();
}

/** "Rodríguez, Martín" -> "Martín Rodríguez" */
export const nombreNatural = (apellidoNombre) =>
  apellidoNombre?.includes(',') ? apellidoNombre.split(',').reverse().map((s) => s.trim()).join(' ') : apellidoNombre;

export function calcularEdad(fechaNacimiento) {
  if (!fechaNacimiento) return null;
  const nac = new Date(`${fechaNacimiento}T00:00:00`);
  const hoy = new Date();
  let edad = hoy.getFullYear() - nac.getFullYear();
  if (hoy < new Date(hoy.getFullYear(), nac.getMonth(), nac.getDate())) edad--;
  return edad;
}

export function saludo() {
  const h = new Date().getHours();
  return h < 12 ? 'Buenos días' : h < 20 ? 'Buenas tardes' : 'Buenas noches';
}

/** "09:30:00" -> minutos desde las 00:00 */
export const minutos = (hora) => {
  const [h, m] = hora.split(':').map(Number);
  return h * 60 + m;
};

export const horaDeMinutos = (min) => `${String(Math.floor(min / 60)).padStart(2, '0')}:${String(min % 60).padStart(2, '0')}`;

/** Hora de fin de un turno: "09:15" + 45 -> "10:00" */
export const finTurno = (t) => horaDeMinutos(minutos(t.hora) + (t.duracionMinutos ?? 30));

/** Turno que todavía ocupa la agenda (para contadores y grillas). */
export const turnoActivo = (t) => t.estado !== 'Cancelado';

export const MEDIOS_PAGO = {
  Efectivo: 'Efectivo',
  Transferencia: 'Transferencia',
  Debito: 'Débito',
  Credito: 'Crédito',
  MercadoPago: 'Mercado Pago',
  ObraSocial: 'Obra social',
  Otro: 'Otro',
};

export const CATEGORIAS = {
  Ingreso: {
    CobroPaciente: 'Cobro a paciente',
    ObraSocial: 'Obra social',
    OtroIngreso: 'Otro ingreso',
  },
  Egreso: {
    Laboratorio: 'Laboratorio',
    Insumos: 'Insumos',
    Alquiler: 'Alquiler',
    Servicios: 'Servicios',
    Sueldos: 'Sueldos',
    Impuestos: 'Impuestos',
    Mantenimiento: 'Mantenimiento',
    OtroEgreso: 'Otro egreso',
  },
};

export const etiquetaCategoria = (cat) => CATEGORIAS.Ingreso[cat] ?? CATEGORIAS.Egreso[cat] ?? cat;

export const ESTADOS_TURNO = {
  Pendiente: 'Por confirmar',
  Confirmado: 'Confirmado',
  EnSala: 'En sala',
  Atendido: 'Atendido',
  Cancelado: 'Cancelado',
  Ausente: 'No asistió',
};

/** Chip de estado de un turno (la urgencia se muestra aparte). */
export const claseEstado = (estado) => `chip chip--${estado.toLowerCase()}`;

export const PRIORIDADES = { Alta: 'Alta', Media: 'Media', Baja: 'Baja' };

export const CATEGORIAS_TAREA = ['Recepción', 'Clínica', 'Laboratorio', 'Insumos', 'Administración'];

export const ESTADOS_LAB = {
  Enviado: 'En laboratorio',
  Recibido: 'Recibido',
  Colocado: 'Colocado',
  Cancelado: 'Cancelado',
};

export const TIPOS_TRABAJO_LAB = [
  'Corona',
  'Puente',
  'Incrustación',
  'Prótesis removible',
  'Prótesis completa',
  'Placa miorrelajante',
  'Perno muñón',
  'Compostura',
  'Otro',
];
