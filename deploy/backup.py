"""Private logical database backup; does not change live data."""
import datetime,gzip,hashlib,json,os,subprocess
from pathlib import Path
from zoneinfo import ZoneInfo
root=Path(__file__).resolve().parent;out=root/'backups'/datetime.datetime.now(ZoneInfo('America/Lima')).strftime('%Y%m%d-%H%M%S');out.mkdir(parents=True,mode=0o700,exist_ok=False)
cmd=['docker','compose','--env-file',str(root/'.env'),'-f',str(root/'compose.yml'),'exec','-T','db','sh','-c','MYSQL_PWD="$MYSQL_PASSWORD" exec mysqldump -u"$MYSQL_USER" --single-transaction --no-tablespaces "$MYSQL_DATABASE"']
# SQL includes private account hashes; keep this artifact off public repositories.
raw=subprocess.check_output(cmd);p=out/'database.sql.gz';p.write_bytes(gzip.compress(raw));p.chmod(0o600)
meta={'fecha':datetime.datetime.now(ZoneInfo('America/Lima')).isoformat(),'archivo':p.name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'tipo':'Respaldo lógico; recuperación no ejecutada por este comando'}
(out/'manifest.json').write_text(json.dumps(meta,indent=2));print('Respaldo privado guardado en '+str(out))
