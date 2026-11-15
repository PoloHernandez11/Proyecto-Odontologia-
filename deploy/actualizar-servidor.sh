#!/usr/bin/env bash
# Instala o actualiza Odontia en el servidor compartido.
# Se ejecuta EN EL SERVIDOR desde ~/odontia/deploy, después de copiar el código en ~/odontia:
#   cd ~/odontia/deploy && bash actualizar-servidor.sh
# Es idempotente: se puede correr las veces que haga falta.
# Solo toca: ~/odontia, ~/sitios-caddy/odontia.caddy y "caddy validate/reload" en miaula-web-1.
# No detiene, borra ni modifica contenedores, volúmenes o archivos de otros proyectos.
set -euo pipefail

DIR_DEPLOY="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARCHIVO_ENV="$DIR_DEPLOY/.env"
ARCHIVO_COMPOSE="$DIR_DEPLOY/docker-compose.yml"
PLANTILLA_CADDY="$DIR_DEPLOY/odontia.caddy.plantilla"
DIR_CADDY="$HOME/sitios-caddy"
SITIO_CADDY="$DIR_CADDY/odontia.caddy"
CONTENEDOR_CADDY="miaula-web-1"
RED_COMPARTIDA="miaula_default"
ESPERA_MAXIMA=420   # segundos para que la API quede "healthy" (la 1.ª vez aplica migraciones)

compose() { docker compose -f "$ARCHIVO_COMPOSE" --env-file "$ARCHIVO_ENV" "$@"; }
falla() { echo "ERROR: $*" >&2; exit 1; }
# Lee una variable del .env sin ejecutarlo (última aparición, valor tal cual).
leer_env() { grep -E "^$1=" "$ARCHIVO_ENV" | tail -n 1 | cut -d= -f2- || true; }

cd "$DIR_DEPLOY"

# ---------- 1. Requisitos ----------
command -v docker >/dev/null || falla "docker no está instalado."
docker compose version >/dev/null 2>&1 || falla "falta el plugin 'docker compose'."
command -v openssl >/dev/null || falla "falta openssl."
[ -f "$ARCHIVO_COMPOSE" ] || falla "no existe $ARCHIVO_COMPOSE."
[ -f "$PLANTILLA_CADDY" ] || falla "no existe $PLANTILLA_CADDY."
docker network inspect "$RED_COMPARTIDA" >/dev/null 2>&1 || falla "no existe la red $RED_COMPARTIDA (¿está levantado miaula?)."
[ "$(docker inspect -f '{{.State.Running}}' "$CONTENEDOR_CADDY" 2>/dev/null || echo false)" = "true" ] \
  || falla "el contenedor $CONTENEDOR_CADDY no está corriendo."
[ -d "$DIR_CADDY" ] || falla "no existe $DIR_CADDY."

# ---------- 2. Archivo .env ----------
if [ ! -f "$ARCHIVO_ENV" ]; then
  [ -f "$DIR_DEPLOY/.env.example" ] || falla "no existe .env.example para crear el .env."
  echo "No hay .env: se crea a partir de .env.example con contraseñas y clave JWT nuevas."
  umask 077
  sed -e "s/^MYSQL_PASSWORD=.*/MYSQL_PASSWORD=$(openssl rand -hex 24)/" \
      -e "s/^MYSQL_ROOT_PASSWORD=.*/MYSQL_ROOT_PASSWORD=$(openssl rand -hex 24)/" \
      -e "s/^JWT_SIGNING_KEY=.*/JWT_SIGNING_KEY=$(openssl rand -hex 32)/" \
      "$DIR_DEPLOY/.env.example" > "$ARCHIVO_ENV.nuevo"
  chmod 600 "$ARCHIVO_ENV.nuevo"
  mv "$ARCHIVO_ENV.nuevo" "$ARCHIVO_ENV"
  echo
  echo "Se creó $ARCHIVO_ENV."
  echo "Completá los valores SMTP_* (y revisá DOMINIO y PERMITIR_REGISTRO) con:"
  echo "  nano $ARCHIVO_ENV"
  echo "y volvé a ejecutar: bash actualizar-servidor.sh"
  exit 0
fi
chmod 600 "$ARCHIVO_ENV"

# Validaciones del .env (sin mostrar valores).
pendientes="$(grep -E '^[A-Z_]+=CAMBIAR_' "$ARCHIVO_ENV" | cut -d= -f1 || true)"
[ -z "$pendientes" ] || falla "faltan completar en .env: $(echo "$pendientes" | tr '\n' ' ')"
for var in DOMINIO MYSQL_PASSWORD MYSQL_ROOT_PASSWORD JWT_SIGNING_KEY SMTP_HOST SMTP_USUARIO SMTP_REMITENTE SMTP_CLAVE; do
  [ -n "$(leer_env "$var")" ] || falla "la variable $var está vacía o no existe en .env."
done
DOMINIO="$(leer_env DOMINIO)"
[[ "$DOMINIO" =~ ^[A-Za-z0-9]([A-Za-z0-9.-]*[A-Za-z0-9])?$ ]] || falla "DOMINIO no es un nombre de host válido."
[[ "$(leer_env MYSQL_PASSWORD)" =~ ^[A-Za-z0-9]+$ ]] || falla "MYSQL_PASSWORD debe tener solo letras y números."
[ "$(leer_env JWT_SIGNING_KEY | tr -d '\n' | wc -c)" -ge 32 ] || falla "JWT_SIGNING_KEY debe tener al menos 32 caracteres."

# ---------- 3. Construir y levantar ----------
compose config --quiet || falla "docker-compose.yml o .env inválidos."
echo "Construyendo y levantando Odontia (puede tardar varios minutos en ARM)..."
compose up -d --build --remove-orphans

# ---------- 4. Esperar a que los contenedores estén sanos ----------
esperar_sano() {
  local servicio="$1" limite="$2" id estado inicio
  id="$(compose ps -q "$servicio")"
  [ -n "$id" ] || falla "no se encontró el contenedor de $servicio."
  inicio=$SECONDS
  while :; do
    estado="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$id" 2>/dev/null || echo desconocido)"
    case "$estado" in
      healthy) echo "  $servicio: healthy"; return 0 ;;
      unhealthy|exited|dead) break ;;
    esac
    if [ "$(docker inspect -f '{{.RestartCount}}' "$id" 2>/dev/null || echo 0)" -gt 2 ]; then
      estado="reiniciando en bucle"; break
    fi
    [ $((SECONDS - inicio)) -lt "$limite" ] || { estado="sin respuesta tras ${limite}s"; break; }
    sleep 5
  done
  echo "El servicio $servicio no quedó sano ($estado). Últimas líneas del log:" >&2
  compose logs --tail=60 "$servicio" >&2 || true
  exit 1
}
echo "Esperando a que los servicios estén sanos..."
esperar_sano odontia-mysql 180
esperar_sano odontia-api "$ESPERA_MAXIMA"
esperar_sano odontia-web 60

# ---------- 5. Sitio en Caddy (escritura atómica, validar y recargar) ----------
nuevo="$SITIO_CADDY.nuevo"      # no termina en .caddy: Caddy no lo importa
respaldo="$SITIO_CADDY.anterior"
sed "s/__DOMINIO__/$DOMINIO/g" "$PLANTILLA_CADDY" > "$nuevo"
chmod 644 "$nuevo"
if [ -f "$SITIO_CADDY" ]; then cp -p "$SITIO_CADDY" "$respaldo"; fi
mv "$nuevo" "$SITIO_CADDY"

if docker exec "$CONTENEDOR_CADDY" caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile; then
  docker exec "$CONTENEDOR_CADDY" caddy reload --config /etc/caddy/Caddyfile --adapter caddyfile
  rm -f "$respaldo"
else
  # Configuración inválida: se deja todo como estaba para no afectar a los demás sitios.
  if [ -f "$respaldo" ]; then mv "$respaldo" "$SITIO_CADDY"; else rm -f "$SITIO_CADDY"; fi
  falla "Caddy rechazó la configuración; se restauró la anterior y no se recargó."
fi

# ---------- 6. Resumen ----------
echo
echo "Odontia actualizado."
compose ps
echo
echo "Sitio:  https://$DOMINIO"
echo "Salud:  https://$DOMINIO/health/ready"
echo "Logs:   cd $DIR_DEPLOY && docker compose logs -f odontia-api"
