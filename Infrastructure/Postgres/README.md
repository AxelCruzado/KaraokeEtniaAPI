# PostgreSQL y convivencia con MySQL

Database__Provider=PostgreSQL registra PostgresAppDbContext y PostgresCajaDbContext
como los contextos de la API. Cada uno tiene sus propias migraciones e historial.
MySQL conserva AppDbContext/CajaDbContext e historiales originales; no se ejecutan
migraciones de un motor sobre el otro ni se importa información automáticamente.

DatabaseRows delimita identificadores mediante los metadatos/proveedor de EF y
parametriza los valores. Mantiene FOR UPDATE en compras y turnos y la transacción
compartida de compra, stock y egreso.

Las fechas se almacenan como timestamp without time zone: tiempos operativos
mantienen el reloj local del turno; campos terminados en Utc recuperan Kind=Utc
para consultas/JSON. El conversor no modifica los ticks ni convierte un turno de
Lima en UTC. No usar EnableLegacyTimestampBehavior global para evitar validaciones.

En Render Storage__Backend=Database conserva imágenes en StoredImages; no añade
un módulo de negocio. Las imágenes se insertan en el mismo SaveChanges que la
categoría/producto; sus rutas públicas siguen /images/<carpeta>/<archivo>. El
contenido cuenta en la cuota de PostgreSQL. La VM MySQL conserva almacenamiento
en disco con su volumen.

PostgresConfiguration admite URI postgres/postgresql o formato Npgsql. Producción
exige VerifyFull; admite un archivo CA por Database__RootCertificate o un PEM por
Database__CaCertificatePem. Nunca registrar ConnectionString, contraseñas ni JWT.
La conexión se limita por defecto a 5 clientes de pool por proceso.

## Verificación local ejecutada

- Compilación .NET 10 y migración de ambas áreas sobre PostgreSQL 18.
- Las cuatro suites de integración existentes con fixtures locales para cada motor.
- Production local: URI, CA TLS, credencial inicial privada, CORS y persistencia de
  imágenes tras reiniciar API (postgres_production_smoke.py).
- Respaldo/restauración PostgreSQL en base nueva: tablas, filas, secuencias y bytes
  de imágenes comparados (postgres_restore_integration.py).
- Navegador con bundle Angular compilado y proxy local a PostgreSQL.

Precondiciones de pruebas: API de ensayo en localhost, contenedor
etnia-dev-postgres, usuario etnia y base etnia_pg; los scripts de Production/TLS
usan rutas locales explícitas bajo /workspace/.setup y certificados de prueba.
CAJA_TEST_PROVIDER=PostgreSQL selecciona fixtures; CAJA_TEST_BASE_URL debe ser
localhost/127.0.0.1. Nunca ejecutar estas suites sobre Aiven ni datos comerciales.

Estas comprobaciones no acreditan despliegue Render/Aiven, acceso por internet ni
WebSocket tras el proxy público. Conservar los logs de esta revisión separados de
las evidencias MySQL de noviembre de 2026 ya incluidas en la tesis.
