import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { pagina, sesionDePrueba } from '../../testing/sesion-de-prueba';
import { TareasComponent } from './tareas.component';

const tarea = { id: 't1', title: 'Llamar al laboratorio', priority: 'High', category: 'General', status: 'Pending', isOverdue: false, dueAtUtc: '2026-03-02T02:59:00Z' };

describe('TareasComponent (OnPush)', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    backend = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).sesion.set(sesionDePrueba(['tasks.read', 'appointments.read']));
  });

  afterEach(() => backend.verify());

  it('muestra las tareas y pide los turnos desde hoy en la zona del consultorio', () => {
    const fixture = TestBed.createComponent(TareasComponent);
    fixture.detectChanges();
    const texto = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto()).not.toContain('Llamar al laboratorio');

    backend.expectOne(r => r.url === '/api/v1/tasks').flush(pagina([tarea]));
    const turnos = backend.expectOne(r => r.url === '/api/v1/appointments');
    // Medianoche del consultorio (UTC-3) = 03:00 UTC.
    expect(turnos.request.params.get('From')).toMatch(/T03:00:00\.000Z$/);
    turnos.flush(pagina([]));
    fixture.detectChanges();

    expect(texto()).toContain('Llamar al laboratorio');
  });
});
