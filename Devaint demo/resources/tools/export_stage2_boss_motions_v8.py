"""2스테이지 보스(로즈·블루·흑화 블루) 공격·컷씬 동작 확정본(적용본 v8)을 리소스 그룹으로 내보낸다.

입력은 저장소 밖 작업본이다(04_테스트/보스 모션 최종 확인/적용본-v8 = 보정본 v8 + R8 0.6배 + 발 보정 + 투사체 지움).
각 동작은 runtime/<id>/ 에 frames/NN.png(384x256, 피벗 192,232, 원래 Aether 칸 번호) 와 motion.json(재생 순서·ms·게임 큐 맞춤표)을 둔다.

    python -I tools/export_stage2_boss_motions_v8.py <적용본-v8 폴더>
"""
import hashlib
import json
import shutil
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
GROUP = ROOT / 'monsters/stage-02/boss-motions-v8-20261108'
src = Path(sys.argv[1])

# (묶음, 작업 폴더) -> (id, 이름, 쓰임)
CLIPS = {
    ('phase1', 'Rose_공중3연사'): ('rose-triple-shot', 'Rose 공중 3연사', '1페이즈 LayerShots: 벽 끝 공중에서 화살 3발'),
    ('phase1', 'Blue_잉크터뜨리기'): ('blue-ink-burst', 'Blue 잉크 터뜨리기', '1페이즈 MachineAndPlatforms: 장치 발판에서 잉크 폭발'),
    ('phase2', 'Rose_조준사격'): ('rose-stun-shot', 'Rose 조준 사격', '2페이즈 StunAndInk: 기절 화살'),
    ('phase2', 'Blue_잉크던지기'): ('blue-ink-throw', 'Blue 잉크 던지기', '2페이즈 StunAndInk: 잉크 공'),
    ('phase2', 'Rose_하늘일제사격'): ('rose-sky-volley', 'Rose 하늘 일제 사격', '2페이즈 Swap: 로즈 쪽 화살 울타리'),
    ('phase2', 'Blue_회오리시전'): ('blue-ink-whirl', 'Blue 회오리 시전', '2페이즈 Swap: 블루 쪽 잉크 회오리'),
    ('phase2', 'Rose_기절'): ('rose-stun', 'Rose 기절', '2페이즈 교환 뒤 기절 2초(ExchangeStun)'),
    ('phase2', 'Blue_기절'): ('blue-stun', 'Blue 기절', '2페이즈 교환 뒤 기절 2초(ExchangeStun)'),
    ('phase3', '1_유도화살'): ('p3-homing', '흑화 Blue 유도 화살', '3페이즈 Homing'),
    ('phase3', '2_튕기는화살5연사'): ('p3-bounce', '흑화 Blue 튕기는 화살 5연사', '3페이즈 Bounce'),
    ('phase3', '3_순차폭발화살비'): ('p3-rain', '흑화 Blue 순차 폭발 화살비', '3페이즈 ArrowRain'),
    ('phase3', '4_확장광역기'): ('p3-fan', '흑화 Blue 확장 광역기', '3페이즈 ExpandingFan'),
    ('phase3', '5_떠오르기-떠있기-착지'): ('p3-hover', '흑화 Blue 떠오르기·떠 있기·착지', '3페이즈 쉬는 동안 이동(FusedMove)'),
    ('phase3', '6_기절2초'): ('p3-stun', '흑화 Blue 기절 2초', '3페이즈 기절'),
    ('phase3', '7_처치쓰러짐'): ('p3-defeat', '흑화 Blue 처치 쓰러짐', '3페이즈 처치'),
    ('phase3', '8_흡수뒤변신-등장_컷씬'): ('p3-transform', '흡수 뒤 변신(흑화 Blue 등장)', '흡수 컷씬 transformStart~idleStart(1.8초)'),
    ('cutscene', '입장_R1_Rose-거만한웃음'): ('R1-rose-smug-laugh', '입장 R1 로즈 거만한 웃음', '입장 컷씬 3.6~5.0초, 반복'),
    ('cutscene', '입장_R2_Rose-석궁겨누고멈춤'): ('R2-rose-boast-aim', '입장 R2 로즈 석궁 겨누고 멈춤', '입장 컷씬 7.4초 시작, 11.1초까지 마지막 칸 유지'),
    ('cutscene', '입장_R3_Rose-겨눔풀고대기'): ('R3-rose-aim-release', '입장 R3 로즈 겨눔 풀기', '입장 컷씬 11.1~12.0초'),
    ('cutscene', '입장_R6_Rose-허둥지둥장전'): ('R6-rose-panic-reload', '입장 R6 로즈 허둥지둥 장전', '입장 컷씬 17.8초~퇴장'),
    ('cutscene', '입장_B3_Blue-환각전장시전'): ('B3-blue-scene-cast', '입장 B3 블루 환각 전장 시전', '입장 컷씬 castStart(16.4초)부터, 기존 blue-cast 자리'),
    ('cutscene', '전환_R4_Rose-놀람손가락질'): ('R4-rose-surprise-point', '전환 R4 로즈 놀람 손가락질', '1→2 전환 컷씬 2.6~5.0초'),
    ('cutscene', '전환_R7_Rose-전투자세진입'): ('R7-rose-battle-ready', '전환 R7 로즈 전투 자세 진입', '1→2 전환 컷씬 12.05~14.4초'),
    ('cutscene', '흡수_R5_Rose-뒷걸음질'): ('R5-rose-backstep', '흡수 R5 로즈 뒷걸음질', '흡수 컷씬(블루 먼저) 2.6~5.85초'),
    ('cutscene', '흡수_R8_Rose-쓰러진채겨눔'): ('R8-rose-downed-aim', '흡수 R8 로즈 쓰러진 채 겨눔', '흡수 컷씬(로즈 먼저) 4.62초~붙잡힐 때까지 마지막 칸'),
    ('cutscene', '흡수_B2_Blue-흡수직후정적'): ('B2-blue-after-absorb', '흡수 B2 블루 흡수 직후 정적', '흡수 컷씬 absorbEnd~roarStart'),
}
# 게임 큐 맞춤표: [[게임 시각(패턴 시작부터 초), 동작 시각(초)], ...]. 사이는 선형으로 늘이고 줄인다.
# 칸별 그림에서 찾은 핵심 칸(팔 뻗기·내리찍기·밀기 등)을 Unity Patterns.cs 큐 시각에 맞춘 값(HTML 시연에서 확인).
CUE_KEYS = {
    'p3-homing': ([[0, 0], [.4, .23], [1.0, .63], [2.365, 1.995]], '0.23 손 모음, 0.63 팔 뻗음 -> HomingTell 1.0초 발사'),
    'p3-bounce': ([[0, 0], [.45, .31], [2.65, 2.89], [3.235, 3.475]], '0.31 팔 뻗음, 2.89 거둠 -> 발사 0.5~2.5초 동안 뻗고 있기'),
    'p3-rain': ([[0, 0], [.5, .36], [2.38, 1.59], [2.45, 1.66], [3.0, 2.22]], '0.36 두 팔 듦, 1.66 주먹 내리찍기 -> 전조 0.5초에 들고 첫 기둥 폭발 2.45초에 내리찍기'),
    'p3-fan': ([[0, 0], [.5, .27], [2.6, 1.65], [3.0, 1.97], [4.3, 2.62], [4.85, 3.16]], '0.27 팔 벌림, 1.97 두 손 밂 -> 퍼지는 2.5초까지 벌리고 부채꼴 발사 3.0~4.3초에 밀기'),
    'rose-stun-shot': ([[0, 0], [.5, .42], [2.6, 2.52]], '0.42 발사 -> StunArrowTell 0.5초'),
    'blue-ink-throw': ([[0, 0], [.5, .42], [2.6, 2.52]], '0.42 던짐 -> 0.5초 잉크 공 발사'),
    'rose-sky-volley': ([[0, 0], [.15, .42], [.5, 1.05], [.9, 1.47], [1.95, 2.52]], '0.42 겨눔, 1.05 쏘기 끝 -> Swap 순간이동 직후 쏘아 0.55초부터 떨어지게'),
    'blue-ink-whirl': ([[0, 0], [.5, .49], [1.4, 1.26], [1.7, 1.54], [2.68, 2.52]], '0.49~1.26 두 손 밀기 -> 회오리 퍼짐 0.5~1.4초'),
    'blue-ink-burst': ([[0, 0], [1.0, 1.26], [2.26, 2.52]], '1.26 내리찍기(몸이 1px 내려앉는 칸) -> MachineBurstTell 1.0초'),
}

man_path = Path(sys.argv[2]) if len(sys.argv) > 2 else None
if GROUP.exists():
    shutil.rmtree(GROUP / 'runtime', ignore_errors=True)
(GROUP / 'runtime').mkdir(parents=True, exist_ok=True)
foot = json.loads((src / 'foot-fix.json').read_text(encoding='utf-8'))
proj = json.loads((src / 'projectiles-removed.json').read_text(encoding='utf-8'))
index = []
for (grp, folder), (mid, name, use) in CLIPS.items():
    clip = src / grp / folder
    timeline = json.loads((clip / 'timeline.json').read_text(encoding='utf-8'))
    out = GROUP / 'runtime' / mid
    (out / 'frames').mkdir(parents=True, exist_ok=True)
    files = sorted(p.name for p in clip.glob('*.png'))
    # Unity 용 시트: frames 순서대로 8열 격자(왼쪽 위부터), 칸 384x256. 텍스처 상한 4096 안(최대 3072x1280).
    cols = min(8, len(files))
    sheet = Image.new('RGBA', (cols * 384, -(-len(files) // cols) * 256), (0, 0, 0, 0))
    for i, f in enumerate(files):
        im = Image.open(clip / f)
        assert im.size == (384, 256) and im.mode == 'RGBA', (mid, f, im.size, im.mode)
        shutil.copy2(clip / f, out / 'frames' / f)
        sheet.paste(im, (i % cols * 384, i // cols * 256))
    sheet.save(out / 'sheet.png', optimize=True)
    motion = {'format': 'stage2-boss-motion-v8', 'id': mid, 'name': name, 'use': use, 'canvas': [384, 256], 'pivot': [192, 232],
              'frames': files, 'sheet': {'file': 'sheet.png', 'columns': cols, 'cell': [384, 256], 'order': 'frames, left-to-right then top-to-bottom'},
              'sequence': [t['file'] for t in timeline], 'durationsMs': [t['ms'] for t in timeline],
              'totalMs': sum(t['ms'] for t in timeline), 'loop': mid == 'R1-rose-smug-laugh',
              'workFolder': f'{grp}/{folder}',
              'footShiftPx': foot.get(f'{grp}/{folder}', {}), 'projectilePixelsRemoved': proj.get(f'{grp}/{folder}', {}),
              'status': 'user-approved-not-unity-integrated'}
    if mid in CUE_KEYS:
        motion['gameCueKeys'], motion['gameCueNote'] = CUE_KEYS[mid]
    (out / 'motion.json').write_text(json.dumps(motion, ensure_ascii=False, indent=1) + '\n', encoding='utf-8', newline='\n')
    index.append({'id': mid, 'name': name, 'use': use, 'frames': len(files), 'steps': len(timeline), 'totalMs': motion['totalMs'],
                  'gameCueKeys': mid in CUE_KEYS})
(GROUP / 'motions.json').write_text(json.dumps({'format': 'stage2-boss-motions-v8-index', 'motions': index}, ensure_ascii=False, indent=1) + '\n',
                                    encoding='utf-8', newline='\n')
print(len(index), 'motions', sum(m['frames'] for m in index), 'frames')
