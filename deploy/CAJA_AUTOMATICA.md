# Turnos automáticos, cobro único y conformidad

La mesera confirma el pedido y su cobro una sola vez. El pedido, las existencias, la venta, el movimiento y sus pagos se guardan en una misma transacción de PostgreSQL. El historial de la mesa conserva la relación al liberarla; no vuelve a aparecer como cobro pendiente. El importe entregado de más en efectivo es vuelto y no incrementa un saldo nuevo; Yape, Plin y tarjeta deben coincidir con el importe por cobrar. Los saldos anteriores, si existen, se consumen como crédito sin volver a ingresarlos a caja.

## Horario y recuperación

Hora de referencia: America/Lima. El turno abre a las 19:00 con S/0 y termina a las 07:00 del día siguiente. Se conservó el calendario anterior de martes a domingo (días .NET 2,3,4,5,6,0). `Caja:Dias` permite configurar otros días y `Caja:Nombre` identifica la caja automática (por defecto CAJA-01). No hay una acción de apertura manual para el administrador.

Un proceso del servidor verifica los límites del horario; las solicitudes también reconcilian los periodos antes de operar. La fila singleton de calendario serializa solicitudes concurrentes y réplicas. Si el alojamiento de Render gratuito está suspendido o no hay conectividad, no puede ejecutar código exactamente a la hora del reloj: al reactivarse recupera los periodos con sus límites lógicos correctos, antes de aceptar pedidos. No depende de que el administrador entre ni de refrescos periódicos del navegador.

El fin del horario conserva el turno como Cerrado. No inventa dinero contado. Si no hay movimientos financieros y el fondo es cero, queda Conforme automáticamente: sin movimientos, con auditoría y sin un arqueo físico ficticio. Si hay movimientos o fondo, queda Pendiente de conformidad.

## Fondo declarado después de abrir

Administrador: Ventas y caja → Fondo inicial. Puede declarar el monto durante el horario ordinario, con motivo. Se guardan monto anterior, monto nuevo, usuario, fecha/hora UTC mostrada en Lima, versión y demora desde las 19:00. No es una venta ni un ingreso; ajusta el efectivo esperado. Las aperturas manuales históricas conservan su monto y la fecha/responsable de su registro original.

## Atención fuera de horario

Administrador: Ventas y caja → Historial de caja → Atención fuera de horario. Debe elegir el turno finalizado esta mañana, indicar motivo y hora límite, anterior o igual a las 19:00 siguientes. Solo un administrador puede autorizar o ampliar esta excepción. Mesera continúa usando Nuevo pedido; no hace un ingreso manual ni cambia el cierre.

Cada venta conserva el mismo turno, su hora real, responsable, relación con el pedido y una indicación Fuera de horario en caja y auditoría. La autorización y su caducidad se auditan. Los reportes incluyen esas ventas e identifican el periodo excepcional. Al vencer el permiso no se admiten nuevos pedidos. Se puede autorizar de nuevo antes del siguiente turno, con versión y motivo; las autorizaciones anteriores permanecen en la auditoría.

## Visto bueno y diferencias

Administrador: Ventas y caja → Cierre y arqueo. Debe ingresar el efectivo contado y los cobros verificados de Yape, Plin y tarjeta (incluyendo cero), confirmar que revisó los movimientos y anotar observaciones obligatorias si hay diferencias. Los valores digitales se contrastan con los cobros del turno, no con el saldo total de una cuenta bancaria. También debe revisar los egresos documentados.

Ejemplo: esperado S/240, contado S/200 → diferencia -S/40 y estado Con diferencias. No se reduce el ingreso a S/200 ni se borran ventas. Quedan ambos valores y las observaciones. Una revisión sin diferencias queda Conforme; con observaciones puede quedar Conforme con observaciones.

Si después de una conformidad se registra una venta autorizada, vuelve a Pendiente de nueva conformidad. El nuevo conteo exige motivo y conserva todas las versiones anteriores. Ninguna revisión sustituye a una firma o a un conteo realmente realizado por el responsable.

## Pantallas y permisos

Cobros es de consulta y se actualiza por SignalR/WebSocket al ocurrir operaciones. Se retiraron apertura e ingreso manual del flujo automático; la API tampoco permite duplicarlos. Egresos, fondo, autorización de excepciones y conformidad son acciones del administrador. Mesera no puede autorizar horarios, declarar fondos ni aprobar cierres. El rol Cajero conserva lectura, sin esos permisos de administración en el modo automático.

## Verificación

- `tests/caja_automatica_integration.py`: reloj privado de desarrollo y PostgreSQL aislado. Límites de horario, ocho solicitudes concurrentes, cobro y vuelto, medios digitales, fondo tardío, permisos, fallo real de escritura de caja con reversión total, liberación sin duplicar, conformidad, excepciones, faltantes, día sin actividad y recuperación tras inactividad.
- `tests/caja_automatica_ui.py`: cuentas separadas en navegadores reales, evento de cobro sin liberar mesa, ausencia de consultas periódicas, fondo con demora, egreso, conformidad y revisión posterior con observaciones.
- `tests/carta_integration.py`: regresión de combos, recetas, tickets, hielo, stock y concurrencia con cobro automático activo.

El reloj de ensayo solo se lee en Development mediante un archivo privado configurado por el entorno. No hay un endpoint público para cambiar la hora. Las pruebas y capturas locales son de demostración; no evidencian operaciones físicas del establecimiento.
