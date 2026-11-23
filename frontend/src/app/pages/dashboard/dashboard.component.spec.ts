import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { sesionDePrueba } from '../../testing/sesion-de-prueba';
import { DashboardComponent } from './dashboard.component';

const recepcion = {
  date: '2026-03-02', appointmentsToday: 1, confirmedToday: 1, pendingConfirmationToday: 0, pendingConfirmationNext48h: 0,
  inWaitingRoom: 0, averageWaitMinutes: 0, waitingRoom: [], openTasks: 0, tasksDueToday: 0, overdueTasks: 0, pendingTasks: [],
  upcomingLabDeliveries: [],
  todayAgenda: [{ id: 'a1', startUtc: '2026-03-02T13:00:00Z', endUtc: '2026-03-02T13:30:00Z', patientId: 'p1', patientName: 'Paciente Demo', professional: 'Od. Demo', status: 'Confirmed' }]
};

describe('DashboardComponent (OnPush)', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    backend = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).sesion.set(sesionDePrueba(['dashboard.reception']));
  });

  afterEach(() => backend.verify());

  it('muestra la agenda de hoy al llegar la respuesta, con la hora del consultorio', () => {
    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
    const texto = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto()).toContain('Cargando el resumen del día');

    backend.expectOne('/api/v1/dashboard/reception').flush(recepcion);
    fixture.detectChanges();

    expect(texto()).not.toContain('Cargando el resumen del día');
    expect(texto()).toContain('Paciente Demo');
    // 13:00 UTC = 10:00 en Buenos Aires (zona de la membresía activa).
    expect((fixture.nativeElement as HTMLElement).querySelector('.appointment-time')?.textContent?.trim()).toBe('10:00');
  });

  it('muestra el error si falla la carga', () => {
    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges();
    backend.expectOne('/api/v1/dashboard/reception').flush(null, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent).toContain('No hay conexión con el servidor');
  });
});
