"""Local-only production startup test on a new database; no production data."""
import json,os,secrets,subprocess,time,urllib.request,urllib.error,uuid
from pathlib import Path
root=Path(__file__).resolve().parents[1];db='etnia_deploy_'+uuid.uuid4().hex[:10]
mysql=['docker','--host=unix:///var/run/docker.sock','exec','-i','etnia-dev-mysql','mysql','-uroot','-N']
subprocess.run(mysql,input=('CREATE DATABASE `'+db+'`;').encode(),check=True)
env=os.environ.copy();password=secrets.token_urlsafe(24)
env.update({'ASPNETCORE_ENVIRONMENT':'Production','ASPNETCORE_URLS':'http://127.0.0.1:5103','DOTNET_ROOT':'/workspace/.tools/dotnet10','ConnectionStrings__DefaultConnection':f'Server=127.0.0.1;Port=3306;Database={db};User=root;Password=;','Jwt__Key':secrets.token_hex(48),'Jwt__Issuer':'https://deployment.invalid','Jwt__Audience':'https://deployment.invalid','Bootstrap__AdminPassword':password,'Database__MigrateOnStartup':'true','Storage__ImagePath':'images/categorias','Deployment__Label':'Ensayo_aislado'})
log=Path('/tmp/etnia-deployment-smoke-api.log');stream=log.open('w')
cmd=['/workspace/.tools/dotnet10/dotnet',str(root/'bin/Release/net10.0/KaraokeEtniaApi.dll')]
base='http://127.0.0.1:5103'
def req(path,body=None,token=None,expected=200):
 headers={'Content-Type':'application/json'}
 if token:headers['Authorization']='Bearer '+token
 r=urllib.request.Request(base+path,data=json.dumps(body).encode() if body is not None else None,headers=headers)
 try:
  with urllib.request.urlopen(r,timeout=8) as x:status=x.status;raw=x.read()
 except urllib.error.HTTPError as e:status=e.code;raw=e.read()
 assert status==expected,(path,status,expected)
 return json.loads(raw) if raw else None
process=subprocess.Popen(cmd,cwd=root,env=env,stdout=stream,stderr=subprocess.STDOUT)
try:
 for attempt in range(100):
  if process.poll() is not None:raise AssertionError('API startup failed; inspect private local log')
  try:req('/health/ready');break
  except (urllib.error.URLError,TimeoutError):time.sleep(0.25)
 else:raise AssertionError('API startup timed out')
 req('/api/autenticacion/login',{'nombreUsuario':'admin','password':'admin123'},expected=401)
 a=req('/api/autenticacion/login',{'nombreUsuario':'admin','password':password})['token']
 for path in ['/api/productos','/api/categorias','/api/mesas','/api/reportes/diario?year=2026&month=11&day=17','/api/gestion/resumen']:
  req(path,expected=401);req(path,token=a)
 assert req('/api/version')['ambiente']=='Ensayo_aislado'
 assert 'deploy-validation' in req('/api/version')['revision']
 req('/health/live')
 # No default password reset on restart, no fixture operations imported.
 process.terminate();process.wait(timeout=10)
 process=subprocess.Popen(cmd,cwd=root,env=env,stdout=stream,stderr=subprocess.STDOUT)
 for attempt in range(100):
  try:req('/health/ready');break
  except (urllib.error.URLError,TimeoutError):time.sleep(0.25)
 req('/api/autenticacion/login',{'nombreUsuario':'admin','password':password})
 counts=subprocess.check_output(mysql+[db],input=b'SELECT COUNT(*) FROM __EFMigrationsHistory; SELECT COUNT(*) FROM __CajaMigrationsHistory; SELECT COUNT(*) FROM Compras; SELECT COUNT(*) FROM Caja_TurnoCaja;').decode().splitlines()
 assert counts==['4','1','0','0'],counts
 print('APROBADO: base nueva; ambos historiales migrados; administrador privado; admin123 rechazado; rutas administrativas 401; versiones; reinicio sin pérdida. DB aislada:',db)
finally:
 process.terminate();process.wait(timeout=10);stream.close()
