import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AuthService } from '../../core/auth.service';
import { sesionDePrueba } from '../../testing/sesion-de-prueba';
import { OdontogramaComponent } from './odontograma.component';

const paciente = { id: 'p1', firstName: 'Paciente', lastName: 'Demo', documentNumber: '30111222', status: 'Active', alerts: [] };
const catalogo = {
  teeth: [{ code: 11, name: 'Incisivo central superior derecho', surfaces: ['Mesial', 'Distal', 'Vestibular', 'Palatal', 'Incisal'] }],
  conditions: [{ id: 'c-caries', code: 'CARIES', name: 'Caries', scope: 'Surface', isActive: true, color: '#d62828' }]
};
const odontograma = {
  patientId: 'p1', lastChangeUtc: null, lastChangeBy: null,
  teeth: [{
    toothCode: 11, notes: [],
    findings: [{ id: 'f1', toothCode: 11, surface: 'Mesial', conditionId: 'c-caries', conditionCode: 'CARIES', conditionName: 'Caries', status: 'Existing', recordedAtUtc: '2026-03-02T12:00:00Z', recordedBy: 'Od. Demo' }]
  }]
};

describe('OdontogramaComponent (OnPush)', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([{ path: 'odontograma/:id', component: OdontogramaComponent }])]
    });
    backend = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).sesion.set(sesionDePrueba(['odontogram.read']));
  });

  afterEach(() => backend.verify());

  async function abrir(): Promise<RouterTestingHarness> {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/odontograma/p1', OdontogramaComponent);
    backend.expectOne('/api/v1/patients/p1').flush(paciente);
    backend.expectOne('/api/v1/odontogram/catalog').flush(catalogo);
    backend.expectOne('/api/v1/patients/p1/odontogram').flush(odontograma);
    harness.detectChanges();
    return harness;
  }

  function boton(harness: RouterTestingHarness, texto: string): HTMLButtonElement | undefined {
    return Array.from(harness.routeNativeElement!.querySelectorAll('button')).find(b => b.textContent?.trim() === texto);
  }

  function elegirPieza11(harness: RouterTestingHarness): void {
    harness.routeNativeElement!.querySelector<HTMLButtonElement>('button.tooth[aria-label^="Pieza 11"]')!.click();
    harness.detectChanges();
  }

  it('dibuja el odontograma del paciente cuando llegan las tres respuestas', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/odontograma/p1', OdontogramaComponent);
    expect(harness.routeNativeElement?.textContent).not.toContain('Paciente Demo');

    backend.expectOne('/api/v1/patients/p1').flush(paciente);
    backend.expectOne('/api/v1/odontogram/catalog').flush({ teeth: [], conditions: [] });
    backend.expectOne('/api/v1/patients/p1/odontogram').flush({ patientId: 'p1', teeth: [], lastChangeUtc: '2026-03-02T02:30:00Z', lastChangeBy: 'Od. Demo' });
    harness.detectChanges();

    const texto = harness.routeNativeElement?.textContent ?? '';
    expect(texto).toContain('Paciente Demo');
    expect(texto).toContain('Última modificación: 01/03/2026 · Od. Demo');
  });

  it('sin permiso de escritura no ofrece "Editar odontograma" ni controles de edición', async () => {
    const harness = await abrir();
    expect(boton(harness, 'Editar odontograma')).toBeUndefined();
    elegirPieza11(harness);
    const texto = harness.routeNativeElement!.textContent ?? '';
    expect(texto).toContain('Caries');
    expect(texto).not.toContain('Registrar un hallazgo');
    expect(boton(harness, 'Quitar')).toBeUndefined();
  });

  it('con permiso de escritura abre en modo lectura y edita sólo después de "Editar odontograma"', async () => {
    TestBed.inject(AuthService).sesion.set(sesionDePrueba(['odontogram.read', 'odontogram.write'], { profesional: true }));
    const harness = await abrir();
    expect(harness.routeNativeElement!.textContent).toContain('Elegí una pieza para ver su estado.');

    elegirPieza11(harness);
    let texto = harness.routeNativeElement!.textContent ?? '';
    expect(texto).not.toContain('Registrar un hallazgo');
    expect(boton(harness, 'Quitar')).toBeUndefined();
    expect(harness.routeNativeElement!.querySelector('input[aria-label="Nueva observación"]')).toBeNull();

    const editar = boton(harness, 'Editar odontograma')!;
    expect(editar.type).toBe('button');
    editar.click();
    harness.detectChanges();
    texto = harness.routeNativeElement!.textContent ?? '';
    expect(texto).toContain('Registrar un hallazgo');
    expect(boton(harness, 'Quitar')).toBeDefined();
    expect(harness.routeNativeElement!.querySelector('input[aria-label="Nueva observación"]')).not.toBeNull();

    boton(harness, 'Terminar edición')!.click();
    harness.detectChanges();
    expect(harness.routeNativeElement!.textContent).not.toContain('Registrar un hallazgo');
    expect(boton(harness, 'Quitar')).toBeUndefined();
    expect(boton(harness, 'Editar odontograma')).toBeDefined();
  });
});
