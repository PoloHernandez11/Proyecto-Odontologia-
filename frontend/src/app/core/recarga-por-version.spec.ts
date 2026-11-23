import { esFalloDeCargaDeFragmento } from './recarga-por-version';

describe('esFalloDeCargaDeFragmento', () => {
  it('reconoce los errores de módulos dinámicos de los navegadores', () => {
    expect(esFalloDeCargaDeFragmento(new TypeError('Failed to fetch dynamically imported module: https://x/chunk-ABC.js'))).toBeTrue();
    expect(esFalloDeCargaDeFragmento(new TypeError('error loading dynamically imported module'))).toBeTrue();
    expect(esFalloDeCargaDeFragmento(new TypeError('Importing a module script failed.'))).toBeTrue();
    const error = new Error('Loading chunk 123 failed.');
    error.name = 'ChunkLoadError';
    expect(esFalloDeCargaDeFragmento(error)).toBeTrue();
  });

  it('ignora otros errores de navegación', () => {
    expect(esFalloDeCargaDeFragmento(new Error('Cannot match any routes'))).toBeFalse();
    expect(esFalloDeCargaDeFragmento(undefined)).toBeFalse();
  });
});
