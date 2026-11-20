"""bgm_ch0 — 챕터 0 의뢰(주인공 건물, 튜토리얼)용. 92 BPM 4/4, C 단조, 16마디(약 41.7초).

Cm -> Abmaj7 -> Fm9 -> G7sus4(후반 G7) (4마디 x 4회). 배우는 중이라 덜 급한 조용한 긴장.
1회: 베이스·하이햇 / 2회: +아르페지오·림샷 / 3~4회: +리드 모티프. 킥 없음.
저역 규칙: 펄스 베이스 기본음 87Hz 이상, 패드 138Hz 이상.
음표는 이 게임용으로 새로 쓴 것이며 기존 곡을 옮기지 않았다.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools" / "audio"))
import synth as sy  # noqa: E402

REPORT: dict = {}

ROOT = [48, 44, 41, 43]                       # C3 Ab2 F2 G2 (기본음 87Hz 이상)
PAD = [
    [51, 55, 60, 63],                         # Cm
    [51, 56, 60, 67],                         # Abmaj7 (Ab C Eb G)
    [53, 56, 60, 63],                         # Fm9
    [50, 55, 60, 65],                         # G7sus4
]
ARP = [
    [60, 63, 67, 72],
    [56, 60, 63, 67],
    [60, 63, 67, 68],                         # Fm9 느낌: C Eb G Ab
    [55, 60, 62, 65],                         # G7sus4 (후반 4스텝은 B 로 바뀜)
]
ARP_PAT = [0, 1, 2, 3, 2, 1, 2, 1]
LEAD_C = [
    [(0, 1, 72), (1, .5, 70), (1.5, .5, 67), (2, 2, 63)],
    [(0, 2, 68), (2, 1, 67), (3, 1, 63)],
    [(0, 1, 65), (1, 1, 68), (2, 2, 72)],
    [(0, 2, 74), (2, 1, 72), (3, 1, 71)],
]
LEAD_D = [
    [(0, 1, 75), (1, 1, 72), (2, 2, 67)],
    [(0, 1.5, 72), (1.5, .5, 75), (2, 2, 79)],
    [(0, 2, 77), (2, 1, 75), (3, 1, 72)],
    [(0, 1, 74), (1, 1, 71), (2, 2, 67)],
]


def build():
    S = sy.Song("bgm_ch0", bpm=92, bars=16, seed=66)
    pad, bass, arp, perc, lead = (S.track(n) for n in ("pad", "bass", "arp", "perc", "lead"))

    for bar in range(16):
        cyc, b = divmod(bar, 4)
        t0 = bar * 4
        S.pad_chord(pad, t0, 4, PAD[b], env=(0.5, 0.2, 0.9, 0.9), gain=0.17, spread=0.6)
        # 펄스 베이스 8분(사각 25%), 근음만
        for i in range(8):
            S.note(bass, t0 + i * 0.5, 0.40, ROOT[b], "square", duty=0.25,
                   env=(0.004, 0.08, 0.7, 0.05), gain=0.26 if i % 2 == 0 else 0.19)
        # 하이햇: 뒷박 8분(1회는 약하게)
        for i in range(4):
            S.hat(perc, t0 + i + 0.5, 0.40 if cyc else 0.28, length=0.03, pan=0.25)
        if cyc >= 1:
            for beat in (1, 3):
                S.rim(perc, t0 + beat, 0.40)
            for s in range(8):
                notes = list(ARP[b])
                if b == 3 and s >= 4:
                    notes[1] = 59            # G7sus4 -> G7 (C 대신 B)
                m = notes[ARP_PAT[s] % len(notes)] + 12
                S.note(arp, t0 + s * 0.5, 0.4, m, "square", duty=0.125,
                       env=(0.003, 0.06, 0.55, 0.05), gain=0.17 if s % 2 == 0 else 0.12,
                       pan=-0.25 if s % 2 else 0.25)
        if cyc >= 2:
            for (bt, dur, m) in (LEAD_C if cyc == 2 else LEAD_D)[b]:
                S.note(lead, t0 + bt, dur * 0.92, m, "square", duty=0.5,
                       env=(0.012, 0.1, 0.6, 0.15), gain=0.20, pan=-0.1)

    spb_s = 60.0 / S.bpm
    pad.buf = sy.reverb(sy.lowpass(pad.buf, 1300, poles=2), mix=0.35, fb=0.82)
    bass.buf = sy.lowpass(bass.buf, 1000)
    arp.buf = sy.reverb(sy.delay(sy.lowpass(arp.buf, 5000), 0.75 * spb_s, 0.35, 0.40), mix=0.20, fb=0.78)
    lead.buf = sy.reverb(sy.delay(sy.lowpass(lead.buf, 3500), 0.75 * spb_s, 0.30, 0.30), mix=0.25, fb=0.80)
    perc.buf = sy.highpass(perc.buf, 1200)

    mix = sy.sum_tracks(S, {"perc": 0.8})
    paths, rep = sy.export_loop(S, mix)
    REPORT.update(rep)
    return paths
