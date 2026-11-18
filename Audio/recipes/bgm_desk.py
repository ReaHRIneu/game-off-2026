"""bgm_desk — 메인·바탕화면·의뢰 선택용. 72 BPM 4/4, D 단조, 16마디(약 53.3초).

Dm9 -> Bbmaj7 -> Gm9 -> A7sus4 -> A7 (4마디 x 4회, 3·4회째 변형).
음표는 이 게임용으로 새로 쓴 것이며 기존 곡을 옮기지 않았다.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools" / "audio"))
import synth as sy  # noqa: E402

REPORT: dict = {}

# 마디별 패드 보이싱(MIDI), 베이스 근음
PAD = [
    [53, 57, 60, 64],      # Dm9  : F A C E
    [53, 57, 58, 62],      # Bbmaj7: F A Bb D
    [55, 58, 62, 65],      # Gm7(9 느낌): G Bb D F
    [57, 62, 64, 67],      # A7sus4 (3박부터 A7: 아래 PAD_A7)
]
PAD_A7 = [57, 61, 64, 67]
BASS = [38, 34, 31, 33]    # D2 Bb1 G1 A1

# 멜로디: 마디 안 (박, 길이, MIDI)
PH_A = [
    [(0, 1.5, 69), (1.5, 0.5, 72), (2, 2, 67)],
    [(0, 3, 65), (3, 1, 67)],
    [(0, 1, 70), (1, 1, 69), (2, 2, 65)],
    [(0, 2, 64), (2, 1, 61), (3, 1, 64)],
]
PH_B = [
    [(0, 1, 72), (1, 1, 69), (2, 2, 64)],
    [(0, 2, 65), (2, 2, 62)],
    [(0, 1.5, 67), (1.5, 0.5, 70), (2, 2, 69)],
    [(0, 2, 64), (2, 1, 61), (3, 1, 57)],
]
PH_C = [  # 3회째: 한 옥타브 위에서 시작하는 변형
    [(0, 1, 76), (1, 0.5, 74), (1.5, 0.5, 72), (2, 2, 69)],
    [(0, 3, 70), (3, 1, 69)],
    [(0, 1, 74), (1, 1, 72), (2, 2, 70)],
    [(0, 2, 69), (2, 1, 64), (3, 1, 61)],
]
PH_D = [  # 4회째: 마지막 마디는 길게 열어 두어 다음 반복(Dm9)으로 이어진다
    [(0, 1.5, 72), (1.5, 0.5, 69), (2, 2, 74)],
    [(0, 2, 70), (2, 2, 65)],
    [(0, 1, 67), (1, 1, 70), (2, 2, 72)],
    [(0, 4, 64)],
]
PHRASES = [PH_A, PH_B, PH_C, PH_D]


def build():
    S = sy.Song("bgm_desk", bpm=72, bars=16, seed=11)
    pad, bass, ep, hat, bell = (S.track(n) for n in ("pad", "bass", "ep", "hat", "bell"))

    for bar in range(16):
        ph, b = divmod(bar, 4)
        t0 = bar * 4
        # 패드: 톱니파 3개 겹침(살짝 어긋난 음정, 좌우 벌림), 느린 어택
        voicing = PAD[b]
        if b == 3:
            for m in voicing:
                S.note(pad, t0, 2, m, "saw", env=(0.5, 0.2, 0.9, 0.8), detune=(-9, 0, 9), spread=0.7, gain=0.30)
            for m in PAD_A7:
                S.note(pad, t0 + 2, 2, m, "saw", env=(0.35, 0.2, 0.9, 0.9), detune=(-9, 0, 9), spread=0.7, gain=0.30)
        else:
            for m in voicing:
                S.note(pad, t0, 4, m, "saw", env=(0.5, 0.2, 0.9, 0.9), detune=(-9, 0, 9), spread=0.7, gain=0.30)
        if ph >= 2:   # 3·4회째: 높은 옥타브 사인 한 겹 추가
            top = voicing[-1] + 12
            S.note(pad, t0, 4, top, "sine", env=(0.8, 0.2, 0.8, 1.0), gain=0.18, pan=-0.3 if bar % 2 else 0.3)
        # 서브 베이스: 온음표 사인
        S.note(bass, t0, 4, BASS[b], "sine", env=(0.06, 0.3, 0.85, 0.35), gain=0.9)
        S.note(bass, t0, 4, BASS[b] + 12, "tri", env=(0.06, 0.3, 0.5, 0.35), gain=0.12)
        # 하이햇 8분: 1·2회째는 뒷박만 약하게, 3·4회째는 8분 전부
        for i in range(8):
            beat = t0 + i * 0.5
            if ph < 2 and i % 2 == 0:
                continue
            vel = 0.34 if i % 2 else 0.20
            S.hat(hat, beat, vel * (0.85 if ph < 2 else 1.0), length=0.03, pan=-0.25 if i % 4 < 2 else 0.25)
        # 멜로디(전자 피아노풍)
        for (bt, dur, m) in PHRASES[ph][b]:
            S.note(ep, t0 + bt, dur, m, "epiano", env=(0.008, 1.2, 0.35, 0.5), gain=0.42, pan=-0.1)
            if ph >= 2 and dur >= 1:   # 3·4회째: 같은 음을 높은 옥타브에 약한 종소리로 얹음
                S.note(bell, t0 + bt + 0.75, 1.0, m + 12, "sine", env=(0.004, 0.6, 0.0, 0.4), gain=0.10, pan=0.4)

    # 효과: 패드는 저역 필터+큰 잔향, 멜로디는 딜레이(3/8박 ≈ 점4분 느낌)+잔향
    spb_s = 60.0 / S.bpm
    pad.buf = sy.reverb(sy.lowpass(pad.buf, 1100, poles=2), mix=0.45, fb=0.84)
    ep.buf = sy.reverb(sy.delay(ep.buf, 0.75 * spb_s, 0.45, 0.45), mix=0.35, fb=0.82)
    bell.buf = sy.reverb(sy.delay(bell.buf, 0.75 * spb_s, 0.5, 0.5), mix=0.5, fb=0.84)
    hat.buf = sy.highpass(hat.buf, 3500)
    bass.buf = sy.lowpass(bass.buf, 400)

    mix = sy.sum_tracks(S, {"hat": 0.8})
    paths, rep = sy.export_loop(S, mix)
    REPORT.update(rep)
    return paths
