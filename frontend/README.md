# Odontia — Frontend

Aplicación web de gestión odontológica hecha con Angular 19 (componentes standalone, TypeScript, HTML y CSS).
Consume la API REST del backend (`../src/Odontia.Api`) bajo `/api/v1`: no guarda datos de negocio en el navegador.

## Requisitos

- Node.js compatible con Angular 19 y npm.
- La API de Odontia corriendo en `http://localhost:5221` (ver el [README principal](../README.md)).

## Instalación y ejecución

Desde la carpeta `frontend`:

```bash
npm install
```

```bash
npm start
```

Abrir `http://localhost:4200/`. En desarrollo, `proxy.conf.json` reenvía `/api` a la API, así el navegador ve todo en el
mismo origen y las cookies de sesión funcionan sin configuración extra.

Para tener un consultorio de prueba con datos (pacientes, turnos, tareas, laboratorio y cobros), con la API levantada:

```bash
node ../tools/seed-dev.js
```

Las credenciales de las dos cuentas de prueba (odontóloga administradora y secretaria) quedan en `tools/.seed-dev.json`,
que no se sube al repositorio.

## Estructura

```text
src/app/
├── api/
│   ├── schema.ts               # Tipos generados desde docs/openapi.v1.json (no editar a mano)
│   └── odontia-api.service.ts  # Único punto de acceso a la API
├── core/
│   ├── auth.service.ts         # Sesión: access token en memoria + renovación con cookie HttpOnly
│   ├── auth.interceptor.ts     # Agrega el Bearer y renueva la sesión ante un 401
│   ├── guards.ts               # Sesión iniciada y permisos por ruta
│   └── api.ts                  # URL base, mensajes de error y formato de fechas e importes
├── layout/shell.component.*    # Menú lateral único, filtrado por permisos
└── pages/                      # Una carpeta por pantalla
```

Para regenerar los tipos después de un cambio en el contrato de la API, desde la carpeta `Odontia`:

```bash
node tools/gen-types.js
```

## Pantallas

| Ruta | Pantalla | Permiso |
|---|---|---|
| `/login` | Inicio de sesión (con segundo paso si la cuenta tiene verificación en dos pasos) | — |
| `/registro` | Alta de un consultorio nuevo (si la API permite el registro público) | — |
| `/confirmar-correo`, `/restablecer-contrasena`, `/aceptar-invitacion` | Destino de los enlaces que la API envía por correo | — |
| `/dashboard` | Inicio: tablero clínico o de recepción según el rol | sesión iniciada |
| `/agenda` | Agenda por día, semana y mes; alta y seguimiento de turnos | `agenda.read` |
| `/turnos-tareas` | Tareas del consultorio y turnos por confirmar | `tasks.read` o `appointments.read` |
| `/pacientes` | Directorio y ficha de pacientes | `patients.read` |
| `/historia-clinica`, `/historia-clinica/:id` | Buscador de paciente e historia clínica | `clinical.read` |
| `/odontograma`, `/odontograma/:id` | Buscador de paciente y odontograma FDI | `odontogram.read` |
| `/laboratorio` | Trabajos de laboratorio | `lab.read` |
| `/pagos` | Conceptos de cobro (presupuestos aprobados) y pagos | `finance.read` |
| `/finanzas` | Ingresos, egresos y balance neto del período; registro de egresos | `finance.read` (`payments.write` para registrar) |
| `/configuracion` | Perfil, seguridad, notificaciones y datos del consultorio | sesión iniciada |

El perfil (odontólogo, secretaria, administrador) no se elige en el login: lo determinan los roles del usuario. El menú y las
rutas se ajustan a sus permisos, y el backend vuelve a validar cada operación.

## Limitaciones conocidas

- Las fechas y horas se muestran y se cargan en la zona horaria del navegador; se asume que coincide con la del consultorio.
- Los selectores de paciente de Laboratorio y Pagos listan hasta 100 pacientes.

## Despliegue

En producción el build de Angular se sirve con nginx dentro de Docker, detrás del mismo dominio que la API
(ver [`deploy/`](../deploy/README.md) y [docs/DEPLOYMENT.md](../docs/DEPLOYMENT.md)).

Vercel sigue disponible como alternativa sin servidor propio, con la configuración de [`vercel.json`](vercel.json) y la variable
`ODONTIA_API_URL`; los pasos y sus limitaciones están en [docs/DEPLOYMENT.md](../docs/DEPLOYMENT.md).

## Pruebas y compilación

```bash
npm test -- --watch=false --browsers=ChromeHeadless
```

```bash
npm run build
```
