import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { LabWorkDto } from '../../api/schema';
import { pagina, sesionDePrueba } from '../../testing/sesion-de-prueba';
import { AuthService } from '../../core/auth.service';
import { LaboratorioComponent, piezasFdi, validarPiezas } from './laboratorio.component';

describe('piezas dentales (FDI) del trabajo de laboratorio', () => {
  it('acepta piezas permanentes y temporales, sin repetir', () => {
    expect(piezasFdi('11, 21; 85 11')).toEqual([11, 21, 85]);
    expect(piezasFdi('')).toEqual([]);
  });

  it('rechaza números que no son piezas FDI', () => {
    for (const texto of ['19', '56', '91', '1', '111', 'a1']) expect(piezasFdi(texto)).withContext(texto).toBeNull();
  });

  it('el validador marca el control como inválido', () => {
    expect(validarPiezas(new FormControl('36', { nonNullable: true }))).toBeNull();
    expect(validarPiezas(new FormControl('36, 99', { nonNullable: true }))).toEqual({ piezas: true });
  });
});

function trabajo(id: string, toothCodes: number[]): LabWorkDto {
  return {
    id, patientId: 'p1', patientName: 'Paciente de prueba', laboratoryId: 'l1', laboratoryName: 'Laboratorio Demo',
    professionalUserId: 'u1', professionalName: 'Od. Demo', workType: 'Corona', status: 'InProduction', isOverdue: false,
    createdAtUtc: '2026-03-01T12:00:00Z', toothCodes
  };
}

describe('LaboratorioComponent (render)', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    backend = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).sesion.set(sesionDePrueba(['lab.read']));
  });

  afterEach(() => backend.verify());

  it('muestra los trabajos con sus piezas y el total en singular', async () => {
    const fixture = TestBed.createComponent(LaboratorioComponent);
    fixture.autoDetectChanges();
    backend.expectOne(r => r.url === '/api/v1/lab-works').flush({ items: [trabajo('w1', [11, 21])], page: 1, pageSize: 50, totalCount: 1, totalPages: 1, hasNext: false });
    backend.expectOne('/api/v1/lab-works/board').flush({ overdue: 0, dueThisWeek: 0, countsByStatus: {} });
    backend.expectOne('/api/v1/laboratories').flush([]);
    await fixture.whenStable();

    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto).toContain('1 resultado');
    expect(texto).not.toContain('1 resultados');
    expect(texto).toContain('Piezas 11, 21');
    // Sin permiso lab.write no se ofrece el alta.
    expect(texto).not.toContain('Nuevo trabajo');
  });

  it('OnPush: la vista se actualiza al llegar la respuesta (sin detección automática)', () => {
    const fixture = TestBed.createComponent(LaboratorioComponent);
    fixture.detectChanges();
    const texto = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto()).not.toContain('Pieza 36');
    backend.expectOne(r => r.url === '/api/v1/lab-works').flush(pagina([trabajo('w1', [36])]));
    backend.expectOne('/api/v1/lab-works/board').flush({ overdue: 2, dueThisWeek: 0, countsByStatus: {} });
    backend.expectOne('/api/v1/laboratories').flush([]);
    fixture.detectChanges();
    expect(texto()).toContain('Pieza 36');
    expect((fixture.nativeElement as HTMLElement).querySelector('.stat-card.warning strong')?.textContent).toBe('2');
  });

  it('si el backend rechaza el cambio de estado, el desplegable vuelve a mostrar el estado real', async () => {
    TestBed.inject(AuthService).sesion.set(sesionDePrueba(['lab.read', 'lab.track']));
    const fixture = TestBed.createComponent(LaboratorioComponent);
    fixture.detectChanges();
    backend.expectOne(r => r.url === '/api/v1/lab-works').flush(pagina([trabajo('w1', [11])]));
    backend.expectOne('/api/v1/lab-works/board').flush({ overdue: 0, dueThisWeek: 0, countsByStatus: {} });
    backend.expectOne('/api/v1/laboratories').flush([]);
    fixture.detectChanges();
    await fixture.whenStable();

    const selector = (fixture.nativeElement as HTMLElement).querySelector<HTMLSelectElement>('select.status-select')!;
    expect(selector.value).toBe('InProduction');
    selector.value = 'Placed';
    selector.dispatchEvent(new Event('change'));
    backend.expectOne('/api/v1/lab-works/w1/status').flush(
      { type: 'x', title: 'Conflicto', status: 409, code: 'concurrency_conflict', detail: 'Otra persona modificó el trabajo.', traceId: 't' },
      { status: 409, statusText: 'Conflict' });
    backend.expectOne(r => r.url === '/api/v1/lab-works').flush(pagina([trabajo('w1', [11])]));
    backend.expectOne('/api/v1/lab-works/board').flush({ overdue: 0, dueThisWeek: 0, countsByStatus: {} });
    fixture.detectChanges();
    await fixture.whenStable();

    expect(selector.value).toBe('InProduction');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Otra persona modificó el trabajo.');
  });
});
