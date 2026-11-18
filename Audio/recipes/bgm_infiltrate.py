"""bgm_infiltrate — 의뢰 진행(퍼즐)용. 100 BPM 4/4, A 단조, 32마디(76.8초).

Am -> Fmaj7 -> Dm7 -> E7sus4 -> E (4마디 단위). 8마디마다 편곡이 바뀐다:
 1~8마디 베이스·킥·하이햇 / 9~16 아르페지오 추가 / 17~24 짧은 리드 모티프 추가 / 25~32 리드 변형·스네어 유지.
라이저·끝맺음 없이 마지막 마디에서 첫 마디로 그대로 이어진다.
음표는 이 게임용으로 새로 쓴 것이며 기존 곡을 옮기지 않았다.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools" / "audio"))
import synth as sy  # noqa: E402

REPORT: dict = {}

ROOT = [45, 41, 38, 40]                       # A2 F2 D2 E2
CHORD = [
    [57, 60, 64, 69],                         # Am
    [53, 57, 60, 64],                         # Fmaj7
    [50, 53, 57, 60],                         # Dm7
    [52, 57, 59, 64],                         # E7sus4 (3박부터 E: 아래)
]
CHORD_E = [52, 56, 59, 64]
ARP_PAT = [0, 1, 2, 3, 2, 1, 2, 3, 1, 2, 3, 2, 3, 2, 1, 0]   # 16분 16스텝, 코드 음 번호
BASS_PAT = [0, 0, 12, 0, 0, 7, 0, 12]                         # 8분 8스텝, 근음 대비 반음 수

# 리드 모티프(2마디 단위): (박, 길이, MIDI)
LEAD_A = [
    [(0, .5, 72), (.5, .5, 76), (1, 1.5, 74), (3, .5, 72), (3.5, .5, 69)],   # Am
    [(0, 1, 72), (1, .5, 69), (1.5, .5, 65), (2, 2, 67)],                    # Fmaj7
    [(0, .5, 69), (.5, .5, 72), (1, 1.5, 74), (3, 1, 72)],                   # Dm7
    [(0, 1, 71), (1, 1, 69), (2, 1, 68), (3, 1, 71)],                        # E7sus4 -> E
]
LEAD_B = [   # 25~32마디: 음을 줄이고 높이를 바꾼 변형
    [(0, 1, 76), (1, 1, 72), (2, 2, 69)],
    [(0, 1.5, 77), (1.5, .5, 76), (2, 2, 72)],
    [(0, 1, 74), (1, .5, 72), (1.5, .5, 69), (2, 2, 65)],
    [(0, 1, 68), (1, 1, 71), (2, 2, 76)],
]


def build():
    S = sy.Song("bgm_infiltrate", bpm=100, bars=32, seed=22)
    bass, arp, drum, hat, lead = (S.track(n) for n in ("bass", "arp", "drum", "hat", "lead"))

    for bar in range(32):
        sec = bar // 8            # 편곡 구간 0~3
        b = bar % 4
        t0 = bar * 4
        # 펄스 베이스 8분 (사각파 25%)
        for i, off in enumerate(BASS_PAT):
            S.note(bass, t0 + i * 0.5, 0.42, ROOT[b] + off, "square", duty=0.25,
                   env=(0.004, 0.08, 0.7, 0.05), gain=0.55 if off == 0 else 0.45)
        # 킥: 4박 모두 / 스네어: 2·4박(2구간부터) / 하이햇: 뒷박 8분
        for beat in range(4):
            S.kick(drum, t0 + beat, 0.85 if beat % 2 == 0 else 0.6)
        if sec >= 1:
            S.snare(drum, t0 + 1, 0.30)
            S.snare(drum, t0 + 3, 0.30)
        for i in range(8):
            if i % 2 == 1 or sec >= 2:
                S.hat(hat, t0 + i * 0.5, 0.30 if i % 2 else 0.16, length=0.03, pan=0.25 if i % 4 < 2 else -0.25)
        # 16분 아르페지오 (사각파 12.5%) — 2구간부터
        if sec >= 1:
            for s in range(16):
                chord = CHORD_E if (b == 3 and s >= 8) else CHORD[b]
                m = chord[ARP_PAT[s]] + 12
                S.note(arp, t0 + s * 0.25, 0.2, m, "square", duty=0.125,
                       env=(0.003, 0.05, 0.6, 0.04), gain=0.22 if sec != 1 or s % 4 else 0.28, pan=0.0)
        # 리드 — 3구간(원형), 4구간(변형)
        if sec >= 2:
            table = LEAD_A if sec == 2 else LEAD_B
            for (bt, dur, m) in table[b]:
                S.note(lead, t0 + bt, dur * 0.9, m, "square", duty=0.5,
                       env=(0.01, 0.1, 0.65, 0.12), gain=0.30, pan=-0.15)

    spb_s = 60.0 / S.bpm
    bass.buf = sy.lowpass(bass.buf, 900)
    arp.buf = sy.delay(arp.buf, 0.75 * spb_s, 0.40, 0.55)             # 3/16 음표 = 0.75박
    arp.buf = sy.reverb(sy.lowpass(arp.buf, 6000), mix=0.18, fb=0.78)
    lead.buf = sy.reverb(sy.delay(sy.lowpass(lead.buf, 5000), 0.75 * spb_s, 0.35, 0.35), mix=0.22, fb=0.80)
    hat.buf = sy.highpass(hat.buf, 3500)
    drum.buf = sy.reverb(drum.buf, mix=0.08, fb=0.7)

    mix = sy.sum_tracks(S, {"hat": 0.8})
    paths, rep = sy.export_loop(S, mix)
    REPORT.update(rep)
    return paths
