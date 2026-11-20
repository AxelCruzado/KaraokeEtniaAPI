# Karaoke Etnia API

Requiere .NET SDK 10 y MySQL 8 o PostgreSQL 18. El proveedor Pomelo y las migraciones mantienen
EF Core 9. Configurar ConnectionStrings__DefaultConnection y las variables JWT
fuera del repositorio. No se incluyen claves ni conexión predeterminada.

Para Render Free + Aiven PostgreSQL Free, consulte
[deploy/RENDER_AIVEN.md](deploy/RENDER_AIVEN.md). Las migraciones y las pruebas
PostgreSQL son independientes de las de MySQL.

La guía para publicar frontend, API y base en una VM está en
[deploy/DESPLIEGUE.md](deploy/DESPLIEGUE.md). Incluye HTTPS, persistencia,
credenciales iniciales privadas, respaldo y recolección F-01. Requiere una cuenta
y una máquina del titular; el repositorio no implica que exista un despliegue.

Para desarrollo usar ASPNETCORE_ENVIRONMENT=Development. Para una base nueva
Database__MigrateOnStartup=true aplica AppDbContext y CajaDbContext. En producción
la aplicación exige clave JWT privada y Bootstrap__AdminPassword cuando encuentra
la cuenta histórica predeterminada. Ese proceso no modifica administradores que
ya tengan una contraseña privada distinta.

Las suites `tests/*integration.py` se ejecutan exclusivamente contra una API local
y datos de ensayo. `tests/despliegue_smoke.py` verifica una instalación Production
local sobre una base nueva. Consulte su configuración antes de reproducirla; sus
rutas de SDK, binario y MySQL corresponden al entorno de validación del proyecto.
