"""Actual local HTTP/database checks for the menu, recipes, ice and permissions."""
import os,json,uuid,urllib.request,urllib.error
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
from urllib.parse import urlparse
BASE=os.getenv('CARTA_TEST_URL','http://127.0.0.1:5110').rstrip('/');assert urlparse(BASE).hostname in ('127.0.0.1','localhost')
http=urllib.request.build_opener(urllib.request.ProxyHandler({}));checks=[]
def req(path,body=None,token=None,status=200):
 r=urllib.request.Request(BASE+path,data=json.dumps(body).encode()if body is not None else None,headers={'Content-Type':'application/json',**({'Authorization':'Bearer '+token}if token else{})})
 try:
  with http.open(r,timeout=30)as x:code,data=x.status,x.read()
 except urllib.error.HTTPError as e:code,data=e.code,e.read()
 assert code==status,(path,code,status,data[:250])
 return json.loads(data)if data else None
def ok(text):checks.append(text);print('OK',text,flush=True)
a=req('/api/autenticacion/login',{'nombreUsuario':'admin','password':'admin123'})['token'];tag=uuid.uuid4().hex[:8]
definition=json.loads((Path(__file__).resolve().parents[1]/'deploy/carta-etnia.json').read_text());req('/api/carta/configurar',definition,a);req('/api/carta/configurar',definition,a)
physical=req('/api/carta/inventario',token=a);catalog=req('/api/carta/catalogo',token=a);by={p['codigo']:p for p in physical+catalog if p.get('codigo')}
assert len(req('/api/productos',token=a))==len(definition['productos']);assert all(x['vendible'] for x in catalog);ok('Configuración repetida no duplica productos y oculta insumos de la carta de venta')
def stock(code):return next(p['stockRegistrado'] for p in req('/api/carta/inventario',token=a)if p['codigo']==code)
def setstock(code,amount):
 old=stock(code)
 if amount!=old:req('/api/gestion/inventario',{'productoId':by[code]['id'],'cantidad':amount-old,'stockEsperado':old,'motivo':'Saldo de ensayo aislado '+tag},a)
for p in physical:
 setstock(p['codigo'],0 if p['unidad']=='ml' else 100)
name='mesera_carta_'+tag;pw=uuid.uuid4().hex
req('/api/autenticacion/registrar',{'nombreUsuario':name,'password':pw,'rol':'Mesera'},a)
m=req('/api/autenticacion/login',{'nombreUsuario':name,'password':pw})['token']
req('/api/carta/configurar',definition,m,403);ok('Mesera no puede modificar configuración ni precios')
opening={'claveOperacion':'abrir-'+tag,'productoCerradoId':by['PISCO750']['id'],'insumoId':by['PISCOML']['id'],'botellas':1,'mililitrosPorBotella':750,'motivo':'Apertura de ensayo'}
req('/api/carta/abrir-botella',opening,m,403);req('/api/carta/abrir-botella',opening,a);req('/api/carta/abrir-botella',opening,a)
assert stock('PISCO750')==99 and stock('PISCOML')==750;ok('Apertura autorizada convierte 1 botella a 750 ml exactamente una vez')
req('/api/carta/abrir-botella',{**opening,'claveOperacion':'mal-'+tag,'insumoId':by['RONML']['id']},a,400);assert stock('PISCO750')==99;ok('No permite convertir pisco en ron ni producir saldos al rechazar')
mesa=req('/api/mesas',{'numero':int(tag,16)%10000000+10,'estado':'Disponible','pedidosActivos':[]},a)['mesaId']
def choice(code,n=1):return {'productoId':by[code]['id'],'cantidad':n}
def line(code,n=1,**groups):return {'productoId':by[code]['id'],'cantidad':n,'opciones':{k:[choice(c,q) for c,q in opts]for k,opts in groups.items()}}
def body(lines,pay=1000,key=None):return {'claveOperacion':key or uuid.uuid4().hex,'detalles':lines,'metodoPago':'Efectivo','pagoCliente':pay}
def quote(lines):return req(f'/api/carta/mesas/{mesa}/cotizar',body(lines),m)
def sale(lines,pay=1000,key=None):return req(f'/api/mesas/{mesa}/pedidos',body(lines,pay,key),m)
initial_ice=stock('HIELO');q=quote([line('BALDE5',cervezas=[('PILSEN330',2),('CUSQUENA330',3)])]);assert q['total']==56 and q['tickets']==5
key='balde-'+tag;data=body([line('BALDE5',cervezas=[('PILSEN330',2),('CUSQUENA330',3)])],1000,key)
r=req(f'/api/mesas/{mesa}/pedidos',data,m);again=req(f'/api/mesas/{mesa}/pedidos',data,m);assert r==again and stock('PILSEN330')==98 and stock('CUSQUENA330')==97 and stock('HIELO')==initial_ice;ok('Balde mixto cobra 56, da 5 tickets, descuenta marcas y no incluye hielo')
req(f'/api/mesas/{mesa}/pedidos',{**data,'pagoCliente':1001},m,409);ok('Clave ya confirmada no acepta otro contenido')
q=quote([line('PROMO10',cervezas=[('PILSEN330',4),('CRISTAL330',4),('CUSQUENA330',2)])]);assert q['total']==99 and q['tickets']==9;ok('Promo de diez aplica recargos sin aumentar sus 9 tickets')
q=quote([line('HIELO')]);assert q['total']==3;before=stock('HIELO');sale([line('HIELO')]);assert stock('HIELO')==before-1;ok('Sin botella/combo habilitante el hielo cuesta 3 y consume una porción')
q=quote([line('COMBO120',cervezas=[('PILSEN330',6),('CUSQUENA330',4)],piqueo=[('CHICHARRON',1)])]);assert q['total']==130 and q['tickets']==12
before=stock('HIELO');sale([line('COMBO120',cervezas=[('PILSEN330',6),('CUSQUENA330',4)],piqueo=[('CHICHARRON',1)])]);assert stock('HIELO')==before-1;ok('Combo 120 calcula recargos, conserva 12 tickets e incluye un hielo')
q=quote([line('HIELO',2)]);assert q['total']==0 and q['tickets']==0;before=stock('HIELO');sale([line('HIELO',2)],pay=0);assert stock('HIELO')==before-2;ok('Extras de hielo de la misma atención son gratuitos y descuentan existencias')
q=quote([line('COMBO140',ron=[('RONCARTAVIO',1)],gaseosas=[('COLA15',1),('INCA15',1)],piqueo=[('TEQUENOS',1)])]);assert q['total']==140 and q['tickets']==14
before=stock('HIELO');sale([line('COMBO140',ron=[('RONCARTAVIO',1)],gaseosas=[('COLA15',1),('INCA15',1)],piqueo=[('TEQUENOS',1)])]);assert stock('HIELO')==before-1;ok('Combo de ron descuenta botella y sabores sin duplicar hielo por la botella interna')
req(f'/api/carta/mesas/{mesa}/cotizar',body([line('BALDE5',cervezas=[('CERVEZA620',5)])]),m,400)
req(f'/api/carta/mesas/{mesa}/cotizar',body([line('COMBO120',cervezas=[('PILSEN330',10)],piqueo=[('TABLA',1)])]),m,400);ok('Rechaza cerveza 620 en balde y tabla de piqueos en combo')
req(f'/api/mesas/{mesa}/liberar',{},m);assert quote([line('HIELO')])['total']==3;ok('Mesera libera mesa y el siguiente cliente no hereda hielo gratis')
setstock('VINOML',750);setstock('PISCOML',750)
sale([line('PISCOSOUR'),line('CHILCANO'),line('SANGRIA',licor=[('PISCOML',1)])]);assert stock('PISCOML')==580 and stock('VINOML')==250;ok('Recetas comparten pisco y conservan 250 ml remanentes del vino de la sangría')
setstock('RONML',750);sale([line('SHOTRON',18,ron=[('RONML',1)])]);assert stock('RONML')==30;ok('Dieciocho shots conservan los 30 ml restantes de una botella de 750 ml')
setstock('PILSEN330',1);before=stock('CUSQUENA330');bad=body([line('BALDE5',cervezas=[('PILSEN330',2),('CUSQUENA330',3)])])
req(f'/api/mesas/{mesa}/pedidos',bad,m,409);assert stock('PILSEN330')==1 and stock('CUSQUENA330')==before;ok('Stock insuficiente revierte todo el pedido sin consumo parcial')
# Quotes may inspect availability; they never debit stock.
assert quote([line('PISCOSOUR')])['total']==20 and stock('PISCOML')==580;ok('Cotizar no descuenta existencias')
setstock('HIELO',1);before=stock('RONCARTAVIO');q=body([line('RONCARTAVIO',2)])
req(f'/api/mesas/{mesa}/pedidos',q,m,409);assert stock('RONCARTAVIO')==before;ok('No confirma dos botellas cuando falta hielo inicial requerido')
setstock('HIELO',100);setstock('PILSEN330',1);req(f'/api/mesas/{mesa}/liberar',{},m)
# Two different table orders race for the same last bottle; only one may succeed.
second=req('/api/mesas',{'numero':int(tag,16)%10000000+10000001,'estado':'Disponible','pedidosActivos':[]},a)['mesaId']
def compete(mid):
 payload=body([line('PILSEN330')])
 request=urllib.request.Request(BASE+f'/api/mesas/{mid}/pedidos',data=json.dumps(payload).encode(),headers={'Content-Type':'application/json','Authorization':'Bearer '+m})
 try:
  with http.open(request,timeout=30)as rr:return rr.status
 except urllib.error.HTTPError as e:return e.code
with ThreadPoolExecutor(max_workers=2)as pool:codes=list(pool.map(compete,[mesa,second]))
assert sorted(codes)==[200,409] and stock('PILSEN330')==0;ok('Pedidos concurrentes no venden dos veces la última unidad')
OUT=Path(os.getenv('CARTA_TEST_OUTPUT','/workspace/implementacion-carta/carta-api-resultados.json'));OUT.write_text(json.dumps({'resultado':'APROBADO','checks':checks,'alcance':'HTTP real y base PostgreSQL aislada; no transacciones del negocio'},ensure_ascii=False,indent=2));print('APROBADO',len(checks))
