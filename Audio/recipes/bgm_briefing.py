"""bgm_briefing — 브리핑·종료 대화용. 84 BPM 4/4, A♭ 중심 sus 화음, 8마디(약 22.9초). 가장 성긴 곡.

Absus2 -> Fm11 -> Dbmaj9 -> Ebsus4 (4마디 x 2회). 짧은 벨 아르페지오(8분음표 성기게) + 얇은 패드, 퍼커션 없음.
글을 읽는 화면이라 음 수를 최소로 하고 두 번째 회는 위치만 바꾼다.
저역 규칙: 패드 기본음 138Hz 이상, 벨은 A4 이상.
음표는 이 게임용으로 새로 쓴 것이며 기존 곡을 옮기지 않았다.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools" / "audio"))
import synth as sy  # noqa: E402

REPORT: dict = {}

PAD = [
    [56, 58, 63],             # Absus2: Ab Bb Eb
    [53, 60, 63, 70],         # Fm11 : F C Eb Bb
    [49, 56, 60, 63],         # Dbmaj9: Db Ab C Eb
    [51, 56, 58, 63],         # Ebsus4: Eb Ab Bb Eb
]
BELL = [
    [68, 70, 75, 80],         # Absus2
    [72, 75, 77, 80],         # Fm11
    [73, 75, 77, 80],         # Dbmaj9
    [68, 70, 75, 80],         # Ebsus4
]
# 8분 격자(0~7) -> 코드 음 번호. 마디마다 4개 음만.
PAT_1 = [{0: 0, 3: 2, 5: 1, 6: 3}, {1: 1, 2: 0, 5: 3, 7: 2}, {0: 2, 3: 0, 4: 1, 6: 3}, {1: 3, 3: 1, 5: 0, 6: 2}]
PAT_2 = [{0: 1, 2: 3, 4: 2, 7: 0}, {0: 3, 3: 2, 5: 0, 6: 1}, {1: 0, 2: 2, 5: 1, 7: 3}, {0: 0, 4: 3, 5: 1, 7: 2}]


def build():
    S = sy.Song("bgm_briefing", bpm=84, bars=8, seed=55)
    pad, bell = (S.track(n) for n in ("pad", "bell"))

    for bar in range(8):
        cyc, b = divmod(bar, 4)
        t0 = bar * 4
        # 얇은 패드: 톱니 3겹(220Hz 이상 음만 디튠)
        S.pad_chord(pad, t0, 4, PAD[b], env=(0.6, 0.2, 0.9, 1.0), gain=0.17, spread=0.5)
        pat = (PAT_1 if cyc == 0 else PAT_2)[b]
        for step, idx in pat.items():
            m = BELL[b][idx]
            S.note(bell, t0 + step * 0.5, 1.2, m, "bell", env=(0.004, 0.9, 0.0, 0.5),
                   gain=0.30 if step in (0, 4) else 0.22, pan=-0.35 if idx % 2 else 0.35)

    spb_s = 60.0 / S.bpm
    pad.buf = sy.reverb(sy.lowpass(pad.buf, 1500, poles=2), mix=0.45, fb=0.84)
    bell.buf = sy.reverb(sy.delay(bell.buf, 0.75 * spb_s, 0.40, 0.40), mix=0.40, fb=0.82)

    mix = sy.sum_tracks(S)
    paths, rep = sy.export_loop(S, mix)
    REPORT.update(rep)
    return paths
