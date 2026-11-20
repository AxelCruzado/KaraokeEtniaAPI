"""Collect public deployment metadata without login or business writes."""
import argparse,datetime,hashlib,json,re,ssl,urllib.request
from pathlib import Path
from urllib.parse import urlparse
from zoneinfo import ZoneInfo
p=argparse.ArgumentParser();p.add_argument('--url',required=True);p.add_argument('--api-url');p.add_argument('--backend-ref',required=True);p.add_argument('--frontend-ref',required=True);p.add_argument('--responsable',required=True);p.add_argument('--cargo',required=True);p.add_argument('--out',required=True);a=p.parse_args()
if not all(re.fullmatch(r'[0-9a-f]{40}', x) for x in [a.backend_ref,a.frontend_ref]):p.error('Use los SHA completos de 40 caracteres de ambos commits.')
api_url=a.api_url or a.url
for origin in [a.url,api_url]:
 u=urlparse(origin)
 if u.scheme!='https' or not u.hostname or u.username or u.password or u.path not in ('','/') or u.query or u.fragment:p.error('Use orígenes HTTPS públicos sin credenciales ni rutas.')
out=Path(a.out);out.mkdir(parents=True,exist_ok=False);checks=[]
for origin,path,name,expected in [(a.url,'/version.json','frontend',a.frontend_ref),(api_url,'/api/version','backend',a.backend_ref),(api_url,'/health/ready','disponibilidad',None)]:
 with urllib.request.urlopen(origin.rstrip('/')+path,context=ssl.create_default_context(),timeout=35) as r:raw=r.read();status=r.status
 content=json.loads(raw);(out/(name+'.json')).write_bytes(raw)
 revision=content.get('revision','');matches=(expected in revision) if name=='backend' else (revision==expected if expected else True)
 checks.append({'origen':origin,'ruta':path,'http':status,'revision_esperada':expected,'revision_obtenida':revision if expected else None,'coincide':matches,'sha256':hashlib.sha256(raw).hexdigest()})
report={'instrumento':'F-01','edicion':'1.1','fecha_Lima':datetime.datetime.now(ZoneInfo('America/Lima')).isoformat(),'url':a.url.rstrip('/'),'url_api':api_url.rstrip('/'),'responsable_declarado':a.responsable,'cargo_declarado':a.cargo,'captura':'Automatizada; identidad y revisión humana pendientes de firma','comprobaciones':checks,'resultado':'COINCIDE' if all(c['coincide'] for c in checks) else 'REVISAR_VERSIONES','alcance':'Acceso HTTPS, versiones declaradas por artefactos y disponibilidad DB. No acredita compra, inventario físico, arqueo ni uso real.'}
(out/'F-01.json').write_text(json.dumps(report,ensure_ascii=False,indent=2));print(report['resultado']+': evidencia guardada sin contraseñas ni tokens.');raise SystemExit(0 if report['resultado']=='COINCIDE' else 1)
