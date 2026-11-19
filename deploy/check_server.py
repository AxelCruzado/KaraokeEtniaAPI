"""Read-only Linux server readiness report; never reads credentials or installs software."""
import argparse,json,os,platform,shutil,subprocess,sys
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--json',action='store_true');a=p.parse_args()
report={'sistema':platform.system(),'arquitectura':platform.machine(),'cpu_logicas':os.cpu_count(),'docker_instalado':bool(shutil.which('docker')),'python':platform.python_version(),'observaciones':[]}
if Path('/etc/os-release').exists():
 values={k:v.strip('"') for line in Path('/etc/os-release').read_text().splitlines() if '=' in line for k,v in [line.split('=',1)]}
 report['distribucion']=values.get('PRETTY_NAME','No identificada')
if Path('/proc/meminfo').exists():
 values={line.split(':',1)[0]:int(line.split()[1]) for line in Path('/proc/meminfo').read_text().splitlines() if line.split()[1].isdigit()}
 report['ram_GiB']=round(values['MemTotal']/1024/1024,2)
 if report['ram_GiB']<3.5:report['observaciones'].append('RAM inferior a los 4 GiB recomendados; valorar builds fuera de la VM antes de publicar.')
report['disco_libre_GiB']=round(shutil.disk_usage(Path.cwd()).free/1024**3,2)
if report['disco_libre_GiB']<12:report['observaciones'].append('Menos de 12 GiB libres: revisar espacio antes de crear imágenes, volúmenes y respaldos.')
if shutil.which('docker'):
 try:
  report['docker_accesible']=subprocess.run(['docker','info','--format','{{.ServerVersion}}'],capture_output=True,text=True,timeout=10).returncode==0
  report['compose_disponible']=subprocess.run(['docker','compose','version'],capture_output=True,text=True,timeout=10).returncode==0
 except (subprocess.TimeoutExpired,OSError):report['docker_accesible']=False
if shutil.which('ss'):
 try:
  output=subprocess.check_output(['ss','-H','-ltn'],text=True,timeout=5)
  ports={int(line.split()[3].rsplit(':',1)[1]) for line in output.splitlines() if len(line.split())>=4}
  report['puerto_80_ocupado']=80 in ports;report['puerto_443_ocupado']=443 in ports
 except (ValueError,subprocess.SubprocessError):report['observaciones'].append('No se pudieron comprobar los puertos locales.')
report['alcance']='Solo lectura local. No verifica cuotas gratuitas, IP pública, DNS, firewall de nube ni acceso desde internet.'
if a.json:print(json.dumps(report,ensure_ascii=False,indent=2))
else:
 for key,value in report.items():print(key+': '+(json.dumps(value,ensure_ascii=False) if isinstance(value,(list,dict)) else str(value)))
sys.exit(0)
