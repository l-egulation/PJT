# 가시 그물 「아래 말고 전체적으로 감싸게」(2026-11-13): 그물이 판정 상자 전체를 덮는 큰 지붕 모양이 되게 휜다.
# 앞으로 늘리기(SX)는 net_scale 과 같다. 높이는 타격 장(16)의 그물 윗선 t(x)·아랫선(손~땅 채찍) b(x)를 재어,
# 아랫선은 그대로 두고 윗선을 목표 지붕 선 T 로 끌어올린다(사이는 비례). 같은 휨을 그물 장 다섯에 모두 써서 펼침·오므림 차이는 남는다.
# 목표 지붕: 판정 위쪽 끝의 95% 높이로 거의 평평하게 가다가 판정 오른쪽 끝에서 땅으로 둥글게 떨어진다. prof(u) = (1 - u^K)^E.
import json, sys
from pathlib import Path
import numpy as np
from scipy import ndimage as nd
sys.path.insert(0, str(Path(__file__).resolve().parent))
import net_scale as ns
K, E, TOP = float(sys.argv[1]) if len(sys.argv) > 1 else 5, float(sys.argv[2]) if len(sys.argv) > 2 else .45, .95
OUT = Path(__file__).resolve().parent.parent
S = json.loads((OUT / '_작업' / 'scale.json').read_text(encoding='utf-8')); SX = S['SX']
a16, net16, grip16 = ns.split('16')
cols = np.nonzero(net16.any(axis=0))[0]; X0c, X1c = cols.min(), cols.max()
W = net16.shape[1]; rows = np.arange(net16.shape[0])[:, None]
t = np.where(net16.any(axis=0), np.where(net16, rows, 10 ** 6).min(axis=0), np.nan)
b = np.where(net16.any(axis=0), np.where(net16, rows, -1).max(axis=0), np.nan)
def fill(v):
    idx = np.arange(W); ok = ~np.isnan(v); return np.interp(idx, idx[ok], v[ok])
t = nd.uniform_filter1d(nd.minimum_filter1d(fill(t), 17), 17); b = nd.uniform_filter1d(nd.maximum_filter1d(fill(b), 17), 17)
HTOP = ns.BOX_TOP * TOP * ns.PPU
def X(x, c): return x if x <= c else c + (x - c) * SX
def make(grip):
    # 1) 앞 시안과 같은 고른 배율(쥔 자리 기준 앞 SX·위 SY)로 옮긴 뒤 2) 꼭짓점(머리 위)에서 멀어질수록 윗선을 지붕 선으로 더 끌어올린다.
    #    꼭짓점 근처(u < .08)는 1)만 써서 촘촘한 그물코가 뭉개지지 않는다. 끌어올림 배율은 4 를 넘기지 않는다(끝 조각이 튀지 않게).
    c, gy = grip; SY = S['SY']; XL, XR = X(X0c, c), X(X1c, c)
    def U(y): return gy - (gy - y) * SY if y < gy else y
    def f(x, y):
        xi = int(min(W - 1, max(0, x))); x2, y2 = X(x, c), U(y)
        u = min(1, max(0, (x2 - XL) / (XR - XL))); s_ = min(1, max(0, (u - .08) / .32)); s_ = s_ * s_ * (3 - 2 * s_)
        tu, bu = U(t[xi]), U(b[xi]); dome = ns.GROUND - HTOP * (1 - u ** K) ** E; T = tu + (min(dome, tu) - tu) * s_
        if y2 >= bu or bu - tu < 3: return x2, y2
        k = min(4, (bu - T) / (bu - tu))
        return x2, max(-120, bu - (bu - y2) * k)
    return f
if __name__ == '__main__':
    print('net cols', X0c, X1c, 'HTOP px', round(HTOP))
