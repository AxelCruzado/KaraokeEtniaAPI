"""Create identifiable local fixtures for either supported engine, never remote databases."""
import os,re,subprocess

def products(category,names,minimum=2):
 assert re.fullmatch(r'[A-Z0-9a-z-]+',category) and all(re.fullmatch(r'[A-Z0-9a-z-]+',n) for n in names)
 base=['docker','--host=unix:///var/run/docker.sock','exec','-i']
 if os.getenv('CAJA_TEST_PROVIDER')=='PostgreSQL':
  database=os.getenv('CAJA_TEST_DATABASE','etnia_pg');assert re.fullmatch(r'etnia_[a-z0-9_]+',database)
  cmd=base+['etnia-dev-postgres','psql','-U','etnia','-d',database,'-At','-v','ON_ERROR_STOP=1']
  category_id=int(subprocess.check_output(cmd,input=f'''INSERT INTO "Categorias" ("Nombre","ImagenUrl","Estado") VALUES ('{category}','','Activo') RETURNING "Id";'''.encode()).decode().splitlines()[0])
  ids=[]
  for name in names:
   sql=f'''INSERT INTO "Productos" ("Nombre","ImagenUrl","Stock","Precio","TicketsPorUnidad","CategoriaId","StockMinimo") VALUES ('{name}','',10,5,0,{category_id},{minimum}) RETURNING "Id";'''
   ids.append(int(subprocess.check_output(cmd,input=sql.encode()).decode().splitlines()[0]))
  return ids
 assert os.getenv('CAJA_TEST_PROVIDER','MySQL')=='MySQL'
 cmd=base+['etnia-dev-mysql','mysql','-uroot','-N','etnia_dev']
 sql=f"INSERT INTO Categorias(Nombre,ImagenUrl,Estado) VALUES('{category}','','Activo');SET @cat=LAST_INSERT_ID();"
 for name in names:sql+=f"INSERT INTO Productos(Nombre,ImagenUrl,Stock,Precio,TicketsPorUnidad,CategoriaId,StockMinimo) VALUES('{name}','',10,5,0,@cat,{minimum});SELECT LAST_INSERT_ID();"
 return list(map(int,subprocess.check_output(cmd,input=sql.encode()).split()))
