"""sting_gameover — 게임오버(접속 끊김)용 효과곡. 약 2.5초(반복 없음).

사각 리드 하강 반음 2음(E5 -> D#5) -> 짧은 글리치 노이즈 -> 사인 비프가 갑자기 끊김.
음표는 이 게임용으로 새로 쓴 것이다.
"""
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools" / "audio"))
import synth as sy  # noqa: E402

REPORT: dict = {}


def build():
    S = sy.Song("sting_gameover", bpm=120, bars=1, seed=88, oneshot_s=2.5)   # 1박 = 0.5초
    lead, noise, beep = (S.track(n) for n in ("lead", "noise", "beep"))
    # 하강 반음 2음(0~0.9초)
    S.note(lead, 0.0, 0.5, 76, "square", duty=0.5, env=(0.006, 0.1, 0.8, 0.08), gain=0.30)
    S.note(lead, 0.6, 0.9, 75, "square", duty=0.5, env=(0.006, 0.2, 0.6, 0.30), gain=0.30)
    # 글리치 노이즈: 25ms 조각을 랜덤으로 켜고 끄며, 샘플홀드(비트크러시)로 거칠게(1.25~1.85초)
    rng = np.random.default_rng(5)
    for k in range(24):
        if rng.random() < 0.55:
            n = int(0.025 * sy.SR)
            x = rng.uniform(-1, 1, n)
            hold = 6 + int(rng.integers(0, 14))
            x = np.repeat(x[::hold], hold)[:n]
            x *= np.minimum(np.arange(n) / 40.0, 1.0) * np.minimum((n - np.arange(n)) / 40.0, 1.0)
            noise.put(int((1.25 + k * 0.025) * sy.SR), x, pan=float(rng.uniform(-0.6, 0.6)), gain=0.5)
    # 마지막 사인 비프(880Hz = A5): 1.9초에 켜서 2.25초에 갑자기 끊김(5ms 릴리즈)
    S.note(beep, 3.8, 0.7, 81, "sine", env=(0.003, 0.05, 1.0, 0.005), gain=0.30)
    lead.buf = sy.reverb(sy.lowpass(lead.buf, 4000), mix=0.15, fb=0.75)
    noise.buf = sy.highpass(noise.buf, 400)
    mix = sy.sum_tracks(S)
    paths, rep = sy.export_sting(S, mix)
    REPORT.update(rep)
    return paths
