# Compras, inventario, responsabilidades y supervisión

Preparación: aplicar `dotnet ef database update --context AppDbContext` además de
las migraciones independientes de CajaDbContext. La nueva migración incorpora
proveedores, compras y líneas, movimientos de stock, auditoría administrativa,
StockMinimo y Activo (los usuarios existentes se conservan activos).

## Flujos

- `/gestion` en Angular: supervisión de existencias y alertas, historial, entradas,
  salidas y ajustes con motivo; umbrales de reposición; proveedores, compras,
  recepción y egreso; estados/roles de usuarios y acciones administrativas.
- `/caja`: el formulario de cobro permite elegir un pedido histórico aún sin
  contabilizar; rellena referencia e importe y valida ambos en el servidor.
  Los pedidos solo se enlazan una vez. El cajero confirma fecha y componentes
  del cobro: no se genera una segunda entrada automática al cerrar una mesa.
- Las rutas de reportes/caja chica/ganancias/movimientos del dashboard llevan
  a caja/R01. Los endpoints financieros heredados leen Caja_* con el criterio
  fecha de apertura (incluye madrugada), sin fondo ni ventas pendientes.
  Su respuesta es el nuevo consolidado canónico, no el antiguo cálculo de
  utilidad. El registro de gastos antiguo devuelve 410 para evitar dos libros.
- Registrar una compra pendiente NO aumenta stock ni genera egresos. Recibirla
  y pagarla valida total, turno abierto, fecha y medios de pago; registra stock,
  historial, compra recibida y egreso en UNA transacción compartida. Reintentar
  la recepción devuelve conflicto y no duplica cantidades ni importes.
- Recepción completa y pago completo, sin crédito ni recepción parcial en esta
  versión. Los egresos vinculados a recepciones no pueden modificarse/anularse
  aisladamente desde caja: se protege la coherencia con la compra recibida.
- El stock no se cambia silenciosamente desde la edición del catálogo: usar
  un ajuste justificado. Entradas positivas, salidas negativas; nunca negativo.
  El descuento por pedidos también genera historial. Una marca de concurrencia
  evita sobrescribir cambios de stock hechos por otra operación.
- Activo=false bloquea login Y tokens ya emitidos. Cambiar rol invalida el token
  anterior. No se puede desactivar ni cambiar el rol de la propia cuenta desde
  gestión. La eliminación heredada de usuarios ahora es desactivación.
- Administrador: catálogo, ajustes, umbrales, proveedores, compras, recepción,
  permisos y estados. Cajero: lecturas de gestión, caja y operación de mesas.
  Mesera: registro de pedidos; Musica: gestión de canciones. Las autorizaciones
  se aplican en servidor además de la interfaz.

## API /api/gestion

GET resumen; GET usuarios (solo Administrador).
POST proveedores {nombre, documento, contacto}.
POST inventario {productoId, cantidad, motivo, stockEsperado}.
POST inventario/{id}/minimo {cantidad}.
POST compras {proveedorId, documento, lineas:[{productoId,cantidad,costoUnitario}]}.
POST compras/{id}/recibir {turnoId, fechaOperacion, pagos:[{medio,importe}]}.
POST usuarios/{id}/estado {activo, rol, motivo}.

Escrituras requieren Administrador. FechaOperacion usa hora local America/Lima
sin offset y debe pertenecer al turno. El movimiento lleva comprobante de compra
y un identificador vinculado desde la compra; auditoría y stock conservan usuario.

Prueba: `CAJA_TEST_CA=/workspace/.setup/localhost.crt python tests/gestion_integration.py`
contra la base local aislada etnia_dev. Crea fixtures de prueba, nunca producción.
Comprueba permisos, stock/alertas, rollback ante pago incorrecto, recepción única,
vínculo a caja, desactivación/JWT y pedido → salida de stock → cobro sin duplicados.
La suite anterior `tests/caja_integration.py` comprueba regresiones de caja/R01.
Los registros de estas pruebas no son observaciones de tesis ni datos del negocio.

## Corrección, cancelación y reversión

Aplicar la migración VersionCompra. Las compras conservan una versión positiva.

- POST compras/{id}/corregir: {datos: CompraInput, motivo, versionEsperada}.
  Solo Pendiente o Revertida; conserva proveedor/documento/líneas anteriores
  y posteriores en auditoría. Una versión desactualizada devuelve 409.
- POST compras/{id}/cancelar: {motivo, versionEsperada}. Solo pendiente o
  revertida; no borra el documento ni altera existencias o caja.
- POST compras/{id}/revertir: {motivo, versionEsperada}. Solo recibida, con
  todo el stock disponible y su turno original abierto (reapertura autorizada
  si procede). Retira stock y anula el egreso original en la misma transacción.
  Si falla, no cambia ninguno. No altera cierres ni reportes guardados.
- Tras revertir, se puede corregir y volver a recibir el mismo documento.
  Cada versión usa otra clave de recepción; no reutiliza el egreso anulado.
- Flujo administrativo para errores de recepción/registro; no es un módulo de
  devoluciones comerciales, notas de crédito o reembolsos de otro turno.

Toda modificación exige Administrador, motivo y versión esperada. La historia
conserva actor y fecha UTC, motivo y valores completos anteriores/posteriores.

## Historial paginado

`GET inventario/historial`: pagina, tamano, productoId, usuario, desde, hasta,
buscar (motivo). `GET acciones/historial`: pagina, tamano, usuario, accion,
desde, hasta, buscar (detalle). Las fechas son UTC: inicio incluido y fin excluido.
Tamano entre 1 y 200 limita cada página, no el total consultable. La respuesta
incluye items, total, pagina, tamano, totalPaginas. Orden descendente por fecha e
identificador. Los filtros se aplican antes de contar y paginar.
El resumen ya no envía historiales truncados: los clientes deben consultar estos
endpoints. La interfaz ofrece filtros y botones Anterior/Siguiente separados.

## Alcance de compra

Recepción íntegra y pago íntegro de todas las líneas. Sin crédito, cuentas por
pagar, recepción parcial ni devolución parcial. Estas variantes requieren otro
modelo y no se presentan como implementadas.

Pruebas adicionales: `tests/compras_ciclo_integration.py` ejecuta corrección,
cancelación, rechazo por stock/turno/permisos, reversión, nueva recepción,
conservación de primer cierre/R01 y consulta de más de 200 movimientos y 100
acciones. `tests/gestion_integration.py` y `tests/caja_integration.py` comprueban
regresiones de compras, pedidos, usuarios y caja.
