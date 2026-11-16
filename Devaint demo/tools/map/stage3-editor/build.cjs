'use strict';
const fs=require('node:fs'),path=require('node:path'),root=__dirname;
const core=require('./map-core.js');
let html=fs.readFileSync(path.join(root,'index.html'),'utf8');
html=html.replace('<link rel="stylesheet" href="editor.css">',()=>'<style>\n'+fs.readFileSync(path.join(root,'editor.css'),'utf8')+'\n</style>');
for(const file of ['map-core.js','editor.js'])html=html.replace('<script src="'+file+'"></script>',()=>'<script>\n'+fs.readFileSync(path.join(root,file),'utf8').replace(/<\/script/gi,'<\\/script')+'\n</script>');
fs.writeFileSync(path.join(root,'3챕터_맵편집기.html'),html);
fs.writeFileSync(path.join(root,'기본맵.json'),JSON.stringify(core.seed(),null,2)+'\n');
console.log('Standalone HTML and default map written. No network or server required.');
