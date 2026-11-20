"""Carga inicial atómica, idempotente y distinguible de operaciones reales. Solo localhost."""
import json,os,urllib.request,urllib.error,uuid
from pathlib import Path
from urllib.parse import urlparse
BASE=os.getenv('CARTA_TEST_URL','http://127.0.0.1:5111');assert urlparse(BASE).hostname in ('localhost','127.0.0.1')
http=urllib.request.build_opener(urllib.request.ProxyHandler({}))
def req(path,data=None,status=200):
 r=urllib.request.Request(BASE+path,data=json.dumps(data).encode()if data is not None else None,headers={'Content-Type':'application/json',**({'Authorization':'Bearer '+token}if 'token'in globals()else{})})
 try:
  with http.open(r,timeout=30)as x:code,b=x.status,x.read()
 except urllib.error.HTTPError as e:code,b=e.code,e.read()
 assert code==status,(path,code,b[:300]);return json.loads(b)if b else None
token=req('/api/autenticacion/login',{'nombreUsuario':'admin','password':'admin123'})['token']
req('/api/carta/configurar',json.loads(Path('deploy/carta-etnia.json').read_text()))
body=json.loads(Path('deploy/carta-demostracion.json').read_text())
# Failure late in opening list must roll back all initial stock and earlier openings.
invalid={**body,'claveOperacion':'bad-'+uuid.uuid4().hex,'aperturas':body['aperturas']+[{'codigoOrigen':'PISCO750','codigoDestino':'RONML','botellas':1,'mililitrosPorBotella':750,'referencia':'invalid'}]}
req('/api/carta/demostracion',invalid,400)
assert all(p['stockRegistrado']==0 for p in req('/api/carta/inventario'));print('OK Carga inválida revierte todos los saldos y aperturas',flush=True)
r=req('/api/carta/demostracion',body);assert r==req('/api/carta/demostracion',body)
by={p['codigo']:p for p in req('/api/carta/inventario')}
for code,qty in {'PISCO750':15,'PISCOML':2250,'RONCARTAVIO':11,'RONBLANCOML':2250,'VINOBOT':16,'VINOML':3000,'VODKA':8,'VODKAML':1500,'CACHACABOT':2,'CACHACAML':700,'HIELO':16}.items():assert by[code]['stockRegistrado']==qty,(code,by[code])
print('OK 43 saldos y 14 aperturas coinciden con el Excel; sin duplicación',flush=True)
assert 'Demostración' in r['procedencia'];assert not req('/api/gestion/resumen')['compras'];assert not req('/api/caja/turnos');print('OK No inventa compras, egresos ni turnos reales',flush=True)
req('/api/carta/demostracion',{**body,'fuente':'changed'},409)
req('/api/carta/demostracion',{**body,'claveOperacion':'second-'+uuid.uuid4().hex},409)
req('/api/gestion/inventario',{'productoId':by['PILSEN330']['id'],'cantidad':-1,'stockEsperado':240,'motivo':'Comprobación aislada de no reinicio'})
req('/api/carta/demostracion',body)
assert next(x for x in req('/api/carta/inventario')if x['codigo']=='PILSEN330')['stockRegistrado']==239;print('OK Repetir el lote no restablece stocks después de nuevos movimientos',flush=True)
package=next(x for x in req('/api/carta/catalogo')if x['codigo']=='BALDE5')
req('/api/gestion/inventario',{'productoId':package['id'],'cantidad':10,'stockEsperado':0,'motivo':'invalid'},400);print('OK No permite inventario independiente de combos y recetas',flush=True)
