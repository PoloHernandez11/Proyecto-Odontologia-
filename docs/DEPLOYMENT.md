# Despliegue

Odontia se publica en producción con Docker Compose en un servidor propio, en **https://odentix.duckdns.org**.
Al final hay una alternativa sin servidor propio (Vercel + MonsterASP.NET), pensada para demostraciones.

## Producción: Docker en un servidor compartido de Oracle Cloud

El servidor es una máquina ARM del plan Always Free de Oracle Cloud (Oracle Linux 9, 1 OCPU) que se comparte con otros proyectos.
Odontia no publica puertos propios: usa el Caddy que ya existe en el servidor, del proyecto miaula.

```text
Navegador ──HTTPS──► Caddy (miaula-web-1, puertos 80/443)
                       │  red Docker "miaula_default"
                       ├── /api/*, /health/* ──► odontia-api (ASP.NET Core 10, :8080) ──► odontia-mysql (MySQL 8.4)
                       │                                                                    red privada "odontia_interna"
                       └── resto ─────────────► odontia-web (nginx con el build de Angular, :80)
```

### Qué corre dónde

| Servicio | Imagen | Redes | Memoria máxima | Datos persistentes |
|---|---|---|---|---|
| `odontia-mysql` | `mysql:8.4` | sólo `odontia_interna` (sin salida a internet) | 640 MB | volumen `odontia_mysql-datos` |
| `odontia-api` | [`deploy/Dockerfile.api`](../deploy/Dockerfile.api) | `odontia_interna` y `miaula_default` | 512 MB | volumen `odontia_api-datos` en `/data` (claves de Data Protection y documentos) |
| `odontia-web` | [`deploy/Dockerfile.web`](../deploy/Dockerfile.web) | `miaula_default` | 64 MB | — |

La definición completa está en [`deploy/docker-compose.yml`](../deploy/docker-compose.yml). La configuración secreta (contraseñas
de MySQL, clave JWT, credenciales SMTP) vive sólo en `deploy/.env` dentro del servidor; la plantilla es
[`deploy/.env.example`](../deploy/.env.example).

### Enrutamiento y HTTPS

- Caddy importa los sitios desde `~/sitios-caddy`. Odontia agrega `~/sitios-caddy/odontia.caddy`, generado desde
  [`deploy/odontia.caddy.plantilla`](../deploy/odontia.caddy.plantilla): `/api/*` y `/health/*` van a `odontia-api:8080` y el
  resto a `odontia-web:80`.
- Caddy obtiene y renueva el certificado TLS solo, para el dominio de `DOMINIO` en `deploy/.env` (un subdominio de DuckDNS que
  apunta al servidor).
- Frontend y API comparten el mismo origen, así que no hace falta CORS y las cookies de sesión usan los valores por defecto.
- nginx ([`deploy/nginx.conf`](../deploy/nginx.conf)) sólo sirve estáticos: caché larga para los recursos con hash, `index.html`
  sin caché y fallback de SPA. La compresión y TLS los hace Caddy.
- La API confía en los encabezados `X-Forwarded-For` y `X-Forwarded-Proto` que llegan desde la red de Docker
  (`ReverseProxy__KnownNetworks__0=172.18.0.0/16`), así que los límites de solicitudes se aplican por IP real del cliente.
  Ese valor tiene que coincidir con la subred de `miaula_default` (`docker network inspect miaula_default`).

### Correo

La API exige confirmar el correo antes del primer ingreso, así que necesita SMTP. En producción se usa una cuenta de Gmail
(`smtp.gmail.com`, puerto 587, con verificación en dos pasos y una contraseña de aplicación). Los valores `SMTP_*` se completan a
mano en `deploy/.env`.

### Instalar, actualizar y operar

El paso a paso está en [`deploy/README.md`](../deploy/README.md): subir el código con `git archive` + `scp`, primera instalación con
`actualizar-servidor.sh`, actualización conservando la versión anterior, logs, respaldo y restauración, y vuelta atrás.

### Notas de operación

- **Memoria:** el servidor es chico y compartido; por eso cada contenedor tiene `mem_limit` y MySQL corre con
  `innodb-buffer-pool-size=128M` y `performance-schema=OFF`. Para revisar el consumo: `docker stats` (ver `deploy/README.md`).
- **Healthchecks:** MySQL responde a `mysqladmin ping`; la API, a `/health/ready`; nginx tiene el suyo en `Dockerfile.web`.
  La API no arranca hasta que MySQL está sano, y [`actualizar-servidor.sh`](../deploy/actualizar-servidor.sh) espera a que los tres
  servicios queden `healthy` antes de escribir el sitio en Caddy y recargarlo. Si Caddy rechaza la configuración, el script deja la
  anterior.
- **Migraciones:** la API las aplica al iniciar (`Database__ApplyMigrationsOnStartup=true`). No se revierten solas: hacer un
  respaldo antes de cada actualización.
- **Datos y respaldos:** todo lo que hay que conservar está en los volúmenes `odontia_mysql-datos` y `odontia_api-datos`, más el
  `deploy/.env` (sin él no se puede usar el volumen de MySQL ni validar las sesiones). Nunca usar `docker compose down -v`, ni
  `docker system prune` o `docker volume prune`, que además afectan a los otros proyectos del servidor.
- **Swagger** está desactivado en producción (`Swagger__Enabled=false`).

### Primer uso

1. Entrar a https://odentix.duckdns.org, elegir **Crear una cuenta** y registrar el consultorio.
2. Abrir el correo de confirmación y seguir el enlace.
3. Iniciar sesión. Desde ahí se puede invitar al resto del equipo.
4. Con el consultorio ya creado, cerrar el registro público: en `deploy/.env` poner `PERMITIR_REGISTRO=false` y volver a ejecutar
   `bash actualizar-servidor.sh` desde `~/odontia/deploy`.

## Alternativa sin servidor propio: Vercel + MonsterASP.NET

Sirve para demostraciones, pruebas y uso académico, sin costo. **No usar con datos reales de pacientes:** un plan gratuito no
garantiza copias de seguridad ni continuidad.

```text
Navegador ──► Vercel (frontend) ──/api/*──► MonsterASP.NET (API .NET) ──► MySQL
```

| Parte | Servicio | Plan gratuito |
|---|---|---|
| Frontend (Angular) | [Vercel](https://vercel.com) | Sitio estático |
| API (.NET 10) y archivos | [MonsterASP.NET](https://www.monsterasp.net/ASP.NET-Freehosting/) | 1 sitio, 5 GB de disco, 256 MB de RAM |
| Base de datos MySQL | MonsterASP.NET | 1 base de 1 GB |
| Correo | [Brevo](https://www.brevo.com/free-smtp-server) o Gmail | El SMTP de MonsterASP.NET no está en el plan gratuito |

1. **API y base:** en MonsterASP.NET crear un sitio (`https://NOMBRE.runasp.net`) y una base MySQL. Copiar
   `src/Odontia.Api/appsettings.Production.example.json` como `appsettings.Production.json` (excluido de git; no subirlo) y completar
   `ConnectionStrings:Default`, `Jwt:SigningKey` (32 caracteres o más), `Frontend:BaseUrl` (la dirección de Vercel) y `Email:*`.
2. **Publicar la API:** `dotnet publish src/Odontia.Api -c Release -o publish/api` desde la carpeta `Odontia` y subir el contenido
   de `publish/api` a `wwwroot` por FTP o Web Deploy. No borrar `App_Data/` al volver a publicar (documentos y claves de cifrado).
   `https://NOMBRE.runasp.net/health/ready` debe responder `Healthy`; la primera vez la API crea las tablas sola.
3. **Frontend:** en Vercel crear el proyecto con **Root Directory** = `frontend` y la variable `ODONTIA_API_URL` =
   `https://NOMBRE.runasp.net` (sin barra final). La configuración está en [`frontend/vercel.json`](../frontend/vercel.json).
4. **Primer uso:** igual que en producción; para cerrar el registro, `Auth:AllowPublicRegistration` = `false` en
   `appsettings.Production.json`.

Limitaciones:

- El plan gratuito puede detener la aplicación tras inactividad: la primera solicitud tarda más y no corren las tareas en segundo
  plano (recordatorios).
- La API ve todas las solicitudes llegar desde Vercel, así que el límite por IP se comparte entre todos los usuarios; por eso la
  plantilla sube `RateLimiting:AuthPermitPerMinute` a 60.
- 256 MB de memoria es justo para la API.
- Sin dominio propio en el plan gratuito de MonsterASP.NET.

Estado: se probó localmente que la API publicada arranca en modo `Production` con la plantilla y que el frontend compila desde un
clon limpio. No se probó el despliegue real en MonsterASP.NET ni en Vercel (falta confirmar la versión de MySQL del plan gratuito y
que permita SMTP saliente).
