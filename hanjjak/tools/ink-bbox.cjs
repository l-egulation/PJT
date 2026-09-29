// PNG 알파 채널의 실제 그림 범위를 잰다. 레이어가 서로 얼마나 어긋났는지 볼 때 쓴다.
const fs = require("fs"), zlib = require("zlib");

function bbox(file) {
  const buf = fs.readFileSync(file);
  let off = 8, w = 0, h = 0, bitDepth = 0, colorType = 0, idat = [];
  while (off < buf.length) {
    const len = buf.readUInt32BE(off), type = buf.toString("ascii", off + 4, off + 8);
    const data = buf.subarray(off + 8, off + 8 + len);
    if (type === "IHDR") { w = data.readUInt32BE(0); h = data.readUInt32BE(4); bitDepth = data[8]; colorType = data[9]; }
    else if (type === "IDAT") idat.push(data);
    else if (type === "IEND") break;
    off += 12 + len;
  }
  if (bitDepth !== 8 || colorType !== 6) throw new Error(`${file}: unsupported ${bitDepth}/${colorType}`);
  const raw = zlib.inflateSync(Buffer.concat(idat));
  const bpp = 4, stride = w * bpp;
  const cur = Buffer.alloc(stride), prev = Buffer.alloc(stride);
  let p = 0, minX = w, minY = h, maxX = -1, maxY = -1, opaque = 0;
  for (let y = 0; y < h; y++) {
    const filter = raw[p++];
    raw.copy(cur, 0, p, p + stride); p += stride;
    for (let i = 0; i < stride; i++) {
      const a = i >= bpp ? cur[i - bpp] : 0, b = prev[i], c = i >= bpp ? prev[i - bpp] : 0;
      let v = cur[i];
      if (filter === 1) v += a;
      else if (filter === 2) v += b;
      else if (filter === 3) v += (a + b) >> 1;
      else if (filter === 4) { const pp = a + b - c, pa = Math.abs(pp - a), pb = Math.abs(pp - b), pc = Math.abs(pp - c); v += (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c); }
      cur[i] = v & 0xff;
    }
    for (let x = 0; x < w; x++) {
      if (cur[x * 4 + 3] > 8) { opaque++; if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
    }
    cur.copy(prev);
  }
  return maxX < 0 ? { file, empty: true } : { file, w, h, minX, minY, maxX, maxY, width: maxX - minX + 1, height: maxY - minY + 1, opaque };
}

module.exports = { bbox };
if (require.main === module) for (const f of process.argv.slice(2)) { try { console.log(JSON.stringify(bbox(f))); } catch (e) { console.log(JSON.stringify({ file: f, error: e.message })); } }
