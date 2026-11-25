import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AuthService } from '../../core/auth.service';
import { fechaEnZona, instanteEnZona } from '../../core/zona-horaria';
import { ZONA_DE_PRUEBA, sesionDePrueba } from '../../testing/sesion-de-prueba';
import { AgendaComponent } from './agenda.component';

const hoy = fechaEnZona(new Date(), ZONA_DE_PRUEBA);
function turno(id: string, paciente: string, hora: string, status: string, reason = 'Control general') {
  const inicio = instanteEnZona(hoy, hora, ZONA_DE_PRUEBA);
  return {
    id, startUtc: inicio.toISOString(), endUtc: new Date(inicio.getTime() + 30 * 60_000).toISOString(), durationMinutes: 30, status,
    patientId: `pac-${id}`, patientName: paciente, patientPhone: null, professionalUserId: 'pr1', professionalName: 'Od. Demo',
    gabineteId: null, gabineteName: null, reason, isOverbooking: false, labWorks: []
  };
}
const turnos = [
  turno('a1', 'Paciente Pendiente', '10:00', 'Pending'),
  turno('a2', 'Paciente Eliminado', '11:00', 'Cancelled'),
  turno('a3', 'Paciente Ausente', '12:00', 'NoShow')
];

describe('AgendaComponent (OnPush)', () => {
  let backend: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([{ path: 'agenda', component: AgendaComponent }])]
    });
    backend = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).sesion.set(sesionDePrueba(['appointments.read', 'appointments.write', 'agenda.all_professionals']));
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/agenda', AgendaComponent);
    backend.expectOne('/api/v1/consultorio/professionals').flush([{ userId: 'pr1', displayName: 'Od. Demo', specialty: null, defaultAppointmentMinutes: 30 }]);
    backend.expectOne('/api/v1/consultorio/gabinetes').flush([]);
    responderAgenda(turnos);
  });

  afterEach(() => backend.verify());

  function responderAgenda(lista: unknown[]): void {
    backend.expectOne(r => r.url === '/api/v1/agenda').flush({ from: hoy, to: hoy, timeZoneId: ZONA_DE_PRUEBA, appointments: lista, blocks: [], workingHours: [] });
    harness.detectChanges();
  }
  const texto = () => harness.routeNativeElement!.textContent ?? '';
  const boton = (contenido: string) =>
    Array.from(harness.routeNativeElement!.querySelectorAll('button')).find(b => b.textContent?.trim() === contenido);
  function seleccionar(paciente: string): void {
    Array.from(harness.routeNativeElement!.querySelectorAll<HTMLButtonElement>('button.slot')).find(b => b.textContent?.includes(paciente))!.click();
    harness.detectChanges();
  }

  it('no muestra los turnos eliminados en la grilla, el resumen ni la leyenda', () => {
    expect(texto()).toContain('Paciente Pendiente');
    expect(texto()).toContain('Paciente Ausente');
    expect(texto()).not.toContain('Paciente Eliminado');
    expect(texto()).not.toContain('Cancelados');
    expect(texto()).not.toContain('Cancelado');
    expect(texto()).toContain('● Ausente');
    const agendados = Array.from(harness.routeNativeElement!.querySelectorAll('.summary p')).find(p => p.textContent?.includes('Turnos agendados'));
    expect(agendados?.querySelector('strong')?.textContent).toBe('2');
  });

  it('en la vista de semana tampoco lista los turnos eliminados', () => {
    boton('Semana')!.click();
    responderAgenda(turnos);
    expect(harness.routeNativeElement!.querySelectorAll('.list-appointment').length).toBe(2);
    expect(texto()).not.toContain('Paciente Eliminado');
  });

  it('elimina el turno con motivo opcional y lo informa', () => {
    seleccionar('Paciente Pendiente');
    boton('× Eliminar turno')!.click();
    harness.detectChanges();
    expect(texto()).toContain('Motivo (opcional)');
    boton('Eliminar turno')!.click();
    const pedido = backend.expectOne('/api/v1/appointments/a1/cancel');
    expect(pedido.request.body).toEqual({ reason: null });
    pedido.flush({ ...turnos[0], status: 'Cancelled' });
    responderAgenda([{ ...turnos[0], status: 'Cancelled' }, turnos[2]]);
    expect(texto()).toContain('Se eliminó el turno de Paciente Pendiente.');
    expect(harness.routeNativeElement!.querySelectorAll('button.slot').length).toBe(1);
  });

  it('no ofrece editar el motivo de un turno que no está pendiente ni confirmado', () => {
    seleccionar('Paciente Ausente');
    expect(boton('Editar motivo')).toBeUndefined();
  });

  describe('edición del motivo', () => {
    function abrirEdicion(): HTMLInputElement {
      seleccionar('Paciente Pendiente');
      boton('Editar motivo')!.click();
      backend.expectOne('/api/v1/appointments/a1').flush({ ...turnos[0], administrativeNotes: 'Trae estudios', version: 'v7' });
      harness.detectChanges();
      return harness.routeNativeElement!.querySelector<HTMLInputElement>('#motivo-turno')!;
    }
    function escribir(input: HTMLInputElement, valor: string): void {
      input.value = valor;
      input.dispatchEvent(new Event('input'));
      harness.detectChanges();
    }

    it('guarda el motivo nuevo con PUT conservando paciente, notas y versión', () => {
      const input = abrirEdicion();
      expect(input.value).toBe('Control general');
      escribir(input, '  Dolor en molar  ');
      boton('Guardar')!.click();
      const pedido = backend.expectOne(r => r.method === 'PUT' && r.url === '/api/v1/appointments/a1');
      expect(pedido.request.body).toEqual({ patientId: 'pac-a1', reason: 'Dolor en molar', administrativeNotes: 'Trae estudios', version: 'v7' });
      pedido.flush({ ...turnos[0], reason: 'Dolor en molar' });
      responderAgenda([{ ...turnos[0], reason: 'Dolor en molar' }, turnos[2]]);
      expect(texto()).toContain('Se actualizó el motivo del turno.');
      expect(harness.routeNativeElement!.querySelector('#motivo-turno')).toBeNull();
      expect(texto()).toContain('Dolor en molar');
    });

    it('exige el motivo y no envía nada si queda vacío', () => {
      const input = abrirEdicion();
      escribir(input, '   ');
      boton('Guardar')!.click();
      harness.detectChanges();
      expect(texto()).toContain('Indicá el motivo de consulta');
      backend.expectNone(r => r.method === 'PUT');
    });

    it('ante un conflicto de concurrencia muestra el aviso y recarga la agenda', () => {
      const input = abrirEdicion();
      escribir(input, 'Otro motivo');
      boton('Guardar')!.click();
      backend.expectOne(r => r.method === 'PUT').flush(
        { code: 'concurrency_conflict', detail: 'Otra persona modificó el turno. Se cargaron los datos vigentes.' },
        { status: 409, statusText: 'Conflict' }
      );
      responderAgenda(turnos);
      expect(texto()).toContain('Otra persona modificó el turno.');
      expect(harness.routeNativeElement!.querySelector('#motivo-turno')).toBeNull();
    });

    it('"Cancelar" cierra la edición sin guardar', () => {
      abrirEdicion();
      boton('Cancelar')!.click();
      harness.detectChanges();
      expect(harness.routeNativeElement!.querySelector('#motivo-turno')).toBeNull();
      expect(boton('Editar motivo')).toBeDefined();
    });
  });
});
