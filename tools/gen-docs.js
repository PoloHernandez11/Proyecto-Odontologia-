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
  Procedures: 'Prestaciones', TreatmentPlans: 'Planes de tratamiento', Budgets: 'Presupuestos', Payments: 'Pagos y cuenta corriente', Expenses: 'Egresos y resumen financiero',
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
      opId: o.operationId ?? "", path: p, method: m.toUpperCase(), tag: o.tags?.[0] ?? "Otros", summary: (o.summary ?? '').replace(/\s*\r?\n\s*/g, ' '),
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
md += `## Postman / newman\n\n` +
  `La colección \`postman/Odontia.postman_collection.json\` es un **flujo ejecutable con aserciones**: las carpetas están ordenadas por dependencia ` +
  `(autenticación → consultorio y catálogos → pacientes → turnos → odontograma → historia clínica → tratamientos → presupuestos → pagos → laboratorio → tareas → documentos → notificaciones → finanzas/dashboards → cierre de sesión). ` +
  `Cada request valida el código de estado esperado (incluidos los 4xx de los casos negativos intencionales) y la forma del JSON; los ids creados se guardan en variables de colección. ` +
  `Las fechas se calculan en scripts *pre-request* (siempre futuras) y los datos únicos (DNI, códigos, nombres) se generan por corrida, así que la colección se puede re-ejecutar.\n\n` +
  `Requisitos de la cuenta de prueba (variables de entorno \`email\` / \`password\` en \`postman/Odontia.local.postman_environment.json\`, vacías en el repositorio): ` +
  `administradora de su consultorio (registrada con \`POST /auth/register\` y correo confirmado), **profesional** y **sin 2FA**. Usar sólo contra entornos locales o de prueba: la corrida crea datos y modifica la configuración del consultorio de esa cuenta.\n\n` +
  '```bash\n' +
  'npx newman run postman/Odontia.postman_collection.json -e postman/Odontia.local.postman_environment.json \\\n' +
  '  --env-var "email=<correo>" --env-var "password=<contraseña>" --working-dir postman --delay-request 100\n' +
  '```\n\n' +
  `- \`--working-dir postman\` es necesario para el archivo de \`postman/fixtures/\` que se sube en Documentos.\n` +
  `- Rate limiting: los endpoints con política *auth* (login, 2FA, contraseñas, confirmación, invitaciones) admiten **10 req/min por IP**; la corrida usa 8. ` +
  `Esperar 1 minuto entre corridas consecutivas (o después de registrar/confirmar la cuenta). \`--delay-request 100\` evita ráfagas contra el límite global (600 req/min por usuario).\n` +
  `- La carpeta **99 · Manual** agrupa requests que necesitan datos que llegan por correo o desde una app autenticadora (confirmación, TOTP, invitaciones, restablecimiento) o que alteran la cuenta de prueba: ` +
  `se omiten salvo que se defina la variable indicada en la descripción de cada uno.\n` +
  `- La colección se regenera con \`node tools/gen-docs.js .\`; el escenario (orden, cuerpos, capturas y códigos esperados) está en \`tools/postman-flow.js\`. ` +
  `El generador falla si una operación del OpenAPI no tiene escenario o si un request usa una variable no declarada.\n\n`;
md += `## Índice\n\n` + order.filter(t => ops.some(o => o.tag === t)).map(t => `- [${tagTitles[t]}](#${tagTitles[t].toLowerCase().replace(/[^a-záéíóúñü0-9 ]/g, '').replace(/ /g, '-')})`).join('\n') + '\n\n';
for (const tag of order) {
  const list = ops.filter(o => o.tag === tag);
  if (!list.length) continue;
  md += `## ${tagTitles[tag]}\n\n| Método | Ruta | operationId | Descripción | Parámetros | Cuerpo | Respuesta | Permiso |\n|---|---|---|---|---|---|---|---|\n`;
  for (const o of list) {
    const params = o.params.filter(x => x.name !== 'version').map(x => `${x.name}${x.in === 'path' ? ' (ruta)' : ''}`).join(', ') || '—';
    const body = o.body ? (o.isMultipart ? 'multipart/form-data' : '`' + refName(o.body) + '`') : '—';
    const resp = `${o.okCode}${o.okSchema ? ' `' + refName(o.okSchema) + '`' : ''}`;
    md += `| ${o.method} | \`${o.path.replace('/api/v1', '')}\` | \`${o.opId}\` | ${o.summary || '—'} | ${params} | ${body} | ${resp} | ${o.auth} |\n`;
  }
  md += '\n';
}
fs.writeFileSync(path.join(root, 'docs', 'ENDPOINTS.md'), md, 'utf8');

// ---------- Postman ----------
// Flujo ejecutable (newman/Postman Runner) definido en tools/postman-flow.js. Cada operación del OpenAPI debe aparecer
// al menos una vez en el flujo; cada variable usada debe estar declarada (entorno o colección).
const { flow } = require('./postman-flow');
const opByKey = new Map(ops.map(o => [`${o.method} ${o.path.replace('/api/v1', '')}`, o]));
const envVars = ['baseUrl', 'email', 'password'];
const covered = new Set();

function schemaKeys(s) {
  const r = resolve(s);
  if (!r) return null;
  if (r.type === 'array' || (Array.isArray(r.type) && r.type.includes('array'))) return { array: true, keys: Object.keys(resolve(r.items)?.properties ?? {}) };
  return { array: false, keys: Object.keys(r.properties ?? {}) };
}

function renderBody(body) {
  // "{{#var}}" -> {{var}} sin comillas (números, booleanos o arreglos guardados como JSON en variables).
  return JSON.stringify(body, null, 2).replace(/"\{\{#(\w+)\}\}"/g, '{{$1}}');
}

function buildTests(st, o) {
  const expected = st.expect ?? [Number(o.okCode)];
  const lines = [
    `const expected = pm.variables.get("expectedStatus") ? JSON.parse(pm.variables.get("expectedStatus")) : ${JSON.stringify(expected)};`,
    `pm.test("Status " + expected.join("/")${st.negative ? ' + " (caso negativo intencional)"' : ''}, () => pm.expect(pm.response.code, pm.response.text().slice(0, 300)).to.be.oneOf(expected));`,
  ];
  const ok = expected.every(c => c < 300);
  if (st.binary) {
    lines.push(`pm.test("Content-Type ${st.binary}", () => pm.expect(pm.response.headers.get("Content-Type") || "").to.include("${st.binary}"));`);
    return lines;
  }
  if (st.negative) {
    lines.push('pm.test("ProblemDetails", () => { const p = pm.response.json(); pm.expect(p.status).to.eql(pm.response.code); pm.expect(p.title).to.be.a("string"); });');
    if (st.code) lines.push(`pm.test("code = ${st.code}", () => pm.expect(pm.response.json().code).to.eql("${st.code}"));`);
    return lines;
  }
  if (!ok || expected.every(c => c === 204)) return lines;
  const shape = o.okSchema ? schemaKeys(o.okSchema) : null;
  lines.push('let j = null;');
  lines.push('pm.test("Respuesta JSON", () => { j = pm.response.json(); pm.expect(j).to.not.eql(null); });');
  if (shape) {
    const name = refName(o.okSchema);
    if (shape.array) {
      lines.push(`pm.test("Forma ${name}", () => { pm.expect(j).to.be.an("array");${shape.keys.length ? ` if (j.length) pm.expect(j[0]).to.include.all.keys(${JSON.stringify(shape.keys)});` : ''} });`);
    } else if (shape.keys.length) {
      lines.push(`pm.test("Forma ${name}", () => { pm.expect(j).to.be.an("object"); pm.expect(j).to.include.all.keys(${JSON.stringify(shape.keys)}); });`);
    }
  }
  return lines;
}

function buildItem(st) {
  const o = opByKey.get(st.op);
  if (!o) throw new Error(`postman-flow: operación inexistente en el OpenAPI: ${st.op}`);
  covered.add(st.op);
  // Ruta: {param} -> valor del paso o {{param}} (nombre de variable homónimo).
  const rawPath = o.path.replace('/api/v1', '').replace(/\{(\w+)\}/g, (_, n) => {
    if (st.path?.[n] !== undefined) return st.path[n];
    if (n === 'id') throw new Error(`postman-flow: ${st.op} necesita path.id explícito`);
    return `{{${n}}}`;
  });
  const query = o.params.filter(x => x.in === 'query').map(x => {
    const v = st.query?.[x.name];
    return { key: x.name, value: v ?? '', disabled: v === undefined };
  });
  const qs = query.filter(q => !q.disabled).map(q => `${q.key}=${q.value}`).join('&');
  const headers = [...(st.headers ?? [])];
  const req = {
    method: o.method,
    header: headers,
    url: { raw: `{{baseUrl}}${rawPath}${qs ? '?' + qs : ''}`, host: ['{{baseUrl}}'], path: rawPath.split('/').filter(Boolean), query },
    description: [o.summary, st.note, `Permiso: ${o.auth.replace(/`/g, '')}`, st.manual ? `MANUAL: se omite salvo que se defina la variable "${st.manual}".` : null].filter(Boolean).join('\n\n'),
  };
  if (st.noAuth) req.auth = { type: 'noauth' };
  if (st.file) {
    req.body = { mode: 'formdata', formdata: [
      { key: 'file', type: 'file', src: 'fixtures/documento.pdf' },
      { key: 'category', value: st.file.category, type: 'text' },
      { key: 'description', value: st.file.description ?? '', type: 'text' },
    ] };
  } else if (st.body !== undefined) {
    headers.push({ key: 'Content-Type', value: 'application/json' });
    req.body = { mode: 'raw', raw: renderBody(st.body), options: { raw: { language: 'json' } } };
  } else if (o.body && !o.isMultipart && o.method !== 'GET') {
    throw new Error(`postman-flow: ${st.op} requiere cuerpo`);
  }
  const pre = [];
  if (st.manual) pre.push(`if (!pm.variables.get("${st.manual}")) { console.log("Omitido (manual): defina ${st.manual}"); pm.execution.skipRequest(); }`);
  pre.push(...(st.pre ?? []));
  const test = buildTests(st, o);
  for (const [v, expr] of Object.entries(st.capture ?? {}))
    test.push(`if (pm.response.code < 300) { const v = (() => { try { return ${expr}; } catch (e) { return undefined; } })(); pm.test("Captura ${v}", () => pm.expect(v, "${v}").to.not.be.oneOf([undefined, null])); pm.collectionVariables.set("${v}", v); }`);
  test.push(...(st.test ?? []));
  const event = [];
  if (pre.length) event.push({ listen: 'prerequest', script: { type: 'text/javascript', exec: pre } });
  event.push({ listen: 'test', script: { type: 'text/javascript', exec: test } });
  return { name: st.name ?? `${o.method} ${o.path.replace('/api/v1', '')}`, request: req, event };
}

const folders = flow.map(f => ({ name: f.name, description: f.description, item: f.steps.map(buildItem) }));
const missing = ops.map(o => `${o.method} ${o.path.replace('/api/v1', '')}`).filter(k => !covered.has(k));
if (missing.length) throw new Error('postman-flow: operaciones sin escenario:\n  ' + missing.join('\n  '));

// Variables: todas las usadas ({{x}} y pm.collectionVariables.set("x")) deben quedar declaradas.
const usedIn = items => new Set([...JSON.stringify(items).matchAll(/\{\{#?(\w+)\}\}/g)].map(m => m[1]));
// Scripts: pm.collectionVariables.set("x") y el atajo set("x") del script inicial (no pm.variables.set, que es local).
const setInScripts = new Set([...JSON.stringify(folders).matchAll(/(?:collectionVariables\.|[^.\w])set\(\\"(\w+)\\"/g)].map(m => m[1]));
const isManual = f => f.name.startsWith('99');
const automatic = usedIn(folders.filter(f => !isManual(f)));
const undefinedVars = [...automatic].filter(v => !envVars.includes(v) && !setInScripts.has(v));
if (undefinedVars.length) throw new Error('postman-flow: variables usadas que ningún script define: ' + undefinedVars.join(', '));
// Las variables propias de la carpeta Manual se declaran vacías (las completa quien la ejecute).
const manualVars = new Set([...usedIn(folders.filter(isManual)), ...flow.filter(isManual).flatMap(f => f.steps.map(st => st.manual))]);
const collectionVars = [...new Set([...setInScripts, ...manualVars])].filter(v => v && !envVars.includes(v)).sort();

const collection = {
  info: {
    name: 'Odontia API v1',
    description: [
      'Colección generada desde OpenAPI por tools/gen-docs.js (escenario en tools/postman-flow.js). Es un flujo ejecutable con aserciones: ejecutar las carpetas EN ORDEN (Runner de Postman o newman).',
      'Entorno: baseUrl, email y password de una cuenta de PRUEBA administradora de su consultorio, profesional y sin 2FA. Nunca contra producción.',
      'El login guarda accessToken (Bearer en toda la colección), csrfToken y el refresh token (cookie odontia_rt, enviada explícitamente en refresh/switch/logout porque la cookie es Secure).',
      'Rate limiting: la política "auth" admite 10 req/min por IP (esta corrida usa 8); esperar 1 minuto entre corridas. Sugerido: newman --delay-request 100 --working-dir postman.',
      'La carpeta "99 · Manual" se omite salvo que se definan sus variables (tokens de correo, códigos TOTP, etc.).',
    ].join('\n\n'),
    schema: 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json',
  },
  auth: { type: 'bearer', bearer: [{ key: 'token', value: '{{accessToken}}', type: 'string' }] },
  event: [{ listen: 'prerequest', script: { type: 'text/javascript', exec: [
    'if (!pm.environment.get("email") && !pm.variables.get("email")) throw new Error("Defina las variables de entorno email y password (cuenta de prueba).");',
    'pm.variables.unset("expectedStatus");',
  ] } }],
  variable: collectionVars.map(k => ({ key: k, value: '' })),
  item: folders,
};
fs.writeFileSync(path.join(root, 'postman', 'Odontia.postman_collection.json'), JSON.stringify(collection, null, 2), 'utf8');

const env = {
  name: 'Odontia - Local', _postman_variable_scope: 'environment',
  values: [
    { key: 'baseUrl', value: 'http://localhost:5221/api/v1', type: 'default', enabled: true },
    { key: 'email', value: '', type: 'default', enabled: true },
    { key: 'password', value: '', type: 'secret', enabled: true },
  ],
};
fs.writeFileSync(path.join(root, 'postman', 'Odontia.local.postman_environment.json'), JSON.stringify(env, null, 2), 'utf8');
const requests = folders.reduce((n, f) => n + f.item.length, 0);
console.log(`ENDPOINTS.md y Postman generados: ${ops.length} operaciones, ${folders.length} carpetas, ${requests} requests, ${collectionVars.length} variables de colección`);
