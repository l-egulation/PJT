"""Open the local layout editor. Loopback only; no file-writing web endpoint."""
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.request import urlopen
import argparse
import json
import webbrowser

GROUP=Path(__file__).resolve().parents[1]/'ui/combat-hud-expanded-20261106'
IDENTITY='deviant-combat-ui-layout-editor-v1'

class Handler(SimpleHTTPRequestHandler):
 def do_GET(self):
  if self.path=='/__editor_health__':
   payload=json.dumps(dict(editor=IDENTITY,root=str(GROUP.resolve()))).encode()
   self.send_response(200);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(payload)));self.end_headers();self.wfile.write(payload);return
  super().do_GET()
 def end_headers(self):
  self.send_header('Cache-Control','no-store');super().end_headers()
 def log_message(self,*args):pass

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--port',type=int,default=8768);parser.add_argument('--no-browser',action='store_true');args=parser.parse_args()
 for port in range(args.port,args.port+20):
  url=f'http://127.0.0.1:{port}/preview/index.html'
  try:server=ThreadingHTTPServer(('127.0.0.1',port),partial(Handler,directory=str(GROUP)))
  except OSError:
   try:
    with urlopen(f'http://127.0.0.1:{port}/__editor_health__',timeout=.5) as response:identity=json.load(response)
    if identity.get('editor')==IDENTITY and identity.get('root')==str(GROUP.resolve()):
     print(url,flush=True)
     if not args.no_browser:webbrowser.open(url)
     return
   except Exception:pass
   continue
  print(url,flush=True)
  if not args.no_browser:webbrowser.open(url)
  try:server.serve_forever()
  except KeyboardInterrupt:pass
  finally:server.server_close()
  return
 raise SystemExit('No free local editor port found.')

if __name__=='__main__':main()
