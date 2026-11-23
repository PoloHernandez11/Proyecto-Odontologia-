import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AceptarInvitacionComponent, ConfirmarCorreoComponent } from './cuenta.components';

describe('Pantallas de cuenta (OnPush)', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        provideRouter([
          { path: 'confirmar-correo', component: ConfirmarCorreoComponent },
          { path: 'aceptar-invitacion', component: AceptarInvitacionComponent },
          { path: 'login', children: [] }
        ])
      ]
    });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('confirmar correo: pasa de "confirmando" al aviso de éxito cuando responde la API', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/confirmar-correo?userId=u1&token=t1', ConfirmarCorreoComponent);
    const texto = () => harness.routeNativeElement?.textContent ?? '';
    expect(texto()).toContain('Estamos confirmando tu correo');

    backend.expectOne('/api/v1/auth/confirm-email').flush({ message: 'ok' });
    harness.detectChanges();
    expect(texto()).not.toContain('Estamos confirmando tu correo');
    expect(texto()).toContain('Tu correo quedó confirmado');
  });

  it('aceptar invitación: muestra los datos de la invitación al llegar', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/aceptar-invitacion?token=abc', AceptarInvitacionComponent);
    expect(harness.routeNativeElement?.textContent).toContain('Buscando tu invitación');

    backend.expectOne('/api/v1/auth/invitations/abc').flush({ consultorioName: 'Consultorio Demo', roles: ['Recepción'], email: 'nuevo@example.test', userExists: false });
    harness.detectChanges();
    const texto = harness.routeNativeElement?.textContent ?? '';
    expect(texto).not.toContain('Buscando tu invitación');
    expect(texto).toContain('Consultorio Demo');
  });
});
