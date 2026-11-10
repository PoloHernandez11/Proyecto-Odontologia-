// Datos de demostración para desarrollo local. Crea (una sola vez) un consultorio de prueba con una odontóloga
// administradora, una secretaria, pacientes, turnos de hoy, tareas, trabajos de laboratorio y cobros.
//
// Uso (con la API corriendo en Development):  node tools/seed-dev.js
//
// Las credenciales de las cuentas de prueba se generan al azar y se guardan en tools/.seed-dev.json,
// que está excluido de git. Nunca usar este script contra un entorno que no sea de desarrollo local.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const API = process.env.ODONTIA_API ?? 'http://localhost:5221/api/v1';
const OUTBOX = path.resolve(__dirname, '..', 'storage', 'dev-outbox');
const CREDENCIALES = path.join(__dirname, '.seed-dev.json');

if (!/^https?:\/\/(localhost|127\.0\.0\.1)(:\d+)?\//.test(API)) {
  console.error('Este script sólo se ejecuta contra una API local.');
  process.exit(1);
}

function clave() {
  // 18 caracteres con mayúsculas, minúsculas y dígitos garantizados (política de contraseñas del backend).
  return 'Od' + crypto.randomBytes(12).toString('base64url').replace(/[-_]/g, 'x') + '7a';
}

function credenciales() {
  if (fs.existsSync(CREDENCIALES)) return JSON.parse(fs.readFileSync(CREDENCIALES, 'utf8'));
  const sufijo = crypto.randomBytes(3).toString('hex');
  const datos = {
    odontologa: { email: `laura.mendez.${sufijo}@odontia.local`, password: clave() },
    secretaria: { email: `carla.sosa.${sufijo}@odontia.local`, password: clave() }
  };
  fs.writeFileSync(CREDENCIALES, JSON.stringify(datos, null, 2) + '\n');
  return datos;
}

let token = null;
async function api(metodo, ruta, cuerpo) {
  const respuesta = await fetch(API + ruta, {
    method: metodo,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo)
  });
  const texto = await respuesta.text();
  const datos = texto ? JSON.parse(texto) : null;
  if (!respuesta.ok) {
    const detalle = datos?.errors ? Object.values(datos.errors).flat().join(' ') : datos?.detail ?? texto;
    throw Object.assign(new Error(`${metodo} ${ruta} -> ${respuesta.status} ${datos?.code ?? ''} ${detalle}`), { status: respuesta.status, code: datos?.code });
  }
  return datos;
}
/** Ejecuta un paso opcional: si el backend lo rechaza, avisa y sigue. */
async function intentar(descripcion, accion) {
  try { return await accion(); } catch (error) { console.warn(`  (omitido) ${descripcion}: ${error.message}`); return null; }
}

/** Busca en la bandeja de desarrollo el último correo enviado a una dirección y devuelve los parámetros de su enlace. */
async function enlaceDelCorreo(destinatario, fragmento) {
  for (let intento = 0; intento < 20; intento++) {
    const archivos = fs.existsSync(OUTBOX) ? fs.readdirSync(OUTBOX).filter(nombre => nombre.endsWith('.eml')).sort().reverse() : [];
    for (const archivo of archivos) {
      const contenido = fs.readFileSync(path.join(OUTBOX, archivo), 'utf8');
      if (!contenido.toLowerCase().includes(`to: ${destinatario.toLowerCase()}`)) continue;
      const enlace = contenido.match(new RegExp(`https?://\\S*${fragmento}\\?\\S+`));
      if (enlace) return new URL(enlace[0]).searchParams;
    }
    await new Promise(resolver => setTimeout(resolver, 300));
  }
  throw new Error(`No apareció el correo para ${destinatario} en ${OUTBOX}`);
}

async function iniciarSesion({ email, password }) {
  const respuesta = await api('POST', '/auth/login', { email, password });
  token = respuesta.result.session.accessToken;
  return respuesta.result.session;
}

const hoyA = (hora, minutos = 0, dias = 0) => { const fecha = new Date(); fecha.setDate(fecha.getDate() + dias); fecha.setHours(hora, minutos, 0, 0); return fecha.toISOString(); };
const dia = dias => { const fecha = new Date(); fecha.setDate(fecha.getDate() + dias); return `${fecha.getFullYear()}-${String(fecha.getMonth() + 1).padStart(2, '0')}-${String(fecha.getDate()).padStart(2, '0')}`; };

(async () => {
  const cuentas = credenciales();

  // 1. Consultorio y odontóloga administradora
  let sesion;
  try { sesion = await iniciarSesion(cuentas.odontologa); console.log('La cuenta de demostración ya existía.'); }
  catch (error) {
    if (error.code !== 'invalid_credentials') throw error;
    console.log('Registrando el consultorio de demostración…');
    await api('POST', '/auth/register', {
      ...cuentas.odontologa, firstName: 'Laura', lastName: 'Méndez', consultorioName: 'Consultorio Méndez (demo)', title: 'Dra.',
      specialty: 'Odontología general', licenseNumber: 'M.P. 12345', timeZoneId: 'America/Argentina/Buenos_Aires', isProfessional: true
    });
    const enlace = await enlaceDelCorreo(cuentas.odontologa.email, 'confirmar-correo');
    await api('POST', '/auth/confirm-email', { userId: enlace.get('userId'), token: enlace.get('token') });
    sesion = await iniciarSesion(cuentas.odontologa);
  }
  const profesional = sesion.user.id;

  const existentes = await api('GET', '/patients?pageSize=1');
  if (existentes.totalCount > 0) { console.log(`El consultorio ya tiene ${existentes.totalCount} pacientes: no se cargan datos de nuevo.`); }
  else {
    console.log('Cargando datos de demostración…');
    // 2. Estructura del consultorio
    const gabinete1 = await intentar('gabinete 1', () => api('POST', '/consultorio/gabinetes', { name: 'Consultorio 1', isActive: true }));
    await intentar('gabinete 2', () => api('POST', '/consultorio/gabinetes', { name: 'Consultorio 2', isActive: true }));
    await intentar('horarios', () => api('PUT', `/agenda/professionals/${profesional}/working-hours`, {
      slots: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'].map(dayOfWeek => ({ dayOfWeek, startTime: '08:00', endTime: '20:00', gabineteId: null }))
    }));
    const obras = {};
    for (const nombre of ['OSDE', 'Swiss Medical', 'Galeno']) obras[nombre] = (await intentar(`obra social ${nombre}`, () => api('POST', '/health-insurances', { name: nombre, isActive: true })))?.id ?? null;
    const laboratorios = [];
    for (const nombre of ['Dental Sur', 'Lab. Belgrano']) laboratorios.push(await intentar(`laboratorio ${nombre}`, () => api('POST', '/laboratories', { name: nombre, isActive: true })));

    // 3. Pacientes
    const sinAntecedentes = { diabetes: false, hypertension: false, heartDisease: false, anticoagulated: false, pregnant: false, asthma: false, hepatitis: false, smoker: false };
    const fichas = [
      ['Martín', 'Rodríguez', '32400112', '+54 9 11 5523-8810', 'OSDE', '1984-03-12', { ...sinAntecedentes, allergies: 'Penicilina' }],
      ['Sofía', 'Herrera', '40112334', '+54 9 11 4410-2231', 'OSDE', '1997-07-02', sinAntecedentes],
      ['Jorge', 'Castro', '28567890', '+54 9 11 6021-7781', 'Galeno', '1980-11-23', sinAntecedentes],
      ['Lucía', 'Fernández', '36774520', '+54 9 11 3388-1290', null, '1992-01-30', sinAntecedentes],
      ['Valentina', 'López', '43588910', '+54 9 11 2201-9934', 'OSDE', '2001-09-14', sinAntecedentes],
      ['Diego', 'Romero', '39500211', '+54 9 11 5099-4412', 'Swiss Medical', '1996-05-08', { ...sinAntecedentes, anticoagulated: true }],
      ['Paula', 'Giménez', '33221640', '+54 9 11 4776-0021', 'Galeno', '1988-12-19', { ...sinAntecedentes, diabetes: true }],
      ['Ricardo', 'Sosa', '29190566', '+54 9 11 4550-1908', 'OSDE', '1982-04-27', sinAntecedentes]
    ];
    const pacientes = [];
    for (const [firstName, lastName, documentNumber, phone, obra, birthDate, medicalHistory] of fichas) {
      pacientes.push(await api('POST', '/patients', {
        patient: { firstName, lastName, documentNumber, phone, birthDate, sex: 'NotSpecified', email: `${firstName}.${lastName}@correo.local`.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, ''), healthInsuranceId: obra ? obras[obra] : null },
        medicalHistory
      }));
    }
    const [martin, sofia, jorge, lucia, valentina, diego, paula, ricardo] = pacientes;

    // 4. Turnos de hoy y de mañana
    const turno = (paciente, hora, minutos, motivo, dias = 0, duracion = 45) => intentar(`turno de ${paciente.firstName}`, () => api('POST', '/appointments', {
      patientId: paciente.id, professionalUserId: profesional, gabineteId: gabinete1?.id ?? null, start: hoyA(hora, minutos, dias),
      durationMinutes: duracion, reason: motivo, isOverbooking: false, confirmed: false
    }));
    const t1 = await turno(sofia, 9, 0, 'Control semestral');
    const t2 = await turno(martin, 10, 0, 'Endodoncia pieza 36 · 2ª sesión', 0, 60);
    const t3 = await turno(jorge, 11, 30, 'Prueba de corona 21');
    await turno(diego, 14, 0, 'Extracción pieza 48', 0, 60);
    await turno(paula, 16, 0, 'Obturación 14 y 15');
    await turno(valentina, 9, 30, 'Consulta ortodoncia', 1);
    await turno(lucia, 11, 0, 'Limpieza y flúor', 1);
    if (t1) await intentar('confirmar turno', () => api('POST', `/appointments/${t1.id}/confirm`));
    if (t2) await intentar('confirmar turno', () => api('POST', `/appointments/${t2.id}/confirm`));
    if (t3) await intentar('confirmar turno', () => api('POST', `/appointments/${t3.id}/confirm`));

    // 5. Tareas
    for (const [title, priority, category, dias] of [
      ['Confirmar los turnos de mañana', 'High', 'Appointments', 0],
      ['Llamar a Jorge Castro por su presupuesto', 'Medium', 'Administrative', 0],
      ['Retirar trabajo de Dental Sur', 'Medium', 'Laboratory', 2],
      ['Reponer guantes y barbijos', 'Low', 'Supplies', 5]
    ]) await intentar(`tarea ${title}`, () => api('POST', '/tasks', { title, priority, category, assignedToUserId: profesional, dueAt: hoyA(18, 0, dias) }));

    // 6. Laboratorio
    if (laboratorios[0] && laboratorios[1]) {
      const trabajo = (paciente, laboratorio, workType, toothCode, dias, cost) => intentar(`trabajo ${workType}`, () => api('POST', '/lab-works', {
        patientId: paciente.id, laboratoryId: laboratorio.id, professionalUserId: profesional, workType, toothCode, estimatedDeliveryDate: dia(dias), cost
      }));
      const corona = await trabajo(jorge, laboratorios[0], 'Corona de zirconio', 21, 1, 85000);
      await trabajo(ricardo, laboratorios[1], 'Corona sobre implante', 46, 6, 120000);
      await trabajo(sofia, laboratorios[0], 'Placa de contención', null, 3, 45000);
      if (corona) {
        await intentar('enviar trabajo', () => api('POST', `/lab-works/${corona.id}/status`, { status: 'Sent' }));
        await intentar('trabajo en proceso', () => api('POST', `/lab-works/${corona.id}/status`, { status: 'InProduction' }));
      }
    }

    // 7. Conceptos de cobro (presupuestos aprobados) y pagos
    const concepto = async (paciente, description, unitPrice) => {
      const borrador = await intentar(`presupuesto ${description}`, () => api('POST', `/patients/${paciente.id}/budgets`, { discountPercent: 0, importPlanItems: false, items: [{ description, quantity: 1, unitPrice, discountPercent: 0 }] }));
      if (!borrador) return null;
      await intentar('emitir presupuesto', () => api('POST', `/budgets/${borrador.id}/issue`));
      return intentar('aprobar presupuesto', () => api('POST', `/budgets/${borrador.id}/approve`));
    };
    const pago = (presupuesto, amount, method) => presupuesto && intentar('pago', () => api('POST', '/payments', { patientId: presupuesto.patientId, budgetId: presupuesto.id, amount, method }));
    await pago(await concepto(martin, 'Endodoncia pieza 36', 185000), 100000, 'BankTransfer');
    await pago(await concepto(jorge, 'Corona de zirconio pieza 21', 160000), 80000, 'DebitCard');
    await pago(await concepto(sofia, 'Control semestral y limpieza', 30000), 30000, 'Cash');
    await concepto(paula, 'Obturaciones piezas 14 y 15', 95000);

    // 8. Historia clínica y odontograma
    await intentar('evolución', () => api('POST', `/patients/${martin.id}/clinical-record/entries`, {
      type: 'Consultation', title: 'Dolor al masticar', description: 'Diagnóstico: Caries profunda en pieza 36\nTratamiento: Endodoncia · primera sesión',
      indications: 'Continuar el tratamiento en la próxima consulta.', teeth: [36]
    }));
    await intentar('odontograma', async () => {
      const catalogo = await api('GET', '/odontogram/catalog');
      const caries = catalogo.conditions.find(condicion => /caries/i.test(condicion.name) && condicion.scope !== 'Tooth');
      if (caries) await api('POST', `/patients/${martin.id}/odontogram/findings`, { items: [{ toothCode: 36, surface: 'Occlusal', conditionId: caries.id, status: 'Existing', notes: 'Caries profunda' }] });
    });
  }

  // 9. Secretaria (por invitación), para probar el perfil de recepción
  const miembros = await api('GET', '/users?includeInactive=true');
  if (!miembros.some(miembro => miembro.email.toLowerCase() === cuentas.secretaria.email.toLowerCase())) {
    await intentar('secretaria', async () => {
      const roles = await api('GET', '/roles');
      const rol = roles.find(item => item.name === 'Secretaria');
      await api('POST', '/invitations', { email: cuentas.secretaria.email, roleIds: [rol.id], isProfessional: false });
      const enlace = await enlaceDelCorreo(cuentas.secretaria.email, 'aceptar-invitacion');
      token = null;
      await api('POST', '/auth/invitations/accept', { token: enlace.get('token'), firstName: 'Carla', lastName: 'Sosa', password: cuentas.secretaria.password });
      console.log('Se creó la cuenta de la secretaria.');
    });
  }

  console.log(`\nListo. Las credenciales de las cuentas de prueba están en ${path.relative(process.cwd(), CREDENCIALES)}`);
})().catch(error => { console.error('\nFalló la carga de datos:', error.message); process.exit(1); });
