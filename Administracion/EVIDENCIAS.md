# Verificación técnica y evidencias administrativas

Este procedimiento conserva pruebas locales del software. No acredita uso real,
compras reales, conteos físicos ni validación por expertos. No calcula mejoras
antes/después. Se usa etnia_dev en Docker local y la API en localhost; nunca
producción. Los datos y documentos TEST/CICLO/UI/VERSION son de ensayo.

## Ejecución

Con API, MySQL y frontend activos, certificado localhost confiado y las
migraciones existentes aplicadas, elegir un EVIDENCIA_DIR nuevo:

```sh
export EVIDENCIA_DIR=/workspace/evidencias/nueva-ejecucion
export CAJA_TEST_CA=/workspace/.setup/localhost.crt
python tests/evidencia_administrativa.py
```

El recolector ejecuta las cuatro suites existentes y conserva fecha Lima,
versiones Git de ambos repositorios, evaluador automatizado, pasos HTTP,
entradas, resultado esperado y obtenido, respuestas sin contraseñas/tokens,
logs y exportaciones R01. Cada script archivado contiene las aserciones de
integridad (stock, caja, historial, cierres y reportes) además de los estados HTTP.
Los archivos deben revisarse antes de compartirlos fuera del equipo.

Desde el frontend, con Playwright/Chromium y TLS confiado:

```sh
GESTION_UI_OUTPUT="$EVIDENCIA_DIR/ui" python tests/gestion_ui.py
python tests/evidencia_capturas.py
```

El segundo script requiere ejecucion.jsonl de la primera etapa; conserva vistas
de los registros guardados, sin construir imágenes artificiales. El recorrido UI
registra recepción y posterior reversión/cancelación de una compra de ensayo;
las capturas corresponden a esos momentos distintos.

Concluir todas las escrituras antes del respaldo:

```sh
python tests/respaldo_aislado.py
```

Respalda únicamente etnia_dev; crea una base nueva evidencia_restore_<id>;
restaura y compara las tablas, las cantidades y SHA256 de los INSERT ordenados.
No modifica la configuración de la API ni restaura sobre la base origen. El SQL
queda privado en .setup/respaldos-evidencia (puede incluir hashes de cuentas).
Compartir restauracion.json, no el respaldo bruto. Mantiene la copia restaurada
para revisión. Esto comprueba recuperación de datos, no un ensayo de recuperación
completa del servicio ni de su infraestructura.

## Pendientes humanos

Registrar evaluador humano y fecha de revisión; contrastar compras con sus
comprobantes, existencias con conteo físico y arqueo con efectivo realmente
contado. Los importes ingresados por los scripts no proceden de conteo físico.
Conservar formularios firmados, diferencias y decisiones del responsable.
El experto debe revisar la versión reformulada de los instrumentos; no reutilizar
una firma previa como validación de otra versión. Confirmar URL y versión del
sistema desplegado y disponibilidad de un entorno de ensayo del despliegue.
El código actual apunta a localhost y no demuestra qué versión está publicada.
