// Genera los tipos TypeScript del frontend a partir del contrato OpenAPI.
// Uso (desde la carpeta Odontia):  node tools/gen-types.js
// Entrada:  docs/openapi.v1.json     Salida:  frontend/src/app/api/schema.ts
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const spec = JSON.parse(fs.readFileSync(path.join(root, 'docs', 'openapi.v1.json'), 'utf8'));
const schemas = spec.components.schemas;

const refName = ref => ref.split('/').pop();

function tsType(schema) {
  if (!schema) return 'unknown';
  if (schema.$ref) return refName(schema.$ref);
  const variants = schema.allOf || schema.oneOf || schema.anyOf;
  if (variants) {
    const joined = variants.map(tsType).join(schema.allOf ? ' & ' : ' | ');
    return schema.nullable ? `(${joined}) | null` : joined;
  }
  let type;
  if (schema.enum) type = schema.enum.map(value => JSON.stringify(value)).join(' | ');
  else if (schema.type === 'array') type = `${wrap(tsType(schema.items))}[]`;
  else if (schema.type === 'integer' || schema.type === 'number') type = 'number';
  else if (schema.type === 'boolean') type = 'boolean';
  else if (schema.type === 'string') type = schema.format === 'binary' ? 'Blob' : 'string';
  else if (schema.type === 'object' || schema.properties || schema.additionalProperties) type = objectType(schema, '  ');
  else type = 'unknown';
  return schema.nullable ? `${wrap(type)} | null` : type;
}

const wrap = type => (/[|&]/.test(type) ? `(${type})` : type);

function objectType(schema, indent) {
  const required = new Set(schema.required || []);
  const lines = Object.entries(schema.properties || {}).map(([name, property]) => {
    // En las respuestas el backend siempre envía todas las propiedades; sólo las "nullable" pueden venir en null.
    const optional = required.has(name) || !property.nullable ? '' : '?';
    return `${indent}${/^[A-Za-z_$][\w$]*$/.test(name) ? name : JSON.stringify(name)}${optional}: ${tsType(property)};`;
  });
  if (schema.additionalProperties && typeof schema.additionalProperties === 'object') {
    lines.push(`${indent}[key: string]: ${tsType(schema.additionalProperties)};`);
  }
  if (!lines.length) return 'Record<string, unknown>';
  return `{\n${lines.join('\n')}\n${indent.slice(2)}}`;
}

const out = [
  '// ARCHIVO GENERADO. No editar a mano: ejecutar `node tools/gen-types.js` desde la carpeta Odontia.',
  `// Origen: docs/openapi.v1.json (${spec.info.title} ${spec.info.version})`,
  '/* eslint-disable */',
  ''
];

for (const [name, schema] of Object.entries(schemas)) {
  if (schema.enum) out.push(`export type ${name} = ${schema.enum.map(value => JSON.stringify(value)).join(' | ')};`, '');
  else if (schema.type === 'object' || schema.properties) {
    const body = objectType(schema, '  ');
    out.push(body.startsWith('{') ? `export interface ${name} ${body}` : `export type ${name} = ${body};`, '');
  } else out.push(`export type ${name} = ${tsType(schema)};`, '');
}

const target = path.join(root, 'frontend', 'src', 'app', 'api', 'schema.ts');
fs.mkdirSync(path.dirname(target), { recursive: true });
fs.writeFileSync(target, out.join('\n'));
console.log(`${Object.keys(schemas).length} tipos -> ${path.relative(root, target)}`);
