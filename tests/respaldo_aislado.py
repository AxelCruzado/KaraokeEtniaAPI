"""Back up etnia_dev locally, restore ONLY to a new uniquely named database.
Keeps raw backup private; exports counts, hashes and execution metadata.
"""
import datetime,hashlib,json,os,subprocess,uuid
from pathlib import Path
from zoneinfo import ZoneInfo
out=Path(os.environ['EVIDENCIA_DIR']);out.mkdir(parents=True,exist_ok=True)
def now():return datetime.datetime.now(ZoneInfo('America/Lima')).isoformat()
base=['docker','--host=unix:///var/run/docker.sock','exec','-i','etnia-dev-mysql']
def sql(db,q):return subprocess.check_output(base+['mysql','-uroot','-N',db],input=q.encode())
def dump(db,extra):return subprocess.check_output(base+['mysqldump','-uroot','--single-transaction','--skip-lock-tables','--no-tablespaces','--set-gtid-purged=OFF','--skip-comments','--hex-blob']+extra+[db])
source='etnia_dev';target='evidencia_restore_'+uuid.uuid4().hex[:12]
r={'inicio':now(),'evaluador':'Codex — ejecución automatizada','origen':source,'destino':target,'resultado_esperado':'Restauración completa en base distinta; tablas, cantidad de filas y datos coincidentes','pasos':['Generar respaldo transaccional local','Crear base nueva con identificador único','Restaurar sin modificar origen','Comparar cada tabla por cantidad y SHA256 de INSERT ordenados','Conservar base restaurada para revisión'],'tablas':[]}
backup=dump(source,[]);private=Path('/workspace/.setup/respaldos-evidencia');private.mkdir(mode=0o700,exist_ok=True);p=private/(target+'.sql');p.write_bytes(backup);p.chmod(0o600)
r['respaldo_sha256']=hashlib.sha256(backup).hexdigest();r['respaldo_bytes']=len(backup);r['respaldo_privado']=str(p)
sql('mysql',f'CREATE DATABASE `{target}`;')
subprocess.run(base+['mysql','-uroot',target],input=backup,check=True)
tables=sql(source,'SHOW TABLES;').decode().splitlines();assert tables==sql(target,'SHOW TABLES;').decode().splitlines()
for t in tables:
 assert '`' not in t
 counts=[int(sql(db,f'SELECT COUNT(*) FROM `{t}`;')) for db in [source,target]]
 def fingerprint(db):
  raw=subprocess.check_output(base+['mysqldump','-uroot','--skip-lock-tables','--no-tablespaces','--set-gtid-purged=OFF','--skip-comments','--hex-blob','--no-create-info','--skip-extended-insert','--order-by-primary','--compact',db,t])
  return hashlib.sha256(raw).hexdigest()
 hashes=[fingerprint(db) for db in [source,target]]
 r['tablas'].append({'tabla':t,'filas_origen':counts[0],'filas_recuperadas':counts[1],'sha256_origen':hashes[0],'sha256_recuperado':hashes[1],'coincide':counts[0]==counts[1] and hashes[0]==hashes[1]})
r['fin']=now();r['estado']='APROBADO' if all(t['coincide'] for t in r['tablas']) else 'FALLO'
(out/'restauracion.json').write_text(json.dumps(r,ensure_ascii=False,indent=2));assert r['estado']=='APROBADO'
print(f"Restauración APROBADA: {len(tables)} tablas; {sum(t['filas_recuperadas'] for t in r['tablas'])} filas; {target}")
