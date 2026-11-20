"""Local production startup with verified PostgreSQL TLS; isolated etnia_pg_tls DB.
Requires etnia-dev-postgres on 127.0.0.1:5433 and local test certificates.
No calls to Aiven or Render; no customer operations.
"""
import base64,hashlib,json,os,secrets,subprocess,time,urllib.request,urllib.error,uuid
from pathlib import Path
root=Path(__file__).resolve().parents[1];db='etnia_tls_'+uuid.uuid4().hex[:10]
cmd=['docker','--host=unix:///var/run/docker.sock','exec','-i','etnia-dev-postgres','psql','-U','etnia','-d','etnia_pg','-v','ON_ERROR_STOP=1','-At']
subprocess.run(cmd,input=('CREATE DATABASE '+db+';').encode(),check=True,stdout=subprocess.DEVNULL)
pg=dict(line.split('=',1) for line in Path('/workspace/.setup/postgres-validation.env').read_text().splitlines());password=secrets.token_urlsafe(24)
env=os.environ.copy();env.update({'DOTNET_ROOT':'/workspace/.tools/dotnet10','ASPNETCORE_ENVIRONMENT':'Production','ASPNETCORE_URLS':'http://127.0.0.1:5105','Database__Provider':'PostgreSQL','Database__MigrateOnStartup':'true','Storage__Backend':'Database','Deployment__Render':'true','Storage__ImagePath':'images/categorias','ConnectionStrings__DefaultConnection':f'postgres://etnia:{pg["POSTGRES_PASSWORD"]}@127.0.0.1:5433/{db}?sslmode=require','Database__CaCertificatePem':Path('/workspace/.setup/pg-tls/ca.crt').read_text(),'Jwt__Key':secrets.token_hex(48),'Jwt__Issuer':'EtniaTest','Jwt__Audience':'EtniaTest','Bootstrap__AdminPassword':password,'Cors__AllowedOrigins__0':'https://etnia-test.invalid'})
for key in ['RENDER_EXTERNAL_URL','Deployment__PublicUrl']:env.pop(key,None)
log=Path('/tmp/pg-production-api.log').open('w');app=['/workspace/.tools/dotnet10/dotnet',str(root/'bin/Release/net10.0/KaraokeEtniaApi.dll')]
def start():
 proc=subprocess.Popen(app,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT)
 for i in range(100):
  if proc.poll() is not None:raise AssertionError('API failed; inspect private local log')
  try:
   with urllib.request.urlopen('http://127.0.0.1:5105/health/ready',timeout=2) as r:assert r.status==200
   return proc
  except (urllib.error.URLError,TimeoutError):time.sleep(.25)
 proc.terminate();raise AssertionError('API timed out')
proc=start()
try:
 def req(path,body=None,expected=200,headers=None,method=None):
  h={'Content-Type':'application/json',**(headers or {})};data=json.dumps(body).encode() if body is not None else None
  request=urllib.request.Request('http://127.0.0.1:5105'+path,data=data,headers=h,method=method)
  try:
   with urllib.request.urlopen(request,timeout=10) as r:code=r.status;raw=r.read();rh=r.headers
  except urllib.error.HTTPError as e:code=e.code;raw=e.read();rh=e.headers
  assert code==expected,(path,code,expected)
  return json.loads(raw) if raw else None,rh
 req('/api/autenticacion/login',{'nombreUsuario':'admin','password':'admin123'},401)
 token=req('/api/autenticacion/login',{'nombreUsuario':'admin','password':password})[0]['token']
 assert req('/api/version')[0]['baseDeDatos']=='PostgreSQL'
 req('/api/gestion/resumen',expected=401)
 for origin,allowed in [('https://etnia-test.invalid',True),('https://other.invalid',False)]:
  _,headers=req('/api/gestion/resumen',expected=204,method='OPTIONS',headers={'Origin':origin,'Access-Control-Request-Method':'GET','Access-Control-Request-Headers':'authorization'})
  assert (headers.get('Access-Control-Allow-Origin')==origin)==allowed
 # Use the same real multipart image test with the private production test account.
 os.environ.update({'CAJA_TEST_BASE_URL':'http://127.0.0.1:5105','CAJA_TEST_PROVIDER':'PostgreSQL','CAJA_TEST_PASSWORD':password,'CAJA_TEST_CA':'/workspace/.setup/localhost.crt','PG_IMAGE_EVIDENCE':'/tmp/pg-production-images.json'})
 import runpy
 runpy.run_path(str(root/'tests/postgres_storage_integration.py'),run_name='__main__')
 images=json.loads(Path('/tmp/pg-production-images.json').read_text())
 proc.terminate();proc.wait(timeout=10);proc=start()
 for image in images:
  with urllib.request.urlopen(image['url']) as r:assert hashlib.sha256(r.read()).hexdigest()==image['sha256']
 req('/api/autenticacion/login',{'nombreUsuario':'admin','password':password})
 print('APROBADO: Production; URI PostgreSQL convertida; TLS VerifyFull con CA; CORS permitido/rechazado; cuenta privada; ambas imágenes persistentes tras reiniciar API. Base aislada:',db)
finally:
 proc.terminate();proc.wait(timeout=10);log.close()
