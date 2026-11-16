"""Check portable approved WAVs, hashes, timing metadata, scope and Unity GUIDs."""

import argparse
import array
import hashlib
import json
import math
from pathlib import Path
import re
import sys
import wave


def check(root):
    catalog = json.loads((root / "data/audio/approved-sfx.json").read_text(encoding="utf-8"))
    scope = json.loads((root / "data/audio/production-scope.json").read_text(encoding="utf-8"))
    excluded = {entry["id"] for key in ("external_source", "excluded") for entry in scope[key]}
    entries = catalog["items"]
    ids = {entry["id"] for entry in entries}
    if len(entries) != len(ids) or ids & excluded:
        raise ValueError("Duplicate or excluded event ID in approved catalog")
    audio_root = root / "unity/Assets/Deviant/Audio"
    expected = {(root / entry["path"]).resolve() for entry in entries}
    runtime_root = audio_root / "Resources/ApprovedCombatAudio"
    runtime_catalog = runtime_root / "Catalog.json"
    runtime_entries = []
    if runtime_catalog.exists():
        runtime_entries = json.loads(runtime_catalog.read_text(encoding="utf-8"))["entries"]
        runtime_ids = set()
        for entry in runtime_entries:
            if entry["id"] in runtime_ids:
                raise ValueError("Duplicate runtime audio ID: " + entry["id"])
            runtime_ids.add(entry["id"])
            path = (audio_root / "Resources" / (entry["clip"] + ".wav")).resolve()
            if not path.is_relative_to(runtime_root.resolve()):
                raise ValueError("Runtime audio path escapes its media directory")
            if hashlib.sha256(path.read_bytes()).hexdigest() != entry["sha256"]:
                raise ValueError("Runtime WAV hash mismatch: " + entry["id"])
            with wave.open(str(path), "rb") as wav:
                if wav.getnchannels() not in (1, 2) or (wav.getframerate(), wav.getsampwidth(), wav.getnframes()) != (entry["frequency"], 2, entry["samples"]):
                    raise ValueError("Runtime WAV format mismatch: " + entry["id"])
                pcm = array.array("h", wav.readframes(wav.getnframes()))
            if sys.byteorder != "little":
                pcm.byteswap()
            if not pcm or not 0 < max(abs(v) for v in pcm) / 32768 <= .94 + 1 / 32768:
                raise ValueError("Silent or overflowing runtime audio: " + entry["id"])
            expected.add(path)
        source = (runtime_root / "Source/basic_sword_selected_07_v01.wav").resolve()
        selected = json.loads((root / "data/audio/approved-weapon-project.json").read_text(encoding="utf-8"))
        if hashlib.sha256(source.read_bytes()).hexdigest() != selected["source_hash"]:
            raise ValueError("Runtime editing source hash mismatch")
        expected.add(source)
    max_peak = 10 ** (catalog["delivery"]["peak_limit_dbfs"] / 20) + 1 / 32768
    manifest = root / "docs/assets/manifests/unity-deployment.json"
    selected_media = [item for item in json.loads(manifest.read_text(encoding="utf-8"))["files"]
                      if item["source"]["asset"] == "audio.deviant-selected"]
    for item in selected_media:
        path = (root / item["target"]).resolve()
        if not path.is_relative_to(audio_root.resolve()) or path in expected:
            raise ValueError("Invalid or duplicate selected audio path: " + item["target"])
        data = path.read_bytes()
        if len(data) != item["bytes"] or hashlib.sha256(data).hexdigest() != item["sha256"]:
            raise ValueError("Selected audio differs from its deployment contract: " + item["target"])
        is_music = path.parent == audio_root / "Music"
        with wave.open(str(path), "rb") as wav:
            rate, channels, width = wav.getframerate(), wav.getnchannels(), wav.getsampwidth()
            if width != 2 or not wav.getnframes() or (
                    (channels != 2 or rate not in (44100, 48000)) if is_music else (channels != 1 or rate != 48000)):
                raise ValueError("Selected WAV format mismatch: " + item["target"])
        expected.add(path)
    if expected != {path.resolve() for path in audio_root.rglob("*.wav")}:
        raise ValueError("WAV inventory differs from the approved and selected audio catalogs")
    total_bytes = 0
    for entry in entries:
        path = (root / entry["path"]).resolve()
        if not path.is_relative_to(audio_root.resolve()):
            raise ValueError("Audio path escapes the Unity audio directory")
        if hashlib.sha256(path.read_bytes()).hexdigest() != entry["sha256"]:
            raise ValueError("WAV hash mismatch: " + entry["id"])
        with wave.open(str(path), "rb") as wav:
            if (wav.getnchannels(), wav.getframerate(), wav.getsampwidth(), wav.getnframes()) != (
                1, 48000, 2, entry["samples"]
            ):
                raise ValueError("WAV format or duration mismatch: " + entry["id"])
            samples = array.array("h", wav.readframes(wav.getnframes()))
        if sys.byteorder != "little":
            samples.byteswap()
        peak = max(abs(value) for value in samples) / 32768
        if not 0 < peak <= max_peak:
            raise ValueError("Silence or excessive peak: " + entry["id"])
        if len(samples) != round(entry["production"]["length"] * 48000):
            raise ValueError("Recipe length mismatch: " + entry["id"])
        for event in entry["event_samples"]:
            window = range(max(0, event - 50), min(len(samples), event + 51))
            if max(window, key=lambda index: abs(samples[index])) != event:
                raise ValueError("Attack peak is not aligned: " + entry["id"])
        offset = entry.get("foot_peak_offset_samples")
        if offset is not None and abs(samples[offset]) != max(abs(value) for value in samples):
            raise ValueError("Footstep peak offset mismatch: " + entry["id"])
        total_bytes += path.stat().st_size
    owned_guids = {}
    for asset in [audio_root] + list(audio_root.rglob("*")):
        if asset.suffix == ".meta":
            continue
        metadata = asset.with_name(asset.name + ".meta")
        text = metadata.read_text(encoding="utf-8")
        match = re.search(r"^guid: ([a-f0-9]{32})$", text, re.MULTILINE)
        if match is None or match[1] in owned_guids:
            raise ValueError("Missing or duplicated audio GUID: " + str(metadata))
        owned_guids[match[1]] = metadata.resolve()
        if asset.suffix == ".wav" and "AudioImporter:" not in text:
            raise ValueError("Expected AudioImporter: " + str(metadata))
    for metadata in (root / "unity/Assets").rglob("*.meta"):
        match = re.search(r"^guid: ([a-f0-9]{32})$", metadata.read_text(encoding="utf-8-sig"), re.MULTILINE)
        if match and match[1] in owned_guids and owned_guids[match[1]] != metadata.resolve():
            raise ValueError("Audio GUID collides with another asset: " + str(metadata))
    print(json.dumps(dict(
        approved=len(entries), bytes=total_bytes, excluded=len(excluded),
        footstep_surfaces=sum(entry["id"].startswith("sfx_player_step_") for entry in entries),
        peak_ceiling_dbfs=round(20 * math.log10(max_peak), 3),
        runtime_entries=len(runtime_entries),
        selected_media=len(selected_media),
        state="asset_files_verified_gameplay_checked_separately",
    ), ensure_ascii=False))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    check(args.root.resolve())
