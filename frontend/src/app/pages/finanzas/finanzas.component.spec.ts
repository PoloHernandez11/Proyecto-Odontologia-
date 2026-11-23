import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { pagina, sesionDePrueba } from '../../testing/sesion-de-prueba';
import { FinanzasComponent } from './finanzas.component';

describe('FinanzasComponent (OnPush)', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    backend = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).sesion.set(sesionDePrueba(['finance.read']));
  });

  afterEach(() => backend.verify());

  it('pide el período en la zona del consultorio y muestra el resumen al llegar', () => {
    const fixture = TestBed.createComponent(FinanzasComponent);
    fixture.detectChanges();
    const resumen = backend.expectOne(r => r.url === '/api/v1/finance/summary');
    // Buenos Aires es UTC-3: el mes empieza a las 03:00 UTC del día 1.
    expect(resumen.request.params.get('From')).toMatch(/^\d{4}-\d{2}-01T03:00:00\.000Z$/);
    resumen.flush({ income: 1500, expenses: 500, net: 1000, paymentCount: 1, expenseCount: 1, expensesByCategory: [{ category: 'Supplies', total: 500 }] });
    backend.expectOne(r => r.url === '/api/v1/payments').flush(pagina([]));
    backend.expectOne(r => r.url === '/api/v1/expenses').flush(pagina([]));
    backend.expectOne(r => r.url === '/api/v1/budgets').flush(pagina([]));
    fixture.detectChanges();

    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto).toContain('1 cobro a pacientes');
    expect(texto).toContain('1 egreso registrado');
    expect(texto).toContain('Insumos');
  });
});
