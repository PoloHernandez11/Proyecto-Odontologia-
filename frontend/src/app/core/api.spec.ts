import { HttpErrorResponse, HttpHeaders, provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { sesionDePrueba } from '../testing/sesion-de-prueba';
import { cantidad, codigoDeError, esConflictoDeConcurrencia, fechaCorta, horaLocal, mensajeDeError, segundosDeEspera } from './api';
import { AuthService } from './auth.service';
import { zonaDelConsultorio } from './zona-horaria';

describe('cantidad (singular / plural)', () => {
  it('concuerda la palabra con el número', () => {
    expect(cantidad(1, 'resultado')).toBe('1 resultado');
    expect(cantidad(0, 'resultado')).toBe('0 resultados');
    expect(cantidad(3, 'paciente')).toBe('3 pacientes');
    expect(cantidad(2, 'profesional')).toBe('2 profesionales');
  });

  it('acepta un plural irregular', () => {
    expect(cantidad(1, 'egreso registrado', 'egresos registrados')).toBe('1 egreso registrado');
    expect(cantidad(5, 'vence', 'vencen')).toBe('5 vencen');
  });
});

describe('esConflictoDeConcurrencia', () => {
  const conflicto = new HttpErrorResponse({
    status: 409,
    error: { type: 'x', title: 'Conflicto', status: 409, code: 'concurrency_conflict', detail: 'Otra persona modificó el pago.', traceId: 't' }
  });

  it('reconoce el 409 con code concurrency_conflict y mensajeDeError expone su detail', () => {
    expect(esConflictoDeConcurrencia(conflicto)).toBeTrue();
    expect(mensajeDeError(conflicto)).toBe('Otra persona modificó el pago.');
  });

  it('descarta otros errores', () => {
    expect(esConflictoDeConcurrencia(new HttpErrorResponse({ status: 409, error: { code: 'duplicate' } }))).toBeFalse();
    expect(esConflictoDeConcurrencia(new HttpErrorResponse({ status: 400, error: { code: 'concurrency_conflict' } }))).toBeFalse();
    expect(esConflictoDeConcurrencia(new Error('x'))).toBeFalse();
  });
});

describe('mensajeDeError (límites del servidor)', () => {
  it('429 con Retry-After en segundos indica cuánto esperar', () => {
    const error = new HttpErrorResponse({ status: 429, headers: new HttpHeaders({ 'Retry-After': '30' }), error: { code: 'rate_limited', detail: 'x' } });
    expect(mensajeDeError(error)).toBe('Demasiadas solicitudes. Esperá 30 segundos y volvé a intentar.');
  });

  it('429 con Retry-After largo lo expresa en minutos, y sin encabezado usa un texto genérico', () => {
    expect(mensajeDeError(new HttpErrorResponse({ status: 429, headers: new HttpHeaders({ 'Retry-After': '90' }) })))
      .toBe('Demasiadas solicitudes. Esperá 2 minutos y volvé a intentar.');
    expect(mensajeDeError(new HttpErrorResponse({ status: 429, headers: new HttpHeaders({ 'Retry-After': '1' }) })))
      .toBe('Demasiadas solicitudes. Esperá un segundo y volvé a intentar.');
    expect(mensajeDeError(new HttpErrorResponse({ status: 429 }))).toBe('Demasiadas solicitudes. Esperá un momento y volvé a intentar.');
  });

  it('segundosDeEspera acepta también una fecha HTTP', () => {
    const ahora = Date.parse('2026-01-01T12:00:00Z');
    const error = new HttpErrorResponse({ status: 429, headers: new HttpHeaders({ 'Retry-After': 'Thu, 01 Jan 2026 12:00:45 GMT' }) });
    expect(segundosDeEspera(error, ahora)).toBe(45);
  });

  it('413 payload_too_large explica los tamaños máximos', () => {
    const error = new HttpErrorResponse({ status: 413, error: { code: 'payload_too_large', detail: 'La solicitud supera el tamaño máximo permitido.' } });
    expect(mensajeDeError(error)).toContain('20 MB');
    expect(mensajeDeError(error)).toContain('2 MB');
  });

  it('422 last_administrator muestra el detalle del backend', () => {
    const error = new HttpErrorResponse({ status: 422, error: { code: 'last_administrator', detail: 'El consultorio debe conservar al menos un administrador activo.' } });
    expect(codigoDeError(error)).toBe('last_administrator');
    expect(mensajeDeError(error)).toBe('El consultorio debe conservar al menos un administrador activo.');
  });

  it('401 session_invalidated avisa que la sesión se cerró', () => {
    expect(mensajeDeError(new HttpErrorResponse({ status: 401, error: { code: 'session_invalidated' } }))).toContain('Tu sesión se cerró');
  });
});

describe('fechas y horas en la zona del consultorio activo', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }));

  it('horaLocal y fechaCorta usan memberships[].timeZoneId y no la zona del navegador', () => {
    const auth = TestBed.inject(AuthService);
    auth.sesion.set(sesionDePrueba([], { zona: 'America/Argentina/Buenos_Aires' }));
    // 02:30 UTC del 2 de marzo = 23:30 del 1 de marzo en Buenos Aires (UTC-3).
    expect(horaLocal('2026-03-02T02:30:00Z')).toBe('23:30');
    expect(fechaCorta('2026-03-02T02:30:00Z')).toBe('01/03/2026');
    expect(zonaDelConsultorio()).toBe('America/Argentina/Buenos_Aires');

    auth.sesion.set(sesionDePrueba([], { zona: 'Asia/Tokyo' }));
    expect(horaLocal('2026-03-02T02:30:00Z')).toBe('11:30');
    expect(fechaCorta('2026-03-02T02:30:00Z')).toBe('02/03/2026');
  });

  it('una fecha de calendario se muestra tal cual, sin correrse de día', () => {
    TestBed.inject(AuthService).sesion.set(sesionDePrueba([], { zona: 'Pacific/Kiritimati' }));
    expect(fechaCorta('2026-03-01')).toBe('01/03/2026');
    expect(fechaCorta(null)).toBe('—');
  });

  it('sin sesión usa la zona del navegador', () => {
    TestBed.inject(AuthService).sesion.set(null);
    expect(zonaDelConsultorio()).toBe(Intl.DateTimeFormat().resolvedOptions().timeZone);
  });
});
