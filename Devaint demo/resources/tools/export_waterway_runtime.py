"""Export the approved terrain bank into one atlas and a Unity lookup adapter."""
from pathlib import Path
from PIL import Image
import json,argparse,math
import numpy as np

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--reference',type=Path);ap.add_argument('--adapter-output',type=Path,required=True);args=ap.parse_args()
 root=Path(__file__).resolve().parents[1];a=root/'environments/waterway-tile-variants'
 source=args.reference/'source/stage1-seed-17.json' if args.reference else a/'source/ground-reference-v3.json'
 src=json.loads(source.read_text(encoding='utf-8'));g=np.array(src['ground'],bool).reshape(src['rows'],src['cols']);m=json.loads((a/'source/terrain-placement-v3.json').read_text(encoding='utf-8'));ids=[]
 for v,n in m['runs']:ids.extend([v]*n)
 cells=[Image.open(f).convert('RGBA') for f in sorted((a/'runtime/context-v3').glob('*.png'))];patterns={};pad=np.pad(g,2,constant_values=True)
 for y in range(g.shape[0]):
  for x in range(g.shape[1]):
   if not g[y,x]:continue
   bits=sum(int(v)<<i for i,v in enumerate(pad[y:y+5,x:x+5].flat));patterns.setdefault(bits,set()).add(ids[y*g.shape[1]+x])
 starts=[];widths=[]
 for name in ['plain-a','plain-b','plain-c','moss-light-a','moss-light-b','moss-wide-2','moss-wide-3']:
  im=Image.open(a/'runtime'/(name+'.png'));starts.append(len(cells));widths.append(im.width//32)
  for row in range(2):
   for col in range(im.width//32):cells.append(im.crop((col*32,row*32,col*32+32,row*32+32)))
 atlas=Image.new('RGBA',(1024,math.ceil(len(cells)/32)*32))
 for i,im in enumerate(cells):atlas.paste(im,(i%32*32,i//32*32))
 atlas.save(a/'runtime/WaterwayRuntimeAtlas.png')
 code='// Generated from approved waterway logical cells. X right; context rows top to bottom.\nnamespace Deviant.Chapter2 { internal static class WaterwayTileData {\n'
 code+='internal static readonly int[] Patterns = new[]{'+','.join(map(str,patterns))+'};\ninternal static readonly int[][] Choices = new int[][]{'+','.join('new[]{'+','.join(map(str,sorted(v)))+'}' for v in patterns.values())+'};\n'
 code+='internal static readonly int[] MotifStarts = new[]{'+','.join(map(str,starts))+'};\ninternal static readonly int[] MotifWidths = new[]{'+','.join(map(str,widths))+'};\n}}\n'
 args.adapter_output.write_text(code,encoding='utf-8');print('Atlas cells',len(cells),'geometry contexts',len(patterns))

if __name__=='__main__':main()
