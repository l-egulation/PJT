"""Render layered selected SFX from Gamemaster Pro Sound Collection sources.

The recipe lives in audio/deviant-selected/asset.json under sources[id].recipe.
Each layer is high-passed, scaled so its loudest 5 ms window has the same RMS
(plus gainDb), and either cut from its start second or shifted so that window
lands at peakAt seconds. The sum is
faded, peak-normalized and written as 48 kHz mono 16-bit PCM.

    python tools/mix_selected_sfx.py --pack "<Pro Sound Collection>" --id sfx_player_dash
    python tools/mix_selected_sfx.py --pack "<Pro Sound Collection>" --id sfx_player_dash --check
"""
import argparse, array, hashlib, io, json, math, sys, wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSET = ROOT / "audio/deviant-selected"
SR = 48000


def read_mono(path):
    with wave.open(str(path), "rb") as w:
        if w.getframerate() != SR or w.getsampwidth() != 2:
            raise SystemExit(f"unsupported source format: {path}")
        ch, pcm = w.getnchannels(), array.array("h", w.readframes(w.getnframes()))
    if sys.byteorder != "little": pcm.byteswap()
    return [sum(pcm[i:i + ch]) / ch / 32768 for i in range(0, len(pcm), ch)]


def highpass(x, fc):
    w0 = 2 * math.pi * fc / SR; a = math.sin(w0) / (2 * math.sqrt(.5)); c = math.cos(w0)
    b0, b1, b2, a0, a1, a2 = (1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + a, -2 * c, 1 - a
    out, x1, x2, y1, y2 = [], 0., 0., 0., 0.
    for v in x:
        y = (b0 * v + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0
        out.append(y); x2, x1, y2, y1 = x1, v, y1, y
    return out


def loudest(x, win=int(SR * .005)):
    best, at = -1., 0
    for i in range(0, len(x) - win, win // 2):
        e = sum(v * v for v in x[i:i + win]) / win
        if e > best: best, at = e, i
    return at + win // 2, math.sqrt(best)


def render(pack, recipe):
    n = round(recipe["length"] * SR); mix = [0.] * n
    for layer in recipe["layers"]:
        raw = (pack / layer["source"]).read_bytes()
        if hashlib.sha256(raw).hexdigest() != layer["sourceSha256"]:
            raise SystemExit("source hash mismatch: " + layer["source"])
        x = highpass(read_mono(pack / layer["source"]), recipe["highpassHz"])
        center, rms = loudest(x)
        gain = 10 ** (layer["gainDb"] / 20) / rms
        # "start" cuts the source at a fixed time; otherwise its loudest window lands at peakAt.
        shift = -round(layer["start"] * SR) if "start" in layer else round(layer["peakAt"] * SR) - center
        for i, v in enumerate(x):
            if 0 <= i + shift < n: mix[i + shift] += v * gain
    fi, fo = round(recipe["fadeIn"] * SR), round(recipe["fadeOut"] * SR)
    for i in range(fi): mix[i] *= i / fi
    for i in range(fo): mix[n - 1 - i] *= .5 - .5 * math.cos(math.pi * i / fo)
    gain = 10 ** (recipe["peakDbfs"] / 20) / max(abs(v) for v in mix)
    pcm = array.array("h", (max(-32768, min(32767, round(v * gain * 32768))) for v in mix))
    if sys.byteorder != "little": pcm.byteswap()
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
    return buf.getvalue()


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--pack", required=True, type=Path, help="Gamemaster Audio - Pro Sound Collection folder")
    p.add_argument("--id", required=True, help="sources key in audio/deviant-selected/asset.json")
    p.add_argument("--check", action="store_true", help="re-render and compare with the registered file")
    a = p.parse_args()
    entry = json.loads((ASSET / "asset.json").read_text(encoding="utf-8"))["sources"][a.id]
    data, target = render(a.pack, entry["recipe"]), ASSET / entry["resourceFile"]
    digest = hashlib.sha256(data).hexdigest()
    if a.check:
        ok = digest == entry["sha256"] and target.read_bytes() == data
        print(("match " if ok else "MISMATCH ") + digest); sys.exit(0 if ok else 1)
    target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(data)
    print(f"{target.relative_to(ROOT).as_posix()} {len(data)} {digest}")


if __name__ == "__main__":
    main()
