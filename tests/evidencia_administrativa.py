"""Run existing localhost suites with timestamped, sanitized execution evidence.
Never represents production use, physical counts or expert validation.
"""
import contextlib,datetime,json,os,runpy,subprocess,traceback
from pathlib import Path
from zoneinfo import ZoneInfo
import caja_integration as caja
OUT=Path(os.environ['EVIDENCIA_DIR']);OUT.mkdir(parents=True,exist_ok=True)
if (OUT/'ejecucion.jsonl').exists():raise SystemExit('Use un EVIDENCIA_DIR nuevo para no mezclar ejecuciones.')
REPO=Path(__file__).resolve().parents[1]
def now():return datetime.datetime.now(ZoneInfo('America/Lima')).isoformat()
def git(path):return subprocess.check_output(['git','-C',str(path),'rev-parse','HEAD'],text=True).strip()
def clean(value):
 if isinstance(value,dict):return {k:('[OMITIDO]' if any(s in k.lower() for s in ('password','token','clave','hash')) else clean(v)) for k,v in value.items()}
 if isinstance(value,list):return [clean(v) for v in value]
 if isinstance(value,bytes):return value.decode('utf-8-sig',errors='replace')
 return value
original=caja.req;events=[];suite='';counter=0
# req validates actual HTTP status; failures retain their exception and fail the suite.
def recorded(path,body=None,token=None,status=200):
 global counter
 counter+=1;event={'paso':counter,'suite':suite,'fecha':now(),'metodo':'POST' if body is not None else 'GET','ruta':path,'entrada':clean(body),'resultado_esperado_http':status,'autenticado':bool(token)}
 try:
  result=original(path,body,token,status);event.update(resultado_obtenido_http=status,respuesta=clean(result),estado='APROBADO');return result
 except Exception as e:
  event.update(estado='FALLO',error=str(e));raise
 finally:
  events.append(event)
  with (OUT/'ejecucion.jsonl').open('a') as f:f.write(json.dumps(event,ensure_ascii=False)+'\n')
caja.req=recorded
report={'inicio':now(),'evaluador':'Codex — ejecución automatizada; revisión humana pendiente','ambiente':'Local de pruebas, etnia_dev; operaciones de ensayo, sin atención real','version_backend':git(REPO),'version_frontend':git(REPO.parent/'karaoke-etnia-frontend'),'suites':[]}
os.environ['CAJA_TEST_RESULT']=str(OUT/'caja-resultados.json')
for filename in ['recepcion_version_integration.py','gestion_integration.py','compras_ciclo_integration.py','caja_integration.py']:
 suite=filename;entry={'prueba':suite,'inicio':now(),'pasos':'Consultar script archivado y ejecucion.jsonl','resultado_esperado':'Todas las aserciones del script se cumplen; rechazos sin efectos indebidos','evidencia':[suite+'.log','ejecucion.jsonl']}
 (OUT/filename).write_bytes((REPO/'tests'/filename).read_bytes())
 with (OUT/(suite+'.log')).open('w') as log,contextlib.redirect_stdout(log),contextlib.redirect_stderr(log):
  try:
   if filename=='caja_integration.py':caja.main()
   else:runpy.run_path(str(REPO/'tests'/filename),run_name='__main__')
   entry.update(estado='APROBADO',resultado_obtenido='Todas las aserciones completadas')
  except Exception:entry.update(estado='FALLO',resultado_obtenido=traceback.format_exc());traceback.print_exc()
 entry['fin']=now();report['suites'].append(entry)
 (OUT/'resultado.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
 print(suite,entry['estado'],flush=True)
# Persist actual generated report exports separately for review.
for e in events:
 if e['ruta'].startswith('/api/caja/reportes/') and '/exportar' in e['ruta'] and e.get('estado')=='APROBADO':
  ident=e['ruta'].split('/')[4];is_csv='formato=csv' in e['ruta'];p=OUT/(f'R01-{ident}'+('.csv' if is_csv else '.json'))
  p.write_text(e['respuesta'] if is_csv else json.dumps(e['respuesta'],ensure_ascii=False,indent=2))
report['fin']=now();(OUT/'resultado.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
assert all(s['estado']=='APROBADO' for s in report['suites']), 'Hay pruebas fallidas'
