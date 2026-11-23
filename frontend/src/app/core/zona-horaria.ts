/**
 * Fechas y horas en la zona horaria del consultorio (IANA, p. ej. "America/Argentina/Buenos_Aires").
 * El backend arma los rangos de la agenda en esa zona; el navegador puede estar en otra.
 */

export interface PartesDeFecha { anio: number; mes: number; dia: number; hora: number; minuto: number; }

const formateadores = new Map<string, Intl.DateTimeFormat>();

export function zonaDelNavegador(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
}

/** Devuelve la zona si el navegador la reconoce; si no, null. */
export function zonaValida(zona: string | null | undefined): string | null {
  if (!zona) return null;
  try { new Intl.DateTimeFormat('en-US', { timeZone: zona }); return zona; } catch { return null; }
}

function formateador(zona: string): Intl.DateTimeFormat {
  let formato = formateadores.get(zona);
  if (!formato) {
    formato = new Intl.DateTimeFormat('en-US', {
      timeZone: zona, hourCycle: 'h23', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit'
    });
    formateadores.set(zona, formato);
  }
  return formato;
}

/** Fecha y hora de pared de un instante en la zona indicada. */
export function partesEnZona(instante: Date | string, zona: string): PartesDeFecha {
  const valores: Record<string, number> = {};
  for (const parte of formateador(zona).formatToParts(new Date(instante))) {
    if (parte.type !== 'literal') valores[parte.type] = Number(parte.value);
  }
  return { anio: valores['year'], mes: valores['month'], dia: valores['day'], hora: valores['hour'] % 24, minuto: valores['minute'] };
}

const dos = (numero: number) => String(numero).padStart(2, '0');

/** yyyy-MM-dd del instante en la zona. */
export function fechaEnZona(instante: Date | string, zona: string): string {
  const p = partesEnZona(instante, zona);
  return `${p.anio}-${dos(p.mes)}-${dos(p.dia)}`;
}

/** HH:mm del instante en la zona. */
export function horaEnZona(instante: Date | string, zona: string): string {
  const p = partesEnZona(instante, zona);
  return `${dos(p.hora)}:${dos(p.minuto)}`;
}

/** Minutos desde la medianoche, en la zona. */
export function minutosEnZona(instante: Date | string, zona: string): number {
  const p = partesEnZona(instante, zona);
  return p.hora * 60 + p.minuto;
}

/** Instante UTC que corresponde a la fecha (yyyy-MM-dd) y hora (HH:mm) de pared en la zona. */
export function instanteEnZona(fecha: string, hora: string, zona: string): Date {
  const [anio, mes, dia] = fecha.split('-').map(Number);
  const [horas, minutos] = hora.split(':').map(Number);
  const deseado = Date.UTC(anio, mes - 1, dia, horas, minutos);
  let instante = deseado;
  // Dos pasadas alcanzan para los cambios de horario de verano.
  for (let i = 0; i < 2; i++) {
    const p = partesEnZona(new Date(instante), zona);
    const visto = Date.UTC(p.anio, p.mes - 1, p.dia, p.hora, p.minuto);
    instante += deseado - visto;
  }
  return new Date(instante);
}

/** Date local (al mediodía) cuyos campos de calendario son la fecha yyyy-MM-dd: sirve para sumar días o meses sin depender de la zona. */
export function diaDeCalendario(fecha: string): Date {
  const [anio, mes, dia] = fecha.split('-').map(Number);
  return new Date(anio, mes - 1, dia, 12, 0, 0, 0);
}

// ── Zona del consultorio activo ──────────────────────────────────────────────
// La define AuthService a partir de la membresía activa de la sesión (memberships[].timeZoneId).
// La fuente lee signals: quien la consulte dentro de una plantilla o de un computed() se actualiza
// solo cuando cambia la sesión o el consultorio activo.
let fuenteDeZona: () => string | null = () => null;

/** Registra de dónde sale la zona del consultorio activo (la llama AuthService). */
export function definirZonaDelConsultorio(fuente: () => string | null): void { fuenteDeZona = fuente; }

/** Zona IANA del consultorio activo; sin sesión, la del navegador. */
export function zonaDelConsultorio(): string { return fuenteDeZona() ?? zonaDelNavegador(); }

/** yyyy-MM-dd de "hoy" (o del instante indicado) en la zona del consultorio. */
export function fechaDelConsultorio(instante: Date | string = new Date()): string {
  return fechaEnZona(instante, zonaDelConsultorio());
}

/** Instante UTC del comienzo (00:00) del día yyyy-MM-dd en la zona del consultorio. */
export function inicioDelDia(fecha: string): Date { return instanteEnZona(fecha, '00:00', zonaDelConsultorio()); }

/** Suma días a una fecha de calendario yyyy-MM-dd (sin depender de la zona del navegador). */
export function sumarDias(fecha: string, dias: number): string {
  const [anio, mes, dia] = fecha.split('-').map(Number);
  const resultado = new Date(Date.UTC(anio, mes - 1, dia + dias));
  return resultado.toISOString().slice(0, 10);
}

/** Formatea un instante con Intl en la zona del consultorio. */
export function formatoEnConsultorio(instante: Date | string, opciones: Intl.DateTimeFormatOptions): string {
  return new Intl.DateTimeFormat('es-AR', { ...opciones, timeZone: zonaDelConsultorio() }).format(new Date(instante));
}
