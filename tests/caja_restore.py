"""Restore verification for the isolated etnia_dev database, never production."""
import os,subprocess,json,hashlib,secrets
from pathlib import Path

def main():
    env={k:v for k,v in os.environ.items() if k not in ['DOCKER_HOST','DOCKER_CONTEXT','DOCKER_TLS','DOCKER_TLS_VERIFY','DOCKER_CERT_PATH']}
    cmd=['docker','--host=unix:///var/run/docker.sock','exec','-i','etnia-dev-mysql']
    def sql(q,db='etnia_dev'):return subprocess.check_output(cmd+['mysql','-uroot','-N','--batch',db],input=q.encode(),env=env)
    tables=sql("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND LEFT(TABLE_NAME,5)='Caja_' ORDER BY TABLE_NAME").decode().splitlines()
    assert len(tables)==7
    backup=subprocess.check_output(cmd+['mysqldump','-uroot','--single-transaction','--skip-comments','--no-tablespaces','--set-gtid-purged=OFF','etnia_dev']+tables,env=env)
    output=Path(os.getenv('CAJA_TEST_OUTPUT','/tmp/caja_restore'));output.mkdir(parents=True,exist_ok=True);(output/'caja_pruebas.sql').write_bytes(backup)
    restore='etnia_caja_restore_'+secrets.token_hex(4);sql(f'CREATE DATABASE `{restore}` CHARACTER SET utf8mb4')
    subprocess.run(cmd+['mysql','-uroot',restore],input=backup,check=True,env=env)
    results=[]
    for table in tables:
        a=sql(f'SELECT * FROM `{table}` ORDER BY Id');b=sql(f'SELECT * FROM `{table}` ORDER BY Id',restore);assert a==b,table
        results.append({'tabla':table,'filas':int(sql(f'SELECT COUNT(*) FROM `{table}`')),'sha256':hashlib.sha256(a).hexdigest(),'restaurado_identico':True})
    q='SELECT COALESCE(SUM(Importe),0),COUNT(*) FROM Caja_MovimientoCaja WHERE Anulado=0';assert sql(q)==sql(q,restore)
    sql(f'DROP DATABASE `{restore}`')
    (output/'F11.json').write_text(json.dumps({'codigo':'F11','resultado':'APROBADA','tablas':results,'alcance':'Base local de prueba; referencias y snapshots, no archivos externos'},indent=2))
    print('F11 OK: siete tablas restauradas idénticas; base temporal eliminada.')
if __name__=='__main__':main()
