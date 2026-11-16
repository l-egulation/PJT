from pathlib import Path
from html.parser import HTMLParser
from urllib.parse import urlsplit,unquote
import hashlib,json
from PIL import Image,ImageChops
ROOT=Path(__file__).resolve().parents[1]
records=json.loads((ROOT/"source/pixel-baseline.json").read_text(encoding="utf-8"))
for row in records:
 p=ROOT/row["path"]
 assert hashlib.sha256(p.read_bytes()).hexdigest()==row["sha256"],p
T=ROOT/"stage2-unified-stone-v21"
terrain=list((T/"terrain/runtime/tiles").glob("*.png"));parts=list((T/"runtime/full").glob("*.png"))
assert len(terrain)==376 and len(parts)==11
for p in terrain+parts:assert Image.open(p).size==(32,32),p
for p in parts:
 im=Image.open(p).convert("RGBA")
 assert im.getchannel("A").crop((0,12,32,32)).getbbox() is None,p
B=ROOT/"stage2-room-background-v24";V=ROOT/"stage2-unified-map-review-v22"
manifest=json.loads((V/"manifest.json").read_text(encoding="utf-8"))
assert hashlib.sha256((ROOT/"source/chapter2-layouts.json").read_bytes()).hexdigest()==manifest["sourceSha256"]
for row in manifest["views"]:
 fg=Image.open(B/"runtime/foreground"/row["screen"]).convert("RGBA")
 bg=Image.open(B/"runtime/background"/row["screen"]).convert("RGBA")
 assert fg.size==bg.size==(960,540)
 white=Image.new("RGBA",fg.size,"white");white.alpha_composite(fg)
 assert ImageChops.difference(white.convert("RGB"),Image.open(V/"preview"/row["screen"]).convert("RGB")).getbbox() is None
 bg.alpha_composite(fg)
 assert ImageChops.difference(bg.convert("RGB"),Image.open(B/"preview"/row["screen"]).convert("RGB")).getbbox() is None
M=ROOT/"stage2-sewer-modern-v25";m25=json.loads((M/"manifest.json").read_text(encoding="utf-8"))
assert m25["sourceSha256"]==manifest["sourceSha256"] and len(m25["views"])==8
for row in m25["views"]:
 b25=Image.open(M/"runtime/background"/row["screen"]);f25=Image.open(M/"runtime/foreground"/row["screen"]);p25=Image.open(M/"preview"/row["screen"])
 assert b25.size==f25.size==p25.size==(960,540) and b25.mode=="RGB" and f25.mode=="RGBA",row["screen"]
N=ROOT/"stage2-sewer-modern-v26";m26=json.loads((N/"manifest.json").read_text(encoding="utf-8"))
assert m26["sourceSha256"]==manifest["sourceSha256"] and len(m26["views"])==8
for row in m26["views"]:
 for sub,mode in (("runtime/background","RGB"),("runtime/foreground","RGBA"),("runtime/water-front","RGBA"),("preview","RGB")):
  im=Image.open(N/sub/row["screen"]);assert im.size==(960,540) and im.mode==mode,(sub,row["screen"])
 for b in row["basins"]:assert abs(b["floorTop"]-b["bottom"]-1)<1e-6 and (b["x1"]-b["x0"]) in (2,2.5,3,3.5,4),b
class Links(HTMLParser):
 def handle_starttag(self,tag,attrs):
  for k,v in attrs:
   if k in ("src","href") and v:
    u=urlsplit(v)
    if not u.scheme and not u.netloc and u.path:assert (self.base/unquote(u.path)).is_file(),v
for p in ROOT.rglob("*.html"):
 parser=Links();parser.base=p.parent;parser.feed(p.read_text(encoding="utf-8"))
print(f"PASS: {len(records)} PNG hashes; 376 terrain + 11 one-way modules; 8 foreground layers; v25 8 views x 3 layers; v26 8 views x 4 layers + basins; local HTML links")
