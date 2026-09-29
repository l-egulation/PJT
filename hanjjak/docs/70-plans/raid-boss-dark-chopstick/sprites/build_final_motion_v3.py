from __future__ import annotations

import json
from collections import deque
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent
OUTPUT = ROOT / "10-fallen-hero-final-motion-v3-hd"
SOURCE_GRID = (4, 3)
FRAMES_PER_ACTION = 12
CELL = 768
ACTIONS = ("idle", "dash-slash", "spin-slash", "blade-rain")
SOURCES = {
    action: ROOT / "sources" / f"final-motion-v3-{action}-4x3.png"
    for action in ACTIONS
}
DURATIONS_MS = {
    "idle": (100,) * 12,
    "dash-slash": (140, 100, 80, 50, 40, 40, 50, 60, 80, 90, 100, 70),
    "spin-slash": (140, 100, 80, 70, 60, 50, 50, 60, 70, 90, 110, 140),
    "blade-rain": (160, 130, 120, 110, 100, 100, 110, 140, 180, 200, 220, 170),
}
TRANSPARENT_RGB = (1, 2, 3)


def remove_checkerboard(image: Image.Image) -> Image.Image:
    """Remove neutral checker pixels connected to the sheet boundary.

    Restricting removal to the exterior component keeps neutral black/gray detail
    inside the boss opaque while still following background through open gaps.
    """
    rgb = image.convert("RGB")
    width, height = rgb.size
    pixels = list(rgb.get_flattened_data())
    candidates = bytearray(width * height)
    for index, (red, green, blue) in enumerate(pixels):
        neutral_delta = max(red, green, blue) - min(red, green, blue)
        if max(red, green, blue) >= 38 and neutral_delta <= 58:
            candidates[index] = 1

    exterior = bytearray(width * height)
    queue: deque[int] = deque()

    def seed(index: int) -> None:
        if candidates[index] and not exterior[index]:
            exterior[index] = 1
            queue.append(index)

    for x in range(width):
        seed(x)
        seed((height - 1) * width + x)
    for y in range(height):
        seed(y * width)
        seed(y * width + width - 1)

    while queue:
        index = queue.popleft()
        x = index % width
        y = index // width
        for offset_y in (-1, 0, 1):
            next_y = y + offset_y
            if next_y < 0 or next_y >= height:
                continue
            row = next_y * width
            for offset_x in (-1, 0, 1):
                next_x = x + offset_x
                if next_x < 0 or next_x >= width or (offset_x == 0 and offset_y == 0):
                    continue
                neighbor = row + next_x
                if candidates[neighbor] and not exterior[neighbor]:
                    exterior[neighbor] = 1
                    queue.append(neighbor)

    cleaned = [
        (0, 0, 0, 0) if exterior[index] else (*color, 255)
        for index, color in enumerate(pixels)
    ]
    output = Image.new("RGBA", rgb.size)
    output.putdata(cleaned)
    return output


def split_source(path: Path) -> list[Image.Image]:
    with Image.open(path) as source:
        rgba = remove_checkerboard(source)
    columns, rows = SOURCE_GRID
    frames = []
    for row in range(rows):
        top = round(row * rgba.height / rows)
        bottom = round((row + 1) * rgba.height / rows)
        for column in range(columns):
            left = round(column * rgba.width / columns)
            right = round((column + 1) * rgba.width / columns)
            frames.append(
                rgba.crop((left, top, right, bottom)).resize(
                    (CELL, CELL), Image.Resampling.LANCZOS
                )
            )
    assert len(frames) == FRAMES_PER_ACTION
    return frames


def make_gif_frames(frames: list[Image.Image]) -> tuple[list[Image.Image], int]:
    sample = Image.new("RGB", (CELL * 4, CELL * 3), TRANSPARENT_RGB)
    keyed_frames = []
    for index, frame in enumerate(frames):
        keyed = Image.new("RGB", frame.size, TRANSPARENT_RGB)
        mask = frame.getchannel("A").point(lambda value: 255 if value >= 96 else 0)
        keyed.paste(frame.convert("RGB"), mask=mask)
        keyed_frames.append(keyed)
        sample.paste(keyed, ((index % 4) * CELL, (index // 4) * CELL))
    palette = sample.quantize(colors=255, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    transparent_index = Image.new("RGB", (1, 1), TRANSPARENT_RGB).quantize(
        palette=palette, dither=Image.Dither.NONE
    ).getpixel((0, 0))
    return (
        [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in keyed_frames],
        transparent_index,
    )


OUTPUT.mkdir(parents=True, exist_ok=True)
atlas = Image.new("RGBA", (CELL * FRAMES_PER_ACTION, CELL * len(ACTIONS)), (0, 0, 0, 0))
animations = {}
frames_by_action = {}
for action_row, action in enumerate(ACTIONS):
    frames = split_source(SOURCES[action])
    frames_by_action[action] = frames
    entries = []
    for frame_index, (frame, duration) in enumerate(
        zip(frames, DURATIONS_MS[action], strict=True)
    ):
        x = frame_index * CELL
        y = action_row * CELL
        atlas.alpha_composite(frame, (x, y))
        entries.append(
            {
                "frame": frame_index,
                "x": x,
                "y": y,
                "width": CELL,
                "height": CELL,
                "durationMs": duration,
            }
        )
    gif_frames, transparent_index = make_gif_frames(frames)
    gif_frames[0].save(
        OUTPUT / f"{action}-hd.gif",
        save_all=True,
        append_images=gif_frames[1:],
        duration=DURATIONS_MS[action],
        loop=0,
        disposal=2,
        transparency=transparent_index,
        optimize=False,
    )
    animations[action] = entries

sheet_name = "fallen-hero-final-motion-sprite-sheet-12x4-768-v3.png"
atlas.save(OUTPUT / sheet_name, optimize=True)
atlas.resize((3072, 1024), Image.Resampling.LANCZOS).save(
    OUTPUT / "fallen-hero-final-motion-sprite-sheet-12x4-v3-preview.png",
    optimize=True,
)

# Five-times edge inspection board. Each quadrant shows a 256px boundary crop
# enlarged to 1280px over split green/magenta chroma backgrounds.
qa_samples = (("idle", 0), ("dash-slash", 6), ("spin-slash", 6), ("blade-rain", 8))
qa_board = Image.new("RGB", (2560, 2560), (0, 0, 0))
for sample_index, (action, frame_index) in enumerate(qa_samples):
    frame = frames_by_action[action][frame_index]
    crop = frame.crop((64, 32, 320, 288)).resize((1280, 1280), Image.Resampling.NEAREST)
    chroma = Image.new("RGBA", crop.size, (24, 185, 70, 255))
    chroma.paste((210, 24, 150, 255), (640, 0, 1280, 1280))
    chroma.alpha_composite(crop)
    qa_board.paste(chroma.convert("RGB"), ((sample_index % 2) * 1280, (sample_index // 2) * 1280))
qa_board.save(OUTPUT / "background-removal-edge-check-5x.png", optimize=True)
metadata = {
    "id": "fallen-hero-final-motion-v3-hd",
    "nameKo": "타락한 젓가락 군주 HD",
    "authority": "candidate",
    "image": sheet_name,
    "size": {"width": atlas.width, "height": atlas.height},
    "grid": {"columns": FRAMES_PER_ACTION, "rows": len(ACTIONS)},
    "cell": {"width": CELL, "height": CELL},
    "framesPerAction": FRAMES_PER_ACTION,
    "order": list(ACTIONS),
    "actionDurationMs": {action: sum(DURATIONS_MS[action]) for action in ACTIONS},
    "resampling": "LANCZOS from independently generated 4x3 HD action sources",
    "animations": animations,
}
(OUTPUT / "fallen-hero-final-motion-sprite-sheet-12x4-768-v3.json").write_text(
    json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
)

with Image.open(OUTPUT / sheet_name) as sheet:
    assert sheet.size == (9216, 3072)
    assert sheet.mode == "RGBA"
    assert sheet.getchannel("A").getextrema()[0] == 0
with Image.open(OUTPUT / "background-removal-edge-check-5x.png") as qa_image:
    assert qa_image.size == (2560, 2560)
for action in ACTIONS:
    with Image.open(OUTPUT / f"{action}-hd.gif") as animation:
        assert animation.size == (CELL, CELL)
        assert animation.n_frames == FRAMES_PER_ACTION
        total = 0
        for index in range(animation.n_frames):
            animation.seek(index)
            total += animation.info["duration"]
        assert total == sum(DURATIONS_MS[action])

print("Validated v3 HD: 9216x3072 RGBA atlas, 48 poses, four 768px GIFs.")
