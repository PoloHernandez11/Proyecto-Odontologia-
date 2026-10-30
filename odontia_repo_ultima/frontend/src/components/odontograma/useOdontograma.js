import { useCallback, useEffect, useReducer, useRef, useState } from 'react';
import { api } from '../../api/client';
import { HERRAMIENTAS, MODOS } from './estados';
import { aApi, estadoInicial, odontogramaReducer } from './odontogramaReducer';

/** Encapsula estado + comunicación con la API. El componente visual no sabe nada de fetch. */
export function useOdontograma(pacienteId) {
  const [state, dispatch] = useReducer(odontogramaReducer, estadoInicial);
  const [herramienta, setHerramienta] = useState(HERRAMIENTAS[0]); // caries
  const [modo, setModo] = useState(MODOS[0]); // a realizar
  const [seleccion, setSeleccion] = useState(null); // { numero, cara } de la pieza abierta en el panel
  const [historialPieza, setHistorialPieza] = useState(null); // null = cargando
  const [versiones, setVersiones] = useState([]);
  const [versionVista, setVersionVista] = useState(null); // null = la actual (editable)
  const [cargando, setCargando] = useState(true);
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState(null);

  const base = `/pacientes/${pacienteId}/odontograma`;

  const cargar = useCallback(
    async (versionId = null, signal) => {
      setCargando(true);
      setError(null);
      try {
        const [odontograma, lista] = await Promise.all([
          api(versionId ? `${base}/${versionId}` : base, { signal }),
          api(`${base}/versiones`, { signal }),
        ]);
        dispatch({ type: 'CARGAR', odontograma });
        setVersiones(lista);
        setVersionVista(versionId && lista[0]?.id !== versionId ? versionId : null);
      } catch (e) {
        if (e.name !== 'AbortError') setError(e.message);
      } finally {
        setCargando(false);
      }
    },
    [base],
  );

  useEffect(() => {
    const ctrl = new AbortController();
    cargar(null, ctrl.signal);
    return () => ctrl.abort();
  }, [cargar]);

  // Historial de la pieza seleccionada, desde la historia clínica.
  const numeroSeleccionado = seleccion?.numero ?? null;
  useEffect(() => {
    if (numeroSeleccionado === null) return undefined;
    const ctrl = new AbortController();
    setHistorialPieza(null);
    api(`${base}/piezas/${numeroSeleccionado}/historial`, { signal: ctrl.signal })
      .then(setHistorialPieza)
      .catch((e) => {
        if (e.name !== 'AbortError') setHistorialPieza([]);
      });
    return () => ctrl.abort();
  }, [base, numeroSeleccionado]);

  // La herramienta y el modo se leen desde refs para que `aplicar` sea estable:
  // cambiarlos no re-renderiza las 52 piezas memoizadas.
  const herramientaRef = useRef(herramienta);
  herramientaRef.current = herramienta;
  const modoRef = useRef(modo);
  modoRef.current = modo;
  const aplicar = useCallback(
    (numero, cara) =>
      dispatch({ type: 'APLICAR', numero, cara, herramienta: herramientaRef.current, modo: modoRef.current }),
    [],
  );

  const seleccionar = useCallback((numero, cara) => setSeleccion({ numero, cara }), []);
  const seleccionarCara = useCallback((cara) => setSeleccion((s) => (s ? { ...s, cara } : s)), []);
  const cerrarPanel = useCallback(() => setSeleccion(null), []);
  const setNota = useCallback((numero, texto) => dispatch({ type: 'NOTA', numero, texto }), []);

  const guardar = useCallback(async () => {
    setGuardando(true);
    setError(null);
    try {
      const guardado = await api(base, { method: 'POST', body: aApi(state) });
      dispatch({ type: 'CARGAR', odontograma: guardado });
      setVersiones(await api(`${base}/versiones`));
    } catch (e) {
      setError(e.message);
    } finally {
      setGuardando(false);
    }
  }, [base, state]);

  return {
    state,
    herramienta,
    setHerramienta,
    modo,
    setModo,
    aplicar,
    seleccion,
    seleccionar,
    seleccionarCara,
    cerrarPanel,
    historialPieza,
    setNota,
    setObservaciones: (texto) => dispatch({ type: 'OBSERVACIONES', texto }),
    descartar: () => dispatch({ type: 'DESCARTAR' }),
    guardar,
    versiones,
    versionVista,
    verVersion: (id) => cargar(id),
    cargando,
    guardando,
    error,
  };
}
