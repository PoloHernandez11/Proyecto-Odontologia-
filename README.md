# Sistema de Ficha Dental

Sistema web de gestión para un consultorio odontológico (secretaria y dentista).

## Stack

- Frontend: React
- Backend: C# / ASP.NET Core Web API
- Base de datos: MySQL

## Estructura

```
.
├── database/
│   ├── ficha_dental.sql
│   └── datos_prueba.sql
├── docs/
│   └── ficha_tecnica_bd.md
├── .gitignore
└── README.md
```

## Base de datos

1. Desde el cliente de MySQL, ejecutar (usar `/` en la ruta):

   ```sql
   SOURCE C:/ruta/al/repo/database/ficha_dental.sql;
   SOURCE C:/ruta/al/repo/database/datos_prueba.sql;
   ```

   El segundo script es opcional (datos de ejemplo).

2. Crear el archivo local `appsettings.Development.json` con tu usuario y contraseña de MySQL (ya está en `.gitignore`, no se sube):

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Port=3306;Database=ficha_dental;User=TU_USUARIO;Password=TU_PASSWORD;"
     }
   }
   ```

Si hay cambios en la base, se modifica `database/ficha_dental.sql` y se hace commit.

Detalle de tablas y relaciones: [ficha técnica](docs/ficha_tecnica_bd.md).
