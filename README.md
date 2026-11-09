# Odontia — Backend

API REST para la gestión odontológica **multi-consultorio**: agenda y turnos, pacientes, historia clínica,
odontograma FDI, tratamientos, presupuestos, pagos y cuenta corriente, laboratorio, documentos y
consentimientos, tareas, notificaciones y dashboards por perfil (odontólogo / secretaria / administrador).

El frontend (React + TypeScript) se integra **sólo** a través de esta API documentada con OpenAPI.

## Tecnologías

| Componente | Versión |
|---|---|
| .NET / ASP.NET Core | 10 (SDK 10.0.403) |
| Entity Framework Core | 10.0.14 |
| Proveedor MySQL | MySql.EntityFrameworkCore 10.0.12 (oficial de Oracle) |
| Base de datos | MySQL 8.4 LTS o superior (verificado con MySQL 9.1) |
| Autenticación | ASP.NET Core Identity + JWT Bearer (10.0.14) |
| Versionado | Asp.Versioning 10.2.1 (`/api/v1/...`) |
| OpenAPI | Swashbuckle.AspNetCore 10.3.1 |
| Validación | FluentValidation 12.2.0 |
| Pruebas | xUnit 2.9.3, Microsoft.AspNetCore.Mvc.Testing, SQLite en memoria / MySQL real |

Las versiones están centralizadas en `Directory.Packages.props`.

## Arquitectura

```
Odontia/
├─ src/
│  ├─ Odontia.Domain          Entidades, enums, reglas de negocio e invariantes, catálogo FDI, permisos
│  ├─ Odontia.Application     Casos de uso (servicios), DTOs, validadores, contexto de consultorio
│  ├─ Odontia.Infrastructure  EF Core + MySQL, Identity, JWT, almacenamiento, correo, trabajos en segundo plano
│  └─ Odontia.Api             Controllers delgados, middleware, autorización, OpenAPI, rate limiting, CORS
├─ tests/
│  ├─ Odontia.UnitTests        Dominio, disponibilidad, finanzas, odontograma, permisos, aislamiento (SQLite)
│  └─ Odontia.IntegrationTests API completa end-to-end (SQLite en memoria o MySQL real)
├─ docs/                       Documentación técnica + openapi.v1.json
└─ postman/                    Colección y entorno de Postman
```

Detalle: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) · Base de datos: [docs/DATABASE.md](docs/DATABASE.md) ·
Roles y permisos: [docs/ROLES_AND_PERMISSIONS.md](docs/ROLES_AND_PERMISSIONS.md) · Endpoints: [docs/ENDPOINTS.md](docs/ENDPOINTS.md) ·
Seguridad: [docs/SECURITY.md](docs/SECURITY.md) · Servicios externos: [docs/EXTERNAL_SERVICES.md](docs/EXTERNAL_SERVICES.md) ·
Integración React: [docs/FRONTEND_INTEGRATION.md](docs/FRONTEND_INTEGRATION.md)

## Requisitos

- .NET SDK 10.0.x
- MySQL 8.4 LTS o superior (utf8mb4)
- Herramienta EF Core: ya declarada como herramienta local (`dotnet-tools.json`) → `dotnet tool restore`

## Instalación

```bash
cd Odontia
dotnet tool restore
dotnet restore
dotnet build
```

## Configuración de MySQL

Crear una base y un usuario dedicado (no usar `root` en producción):

```sql
CREATE DATABASE odontia CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER 'odontia_app'@'%' IDENTIFIED BY '<contraseña-segura>';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES, DROP ON odontia.* TO 'odontia_app'@'%';
```

> Para producción conviene separar el usuario que aplica migraciones (DDL) del usuario de la aplicación (sólo DML).

## Variables de entorno y secretos

No hay secretos en el repositorio. En desarrollo se usan **User Secrets**; en otros entornos, **variables de entorno**
(los `:` se escriben como `__`). Ver [.env.example](.env.example).

Desarrollo (una sola vez):

```bash
dotnet user-secrets set "ConnectionStrings:Default" "server=localhost;port=3306;database=odontia_dev;user=odontia_app;password=<contraseña>" --project src/Odontia.Api
```

```bash
dotnet user-secrets set "Jwt:SigningKey" "<cadena aleatoria de 32+ caracteres>" --project src/Odontia.Api
```

| Variable | Obligatoria | Descripción |
|---|---|---|
| `ConnectionStrings__Default` | Sí | Cadena de conexión MySQL |
| `Jwt__SigningKey` | Sí | Clave HMAC-SHA256 (≥ 32 bytes) |
| `Cors__AllowedOrigins__0` | Sí (prod) | Origen del frontend (p. ej. `https://app.odontia.com`) |
| `Frontend__BaseUrl` | Sí | URL del frontend para los enlaces de correos |
| `DataProtection__KeysPath` | Sí (prod) | Carpeta persistente para las claves de Data Protection |
| `Storage__Local__RootPath` | Recomendada | Carpeta privada de documentos |
| `Email__Provider`, `Email__Smtp__*` | Para enviar correos | `Smtp` + credenciales del proveedor |
| `Auth__AllowPublicRegistration` | No | Alta pública de consultorios (por defecto `false` en producción) |

## Migraciones

La migración inicial está en `src/Odontia.Infrastructure/Persistence/Migrations` (`InitialCreate`).
Incluye las semillas controladas de **roles de sistema y permisos**, piezas FDI, superficies y condiciones del odontograma.
**No se crean usuarios administradores con contraseña fija**: el primer administrador se registra vía `POST /api/v1/auth/register`
(o por invitación).

Aplicar migraciones (las herramientas leen la conexión de `ODONTIA_DESIGN_CONNECTION`):

```bash
ODONTIA_DESIGN_CONNECTION="server=localhost;port=3306;database=odontia_dev;user=odontia_app;password=<contraseña>" dotnet ef database update --project src/Odontia.Infrastructure --startup-project src/Odontia.Infrastructure
```

Generar el script SQL idempotente para producción (recomendado, revisable por un DBA):

```bash
dotnet ef migrations script --idempotent --project src/Odontia.Infrastructure --startup-project src/Odontia.Infrastructure -o deploy/odontia.sql
```

Nueva migración tras cambiar el modelo:

```bash
dotnet ef migrations add <Nombre> --project src/Odontia.Infrastructure --startup-project src/Odontia.Infrastructure -o Persistence/Migrations
```

En `Development` la API aplica migraciones al iniciar (`Database:ApplyMigrationsOnStartup=true`); en producción está desactivado.
**Nunca se usa `EnsureCreated`** fuera de las pruebas con SQLite.

## Ejecución

```bash
dotnet run --project src/Odontia.Api --launch-profile http
```

- API: `http://localhost:5221/api/v1`
- Swagger UI (sólo Development o `Swagger:Enabled=true`): `http://localhost:5221/swagger`
- Health: `GET /health/live` (proceso) y `GET /health/ready` (incluye base de datos)
- Correos en desarrollo: no se envían; se guardan como `.eml` en `storage/dev-outbox/` (modo `DevelopmentFile`)

Primer uso: `POST /api/v1/auth/register` → abrir el `.eml` de `storage/dev-outbox` → `POST /api/v1/auth/confirm-email` → `POST /api/v1/auth/login`.

## Pruebas

```bash
dotnet test Odontia.slnx
```

Con SQLite en memoria (FKs, índices únicos y transacciones reales). Para correr la integración **contra MySQL real**
(crea y borra una base temporal por clase de prueba e incluye la prueba de concurrencia de reservas):

```bash
ODONTIA_TEST_MYSQL="server=localhost;port=3306;database=odontia_test;user=root;password=<contraseña>" dotnet test tests/Odontia.IntegrationTests
```

## Documentación de la API

- OpenAPI: `/swagger/v1/swagger.json` (copia versionada en [docs/openapi.v1.json](docs/openapi.v1.json))
- Tabla completa de endpoints y permisos: [docs/ENDPOINTS.md](docs/ENDPOINTS.md)
- Postman: importar `postman/Odontia.postman_collection.json` y `postman/Odontia.local.postman_environment.json`

Para regenerar `docs/ENDPOINTS.md` y la colección Postman después de cambiar la API, exportar el OpenAPI a
`docs/openapi.v1.json` y ejecutar `node tools/gen-docs.js .` desde la carpeta `Odontia`.
