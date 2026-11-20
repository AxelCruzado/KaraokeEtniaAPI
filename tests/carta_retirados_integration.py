"""Retirar una categoría oculta su carta/inventario operativo, conservando registros."""
import json,urllib.request,urllib.parse,uuid
BASE='http://127.0.0.1:5112';http=urllib.request.build_opener(urllib.request.ProxyHandler({}))
def req(path,data=None,method=None,form=None):
 headers={'Authorization':'Bearer '+token}if 'token'in globals()else{}
 if form is not None:raw=urllib.parse.urlencode(form).encode();headers['Content-Type']='application/x-www-form-urlencoded'
 else:raw=json.dumps(data).encode()if data is not None else None;headers['Content-Type']='application/json'
 with http.open(urllib.request.Request(BASE+path,data=raw,method=method,headers=headers),timeout=30)as r:b=r.read();return json.loads(b)if b else None
token=req('/api/autenticacion/login',{'nombreUsuario':'admin','password':'admin123'})['token'];tag=uuid.uuid4().hex[:8]
c=req('/api/categorias',form={'Nombre':'Retirada-'+tag,'Estado':'true'})
p=req('/api/productos',form={'Nombre':'Retirado-'+tag,'CategoriaId':c['id'],'Stock':2,'Precio':5,'TicketsPorUnidad':0})
assert any(x['id']==p['id']for x in req('/api/carta/inventario'))
req('/api/categorias/'+str(c['id']),method='PUT',form={'Nombre':c['nombre'],'Estado':'false'})
assert all(x['id']!=p['id']for x in req('/api/carta/inventario'))
assert all(x['id']!=p['id']for x in req('/api/carta/catalogo'))
assert next(x for x in req('/api/productos')if x['id']==p['id'])['stock']==2
print('OK Categoría retirada no duplica catálogo/inventario operativo; conserva producto y saldo histórico')
