from __future__ import annotations

import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / "sources" / "10-fallen-hero-final-motion-keyposes-10x4-v2.png"
OUTPUT = ROOT / "10-fallen-hero-final-motion-v2"
CELL = 256
PREVIEW_CELL = 512
COLUMNS = 10
ROWS = 4
ACTIONS = ("idle", "dash-slash", "spin-slash", "blade-rain")
DURATIONS_MS = {
    "idle": (120,) * 10,
    "dash-slash": (150, 90, 60, 50, 50, 60, 70, 90, 110, 170),
    "spin-slash": (160, 100, 80, 70, 60, 60, 70, 90, 120, 190),
    "blade-rain": (180, 130, 110, 100, 90, 120, 160, 220, 250, 240),
}
TRANSPARENT_RGB = (1, 2, 3)


def remove_checkerboard(image: Image.Image) -> Image.Image:
    """Key out the neutral light checker while preserving colored VFX highlights."""
    rgba = image.convert("RGBA")
    cleaned = []
    for red, green, blue, _alpha in rgba.get_flattened_data():
        neutral_delta = max(red, green, blue) - min(red, green, blue)
        if max(red, green, blue) >= 40 and neutral_delta <= 42:
            cleaned.append((0, 0, 0, 0))
            continue
        cleaned.append((red, green, blue, 255))
    rgba.putdata(cleaned)
    return rgba


def split_cells(source: Image.Image) -> list[list[Image.Image]]:
    source = remove_checkerboard(source)
    rows: list[list[Image.Image]] = []
    for row in range(ROWS):
        top = round(row * source.height / ROWS)
        bottom = round((row + 1) * source.height / ROWS)
        cells = []
        for column in range(COLUMNS):
            left = round(column * source.width / COLUMNS)
            right = round((column + 1) * source.width / COLUMNS)
            cells.append(
                source.crop((left, top, right, bottom)).resize(
                    (CELL, CELL), Image.Resampling.NEAREST
                )
            )
        rows.append(cells)
    return rows


def gif_frames(frames: list[Image.Image]) -> tuple[list[Image.Image], int]:
    sample = Image.new("RGB", (CELL * COLUMNS, CELL), TRANSPARENT_RGB)
    keyed = []
    for index, frame in enumerate(frames):
        rgb = Image.new("RGB", frame.size, TRANSPARENT_RGB)
        mask = frame.getchannel("A").point(lambda value: 255 if value >= 128 else 0)
        rgb.paste(frame.convert("RGB"), mask=mask)
        keyed.append(rgb)
        sample.paste(rgb, (index * CELL, 0))

    palette = sample.quantize(colors=255, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    probe = Image.new("RGB", (1, 1), TRANSPARENT_RGB).quantize(
        palette=palette, dither=Image.Dither.NONE
    )
    transparent_index = probe.getpixel((0, 0))
    rendered = [
        frame.quantize(palette=palette, dither=Image.Dither.NONE).resize(
            (PREVIEW_CELL, PREVIEW_CELL), Image.Resampling.NEAREST
        )
        for frame in keyed
    ]
    return rendered, transparent_index


OUTPUT.mkdir(parents=True, exist_ok=True)
with Image.open(SOURCE) as source_image:
    frames_by_action = split_cells(source_image)

sheet = Image.new("RGBA", (CELL * COLUMNS, CELL * ROWS), (0, 0, 0, 0))
animations: dict[str, list[dict[str, int]]] = {}
for row, action in enumerate(ACTIONS):
    frames = frames_by_action[row]
    durations = DURATIONS_MS[action]
    entries = []
    for column, (frame, duration) in enumerate(zip(frames, durations, strict=True)):
        x = column * CELL
        y = row * CELL
        sheet.alpha_composite(frame, (x, y))
        entries.append(
            {
                "frame": column,
                "x": x,
                "y": y,
                "width": CELL,
                "height": CELL,
                "durationMs": duration,
            }
        )
    rendered, transparent_index = gif_frames(frames)
    rendered[0].save(
        OUTPUT / f"{action}.gif",
        save_all=True,
        append_images=rendered[1:],
        duration=durations,
        loop=0,
        disposal=2,
        transparency=transparent_index,
        optimize=False,
    )
    animations[action] = entries

sheet_name = "fallen-hero-final-motion-sprite-sheet-10x4-v2.png"
sheet.save(OUTPUT / sheet_name, optimize=True)

metadata = {
    "id": "fallen-hero-final-motion-v2",
    "nameKo": "타락한 젓가락 군주",
    "authority": "candidate",
    "image": sheet_name,
    "grid": {"columns": COLUMNS, "rows": ROWS},
    "cell": {"width": CELL, "height": CELL},
    "previewCell": {"width": PREVIEW_CELL, "height": PREVIEW_CELL},
    "framesPerAction": COLUMNS,
    "order": list(ACTIONS),
    "actionDurationMs": {
        action: sum(DURATIONS_MS[action]) for action in ACTIONS
    },
    "animations": animations,
    "notes": "Ten distinct generated key poses per action. Variable frame timing follows the supplied dash/spin references; no synthetic crossfades.",
}
(OUTPUT / "fallen-hero-final-motion-sprite-sheet-10x4-v2.json").write_text(
    json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
)

with Image.open(OUTPUT / sheet_name) as validation_sheet:
    assert validation_sheet.size == (2560, 1024)
    assert validation_sheet.mode == "RGBA"
    assert validation_sheet.getextrema()[3][0] == 0
for action in ACTIONS:
    with Image.open(OUTPUT / f"{action}.gif") as animation:
        assert animation.size == (PREVIEW_CELL, PREVIEW_CELL)
        assert animation.n_frames == COLUMNS
        actual_duration = 0
        for frame_index in range(animation.n_frames):
            animation.seek(frame_index)
            actual_duration += animation.info["duration"]
        assert actual_duration == sum(DURATIONS_MS[action])

print("Validated v2: one 2560x1024 RGBA sheet and four transparent 10-frame GIFs.")
