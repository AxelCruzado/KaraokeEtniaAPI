"""Run against the isolated local etnia_dev API, never production.
Exercises purchase corrections/cancellation/reversal and history beyond old limits.
"""
import os,uuid,secrets,subprocess,json
os.environ.setdefault('CAJA_TEST_CA','/workspace/.setup/localhost.crt')
from caja_integration import req
nonce=uuid.uuid4().hex[:8];a=req('/api/autenticacion/login',{'nombreUsuario':'admin','password':os.getenv('CAJA_TEST_PASSWORD','admin123')})['token'];root='/api/gestion'
pw=secrets.token_urlsafe(24);req('/api/autenticacion/registrar',{'nombreUsuario':'ciclo_'+nonce,'password':pw,'rol':'Cajero'},a)
cash=req('/api/autenticacion/login',{'nombreUsuario':'ciclo_'+nonce,'password':pw})['token']
cmd=['docker','--host=unix:///var/run/docker.sock','exec','-i','etnia-dev-mysql','mysql','-uroot','-N','etnia_dev']
pid=int(subprocess.check_output(cmd,input=f"INSERT INTO Categorias(Nombre,ImagenUrl,Estado) VALUES('CICLO-{nonce}','','Activo');INSERT INTO Productos(Nombre,ImagenUrl,Stock,Precio,TicketsPorUnidad,CategoriaId,StockMinimo) VALUES('CICLO-{nonce}','',10,5,0,LAST_INSERT_ID(),2);SELECT LAST_INSERT_ID();".encode()))
provider=req(root+'/proveedores',{'nombre':'CICLO '+nonce,'documento':'CICLO-'+nonce,'contacto':'Prueba'},a)
def state():return req(root+'/resumen',token=a)
def product():return next(x for x in state()['productos'] if x['id']==pid)
def purchase(cid):return next(x for x in state()['compras'] if x['id']==cid)
def inputs(doc,q=3):return {'proveedorId':provider['id'],'documento':doc,'lineas':[{'productoId':pid,'cantidad':q,'costoUnitario':2}]}
def create(label):return req(root+'/compras',inputs(label+'-'+nonce),a)['id']
id=create('ORIGINAL');x=purchase(id);before=product()['stock']
body={'datos':inputs('CORREGIDA-'+nonce,4),'motivo':'Rectificar cantidad del documento fuente','versionEsperada':x['version']}
req(root+f'/compras/{id}/corregir',body,cash,403)
req(root+f'/compras/{id}/corregir',{**body,'motivo':''},a,400)
req(root+f'/compras/{id}/corregir',body,a)
assert product()['stock']==before and purchase(id)['lineas'][0]['cantidad']==4
req(root+f'/compras/{id}/corregir',body,a,409);print('Corrección pendiente, motivo, permisos y versión OK',flush=True)
turn=req('/api/caja/turnos',{'caja':'CICLO-'+nonce,'fecha':'2026-11-17','fondoInicial':100},a);troot='/api/caja/turnos/'+turn['id']
pay={'turnoId':turn['id'],'fechaOperacion':'2026-11-18T02:00:00','pagos':[{'medio':'Efectivo','importe':8}]}
req(root+f'/compras/{id}/recibir',pay,a);x=purchase(id);assert product()['stock']==14
first=req(troot+'/cierres',{'claveOperacion':'close-'+nonce,'contado':92},a)
saved=req(troot,token=a)['cierres'][0]
r=req(troot+'/reportes',{},a);snapshot=r['snapshotJson']
rev={'motivo':'Recepción equivocada; productos y pago íntegros devueltos','versionEsperada':x['version']}
req(root+f'/compras/{id}/revertir',rev,a,409)
assert product()['stock']==14 and purchase(id)['estado']=='Recibida'
req(troot+'/reabrir',{'motivo':'Autorizar rectificación de recepción equivocada'},a)
req(root+'/inventario',{'productoId':pid,'cantidad':-12,'motivo':'Salida de prueba antes de reversión','stockEsperado':14},a)
req(root+f'/compras/{id}/revertir',rev,a,409);assert product()['stock']==2
req(root+'/inventario',{'productoId':pid,'cantidad':12,'motivo':'Restituir disponibilidad para reversión','stockEsperado':2},a)
req(root+f'/compras/{id}/revertir',rev,cash,403)
req(root+f'/compras/{id}/revertir',{**rev,'motivo':''},a,400)
req(root+f'/compras/{id}/revertir',rev,a)
assert product()['stock']==10 and purchase(id)['estado']=='Revertida'
d=req(troot,token=a);assert d['resumen']['egresos']==0 and d['resumen']['efectivoEsperado']==100 and d['cierres'][0]==saved
assert req('/api/caja/reportes/'+r['id'],token=a)['snapshotJson']==snapshot
req(root+f'/compras/{id}/revertir',rev,a,409);assert product()['stock']==10
print('Reversión atómica; stock/turno/permisos; cierre y R01 conservados OK',flush=True)
x=purchase(id);req(root+f'/compras/{id}/corregir',{'datos':inputs('CORREGIDA-'+nonce,2),'motivo':'Recepción corregida','versionEsperada':x['version']},a)
pay['pagos'][0]['importe']=4
req(root+f'/compras/{id}/recibir',pay,a);assert product()['stock']==12
assert req(troot,token=a)['resumen']['egresos']==4
cancel=create('CANCELAR');x=purchase(cancel)
req(root+f'/compras/{cancel}/cancelar',{'motivo':'Pedido cancelado por proveedor','versionEsperada':x['version']},a)
assert purchase(cancel)['estado']=='Cancelada'
req(root+f'/compras/{cancel}/recibir',pay,a,409)
print('Nueva recepción tras corrección sin reutilizar el egreso anulado; cancelación OK',flush=True)
# Generate genuine API stock events; never truncate the history to the old 200/100 limit.
stock=product()['stock'];reason='PAGINACION-'+nonce
for i in range(205):req(root+'/inventario',{'productoId':pid,'cantidad':1,'motivo':reason,'stockEsperado':stock+i},a)
url=root+f'/inventario/historial?productoId={pid}&buscar={reason}&tamano=25'
firstpage=req(url+'&pagina=1',token=a);last=req(url+'&pagina=9',token=a)
assert firstpage['total']==205 and last['total']==205 and len(last['items'])==5
assert not {x['id'] for x in firstpage['items']} & {x['id'] for x in last['items']}
audit=req(root+f'/acciones/historial?buscar={reason}&tamano=25&pagina=9',token=a)
assert audit['total']==205 and len(audit['items'])==5
req(root+'/inventario/historial?pagina=0',token=a,status=400)
req(root+'/acciones/historial?tamano=201',token=a,status=400)
q=root+f'/acciones/historial?accion=Reversi&buscar={id}&usuario=admin&tamano=25'
assert any(x['accion']=='Reversión de compra' for x in req(q,token=a)['items'])
req(root+'/inventario/historial?desde=2026-12-01T00:00:00Z&hasta=2026-01-01T00:00:00Z',token=a,status=400)
assert req(root+f'/inventario/historial?productoId={pid}&desde=2099-01-01T00:00:00Z',token=a)['total']==0
print('Historial >200 movimientos / >100 acciones, páginas, filtros y validación OK',flush=True)
