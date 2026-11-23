import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AuthService } from '../../core/auth.service';
import { pagina, sesionDePrueba } from '../../testing/sesion-de-prueba';
import { ElegirPacienteComponent } from './elegir-paciente.component';

describe('ElegirPacienteComponent (OnPush)', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        provideRouter([{ path: 'odontograma', component: ElegirPacienteComponent, data: { titulo: 'Odontograma', destino: '/odontograma' } }])
      ]
    });
    backend = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).sesion.set(sesionDePrueba(['odontogram.read']));
  });

  afterEach(() => backend.verify());

  it('muestra los resultados de la búsqueda al llegar la respuesta', fakeAsync(() => {
    let harness!: RouterTestingHarness;
    RouterTestingHarness.create('/odontograma').then(h => (harness = h));
    tick();
    const texto = () => harness.routeNativeElement?.textContent ?? '';
    expect(texto()).toContain('Buscando…');

    tick(200);
    backend.expectOne(r => r.url === '/api/v1/patients').flush({
      ...pagina([{ id: 'p1', firstName: 'Paciente', lastName: 'Demo', documentNumber: '30111222', status: 'Active', lastVisitUtc: '2026-03-02T02:30:00Z' }]),
      totalCount: 1
    });
    harness.detectChanges();

    expect(texto()).not.toContain('Buscando…');
    expect(texto()).toContain('Paciente Demo');
    expect(texto()).toContain('Última visita: 01/03/2026');
  }));
});
