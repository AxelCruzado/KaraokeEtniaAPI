"""Integration checks for inventory/purchase/cash atomicity and user lifecycle.
Uses etnia_dev on the managed local Docker daemon. Never production.
"""
import os,uuid,secrets,subprocess
os.environ.setdefault('CAJA_TEST_CA','/workspace/.setup/localhost.crt')
from caja_integration import req
s=uuid.uuid4().hex[:8]
a=req('/api/autenticacion/login',{'nombreUsuario':'admin','password':os.getenv('CAJA_TEST_PASSWORD','admin123')})['token']
pw=secrets.token_urlsafe(20);name='gestion_'+s
req('/api/autenticacion/registrar',{'nombreUsuario':name,'password':pw,'rol':'Cajero'},a)
c=req('/api/autenticacion/login',{'nombreUsuario':name,'password':pw})['token']
sql=f"INSERT INTO Categorias(Nombre,ImagenUrl,Estado) VALUES('TEST-{s}','','Activo'); INSERT INTO Productos(Nombre,ImagenUrl,Stock,Precio,TicketsPorUnidad,CategoriaId,StockMinimo) VALUES('TEST-{s}','',10,5,0,LAST_INSERT_ID(),2); SELECT LAST_INSERT_ID();"
pid=int(subprocess.check_output(['docker','--host=unix:///var/run/docker.sock','exec','-i','etnia-dev-mysql','mysql','-uroot','-N','etnia_dev'],input=sql.encode()))
root='/api/gestion'
def data():
 d=req(root+'/resumen',token=a);d['movimientos']=req(root+'/inventario/historial?tamano=200',token=a)['items'];d['acciones']=req(root+'/acciones/historial?tamano=200',token=a)['items'];return d
def product():return next(p for p in data()['productos'] if p['id']==pid)
req(root+'/inventario',{'productoId':pid,'cantidad':-8,'motivo':'Salida justificada de prueba','stockEsperado':10},c,403)
req(root+'/inventario',{'productoId':pid,'cantidad':-8,'motivo':'Salida justificada de prueba','stockEsperado':10},a)
assert product()['stock']==2 and product()['reponer'];print('Inventario, permiso y alerta OK')
req(root+'/inventario',{'productoId':pid,'cantidad':-3,'motivo':'Sin stock','stockEsperado':2},a,400)
req(root+'/inventario',{'productoId':pid,'cantidad':1,'motivo':'','stockEsperado':2},a,400)
provider=req(root+'/proveedores',{'nombre':'TEST '+s,'documento':'TEST-'+s,'contacto':'demo'},a)
purchase=req(root+'/compras',{'proveedorId':provider['id'],'documento':'COMPRA-'+s,'lineas':[{'productoId':pid,'cantidad':3,'costoUnitario':2.5}]},a)
t=req('/api/caja/turnos',{'caja':'GESTION-'+s,'fecha':'2026-11-17','fondoInicial':100},a)
payload={'versionEsperada':next(x for x in data()['compras'] if x['id']==purchase['id'])['version'],'turnoId':t['id'],'fechaOperacion':'2026-11-18T02:00:00','pagos':[{'medio':'Efectivo','importe':7.5}]}
req(root+f"/compras/{purchase['id']}/recibir",{**payload,'pagos':[{'medio':'Efectivo','importe':7}]},a,400)
assert product()['stock']==2;assert next(x for x in data()['compras'] if x['id']==purchase['id'])['estado']=='Pendiente';print('Recepción inválida revierte stock y compra OK')
req(root+f"/compras/{purchase['id']}/recibir",payload,a)
assert product()['stock']==5
cash=req('/api/caja/turnos/'+t['id'],token=a)['resumen'];assert cash['egresos']==7.5 and cash['efectivoEsperado']==92.5
req(root+f"/compras/{purchase['id']}/recibir",payload,a,409);assert product()['stock']==5
assert any(m['productoId']==pid and m['anterior']==2 and m['posterior']==5 for m in data()['movimientos']);print('Compra, recepción única, historial y caja coherentes OK')
r=req('/api/reportes/diario?year=2026&month=11&day=17',token=a);assert r['fuente']=='Libro de caja por turno / R01'
user=next(u for u in req(root+'/usuarios',token=a) if u['nombreUsuario']==name)
req(root+f"/usuarios/{user['id']}/estado",{'activo':False,'rol':'Cajero','motivo':'Fin de prueba'},a)
req('/api/caja/sesion',token=c,status=401)
req('/api/autenticacion/login',{'nombreUsuario':name,'password':pw},status=401)
assert any(x['accion']=='Permisos/estado de usuario' for x in data()['acciones']);print('Desactivación, invalidación JWT y auditoría OK')
print('GESTION: verificaciones aprobadas')
# Complete an actual order, inspect its stock event, archive it and link its one collection.
mesa=req('/api/mesas',{'numero':int(s,16)%1000000+1000000,'estado':'Disponible','pedidosActivos':[]},a)
mid=mesa['mesaId']
order=req(f'/api/mesas/{mid}/pedidos',{'detalles':[{'productoId':pid,'cantidad':2}],'metodoPago':'Efectivo','pagoCliente':10},a)
assert product()['stock']==3
req(f'/api/mesas/{mid}/liberar',{},a)
pending=req('/api/caja/pedidos-pendientes',token=a)
linked=next(p for p in pending if p['mesaId']==mid)
body={'claveOperacion':'pedido-'+s,'tipo':'Ingreso','naturaleza':'Venta cobrada','concepto':'Pedido real de prueba','categoria':'Ventas','documento':'PED-'+s,'importe':linked['total'],'fechaOperacion':'2026-11-18T03:00:00','pagos':[{'medio':'Efectivo','importe':linked['total']}],'referenciaVenta':'PED-'+s,'pedidoHistorialId':linked['id']}
req('/api/caja/turnos/'+t['id']+'/movimientos',body,a)
req('/api/caja/turnos/'+t['id']+'/movimientos',{**body,'claveOperacion':'dup-'+s},a,409)
assert all(p['id']!=linked['id'] for p in req('/api/caja/pedidos-pendientes',token=a))
assert any(m['productoId']==pid and m['anterior']==5 and m['posterior']==3 for m in data()['movimientos'])
print('Pedido, salida de stock, historial, cobro vinculado y no duplicación OK')
