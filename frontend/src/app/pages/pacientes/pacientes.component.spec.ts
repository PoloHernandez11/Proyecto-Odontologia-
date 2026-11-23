import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { pagina, sesionDePrueba } from '../../testing/sesion-de-prueba';
import { PacientesComponent } from './pacientes.component';

const paciente = { id: 'p1', firstName: 'Paciente', lastName: 'Demo', documentNumber: '30111222', phone: '1155550000', status: 'Active', healthInsurance: null };

describe('PacientesComponent (OnPush)', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    backend = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).sesion.set(sesionDePrueba(['patients.read']));
  });

  afterEach(() => backend.verify());

  it('lista los pacientes y abre la ficha del primero cuando llegan las respuestas', fakeAsync(() => {
    const fixture = TestBed.createComponent(PacientesComponent);
    fixture.detectChanges();
    const texto = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
    backend.expectOne('/api/v1/health-insurances').flush([]);
    expect(texto()).toContain('Cargando…');

    tick(250); // búsqueda con debounce
    backend.expectOne(r => r.url === '/api/v1/patients').flush({ ...pagina([paciente]), totalCount: 1 });
    fixture.detectChanges();
    expect(texto()).toContain('1 paciente ·');
    expect(texto()).toContain('1 resultado');
    expect(texto()).toContain('30111222');

    backend.expectOne('/api/v1/patients/p1/summary').flush({
      patient: { ...paciente, email: 'paciente@example.test', alerts: [], age: 40 },
      lastVisitUtc: '2026-03-02T02:30:00Z', upcomingAppointments: [], account: { balance: 0 }
    });
    fixture.detectChanges();
    expect(texto()).toContain('paciente@example.test');
    // 02:30 UTC del 2 de marzo es todavía 1 de marzo en Buenos Aires.
    expect(texto()).toContain('01/03/2026');
  }));
});
