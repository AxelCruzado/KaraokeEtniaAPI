# Render Free + Aiven PostgreSQL Free

Esta ruta usa la base **PostgreSQL Free** creada por el titular. No utiliza los
créditos temporales ni un plan Developer. Los límites y la continuidad dependen
de los proveedores: Aiven puede apagar servicios inactivos y Render Free puede
suspender la API por inactividad. No se promete disponibilidad permanente ni SLA.
No cambia ni importa automáticamente datos de MySQL; se inicializa una base nueva.
Las evidencias históricas MySQL no se presentan como pruebas de PostgreSQL.

## 1. Aiven: esperar Running y guardar conexión privadamente

- Servicio: etnia-postgresql, PostgreSQL 18, nivel Free; confirmar precio mensual Free.
- Esperar que Building pase a Running. No pulsar Upgrade.
- En Connection information copiar la URI real para utilizarla únicamente en Render.
  No enviarla al chat, capturas públicas, repositorios ni al informe de tesis.
- Descargar el **certificado CA** del servicio. Es un certificado público, no una
  clave privada. Se usa para comprobar certificado y hostname; no desactivar TLS.
- Conservar nombre del servicio y fecha de creación como evidencia de provisión;
  eso todavía no acredita que la aplicación funcione ni que F-01 esté completa.

## 2. Crear la API en Render

En el Dashboard: **New → Web Service**, conectar el repositorio
**AxelCruzado/KaraokeEtniaAPI** y completar:

| Campo | Valor |
| --- | --- |
| Name | etnia-api (o nombre disponible) |
| Branch | work |
| Runtime | Docker |
| Dockerfile Path | ./deploy/render.Dockerfile |
| Instance Type | Free, US$0 |
| Health Check Path | /health/ready |

No crear una base de Render ni añadir discos de pago. Revisar el resumen del plan
antes de crear el servicio. La API necesita la base externa de Aiven, no una base
dentro del disco efímero de Render.

### Variables de entorno

Agregar en Environment, sin comillas exteriores:

| Clave | Valor |
| --- | --- |
| ASPNETCORE_ENVIRONMENT | Production |
| Database__Provider | PostgreSQL |
| Database__MigrateOnStartup | true |
| ConnectionStrings__DefaultConnection | URI real de Aiven, copiada en privado |
| Storage__Backend | Database |
| Deployment__Render | true |
| Deployment__Label | Despliegue_Render |
| Jwt__Issuer | EtniaAPI |
| Jwt__Audience | EtniaWeb |
| Jwt__Key | Clave aleatoria privada de al menos 32 caracteres; no reutilizar ejemplos |
| Bootstrap__AdminPassword | Contraseña privada nueva de al menos 16 caracteres |
| Caja__DatosPrueba | false |

En **Secret Files**, crear `aiven-ca.crt` y pegar el contenido completo del CA,
con BEGIN CERTIFICATE y END CERTIFICATE. Agregar también la variable
`Database__RootCertificate=/etc/secrets/aiven-ca.crt`.

Alternativa: guardar el PEM completo como variable multilinea
`Database__CaCertificatePem`; la aplicación validará el PEM y lo escribirá en un
archivo temporal exclusivamente para la conexión TLS. Usar una de las dos rutas.
La URI de Aiven se convierte internamente al formato de Npgsql; para URI se exige
VerifyFull. No sustituirlo por Trust Server Certificate ni desactivar la validación.

Render proporciona RENDER_GIT_COMMIT al build Docker. Si un build indica que falta
la revisión, agregar SOURCE_REVISION con el SHA completo de la rama work que se
está construyendo. No usar un commit de otra entrega. La revisión se integra en
el binario y se consulta en /api/version.

Al crear la API, las migraciones PostgreSQL crean las tablas operativas, caja e
imágenes en Aiven. El usuario inicial es admin, con la contraseña privada indicada.
No se cargan datos TEST, CICLO, VERSION o UI ni movimientos de la tesis. Guardar la
URL pública real de la API. Verificar en el navegador:

- https://SU_API.onrender.com/health/ready → disponible.
- https://SU_API.onrender.com/api/version → revisión y baseDeDatos PostgreSQL.

No interpretar una respuesta 200 aislada como aceptación del establecimiento.

## 3. Crear frontend como Static Site

Render: **New → Static Site**, conectar
**AxelCruzado/karaoke-etnia-frontend**, rama **work**:

| Campo | Valor |
| --- | --- |
| Build Command | npm ci && npm run build:cloud |
| Publish Directory | dist/karaoke-etnia-frontend/browser |
| NODE_VERSION | 22 |
| ETNIA_API_URL | URL HTTPS real de la API, sin /api y sin barra final |

El sitio estático usa la modalidad gratuita, no un Web Service de pago. Agregar una
regla **Rewrite**, origen `/*`, destino `/index.html`, para las rutas Angular.
Agregar Cache-Control `no-store` a `/runtime-config.js` y `/version.json`.
El render.yaml del repositorio describe estas opciones si se prefiere un Blueprint.

## 4. Habilitar el origen del frontend

Copiar la URL pública real del Static Site. En la API añadir:

`Cors__AllowedOrigins__0=https://SU_FRONTEND.onrender.com`

Sin barra final ni rutas. Guardar y esperar el nuevo deploy. No usar `*` con
credenciales ni permitir todos los sitios. El dominio del frontend es distinto del
backend; esto no necesita comprar un dominio propio.

## 5. Verificación del despliegue

1. Abrir /login en el sitio estático y entrar con la cuenta administrativa privada.
2. Verificar HTTPS válido, acceso a gestión y /caja, y recarga directa de rutas.
3. Guardar una imagen de catálogo y comprobarla después de redeploy: el contenido
   se guarda en StoredImages dentro de PostgreSQL. Cuenta en la cuota de 1 GB de
   Aiven; usar imágenes pequeñas. No depende del disco efímero de Render.
4. Comprobar SignalR desde las pantallas operativas: /pedidoHub/negotiate y conexión
   posterior. Las pruebas locales de navegador no acreditan WebSocket público.
5. Confirmar versión del frontend en /version.json y backend en /api/version.
6. Recoger los archivos de F-01 con las dos direcciones:

```sh
python3 deploy/collect_f01.py --url https://SU_FRONTEND.onrender.com \
 --api-url https://SU_API.onrender.com \
 --backend-ref SHA_COMPLETO_BACKEND --frontend-ref SHA_COMPLETO_FRONTEND \
 --responsable 'NOMBRE REAL' --cargo 'CARGO REAL' --out deploy/f01/registro-render-001
```

Registrar fecha y responsable reales y revisar/firmar la ficha. No pegar contraseñas
en los registros ni anexos. Las pruebas destructivas o de rechazo se hacen sobre
otra base aislada, no sobre el servicio que contenga operaciones reales.

## Respaldo

El backup.py de la VM corresponde a MySQL y no se utiliza en esta ruta. Conservar
copias privadas de PostgreSQL mediante pg_dump (versión compatible con PostgreSQL
18), verificando TLS con el CA de Aiven. El respaldo incluye las imágenes guardadas
en la base y hashes de cuentas; no publicarlo ni incluirlo sin anonimizar en la tesis.
La restauración debe probarse en otra base, no sobre defaultdb en uso.

## Alcance y convivencia

MySQL continúa disponible con Database__Provider=MySQL (valor por defecto) y sus
migraciones originales. PostgreSQL usa contextos e historiales independientes.
Las fechas locales de turnos mantienen el contrato America/Lima y las marcas *Utc
se serializan como UTC. Esta adaptación no migra operaciones reales del negocio;
si existen datos MySQL que deban trasladarse, definir y validar esa migración aparte.

No contratar Developer ni Professional para continuar esta guía. Si cualquiera de
los proveedores exige un recurso de pago, revisar el paso antes de confirmarlo.
