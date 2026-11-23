import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AuthService } from '../../core/auth.service';
import { pagina, sesionDePrueba } from '../../testing/sesion-de-prueba';
import { HistoriaClinicaComponent } from './historia-clinica.component';

describe('HistoriaClinicaComponent (OnPush)', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([{ path: 'historia-clinica/:id', component: HistoriaClinicaComponent }])]
    });
    backend = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).sesion.set(sesionDePrueba(['clinical.read']));
  });

  afterEach(() => backend.verify());

  it('muestra la ficha, los antecedentes y los registros al llegar las respuestas', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/historia-clinica/p1', HistoriaClinicaComponent);

    backend.expectOne('/api/v1/patients/p1').flush({ id: 'p1', firstName: 'Paciente', lastName: 'Demo', documentNumber: '30111222', status: 'Active' });
    backend.expectOne('/api/v1/patients/p1/clinical-record').flush({
      alerts: [{ title: 'Alergia a la penicilina', severity: 'Critical' }],
      medicalHistory: { diabetes: true, allergies: 'Penicilina' }
    });
    backend.expectOne(r => r.url === '/api/v1/patients/p1/clinical-record/entries').flush(pagina([
      { id: 'e1', type: 'Consultation', title: 'Control anual', occurredAtUtc: '2026-03-02T02:30:00Z', professionalName: 'Od. Demo', isVoided: false, teeth: [] }
    ]));
    harness.detectChanges();

    const texto = harness.routeNativeElement?.textContent ?? '';
    expect(texto).toContain('Paciente Demo');
    expect(texto).toContain('Alergia a la penicilina');
    expect(texto).toContain('Diabetes');
    expect(texto).toContain('Control anual');
    expect(texto).toContain('01/03/2026');
  });
});
