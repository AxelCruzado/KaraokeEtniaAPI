# Carta e inventario de Etnia

La carta confirmada se configura con `POST /api/carta/configurar` (Administrador) y `carta-etnia.json`. Conserva existencias existentes; crea productos nuevos con cero. El catálogo tiene 59 registros: 47 vendibles y 12 insumos de barra. Hay 43 saldos físicos; los combos y preparaciones calculan disponibilidad por componentes.

## Operación

- Mesera: abrir mesa, agregar pedido, elegir marcas/cantidades, piqueo, sabores o licor base y confirmar la cotización del servidor. Los recargos no aumentan tickets fijos.
- Inventario: botellas y bebidas por unidades, piqueos por porciones y licor abierto por ml. «Abrir botella para barra» resta unidades cerradas y suma ml de la misma familia, sin crear gasto.
- Compras: registrar los insumos físicos. Para hielo, elegir bolsa completa convierte cada bolsa en cuatro porciones manteniendo el importe total (S/8 bolsa = S/2 por cuarto). Recepción y pago mantienen las validaciones de turno, versión y caja.
- Hielo: una porción inicial automática por botella de licor o combo de S/120/S/140; la botella interna del combo no lo duplica. «Registrar hielo» registra cada extra manualmente: gratis para la misma atención habilitada, S/3 en otro caso. Cada porción consume ¼ bolsa y genera cero tickets. Baldes y promo10 no habilitan cortesía. Liberar mesa termina el derecho de la atención.
- El adicional de S/15 incorpora una gaseosa de 1.5 L y requiere una atención habilitada. No incluye otra botella de licor ni repite el hielo inicial ya descontado; entregas extra se registran como hielo.
- Pedidos y aperturas tienen clave idempotente: reintentar los mismos datos no duplica consumo. Una clave confirmada no puede reutilizarse con otro contenido.

## Saldos de demostración

`carta-demostracion.json` procede de `Carta_Etnia_y_preparacion_inventario2.xlsx` (SHA256 dentro del manifiesto). La hoja LEEME identifica cantidades/costos ficticios para feria. El usuario autorizó cargarlos como demostración. Sus fechas y responsables son referencias ficticias, no comprobaciones físicas ni firmas. Se añadieron cuatro bolsas ficticias de hielo (16 porciones); únicamente el costo S/8 por bolsa procede de la regla confirmada del negocio.

`POST /api/carta/demostracion` es solo para Administrador. Carga atómica de 43 saldos y 14 aperturas de referencia; no crea comprobantes, compras, ventas, turnos ni egresos. Rechaza reemplazar productos con movimientos previos. Repetir el mismo lote devuelve su resultado original sin restablecer existencias posteriores. Costos son referencias guardadas en el historial, no evidencia contable. No ejecutar un borrado global para retirar estos datos: revisar primero consumos posteriores y respaldar.

## Verificación local

`tests/carta_integration.py`: opciones, recargos, tickets, ml, hielo, permisos, reintentos y competencia por última unidad.

`tests/carta_demo_integration.py`: ejecutar en una base local recién migrada y configurada para probar reversión completa, correspondencia del Excel, no duplicación, ausencia de compras ficticias y protección de saldos posteriores.

Frontend: `tests/carta_ui.py` y `tests/dashboard_unified_ui.py`; navegador real, API PostgreSQL aislada y SignalR/WebSocket. No usar datos de ensayos como evidencia de uso real, conteos físicos o aceptación firmada.
