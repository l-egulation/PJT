// Render the approved Canvas source into alpha-preserving, foot-anchored sheets.
// Chrome must already be running with a local debugging port; no remote generator is called.
const fs=require('node:fs'),path=require('node:path');
(async()=>{
 const dest=path.resolve(process.argv[2]),port=process.argv[3]||63403;
 fs.mkdirSync(dest,{recursive:true});
 const tabs=await(await fetch('http://127.0.0.1:'+port+'/json')).json(),tab=tabs.find(t=>t.type==='page'&&t.url.includes('shield-unity-bake/bake.html'));
 if(!tab)throw Error('Open shield-unity-bake/bake.html in the local bake browser.');
 const ws=new WebSocket(tab.webSocketDebuggerUrl),pending=new Map();let id=0;
 ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.id){const p=pending.get(m.id);pending.delete(m.id);m.error?p.reject(m.error):p.resolve(m.result);}};
 await new Promise((resolve,reject)=>{ws.onopen=resolve;ws.onerror=reject;});
 function send(method,params={}){return new Promise((resolve,reject)=>{const n=++id;pending.set(n,{resolve,reject});ws.send(JSON.stringify({id:n,method,params}));});}
 async function ev(expression){const r=await send('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});if(r.exceptionDetails)throw Error(JSON.stringify(r.exceptionDetails));return r.result.value;}
 await ev(`new Promise((resolve,reject)=>{let n=0;const f=()=>{if(document.body.dataset.error)return reject(Error(document.body.dataset.error));if(window.shieldBake)return resolve(true);if(++n>500)return reject(Error('bake load timeout'));setTimeout(f,20);};f();})`);
 const clips=await ev('window.shieldBake.manifest');
 for(const clip of clips){const b64=await ev('window.shieldBake.render('+JSON.stringify(clip.name)+')');fs.writeFileSync(path.join(dest,clip.name+'.png'),Buffer.from(b64,'base64'));}
 fs.writeFileSync(path.join(dest,'clips.json'),JSON.stringify({format:'shield-reviewed-v1',clips},null,2)+'\n');console.log('Baked '+clips.length+' approved shield sheets.');
 await send('Browser.close');ws.close();
})().catch(e=>{console.error(e);process.exit(1);});
