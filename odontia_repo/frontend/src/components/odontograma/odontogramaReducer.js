import { CARAS, valorDe } from './estados';
import { TODAS_LAS_PIEZAS } from './fdi';

/**
 * Estado del odontograma, normalizado por número de pieza:
 *
 * {
 *   dientes: { 36: { estadoGeneral: 'Presente', caras: { oclusal: 'CariesARealizar', ... }, nota: '' }, ... },
 *   observaciones: '',
 *   meta: { id, fechaRegistro, profesional },   // versión cargada
 *   original: <dientes al cargar>,              // para "Descartar cambios"
 *   cambios: false,
 * }
 *
 * Cada acción reemplaza SOLO el objeto de la pieza tocada; el resto conserva su referencia,
 * así <Diente> (memo) re-renderiza únicamente la pieza que cambió.
 */

const CARAS_SANAS = Object.freeze(Object.fromEntries(CARAS.map((c) => [c, 'Sano'])));
export const DIENTE_SANO = Object.freeze({ estadoGeneral: 'Presente', caras: CARAS_SANAS, nota: '' });

const esSano = (d) =>
  d.estadoGeneral === 'Presente' && CARAS.every((c) => d.caras[c] === 'Sano') && !d.nota?.trim();

/** API (lista plana, solo piezas con datos) -> mapa con las 52 piezas. */
function desdeApi(dientesApi) {
  const mapa = Object.fromEntries(TODAS_LAS_PIEZAS.map((n) => [n, DIENTE_SANO]));
  for (const d of dientesApi) {
    mapa[d.numeroDiente] = {
      estadoGeneral: d.estadoGeneral,
      caras: Object.fromEntries(CARAS.map((c) => [c, d[c]])),
      nota: d.nota ?? '',
    };
  }
  return mapa;
}

/** Mapa -> body para POST /api/pacientes/{id}/odontograma (solo piezas con algo registrado). */
export function aApi(state) {
  return {
    observaciones: state.observaciones,
    dientes: Object.entries(state.dientes)
      .filter(([, d]) => !esSano(d))
      .map(([numero, d]) => ({
        numeroDiente: Number(numero),
        estadoGeneral: d.estadoGeneral,
        ...d.caras,
        nota: d.nota?.trim() ? d.nota.trim() : null,
      })),
  };
}

export const estadoInicial = {
  dientes: desdeApi([]),
  original: null,
  observaciones: '',
  meta: null,
  cambios: false,
};

function aplicarHerramienta(diente, cara, herramienta, modo) {
  switch (herramienta.tipo) {
    case 'cara': {
      // Una pieza ausente no tiene caras que marcar.
      if (diente.estadoGeneral === 'AusenteExistente') return diente;
      const valor = valorDe(herramienta, modo);
      const nuevo = diente.caras[cara] === valor ? 'Sano' : valor;
      return { ...diente, caras: { ...diente.caras, [cara]: nuevo } };
    }
    case 'general': {
      const valor = valorDe(herramienta, modo);
      const estadoGeneral = diente.estadoGeneral === valor ? 'Presente' : valor;
      // Si la pieza pasa a ausente, sus caras dejan de tener sentido.
      return estadoGeneral === 'AusenteExistente'
        ? { ...diente, estadoGeneral, caras: CARAS_SANAS }
        : { ...diente, estadoGeneral };
    }
    case 'limpiar':
      // "Sano" borra las marcas, pero conserva la nota de la pieza.
      return { ...DIENTE_SANO, nota: diente.nota };
    default:
      return diente;
  }
}

export function odontogramaReducer(state, action) {
  switch (action.type) {
    case 'CARGAR': {
      const dientes = desdeApi(action.odontograma.dientes);
      return {
        dientes,
        original: dientes,
        observaciones: action.odontograma.observaciones ?? '',
        observacionesOriginal: action.odontograma.observaciones ?? '',
        meta: action.odontograma.id
          ? { id: action.odontograma.id, fechaRegistro: action.odontograma.fechaRegistro, profesional: action.odontograma.profesional }
          : null,
        cambios: false,
      };
    }
    case 'APLICAR': {
      const { numero, cara, herramienta, modo } = action;
      const actual = state.dientes[numero];
      const nuevo = aplicarHerramienta(actual, cara, herramienta, modo);
      if (nuevo === actual) return state;
      return { ...state, dientes: { ...state.dientes, [numero]: nuevo }, cambios: true };
    }
    case 'NOTA': {
      const actual = state.dientes[action.numero];
      const texto = action.texto.slice(0, 255);
      if ((actual.nota ?? '') === texto) return state;
      return {
        ...state,
        dientes: { ...state.dientes, [action.numero]: { ...actual, nota: texto } },
        cambios: true,
      };
    }
    case 'OBSERVACIONES':
      return { ...state, observaciones: action.texto, cambios: true };
    case 'DESCARTAR':
      return {
        ...state,
        dientes: state.original ?? estadoInicial.dientes,
        observaciones: state.observacionesOriginal ?? '',
        cambios: false,
      };
    default:
      throw new Error(`Acción desconocida: ${action.type}`);
  }
}
