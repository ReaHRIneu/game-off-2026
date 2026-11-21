"""효과음 후보 원본을 게임용 최종 WAV로 가공한다.

선택 목록(JSON)의 ID마다 원본을 읽어 모노로 합치고, 앞 무음을 건너뛴 뒤
지정 길이만큼 자르고, 저역 제거(80Hz 규칙)·페이드·피크 정규화를 거쳐
<out>/<ID>.wav(여러 개면 <ID>_<n>.wav)로 저장한다. 반복음(loop)은 꼬리를
머리에 겹쳐 이음매를 없앤다.

사용: python -P tools/audio/prepare_sfx.py --src-root <원본 폴더> \
        --picks Audio/Export/sfx-picks.json --out Audio/Export/sfx-final
경로는 인자로만 받는다(코드에 로컬 절대 경로를 넣지 않는다).
"""
import argparse
import json
import os
import struct
import wave

import numpy as np


def read_wav(path):
    """PCM 16/24/32비트와 32비트 float WAV를 읽어 (모노 float64, 샘플레이트)를 돌려준다."""
    with open(path, "rb") as f:
        data = f.read()
    if data[:4] != b"RIFF" or data[8:12] != b"WAVE":
        raise ValueError("WAV 아님: " + path)
    pos, fmt, raw = 12, None, None
    while pos + 8 <= len(data):
        cid = data[pos:pos + 4]
        size = struct.unpack("<I", data[pos + 4:pos + 8])[0]
        body = data[pos + 8:pos + 8 + size]
        if cid == b"fmt ":
            tag, ch, rate, _, _, bits = struct.unpack("<HHIIHH", body[:16])
            if tag == 0xFFFE and len(body) >= 26:  # WAVE_FORMAT_EXTENSIBLE
                tag = struct.unpack("<H", body[24:26])[0]
            fmt = (tag, ch, rate, bits)
        elif cid == b"data":
            raw = body
        pos += 8 + size + (size & 1)
    if fmt is None or raw is None:
        raise ValueError("fmt/data 없음: " + path)
    tag, ch, rate, bits = fmt
    if tag == 3 and bits == 32:
        x = np.frombuffer(raw[: len(raw) // 4 * 4], dtype="<f4").astype(np.float64)
    elif tag == 1 and bits == 8:  # 8비트는 부호 없는 정수
        x = (np.frombuffer(raw, dtype=np.uint8).astype(np.float64) - 128.0) / 128.0
    elif tag == 1 and bits == 16:
        x = np.frombuffer(raw[: len(raw) // 2 * 2], dtype="<i2") / 32768.0
    elif tag == 1 and bits == 24:
        b = np.frombuffer(raw[: len(raw) // 3 * 3], dtype=np.uint8).reshape(-1, 3)
        v = (b[:, 0].astype(np.int32) | (b[:, 1].astype(np.int32) << 8) | (b[:, 2].astype(np.int32) << 16))
        v = np.where(v >= 1 << 23, v - (1 << 24), v)
        x = v / float(1 << 23)
    elif tag == 1 and bits == 32:
        x = np.frombuffer(raw[: len(raw) // 4 * 4], dtype="<i4") / 2147483648.0
    else:
        raise ValueError("지원 안 하는 형식 tag=%d bits=%d: %s" % (tag, bits, path))
    n = len(x) // ch
    x = x[: n * ch].reshape(n, ch).mean(axis=1)
    return x, rate


def write_wav(path, x, rate):
    y = np.clip(x, -1.0, 1.0)
    pcm = (y * 32767.0).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(pcm.tobytes())


def one_pole_hp(x, rate, fc):
    a = np.exp(-2 * np.pi * fc / rate)
    y = np.empty_like(x)
    prev_x = prev_y = 0.0
    for i, v in enumerate(x):
        prev_y = a * (prev_y + v - prev_x)
        prev_x = v
        y[i] = prev_y
    return y


def one_pole_lp(x, rate, fc):
    a = np.exp(-2 * np.pi * fc / rate)
    y = np.empty_like(x)
    s = 0.0
    for i, v in enumerate(x):
        s = (1 - a) * v + a * s
        y[i] = s
    return y


def sub_pct(x, rate, cutoff=80.0):
    """cutoff 아래 에너지 비율(%)."""
    if len(x) < 16:
        return 0.0
    spec = np.abs(np.fft.rfft(x * np.hanning(len(x)))) ** 2
    freqs = np.fft.rfftfreq(len(x), 1.0 / rate)
    total = spec.sum()
    return float(spec[freqs < cutoff].sum() / total * 100.0) if total > 0 else 0.0


def first_sound(x, rate, db=-40.0):
    """최고점 대비 db 위로 처음 올라오는 위치(앞 무음 건너뛰기)."""
    peak = np.max(np.abs(x)) if len(x) else 0.0
    if peak <= 0:
        return 0
    idx = np.nonzero(np.abs(x) >= peak * 10 ** (db / 20.0))[0]
    return max(0, int(idx[0]) - int(0.002 * rate)) if len(idx) else 0


def steady_window(x, rate, dur):
    """dur초 창 중 0.5초 RMS의 변동이 가장 작은 시작 위치(비 같은 고른 환경음 구간 찾기)."""
    hop = int(0.5 * rate)
    nwin = int(dur * rate)
    blocks = np.array([np.sqrt(np.mean(x[i:i + hop] ** 2)) for i in range(0, len(x) - hop, hop)])
    k = nwin // hop
    best, best_i = None, 0
    for i in range(0, len(blocks) - k):
        seg = blocks[i:i + k]
        score = seg.std() / (seg.mean() + 1e-9) + (seg.max() / (seg.mean() + 1e-9)) * 0.2
        if best is None or score < best:
            best, best_i = score, i
    return best_i * hop


def fade(x, rate, fin=0.005, fout=0.02):
    n = len(x)
    a = min(int(fin * rate), n // 4)
    b = min(int(fout * rate), n // 4)
    if a > 0:
        x[:a] *= np.linspace(0, 1, a)
    if b > 0:
        x[-b:] *= np.linspace(1, 0, b)
    return x


def make_loop(x, rate, xf=0.05):
    """꼬리 xf초를 머리에 교차 페이드로 겹쳐 이음매 없는 반복음을 만든다."""
    n = int(xf * rate)
    head, body, tail = x[:n], x[n:-n], x[-n:]
    t = np.linspace(0, 1, n)
    mixed = tail * (1 - t) + head * t
    return np.concatenate([mixed, body])


def process(src_root, pick, rate_out=None):
    x, rate = read_wav(os.path.join(src_root, pick["src"]))
    if pick.get("window") == "steady":
        start = steady_window(x, rate, pick["dur"])
    else:
        start = int(pick.get("start", 0) * rate)
        if pick.get("skip_silence", True):
            start += first_sound(x[start:], rate)
    dur = pick.get("dur")
    end = len(x) if dur is None else min(len(x), start + int(dur * rate))
    y = x[start:end].copy()
    y -= y.mean()
    y = one_pole_hp(one_pole_hp(y, rate, pick.get("hp", 90.0)), rate, pick.get("hp", 90.0))
    if pick.get("lp"):
        y = one_pole_lp(one_pole_lp(y, rate, pick["lp"]), rate, pick["lp"])
    if pick.get("loop"):
        y = make_loop(y, rate)
    else:
        y = fade(y, rate)
    peak = np.max(np.abs(y)) if len(y) else 0.0
    if peak > 0:
        y *= pick.get("peak", 0.7) / peak
    return y, rate


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src-root", required=True)
    ap.add_argument("--picks", required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    with open(a.picks, encoding="utf-8") as f:
        picks = json.load(f)
    os.makedirs(a.out, exist_ok=True)
    selection = {"sfx": {}, "bgm": picks.get("bgm", {})}
    rows = []
    for sid, items in picks["sfx"].items():
        outs = []
        for n, pick in enumerate(items):
            y, rate = process(a.src_root, pick)
            name = sid + ".wav" if len(items) == 1 else "%s_%d.wav" % (sid, n + 1)
            path = os.path.join(a.out, name)
            write_wav(path, y, rate)
            outs.append(path.replace("\\", "/"))
            rows.append((name, len(y) / rate, rate, sub_pct(y, rate), os.path.basename(pick["src"])))
        selection["sfx"][sid] = outs
    for name, d, r, s, src in rows:
        flag = "  <-- 80Hz 규칙 초과" if s > 10.0 else ""
        print("%-12s %6.3fs %6dHz sub80=%5.1f%%  %s%s" % (name, d, r, s, src, flag))
    print("총 %d개 파일" % len(rows))
    sel_path = os.path.join(os.path.dirname(a.picks), "selection.generated.json")
    with open(sel_path, "w", encoding="utf-8") as f:
        json.dump(selection, f, ensure_ascii=False, indent=2)
    print("반입 목록:", sel_path.replace("\\", "/"))


if __name__ == "__main__":
    main()
