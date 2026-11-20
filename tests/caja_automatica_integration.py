"""Local PostgreSQL with private advancing clock. Never production or a live database."""
import json,uuid,urllib.request,urllib.error,urllib.parse,subprocess,os
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
BASE=os.getenv('AUTO_TEST_URL','http://127.0.0.1:5113');assert urllib.parse.urlparse(BASE).hostname in ('127.0.0.1','localhost')
CLOCK=Path(os.getenv('AUTO_TEST_CLOCK','/workspace/validacion-caja-automatica/reloj.txt'));checks=[];http=urllib.request.build_opener(urllib.request.ProxyHandler({}))
def req(path,data=None,token=None,status=200,method=None,form=None):
 headers={'Authorization':'Bearer '+token}if token else{}
 if form is not None:raw=urllib.parse.urlencode(form).encode();headers['Content-Type']='application/x-www-form-urlencoded'
 else:raw=json.dumps(data).encode()if data is not None else None;headers['Content-Type']='application/json'
 try:
  with http.open(urllib.request.Request(BASE+path,data=raw,headers=headers,method=method),timeout=40)as r:code,b=r.status,r.read()
 except urllib.error.HTTPError as e:code,b=e.code,e.read()
 assert (code in (200,201)if status==200 else code==status),(path,code,status,b[:400]);return json.loads(b)if b else None
def ok(message):checks.append(message);print('OK',message,flush=True)
def clock(utc):CLOCK.write_text(utc)
clock('2026-11-20T23:59:00Z')
a=req('/api/autenticacion/login',{'nombreUsuario':'admin','password':'admin123'})['token'];tag=uuid.uuid4().hex[:8]
for role in ['Mesera','Cajero']:
 name=role.lower()+tag;pw=uuid.uuid4().hex;req('/api/autenticacion/registrar',{'nombreUsuario':name,'password':pw,'rol':role},a)
 token=req('/api/autenticacion/login',{'nombreUsuario':name,'password':pw})['token']
 if role=='Mesera':m=token
 else:c=token
cat=req('/api/categorias',token=a,form={'Nombre':'AUTO-'+tag,'Estado':'true'})
p=req('/api/productos',token=a,form={'Nombre':'Producto-AUTO-'+tag,'Stock':'100','Precio':'12','TicketsPorUnidad':'1','CategoriaId':cat['id']})
mid=req('/api/mesas',{'numero':int(tag,16)%1000000+200,'estado':'Disponible','pedidosActivos':[]},a)['mesaId']
def stock():return req('/api/productos/'+str(p['id']),token=a)['stock']
def body(amount=12,medio='Efectivo',key=None):return {'claveOperacion':key or uuid.uuid4().hex,'pagoCliente':amount,'metodoPago':medio,'detalles':[{'productoId':p['id'],'cantidad':1}]}
def order(data=None,token=m,status=200):return req(f'/api/mesas/{mid}/pedidos',data or body(),token,status)
assert not req('/api/caja/turnos',token=a);order(status=409);assert stock()==100;ok('Antes de 19:00 no abre turno ni cobra/consume stock')
clock('2026-11-21T00:00:00Z')
with ThreadPoolExecutor(max_workers=8)as pool:list(pool.map(lambda _:req('/api/caja/turnos',token=a),range(8)))
turns=req('/api/caja/turnos',token=a);assert len(turns)==1;t=turns[0];tid=t['id'];root='/api/caja/turnos/'+tid
assert t['automatico']and t['fondoInicial']==0 and t['fondoDeclaradoUtc']is None and t['inicio'].startswith('2026-11-20T19:00')
def detail():return req(root,token=a)
def summary():return detail()['resumen']
ok('19:00 Lima abre un solo turno con S/0 aun con ocho solicitudes concurrentes')
data=body(20);r=order(data);assert r['cobrado']==12 and r['vuelto']==8 and r['turnoId']==tid
assert summary()['ingresos']==12 and summary()['efectivoEsperado']==12 and stock()==99
assert req('/api/mesas/'+str(mid),token=m)['saldo']==0
assert r==order(data)and summary()['cantidadMovimientos']==1;ok('Pedido y cobro son inmediatos, conservan vuelto y reintento sin duplicación')
order(body(12,'Yape'));order(body(12,'Plin'));order(body(12,'Tarjeta'));s=summary();assert s['cobrosPorMedio']==dict(efectivo=12,yape=12,plin=12,tarjeta=12)
order(body(13,'Yape'),status=400);assert summary()['ingresos']==48;ok('Yape, Plin y tarjeta se separan; no acepta exceso digital')
req('/api/caja/turnos',{'caja':'CAJA-01','fecha':'2026-11-20','fondoInicial':100},a,409)
manual={'claveOperacion':uuid.uuid4().hex,'tipo':'Ingreso','naturaleza':'Venta cobrada','concepto':'Duplicado','categoria':'Ventas','documento':'LOCAL','importe':12,'fechaOperacion':'2026-11-20T19:00:00','pagos':[{'medio':'Efectivo','importe':12}],'referenciaVenta':'DUP'}
req(root+'/movimientos',manual,a,409)
req(root+'/fondo',{'monto':100,'versionEsperada':0,'motivo':'Conteo real de ensayo'},m,403)
clock('2026-11-21T01:30:00Z');fund=req(root+'/fondo',{'monto':100,'versionEsperada':0,'motivo':'Administrador declaró el fondo después de iniciar'},a)
assert fund['fondoInicial']==100 and fund['versionFondo']==1 and fund['fondoDeclaradoPor']=='admin';assert fund['fondoDeclaradoUtc'].startswith('2026-11-21T01:30')
assert summary()['efectivoEsperado']==112
req(root+'/fondo',{'monto':200,'versionEsperada':0,'motivo':'Obsoleto'},a,409)
audit=next(x for x in detail()['historial']if x['accion']=='Declaración de fondo inicial');assert json.loads(audit['posteriorJson'])['horasDesdeApertura']==1.5
ok('Fondo tardío conserva monto, usuario, fecha, demora de 1.5 h y versión; permisos y duplicados protegidos')
expense={**manual,'tipo':'Egreso','naturaleza':'Gasto','categoria':'Otros','concepto':'Gasto ensayo','importe':5,'pagos':[{'medio':'Efectivo','importe':5}],'fechaOperacion':'2026-11-20T20:30:00'}
req(root+'/movimientos',expense,c,403);req(root+'/movimientos',expense,a);assert summary()['efectivoEsperado']==107
ok('Solo administrador registra egresos; el fondo no se convierte en venta')
# Inject a real DB failure in the financial write after the inventory/order save.
DB=os.getenv('AUTO_TEST_DATABASE','etnia_carta_auto_test');assert DB.startswith('etnia_carta_')
def sql(text):return subprocess.check_output(['docker','--host=unix:///var/run/docker.sock','exec','-i','etnia-dev-postgres','psql','-U','etnia','-d',DB,'-At','-v','ON_ERROR_STOP=1'],input=text.encode()).decode()
before=stock();before_cash=summary()['ingresos']
sql('''CREATE FUNCTION fail_auto_test() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'isolated ledger failure'; END; $$; CREATE TRIGGER fail_auto_test BEFORE INSERT ON "Caja_MovimientoCaja" FOR EACH ROW EXECUTE FUNCTION fail_auto_test();''')
try:order(status=409)
finally:sql('DROP TRIGGER fail_auto_test ON "Caja_MovimientoCaja"; DROP FUNCTION fail_auto_test();')
assert stock()==before and summary()['ingresos']==before_cash;ok('Fallo real de caja revierte pedido, venta y stock en una sola transacción')
# Advance midnight and release: automatically paid histories never become manual pending cobros.
clock('2026-11-21T06:00:00Z');assert req('/api/carta/turno',token=m)['turno']['id']==tid
req(f'/api/mesas/{mid}/liberar',{},m);assert not req('/api/caja/pedidos-pendientes',token=a);assert summary()['ingresos']==48
ok('Madrugada conserva turno; liberar mesa no duplica ingresos ni deja cobros manuales pendientes')
clock('2026-11-21T12:00:00Z');d=detail();assert d['resumen']['turno']['estado']=='Cerrado'and not d['cierres']and d['resumen']['conformidad']=='Pendiente de conformidad'
assert d['resumen']['turno']['finalizadoAutomaticamenteUtc'].startswith('2026-11-21T12:00');order(status=409)
req(root+'/fondo',{'monto':200,'versionEsperada':1,'motivo':'Tarde'},a,409)
ok('07:00 finaliza automáticamente sin inventar arqueo ni visto bueno; no registra pedidos fuera de horario sin autorización')
confirm={'claveOperacion':uuid.uuid4().hex,'contado':107,'yape':12,'plin':12,'tarjeta':12,'confirmoRevision':True,'observaciones':''}
req(root+'/cierres',{**confirm,'confirmoRevision':False},a,400);req(root+'/cierres',confirm,m,403)
clock('2026-11-21T12:05:00Z');assert not detail()['cierres'];ok('07:05 continúa pendiente hasta que el administrador decida validar definitivamente')
extra={'hasta':'2026-11-21T08:00:00','motivo':'Clientes continúan en el local','versionEsperada':0}
req(root+'/atencion-extra',extra,m,403);authorized=req(root+'/atencion-extra',extra,a);assert authorized['estado']=='Cerrado'
clock('2026-11-21T12:30:00Z');r=order();assert r['turnoId']==tid and r['fueraDeHorario'];d=detail();assert d['arqueoPendiente']and not d['cierres']and d['resumen']['conformidad']=='Pendiente de conformidad'
assert d['resumen']['ventasFueraHorario']==1 and any('Fuera de horario'in x['concepto']for x in d['resumen']['movimientos']);assert d['resumen']['efectivoEsperado']==119
ok('Antes de la validación permite ventas excepcionales en turno finalizado a las 07:00')
new={**confirm,'claveOperacion':uuid.uuid4().hex}
req(root+'/cierres',new,a,400)
new['contado']=119
closed=req(root+'/cierres',new,a);assert closed['estadoConformidad']=='Conforme' and summary()['turno']['estado']=='Validado'
assert req(root+'/cierres',new,a)==closed
before_stock=stock();before_summary=summary();before_hist=detail()['historial']
order(status=409)
req(root+'/atencion-extra',{**extra,'versionEsperada':1,'hasta':'2026-11-21T09:00:00'},a,409)
req(root+'/movimientos',{**expense,'claveOperacion':uuid.uuid4().hex},a,409)
req(root+'/fondo',{'monto':200,'versionEsperada':1,'motivo':'No permitido tras validación'},a,409)
req(root+'/cierres',{**new,'claveOperacion':uuid.uuid4().hex,'motivo':'No debe volver a editarse'},a,409)
expense_id=next(x['id']for x in before_summary['movimientos']if x['tipo']=='Egreso')
req(root+'/movimientos/'+expense_id+'/anulacion',{'motivo':'No permitido','anulado':True,'versionEsperada':1},a,409)
req(root+'/reabrir',{'motivo':'No permitido'},a,409)
assert stock()==before_stock and summary()==before_summary and detail()['historial']==before_hist
assert not req('/api/carta/turno',token=m)['habilitado']
ok('Validación final Conforme bloquea pedidos, egresos, fondo, excepciones, edición y reapertura sin cambios parciales')
clock('2026-11-21T13:00:00Z');order(status=409);ok('El turno validado permanece bloqueado cuando vence la autorización anterior')
clock('2026-11-22T00:00:00Z');turns=req('/api/caja/turnos',token=a);assert len(turns)==2 and turns[0]['fondoInicial']==0 and turns[0]['fondoDeclaradoUtc']is None
assert turns[0]['id']!=tid;ok('Siguiente noche abre otro turno independiente con cero y sin arrastrar el fondo anterior')
# Exact business example: expected S/240, physical S/200 -> shortage S/40.
second_id=turns[0]['id'];root2='/api/caja/turnos/'+second_id
payload=body(240);payload['detalles'][0]['cantidad']=20
order(payload)
clock('2026-11-22T12:00:00Z');assert req(root2,token=a)['resumen']['efectivoEsperado']==240
review=req(root2+'/cierres',{'claveOperacion':uuid.uuid4().hex,'contado':200,'yape':0,'plin':0,'tarjeta':0,'observaciones':'Faltan cuarenta soles; responsable revisará la entrega.','confirmoRevision':True},a)
assert review['esperado']==240 and review['contado']==200 and review['diferencia']==-40 and review['estadoConformidad']=='Con diferencias'
assert req(root2,token=a)['resumen']['turno']['estado']=='Validado'
order(status=409)
req(root2+'/atencion-extra',{'hasta':'2026-11-22T08:00:00','motivo':'No permitido','versionEsperada':0},a,409)
ok('Caso del negocio: esperado240, contado200, faltante40; se conserva el ingreso original')
# The next scheduled day has no activity: no fabricated physical count is required.
clock('2026-11-23T00:00:00Z');empty=req('/api/carta/turno',token=m)['turno'];assert empty['fondoInicial']==0
clock('2026-11-23T12:00:00Z');empty_detail=req('/api/caja/turnos/'+empty['id'],token=a)
assert empty_detail['resumen']['conformidad']=='Conforme automáticamente: sin movimientos' and not empty_detail['cierres'] and not empty_detail['arqueoPendiente']
assert any(x['accion']=='Conformidad automática sin movimientos'for x in empty_detail['historial'])
ok('Turno sin movimientos ni fondo queda conforme automáticamente, con auditoría y sin arqueo ficticio')
# Recovery after sleeping through two boundaries, and Monday remains the configured rest day.
clock('2026-11-24T00:00:00Z');assert not req('/api/carta/turno',token=m)['habilitado']
clock('2026-11-26T13:00:00Z');recovered=req('/api/caja/turnos',token=a)
assert len([t for t in recovered if t['inicio'].startswith('2026-11-24')])==1
for t in recovered:
 if t['inicio'][:10]in ['2026-11-24','2026-11-25']:
  assert t['estado']=='Cerrado' and req('/api/caja/turnos/'+t['id'],token=a)['resumen']['conformidad'].startswith('Conforme automáticamente')
ok('Recupera límites tras inactividad sin duplicar turnos y respeta días de atención configurados')
Path('/workspace/validacion-caja-automatica/resultado-api.json').write_text(json.dumps({'resultado':'APROBADO','checks':checks,'alcance':'PostgreSQL y reloj controlado locales; no son ventas reales'},ensure_ascii=False,indent=2))
print('APROBADO',len(checks),flush=True)
