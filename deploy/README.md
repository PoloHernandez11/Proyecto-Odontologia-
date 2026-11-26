# Despliegue de Odontia con Docker Compose

Odontia corre en el servidor compartido (Oracle Linux ARM, usuario `opc`) junto a los otros proyectos:

- `odontia-api` (ASP.NET Core 10, puerto interno 8080) y `odontia-web` (nginx con el build de Angular, puerto interno 80) se unen a la red externa `miaula_default` con esos alias.
- `odontia-mysql` (MySQL 8.4) solo está en la red privada `odontia_interna`, sin salida a internet.
- Ningún contenedor publica puertos. El Caddy de miaula (`miaula-web-1`) termina TLS e importa `~/sitios-caddy/odontia.caddy`: `/api/*` y `/health/live` van a la API; el resto de `/health/*` (incluido `/health/ready`) responde 404 desde afuera; todo lo demás va al frontend. El healthcheck de la imagen de la API consulta `/health/ready` dentro del propio contenedor (solo mira el código 200), así que no depende de Caddy.
- La API solo acepta cabeceras `X-Forwarded-*` que vengan del contenedor `miaula-web-1` (`ReverseProxy__KnownProxyHosts__0` en `docker-compose.yml`). La IP se resuelve con el DNS de Docker y se vuelve a resolver cada 60 s o cuando llega una IP desconocida (si Caddy se recrea con otra IP). Si la resolución falla, se descartan esas cabeceras (se registra un aviso) y la API ve la IP de Caddy en vez de la del cliente: afecta al rate limiting y a la IP de auditoría, no a la disponibilidad. Los demás contenedores de `miaula_default` ya no pueden falsear la IP del cliente.
- El frontend (nginx) envía `Content-Security-Policy` estricta (`script-src 'self'`, sin scripts en línea), `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`, `X-Frame-Options` y HSTS; caché de un año para recursos con hash y `no-cache` para `index.html`. Para que la CSP no rompa los estilos, `frontend/angular.json` desactiva `optimization.styles.inlineCritical` (el CSS crítico en línea usa un `<script>` en línea). Si se agregan recursos de otro origen hay que sumarlos a la CSP en `nginx.conf`.
- El registro público de consultorios está abierto (`PERMITIR_REGISTRO=true`; si la variable falta, también queda abierto). Para cerrarlo, poner `PERMITIR_REGISTRO=false`.
- Datos persistentes: volúmenes `odontia_mysql-datos` (base) y `odontia_api-datos` (`/data`: claves de Data Protection y documentos).

Archivos: `docker-compose.yml`, `Dockerfile.api`, `Dockerfile.web`, `nginx.conf`, `odontia.caddy.plantilla`, `.env.example`, `actualizar-servidor.sh`.

## Subir el código (desde la PC de desarrollo)

`git archive` empaqueta solo lo que está commiteado (el `.env` nunca viaja):

```bash
git archive --format=tar.gz -o odontia.tar.gz HEAD
scp odontia.tar.gz opc@<servidor>:~/
```

## Primera instalación (en el servidor)

```bash
mkdir -p ~/odontia && tar -xzf ~/odontia.tar.gz -C ~/odontia
cd ~/odontia/deploy
bash actualizar-servidor.sh     # crea .env (chmod 600) con contraseñas y clave JWT generadas, y se detiene
nano .env                       # completar SMTP_HOST, SMTP_USUARIO, SMTP_REMITENTE, SMTP_CLAVE
bash actualizar-servidor.sh     # construye, levanta, espera "healthy", escribe el sitio en Caddy y recarga
```

La primera compilación en ARM puede tardar bastante. La API aplica las migraciones al iniciar.

## Actualizar

Se conserva la versión anterior en `~/odontia-anterior`, igual que en los otros proyectos:

```bash
cd ~
rm -rf ~/odontia-anterior
mv ~/odontia ~/odontia-anterior
mkdir ~/odontia && tar -xzf ~/odontia.tar.gz -C ~/odontia
cp -p ~/odontia-anterior/deploy/.env ~/odontia/deploy/.env
cd ~/odontia/deploy && bash actualizar-servidor.sh
```

La API aplica las migraciones al iniciar y no se revierten solas. Por eso `actualizar-servidor.sh`, si `odontia-mysql` está corriendo, hace primero un respaldo de la base en `~/respaldos/odontia-db-AAAA-MM-DD-HHMMSS-pre-actualizacion.sql.gz` (mismo `mysqldump` que en "Respaldo"; la contraseña se lee dentro del contenedor). Verifica que el archivo sea un gzip válido, que contenga tablas y que el volcado esté completo; si algo falla, borra el archivo parcial y **aborta sin tocar los contenedores**. Se conservan los últimos 10 respaldos automáticos (`RESPALDOS_A_CONSERVAR` en el script); los respaldos manuales con otro nombre no se borran. Los documentos (`/data`) no entran en este respaldo automático: respaldarlos a mano (ver abajo) antes de cambios importantes.

## Registro público de consultorios

El registro público está abierto: cualquiera puede dar de alta un consultorio desde `https://<dominio>/registro`.
Un `.env` creado con una versión anterior puede tener `PERMITIR_REGISTRO=false`; en ese caso, abrirlo con:

```bash
cd ~/odontia/deploy
sed -i 's/^PERMITIR_REGISTRO=.*/PERMITIR_REGISTRO=true/' .env && bash actualizar-servidor.sh
```

Para cerrarlo (solo se entra por invitación), usar el mismo comando con `PERMITIR_REGISTRO=false`.

## Logs y estado

```bash
cd ~/odontia/deploy
docker compose ps
docker compose logs -f --tail=100 odontia-api
docker compose logs --tail=100 odontia-mysql
docker stats --no-stream odontia-odontia-api-1 odontia-odontia-mysql-1 odontia-odontia-web-1
```

## Respaldo

```bash
mkdir -p ~/respaldos && cd ~/odontia/deploy
# Base de datos (la contraseña se lee dentro del contenedor, no queda en el historial)
docker compose exec -T odontia-mysql sh -c 'export MYSQL_PWD="$MYSQL_ROOT_PASSWORD"; exec mysqldump -uroot --single-transaction --routines --triggers --set-gtid-purged=OFF --databases odontia' \
  | gzip > ~/respaldos/odontia-db-$(date +%F-%H%M).sql.gz
# Documentos y claves de Data Protection
docker compose exec -T odontia-api tar -czf - -C /data . > ~/respaldos/odontia-data-$(date +%F-%H%M).tar.gz
```

Restaurar la base:

```bash
gunzip -c ~/respaldos/odontia-db-AAAA-MM-DD-HHMM.sql.gz \
  | docker compose exec -T odontia-mysql sh -c 'export MYSQL_PWD="$MYSQL_ROOT_PASSWORD"; exec mysql -uroot'
```

Guardar también una copia del `.env` fuera del servidor: sin él no se puede usar el volumen de MySQL ni validar las sesiones.

## Volver a la versión anterior

```bash
cd ~
mv ~/odontia ~/odontia-fallida
mv ~/odontia-anterior ~/odontia
cd ~/odontia/deploy && bash actualizar-servidor.sh
```

Si la versión fallida aplicó migraciones incompatibles, restaurar además el respaldo de la base.

## Notas

- `MYSQL_PASSWORD` y `MYSQL_ROOT_PASSWORD` solo se aplican cuando se crea el volumen; cambiarlas después requiere `ALTER USER` dentro de MySQL.
- Nunca usar `docker compose down -v`: borra los volúmenes con los datos.
- No ejecutar `docker system prune` ni `docker volume prune`: afectan a los otros proyectos del servidor.
- Comprobar que la API resuelve el Caddy: `docker exec odontia-odontia-api-1 getent hosts miaula-web-1` debe mostrar la IP actual de `miaula-web-1` en `miaula_default`. En los logs de la API aparece `Proxys de confianza (miaula-web-1): <IP>` con la primera petición que llega por Caddy; un aviso `No se pudo resolver el proxy de confianza` indica que no se está confiando en Caddy.
- Para quitar Odontia del Caddy: borrar `~/sitios-caddy/odontia.caddy`, validar y recargar `miaula-web-1` como hace el script.
