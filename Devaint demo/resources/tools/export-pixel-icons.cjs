// Export the generated component atlas, never a UI screenshot. Originals remain untouched.
const fs = require('fs');
const path = require('path');
const sharp = require(process.env.SHARP_MODULE_PATH || 'sharp');
const root = path.resolve(__dirname, '..');
const source = path.join(root,'ui/temporary-gui/source/paper-gui/단순도트_문양20종_원본.png');
const catalog = process.argv[2], output = process.argv[3];
if (!catalog || !output) throw new Error('Pass the main WeaponGrowthMvp.json and an empty review output directory.');
const out = path.resolve(output);
if (out === root || out.startsWith(root+path.sep) || (fs.existsSync(out) && fs.readdirSync(out).length)) throw new Error('Choose a new/empty output outside resources.');
fs.mkdirSync(out, {recursive:true});
const data = JSON.parse(fs.readFileSync(catalog,'utf8'));
(async()=>{
 const {width,height}=await sharp(source).metadata();
 // Empty gutters in the approved simplified atlas; isolate complete symbols before resizing.
 const columns=[0,.215,.41,.6,.795,1], rows=[0,.295,.522,.748,1];
 for (const [column,color] of ['red','yellow','green','blue','purple'].entries()) {
  const parts=data.parts.filter(p=>p.color===color);
  for (let row=0;row<4;row++) {
   // Grid isolation and nearest-neighbour export to native 32px game resolution.
   const left=Math.round(columns[column]*width),top=Math.round(rows[row]*height);
   await sharp(source).extract({left,top,width:Math.round(columns[column+1]*width)-left,height:Math.round(rows[row+1]*height)-top})
    .resize(32,32,{fit:'contain',kernel:'nearest',background:'#00000000'})
    .png().toFile(path.join(out,'ink-'+parts[row].id+'.png'));
  }
 }
 console.log('Exported 20 individual 32px ink sprites to '+out);
})();
