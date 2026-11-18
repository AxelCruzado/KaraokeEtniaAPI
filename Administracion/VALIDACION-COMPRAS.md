# Validación ejecutada — 18 de noviembre de 2026

Entorno aislado de desarrollo: MySQL local etnia_dev, API .NET y frontend Angular
arrancados. Datos identificados como CICLO/TEST/UI; no atención real ni mediciones
de la tesis. TLS verificado con certificado localhost confiado, sin ignorar errores.

- Compilación .NET: aprobada (advertencias previas de nulabilidad/paquetes).
- Compilación Angular producción: aprobada (advertencias previas de presupuesto).
- tests/compras_ciclo_integration.py: aprobado. Corrección con motivo y versión;
  cancelación; permisos; reversión rechazada con turno cerrado o stock insuficiente;
  reversión atómica; primer cierre y R01 conservados; corrección y nueva recepción
  sin duplicar el egreso; filtros, rangos inválidos y paginación hasta la página 9.
  Se generaron 205 movimientos reales de prueba por la API para verificar que
  ambos historiales recuperen datos más antiguos que los límites anteriores.
- tests/gestion_integration.py: aprobado. Compras, rollback, stock, cobros de pedidos,
  no duplicación, desactivación y revocación de tokens.
- tests/caja_integration.py: 13 comprobaciones de regresión aprobadas.
- Frontend tests/gestion_ui.py: aprobado en Chromium. Crear, corregir, recibir,
  revertir, cancelar y navegar el historial. Cinco respuestas POST 200 y otras
  filas en la segunda página; sin errores JavaScript durante ese recorrido.

## Reproducción

1. Migrar AppDbContext (incluida VersionCompra) y CajaDbContext, e iniciar API.
2. Ejecutar con CAJA_TEST_CA apuntando al certificado local:
   `python tests/compras_ciclo_integration.py`
   `python tests/gestion_integration.py`
   `python tests/caja_integration.py`
3. Iniciar frontend, confiar su certificado local y, desde el repositorio frontend,
   ejecutar `python tests/gestion_ui.py` con Playwright y Chromium instalados.
   El test requiere los fixtures del primer paso de pruebas. Bloquea llamadas API
   a destinos distintos de localhost/127.0.0.1 para evitar cambios en producción.

La suite unitaria Angular heredada no se declara aprobada; sus imports incorrectos
anteriores son independientes de estos recorridos ejecutados. Tampoco se afirma
una auditoría exhaustiva ni una prueba de carga de producción.
