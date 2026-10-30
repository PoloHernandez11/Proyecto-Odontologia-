// Numeración FDI y geometría de las caras, tal como en la ficha dental en papel.

/** Filas del odontograma en el mismo orden que la ficha: [lado derecho del paciente, lado izquierdo]. */
export const FILAS = [
  { id: 'perm-sup', titulo: 'Permanentes superiores', cuadrantes: [[18, 17, 16, 15, 14, 13, 12, 11], [21, 22, 23, 24, 25, 26, 27, 28]] },
  { id: 'perm-inf', titulo: 'Permanentes inferiores', cuadrantes: [[48, 47, 46, 45, 44, 43, 42, 41], [31, 32, 33, 34, 35, 36, 37, 38]] },
  { id: 'temp-sup', titulo: 'Temporales superiores', cuadrantes: [[55, 54, 53, 52, 51], [61, 62, 63, 64, 65]] },
  { id: 'temp-inf', titulo: 'Temporales inferiores', cuadrantes: [[85, 84, 83, 82, 81], [71, 72, 73, 74, 75]] },
];

export const TODAS_LAS_PIEZAS = FILAS.flatMap((f) => f.cuadrantes.flat());

const cuadrante = (n) => Math.floor(n / 10);

/** Cuadrantes 1, 2, 5 y 6 = maxilar superior. */
export const esSuperior = (n) => [1, 2, 5, 6].includes(cuadrante(n));

/** Cuadrantes 1, 4, 5 y 8 = lado derecho del paciente, que se dibuja a la IZQUIERDA del odontograma. */
const esLadoDerechoPaciente = (n) => [1, 4, 5, 8].includes(cuadrante(n));

/**
 * Traduce la posición visual dentro del cuadrado a la cara anatómica:
 *  - centro: oclusal
 *  - arriba/abajo: vestibular hacia afuera de la boca (arriba en superiores, abajo en inferiores)
 *  - izquierda/derecha: mesial siempre mira hacia la línea media
 */
export function caraEnPosicion(numero, posicion) {
  switch (posicion) {
    case 'centro':
      return 'oclusal';
    case 'arriba':
      return esSuperior(numero) ? 'vestibular' : 'lingual';
    case 'abajo':
      return esSuperior(numero) ? 'lingual' : 'vestibular';
    case 'izquierda':
      return esLadoDerechoPaciente(numero) ? 'distal' : 'mesial';
    case 'derecha':
      return esLadoDerechoPaciente(numero) ? 'mesial' : 'distal';
    default:
      throw new Error(`Posición desconocida: ${posicion}`);
  }
}

export const cuadranteDe = (n) => Math.floor(n / 10);

const NOMBRE_PERMANENTE = [
  'incisivo central', 'incisivo lateral', 'canino', '1.er premolar',
  '2.º premolar', '1.er molar', '2.º molar', '3.er molar',
];
const NOMBRE_TEMPORAL = ['incisivo central', 'incisivo lateral', 'canino', '1.er molar', '2.º molar'];
const LADO = {
  1: 'superior derecho', 2: 'superior izquierdo', 3: 'inferior izquierdo', 4: 'inferior derecho',
  5: 'superior derecho', 6: 'superior izquierdo', 7: 'inferior izquierdo', 8: 'inferior derecho',
};

/** Nombre anatómico de la pieza, por ejemplo "1.er molar inferior izquierdo". */
export function nombrePieza(n) {
  const cuadrante = cuadranteDe(n);
  const posicion = n % 10;
  const temporal = cuadrante >= 5;
  const nombre = (temporal ? NOMBRE_TEMPORAL : NOMBRE_PERMANENTE)[posicion - 1] ?? 'pieza';
  return `${nombre}${temporal ? ' temporal' : ''} ${LADO[cuadrante]}`;
}
