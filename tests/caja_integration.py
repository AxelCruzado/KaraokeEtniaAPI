"""Integration checks against an isolated local API. Uses no mocked responses.
CAJA_TEST_CA=/path/to/localhost.crt python tests/caja_integration.py
Do not run against production: creates explicitly identified local test records.
"""
import os,json,ssl,urllib.request,urllib.error,urllib.parse,uuid,secrets
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
BASE=os.getenv('CAJA_TEST_BASE_URL','https://localhost:7251').rstrip('/')
assert urllib.parse.urlparse(BASE).hostname in ('localhost','127.0.0.1'), 'Local isolated database only'
CTX=ssl.create_default_context(cafile=os.getenv('CAJA_TEST_CA'))
results=[]
def req(path,body=None,token=None,status=200):
    headers={'Content-Type':'application/json'}
    if token:headers['Authorization']='Bearer '+token
    request=urllib.request.Request(BASE+path,data=json.dumps(body).encode() if body is not None else None,headers=headers)
    try:
        with urllib.request.urlopen(request,context=CTX,timeout=25) as r:code,data=r.status,r.read()
    except urllib.error.HTTPError as e:code,data=e.code,e.read()
    assert code==status,f'{path}: HTTP {code}, expected {status}; {data[:300]!r}'
    try:return json.loads(data)
    except (ValueError,UnicodeDecodeError):return data

def main():
    admin=req('/api/autenticacion/login',{'nombreUsuario':os.getenv('CAJA_TEST_ADMIN','admin'),'password':os.getenv('CAJA_TEST_PASSWORD','admin123')})['token']
    suffix=uuid.uuid4().hex[:8];who='cajero_test_'+suffix;pw=secrets.token_urlsafe(24)
    req('/api/autenticacion/registrar',{'nombreUsuario':who,'password':pw,'rol':'Cajero'},admin)
    cash=req('/api/autenticacion/login',{'nombreUsuario':who,'password':pw})['token']
    t=req('/api/caja/turnos',{'caja':'TEST-'+suffix,'fecha':'2026-11-17','fondoInicial':100},cash);root='/api/caja/turnos/'+t['id']
    def detail():return req(root,token=admin)
    def summary():return detail()['resumen']
    def passed(code):results.append(code);print(code,'OK',flush=True)
    def movement(kind,amount,key,date='2026-11-18T02:00:00'):
        return {'claveOperacion':key,'tipo':kind,'naturaleza':'Venta cobrada' if kind=='Ingreso' else 'Gasto','concepto':'Prueba aislada','categoria':'Ventas' if kind=='Ingreso' else 'Insumos','documento':'DOC-'+key,'importe':amount,'fechaOperacion':date,'pagos':[{'medio':'Efectivo','importe':amount}],'referenciaVenta':'VENTA-'+key}
    inc=movement('Ingreso',200,'ing-'+suffix,'2026-11-17T21:00:00');inc['pagos']=[{'medio':'Efectivo','importe':120},{'medio':'Yape','importe':80}]
    saved=req(root+'/movimientos',inc,cash)
    assert summary()['ingresos']==200 and summary()['entradasEfectivo']==120;passed('F02')
    eg=movement('Egreso',35,'eg-'+suffix);me=req(root+'/movimientos',eg,cash)
    dy=movement('Egreso',20,'dy-'+suffix);dy['categoria']='Servicios';dy['pagos']=[{'medio':'Yape','importe':20}];req(root+'/movimientos',dy,cash)
    correction={'versionEsperada':1,'motivo':'Comprobante confirma S/ 38','concepto':eg['concepto'],'categoria':'Insumos','documento':eg['documento'],'importe':38,'pagos':[{'medio':'Efectivo','importe':38}]}
    req(root+'/movimientos/'+me['id']+'/corregir',correction,cash,403)
    req(root+'/movimientos/'+me['id']+'/corregir',{**correction,'motivo':''},admin,400)
    req(root+'/movimientos/'+me['id']+'/corregir',correction,admin)
    assert summary()['egresos']==58 and summary()['salidasEfectivo']==38
    assert any(json.loads(a['anteriorJson']).get('importe')==35 and json.loads(a['posteriorJson']).get('importe')==38 for a in detail()['historial'] if a['accion']=='Corrección');passed('F03')
    assert req(root+'/movimientos',inc,cash)['id']==saved['id']
    req(root+'/movimientos',{**inc,'importe':201},cash,409)
    req(root+'/movimientos',{**inc,'claveOperacion':'dup-'+suffix},cash,409)
    bad=movement('Egreso',10,'bad-'+suffix);bad['pagos'][0]['importe']=9;req(root+'/movimientos',bad,cash,400)
    assert summary()['cantidadMovimientos']==3;passed('F04')
    req('/api/caja/turnos',{'caja':t['caja'],'fecha':'2026-11-18','fondoInicial':0},admin,409)
    req('/api/caja/turnos',{'caja':'MON-'+suffix,'fecha':'2026-11-16','fondoInicial':0},admin,400)
    assert t['inicio']=='2026-11-17T19:00:00' and t['fin']=='2026-11-18T07:00:00';passed('F05')
    req(root+'/movimientos',movement('Egreso',1,'late-'+suffix,'2026-11-18T07:00:00'),cash,400)
    req(root+'/movimientos',movement('Egreso',1,'early-'+suffix,'2026-11-17T18:59:59'),cash,400)
    filtered=req('/api/caja/movimientos?turnoId='+t['id']+'&desdeTurno=2026-11-17&hastaTurno=2026-11-17',token=cash)
    assert len(filtered)==3 and summary()['efectivoEsperado']==182;passed('F07')
    req(root+'/cierres',{'claveOperacion':'close-'+suffix},cash,400)
    first=req(root+'/cierres',{'claveOperacion':'close-'+suffix,'contado':180},cash)
    baseline=detail()['cierres'][0]
    assert (first['esperado'],first['contado'],first['diferencia'],first['version'])==(182,180,-2,1)
    assert req(root+'/cierres',{'claveOperacion':'close-'+suffix,'contado':180},cash)['id']==first['id']
    req(root+'/movimientos',movement('Egreso',1,'closed-'+suffix),cash,409);passed('F06')
    before=detail();req(root+'/reabrir',{'motivo':'Intento de cajero'},cash,403);assert detail()==before
    req('/api/caja/turnos',status=401)
    req('/api/autenticacion/registrar',{'nombreUsuario':'invalid','password':'x','rol':'Administrador'},status=401);passed('F01')
    r=req(root+'/reportes',{},cash);export=req('/api/caja/reportes/'+r['id']+'/exportar',token=cash);s=json.loads(r['snapshotJson'])
    assert s['ingresos']==200 and s['egresos']==58 and s['saldoMovimientos']==142
    assert s['cobrosPorMedio']=={'efectivo':120,'yape':80,'tarjeta':0} and export['datos']==s;passed('F09')
    assert len(req('/api/caja/movimientos?turnoId='+t['id']+'&medio=Yape&tipo=Egreso',token=cash))==1
    csv=req('/api/caja/reportes/'+r['id']+'/exportar?formato=csv',token=cash).decode('utf-8-sig');assert r['codigo'] in csv and t['codigo'] in csv;passed('F10')
    req(root+'/reabrir',{'motivo':''},admin,400)
    req(root+'/reabrir',{'motivo':'Revisión autorizada; conservar primer cierre'},admin)
    assert detail()['cierres'][0]==baseline;passed('F08')
    path=root+'/movimientos/'+me['id']+'/anulacion'
    req(path,{'motivo':'Anulación de prueba','anulado':True,'versionEsperada':2},admin);assert summary()['egresos']==20
    req(path,{'motivo':'Reintento','anulado':True,'versionEsperada':2},admin,409)
    req(path,{'motivo':'Reversión','anulado':False,'versionEsperada':3},admin);assert summary()['egresos']==58
    assert req('/api/caja/reportes/'+r['id']+'/exportar',token=cash)==export;passed('F12')
    payload=movement('Egreso',2,'concurrent-'+suffix)
    with ThreadPoolExecutor(max_workers=6) as pool:rs=list(pool.map(lambda _:req(root+'/movimientos',payload,cash),range(6)))
    assert len({x['id'] for x in rs})==1 and summary()['cantidadMovimientos']==4;passed('INT-01')
    v=req('/api/caja/ventas',{'referencia':'PEND-'+suffix,'total':15},cash);assert v['estado']=='Pendiente' and summary()['ingresos']==200
    payload=movement('Ingreso',15,'pending-'+suffix);payload['ventaId']=v['id'];req(root+'/movimientos',payload,cash);assert summary()['ingresos']==215
    second=req(root+'/cierres',{'claveOperacion':'close2-'+suffix,'contado':195},cash)
    assert second['version']==2 and detail()['cierres'][0]==baseline;passed('INT-02')
    Path(os.getenv('CAJA_TEST_RESULT','/tmp/caja_test_resultados.json')).write_text(json.dumps({'pruebas_aprobadas':results,'turno_de_prueba':t['codigo'],'datos':'Pruebas locales, no observaciones de tesis'},ensure_ascii=False,indent=2))
    print('Aprobadas:',len(results))
if __name__=='__main__':main()
