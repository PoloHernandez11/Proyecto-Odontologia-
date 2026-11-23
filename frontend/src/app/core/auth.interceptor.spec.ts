import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { SessionResponse } from '../api/schema';
import { sesionDePrueba } from '../testing/sesion-de-prueba';
import { authInterceptor } from './auth.interceptor';
import { AuthService, ESPERA_ANTE_CONFLICTO_MS } from './auth.service';

const sesion = (token: string) => sesionDePrueba(['patients.read'], { token });
const respuesta = (token: string): SessionResponse => ({ session: sesion(token), csrfToken: 'csrf-nuevo' });
const NO_AUTORIZADO = { status: 401, statusText: 'Unauthorized' };
const problema = (code: string) => ({ type: 'about:blank', title: 'No autorizado', status: 401, code, detail: 'x', traceId: 't' });

describe('authInterceptor + AuthService (renovación de sesión)', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: AuthService;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), provideRouter([])]
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
    auth.sesion.set(sesion('viejo'));
  });

  afterEach(() => backend.verify());

  it('agrega el Bearer a las llamadas de la API pero no a las de autenticación', () => {
    http.get('/api/v1/patients').subscribe();
    expect(backend.expectOne('/api/v1/patients').request.headers.get('Authorization')).toBe('Bearer viejo');
    http.post('/api/v1/auth/login', {}).subscribe();
    expect(backend.expectOne('/api/v1/auth/login').request.headers.has('Authorization')).toBeFalse();
  });

  it('ante varios 401 simultáneos renueva la sesión una sola vez y reintenta con el token nuevo', () => {
    const resultados: unknown[] = [];
    http.get('/api/v1/patients').subscribe(r => resultados.push(r));
    http.get('/api/v1/agenda').subscribe(r => resultados.push(r));
    backend.expectOne('/api/v1/patients').flush(null, NO_AUTORIZADO);
    backend.expectOne('/api/v1/agenda').flush(null, NO_AUTORIZADO);

    const renovacion = backend.match('/api/v1/auth/refresh');
    expect(renovacion.length).toBe(1);
    expect(renovacion[0].request.withCredentials).toBeTrue();
    expect(renovacion[0].request.headers.has('X-CSRF-Token')).toBeTrue();
    renovacion[0].flush(respuesta('nuevo'));

    const reintento = backend.expectOne('/api/v1/patients');
    expect(reintento.request.headers.get('Authorization')).toBe('Bearer nuevo');
    reintento.flush({ ok: 1 });
    backend.expectOne('/api/v1/agenda').flush({ ok: 2 });
    expect(resultados).toEqual([{ ok: 1 }, { ok: 2 }]);
    expect(auth.accessToken).toBe('nuevo');
  });

  it('si la renovación falla, olvida la sesión y lleva al login avisando que venció', () => {
    const navegar = spyOn(router, 'navigate').and.resolveTo(true);
    let error: unknown = null;
    http.get('/api/v1/patients').subscribe({ error: e => (error = e) });
    backend.expectOne('/api/v1/patients').flush(null, NO_AUTORIZADO);
    backend.expectOne('/api/v1/auth/refresh').flush(null, NO_AUTORIZADO);
    expect(error).not.toBeNull();
    expect(auth.autenticado()).toBeFalse();
    expect(navegar).toHaveBeenCalledWith(['/login'], jasmine.objectContaining({ queryParams: jasmine.objectContaining({ sesion: 'vencida' }) }));
  });

  it('session_invalidated: intenta renovar y, si no puede, lleva al login con "Tu sesión se cerró"', () => {
    const navegar = spyOn(router, 'navigate').and.resolveTo(true);
    let error: unknown = null;
    http.get('/api/v1/patients').subscribe({ error: e => (error = e) });
    backend.expectOne('/api/v1/patients').flush(problema('session_invalidated'), NO_AUTORIZADO);
    backend.expectOne('/api/v1/auth/refresh').flush(problema('invalid_refresh_token'), NO_AUTORIZADO);
    expect(error).not.toBeNull();
    expect(auth.autenticado()).toBeFalse();
    expect(navegar).toHaveBeenCalledWith(['/login'], jasmine.objectContaining({ queryParams: jasmine.objectContaining({ sesion: 'cerrada' }) }));
  });

  it('session_invalidated con una renovación exitosa reintenta sin cerrar la sesión', () => {
    let resultado: unknown = null;
    http.get('/api/v1/patients').subscribe(r => (resultado = r));
    backend.expectOne('/api/v1/patients').flush(problema('session_invalidated'), NO_AUTORIZADO);
    backend.expectOne('/api/v1/auth/refresh').flush(respuesta('nuevo'));
    backend.expectOne('/api/v1/patients').flush({ ok: true });
    expect(resultado).toEqual({ ok: true });
    expect(auth.autenticado()).toBeTrue();
  });

  it('refresh_conflict: espera un instante, renueva otra vez y no cierra la sesión', fakeAsync(() => {
    const navegar = spyOn(router, 'navigate').and.resolveTo(true);
    let resultado: unknown = null;
    http.get('/api/v1/patients').subscribe(r => (resultado = r));
    backend.expectOne('/api/v1/patients').flush(null, NO_AUTORIZADO);
    backend.expectOne('/api/v1/auth/refresh').flush(problema('refresh_conflict'), NO_AUTORIZADO);

    // Todavía no reintenta: espera a que la otra pestaña termine de renovar.
    backend.expectNone('/api/v1/auth/refresh');
    tick(ESPERA_ANTE_CONFLICTO_MS);
    backend.expectOne('/api/v1/auth/refresh').flush(respuesta('de-la-otra-pestana'));
    const reintento = backend.expectOne('/api/v1/patients');
    expect(reintento.request.headers.get('Authorization')).toBe('Bearer de-la-otra-pestana');
    reintento.flush({ ok: true });

    expect(resultado).toEqual({ ok: true });
    expect(auth.autenticado()).toBeTrue();
    expect(navegar).not.toHaveBeenCalled();
  }));

  it('refresh_conflict repetido: devuelve el error a quien llamó pero conserva la sesión', fakeAsync(() => {
    const navegar = spyOn(router, 'navigate').and.resolveTo(true);
    let error: unknown = null;
    http.get('/api/v1/patients').subscribe({ error: e => (error = e) });
    backend.expectOne('/api/v1/patients').flush(null, NO_AUTORIZADO);
    backend.expectOne('/api/v1/auth/refresh').flush(problema('refresh_conflict'), NO_AUTORIZADO);
    tick(ESPERA_ANTE_CONFLICTO_MS);
    backend.expectOne('/api/v1/auth/refresh').flush(problema('refresh_conflict'), NO_AUTORIZADO);
    expect(error).not.toBeNull();
    expect(auth.autenticado()).toBeTrue();
    expect(navegar).not.toHaveBeenCalled();
  }));

  it('si falla el reintento (no la renovación), el error llega a quien llamó sin cerrar la sesión', () => {
    let error: unknown = null;
    http.get('/api/v1/patients').subscribe({ error: e => (error = e) });
    backend.expectOne('/api/v1/patients').flush(null, NO_AUTORIZADO);
    backend.expectOne('/api/v1/auth/refresh').flush(respuesta('nuevo'));
    backend.expectOne('/api/v1/patients').flush(null, { status: 500, statusText: 'Error' });
    expect(error).not.toBeNull();
    expect(auth.autenticado()).toBeTrue();
  });

  it('no renueva cuando falla una llamada de autenticación ni ante otros errores', () => {
    http.post('/api/v1/auth/login', {}).subscribe({ error: () => undefined });
    backend.expectOne('/api/v1/auth/login').flush(null, NO_AUTORIZADO);
    http.get('/api/v1/patients').subscribe({ error: () => undefined });
    backend.expectOne('/api/v1/patients').flush(null, { status: 409, statusText: 'Conflict' });
    backend.expectNone('/api/v1/auth/refresh');
    expect(auth.autenticado()).toBeTrue();
  });
});
