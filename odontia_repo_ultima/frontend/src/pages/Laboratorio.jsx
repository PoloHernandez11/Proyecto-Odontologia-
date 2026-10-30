import TrabajosLaboratorio from '../components/TrabajosLaboratorio';

/** Entradas y salidas de trabajos al laboratorio dental. */
export default function Laboratorio() {
  return (
    <div className="pagina">
      <div>
        <h1>Laboratorio dental</h1>
        <p className="subtitulo">Seguimiento de los trabajos enviados al laboratorio: salida, ingreso, colocación y pago</p>
      </div>
      <TrabajosLaboratorio />
    </div>
  );
}
