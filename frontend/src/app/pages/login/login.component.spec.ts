import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AuthService } from '../../core/auth.service';
import { sesionDePrueba } from '../../testing/sesion-de-prueba';
import { LoginComponent } from './login.component';

const NO_AUTORIZADO = { status: 401, statusText: 'Unauthorized' };
const problema = (code: string) => ({ type: 'x', title: 'No autorizado', status: 401, code, detail: 'detalle del backend', traceId: 't' });

describe('LoginComponent (OnPush)', () => {
  let backend: HttpTestingController;
  let harness: RouterTestingHarness;
  const elemento = () => harness.routeNativeElement as HTMLElement;
  const texto = () => elemento().textContent ?? '';

  function escribir(selector: string, valor: string): void {
    const input = elemento().querySelector<HTMLInputElement>(selector)!;
    input.value = valor;
    input.dispatchEvent(new Event('input'));
  }
  function enviar(): void {
    elemento().querySelector('form')!.dispatchEvent(new Event('submit'));
    harness.detectChanges();
  }

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        provideRouter([{ path: 'login', component: LoginComponent }, { path: 'dashboard', children: [] }, { path: 'registro', children: [] }])
      ]
    });
    backend = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => backend.verify());

  async function pasarAlSegundoPaso(): Promise<void> {
    await harness.navigateByUrl('/login', LoginComponent);
    escribir('#correo', 'demo@example.test');
    escribir('#contrasena', 'secreto-largo');
    enviar();
    backend.expectOne('/api/v1/auth/login').flush({ requiresTwoFactor: true, twoFactorChallengeToken: 'desafio-1' });
    harness.detectChanges();
    expect(elemento().querySelector('#codigo')).not.toBeNull();
  }

  it('al llegar por un cierre de sesión forzado muestra "Tu sesión se cerró"', async () => {
    await harness.navigateByUrl('/login?sesion=cerrada', LoginComponent);
    expect(texto()).toContain('Tu sesión se cerró');
  });

  it('2FA: un código ya usado pide esperar el siguiente y se queda en el paso del código', async () => {
    await pasarAlSegundoPaso();
    escribir('#codigo', '123456');
    enviar();
    backend.expectOne('/api/v1/auth/login/2fa').flush(problema('two_factor_code_reused'), NO_AUTORIZADO);
    harness.detectChanges();

    expect(texto()).toContain('Ese código ya se usó; esperá el siguiente');
    expect(elemento().querySelector('#codigo')).not.toBeNull();
  });

  it('2FA: un desafío vencido vuelve al paso del correo y la contraseña', async () => {
    await pasarAlSegundoPaso();
    escribir('#codigo', '123456');
    enviar();
    backend.expectOne('/api/v1/auth/login/2fa').flush(problema('two_factor_expired'), NO_AUTORIZADO);
    harness.detectChanges();

    expect(texto()).toContain('La verificación venció');
    expect(elemento().querySelector('#codigo')).toBeNull();
    expect(elemento().querySelector('#correo')).not.toBeNull();
  });

  it('2FA correcto: aplica la sesión y entra al inicio', async () => {
    await pasarAlSegundoPaso();
    escribir('#codigo', '123456');
    enviar();
    const verificacion = backend.expectOne('/api/v1/auth/login/2fa');
    expect(verificacion.request.body).toEqual({ challengeToken: 'desafio-1', code: '123456' });
    verificacion.flush({ session: sesionDePrueba([]), csrfToken: 'c' });
    await harness.fixture.whenStable();
    expect(TestBed.inject(AuthService).autenticado()).toBeTrue();
    expect(TestBed.inject(Router).url).toBe('/dashboard');
  });
});
