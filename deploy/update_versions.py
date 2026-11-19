"""Refresh only revision metadata after a deliberate clean checkout update."""
import subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[1];env=root/'deploy/.env'
lines=env.read_text().splitlines();updates={}
for key,repo in [('BACKEND_REVISION',root),('FRONTEND_REVISION',root.parent/'karaoke-etnia-frontend')]:
 if subprocess.check_output(['git','-C',str(repo),'status','--porcelain'],text=True).strip():raise SystemExit('Hay cambios locales; no se actualizaron las revisiones.')
 updates[key]=subprocess.check_output(['git','-C',str(repo),'rev-parse','HEAD'],text=True).strip()
seen=set();result=[]
for l in lines:
 key=l.split('=',1)[0]
 if key in updates:result.append(key+'='+updates[key]);seen.add(key)
 else:result.append(l)
if seen!=set(updates):raise SystemExit('Faltan claves de revisión en .env; no se modificó.')
env.write_text('\n'.join(result)+'\n');env.chmod(0o600);print('Revisiones actualizadas; credenciales conservadas.')
