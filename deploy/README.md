# Despliegue de Odontia con Docker Compose

Odontia corre en el servidor compartido (Oracle Linux ARM, usuario `opc`) junto a los otros proyectos:

- `odontia-api` (ASP.NET Core 10, puerto interno 8080) y `odontia-web` (nginx con el build de Angular, puerto interno 80) se unen a la red externa `miaula_default` con esos alias.
- `odontia-mysql` (MySQL 8.4) solo está en la red privada `odontia_interna`, sin salida a internet.
- Ningún contenedor publica puertos. El Caddy de miaula (`miaula-web-1`) termina TLS e importa `~/sitios-caddy/odontia.caddy`: `/api/*` y `/health/*` van a la API; el resto, al frontend.
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

Antes de actualizar conviene hacer un respaldo (ver abajo): las migraciones no se revierten solas.

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
- Para quitar Odontia del Caddy: borrar `~/sitios-caddy/odontia.caddy`, validar y recargar `miaula-web-1` como hace el script.
