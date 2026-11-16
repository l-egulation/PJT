"""Generate the Unity playback settings from the main repository's music data."""

import argparse
import json
import math
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "data/audio/background-music.json"
TARGET = ROOT / "unity/Assets/Deviant/Game/Resources/BackgroundMusic.json"
SELECTED = ROOT / "unity/Assets/Deviant/Audio/Resources/SelectedAudioClips.asset"
CUES = {
    "title", "village", "chapter1_field", "chapter1_boss", "chapter2_field", "chapter2_boss",
    "opening_road", "opening_road_quiet", "opening_erase", "maintenance",
}
TIMINGS = ("fadeOutSeconds", "fadeInSeconds", "loopCrossfadeSeconds", "scheduleLeadSeconds", "fadeStepLimitSeconds")
TRACK_KEYS = {
    "id", "resource", "group", "placeholder", "volume", "startSeconds", "endSeconds", "loop",
    "loopStartSeconds", "loopCrossfadeSeconds", "fadeInSeconds", "fadeOutSeconds",
}


def selected_ids():
    return set(re.findall(r"^  - id: (\S+)$", SELECTED.read_text(encoding="utf-8"), re.MULTILINE))


def render():
    data = json.loads(SOURCE.read_text(encoding="utf-8"))
    tracks = data["tracks"]
    if data["schema"] != 2 or len(tracks) != len(CUES) or {track["id"] for track in tracks} != CUES:
        raise ValueError("Expected the title, village, chapter field/boss, opening, quiet road and maintenance cues")
    if set(data) != {"schema", "tracks", *TIMINGS}:
        raise ValueError("Unexpected or missing library keys")

    for key in TIMINGS:
        if not math.isfinite(data[key]) or data[key] <= 0:
            raise ValueError(f"Invalid timing: {key}")

    selected = selected_ids()

    by_id = {track["id"]: track for track in tracks}
    for track in tracks:
        if not set(track) <= TRACK_KEYS:
            raise ValueError(f"Unexpected keys for {track['id']}")
        start, end, volume = (track[key] for key in ("startSeconds", "endSeconds", "volume"))
        loop_start = track.get("loopStartSeconds", start)
        overlap = track.get("loopCrossfadeSeconds", data["loopCrossfadeSeconds"])
        fades = [track.get(key, 0) for key in ("fadeInSeconds", "fadeOutSeconds")]
        resource = track["resource"]
        if (
            not resource.startswith(("BackgroundMusic/", "SelectedAudio/"))
            or resource.startswith("SelectedAudio/") and resource[len("SelectedAudio/"):] not in selected
            or track["loop"] is not True
            or not all(map(math.isfinite, (start, loop_start, end, overlap, volume, *fades)))
            or not 0 < volume <= 1
            or not 0 <= start <= loop_start < end - 2 * overlap
            or overlap <= 0
            or any(fade < 0 for fade in fades)
            or not isinstance(track.get("group", ""), str)
            or not isinstance(track.get("placeholder", ""), str)
        ):
            raise ValueError(f"Invalid playback settings for {track['id']}")

    # The tutorial and hub must share a playback window so entering the road never restarts the music.
    village = {key: value for key, value in by_id["village"].items() if key != "id"}
    for cue in ("opening_road", "opening_road_quiet"):
        if {key: value for key, value in by_id[cue].items() if key != "id"} != village:
            raise ValueError(f"{cue} must use the same playback as the village")
    for track in tracks:
        if track["id"] != "chapter1_boss" and track["loopStartSeconds"] <= track["startSeconds"]:
            raise ValueError(f"{track['id']} must repeat after its intro")

    return json.dumps(data, ensure_ascii=False, indent=2) + "\n"


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    settings = render()
    if parser.parse_args().check:
        if TARGET.read_text(encoding="utf-8") != settings:
            raise SystemExit("Regenerate the Unity background music settings")
    else:
        TARGET.write_text(settings, encoding="utf-8", newline="\n")
    print(f"PASS: {len(CUES)} background music cues, playback windows, and fade policy")
