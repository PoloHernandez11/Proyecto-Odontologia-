// Los nombres coinciden con los enums del backend (EstadoCara / EstadoGeneralDiente) y con la base de datos.
// Convención: <Tipo><ARealizar|Existente>. ARealizar = rojo, Existente = azul, Sellante = verde.

export const CARAS = ['oclusal', 'mesial', 'distal', 'vestibular', 'lingual'];

export const ROJO = '#d32f2f'; // a realizar
export const AZUL = '#1565c0'; // existente
export const VERDE = '#2e7d32'; // sellante

/** Modo activo: decide el sufijo de las herramientas que tienen estado. */
export const MODOS = [
  { id: 'ARealizar', etiqueta: 'A realizar', color: ROJO },
  { id: 'Existente', etiqueta: 'Existente', color: AZUL },
];

/** Color de un valor guardado. Null = sano o sin marca. */
export function colorDe(valor) {
  if (!valor || valor === 'Sano' || valor === 'Presente') return null;
  if (valor === 'Sellante') return VERDE;
  return valor.endsWith('ARealizar') ? ROJO : AZUL;
}

const FORMAS_GENERALES = [
  [/^Corona/, 'corona'],
  [/^Endodoncia/, 'endodoncia'],
  [/^Implante/, 'implante'],
  [/^ProtesisFija/, 'fija'],
  [/^ProtesisRemovible/, 'removible'],
];

/** Cómo se dibuja un estado general encima del diente. Null = no se dibuja nada. */
export function simboloDe(valor) {
  if (!valor || valor === 'Presente') return null;
  if (valor === 'AusenteExistente') return { forma: 'x', color: AZUL };
  if (valor === 'ExtraccionARealizar') return { forma: 'x', color: ROJO };
  const forma = FORMAS_GENERALES.find(([re]) => re.test(valor));
  return forma ? { forma: forma[1], color: colorDe(valor) } : null;
}

/**
 * Herramientas de la barra. "cara" pinta la cara clickeada; "general" marca la pieza entera.
 * Las que tienen `conModo` toman el sufijo del modo activo. Aplicar dos veces la misma marca la quita.
 */
export const HERRAMIENTAS = [
  { id: 'caries', tipo: 'cara', base: 'Caries', etiqueta: 'Caries', conModo: true },
  { id: 'obturacion', tipo: 'cara', base: 'Obturacion', etiqueta: 'Obturación', conModo: true },
  { id: 'sellante', tipo: 'cara', valor: 'Sellante', etiqueta: 'Sellante', color: VERDE },
  { id: 'corona', tipo: 'general', base: 'Corona', etiqueta: 'Corona', conModo: true },
  { id: 'endodoncia', tipo: 'general', base: 'Endodoncia', etiqueta: 'Endodoncia', conModo: true },
  { id: 'extraccion', tipo: 'general', valor: 'ExtraccionARealizar', etiqueta: 'Extracción', color: ROJO },
  { id: 'ausente', tipo: 'general', valor: 'AusenteExistente', etiqueta: 'Ausente', color: AZUL },
  { id: 'implante', tipo: 'general', base: 'Implante', etiqueta: 'Implante', conModo: true },
  { id: 'fija', tipo: 'general', base: 'ProtesisFija', etiqueta: 'Prót. fija', conModo: true },
  { id: 'removible', tipo: 'general', base: 'ProtesisRemovible', etiqueta: 'Prót. removible', conModo: true },
  { id: 'sano', tipo: 'limpiar', etiqueta: 'Sano', color: '#b0bec5' },
];

/** Valor que se guarda al aplicar una herramienta con el modo activo. */
export function valorDe(herramienta, modo) {
  const sufijo = typeof modo === 'string' ? modo : modo.id;
  return herramienta.conModo ? `${herramienta.base}${sufijo}` : herramienta.valor;
}
