# Odontia - Frontend

Aplicación web de gestión odontológica desarrollada con Angular 19, TypeScript, HTML y CSS tradicional.

## Requisitos

- Node.js compatible con Angular 19.
- npm.

## Instalación y ejecución

Desde la carpeta `Frontend`, ejecutar:

```bash
npm install
npm start
```

Luego abrir `http://localhost:4200/`.

## Estructura principal

```text
src/
├── app/
│   ├── pages/
│   │   ├── login/       # Pantalla de inicio de sesión
│   │   └── dashboard/   # Panel principal según el perfil
│   ├── app.component.*  # Componente raíz con router-outlet
│   ├── app.routes.ts    # Rutas de la aplicación
│   └── app.config.ts    # Configuración de Angular
├── index.html
└── styles.css           # Estilos generales
```

## Pantallas implementadas

- `/login`: formulario de acceso con selección de perfil y validaciones.
- `/dashboard`: panel principal. El perfil se recibe mediante el parámetro `?perfil=secretaria` o `?perfil=odontologo`.

El ingreso actual es una demostración de interfaz: valida los campos del formulario y navega al panel, pero todavía no autentica usuarios contra un servidor.

## Conceptos de Angular utilizados

- Componentes standalone.
- Templates HTML y hojas CSS separadas.
- Angular Router, rutas y `router-outlet`.
- Interpolación `{{ }}` y property binding `[ ]`.
- Event binding `(click)`, `(input)` y `(change)`.
- Directivas `*ngIf`, `*ngFor` y `ngClass`.
- Reactive Forms y Validators en el inicio de sesión.
- Interfaces TypeScript para representar turnos y tareas.

## Próximos pasos

Conectar los datos a servicios, implementar autenticación real, completar las rutas de los módulos y reemplazar los datos de demostración por información persistida.


## Módulo Agenda (tercera etapa)

La pantalla `/agenda` permite consultar turnos, filtrar por profesional y paciente, cambiar entre vistas de día/semana/mes, crear turnos y actualizar su estado. Los datos actuales son de demostración y se mantienen en memoria mientras la aplicación está abierta.

### Archivos nuevos

- `src/app/models/turno.model.ts`: interfaz `Turno`, que define los datos de una cita.
- `src/app/services/turno.service.ts`: servicio compartido para listar, agregar y actualizar turnos.
- `src/app/pages/agenda/agenda.component.ts`: lógica, filtros y formulario reactivo de la agenda.
- `src/app/pages/agenda/agenda.component.html`: estructura visual de la agenda y el formulario.
- `src/app/pages/agenda/agenda.component.css`: estilos y adaptación responsive.

### Probar el módulo

Desde la carpeta `Frontend`, ejecutar `npm install` y luego `npm start`. Iniciar sesión con datos que cumplan las validaciones y entrar en **Agenda** desde el menú lateral. Para crear un turno, seleccionar **Nuevo turno**, completar el formulario y guardar.

> Los turnos todavía no se guardan en un archivo ni en una base de datos: el servicio utiliza una lista en memoria. En una etapa posterior podremos conectarlo con una API.

## Etapa 4: Gestión de pacientes

Se incorporó el módulo de pacientes en la ruta `/pacientes`.

### Archivos nuevos
- `src/app/models/paciente.model.ts`: interfaz `Paciente`, con los datos que identifican a una persona registrada.
- `src/app/services/paciente.service.ts`: lista de ejemplo y métodos para consultar, agregar y actualizar pacientes.
- `src/app/pages/pacientes/pacientes.component.ts`: lógica de búsqueda, filtros, selección de ficha y formulario de alta/edición.
- `src/app/pages/pacientes/pacientes.component.html`: estructura de la tabla, ficha lateral y formulario.
- `src/app/pages/pacientes/pacientes.component.css`: estilos y adaptación a pantallas pequeñas.

### Funcionalidades
- Búsqueda por nombre, DNI o teléfono.
- Filtros por obra social y estado.
- Alta y edición de pacientes con Reactive Forms y Validators.
- Ficha de paciente con datos de contacto.
- Historial de turnos consultado desde `TurnoService`.

### Datos de demostración
Los servicios guardan los datos en arreglos en memoria. Los cambios se mantienen mientras la aplicación está abierta, pero todavía no persisten al reiniciar. En una etapa posterior se puede agregar almacenamiento persistente o conectar una API.


## Etapa 5: Historia clínica y odontograma

Se agregaron las rutas `/pacientes/:id/historia` y `/pacientes/:id/odontograma`. El modelo `RegistroClinico` representa consultas y `PiezaDental` representa el estado de cada pieza. `HistoriaClinicaService` centraliza los datos y los guarda en `localStorage` cuando la aplicación se ejecuta en el navegador. La comprobación `isPlatformBrowser` evita acceder a `localStorage` durante el renderizado del servidor (SSR).

Los datos de esta etapa son demostrativos y no deben utilizarse con información médica real: `localStorage` no es una base de datos segura ni sustituye un backend con autenticación y permisos.

## Etapa 6: Laboratorio dental

Se incorporó el módulo `/laboratorio` para registrar y realizar el seguimiento de trabajos enviados a laboratorios externos.

### Archivos nuevos
- `src/app/models/trabajo-laboratorio.model.ts`: define `TrabajoLaboratorio` y los estados posibles del trabajo.
- `src/app/services/laboratorio.service.ts`: centraliza el listado, alta y actualización de estados. Persiste los datos de demostración en `localStorage` y comprueba que se ejecute en el navegador para mantener la compatibilidad con SSR.
- `src/app/pages/laboratorio/laboratorio.component.ts`: contiene búsqueda, filtros, indicadores, formulario y acciones.
- `src/app/pages/laboratorio/laboratorio.component.html`: presenta el resumen, listado y formulario de alta.
- `src/app/pages/laboratorio/laboratorio.component.css`: estilos responsive del módulo.

### Funcionalidades
- Alta de trabajos asociados a pacientes activos.
- Registro de tipo de trabajo, pieza dental, laboratorio, fecha estimada y costo.
- Búsqueda por paciente, tipo de trabajo o laboratorio.
- Filtros por vista y estado.
- Actualización de estados: Solicitado, En proceso, Listo para retirar, Recibido y Entregado al paciente.
- Cálculo de trabajos atrasados, entregas de los próximos siete días y trabajos listos para retirar.
- Registro automático de fecha al marcar un trabajo como recibido o entregado.

### Persistencia y seguridad
Los datos se guardan en `localStorage` para esta demostración. No debe utilizarse para información clínica o financiera real; para producción se requiere una API/backend con autenticación, autorización y almacenamiento seguro.


## Etapa 7: Pagos de pacientes

Se incorporó el módulo `/pagos`, accesible desde el Dashboard y desde el menú de Pacientes.

### Archivos nuevos
- `src/app/models/pago.model.ts`: define los conceptos de cobro, pagos, métodos y estados.
- `src/app/services/pago.service.ts`: centraliza los conceptos, registra cobros parciales, calcula saldos y persiste los datos de demostración en `localStorage` con protección SSR.
- `src/app/pages/pagos/pagos.component.ts`: controla búsqueda, filtros, indicadores, validaciones y formulario de cobro.
- `src/app/pages/pagos/pagos.component.html`: muestra indicadores, conceptos, saldos, historial y formulario de registro.
- `src/app/pages/pagos/pagos.component.css`: estilos adaptables a pantallas pequeñas.

### Funcionalidades
- Consulta de conceptos por paciente y tratamiento.
- Registro de cobros con efectivo, transferencia, tarjeta de débito o crédito.
- Pagos parciales asociados al mismo concepto.
- Cálculo automático de importe abonado, saldo pendiente y estado (Pendiente, Pago parcial o Pagado).
- Generación de identificador de comprobante cuando no se ingresa uno.
- Validación para impedir importes cero, negativos o superiores al saldo.
- Búsqueda de conceptos y comprobantes, filtros por estado y método.

### Modelo de datos y alcance
Un `ConceptoCobro` representa el importe total de un tratamiento adeudado. Un `Pago` representa una operación de dinero recibida. La relación se establece mediante `conceptoId`, lo que permite varios pagos parciales para una sola deuda. Los datos de ejemplo son ficticios y se basan en el contenido de referencia de Figma. El módulo no implementa todavía anulación/corrección auditada, impresión de comprobantes, integración bancaria ni sincronización con Finanzas.

### Persistencia y seguridad
Los conceptos y pagos se guardan en `localStorage` para la demostración. No debe utilizarse con datos financieros o personales reales: en producción se requiere backend/API con autenticación, autorización por rol, auditoría y almacenamiento seguro. Ocultar una opción en la interfaz no sustituye la validación de permisos en el servidor.

## Etapa 8 – Finanzas

Se agregó el módulo `Finanzas` para consultar los movimientos económicos del consultorio por mes.

### Archivos agregados
- `src/app/models/finanza.model.ts`: tipos e interfaz de movimientos financieros.
- `src/app/services/finanzas.service.ts`: registro de movimientos manuales, integración de cobros de pacientes y cálculo de saldos de laboratorios.
- `src/app/pages/finanzas/finanzas.component.ts`: lógica de filtros, formularios e indicadores.
- `src/app/pages/finanzas/finanzas.component.html`: interfaz del panel, libro de movimientos y pendientes.
- `src/app/pages/finanzas/finanzas.component.css`: estilos responsive.

### Archivos modificados
- `src/app/app.routes.ts`: ruta `/finanzas`.
- `src/app/pages/dashboard/dashboard.component.html`: enlace al módulo Finanzas.

### Reglas de cálculo
- Los ingresos por pacientes se leen directamente de `PagoService`; solo se consideran pagos no anulados.
- Los conceptos pendientes no se suman a ingresos.
- Los egresos se consideran dinero pagado; los saldos pendientes a laboratorios se muestran aparte.
- Los pagos a laboratorios se asocian por `trabajoLaboratorioId`, y el importe no puede superar el saldo del trabajo.
- El balance es ingresos menos egresos del período seleccionado.

### Prueba manual
1. Ejecutar `npm install` y `npm start` desde `Frontend`.
2. Ingresar a la aplicación y abrir Finanzas desde el menú lateral o `/finanzas`.
3. Cambiar el mes para filtrar el libro de movimientos.
4. Revisar que un cobro guardado en Pagos de pacientes aparezca una sola vez como ingreso.
5. Registrar un egreso general y comprobar que actualice egresos y balance.
6. Registrar un pago parcial a un trabajo de laboratorio y comprobar que baje su saldo pendiente.
7. Intentar registrar un pago superior al saldo: el servicio debe rechazarlo.

### Datos y seguridad
Los importes y movimientos de ejemplo son ficticios. La persistencia de esta demo usa `localStorage` y está protegida para no ejecutarse durante SSR. `localStorage` no es adecuado para información financiera o clínica real; una instalación productiva necesita API/backend, autenticación, autorización, auditoría y almacenamiento seguro.

## Correcciones de interacción y navegación
- Se incorporó la pantalla Perfil y configuración (`/configuracion`).
- Los accesos a Turnos y tareas, Historia clínica y Odontograma navegan a rutas reales.
- Nuevo turno desde el Dashboard abre Agenda con el formulario desplegado.
- Notificaciones muestra un panel interactivo de avisos de demostración.
- Se separaron los campos de búsqueda de conceptos y del historial de pagos.
- Se amplió el listado de obras sociales; Particular sigue siendo el valor inicial.
- El odontograma ahora incluye dentición temporal y estados por cara además del estado general.
- Los formularios de ingreso/egreso y pagos a laboratorios se muestran como ventanas modales visibles.

## Correcciones adicionales de navegación, odontograma y pagos

- La opción **Turnos y tareas** dispone de la ruta Angular `/turnos-tareas` y conserva el perfil seleccionado. Los accesos laterales que antes eran anclas HTML ahora utilizan navegación de Angular.
- La agenda toma la fecha actual y los turnos de demostración se acomodan al día actual y al siguiente para que no aparezca vacía por fechas de ejemplo vencidas.
- En el odontograma se pueden seleccionar las cinco caras de cada pieza: arriba (vestibular), abajo (lingual), izquierda (mesial), derecha (distal) y centro (oclusal). Se puede elegir el estado de cada cara y guardar los cambios.
- La pantalla de pagos muestra a todos los pacientes cargados en el sistema, incluso si todavía no tienen conceptos de cobro. Desde cada paciente se puede agregar un concepto/tratamiento con su importe y luego registrar cobros parciales.
- Los cobros con tarjeta de crédito permiten elegir 1, 3, 6 o 12 cuotas, o una cantidad personalizada de 1 a 24. El historial muestra la cantidad y el importe aproximado de cada cuota.

**Alcance de demostración:** los pagos y cuotas se guardan localmente en `localStorage`. No se conecta con una pasarela bancaria ni procesa tarjetas, no calcula intereses, aranceles, impuestos o costos financieros y no confirma operaciones con una entidad emisora. Para producción se requiere backend seguro, integración con un proveedor de pagos y controles de autorización. No almacenar números completos de tarjetas ni códigos de seguridad en esta aplicación.
