# Despliegue en una máquina gratuita y evidencia F-01

Estado: configuración preparada. Publicación pendiente de una cuenta y una máquina
asignada al usuario. No hay URL pública ni disponibilidad comercial acreditadas.

## Servicio gratuito propuesto

Una instancia Ubuntu en Oracle Cloud **Always Free**, con CPU y memoria suficientes
para MySQL, .NET y los builds (recomendado al menos 2 CPU y 4 GB). Usar únicamente
recursos cuyo panel indique Always Free y cuotas incluidas; no habilitar una cuenta
Pay As You Go ni crear recursos cobrables. La capacidad depende de región y alta;
el formulario puede pedir tarjeta de verificación. No equivale a disponibilidad
permanente garantizada. También se puede ejecutar esta configuración en otra VM
gratuita que ya posea el usuario.

No recomendamos actualmente Aiven MySQL free: su documentación oficial indica
que no admite nuevas altas gratuitas desde el 6/11/2026 y termina el 21/12/2026.
Fuente: https://github.com/aiven/aiven-docs/blob/d1dfe68f566f88c5fe20637c26781486ce5caf37/docs/products/mysql/howto/mysql-free-tier-migration-options.md

La app conserva MySQL. Frontend, API e imágenes comparten una dirección HTTPS;
Caddy renueva el certificado; MySQL y archivos usan volúmenes persistentes. No hay
puertos públicos de la base ni de la API interna. Se necesita dirección pública,
un hostname que resuelva a ella y entrada TCP 80/443 para el certificado.

## 1. Cuenta y máquina (pasos del titular)

1. Crear/iniciar sesión en https://www.oracle.com/cloud/free/ y revisar las cuotas
   gratuitas disponibles en ese momento. Elegir Ubuntu y recursos Always Free.
2. Conservar la clave SSH privada en su equipo. No pegar claves ni contraseñas en
   el chat. Anotar IP pública y región; confirmar que no hay recursos cobrables.
3. En reglas de ingreso de la red permitir TCP 80/443. Mantener SSH 22 limitado a
   la IP del responsable. Mantener MySQL 3306 sin exposición pública.
4. Tener un nombre DNS que apunte a la IP. Puede utilizar un subdominio gratuito
   de un proveedor que controle, o evaluar `<IP>.sslip.io` si resuelve correctamente.
   El certificado depende de DNS, puertos y límites de la autoridad certificadora;
   no se compra un dominio para esta opción.

## 2. Preparar servidor

Instalar Docker Engine con Compose v2 siguiendo https://docs.docker.com/engine/install/ubuntu/
Instalar git y python3. Usar la cuenta administradora del servidor para configurar
Docker, sin compartir sus credenciales.

Clonar ambos repositorios en un directorio común; sustituir `work` por el commit
aprobado si se desea fijar una entrega. Para repositorios privados autorizar GitHub
en el servidor con el mecanismo del titular; no incrustar tokens en las URLs.

```sh
mkdir -p ~/etnia
cd ~/etnia
git clone --branch work https://github.com/AxelCruzado/KaraokeEtniaAPI.git
git clone --branch work https://github.com/AxelCruzado/karaoke-etnia-frontend.git
cd KaraokeEtniaAPI/deploy
python3 prepare.py --host SU_HOST_REAL --email SU_CORREO_REAL
```

`prepare.py` exige repositorios limpios y registra sus commits. Genera `.env` con
permisos 600, contraseñas nuevas y una clave JWT privada. El responsable consulta
ADMIN_PASSWORD directamente en el servidor para iniciar sesión como admin. No
usar ni conservar la contraseña de desarrollo admin123 en el despliegue.

## Comprobar la máquina antes de publicar

Desde la carpeta deploy de la VM:

```sh
python3 check_server.py --json
```

El informe es de solo lectura: no instala software ni lee contraseñas. Indica
sistema, arquitectura, RAM, disco, Docker/Compose y ocupación local de 80/443.
Se puede compartir su salida con el asistente; no sustituye comprobar las reglas
de red de Oracle ni la disponibilidad del plan gratuito. Ejecutarlo en el entorno
Cloud de desarrollo no dice nada sobre la máquina del establecimiento.

## 3. Publicar

```sh
docker compose --env-file .env -f compose.yml up -d --build
docker compose --env-file .env -f compose.yml ps
```

La API aplica los dos juegos de migraciones sobre una base nueva y sustituye la
cuenta histórica predeterminada antes de aceptar solicitudes. No se importa
etnia_dev, ni TEST/CICLO/UI/VERSION, ni movimientos de ensayo. Registrar usuarios,
catálogo y saldos iniciales reales con autorización del responsable del negocio.
La copia local de los proyectos no se publica por el solo hecho de ejecutar estos
comandos en el entorno de desarrollo: deben ejecutarse en la VM asignada.

Abrir https://SU_HOST_REAL/login. Verificar que los certificados son válidos, que
`/api/version` y `/version.json` identifican las revisiones esperadas y que
`/health/ready` devuelve 200. Las rutas Angular deben soportar recarga directa.
El runtime pasa de .NET 8 a .NET 10 para el despliegue; validar la regresión de esta
nueva entrega, sin atribuirle retroactivamente los resultados del 18/11/2026.

## 4. Recoger F-01 sin operaciones comerciales

```sh
python3 collect_f01.py --url https://SU_HOST_REAL \
  --backend-ref COMMIT_BACKEND --frontend-ref COMMIT_FRONTEND \
  --responsable 'NOMBRE DEL RESPONSABLE' --cargo 'CARGO' --out f01/registro-001
```

El recolector verifica HTTPS, guarda las respuestas públicas y sus huellas y
contrasta revisiones. El responsable revisa y firma F-01 del kit 1.1; la identidad
indicada es declarada, no autenticada por el script. Añadir una captura del panel
de despliegue con fecha, servidor/servicio y revisiones, ocultando datos privados.
No usar fecha o nombre supuesto, ni declarar F-01 completa antes de hacerlo.

## 5. Persistencia, respaldo y actualización

Los volúmenes conservan base, imágenes y certificados al recrear contenedores.
No ejecutar `docker compose down -v`: elimina esos datos. La persistencia local
no sustituye un respaldo fuera de la máquina.

```sh
python3 backup.py
```

Guardar copia privada externa del SQL y de las imágenes del volumen
`etnia_uploaded_images` con las herramientas del responsable. El SQL contiene
hashes de cuentas: no adjuntarlo públicamente a la tesis. Este comando crea un
respaldo; no afirma haber comprobado su restauración.

Para actualizar: respaldar, comprobar que no hay operaciones en curso, avanzar
ambos repositorios al commit elegido, ejecutar `python3 update_versions.py` y
recrear con `up -d --build`. Este script conserva las credenciales existentes.
Volver a recoger F-01 y ejecutar las pruebas de regresión en una base separada,
no en la base real ni durante la atención.

## Alcance

Esta configuración no acredita uso real, conteos físicos, aceptación administrativa
ni juicio del experto. Las interfaces de administración requieren autenticación.
La consulta pública de mesa y solicitudes musicales mantiene su diseño heredado;
no se declara una auditoría integral de seguridad ni aislamiento entre mesas.

## Validación de esta preparación

Se comprobaron compilación .NET 10, compilación Angular de producción, instalación
Production local sobre una base vacía, ambos historiales de migraciones,
credencial inicial privada, rechazo del acceso predeterminado y autenticación de
rutas administrativas. Las cuatro suites de integración y el recorrido Angular
compilado con proxy HTTP local completaron sus comprobaciones.

No se construyó ni arrancó el conjunto completo de contenedores en este entorno:
la política de red bloqueó el CDN de descarga de las imágenes Microsoft. La sintaxis
Compose se validó; el certificado público, red de Oracle, volúmenes y WebSocket
tras Caddy deben comprobarse en la máquina real. Los ensayos locales no se
presentan como F-01 aplicada en una dirección pública.
