import { memo } from 'react';
import { colorDe, simboloDe } from './estados';
import { caraEnPosicion, esSuperior } from './fdi';

// Cuadrado de 40x40 dividido en 5 sectores, como en la ficha de papel.
export const SECTORES = [
  { posicion: 'arriba', puntos: '2,2 38,2 27,13 13,13' },
  { posicion: 'abajo', puntos: '2,38 13,27 27,27 38,38' },
  { posicion: 'izquierda', puntos: '2,2 13,13 13,27 2,38' },
  { posicion: 'derecha', puntos: '38,2 38,38 27,27 27,13' },
  { posicion: 'centro', puntos: '13,13 27,13 27,27 13,27' },
];

export function Simbolo({ estadoGeneral }) {
  const simbolo = simboloDe(estadoGeneral);
  if (!simbolo) return null;
  const { forma, color } = simbolo;
  const comun = { stroke: color, fill: 'none', pointerEvents: 'none' };

  switch (forma) {
    case 'x':
      return (
        <g {...comun} strokeWidth={3.5} strokeLinecap="round">
          <line x1={4} y1={4} x2={36} y2={36} />
          <line x1={36} y1={4} x2={4} y2={36} />
        </g>
      );
    case 'corona':
      return <circle {...comun} cx={20} cy={20} r={18.5} strokeWidth={2.5} />;
    case 'endodoncia':
      return <line {...comun} x1={20} y1={0} x2={20} y2={40} strokeWidth={3.5} strokeLinecap="round" />;
    case 'implante':
      return <rect {...comun} x={8} y={8} width={24} height={24} strokeWidth={3} />;
    case 'fija':
      return <rect {...comun} x={0.75} y={0.75} width={38.5} height={38.5} strokeWidth={3} />;
    case 'removible':
      return <rect {...comun} x={0.75} y={0.75} width={38.5} height={38.5} strokeWidth={3} strokeDasharray="5 3" />;
    default:
      return null;
  }
}

/**
 * Una pieza dental. Recibe su propio objeto `diente` (referencia estable si no cambió) y callbacks estables,
 * por eso memo evita re-renderizar las otras 51 piezas al hacer click en una.
 */
function DienteBase({ numero, diente, editable, seleccionada, onAplicar, onSeleccionar }) {
  const superior = esSuperior(numero);
  const etiqueta = <span className="diente__numero">{numero}</span>;
  const clases = ['diente'];
  if (diente.estadoGeneral === 'AusenteExistente') clases.push('diente--ausente');
  if (seleccionada) clases.push('diente--seleccionada');

  return (
    <div className={clases.join(' ')}>
      {superior && etiqueta}
      <svg viewBox="0 0 40 40" className="diente__svg" role="group" aria-label={`Pieza ${numero}`}>
        {SECTORES.map(({ posicion, puntos }) => {
          const cara = caraEnPosicion(numero, posicion);
          const valor = diente.caras[cara];
          return (
            <polygon
              key={posicion}
              points={puntos}
              fill={colorDe(valor) ?? '#ffffff'}
              className="cara cara--editable"
              onClick={() => {
                onSeleccionar(numero, cara);
                if (editable) onAplicar(numero, cara);
              }}
            >
              <title>{`${numero} · ${cara}: ${valor}`}</title>
            </polygon>
          );
        })}
        <Simbolo estadoGeneral={diente.estadoGeneral} />
      </svg>
      {!superior && etiqueta}
    </div>
  );
}

export const Diente = memo(DienteBase);
