# Odontia

Sistema de gestión para un consultorio odontológico independiente: **un odontólogo dueño** y **secretarias invitadas**.
Reúne agenda, pacientes, historia clínica, odontograma FDI, laboratorio, pagos, finanzas y permisos por rol.

Diseño basado en el prototipo "Odontia" y en la planilla de atención ambulatoria (signos vitales, diagnóstico CIE-10 y prácticas por pieza).

## Características

- **Agenda** día, semana y mes en una sola columna: los turnos no se superponen (salvo urgencias).
- **Pacientes**: datos de contacto, cobertura, antecedentes, alergias y consentimiento firmado.
- **Historia clínica** por consulta: signos vitales, diagnóstico CIE-10, tratamiento, receta y prácticas por pieza.
- **Odontograma FDI** con estados "A realizar" (rojo) y "Existente" (azul), y un panel por pieza con su historial.
- **Pagos y finanzas**: cuenta corriente del paciente, ingresos y egresos. Los pagos se anulan, no se borran.
- **Laboratorio**: trabajos y pagos a proveedores.
- **Equipo y permisos**: invitaciones de un solo uso, permisos configurables por módulo y acción, e historial de lo que hace la secretaria.

## Stack

| Capa | Tecnologías |
|---|---|
| Backend | ASP.NET Core 8, Entity Framework Core, Pomelo (MySQL) |
| Frontend | React 18, Vite, React Router (JSX y TSX) |
| Base de datos | MySQL 8 o MariaDB |
| Autenticación | Firebase Authentication con Google. La API verifica el token con los certificados públicos de Google y emite su propio JWT. |

## Roles

- **Odontólogo (administrador):** acceso total. Crea el consultorio, invita a la secretaria y define sus permisos.
- **Secretaria:** entra solo con una invitación de un solo uso que vence en 7 días. Lo que puede hacer lo define el odontólogo.

La app **no guarda contraseñas**: el acceso es solo con cuenta de Google. La base registra únicamente qué correos están autorizados, su rol y si están activos.

## Puesta en marcha para desarrollo

Requisitos: .NET 8 SDK, Node.js 20 o superior, MySQL 8 o MariaDB.

1. **Base de datos.** Crear la base con `backend/database/schema.sql`. (Opcional: en desarrollo, la API crea las tablas sola si la base está vacía.)
2. **API.** Copiar `backend/OdontoApp.Api/appsettings.Development.example.json` a `appsettings.Development.json` y completar:
   - la contraseña de MySQL;
   - `Jwt:Key`, una cadena aleatoria de al menos 32 caracteres.
3. **Web.** Copiar `frontend/.env.example` a `frontend/.env.local` y completar la configuración web de Firebase (Configuración del proyecto → Tus apps → SDK).
4. **Levantar** en dos terminales:
   ```bash
   cd backend/OdontoApp.Api && dotnet run      # API en http://localhost:5000
   cd frontend && npm install && npm run dev   # web en http://localhost:5173
   ```

Los archivos `appsettings.Development.json` y `frontend/.env.local` contienen datos sensibles o locales: **no se versionan**.


## Actualizar una base existente

Las migraciones están en `backend/database/actualizar_vN.sql`. Ejecutarlas **en orden**, desde la versión que tenga la base hasta la última (hoy, `actualizar_v9.sql`). Lo que cambia en cada versión está documentado en [docs/CAMBIOS.md](docs/CAMBIOS.md).

## Seguridad

- La clave `Jwt:Key` y la contraseña de MySQL van en la configuración de cada PC, nunca en el repositorio.
- `appsettings.Development.json`, `frontend/.env.local` y las carpetas `aplicacion/` están excluidos en `.gitignore`.
- Cualquier cuenta de Google que no esté autorizada en el consultorio recibe 403.

## Estructura

```
backend/
  database/                 schema.sql y migraciones actualizar_v2 … v9
  OdontoApp.Api/
    Auth/                   Verificación de Firebase, JWT, permisos (atributo [Permiso])
    Controllers/            Endpoints por área (agenda, pacientes, clínica, odontograma, caja, laboratorio…)
    Models/                 Entidades EF Core (tablas y columnas en snake_case)
    Dtos/                   Contratos de la API y mapeos
    Agenda/                 Horario, almuerzo, duración de turnos y zona horaria
    Data/                   DbContext y datos iniciales de desarrollo
frontend/
  src/
    auth/                   Inicio de sesión con Google y permisos del usuario
    components/             Layout, formularios, odontograma, historia clínica, equipo y permisos
    pages/                  Inicios por rol, agenda, pacientes, ficha, caja, laboratorio, perfil
    types/                  Tipos espejo de los DTOs del backend
instalador/                 Scripts para generar la carpeta aplicacion y configurarla en el consultorio
docs/                       Guía de instalación y registro de cambios por versión
```

## Estado del proyecto

- El backend se escribió sin compilarlo en el entorno de desarrollo: la primera compilación real puede requerir correcciones menores.
- La verificación de tokens de Firebase y el flujo completo de invitación todavía no se probaron de punta a punta.

Pendientes conocidos:

- Adjuntar archivos (radiografías, consentimiento escaneado) a la historia clínica. Falta definir dónde se guardan.
- Recordatorios automáticos. Hoy el botón de WhatsApp abre el mensaje para que la secretaria lo envíe.
- El permiso **"Con registro"** solo registra la acción en el historial; no pide aprobación previa del odontólogo.
- Módulos sin pantalla todavía: resultado económico de laboratorio y "Confidencial".
- Reemplazar la creación de tablas por migraciones de EF Core, en lugar de scripts SQL manuales.
