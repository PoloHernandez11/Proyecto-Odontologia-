import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AuthService } from '../../core/auth.service';
import { sesionDePrueba } from '../../testing/sesion-de-prueba';
import { OdontogramaComponent } from './odontograma.component';

const paciente = { id: 'p1', firstName: 'Paciente', lastName: 'Demo', documentNumber: '30111222', status: 'Active', alerts: [] };

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
});
