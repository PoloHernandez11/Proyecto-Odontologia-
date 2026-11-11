# Despliegue gratuito: Vercel + MonsterASP.NET

Esta guía publica Odontia sin costo, pensada para demostraciones, pruebas y uso académico.

| Parte | Servicio | Plan gratuito |
|---|---|---|
| Frontend (Angular) | [Vercel](https://vercel.com) | Sitio estático |
| API (.NET 10) y archivos | [MonsterASP.NET](https://www.monsterasp.net/ASP.NET-Freehosting/) | 1 sitio, 5 GB de disco, 256 MB de RAM, sin tarjeta |
| Base de datos MySQL | MonsterASP.NET | 1 base de 1 GB |
| Correo | [Brevo](https://www.brevo.com/free-smtp-server) o Gmail | 300 correos por día |

> **No usar con datos reales de pacientes.** Un plan gratuito no garantiza copias de seguridad ni continuidad: el proveedor puede
> suspender o archivar el sitio (por ejemplo, tras meses sin uso). Para un consultorio real hace falta un plan pago.

El navegador sólo habla con Vercel. Vercel reenvía `/api/*` a la API, así que las cookies de sesión funcionan sin configurar CORS.

```text
Navegador ──► Vercel (frontend) ──/api/*──► MonsterASP.NET (API .NET) ──► MySQL
```

## 1. Correo saliente

La API exige confirmar el correo antes del primer ingreso, así que necesita un servidor SMTP. El SMTP propio de MonsterASP.NET
no está disponible en el plan gratuito; hay que usar uno externo.

- **Brevo:** crear la cuenta, verificar el remitente y copiar el usuario y la clave SMTP (`smtp-relay.brevo.com`, puerto 587).
- **Gmail (alternativa):** activar la verificación en dos pasos y generar una "contraseña de aplicación" (`smtp.gmail.com`, puerto 587).

## 2. API y base de datos en MonsterASP.NET

1. Crear la cuenta gratuita y un sitio web. Anotar su dirección (`https://NOMBRE.runasp.net`).
2. Crear una base de datos **MySQL** y anotar servidor, nombre de la base, usuario y contraseña.
3. Copiar `src/Odontia.Api/appsettings.Production.example.json` como `appsettings.Production.json` en la misma carpeta y completar:

   | Clave | Valor |
   |---|---|
   | `ConnectionStrings:Default` | Datos de la base MySQL creada en el paso 2 |
   | `Jwt:SigningKey` | Una cadena aleatoria de 32 caracteres o más (no reutilizar la de desarrollo) |
   | `Frontend:BaseUrl` | La dirección del sitio en Vercel (se conoce en el paso 3; se puede completar después) |
   | `Email:*` | Remitente verificado y credenciales SMTP del paso 1 |

   Ese archivo contiene contraseñas: está excluido de git y **no debe subirse al repositorio**.
4. Publicar la API. Desde la carpeta `Odontia`:

   ```bash
   dotnet publish src/Odontia.Api -c Release -o publish/api
   ```

   Subir el contenido de `publish/api` a la carpeta raíz del sitio (`wwwroot`) por FTP o Web Deploy, con los datos que muestra el
   panel de MonsterASP.NET. El panel también permite publicar desde GitHub; en ese caso hay que crear `appsettings.Production.json`
   directamente en el servidor y marcarlo como archivo protegido para que una publicación no lo pise.
5. Abrir `https://NOMBRE.runasp.net/health/ready`. Debe responder `Healthy`. La primera vez la API crea las tablas sola
   (`Database:ApplyMigrationsOnStartup` está en `true` en la plantilla).

La API guarda los documentos y sus claves de cifrado en `App_Data/` dentro del sitio. Al volver a publicar, no borrar esa carpeta
(en Web Deploy, desactivar "eliminar archivos adicionales en el destino").

## 3. Frontend en Vercel

1. Crear un proyecto en Vercel a partir del repositorio, con **Root Directory** = `frontend`.
2. Agregar la variable de entorno `ODONTIA_API_URL` = `https://NOMBRE.runasp.net` (sin barra final).
3. Desplegar. Después, completar `Frontend:BaseUrl` en `appsettings.Production.json` con la dirección que asignó Vercel y volver a
   subir ese archivo: los enlaces de los correos (confirmación, invitaciones, contraseña) apuntan a esa dirección.

## 4. Primer uso

1. En el sitio de Vercel, entrar a **Crear una cuenta** y registrar el consultorio.
2. Abrir el correo de confirmación y seguir el enlace.
3. Iniciar sesión. Desde ahí se puede invitar al resto del equipo.
4. Cuando el consultorio ya esté creado, cambiar `Auth:AllowPublicRegistration` a `false` y volver a subir la configuración,
   para que nadie más pueda registrar consultorios.

## Limitaciones de esta configuración

- **Arranque lento tras inactividad:** el plan gratuito puede detener la aplicación cuando no se usa; la primera solicitud tarda más.
  Mientras está detenida no corren las tareas en segundo plano (recordatorios).
- **Límite de solicitudes compartido:** la API ve todas las solicitudes llegar desde Vercel, así que el límite por dirección IP se
  comparte entre todos los usuarios. Por eso la plantilla sube `RateLimiting:AuthPermitPerMinute` a 60.
- **Memoria:** 256 MB es justo para la API. Si el sitio se reinicia seguido, es la primera causa a revisar.
- **Sin dominio propio** en el plan gratuito de MonsterASP.NET; no afecta a los usuarios, que sólo ven la dirección de Vercel.

## Qué se verificó y qué no

Se probó localmente que la API publicada arranca en modo `Production` con esta plantilla (estado saludable, inicio de sesión, cookies
`Secure`, claves guardadas en `App_Data/keys`) y que el frontend compila desde un clon limpio con `npm ci` y `npm run build`.
No se probó el despliegue real en MonsterASP.NET ni en Vercel; en particular falta confirmar la versión de MySQL que ofrece el plan
gratuito y que su red permita la conexión SMTP saliente.
