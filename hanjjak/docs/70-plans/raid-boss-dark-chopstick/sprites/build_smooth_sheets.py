from __future__ import annotations

import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent
LOGICAL_CELL = 256
CELL = 512
KEY_COLUMNS = 8
FRAMES_PER_ACTION = 64
FRAME_DURATION_AVERAGE_MS = 5000 / FRAMES_PER_ACTION
ATLAS_COLUMNS = 16
ACTIONS = ("idle", "attack-1", "attack-2", "attack-3")
TRANSPARENT_RGB = (1, 2, 3)


def gif_durations() -> tuple[int, ...]:
    # GIF timing is stored in 10 ms units. Distribute twelve 70 ms frames evenly
    # among fifty-two 80 ms frames: 12*70 + 52*80 = exactly 5000 ms.
    durations: list[int] = []
    for index in range(FRAMES_PER_ACTION):
        short_before = index * 12 // FRAMES_PER_ACTION
        short_after = (index + 1) * 12 // FRAMES_PER_ACTION
        durations.append(70 if short_after > short_before else 80)
    assert sum(durations) == 5000
    return tuple(durations)


GIF_DURATIONS_MS = gif_durations()

BOSSES = (
    ("01-abyss-ember-sovereign", "심연 불씨 군주", "sources/01-abyss-ember-sovereign-keyframes-8x4.png"),
    ("07-blighted-bamboo-lich", "병든 대나무 리치", "sources/07-blighted-bamboo-lich-keyframes-8x4.png"),
    ("10-fallen-hero-final-ascension", "타락한 영웅 최종각성", "sources/10-fallen-hero-final-ascension-keyframes-8x4.png"),
)


def remove_checker_background(image: Image.Image) -> Image.Image:
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            red, green, blue, alpha = pixels[x, y]
            neutral_delta = max(red, green, blue) - min(red, green, blue)
            if alpha and neutral_delta <= 28 and min(red, green, blue) >= 145:
                pixels[x, y] = (0, 0, 0, 0)
    return rgba


def split_keyframes(source: Image.Image) -> list[list[Image.Image]]:
    source = remove_checker_background(source)
    rows: list[list[Image.Image]] = []
    for row in range(len(ACTIONS)):
        frames: list[Image.Image] = []
        top = round(row * source.height / len(ACTIONS))
        bottom = round((row + 1) * source.height / len(ACTIONS))
        for column in range(KEY_COLUMNS):
            left = round(column * source.width / KEY_COLUMNS)
            right = round((column + 1) * source.width / KEY_COLUMNS)
            frame = source.crop((left, top, right, bottom)).resize(
                (LOGICAL_CELL, LOGICAL_CELL), Image.Resampling.NEAREST
            )
            frames.append(frame)
        rows.append(frames)
    return rows


def tween(first: Image.Image, second: Image.Image, progress: float) -> Image.Image:
    eased = progress * progress * (3.0 - 2.0 * progress)
    mixed = Image.blend(first, second, eased)
    alpha = mixed.getchannel("A").point(
        lambda value: 0 if value < 16 else 255 if value > 239 else value
    )
    mixed.putalpha(alpha)
    return mixed


def expand_frames(keys: list[Image.Image]) -> list[Image.Image]:
    frames: list[Image.Image] = []
    frames_per_segment = FRAMES_PER_ACTION // len(keys)
    for index, first in enumerate(keys):
        second = keys[(index + 1) % len(keys)]
        for step in range(frames_per_segment):
            frames.append(tween(first, second, step / frames_per_segment))
    assert len(frames) == FRAMES_PER_ACTION
    return frames


def make_fixed_palette_gif_frames(frames: list[Image.Image]) -> tuple[list[Image.Image], int]:
    # One shared, non-dithered palette prevents frame-to-frame palette crawl.
    sample = Image.new("RGB", (LOGICAL_CELL * 8, LOGICAL_CELL * 8), TRANSPARENT_RGB)
    keyed_frames: list[Image.Image] = []
    for index, frame in enumerate(frames):
        keyed = Image.new("RGB", frame.size, TRANSPARENT_RGB)
        opaque_mask = frame.getchannel("A").point(lambda value: 255 if value >= 128 else 0)
        keyed.paste(frame.convert("RGB"), mask=opaque_mask)
        keyed_frames.append(keyed)
        sample.paste(keyed, ((index % 8) * LOGICAL_CELL, (index // 8) * LOGICAL_CELL))

    palette = sample.quantize(colors=255, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    transparent_probe = Image.new("RGB", (1, 1), TRANSPARENT_RGB).quantize(
        palette=palette, dither=Image.Dither.NONE
    )
    transparent_index = transparent_probe.getpixel((0, 0))
    gif_frames = [
        keyed.quantize(palette=palette, dither=Image.Dither.NONE).resize(
            (CELL, CELL), Image.Resampling.NEAREST
        )
        for keyed in keyed_frames
    ]
    return gif_frames, transparent_index


def save_boss(slug: str, name_ko: str, source_path: str) -> None:
    output = ROOT / slug
    output.mkdir(parents=True, exist_ok=True)
    with Image.open(ROOT / source_path) as source:
        keys_by_row = split_keyframes(source)

    atlas_rows_per_action = FRAMES_PER_ACTION // ATLAS_COLUMNS
    atlas_rows = atlas_rows_per_action * len(ACTIONS)
    atlas = Image.new(
        "RGBA", (CELL * ATLAS_COLUMNS, CELL * atlas_rows), (0, 0, 0, 0)
    )
    animations: dict[str, list[dict[str, int]]] = {}

    for action_index, action in enumerate(ACTIONS):
        logical_frames = expand_frames(keys_by_row[action_index])
        entries: list[dict[str, int]] = []
        for frame_index, logical_frame in enumerate(logical_frames):
            frame = logical_frame.resize((CELL, CELL), Image.Resampling.NEAREST)
            column = frame_index % ATLAS_COLUMNS
            row = action_index * atlas_rows_per_action + frame_index // ATLAS_COLUMNS
            x = column * CELL
            y = row * CELL
            atlas.alpha_composite(frame, (x, y))
            entries.append(
                {
                    "frame": frame_index,
                    "x": x,
                    "y": y,
                    "width": CELL,
                    "height": CELL,
                    "durationMs": GIF_DURATIONS_MS[frame_index],
                }
            )

        gif_frames, transparent_index = make_fixed_palette_gif_frames(logical_frames)
        gif_frames[0].save(
            output / f"{action}-5s.gif",
            save_all=True,
            append_images=gif_frames[1:],
            duration=GIF_DURATIONS_MS,
            loop=0,
            disposal=2,
            transparency=transparent_index,
            optimize=False,
        )
        animations[action] = entries

    sheet_name = f"{slug}-sprite-sheet-64f-512.png"
    atlas.save(output / sheet_name, optimize=True)
    metadata = {
        "id": slug,
        "nameKo": name_ko,
        "authority": "candidate",
        "image": sheet_name,
        "size": {"width": atlas.width, "height": atlas.height},
        "grid": {"columns": ATLAS_COLUMNS, "rows": atlas_rows},
        "logicalCell": {"width": LOGICAL_CELL, "height": LOGICAL_CELL},
        "cell": {"width": CELL, "height": CELL},
        "order": list(ACTIONS),
        "keyframesPerAction": KEY_COLUMNS,
        "framesPerAction": FRAMES_PER_ACTION,
        "averageFrameDurationMs": FRAME_DURATION_AVERAGE_MS,
        "gifFrameDurationsMs": list(GIF_DURATIONS_MS),
        "actionDurationMs": 5000,
        "playback": {action: "loop" for action in ACTIONS},
        "animations": animations,
        "notes": "Eight generated key poses per action, eased at 256 logical pixels, exact 2x nearest-neighbor scale, and a shared non-dithered GIF palette.",
    }
    (output / f"{slug}-sprite-sheet-64f-512.json").write_text(
        json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


for boss in BOSSES:
    save_boss(*boss)

for slug, _name_ko, _source_path in BOSSES:
    folder = ROOT / slug
    with Image.open(folder / f"{slug}-sprite-sheet-64f-512.png") as sheet:
        assert sheet.size == (8192, 8192) and sheet.mode == "RGBA"
        assert sheet.getextrema()[3][0] == 0
    metadata = json.loads(
        (folder / f"{slug}-sprite-sheet-64f-512.json").read_text(encoding="utf-8")
    )
    assert metadata["actionDurationMs"] == 5000
    assert sum(len(frames) for frames in metadata["animations"].values()) == 256
    for action in ACTIONS:
        with Image.open(folder / f"{action}-5s.gif") as gif:
            assert gif.size == (CELL, CELL) and gif.n_frames == FRAMES_PER_ACTION
            duration = 0
            for frame_index in range(gif.n_frames):
                gif.seek(frame_index)
                duration += gif.info["duration"]
            assert duration == 5000

print("Validated 3 RGBA atlases and 12 GIFs: 64 frames / 5000 ms per action.")
