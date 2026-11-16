"""Publish controller presentation tuning without modifying combat/input rules."""
import argparse
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "data/combat/presentation/gamepad_feedback.json"
TARGET = ROOT / "unity/Assets/Deviant/Combat/Resources/GamepadFeedback.json"


PULSES = ("basic", "special", "dash", "jump", "hit", "heavyHit", "hurt", "parry")
INTEGER_FIELDS = {"triggerStart": (0, 9), "readyStrength": (0, 8), "chargingStrength": (0, 8),
                  "kickStrength": (1, 8), "kickFrequency": (1, 255)}
TIME_FIELDS = ("kickDuration", "maximumEventAge")
CURVES = ("decay", "hold", "rise")
MAX_SEGMENTS = 16
MAX_PATTERN_DURATION = 2.0


def number(value):
    return type(value) is int or type(value) is float and math.isfinite(value)


def validate(data):
    expected = {"schemaVersion", *PULSES, *INTEGER_FIELDS, *TIME_FIELDS}
    if not isinstance(data, dict) or set(data) != expected:
        raise ValueError("Expected the complete controller feedback profile with no unknown fields")
    if type(data["schemaVersion"]) is not int or data["schemaVersion"] not in (1, 2):
        raise ValueError("Unsupported controller feedback schema")
    for name in PULSES:
        pulse = data[name]
        if not isinstance(pulse, dict):
            raise ValueError(f"Invalid {name} pattern")
        if "segments" in pulse:
            segments = pulse["segments"]
            if data["schemaVersion"] != 2 or set(pulse) != {"segments"} or not isinstance(segments, list) or not 1 <= len(segments) <= MAX_SEGMENTS:
                raise ValueError(f"Expected 1..{MAX_SEGMENTS} segments in schema 2 {name}")
            total = 0
            for index, segment in enumerate(segments):
                if not isinstance(segment, dict) or set(segment) != {"low", "high", "duration", "curve"} or segment["curve"] not in CURVES:
                    raise ValueError(f"Invalid {name}.segments[{index}] curve or fields")
                validate_levels(segment, f"{name}.segments[{index}]")
                total += segment["duration"]
            if total > MAX_PATTERN_DURATION + 1e-9:
                raise ValueError(f"{name} pattern must not exceed {MAX_PATTERN_DURATION:g} seconds")
        else:
            if set(pulse) != {"low", "high", "duration"}:
                raise ValueError(f"Expected low, high and duration in {name}")
            validate_levels(pulse, name)
    for key, (minimum, maximum) in INTEGER_FIELDS.items():
        if type(data[key]) is not int or not minimum <= data[key] <= maximum:
            raise ValueError(f"Invalid {key}")
    for key in TIME_FIELDS:
        if not number(data[key]) or not 0 < data[key] <= 1:
            raise ValueError(f"Invalid {key}")
    return data


def validate_levels(segment, label):
    for key in ("low", "high", "duration"):
        value = segment[key]
        if not number(value) or not 0 <= value <= 1 or key == "duration" and value == 0:
            raise ValueError(f"Invalid {label}.{key}")


def serialize(data):
    validate(data)
    return json.dumps(data, ensure_ascii=False, indent=2, allow_nan=False) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    data = json.loads(SOURCE.read_text(encoding="utf-8"))
    text = serialize(data)
    if args.check:
        if not TARGET.exists() or TARGET.read_text(encoding="utf-8") != text:
            raise SystemExit("Controller feedback catalog is stale; run tools/combat/export_gamepad_feedback.py")
    else:
        TARGET.parent.mkdir(parents=True, exist_ok=True)
        TARGET.write_text(text, encoding="utf-8")
    print("Controller feedback catalog matches source" if args.check else TARGET)


if __name__ == "__main__":
    main()
