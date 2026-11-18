"""A stale receipt must fail even when an administrator changes items without changing total.
Only isolated localhost/etnia_dev; creates identifiable test fixtures.
"""
import os,uuid,subprocess
os.environ.setdefault('CAJA_TEST_CA','/workspace/.setup/localhost.crt')
from caja_integration import req
s=uuid.uuid4().hex[:8];root='/api/gestion'
a=req('/api/autenticacion/login',{'nombreUsuario':os.getenv('CAJA_TEST_ADMIN','admin'),'password':os.getenv('CAJA_TEST_PASSWORD','admin123')})['token']
cmd=['docker','--host=unix:///var/run/docker.sock','exec','-i','etnia-dev-mysql','mysql','-uroot','-N','etnia_dev']
sql=f"INSERT INTO Categorias(Nombre,ImagenUrl,Estado) VALUES('VERSION-{s}','','Activo');SET @cat=LAST_INSERT_ID();INSERT INTO Productos(Nombre,ImagenUrl,Stock,Precio,TicketsPorUnidad,CategoriaId,StockMinimo) VALUES('VERSION-A-{s}','',10,5,0,@cat,0);SELECT LAST_INSERT_ID();INSERT INTO Productos(Nombre,ImagenUrl,Stock,Precio,TicketsPorUnidad,CategoriaId,StockMinimo) VALUES('VERSION-B-{s}','',10,5,0,@cat,0);SELECT LAST_INSERT_ID();"
p1,p2=map(int,subprocess.check_output(cmd,input=sql.encode()).split())
provider=req(root+'/proveedores',{'nombre':'VERSION '+s,'documento':'VERSION-'+s,'contacto':'Prueba'},a)
def inputs(pid,q,c):return {'proveedorId':provider['id'],'documento':'COMPRA-'+s,'lineas':[{'productoId':pid,'cantidad':q,'costoUnitario':c}]}
cid=req(root+'/compras',inputs(p1,3,2),a)['id']
def state():
 d=req(root+'/resumen',token=a)
 return {'compra':next(x for x in d['compras'] if x['id']==cid),'productos':[x for x in d['productos'] if x['id'] in [p1,p2]]}
seen=state()['compra'];assert seen['total']==6
turn=req('/api/caja/turnos',{'caja':'VERSION-'+s,'fecha':'2026-11-17','fondoInicial':100},a)
url=root+f'/compras/{cid}/recibir'
pay={'versionEsperada':seen['version'],'turnoId':turn['id'],'fechaOperacion':'2026-11-18T02:00:00','pagos':[{'medio':'Efectivo','importe':6}]}
req(root+f'/compras/{cid}/corregir',{'datos':inputs(p2,2,3),'motivo':'Cambio de producto manteniendo el total','versionEsperada':seen['version']},a)
updated=state()['compra'];assert updated['total']==seen['total'] and updated['version']>seen['version']
before=state();before_cash=req('/api/caja/turnos/'+turn['id'],token=a)
stock_before=req(root+f'/inventario/historial?productoId={p2}',token=a)
audit_before=req(root+f'/acciones/historial?buscar=COMPRA-{s}',token=a)
error=req(url,pay,a,409);assert 'cambió' in error['message']
assert state()==before and req('/api/caja/turnos/'+turn['id'],token=a)==before_cash
assert req(root+f'/inventario/historial?productoId={p2}',token=a)==stock_before
assert req(root+f'/acciones/historial?buscar=COMPRA-{s}',token=a)==audit_before
print('Versión obsoleta con mismo total: 409; compra, stock, caja e historial intactos.')
for invalid in [None,0,-1]:
 body={k:v for k,v in pay.items() if k!='versionEsperada'}
 if invalid is not None:body['versionEsperada']=invalid
 req(url,body,a,400)
assert state()==before
pay['versionEsperada']=updated['version'];req(url,pay,a)
d=state();assert d['compra']['estado']=='Recibida'
assert next(x for x in d['productos'] if x['id']==p1)['stock']==10
assert next(x for x in d['productos'] if x['id']==p2)['stock']==12
cash=req('/api/caja/turnos/'+turn['id'],token=a);assert cash['resumen']['egresos']==6 and cash['resumen']['cantidadMovimientos']==1
req(url,pay,a,409);assert req('/api/caja/turnos/'+turn['id'],token=a)==cash
print('Versión actual: recepción correcta; omisión/valores inválidos y reintento rechazados.')
