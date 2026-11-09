// Genera docs/ENDPOINTS.md y la colección Postman a partir de docs/openapi.v1.json
// Uso (desde la carpeta Odontia): node tools/gen-docs.js .
const fs = require('fs');
const path = require('path');
const root = process.argv[2];
const spec = JSON.parse(fs.readFileSync(path.join(root, 'docs', 'openapi.v1.json'), 'utf8'));
const schemas = spec.components?.schemas ?? {};

const tagTitles = {
  Auth: 'Autenticación y sesiones', Consultorio: 'Consultorio', Users: 'Usuarios, roles e invitaciones', Profile: 'Perfil',
  Audit: 'Auditoría', Patients: 'Pacientes', HealthInsurances: 'Obras sociales', Agenda: 'Agenda', Appointments: 'Turnos',
  Tasks: 'Tareas', ClinicalRecord: 'Historia clínica', OdontogramCatalog: 'Odontograma · catálogo', Odontogram: 'Odontograma',
  Procedures: 'Prestaciones', TreatmentPlans: 'Planes de tratamiento', Budgets: 'Presupuestos', Payments: 'Pagos y cuenta corriente',
  Laboratories: 'Laboratorios', LabWorks: 'Trabajos de laboratorio', Documents: 'Documentos y consentimientos',
  Notifications: 'Notificaciones y mensajes', Dashboard: 'Dashboards',
};
const order = Object.keys(tagTitles);

function resolve(s) {
  if (s && s.$ref) return schemas[s.$ref.split('/').pop()];
  return s;
}
function refName(s) { return s?.$ref ? s.$ref.split('/').pop() : (s?.type === 'array' && s.items?.$ref ? s.items.$ref.split('/').pop() + '[]' : s?.type ?? ''); }

function sample(s, name, depth = 0) {
  s = resolve(s);
  if (!s || depth > 4) return null;
  if (s.oneOf) return sample(s.oneOf[0], name, depth);
  if (s.allOf) return sample(s.allOf[0], name, depth);
  if (s.enum) return s.enum[0];
  const t = Array.isArray(s.type) ? s.type.find(x => x !== 'null') : s.type;
  if (t === 'object' || s.properties) {
    const o = {};
    for (const [k, v] of Object.entries(s.properties ?? {})) o[k] = sample(v, k, depth + 1);
    return o;
  }
  if (t === 'array') return [sample(s.items, name, depth + 1)].filter(x => x !== null);
  if (t === 'string') {
    if (s.format === 'uuid') return name && /id$/i.test(name) ? `{{${name}}}` : '{{id}}';
    if (s.format === 'date') return '2026-10-20';
    if (s.format === 'date-time') return '2026-10-20T10:30:00-03:00';
    if (/^(email)$/i.test(name)) return 'usuario@ejemplo.com';
    if (/password/i.test(name)) return 'ClaveSegura2026';
    if (/time$/i.test(name)) return '09:00';
    return '';
  }
  if (t === 'integer') return 0;
  if (t === 'number') return 0;
  if (t === 'boolean') return false;
  return null;
}

const ops = [];
for (const [p, methods] of Object.entries(spec.paths)) {
  for (const [m, o] of Object.entries(methods)) {
    const perms = o['x-permissions'] ?? [];
    const desc = o.description ?? '';
    const anonymous = desc.includes('Público (sin autenticación)');
    const auth = anonymous ? 'Público' : perms.length ? perms.map(x => '`' + x + '`').join(', ') : 'Autenticado';
    const body = o.requestBody?.content?.['application/json']?.schema ?? o.requestBody?.content?.['multipart/form-data']?.schema;
    const isMultipart = !!o.requestBody?.content?.['multipart/form-data'];
    const okCode = Object.keys(o.responses ?? {}).find(c => c.startsWith('2')) ?? '200';
    const okSchema = o.responses?.[okCode]?.content?.['application/json']?.schema;
    ops.push({
      path: p, method: m.toUpperCase(), tag: o.tags?.[0] ?? 'Otros', summary: (o.summary ?? '').replace(/\n/g, ' '),
      auth, anonymous, perms, body, isMultipart, okCode, okSchema,
      params: (o.parameters ?? []).map(x => ({ name: x.name, in: x.in, required: !!x.required })),
      codes: Object.keys(o.responses ?? {}),
    });
  }
}
ops.sort((a, b) => (order.indexOf(a.tag) - order.indexOf(b.tag)) || a.path.localeCompare(b.path));

// ---------- ENDPOINTS.md ----------
let md = `# Endpoints de la API Odontia v1\n\n> Generado automáticamente a partir del documento OpenAPI (\`docs/openapi.v1.json\`).\n> Total: **${ops.length} operaciones**. Base: \`/api/v1\`. Errores: ProblemDetails (ver \`docs/FRONTEND_INTEGRATION.md\`).\n\n`;
md += `Códigos comunes en todas las operaciones autenticadas: \`401\` (sin sesión o sesión invalidada), \`403\` (sin permiso / sin consultorio activo), \`400\` (validación), \`404\` (no existe **o pertenece a otro consultorio**), \`409\` (conflicto/concurrencia), \`422\` (regla de negocio), \`429\` (rate limit).\n\n`;
md += `## Índice\n\n` + order.filter(t => ops.some(o => o.tag === t)).map(t => `- [${tagTitles[t]}](#${tagTitles[t].toLowerCase().replace(/[^a-záéíóúñü0-9 ]/g, '').replace(/ /g, '-')})`).join('\n') + '\n\n';
for (const tag of order) {
  const list = ops.filter(o => o.tag === tag);
  if (!list.length) continue;
  md += `## ${tagTitles[tag]}\n\n| Método | Ruta | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |\n|---|---|---|---|---|---|---|\n`;
  for (const o of list) {
    const params = o.params.filter(x => x.name !== 'version').map(x => `${x.name}${x.in === 'path' ? ' (ruta)' : ''}`).join(', ') || '—';
    const body = o.body ? (o.isMultipart ? 'multipart/form-data' : '`' + refName(o.body) + '`') : '—';
    const resp = `${o.okCode}${o.okSchema ? ' `' + refName(o.okSchema) + '`' : ''}`;
    md += `| ${o.method} | \`${o.path.replace('/api/v1', '')}\` | ${o.summary || '—'} | ${params} | ${body} | ${resp} | ${o.auth} |\n`;
  }
  md += '\n';
}
fs.writeFileSync(path.join(root, 'docs', 'ENDPOINTS.md'), md, 'utf8');

// ---------- Postman ----------
const csrfPaths = ['/api/v1/auth/refresh', '/api/v1/auth/logout', '/api/v1/auth/switch-consultorio'];
const saveSession = [
  'const r = pm.response.json();',
  'const s = r.result ?? r;',
  'if (s && s.session) {',
  '  pm.collectionVariables.set("accessToken", s.session.accessToken);',
  '  pm.collectionVariables.set("csrfToken", s.csrfToken);',
  '  pm.collectionVariables.set("consultorioId", s.session.activeConsultorioId ?? "");',
  '  pm.collectionVariables.set("userId", s.session.user.id);',
  '}',
  'if (r.requiresTwoFactor) pm.collectionVariables.set("twoFactorChallengeToken", r.twoFactorChallengeToken);',
];
const saveId = (v) => [`if (pm.response.code < 300) { const j = pm.response.json(); if (j && j.id) pm.collectionVariables.set("${v}", j.id); }`];
const idVarByPath = [
  [/\/patients$/, 'patientId'], [/\/appointments$/, 'appointmentId'], [/\/tasks$/, 'taskId'], [/\/budgets$/, 'budgetId'],
  [/\/payments$/, 'paymentId'], [/\/lab-works$/, 'labWorkId'], [/\/laboratories$/, 'laboratoryId'], [/\/procedures$/, 'procedureId'],
  [/\/treatment-plans$/, 'planId'], [/\/entries$/, 'entryId'], [/\/documents$/, 'documentId'], [/\/consents$/, 'consentId'],
  [/\/roles$/, 'roleId'], [/\/gabinetes$/, 'gabineteId'], [/\/health-insurances$/, 'healthInsuranceId'], [/\/diagnoses$/, 'diagnosisId'],
];

const folders = order.filter(t => ops.some(o => o.tag === t)).map(tag => ({
  name: tagTitles[tag],
  item: ops.filter(o => o.tag === tag).map(o => {
    const rawPath = o.path.replace(/\{(\w+)\}/g, (_, n) => `{{${n}}}`).replace('/api/v1', '');
    const query = o.params.filter(x => x.in === 'query').map(x => ({ key: x.name, value: '', disabled: !x.required }));
    const headers = [];
    if (csrfPaths.includes(o.path)) headers.push({ key: 'X-CSRF-Token', value: '{{csrfToken}}' });
    const req = {
      method: o.method,
      header: headers,
      url: { raw: `{{baseUrl}}${rawPath}`, host: ['{{baseUrl}}'], path: rawPath.split('/').filter(Boolean), query },
      description: `${o.summary}\n\nPermiso: ${o.auth.replace(/`/g, '')}`,
    };
    if (o.anonymous) req.auth = { type: 'noauth' };
    if (o.body && !o.isMultipart) {
      req.header.push({ key: 'Content-Type', value: 'application/json' });
      req.body = { mode: 'raw', raw: JSON.stringify(sample(o.body), null, 2), options: { raw: { language: 'json' } } };
    } else if (o.isMultipart) {
      req.body = { mode: 'formdata', formdata: [{ key: 'file', type: 'file', src: [] }, { key: 'category', value: 'Radiograph', type: 'text' }, { key: 'description', value: '', type: 'text' }] };
    }
    const events = [];
    if (['/api/v1/auth/login', '/api/v1/auth/login/2fa', '/api/v1/auth/refresh', '/api/v1/auth/switch-consultorio'].includes(o.path))
      events.push({ listen: 'test', script: { type: 'text/javascript', exec: saveSession } });
    else if (o.method === 'POST') {
      const v = idVarByPath.find(([re]) => re.test(o.path));
      if (v) events.push({ listen: 'test', script: { type: 'text/javascript', exec: saveId(v[1]) } });
    }
    return { name: `${o.method} ${rawPath}`, request: req, event: events };
  }),
}));

const vars = ['baseUrl', 'accessToken', 'csrfToken', 'consultorioId', 'userId', 'twoFactorChallengeToken', 'professionalUserId', 'memberId', 'id']
  .concat(idVarByPath.map(([, v]) => v)).concat(['alertId', 'findingId', 'itemId', 'sessionId', 'token', 'toothCode', 'invitationId']);
const collection = {
  info: {
    name: 'Odontia API v1',
    description: 'Colección generada desde OpenAPI. 1) Ejecutar "Autenticación y sesiones / POST /auth/login" (guarda accessToken y csrfToken). 2) El resto usa Bearer {{accessToken}}. 3) Refresh/Logout envían X-CSRF-Token y la cookie odontia_rt (Postman la conserva automáticamente para el dominio).',
    schema: 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json',
  },
  auth: { type: 'bearer', bearer: [{ key: 'token', value: '{{accessToken}}', type: 'string' }] },
  variable: [...new Set(vars)].map(k => ({ key: k, value: k === 'baseUrl' ? 'http://localhost:5221/api/v1' : '' })),
  item: folders,
};
fs.writeFileSync(path.join(root, 'postman', 'Odontia.postman_collection.json'), JSON.stringify(collection, null, 2), 'utf8');

const env = {
  name: 'Odontia - Local', _postman_variable_scope: 'environment',
  values: [{ key: 'baseUrl', value: 'http://localhost:5221/api/v1', enabled: true }],
};
fs.writeFileSync(path.join(root, 'postman', 'Odontia.local.postman_environment.json'), JSON.stringify(env, null, 2), 'utf8');
console.log(`ENDPOINTS.md y Postman generados: ${ops.length} operaciones, ${folders.length} carpetas`);
