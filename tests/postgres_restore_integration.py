"""Backup and restore only local etnia_pg into a uniquely named isolated database."""
import datetime,hashlib,json,os,subprocess,uuid
from pathlib import Path
from zoneinfo import ZoneInfo
base=['docker','--host=unix:///var/run/docker.sock','exec','-i','etnia-dev-postgres'];source='etnia_pg';target='etnia_restore_'+uuid.uuid4().hex[:10]
def sql(db,query):return subprocess.check_output(base+['psql','-U','etnia','-d',db,'-At','-v','ON_ERROR_STOP=1'],input=query.encode())
assert int(sql(source,'SELECT COUNT(*) FROM "StoredImages";'))>0, 'Ejecute postgres_storage_integration.py antes de comprobar el respaldo de imágenes.'
report={'inicio':datetime.datetime.now(ZoneInfo('America/Lima')).isoformat(),'origen':source,'destino':target,'evaluador':'Ejecución automatizada local; no operación del establecimiento','esperado':'Restauración aislada, igualdad de tablas, filas y contenido; secuencias recuperadas','tablas':[]}
backup=subprocess.check_output(base+['pg_dump','-U','etnia','-d',source,'-Fc','--no-owner','--no-privileges']);folder=Path('/workspace/.setup/pg-backups');folder.mkdir(mode=0o700,exist_ok=True);p=folder/(target+'.dump');p.write_bytes(backup);p.chmod(0o600)
report['backup_sha256']=hashlib.sha256(backup).hexdigest();report['backup_bytes']=len(backup)
sql('etnia_pg',f'CREATE DATABASE {target};')
subprocess.run(base+['pg_restore','-U','etnia','-d',target,'--no-owner','--no-privileges','--exit-on-error'],input=backup,check=True)
query="SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename;"
tables=sql(source,query).decode().splitlines();assert tables==sql(target,query).decode().splitlines()
for table in tables:
 assert '"' not in table
 q=f'SELECT row_to_json(t)::text FROM public."{table}" t ORDER BY row_to_json(t)::text;'
 before=sql(source,q);after=sql(target,q);count=len(before.splitlines())
 item={'tabla':table,'filas_origen':count,'filas_recuperadas':len(after.splitlines()),'sha256_origen':hashlib.sha256(before).hexdigest(),'sha256_destino':hashlib.sha256(after).hexdigest(),'coincide':before==after};report['tablas'].append(item)
seq="SELECT sequencename,last_value FROM pg_sequences WHERE schemaname='public' ORDER BY sequencename;"
report['secuencias_coinciden']=sql(source,seq)==sql(target,seq)
report['estado']='APROBADO' if all(x['coincide'] for x in report['tablas']) and report['secuencias_coinciden'] else 'FALLO';report['fin']=datetime.datetime.now(ZoneInfo('America/Lima')).isoformat()
out=Path(os.getenv('PG_RESTORE_RESULT','/tmp/postgres-restauracion.json'));out.write_text(json.dumps(report,ensure_ascii=False,indent=2));assert report['estado']=='APROBADO'
print('Restauración PostgreSQL APROBADA:',len(tables),'tablas,',sum(x['filas_recuperadas'] for x in report['tablas']),'filas, secuencias e imágenes incluidas; base aislada',target)
