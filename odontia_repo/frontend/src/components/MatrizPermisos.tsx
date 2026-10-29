import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { AccionPermiso, ModuloPermiso, NivelPermiso, PermisoItemDto } from '../types/permisos';
import { ACCIONES, MODULOS, NIVELES } from '../types/permisos';

export const ETIQUETA_MODULO: Record<ModuloPermiso, { titulo: string; detalle: string }> = {
  PagosPacientes: { titulo: 'Pagos de pacientes', detalle: 'Cobros y cuenta corriente' },
  FinanzasGenerales: { titulo: 'Finanzas generales', detalle: 'Ingresos, egresos y balance' },
  LaboratorioTrabajos: { titulo: 'Laboratorio · trabajos', detalle: 'Alta, estado y costo acordado' },
  LaboratorioPagos: { titulo: 'Laboratorio · pagos', detalle: 'Pagos a proveedores' },
  LaboratorioResultadoEconomico: { titulo: 'Laboratorio · resultado económico', detalle: 'Márgenes por trabajo' },
  HistoriaClinicaOdontograma: { titulo: 'Historia clínica y odontograma', detalle: 'Información clínica' },
};

export const ETIQUETA_ACCION: Record<AccionPermiso, string> = {
  Registrar: 'Registrar',
  EditarCorregir: 'Editar / corregir',
  Anular: 'Anular',
  VerResumenes: 'Ver resúmenes',
  Confidencial: 'Confidencial',
};

/** Textos de pantalla. Los valores del backend no cambian: Permitido = "Con acceso", ConAprobacion = "Con registro". */
export const ETIQUETA_NIVEL: Record<NivelPermiso, string> = {
  SinAcceso: 'Sin acceso',
  Permitido: 'Con acceso',
  ConAprobacion: 'Con registro',
};

/** Combinaciones que hoy tienen pantalla o acción asociada en el backend. El resto se marca con *. */
const CON_EFECTO = new Set([
  'PagosPacientes:Registrar', 'PagosPacientes:Anular', 'PagosPacientes:VerResumenes',
  'FinanzasGenerales:Registrar', 'FinanzasGenerales:Anular', 'FinanzasGenerales:VerResumenes',
  'LaboratorioTrabajos:Registrar', 'LaboratorioTrabajos:EditarCorregir',
  'LaboratorioPagos:Registrar',
  'HistoriaClinicaOdontograma:Registrar', 'HistoriaClinicaOdontograma:EditarCorregir',
  'HistoriaClinicaOdontograma:VerResumenes',
]);

/** Matriz de permisos de la secretaria. Solo el odontólogo la ve y la guarda. */
export default function MatrizPermisos() {
  const [items, setItems] = useState<PermisoItemDto[] | null>(null);
  const [guardando, setGuardando] = useState(false);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let vigente = true;
    api<PermisoItemDto[]>('/permisos/secretaria')
      .then((datos) => {
        if (vigente) setItems(datos);
      })
      .catch((e: unknown) => {
        if (vigente) setError(e instanceof Error ? e.message : 'No se pudo cargar la matriz de permisos.');
      });
    return () => {
      vigente = false;
    };
  }, []);

  function cambiar(modulo: ModuloPermiso, accion: AccionPermiso, nivel: NivelPermiso) {
    setMensaje(null);
    setItems((previos) =>
      previos?.map((i) => (i.modulo === modulo && i.accion === accion ? { ...i, nivel } : i)) ?? null,
    );
  }

  async function guardar() {
    if (!items) return;
    setGuardando(true);
    setError(null);
    setMensaje(null);
    try {
      await api('/permisos/secretaria', { method: 'PUT', body: { items } });
      setMensaje('Permisos guardados.');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudieron guardar los permisos.');
    } finally {
      setGuardando(false);
    }
  }

  if (!items) return <p className="tenue">{error ?? 'Cargando permisos…'}</p>;

  return (
    <section className="tarjeta tabla-scroll">
      <h2>Permisos de la secretaria</h2>
      <p className="tenue">Define qué puede hacer la secretaria en cada módulo.</p>
      <table className="tabla">
        <thead>
          <tr>
            <th>Módulo</th>
            {ACCIONES.map((a) => (
              <th key={a}>{ETIQUETA_ACCION[a]}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {MODULOS.map((m) => (
            <tr key={m}>
              <td>
                <b>{ETIQUETA_MODULO[m].titulo}</b>
                <small style={{ display: 'block', color: '#6b7b80' }}>{ETIQUETA_MODULO[m].detalle}</small>
              </td>
              {ACCIONES.map((a) => {
                const actual = items.find((i) => i.modulo === m && i.accion === a)?.nivel ?? 'SinAcceso';
                return (
                  <td key={a}>
                    <select
                      aria-label={`${ETIQUETA_MODULO[m].titulo} · ${ETIQUETA_ACCION[a]}`}
                      value={actual}
                      onChange={(e) => cambiar(m, a, e.target.value as NivelPermiso)}
                    >
                      {NIVELES.map((n) => (
                        <option key={n} value={n}>
                          {ETIQUETA_NIVEL[n]}
                        </option>
                      ))}
                    </select>
                    {!CON_EFECTO.has(`${m}:${a}`) && <span title="Sin pantalla o acción asociada todavía"> *</span>}
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
      <p className="tenue">* Sin pantalla o acción asociada todavía.</p>

      {error && <p className="error" role="alert">{error}</p>}
      {mensaje && <p role="status">{mensaje}</p>}

      <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
        <button type="button" className="primario" onClick={guardar} disabled={guardando}>
          {guardando ? 'Guardando…' : 'Guardar permisos'}
        </button>
      </div>
    </section>
  );
}
