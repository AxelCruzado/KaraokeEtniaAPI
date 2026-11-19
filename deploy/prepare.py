"""Prepare private deployment variables, without sending or printing secrets."""
import argparse,json,os,re,secrets,subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--host',required=True);p.add_argument('--email',required=True);p.add_argument('--label',default='Produccion');a=p.parse_args()
if not re.fullmatch(r'[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?',a.host) or '.' not in a.host: p.error('Use solo el hostname público, sin https://, puerto ni ruta.')
if not re.fullmatch(r'[^\s=@]+@[^\s=@]+\.[^\s=@]+',a.email):p.error('Correo no válido.')
if not re.fullmatch(r'[A-Za-z0-9_-]+',a.label):p.error('Etiqueta sin espacios ni símbolos.')
root=Path(__file__).resolve().parents[1];front=root.parent/'karaoke-etnia-frontend';env=root/'deploy/.env'
if env.exists():p.error('Ya existe .env; se conserva para no rotar credenciales ni perder acceso a la base.')
def revision(repo):
 if subprocess.check_output(['git','-C',str(repo),'status','--porcelain'],text=True).strip():raise SystemExit('El repositorio debe estar limpio antes de registrar la versión instalada.')
 return subprocess.check_output(['git','-C',str(repo),'rev-parse','HEAD'],text=True).strip()
data={'ETNIA_HOST':a.host,'TLS_EMAIL':a.email,'DEPLOYMENT_LABEL':a.label,'BACKEND_REVISION':revision(root),'FRONTEND_REVISION':revision(front),'DB_PASSWORD':secrets.token_hex(24),'DB_ROOT_PASSWORD':secrets.token_hex(24),'JWT_KEY':secrets.token_hex(48),'ADMIN_PASSWORD':secrets.token_urlsafe(28)}
fd=os.open(env,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
with os.fdopen(fd,'w') as f:f.write(''.join(k+'='+v+'\n' for k,v in data.items()))
print('Creado deploy/.env privado. Consulte ADMIN_PASSWORD únicamente en el servidor; no lo comparta en el chat.')
