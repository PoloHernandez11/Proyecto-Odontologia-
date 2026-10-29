
---

## Entrega: inicio de sesión solo con Google

### Eliminado
- Contraseñas y nombres de usuario en la base y en la API (`usuario.password`, `usuario.usuario`, `BCrypt`).
- Endpoints `POST /auth/login`, `POST /auth/password` y el alta con contraseña.
- Sección **Consultorio** y tarjeta **Credenciales y seguridad** en Perfil y configuración.
- Selector de perfil, "Recordarme" y "¿Olvidaste tu contraseña?" en el login.
- Sección `Seed` de `appsettings.Development.json` (ya no hay usuarios de prueba).

### Backend
| Archivo | Cambio |
|---|---|
| `OdontoApp.Api.csproj` | Quita `BCrypt.Net-Next`. Agrega `Google.Apis.Auth`. |
| `appsettings.json` | Nueva sección `Google:ClientId` (no es secreto). |
| `Auth/GoogleIdentidad.cs` (nuevo) | Valida el ID token de Google: firma, audiencia (Client ID), expiración y correo verificado. |
| `Models/Usuario.cs` | Sin contraseña ni usuario. Tiene `Email` obligatorio y `GoogleSub` (identificador estable de Google). |
| `Data/OdontoDbContext.cs` | Índices únicos: `(consultorio_id, email)` y `google_sub`. |
| `Dtos/Dtos.cs` | DTOs de autenticación con `credential` de Google. |
| `Controllers/AuthController.cs` | `POST auth/google` (solo cuentas autorizadas y activas), `registro-dueno` y `invitaciones/aceptar` con Google. La aceptación exige que el correo de Google coincida con el invitado. |
| `Program.cs` | Registra `GoogleOptions` y `GoogleIdentidad`. |

### Base de datos
| Archivo | Cambio |
|---|---|
| `database/schema.sql` | `usuario` sin `usuario` ni `password`; agrega `google_sub`, índices únicos. |
| `database/actualizar_v4.sql` (nuevo) | Migración para bases de la v3. Requiere correo en todos los usuarios antes de ejecutarla. |

### Frontend
| Archivo | Cambio |
|---|---|
| `package.json` | Agrega `@react-oauth/google`. |
| `.env.example` (nuevo) | `VITE_GOOGLE_CLIENT_ID` y `VITE_API_URL`. |
| `main.jsx` | Envuelve la app en `GoogleOAuthProvider`. |
| `types/api.ts` | Tipos de autenticación con Google. |
| `auth/AuthContext.tsx` | `loginGoogle`, `registrarDueno` y `aceptarInvitacion` con `credential`. |
| `api/client.ts` | Los 401 de `/auth/*` no recargan la página. Los 403 muestran el mensaje del servidor. |
| `pages/Login.tsx` | Botón de Google. |
| `pages/RegistroDueno.tsx` | Nombre del consultorio y botón de Google. |
| `pages/AceptarInvitacion.tsx` | Muestra el correo invitado y botón de Google. |
| `pages/Perfil.jsx` | Sin Consultorio ni cambio de contraseña. |
| `components/EquipoInvitaciones.tsx` | Muestra el correo de cada miembro. |

### Configuración necesaria
1. Crear un **ID de cliente OAuth (Aplicación web)** en Google Cloud Console, con `http://localhost:5173` como origen autorizado.
2. Backend: `dotnet user-secrets set "Google:ClientId" "..."`.
3. Frontend: `frontend/.env.local` con `VITE_GOOGLE_CLIENT_ID=...`.

### Verificación
- Frontend: sintaxis validada con TypeScript. No se instaló `@react-oauth/google` ni se probó el flujo de Google.
- Backend: no compilado. Revisado a mano, incluido el uso de `Google.Apis.Auth`.

---

## Entrega: autenticación con Firebase (proveedor Google)

Reemplaza la validación directa de Google por **Firebase Authentication**. Es gratis (plan Spark) y no requiere tarjeta. El flujo de la app no cambió: sigue habiendo cuentas autorizadas en la base, sin contraseñas.

### Backend
| Archivo | Cambio |
|---|---|
| `OdontoApp.Api.csproj` | Quita `Google.Apis.Auth`. Agrega `FirebaseAdmin` 3.0.0. |
| `appsettings.json` | Sección `Firebase:ProjectId` (reemplaza `Google:ClientId`). |
| `Auth/FirebaseIdentidad.cs` (nuevo) | Verifica el ID token de Firebase: firma, proyecto, expiración y correo verificado. Falla cerrado (cualquier error = no autorizado). |
| `Auth/GoogleIdentidad.cs` | Eliminado. |
| `Controllers/AuthController.cs` | Usa `FirebaseIdentidad`. Los endpoints y las reglas no cambian. |
| `Program.cs` | Registra `FirebaseSettings` y `FirebaseIdentidad`. |

### Frontend
| Archivo | Cambio |
|---|---|
| `package.json` | Quita `@react-oauth/google`. Agrega `firebase`. |
| `.env.example` | Variables `VITE_FIREBASE_*`. |
| `auth/firebase.ts` (nuevo) | Inicia sesión con Google vía Firebase y devuelve el ID token. |
| `components/BotonGoogle.tsx` (nuevo) | Botón "Continuar con Google" reutilizable. |
| `main.jsx` | Quita `GoogleOAuthProvider`. |
| `pages/Login.tsx`, `RegistroDueno.tsx`, `AceptarInvitacion.tsx` | Usan `BotonGoogle`. |
| `pages/Login.css` | Estilo del botón. |

### Configuración necesaria
1. Crear proyecto en Firebase Console, habilitar **Authentication → Google**.
2. Frontend: `frontend/.env.local` con los valores de `firebaseConfig`.
3. Backend: `Firebase:ProjectId` en `appsettings.json` o con `user-secrets`.

### Verificación
- Frontend: sintaxis validada con TypeScript. No se instaló `firebase` ni se probó el flujo.
- Backend: no compilado. La verificación de tokens sin cuenta de servicio se basa en la documentación de FirebaseAdmin y **no fue probada**. Si `VerifyIdTokenAsync` pide credenciales, hay que agregar una cuenta de servicio.

---

## Odontograma: nuevos estados y colores (versión 5)

Se extendió el odontograma existente en vez de crear uno nuevo.

### Colores
- **Rojo = a realizar**, **azul = existente**, **verde = sellante**.
- Los significados guardados no cambian: solo cambia el color que se dibuja.

### Estados nuevos
- Caras: `CariesRequerida`, `CariesExistente`, `ObturacionRequerida`, `ObturacionExistente`, `Sellante`.
- Pieza: `EndodonciaRequerida`, `EndodonciaExistente`, `ImplanteRequerida`, `ImplanteExistente`.
- Se mantienen corona, prótesis fija y removible, ausente y extracción.

### Modo
El selector **A realizar / Existente** decide el estado de caries, obturación, corona, endodoncia, implante y prótesis. Sellante, extracción, ausente y sano no dependen del modo.

### Archivos
| Archivo | Cambio |
|---|---|
| `Models/Enums.cs` | Nuevos valores de `EstadoCara` y `EstadoGeneralDiente`. |
| `database/actualizar_v5.sql` (nuevo) | Pasa `Requerida`/`Existente` de las caras a obturación. |
| `odontograma/estados.js` | Modos, colores, herramientas y símbolos. |
| `odontograma/odontogramaReducer.js` | Aplica la herramienta según el modo. |
| `odontograma/useOdontograma.js` | Estado del modo. |
| `odontograma/Diente.jsx` | Dibuja endodoncia e implante. |
| `odontograma/Odontograma.jsx`, `.css` | Selector de modo y leyenda nueva. |

### Pendientes
- Ejecutar `actualizar_v5.sql` en bases con datos antes de usar la versión nueva.
- Verificación: el frontend pasó una revisión de sintaxis; el backend no se compiló.

---

## Odontograma: panel de la pieza (versión 6)

- **Panel lateral** al tocar una pieza: número y nombre anatómico, cuadro de caras con su estado, chips de resumen, nota de la pieza e historial clínico.
- **Aplicar estado** aplica la herramienta y el modo activos a la cara elegida en el panel (o a la pieza, en las marcas generales).
- **Nota de la pieza:** se guarda con cada versión del odontograma (columna `nota` en `odontograma_diente`).
- **Historial de la pieza:** endpoint `GET api/pacientes/{id}/odontograma/piezas/{numero}/historial`. Toma las atenciones de la historia clínica cuyo campo `piezas` incluye la pieza.

### Archivos
| Archivo | Cambio |
|---|---|
| `Models/OdontogramaDiente.cs` | Propiedad `Nota`. |
| `Dtos/Dtos.cs` | `DienteDto.Nota` (opcional) y `HistorialPiezaDto`. |
| `Controllers/OdontogramaController.cs` | Guarda y devuelve la nota; endpoint de historial. |
| `database/actualizar_v6.sql` (nuevo) | Agrega la columna `nota`. |
| `database/schema.sql` | Columna `nota` en instalaciones nuevas. |
| `odontograma/PanelPieza.jsx` (nuevo) | Panel de la pieza. |
| `odontograma/Diente.jsx`, `useOdontograma.js`, `odontogramaReducer.js`, `Odontograma.jsx`, `Odontograma.css`, `fdi.js` | Selección, nota, historial y layout. |

### Pendientes
- Ejecutar `actualizar_v6.sql` en bases existentes.
- Las atenciones de práctica por pieza (`practica_atencion`) todavía no se incluyen en el historial.
- Verificación: sintaxis del frontend revisada; el backend no se compiló.

---

## Nombres de estados del odontograma (versión 7)

Los nombres de los estados ahora coinciden con las opciones de la pantalla, en el backend (C#), en el frontend (JS) y en la base de datos. Se eliminó la reutilización de nombres: "Requerida" dejó de usarse para dos significados.

| Pantalla | Nombre en código y base |
|---|---|
| Caries (a realizar) | `CariesARealizar` |
| Obturación (a realizar) | `ObturacionARealizar` |
| Corona / Endodoncia / Implante / Prótesis (a realizar) | `CoronaARealizar`, `EndodonciaARealizar`, `ImplanteARealizar`, `ProtesisFijaARealizar`, `ProtesisRemovibleARealizar` |
| Extracción | `ExtraccionARealizar` |
| Ausente | `AusenteExistente` |
| Existente (cualquier opción) | `...Existente` |

Migración: `database/actualizar_v7.sql`. Orden para una base existente: `actualizar_v5.sql`, `actualizar_v6.sql` y después `actualizar_v7.sql`.

---

## Permisos configurables de la secretaria (versión 8)

El odontólogo define en Perfil y configuración qué puede hacer la secretaria. La matriz se guarda por consultorio y el backend la aplica en cada endpoint.

### Niveles
- **Sin acceso:** la acción devuelve 403.
- **Con acceso** (valor `Permitido`): la acción se realiza y queda en el historial.
- **Con registro** (valor `ConAprobacion`): la acción se realiza y queda en el historial, marcada como Con registro.

Ambos niveles se aplican al instante. No hay aprobación previa del odontólogo. Los nombres internos no cambian para no tocar los datos guardados.

### Módulos y acciones (mismos nombres en C#, TypeScript y base)
Módulos: `PagosPacientes`, `FinanzasGenerales`, `LaboratorioTrabajos`, `LaboratorioPagos`, `LaboratorioResultadoEconomico`, `HistoriaClinicaOdontograma`.
Acciones: `Registrar`, `EditarCorregir`, `Anular`, `VerResumenes`, `Confidencial`.

### Endpoints protegidos
| Módulo · acción | Endpoint |
|---|---|
| PagosPacientes · Registrar | POST pacientes/{id}/pagos |
| PagosPacientes · VerResumenes | GET pacientes/{id}/cuenta, GET api/pagos |
| PagosPacientes · Anular | PATCH api/pagos/{id}/anular |
| FinanzasGenerales · VerResumenes / Registrar / Anular | GET, POST, DELETE api/caja |
| LaboratorioTrabajos · Registrar | POST api/laboratorio |
| LaboratorioTrabajos · EditarCorregir | PUT y PATCH estado de api/laboratorio |
| LaboratorioPagos · Registrar | PATCH api/laboratorio/{id}/pagar |
| HistoriaClinicaOdontograma · VerResumenes | GET historial, GET odontograma |
| HistoriaClinicaOdontograma · Registrar | POST historial |
| HistoriaClinicaOdontograma · EditarCorregir | POST odontograma |

Sin endpoint todavía: `PagosPacientes · EditarCorregir`, `FinanzasGenerales · EditarCorregir`, `LaboratorioResultadoEconomico` (todas), `Confidencial` (todas). Se marcan con * en la matriz.

Los listados de laboratorio (GET) siguen abiertos a todo el personal.

### Valores iniciales
Los de la captura de Perfil. Se usan mientras el consultorio no guardó su matriz.

### Archivos nuevos
- `Models/Permisos.cs`, `Dtos/PermisosDtos.cs`, `Auth/PermisosService.cs`, `Auth/PermisoAttribute.cs`, `Controllers/PermisosController.cs`.
- `frontend/src/types/permisos.ts`, `auth/permisos.ts`, `components/MatrizPermisos.tsx`.
- `database/actualizar_v8.sql`, `database/actualizar_v9.sql`.

### Pendientes
- Ejecutar `actualizar_v8.sql` y luego `actualizar_v9.sql` en bases existentes.
- Verificación: el backend no se compiló y la matriz no se probó en ejecución.


---

## Historial de la secretaria y envío de invitación (versión 9)

- **Historial:** Perfil y configuración muestra las últimas 200 acciones de la secretaria en módulos con permiso, con fecha, quién, módulo, acción y nivel (Con acceso o Con registro). Solo lo ve el odontólogo.
  - Endpoint nuevo: `GET api/permisos/actividad?limite=200`.
  - Tabla `actividad_permiso`: nueva columna `nivel`.
- **Invitación:** después de crear la invitación aparece el botón **Redactar correo a …**, que abre el correo del odontólogo con destinatario, asunto y texto con el enlace. Nada se envía solo: el odontólogo puede editar el texto y enviarlo.
- **Textos de pantalla:** "Permitido" pasa a "Con acceso" y "Con aprobación" pasa a "Con registro".

### Archivos
- Backend: `Models/Permisos.cs`, `Dtos/PermisosDtos.cs`, `Auth/PermisosService.cs`, `Auth/PermisoAttribute.cs`, `Controllers/PermisosController.cs`.
- Frontend: `components/HistorialActividad.tsx` (nuevo), `components/MatrizPermisos.tsx`, `components/EquipoInvitaciones.tsx`, `pages/Perfil.jsx`, `types/permisos.ts`.
- Base: `database/actualizar_v9.sql` (ejecutar después de v8).
