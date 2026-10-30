// Catálogos de la consulta clínica. Son sugerencias: los campos aceptan texto libre.

/** Diagnósticos CIE-10 frecuentes en odontología (capítulo K00–K14 y algunos códigos Z/S). */
export const CIE10 = [
  { codigo: 'Z01.2', descripcion: 'Examen odontológico' },
  { codigo: 'Z00.0', descripcion: 'Control / examen médico general' },
  { codigo: 'K00.0', descripcion: 'Anodoncia' },
  { codigo: 'K00.6', descripcion: 'Alteraciones en la erupción dentaria' },
  { codigo: 'K01.0', descripcion: 'Dientes incluidos' },
  { codigo: 'K01.1', descripcion: 'Dientes impactados' },
  { codigo: 'K02.0', descripcion: 'Caries limitada al esmalte' },
  { codigo: 'K02.1', descripcion: 'Caries de la dentina' },
  { codigo: 'K02.2', descripcion: 'Caries del cemento' },
  { codigo: 'K02.9', descripcion: 'Caries dental, no especificada' },
  { codigo: 'K03.0', descripcion: 'Atrición excesiva de los dientes' },
  { codigo: 'K03.1', descripcion: 'Abrasión de los dientes' },
  { codigo: 'K03.2', descripcion: 'Erosión de los dientes' },
  { codigo: 'K03.6', descripcion: 'Depósitos (acreciones) en los dientes / cálculo' },
  { codigo: 'K04.0', descripcion: 'Pulpitis' },
  { codigo: 'K04.1', descripcion: 'Necrosis de la pulpa' },
  { codigo: 'K04.4', descripcion: 'Periodontitis apical aguda' },
  { codigo: 'K04.5', descripcion: 'Periodontitis apical crónica' },
  { codigo: 'K04.6', descripcion: 'Absceso periapical con fístula' },
  { codigo: 'K04.7', descripcion: 'Absceso periapical sin fístula' },
  { codigo: 'K05.0', descripcion: 'Gingivitis aguda' },
  { codigo: 'K05.1', descripcion: 'Gingivitis crónica' },
  { codigo: 'K05.2', descripcion: 'Periodontitis aguda' },
  { codigo: 'K05.3', descripcion: 'Periodontitis crónica' },
  { codigo: 'K06.0', descripcion: 'Retracción gingival' },
  { codigo: 'K07.3', descripcion: 'Anomalías de la posición del diente' },
  { codigo: 'K07.6', descripcion: 'Trastornos de la articulación temporomaxilar' },
  { codigo: 'K08.1', descripcion: 'Pérdida de dientes por accidente, extracción o enfermedad periodontal' },
  { codigo: 'K08.3', descripcion: 'Raíz dental retenida' },
  { codigo: 'K12.0', descripcion: 'Estomatitis aftosa recurrente' },
  { codigo: 'S02.5', descripcion: 'Fractura de los dientes' },
];

/** Prácticas y hallazgos habituales para "Hallazgo / práctica". */
export const PRACTICAS = [
  'Consulta, examen y diagnóstico',
  'Consulta preventiva periódica',
  'Consulta de urgencia',
  'Tartrectomía y pulido',
  'Topicación con flúor',
  'Sellante de fosas y fisuras',
  'Enseñanza de técnica de cepillado',
  'Obturación con resina compuesta',
  'Obturación con amalgama',
  'Reconstrucción de ángulo',
  'Pulpotomía',
  'Endodoncia unirradicular',
  'Endodoncia multirradicular',
  'Extracción simple',
  'Extracción de pieza retenida',
  'Radiografía periapical',
  'Radiografía panorámica',
  'Perno muñón',
  'Corona',
  'Prótesis removible',
  'Blanqueamiento',
  'Control de ortodoncia',
  'Implante',
  'Caries (hallazgo)',
  'Pieza ausente (hallazgo)',
  'Restauración existente (hallazgo)',
];

export const TIPOS_REGISTRO = {
  Consulta: 'Consulta',
  Evolucion: 'Evolución',
  Diagnostico: 'Diagnóstico',
  Receta: 'Receta',
  Estudio: 'Estudio',
};

export const TIPOS_CONSULTA = { PrimeraVez: '1.ª vez', Ulterior: 'Ulterior' };

export const CARACTER = { Presuntivo: 'Presuntivo', Confirmado: 'Confirmado' };

export const SIGNOS_VITALES = [
  { name: 'taMax', label: 'T.A. máx (mmHg)', min: 40, max: 300 },
  { name: 'taMin', label: 'T.A. mín (mmHg)', min: 20, max: 200 },
  { name: 'peso', label: 'Peso (kg)', min: 1, max: 400, step: 0.1 },
  { name: 'talla', label: 'Talla (cm)', min: 30, max: 250, step: 0.1 },
  { name: 'temperatura', label: 'Temp. (°C)', min: 30, max: 45, step: 0.1 },
  { name: 'frecuenciaCardiaca', label: 'F.C. (lpm)', min: 20, max: 250 },
  { name: 'frecuenciaRespiratoria', label: 'F.R. (rpm)', min: 5, max: 80 },
  { name: 'saturacionO2', label: 'Sat. O₂ (%)', min: 50, max: 100 },
];

/** "K02.1 · Caries de la dentina" -> { codigo, descripcion } (o texto libre sin código). */
export function parsearDiagnostico(texto) {
  const t = texto.trim();
  const exacto = CIE10.find((d) => `${d.codigo} · ${d.descripcion}` === t);
  if (exacto) return { ...exacto };
  const m = t.match(/^([A-Z]\d{2}(?:\.\d)?)\s*[·\-–]?\s*(.*)$/i);
  if (m && m[2]) return { codigo: m[1].toUpperCase(), descripcion: m[2].trim() };
  return { codigo: null, descripcion: t };
}
