import { useEffect, useState } from 'react';
import { CARAS, colorDe, HERRAMIENTAS } from './estados';
import { caraEnPosicion, cuadranteDe, nombrePieza } from './fdi';
import { SECTORES, Simbolo } from './Diente';

const ETIQUETA_CARA = {
  Sano: 'Sana',
  CariesARealizar: 'Caries',
  CariesExistente: 'Caries',
  ObturacionARealizar: 'Obturación',
  ObturacionExistente: 'Obturación',
  Sellante: 'Sellante',
};

const ETIQUETA_GENERAL = {
  CoronaARealizar: 'Corona indicada',
  CoronaExistente: 'Corona realizada',
  EndodonciaARealizar: 'Endodoncia indicada',
  EndodonciaExistente: 'Endodoncia realizada',
  ImplanteARealizar: 'Implante indicado',
  ImplanteExistente: 'Implante colocado',
  ProtesisFijaARealizar: 'Prótesis fija indicada',
  ProtesisFijaExistente: 'Prótesis fija colocada',
  ProtesisRemovibleARealizar: 'Prótesis removible indicada',
  ProtesisRemovibleExistente: 'Prótesis removible colocada',
  AusenteExistente: 'Ausente',
  ExtraccionARealizar: 'A extraer',
};

const capitalizar = (texto) => texto.charAt(0).toUpperCase() + texto.slice(1);

/** Chips de resumen de la pieza: lo general primero, después las caras marcadas. */
function chipsDe(diente) {
  const chips = [];
  const general = ETIQUETA_GENERAL[diente.estadoGeneral];
  if (general) chips.push({ texto: general, color: colorDe(diente.estadoGeneral) });
  for (const cara of CARAS) {
    const valor = diente.caras[cara];
    if (valor !== 'Sano') chips.push({ texto: `${ETIQUETA_CARA[valor]} ${cara}`, color: colorDe(valor) });
  }
  return chips;
}

/**
 * Panel de la pieza seleccionada: cuadro de caras, resumen, nota e historial de la historia clínica.
 * Es solo presentación: todo cambio sale por callbacks del padre.
 */
export default function PanelPieza({
  numero,
  diente,
  caraSeleccionada,
  editable,
  herramienta,
  modoEtiqueta,
  historial,
  onSeleccionarCara,
  onAplicar,
  onNota,
  onCerrar,
}) {
  const [borrador, setBorrador] = useState(diente.nota ?? '');
  useEffect(() => setBorrador(diente.nota ?? ''), [numero, diente.nota]);

  const chips = chipsDe(diente);
  const notaGuardada = diente.nota ?? '';
  const hayNuevaNota = borrador.trim() !== notaGuardada.trim();
  const etiquetaAplicar = HERRAMIENTAS.find((h) => h.id === herramienta.id)?.etiqueta ?? herramienta.etiqueta;

  return (
    <aside className="pieza" aria-label={`Detalle de la pieza ${numero}`}>
      <header className="pieza__cabecera">
        <span className="pieza__numero">{numero}</span>
        <div className="pieza__titulo">
          <h3>Pieza {numero}</h3>
          <p>{nombrePieza(numero)} · C{cuadranteDe(numero)}</p>
        </div>
        <button type="button" className="pieza__cerrar" onClick={onCerrar} aria-label="Cerrar panel">✕</button>
      </header>

      <div className="pieza__caras">
        <svg viewBox="0 0 40 40" className="pieza__cuadro" role="group" aria-label={`Caras de la pieza ${numero}`}>
          {SECTORES.map(({ posicion, puntos }) => {
            const cara = caraEnPosicion(numero, posicion);
            const valor = diente.caras[cara];
            return (
              <polygon
                key={posicion}
                points={puntos}
                fill={colorDe(valor) ?? '#ffffff'}
                className={`cara ${caraSeleccionada === cara ? 'cara--seleccionada' : ''}`}
                onClick={() => onSeleccionarCara(cara)}
              >
                <title>{`${capitalizar(cara)}: ${ETIQUETA_CARA[valor] ?? 'Sana'}`}</title>
              </polygon>
            );
          })}
          <Simbolo estadoGeneral={diente.estadoGeneral} />
        </svg>
        <ul className="pieza__lista">
          {CARAS.map((cara) => {
            const valor = diente.caras[cara];
            return (
              <li key={cara} className={caraSeleccionada === cara ? 'pieza__fila--activa' : ''}>
                <button type="button" onClick={() => onSeleccionarCara(cara)}>
                  <span>{capitalizar(cara)}</span>
                  <b style={{ color: colorDe(valor) ?? '#6b7b80' }}>{ETIQUETA_CARA[valor] ?? 'Sana'}</b>
                </button>
              </li>
            );
          })}
        </ul>
      </div>

      {chips.length > 0 && (
        <div className="pieza__chips">
          {chips.map((c) => (
            <span key={c.texto} className="pieza__chip" style={{ '--color': c.color }}>
              <i /> {c.texto}
            </span>
          ))}
        </div>
      )}

      <label className="pieza__nota">
        Nota de la pieza
        <textarea
          rows={2}
          maxLength={255}
          value={borrador}
          readOnly={!editable}
          placeholder="Próxima sesión: obturar conductos"
          onChange={(e) => setBorrador(e.target.value)}
        />
      </label>

      {editable && (
        <>
          <p className="pieza__aplicar">
            Se aplicará: <b>{etiquetaAplicar}</b> · {modoEtiqueta}
            {herramienta.tipo === 'cara' && <> en la cara <b>{caraSeleccionada ?? 'oclusal'}</b></>}
          </p>
          <div className="pieza__acciones">
            <button type="button" onClick={() => onNota(borrador)} disabled={!hayNuevaNota}>
              ✎ Agregar nota
            </button>
            <button type="button" className="primario" onClick={onAplicar}>
              ✓ Aplicar estado
            </button>
          </div>
        </>
      )}

      <section className="pieza__historial">
        <h4>Historial de la pieza</h4>
        {historial === null && <p className="tenue">Cargando…</p>}
        {historial !== null && historial.length === 0 && (
          <p className="tenue">Sin atenciones registradas para esta pieza.</p>
        )}
        {historial?.length > 0 && (
          <ol>
            {historial.map((h) => (
              <li key={h.id}>
                <b>{h.titulo}</b>
                <span>
                  {new Date(h.fecha).toLocaleDateString('es-AR')} · {h.profesional}
                </span>
              </li>
            ))}
          </ol>
        )}
      </section>
    </aside>
  );
}
