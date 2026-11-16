#!/usr/bin/env python3
"""Create small synthetic Opus/reference fixtures outside Assets (requires ffmpeg)."""
import argparse
from pathlib import Path
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    output = parser.parse_args().output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    for name, signal, duration in [
        ('mono', '0.2*sin(2*PI*440*t)', '0.125'),
        ('stereo', '0.2*sin(2*PI*440*t)|0.2*sin(2*PI*880*t)', '0.1'),
    ]:
        opus = output / (name + '.opus')
        subprocess.run(['ffmpeg', '-v', 'error', '-nostdin', '-f', 'lavfi', '-i',
                        f'aevalsrc={signal}:s=48000:d={duration}', '-c:a', 'libopus', '-b:a', '128000',
                        '-vbr', 'off', '-ar', '48000', '-fflags', '+bitexact', '-flags:a', '+bitexact', str(opus)], check=True)
        subprocess.run(['ffmpeg', '-v', 'error', '-nostdin', '-i', str(opus), '-f', 'f32le',
                        '-c:a', 'pcm_f32le', str(output / (name + '.f32'))], check=True)
    print(output)


if __name__ == '__main__':
    main()
