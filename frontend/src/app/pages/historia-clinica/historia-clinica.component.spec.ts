import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AccountMovementDto, AccountStatementDto } from '../../api/schema';
import { pesos } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { fechaDelConsultorio } from '../../core/zona-horaria';
import { pagina, sesionDePrueba } from '../../testing/sesion-de-prueba';
import { HistoriaClinicaComponent } from './historia-clinica.component';

const PACIENTE = { id: 'p1', firstName: 'Paciente', lastName: 'Demo', documentNumber: '30111222', status: 'Active' };
const ENTRADAS = '/api/v1/patients/p1/clinical-record/entries';
const CUENTA = '/api/v1/patients/p1/account';

function movimiento(datos: Partial<AccountMovementDto> & Pick<AccountMovementDto, 'id' | 'type' | 'direction' | 'amount' | 'runningBalance'>): AccountMovementDto {
  return { occurredAtUtc: '2026-11-20T15:00:00Z', description: 'Movimiento de prueba', ...datos };
}
function estadoDeCuenta(balance: number, movements: AccountMovementDto[] = []): AccountStatementDto {
  return { patientId: 'p1', totalCharges: balance > 0 ? balance : 0, totalCredits: 0, balance, movements, budgets: [] };
}

describe('HistoriaClinicaComponent (OnPush)', () => {
  let backend: HttpTestingController;
  let harness: RouterTestingHarness;

  const raiz = () => harness.routeNativeElement as HTMLElement;
  const texto = () => raiz().textContent ?? '';
  const boton = (titulo: string) => [...raiz().querySelectorAll('button')].find(b => b.textContent?.trim() === titulo) as HTMLButtonElement | undefined;
  const escribir = (selector: string, valor: string) => {
    const campo = raiz().querySelector(selector) as HTMLInputElement;
    campo.value = valor;
    campo.dispatchEvent(new Event('input'));
  };

  function iniciar(permisos: string[], profesional = false): void {
    TestBed.inject(AuthService).sesion.set(sesionDePrueba(permisos, { profesional }));
  }

  /** Navega a la ficha y responde paciente, historia y registros (y la cuenta, si se indica). */
  async function abrir(entradas: unknown[] = [], cuenta?: AccountStatementDto): Promise<void> {
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/historia-clinica/p1', HistoriaClinicaComponent);
    backend.expectOne('/api/v1/patients/p1').flush(PACIENTE);
    backend.expectOne('/api/v1/patients/p1/clinical-record').flush({
      alerts: [{ title: 'Alergia a la penicilina', severity: 'Critical' }],
      medicalHistory: { diabetes: true, allergies: 'Penicilina' }
    });
    backend.expectOne(r => r.url === ENTRADAS && r.method === 'GET').flush(pagina(entradas));
    if (cuenta) backend.expectOne(r => r.url === CUENTA && r.method === 'GET').flush(cuenta);
    harness.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([{ path: 'historia-clinica/:id', component: HistoriaClinicaComponent }])]
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('muestra la ficha, los antecedentes y los registros al llegar las respuestas', async () => {
    iniciar(['clinical.read']);
    await abrir([{ id: 'e1', type: 'Consultation', title: 'Control anual', occurredAtUtc: '2026-03-02T02:30:00Z', professionalName: 'Od. Demo', isVoided: false, teeth: [] }]);

    expect(texto()).toContain('Paciente Demo');
    expect(texto()).toContain('Alergia a la penicilina');
    expect(texto()).toContain('Diabetes');
    expect(texto()).toContain('Control anual');
    // 02:30 UTC del 2 de marzo es el 1 de marzo a las 23:30 en el consultorio.
    expect(texto()).toContain('Domingo 1 de marzo de 2026');
    expect(texto()).toContain('23:30');
  });

  it('agrupa los registros por día del consultorio: el día más reciente primero y, dentro del día, el registro más reciente primero', async () => {
    iniciar(['clinical.read']);
    const entrada = (id: string, title: string, occurredAtUtc: string) => ({ id, type: 'Consultation', title, occurredAtUtc, professionalName: 'Od. Demo', isVoided: false, teeth: [] });
    await abrir([
      entrada('e1', 'Mañana del lunes', '2026-11-23T12:00:00Z'),
      entrada('e2', 'Martes temprano', '2026-11-24T11:00:00Z'),
      entrada('e3', 'Tarde del lunes', '2026-11-23T20:00:00Z'),
      entrada('e4', 'Martes tarde', '2026-11-24T19:00:00Z')
    ]);

    const dias = [...raiz().querySelectorAll('.record-day')];
    expect(dias.map(dia => dia.querySelector('.day-heading')?.textContent?.trim())).toEqual(['Martes 24 de noviembre de 2026', 'Lunes 23 de noviembre de 2026']);
    expect([...dias[0].querySelectorAll('.record-meta strong')].map(t => t.textContent)).toEqual(['Martes tarde', 'Martes temprano']);
    expect([...dias[1].querySelectorAll('.record-meta strong')].map(t => t.textContent)).toEqual(['Tarde del lunes', 'Mañana del lunes']);
  });

  it('envía occurredAt: un día anterior se registra al mediodía de ese día en la zona del consultorio', async () => {
    iniciar(['clinical.read', 'clinical.write'], true);
    await abrir();

    boton('＋ Nueva consulta')!.click();
    harness.detectChanges();
    const fecha = raiz().querySelector('input[type=date]') as HTMLInputElement;
    expect(fecha.value).toBe(fechaDelConsultorio());
    expect(fecha.max).toBe(fechaDelConsultorio());

    escribir('input[type=date]', '2026-01-10');
    escribir('input[formControlName=motivo]', 'Control de rutina');
    raiz().querySelector('form')!.dispatchEvent(new Event('submit'));

    const alta = backend.expectOne(r => r.url === ENTRADAS && r.method === 'POST');
    expect(alta.request.body.occurredAt).toBe('2026-01-10T15:00:00.000Z');
    expect(alta.request.body.title).toBe('Control de rutina');
    alta.flush({});
    backend.expectOne(r => r.url === ENTRADAS && r.method === 'GET').flush(pagina([]));
  });

  it('para el día de hoy envía el momento actual', async () => {
    iniciar(['clinical.read', 'clinical.write'], true);
    await abrir();

    boton('＋ Nueva consulta')!.click();
    harness.detectChanges();
    escribir('input[formControlName=motivo]', 'Urgencia');
    const antes = Date.now();
    raiz().querySelector('form')!.dispatchEvent(new Event('submit'));

    const alta = backend.expectOne(r => r.url === ENTRADAS && r.method === 'POST');
    const enviado = Date.parse(alta.request.body.occurredAt);
    expect(enviado).toBeGreaterThanOrEqual(antes - 1000);
    expect(enviado).toBeLessThanOrEqual(Date.now() + 1000);
    alta.flush({});
    backend.expectOne(r => r.url === ENTRADAS && r.method === 'GET').flush(pagina([]));
  });

  it('sin finance.read no pide la cuenta ni muestra Pagos y saldo', async () => {
    iniciar(['clinical.read']);
    await abrir();

    backend.expectNone(CUENTA);
    expect(texto()).not.toContain('Pagos y saldo');
  });

  it('con finance.read pero sin payments.write ni payments.void solo muestra el saldo', async () => {
    iniciar(['clinical.read', 'finance.read']);
    await abrir([], estadoDeCuenta(1000, [movimiento({ id: 'm1', type: 'ManualCharge', direction: 'Debit', amount: 1000, runningBalance: 1000 })]));

    expect(texto()).toContain('Pagos y saldo');
    expect(texto()).toContain('Saldo pendiente');
    expect(texto()).toContain(pesos(1000));
    expect(boton('Registrar cargo')).toBeUndefined();
    expect(boton('Registrar pago')).toBeUndefined();
    expect(boton('Anular')).toBeUndefined();
  });

  it('muestra "A favor" cuando el saldo es negativo', async () => {
    iniciar(['clinical.read', 'finance.read']);
    await abrir([], estadoDeCuenta(-300));

    expect(raiz().querySelector('.balance')?.textContent).toContain('A favor');
    expect(raiz().querySelector('.balance')?.textContent).toContain(pesos(300));
  });

  it('ofrece Anular solo en cargos manuales vigentes y marca los anulados', async () => {
    iniciar(['clinical.read', 'finance.read', 'payments.void']);
    await abrir([], estadoDeCuenta(1000, [
      movimiento({ id: 'm1', type: 'ManualCharge', direction: 'Debit', amount: 1000, runningBalance: 1000, description: 'Cargo vigente' }),
      movimiento({ id: 'm2', type: 'ManualCharge', direction: 'Debit', amount: 400, runningBalance: 1400, description: 'Cargo anulado', reversedByMovementId: 'm3' }),
      movimiento({ id: 'm3', type: 'ManualChargeReversal', direction: 'Credit', amount: 400, runningBalance: 1000, reversesMovementId: 'm2' }),
      movimiento({ id: 'm4', type: 'BudgetCharge', direction: 'Debit', amount: 500, runningBalance: 1500 })
    ]));

    const anulables = [...raiz().querySelectorAll('button')].filter(b => b.textContent?.trim() === 'Anular');
    expect(anulables.length).toBe(1);
    expect(anulables[0].closest('tr')?.textContent).toContain('Cargo vigente');
    const anulado = raiz().querySelector('tr.voided');
    expect(anulado?.textContent).toContain('Cargo anulado');
    expect(anulado?.textContent).toContain('Anulado');
    expect(texto()).toContain('Cargo manual');
    expect(texto()).toContain('Anulación de cargo');
  });

  it('registrar un cargo sube el saldo con el estado de cuenta que devuelve el backend', async () => {
    iniciar(['clinical.read', 'finance.read', 'payments.write']);
    await abrir([], estadoDeCuenta(1000));

    boton('Registrar cargo')!.click();
    harness.detectChanges();
    escribir('input[formControlName=descripcion]', 'Consulta de urgencia');
    escribir('input[formControlName=importe]', '0');
    raiz().querySelector('.od-modal form')!.dispatchEvent(new Event('submit'));
    harness.detectChanges();
    backend.expectNone(`${CUENTA}/charges`);
    expect(texto()).toContain('Ingresá un monto mayor a cero');

    escribir('input[formControlName=importe]', '500');
    raiz().querySelector('.od-modal form')!.dispatchEvent(new Event('submit'));
    const cargo = backend.expectOne(`${CUENTA}/charges`);
    expect(cargo.request.method).toBe('POST');
    expect(cargo.request.body).toEqual({ description: 'Consulta de urgencia', amount: 500, occurredAt: null });
    cargo.flush(estadoDeCuenta(1500, [movimiento({ id: 'm1', type: 'ManualCharge', direction: 'Debit', amount: 500, runningBalance: 1500 })]));
    harness.detectChanges();

    expect(texto()).toContain('Se registró el cargo.');
    expect(raiz().querySelector('.balance')?.textContent).toContain(pesos(1500));
    expect(raiz().querySelector('.od-modal')).toBeNull();
  });

  it('registrar un pago baja el saldo y lo informa', async () => {
    iniciar(['clinical.read', 'finance.read', 'payments.write']);
    await abrir([], estadoDeCuenta(1500));

    boton('Registrar pago')!.click();
    harness.detectChanges();
    escribir('.od-modal input[formControlName=importe]', '1000');
    raiz().querySelector('.od-modal form')!.dispatchEvent(new Event('submit'));

    const pago = backend.expectOne('/api/v1/payments');
    expect(pago.request.body).toEqual(jasmine.objectContaining({ patientId: 'p1', budgetId: null, amount: 1000, method: 'BankTransfer', paidAt: null }));
    pago.flush({ id: 'pg1', amount: 1000 });
    backend.expectOne(r => r.url === CUENTA && r.method === 'GET').flush(estadoDeCuenta(500));
    harness.detectChanges();

    expect(texto()).toContain(`Se registró el pago. Saldo pendiente: ${pesos(500)}`);
    expect(raiz().querySelector('.balance')?.textContent).toContain(pesos(500));
  });

  it('anular un cargo exige motivo y actualiza el saldo', async () => {
    iniciar(['clinical.read', 'finance.read', 'payments.void']);
    await abrir([], estadoDeCuenta(1000, [movimiento({ id: 'm1', type: 'ManualCharge', direction: 'Debit', amount: 1000, runningBalance: 1000 })]));

    boton('Anular')!.click();
    harness.detectChanges();
    boton('Anular cargo')!.click();
    harness.detectChanges();
    backend.expectNone(`${CUENTA}/charges/m1/void`);
    expect(texto()).toContain('Indicá el motivo de la anulación.');

    escribir('.od-modal input', 'Cargado por error');
    boton('Anular cargo')!.click();
    const anulacion = backend.expectOne(`${CUENTA}/charges/m1/void`);
    expect(anulacion.request.body).toEqual({ reason: 'Cargado por error' });
    anulacion.flush(estadoDeCuenta(0, [
      movimiento({ id: 'm1', type: 'ManualCharge', direction: 'Debit', amount: 1000, runningBalance: 1000, reversedByMovementId: 'm2' }),
      movimiento({ id: 'm2', type: 'ManualChargeReversal', direction: 'Credit', amount: 1000, runningBalance: 0, reversesMovementId: 'm1' })
    ]));
    harness.detectChanges();

    expect(texto()).toContain('Se anuló el cargo.');
    expect(boton('Anular')).toBeUndefined();
  });
});
