const http=require('http'),fs=require('fs'),path=require('path');
const roots={assets:path.join(__dirname,'assets'),design:path.resolve(__dirname,'../Source'),Source:path.resolve(__dirname,'../Source'),Play:__dirname,tiles:path.resolve(__dirname,'../Tiles'),Tiles:path.resolve(__dirname,'../Tiles'),Preview:path.resolve(__dirname,'../Preview'),unity:path.resolve(__dirname,'../Preview'),monsters:path.join(__dirname,'monsters'),monsterOriginal:path.join(__dirname,'monsterOriginal')};
const types={'.html':'text/html; charset=utf-8','.js':'text/javascript; charset=utf-8','.json':'application/json; charset=utf-8','.css':'text/css; charset=utf-8','.png':'image/png','.md':'text/plain; charset=utf-8'};
http.createServer((req,res)=>{try{
 const u=new URL(req.url,'http://127.0.0.1'),parts=decodeURIComponent(u.pathname).split('/').filter(Boolean);let root=__dirname;
 if(parts.length===1&&['README.md','assets-manifest.json','code-sha256.json','approved.json'].includes(parts[0]))root=path.resolve(__dirname,'..');else if(roots[parts[0]])root=roots[parts.shift()];
 const file=path.resolve(root,...(parts.length?parts:['index.html'])),rel=path.relative(root,file);
 if(rel.startsWith('..')||path.isAbsolute(rel)||!fs.existsSync(file)||!fs.statSync(file).isFile()){res.writeHead(404);return res.end('Not found');}
 res.writeHead(200,{'Content-Type':types[path.extname(file)]||'application/octet-stream','Cache-Control':'no-store'});fs.createReadStream(file).pipe(res);
 }catch(e){res.writeHead(400);res.end('Bad request');}
}).listen(Number(process.env.FOREST_PORT)||8806,'127.0.0.1',()=>console.log('Forest map preview http://127.0.0.1:'+(Number(process.env.FOREST_PORT)||8806)+'/ PID '+process.pid));
