"""Real HTTP image round-trip on isolated PostgreSQL; no cloud credentials."""
import base64,hashlib,json,os,urllib.request,uuid
from pathlib import Path
from urllib.parse import urlparse
from caja_integration import req,BASE,CTX
assert os.getenv('CAJA_TEST_PROVIDER')=='PostgreSQL'
a=req('/api/autenticacion/login',{'nombreUsuario':'admin','password':os.getenv('CAJA_TEST_PASSWORD','admin123')})['token']
png=base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a6ioAAAAASUVORK5CYII=')
s=uuid.uuid4().hex[:8]
def upload(path,fields):
 boundary='ETNIA'+uuid.uuid4().hex;data=b''
 for key,value in fields.items():data+=f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n'.encode()
 data+=f'--{boundary}\r\nContent-Disposition: form-data; name="Imagen"; filename="prueba.png"\r\nContent-Type: image/png\r\n\r\n'.encode()+png+f'\r\n--{boundary}--\r\n'.encode()
 request=urllib.request.Request(BASE+path,data=data,headers={'Authorization':'Bearer '+a,'Content-Type':'multipart/form-data; boundary='+boundary})
 with urllib.request.urlopen(request,context=CTX) as r:assert r.status==201;return json.load(r)
cat=upload('/api/categorias',{'Nombre':'PG-IMAGEN-'+s,'Estado':'true'})
product=upload('/api/productos',{'Nombre':'PG-PRODUCTO-'+s,'Stock':'0','Precio':'5','TicketsPorUnidad':'0','CategoriaId':str(cat['id'])})
images=[]
for value in [cat,product]:
 url=value['imagenUrl'];assert urlparse(url).hostname in ('localhost','127.0.0.1')
 with urllib.request.urlopen(url,context=CTX) as r:assert r.read()==png and r.headers['Content-Type'].startswith('image/png')
 images.append({'url':url,'sha256':hashlib.sha256(png).hexdigest()})
Path(os.getenv('PG_IMAGE_EVIDENCE','/tmp/pg-images.json')).write_text(json.dumps(images,indent=2))
print('Imágenes de categoría/producto guardadas en PostgreSQL y recuperadas por HTTP sin autenticación, con bytes coincidentes.')
