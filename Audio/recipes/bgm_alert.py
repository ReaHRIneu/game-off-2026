"""bgm_alert — 추적도 위험·카운트다운용. 128 BPM 4/4, E 프리지안, 16마디(30.0초).

Em(2마디) -> F(2) -> Em(2) -> D(2) 를 2번 반복. 2번째 바퀴는 경보 리드를 더 자주 울린다.
bgm_ch1(100 BPM)와 나중에 겹쳐 바꿀 수 있도록 모든 변화는 마디 경계에서만 일어난다.
음표는 이 게임용으로 새로 쓴 것이며 기존 곡을 옮기지 않았다.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools" / "audio"))
import synth as sy  # noqa: E402

REPORT: dict = {}

ROOT = [52, 53, 52, 50]                       # E3 F3 E3 D3 (2마디씩, 기본음 147Hz 이상)
ALARM = [76, 77, 76, 74]                      # 경보음 기준음(E5 F5 E5 D5), 반음 위와 번갈아
# 경보 리드 켜는 마디(2마디 단위 인덱스 0~7): 1바퀴는 홀수 단위, 2바퀴는 전부
LEAD_ON = {0: False, 1: True, 2: False, 3: True, 4: True, 5: True, 6: True, 7: True}


def build():
    S = sy.Song("bgm_alert", bpm=128, bars=16, seed=33)
    bass, drum, hat, lead = (S.track(n) for n in ("bass", "drum", "hat", "lead"))

    for bar in range(16):
        unit = bar // 2                 # 2마디 단위 번호 0~7
        ci = unit % 4                   # 코드 번호
        t0 = bar * 4
        # 사각파 베이스 8분(옥타브 오가기 없음, 근음만)
        for s in range(8):
            S.note(bass, t0 + s * 0.5, 0.40, ROOT[ci], "square", duty=0.5,
                   env=(0.003, 0.06, 0.6, 0.04), gain=0.22 if s % 2 == 0 else 0.16)
        # 킥 4박(짧게: 190→60Hz, 감쇠 60ms), 스네어 2·4박, 오프비트 하이햇
        for beat in range(4):
            S.kick(drum, t0 + beat, 0.5, decay=0.06)
        S.snare(drum, t0 + 1, 0.55)
        S.snare(drum, t0 + 3, 0.55)
        for i in range(4):
            S.hat(hat, t0 + i + 0.5, 0.55, length=0.035, pan=0.3)
        if unit >= 4:   # 2바퀴째: 16분 약한 하이햇 추가
            for i in range(8):
                S.hat(hat, t0 + 0.25 + i * 0.5, 0.18, length=0.02, pan=-0.3)
        # 경보 리드: 사각파, 기준음과 반음 위를 번갈아(단위의 첫 마디는 16분 8개, 둘째 마디는 8분 4개)
        if LEAD_ON[unit]:
            first = bar % 2 == 0
            steps = 8 if first else 4          # 첫 마디 16분 8개, 둘째 마디 8분 4개
            for i in range(steps):
                m = ALARM[ci] + (i % 2)
                S.note(lead, t0 + i * 0.25 * (1 if first else 2), 0.2 if first else 0.4, m, "square", duty=0.5,
                       env=(0.004, 0.04, 0.8, 0.04), gain=0.34, pan=-0.1)

    bass.buf = sy.lowpass(bass.buf, 1600)
    lead.buf = sy.reverb(sy.lowpass(lead.buf, 5000), mix=0.15, fb=0.75)
    hat.buf = sy.highpass(hat.buf, 4000)
    drum.buf = sy.reverb(drum.buf, mix=0.05, fb=0.7)

    mix = sy.sum_tracks(S, {"hat": 0.8})
    paths, rep = sy.export_loop(S, mix)
    REPORT.update(rep)
    return paths
