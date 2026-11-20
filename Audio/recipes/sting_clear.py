"""sting_clear — 의뢰 성공 결과 화면 진입용 효과곡. 약 3초(반복 없음).

C 장조 아르페지오 상승(C5 E5 G5 C6) -> Cmaj9 패드·벨. 잔향 꼬리로 끝나며 끝 20ms 페이드아웃.
음표는 이 게임용으로 새로 쓴 것이다.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools" / "audio"))
import synth as sy  # noqa: E402

REPORT: dict = {}


def build():
    S = sy.Song("sting_clear", bpm=120, bars=1, seed=77, oneshot_s=3.0)   # 1박 = 0.5초
    bell, pad = (S.track(n) for n in ("bell", "pad"))
    for i, m in enumerate((72, 76, 79, 84)):                               # 0.5박(0.25초) 간격으로 상승
        S.note(bell, i * 0.5, 1.0, m, "bell", env=(0.004, 0.7, 0.0, 0.5), gain=0.32, pan=-0.3 + 0.2 * i)
    # Cmaj9: C4 E4 G4 B4 D5, 2박(1.0초)부터
    S.pad_chord(pad, 2.0, 1.6, [60, 64, 67, 71, 74], env=(0.12, 0.2, 0.9, 0.8), gain=0.18, spread=0.6)
    S.note(bell, 2.0, 1.0, 83, "bell", env=(0.004, 0.9, 0.0, 0.6), gain=0.16, pan=0.3)    # B5
    S.note(bell, 2.25, 1.0, 88, "bell", env=(0.004, 0.9, 0.0, 0.6), gain=0.12, pan=-0.3)  # E6
    bell.buf = sy.reverb(sy.delay(bell.buf, 0.375, 0.35, 0.35), mix=0.45, fb=0.82)
    pad.buf = sy.reverb(sy.lowpass(pad.buf, 2000, poles=2), mix=0.5, fb=0.84)
    mix = sy.sum_tracks(S)
    paths, rep = sy.export_sting(S, mix)
    REPORT.update(rep)
    return paths
