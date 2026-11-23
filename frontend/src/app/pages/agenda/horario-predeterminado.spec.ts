import { fechaEnZona, horaEnZona, instanteEnZona } from '../../core/zona-horaria';
import { horarioPredeterminado } from './horario-predeterminado';

// Buenos Aires: UTC-3 todo el año, sin importar la zona de la máquina que corre las pruebas.
const ZONA = 'America/Argentina/Buenos_Aires';
const en = (fecha: string, hora: string) => instanteEnZona(fecha, hora, ZONA);

describe('zona horaria del consultorio', () => {
  it('convierte la hora de pared de la zona a UTC y viceversa', () => {
    const instante = instanteEnZona('2026-03-10', '09:30', ZONA);
    expect(instante.toISOString()).toBe('2026-03-10T12:30:00.000Z');
    expect(horaEnZona(instante, ZONA)).toBe('09:30');
    expect(fechaEnZona('2026-03-11T02:00:00Z', ZONA)).toBe('2026-03-10');
  });

  it('tiene en cuenta el horario de verano de otras zonas', () => {
    // Madrid pasa a UTC+2 el 29/03/2026.
    expect(instanteEnZona('2026-03-28', '10:00', 'Europe/Madrid').toISOString()).toBe('2026-03-28T09:00:00.000Z');
    expect(instanteEnZona('2026-03-30', '10:00', 'Europe/Madrid').toISOString()).toBe('2026-03-30T08:00:00.000Z');
  });
});

describe('horarioPredeterminado (alta de turno)', () => {
  it('propone el próximo cuarto de hora desde ahora', () => {
    expect(horarioPredeterminado(en('2026-03-10', '10:07'), '2026-03-10', ZONA)).toEqual({ fecha: '2026-03-10', hora: '10:15' });
    expect(horarioPredeterminado(en('2026-03-10', '10:15'), '2026-03-10', ZONA)).toEqual({ fecha: '2026-03-10', hora: '10:30' });
  });

  it('respeta un paso de 30 minutos', () => {
    expect(horarioPredeterminado(en('2026-03-10', '10:07'), '2026-03-10', ZONA, 30)).toEqual({ fecha: '2026-03-10', hora: '10:30' });
  });

  it('antes de la jornada propone el inicio de la jornada de hoy', () => {
    expect(horarioPredeterminado(en('2026-03-10', '06:40'), '2026-03-10', ZONA)).toEqual({ fecha: '2026-03-10', hora: '09:00' });
  });

  it('después de hora pasa al inicio de la jornada de mañana (también a fin de mes)', () => {
    expect(horarioPredeterminado(en('2026-03-10', '19:50'), '2026-03-10', ZONA)).toEqual({ fecha: '2026-03-11', hora: '09:00' });
    expect(horarioPredeterminado(en('2026-03-31', '22:00'), '2026-03-31', ZONA)).toEqual({ fecha: '2026-04-01', hora: '09:00' });
  });

  it('si la agenda muestra un día pasado no propone ese día', () => {
    expect(horarioPredeterminado(en('2026-03-10', '11:01'), '2026-03-02', ZONA)).toEqual({ fecha: '2026-03-10', hora: '11:15' });
  });

  it('si la agenda muestra un día futuro propone el inicio de la jornada de ese día', () => {
    expect(horarioPredeterminado(en('2026-03-10', '11:01'), '2026-03-12', ZONA)).toEqual({ fecha: '2026-03-12', hora: '09:00' });
  });
});
