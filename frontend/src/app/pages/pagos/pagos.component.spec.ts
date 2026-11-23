import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { pagina, sesionDePrueba } from '../../testing/sesion-de-prueba';
import { PagosComponent } from './pagos.component';

const presupuesto = {
  id: 'b1', number: 7, patientId: 'p1', patientName: 'Paciente Demo', issueDate: '2026-03-01', expirationDate: '2026-04-01',
  status: 'Approved', subtotal: 1000, discountPercent: 0, discountAmount: 0, total: 1000, paid: 0, outstanding: 1000,
  items: [{ description: 'Limpieza' }], createdAtUtc: '2026-03-01T12:00:00Z'
};

describe('PagosComponent (OnPush)', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    backend = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).sesion.set(sesionDePrueba(['finance.read']));
  });

  afterEach(() => backend.verify());

  it('muestra los conceptos de cobro y el saldo cuando llegan las respuestas', () => {
    const fixture = TestBed.createComponent(PagosComponent);
    fixture.detectChanges();
    const texto = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto()).not.toContain('Limpieza');

    backend.expectOne(r => r.url === '/api/v1/budgets').flush(pagina([presupuesto]));
    backend.expectOne(r => r.url === '/api/v1/payments').flush(pagina([]));
    fixture.detectChanges();

    expect(texto()).toContain('Paciente Demo');
    expect(texto()).toContain('Limpieza · N.º 7');
    expect(texto()).toContain('Pendiente');
    expect(texto()).toContain('01/03/2026');
  });

  it('429 al cargar: muestra cuánto esperar', () => {
    const fixture = TestBed.createComponent(PagosComponent);
    fixture.detectChanges();
    backend.expectOne(r => r.url === '/api/v1/budgets').flush(null, { status: 429, statusText: 'Too Many Requests', headers: { 'Retry-After': '20' } });
    backend.expectOne(r => r.url === '/api/v1/payments').flush(pagina([]));
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent).toContain('Esperá 20 segundos');
  });
});
