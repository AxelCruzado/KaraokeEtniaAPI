# Caja por turno y reporte administrativo R01

Implementación reconstruida a partir del código conservado en la conversación
porque los archivos de la sesión de octubre no estaban en el entorno restaurado.
La reconstrucción se compiló y comprobó nuevamente antes de subirla. No incluye
las capturas ni los documentos de tesis que faltan en ese entorno.

## Preparación

Requiere .NET 8, MySQL 8 y EF CLI 9.0.4. Desde la raíz del backend:

```sh
dotnet restore
dotnet build
dotnet ef database update --context CajaDbContext
dotnet run --no-launch-profile
```

La conexión se configura en `ConnectionStrings__DefaultConnection`; no guardar
credenciales en el repositorio. El esquema operativo anterior se migra con su
propio contexto `AppDbContext`. Caja utiliza tablas `Caja_*` y un historial de
migración independiente `__CajaMigrationsHistory`. La aplicación no migra una
base de producción automáticamente al arrancar.

El frontend incorpora `/caja` mediante carga diferida y un enlace desde el
dashboard. Admite Administrador y Cajero. El administrador puede crear usuarios
Cajero desde gestión de usuarios. El registro de cuentas exige autenticación
administrativa; ya no permite asignarse privilegios desde una petición anónima.

`Caja__DatosPrueba=true` habilita el distintivo de datos de prueba exclusivamente
en Development. No cambia las reglas contables ni los permisos.

## Reglas recuperadas

- Una apertura por caja/fecha y un turno activo por caja; martes a domingo,
  19:00 incluido a 07:00 del día siguiente excluido. No se crea un turno nuevo
  al pasar medianoche. Las fechas de operación son locales sin offset,
  America/Lima; las marcas de registro y auditoría son UTC del servidor.
- Venta pendiente, venta cobrada y movimiento de cobro están separados. Los
  pagos combinados deben sumar exactamente el total, usando Efectivo, Yape o
  Tarjeta. Una venta pendiente no suma ingresos. No se duplica una venta por
  referencia ni por pedido histórico vinculado.
- El vínculo con un pedido histórico es explícito y valida el total. No se
  importan automáticamente los registros operativos antiguos al nuevo R01.
- Egresos con comprobante, concepto, categoría Insumos/Servicios/Otros y
  naturaleza Gasto/Retiro/Devolución, conservando su medio de pago.
- Fondo inicial excluido de ventas y saldo de movimientos. Efectivo esperado
  = fondo + entradas de efectivo - salidas de efectivo. Yape y tarjeta quedan
  separados del efectivo físico. El conteo físico debe introducirse como dato
  independiente y puede ser cero; la diferencia tiene signo.
- Cierres, reportes y auditoría son de solo adición. Se conserva la primera
  versión. Reabrir requiere Administrador y motivo. Cerrar otra vez genera
  una versión nueva, nunca sobreescribe el conteo ni la diferencia anteriores.
- Correcciones, anulaciones y reversiones conservan motivo, usuario, fecha y
  valores anteriores/posteriores. No existe borrado por API. Los anulados
  permanecen en detalle, fuera de los totales, sin restarse dos veces.
- Claves de idempotencia para movimientos y cierres, referencias únicas,
  bloqueo transaccional por turno y versión esperada para correcciones.
- R01 consolida el turno completo, registra filtros, usuario, hora, identificador
  y versión. Incluye conteo de movimientos, cobros por medio, egresos por
  categoría/naturaleza y saldo ingresos menos egresos, que no es utilidad neta.
  CSV y JSON exportan la misma versión guardada, incluso tras correcciones.
- Consultar registros no sustituye fichas FO-01, documentos fuente independientes
  ni mediciones de tiempo realizadas por un observador. Los comprobantes son
  referencias de texto; no se implementó carga de archivos fiscales adjuntos.

## API

Todas las rutas bajo `/api/caja` requieren JWT y rol Administrador o Cajero.

- GET sesion, turnos, turnos/{id}, movimientos.
- POST turnos: caja, fecha y fondoInicial.
- POST ventas: referencia, total y pedidoHistorialId opcional; queda pendiente.
- POST turnos/{id}/movimientos: claveOperacion, tipo, naturaleza, concepto,
  categoria, documento, importe, fechaOperacion, pagos y ventaId o referenciaVenta.
- POST turnos/{id}/cierres: claveOperacion y contado.
- POST turnos/{id}/reabrir: motivo; solo Administrador.
- POST turnos/{id}/movimientos/{movimientoId}/corregir: versionEsperada, motivo,
  concepto, categoria, documento, importe y pagos; solo Administrador.
- POST turnos/{id}/movimientos/{movimientoId}/anulacion: versionEsperada, motivo
  y anulado; false revierte la anulación, solo Administrador.
- POST turnos/{id}/reportes; GET reportes/{id} y
  reportes/{id}/exportar?formato=csv|json.

Los filtros desdeTurno/hastaTurno usan fecha de apertura e incluyen madrugada.
Los filtros desdeOperacion/hastaOperacion usan hora local y fin excluido. Se
admiten también turnoId, tipo, categoria, medio y usuario. El listado de turnos
muestra los 200 más recientes del rango; restringir fechas para consultar anteriores.

## Pruebas locales reproducibles

```sh
CAJA_TEST_BASE_URL=https://localhost:7251 \
CAJA_TEST_CA=/path/to/localhost.crt \
python tests/caja_integration.py
python tests/caja_restore.py
```

Se pueden configurar CAJA_TEST_ADMIN y CAJA_TEST_PASSWORD sin imprimirlos.
La contraseña predeterminada del script corresponde únicamente al administrador
sembrado por el proyecto en la base local de desarrollo. Nunca usar estos scripts
contra producción: crean casos identificados como TEST y usuarios de prueba.

La suite cubre F01–F10 y F12, más concurrencia e ingresos pendientes. F11 restaura
las siete tablas Caja_* en otra base temporal local y verifica filas, importes y
snapshots antes de eliminar esa base temporal. Requiere el contenedor local
etnia-dev-mysql con etnia_dev; no incluye archivos externos de comprobantes.
No se declara aprobada la suite unitaria Angular heredada, que contiene imports
incorrectos previos y es independiente de estos ensayos de integración.
