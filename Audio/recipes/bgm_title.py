"""bgm_title — 메인(방 일러스트)·크레딧용. 66 BPM 4/4, F 단조, 16마디(약 58.2초).

Fm9 -> Dbmaj7 -> Bbm9 -> Cm7 (4마디 x 4회, 회차마다 멜로디 변형, 마지막 회는 열어 둠).
비 오는 밤 혼자 깨어 있는 방. 전자 피아노풍 모티프 + 넓은 패드 + 부드러운 노이즈 셰이커 4분 + 삼각파 2분음표 베이스.
저역 규칙: 베이스·패드 기본음 모두 110Hz 이상, 디튠은 220Hz 이상 음만.
음표는 이 게임용으로 새로 쓴 것이며 기존 곡을 옮기지 않았다.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools" / "audio"))
import synth as sy  # noqa: E402

REPORT: dict = {}

PAD = [
    [53, 56, 60, 63, 67],     # Fm9  : F Ab C Eb G
    [49, 53, 56, 60],         # Dbmaj7: Db F Ab C
    [53, 58, 61, 65, 68],     # Bbm   : F Bb Db F Ab (9도 C 는 멜로디가 담당)
    [51, 55, 58, 63],         # Cm7  : Eb G Bb Eb
]
BASS = [53, 49, 46, 48]       # F3 Db3 Bb2 C3 (2분음표, 기본음 116Hz 이상)

PH_A = [
    [(0, 2, 72), (2, 1, 68), (3, 1, 67)],
    [(0, 3, 65), (3, 1, 67)],
    [(0, 1, 65), (1, 1, 68), (2, 2, 70)],
    [(0, 2, 67), (2, 2, 63)],
]
PH_B = [
    [(0, 1, 75), (1, 1, 72), (2, 2, 68)],
    [(0, 2, 72), (2, 1, 68), (3, 1, 65)],
    [(0, 1.5, 73), (1.5, 0.5, 72), (2, 2, 68)],
    [(0, 1, 70), (1, 1, 67), (2, 2, 70)],
]
PH_C = [
    [(0, 1, 68), (1, 1, 72), (2, 1, 75), (3, 1, 72)],
    [(0, 2, 77), (2, 2, 72)],
    [(0, 1, 73), (1, 1, 70), (2, 1, 68), (3, 1, 65)],
    [(0, 2, 67), (2, 1, 70), (3, 1, 72)],
]
PH_D = [   # 마지막 회: 마지막 마디를 길게 열어 두어 첫 마디(Fm9)로 이어진다
    [(0, 2, 72), (2, 2, 68)],
    [(0, 1.5, 65), (1.5, 0.5, 68), (2, 2, 72)],
    [(0, 2, 70), (2, 2, 65)],
    [(0, 4, 67)],
]
PHRASES = [PH_A, PH_B, PH_C, PH_D]


def build():
    S = sy.Song("bgm_title", bpm=66, bars=16, seed=44)
    pad, bass, ep, shk, bell = (S.track(n) for n in ("pad", "bass", "ep", "shk", "bell"))

    for bar in range(16):
        ph, b = divmod(bar, 4)
        t0 = bar * 4
        S.pad_chord(pad, t0, 4, PAD[b], env=(0.7, 0.2, 0.9, 1.0), gain=0.26)
        # 삼각파 베이스 2분음표(1·3박), 기본음 110Hz 이상
        for beat in (0, 2):
            S.note(bass, t0 + beat, 2, BASS[b], "tri", env=(0.01, 0.5, 0.3, 0.15),
                   gain=0.34 if beat == 0 else 0.24)
        # 노이즈 셰이커 4분(부드럽게, 마지막 두 회는 조금 더 또렷)
        for i in range(4):
            S.shaker(shk, t0 + i, (0.30 if ph < 2 else 0.38) * (1.0 if i % 2 == 0 else 0.7),
                     pan=-0.3 if i % 2 else 0.3)
        # 전자 피아노 모티프
        for (bt, dur, m) in PHRASES[ph][b]:
            S.note(ep, t0 + bt, dur, m, "epiano", env=(0.008, 1.3, 0.35, 0.6), gain=0.40, pan=-0.1)
            if ph == 2 and dur >= 1:
                S.note(bell, t0 + bt + 0.75, 1.0, m + 12, "bell", env=(0.004, 0.6, 0.0, 0.4), gain=0.07, pan=0.4)

    spb_s = 60.0 / S.bpm
    pad.buf = sy.reverb(sy.lowpass(pad.buf, 1000, poles=2), mix=0.50, fb=0.85)
    ep.buf = sy.reverb(sy.delay(ep.buf, 0.75 * spb_s, 0.45, 0.45), mix=0.35, fb=0.82)
    bell.buf = sy.reverb(sy.delay(bell.buf, 0.75 * spb_s, 0.5, 0.5), mix=0.5, fb=0.84)
    shk.buf = sy.reverb(sy.highpass(shk.buf, 3000), mix=0.2, fb=0.7)
    bass.buf = sy.lowpass(bass.buf, 700)

    mix = sy.sum_tracks(S, {"shk": 0.7})
    paths, rep = sy.export_loop(S, mix)
    REPORT.update(rep)
    return paths
