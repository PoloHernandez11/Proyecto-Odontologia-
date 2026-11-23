import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { sesionDePrueba } from '../../testing/sesion-de-prueba';
import { ConfiguracionComponent } from './configuracion.component';

const perfil = {
  userId: 'u1', email: 'demo@example.test', emailConfirmed: true, firstName: 'Ana', lastName: 'Prueba', displayName: 'Ana Prueba',
  twoFactorEnabled: false,
  notificationSettings: { emailEnabled: true, inAppEnabled: true, notifyTaskAssigned: true, notifyTaskDue: true, notifyLabUpdates: true, notifyAppointmentChanges: true, dailyAgendaSummary: false }
};

describe('ConfiguracionComponent (OnPush)', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    backend = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).sesion.set(sesionDePrueba([]));
  });

  afterEach(() => backend.verify());

  it('muestra el perfil cuando llega la respuesta', () => {
    const fixture = TestBed.createComponent(ConfiguracionComponent);
    fixture.detectChanges();
    const texto = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto()).not.toContain('demo@example.test');

    backend.expectOne('/api/v1/profile').flush(perfil);
    fixture.detectChanges();

    expect(texto()).toContain('Ana Prueba');
    expect(texto()).toContain('demo@example.test');
  });

  it('si el backend rechaza el cambio, muestra su detalle (p. ej. 422)', () => {
    const fixture = TestBed.createComponent(ConfiguracionComponent);
    fixture.detectChanges();
    backend.expectOne('/api/v1/profile').flush(perfil);
    fixture.detectChanges();

    fixture.componentInstance.guardarDatos();
    backend.expectOne(r => r.method === 'PUT' && r.url === '/api/v1/profile').flush(
      { type: 'x', title: 'Regla de negocio', status: 422, code: 'business_rule', detail: 'No se puede guardar el perfil.', traceId: 't' },
      { status: 422, statusText: 'Unprocessable Entity' });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No se puede guardar el perfil.');
  });
});
