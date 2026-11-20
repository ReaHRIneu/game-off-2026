"""코드 합성 BGM 공용 도구 (numpy + 표준 라이브러리, 외부 음원·샘플 없음).

레시피(Audio/recipes/bgm_*.py)가 이 모듈의 Song·효과 함수로 곡을 만들고
export_loop()로 루프 본(WAV)·확인용 파일·파형 그림을 낸다.
저장소 기준 상대 경로만 사용한다(공개 저장소 — 로컬 절대 경로 금지).

루프 규칙: 곡을 reps(=2)번 이어 렌더링하고 마지막 반복 구간만 잘라 루프 본으로 쓴다.
앞 반복의 잔향·딜레이 꼬리가 루프 앞부분에 들어가 이음매가 자연스럽다.
"""
from __future__ import annotations

import wave
from pathlib import Path

import numpy as np

SR = 44100
REPO = Path(__file__).resolve().parents[2]
EXPORT = REPO / "Audio" / "Export"
PREVIEW = EXPORT / "preview"


def midi_hz(m: float) -> float:
    return 440.0 * 2.0 ** ((m - 69) / 12.0)


# ---------------------------------------------------------------- 오실레이터
def osc(kind: str, freq: float, n: int, duty: float = 0.5, phase0: float = 0.0) -> np.ndarray:
    ph = phase0 + freq * np.arange(n) / SR
    frac = ph % 1.0
    if kind == "sine":
        return np.sin(2 * np.pi * ph)
    if kind == "square":
        return np.where(frac < duty, 1.0, -1.0)
    if kind == "tri":
        return 4.0 * np.abs(frac - 0.5) - 1.0
    if kind == "saw":
        return 2.0 * frac - 1.0
    raise ValueError(kind)


def adsr(n_gate: int, a: float, d: float, s: float, r: float) -> np.ndarray:
    """0에서 시작해 0으로 끝나는 엔벨로프(클릭 방지). 길이 = 게이트 + 릴리즈."""
    na = max(int(a * SR), 8)
    nr = max(int(r * SR), 8)
    i = np.arange(n_gate)
    att = np.minimum(i / na, 1.0)
    tau = max(d, 1e-3) / 3.0
    dec = s + (1 - s) * np.exp(-np.maximum(i - na, 0) / (tau * SR))
    env = att * dec
    j = np.arange(nr)
    rel = env[-1] * 0.5 * (1 + np.cos(np.pi * j / nr))
    return np.concatenate([env, rel])


# ---------------------------------------------------------------- 필터·효과(스테레오 버퍼 shape (2, n))
def _fftconv(x: np.ndarray, h: np.ndarray) -> np.ndarray:
    n = len(x) + len(h) - 1
    nf = 1 << (n - 1).bit_length()
    return np.fft.irfft(np.fft.rfft(x, nf) * np.fft.rfft(h, nf), nf)[: len(x)]


def lowpass(buf: np.ndarray, cutoff: float, poles: int = 1) -> np.ndarray:
    a = 1 - np.exp(-2 * np.pi * cutoff / SR)
    length = int(min(12 / a, 20000))
    h = a * (1 - a) ** np.arange(length)
    out = buf
    for _ in range(poles):
        out = np.stack([_fftconv(ch, h) for ch in out])
    return out


def highpass(buf: np.ndarray, cutoff: float) -> np.ndarray:
    return buf - lowpass(buf, cutoff)


def _blocks(n: int, d: int):
    for k in range(d, n, d):
        yield k, min(k + d, n)


def delay(buf: np.ndarray, time_s: float, fb: float, mix: float, hp: float = 200.0) -> np.ndarray:
    """피드백 딜레이. 지연 입력에는 hp(Hz) 하이패스를 건다(저음 웅웅거림 방지, 원음은 그대로)."""
    d = int(round(time_s * SR))
    src = highpass(buf, hp) if hp else buf
    e = np.zeros_like(buf)
    for k, m in _blocks(buf.shape[1], d):
        w = m - k
        e[:, k:m] = src[:, k - d:k - d + w] + fb * e[:, k - d:k - d + w]
    return buf + mix * e


def _comb(x: np.ndarray, d: int, g: float) -> np.ndarray:
    y = x.copy()
    for k, m in _blocks(len(x), d):
        y[k:m] += g * y[k - d:k - d + (m - k)]
    return y


def _allpass(x: np.ndarray, d: int, g: float) -> np.ndarray:
    y = -g * x
    y[d:] += x[:-d]
    for k, m in _blocks(len(x), d):
        y[k:m] += g * y[k - d:k - d + (m - k)]
    return y


def reverb(buf: np.ndarray, mix: float, fb: float = 0.80, size: float = 1.0, hp: float = 200.0) -> np.ndarray:
    """빗살 필터 4개 + 올패스 2개(Schroeder형 간단 잔향). 좌우 길이를 조금 달리해 넓게.
    잔향 입력에는 hp(Hz) 하이패스를 건다(저음 웅웅거림 방지, 원음은 그대로)."""
    combs = [1116, 1188, 1277, 1356]
    wet = np.zeros_like(buf)
    src = highpass(buf, hp) if hp else buf
    for c, x in enumerate(src):
        off = 23 * c
        acc = np.zeros_like(x)
        for L in combs:
            acc += _comb(x, int((L + off) * size), fb)
        acc *= 0.25
        acc = _allpass(acc, 556 + off, 0.5)
        acc = _allpass(acc, 441 + off, 0.5)
        wet[c] = acc
    wet = lowpass(wet, 4500)
    return buf + mix * wet


# ---------------------------------------------------------------- Song
class Track:
    def __init__(self, n: int):
        self.buf = np.zeros((2, n))

    def put(self, start: int, sig: np.ndarray, pan: float = 0.0, gain: float = 1.0) -> None:
        n = self.buf.shape[1]
        if start >= n:
            return
        m = min(len(sig), n - start)
        th = (pan + 1) * np.pi / 4
        self.buf[0, start:start + m] += sig[:m] * gain * np.cos(th)
        self.buf[1, start:start + m] += sig[:m] * gain * np.sin(th)


class Song:
    def __init__(self, name: str, bpm: float, bars: int, reps: int = 2, seed: int = 1,
                 oneshot_s: float | None = None):
        """oneshot_s 를 주면 반복 없는 효과곡: 길이 = oneshot_s 초 그대로(꼬리 포함), reps=1."""
        self.name, self.bpm, self.bars, self.reps = name, bpm, bars, reps
        self.spb = SR * 60.0 / bpm                      # 박당 샘플(실수)
        self.N = int(round(bars * 4 * self.spb))        # 루프 길이(샘플) — 반올림 한 번만
        self.bar_samples = self.N / bars
        self.n_total = reps * self.N + 4 * SR           # 끝 꼬리 여유
        if oneshot_s is not None:
            self.reps = 1
            self.N = self.n_total = int(round(oneshot_s * SR))
        self.rng = np.random.default_rng(seed)
        self.tracks: dict[str, Track] = {}

    def track(self, name: str) -> Track:
        self.tracks[name] = Track(self.n_total)
        return self.tracks[name]

    def _each_rep(self, tr: Track, beat: float, sig: np.ndarray, pan: float, gain: float) -> None:
        s0 = int(round(beat * self.spb))
        for r in range(self.reps):
            tr.put(s0 + r * self.N, sig, pan, gain)

    # --- 음
    def note(self, tr: Track, beat: float, dur: float, midi: float, wave: str = "saw",
             duty: float = 0.5, env=(0.01, 0.1, 0.8, 0.1), pan: float = 0.0, gain: float = 1.0,
             detune=(0.0,), spread: float = 0.0) -> None:
        """beat·dur 는 박 단위. detune 은 cent 목록(여러 개면 겹침), spread 는 겹친 음의 좌우 벌림.
        저역 규칙(bgm-plan 4-2)을 코드로 강제한다: 1박 초과 음 기본음 >= 110Hz, 1박 이하 >= 65Hz,
        사인 계열 >= 130Hz, 디튠은 220Hz 이상 음에만."""
        f_low = midi_hz(midi + min(detune) / 100.0)
        if dur > 1.0 + 1e-9 and f_low < 110.0 - 1e-6:
            raise ValueError(f"저역 규칙 위반: 1박 초과 음 기본음 {f_low:.1f}Hz < 110Hz (midi {midi})")
        if f_low < 65.0 - 1e-6:
            raise ValueError(f"저역 규칙 위반: 기본음 {f_low:.1f}Hz < 65Hz (midi {midi})")
        if wave in ("sine", "epiano", "bell") and f_low < 130.0 - 1e-6:
            raise ValueError(f"저역 규칙 위반: 사인 계열 기본음 {f_low:.1f}Hz < 130Hz (midi {midi})")
        if any(abs(c) > 1e-9 for c in detune) and midi_hz(midi) < 220.0 - 1e-6:
            raise ValueError(f"저역 규칙 위반: 220Hz 미만 음에 디튠 (midi {midi})")
        ng = max(int(round(dur * self.spb)), 16)
        e = adsr(ng, *env)
        n = len(e)
        k = len(detune)
        for idx, cents in enumerate(detune):
            f = midi_hz(midi + cents / 100.0)
            if wave == "epiano":
                t = np.arange(n) / SR
                sig = (np.sin(2 * np.pi * f * t)
                       + 0.30 * np.exp(-t * 4.0) * np.sin(2 * np.pi * 2 * f * t)
                       + 0.10 * np.exp(-t * 7.0) * np.sin(2 * np.pi * 3 * f * t))
            elif wave == "bell":
                t = np.arange(n) / SR
                sig = (np.sin(2 * np.pi * f * t)
                       + 0.35 * np.exp(-t * 9.0) * np.sin(2 * np.pi * 2.76 * f * t)
                       + 0.15 * np.exp(-t * 14.0) * np.sin(2 * np.pi * 5.4 * f * t))
            else:
                sig = osc(wave, f, n, duty, phase0=(idx * 0.37) % 1.0)
            p = pan + (spread * (2 * idx / (k - 1) - 1) if k > 1 else 0.0)
            self._each_rep(tr, beat, sig * e / k, float(np.clip(p, -1, 1)), gain)

    def pad_chord(self, tr: Track, beat: float, dur: float, notes, wave: str = "saw",
                  env=(0.5, 0.2, 0.9, 0.9), gain: float = 0.3, spread: float = 0.7,
                  cents: float = 9.0) -> None:
        """코드 한 덩어리. 220Hz 이상 음만 디튠·벌림(저역 규칙 6), 그 아래 음은 단일 음."""
        for m in notes:
            if midi_hz(m) >= 220.0 - 1e-6:
                self.note(tr, beat, dur, m, wave, env=env, detune=(-cents, 0.0, cents), spread=spread, gain=gain)
            else:
                self.note(tr, beat, dur, m, wave, env=env, gain=gain)

    # --- 타악(코드 합성)
    def kick(self, tr: Track, beat: float, vel: float = 1.0, f0: float = 190.0, f1: float = 60.0,
             decay: float = 0.07) -> None:
        """사인 하강 킥. 규칙 4-2-5: 끝 주파수 >= 55Hz, 감쇠 <= 120ms."""
        if f1 < 55.0 or decay > 0.120:
            raise ValueError("킥 규칙 위반: 끝 주파수 >= 55Hz, 감쇠 <= 120ms")
        n = int(0.20 * SR)
        t = np.arange(n) / SR
        f = f1 + (f0 - f1) * np.exp(-t / 0.025)
        ph = 2 * np.pi * np.cumsum(f) / SR
        sig = np.sin(ph) * np.exp(-t / decay) * np.minimum(t / 0.001, 1.0)
        sig[-200:] *= np.linspace(1, 0, 200)
        self._each_rep(tr, beat, sig, 0.0, vel)

    def shaker(self, tr: Track, beat: float, vel: float = 1.0, pan: float = 0.2) -> None:
        """부드러운 노이즈 셰이커(느린 어택). 트랙에서 고역 필터를 따로 건다."""
        n = int(0.16 * SR)
        t = np.arange(n) / SR
        noise = self.rng.uniform(-1, 1, n)
        noise = noise - np.concatenate([[0], noise[:-1]])
        env = np.minimum(t / 0.018, 1.0) * np.exp(-t / 0.05)
        sig = noise * env
        sig[-150:] *= np.linspace(1, 0, 150)
        self._each_rep(tr, beat, sig * 0.5, pan, vel)

    def rim(self, tr: Track, beat: float, vel: float = 1.0, pan: float = -0.15) -> None:
        """림샷: 짧은 사각 톤(1.7kHz) + 노이즈 클릭."""
        n = int(0.06 * SR)
        t = np.arange(n) / SR
        tone = np.where((1700 * t) % 1.0 < 0.5, 1.0, -1.0) * np.exp(-t / 0.012)
        noise = self.rng.uniform(-1, 1, n) * np.exp(-t / 0.008)
        sig = (0.5 * tone + 0.5 * noise) * np.minimum(t / 0.0006, 1.0)
        sig[-100:] *= np.linspace(1, 0, 100)
        self._each_rep(tr, beat, sig, pan, vel)

    def snare(self, tr: Track, beat: float, vel: float = 1.0) -> None:
        n = int(0.22 * SR)
        t = np.arange(n) / SR
        noise = self.rng.uniform(-1, 1, n)
        noise = noise - np.concatenate([[0], noise[:-1]]) * 0.6   # 가벼운 고역 강조
        sig = (0.8 * noise * np.exp(-t / 0.06) + 0.5 * np.sin(2 * np.pi * 185 * t) * np.exp(-t / 0.04))
        sig *= np.minimum(t / 0.0008, 1.0)
        sig[-200:] *= np.linspace(1, 0, 200)
        self._each_rep(tr, beat, sig, 0.05, vel)

    def hat(self, tr: Track, beat: float, vel: float = 1.0, length: float = 0.05, pan: float = 0.2) -> None:
        n = int(length * 4 * SR)
        t = np.arange(n) / SR
        noise = self.rng.uniform(-1, 1, n)
        noise = noise - np.concatenate([[0], noise[:-1]])        # 고역만(1차 차분)
        sig = noise * np.exp(-t / length) * np.minimum(t / 0.0005, 1.0)
        sig[-100:] *= np.linspace(1, 0, 100)
        self._each_rep(tr, beat, sig * 0.5, pan, vel)


# ---------------------------------------------------------------- 정규화·출력
def db(x: float) -> float:
    return float(20 * np.log10(max(x, 1e-12)))


def normalize(mix: np.ndarray, loop_slice: slice, target_rms_db: float = -18.0,
              ceiling_db: float = -1.5) -> np.ndarray:
    """RMS 를 목표에 맞추고, 최고점이 천장을 넘으면 부드러운 리미터(tanh)로 눌러 다시 맞춘다."""
    ceil = 10 ** (ceiling_db / 20)
    x = mix.copy()
    for _ in range(6):
        seg = x[:, loop_slice]
        rms = np.sqrt(np.mean(seg ** 2))
        x = x * (10 ** (target_rms_db / 20) / rms)
        x = ceil * np.tanh(x / ceil)          # 메모리 없는 비선형 → 이음매 일관성 유지
    return x


def master(mix: np.ndarray) -> np.ndarray:
    """마스터 50Hz 하이패스 2번(규칙 4-2-2)."""
    return highpass(highpass(mix, 50.0), 50.0)


def band_percent(loop: np.ndarray) -> dict:
    """루프 본 전체 FFT(모노 합) 에너지 중 80Hz 아래·40Hz 아래·160~2000Hz 비율(%)."""
    mono = loop.mean(axis=0)
    sp = np.abs(np.fft.rfft(mono)) ** 2
    fr = np.fft.rfftfreq(len(mono), 1 / SR)
    tot = sp[1:].sum() + 1e-30
    return {"low80_pct": 100.0 * sp[(fr > 0) & (fr < 80)].sum() / tot,
            "low40_pct": 100.0 * sp[(fr > 0) & (fr < 40)].sum() / tot,
            "mid_pct": 100.0 * sp[(fr >= 160) & (fr <= 2000)].sum() / tot}


def write_wav(path: Path, stereo: np.ndarray) -> np.ndarray:
    pcm = np.round(np.clip(stereo, -1, 1) * 32767).astype("<i2")
    inter = np.empty(pcm.shape[1] * 2, dtype="<i2")
    inter[0::2], inter[1::2] = pcm[0], pcm[1]
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(inter.tobytes())
    return pcm.astype(np.float64) / 32767


_EDGES = np.geomspace(60, 12000, 21)


def _band_db(seg: np.ndarray) -> np.ndarray:
    w = np.hanning(len(seg))
    sp = np.abs(np.fft.rfft(seg * w)) ** 2
    fr = np.fft.rfftfreq(len(seg), 1 / SR)
    return np.array([10 * np.log10(sp[(fr >= lo) & (fr < hi)].sum() + 1e-9)
                     for lo, hi in zip(_EDGES[:-1], _EDGES[1:])])


def _spec_change(mono: np.ndarray, pos: int, half: int) -> float:
    return float(np.mean(np.abs(_band_db(mono[pos - half:pos]) - _band_db(mono[pos:pos + half]))))


def loop_report(loop: np.ndarray, song: Song) -> dict:
    n = loop.shape[1]
    mono3 = np.tile(loop.mean(axis=0), 3)
    seam_diff = float(np.max(np.abs(loop[:, 0] - loop[:, -1])))
    typ = float(np.mean(np.abs(np.diff(loop, axis=1))))
    ms10 = int(0.010 * SR)
    r_before = np.sqrt(np.mean(loop[:, -ms10:] ** 2))
    r_after = np.sqrt(np.mean(loop[:, :ms10] ** 2))
    full = np.tile(loop, (1, 3))
    ratios = []
    for k in range(1, song.bars):
        pos = n + int(round(k * song.bar_samples))
        rb = np.sqrt(np.mean(full[:, pos - ms10:pos] ** 2))
        ra = np.sqrt(np.mean(full[:, pos:pos + ms10] ** 2))
        ratios.append(db(float(ra)) - db(float(rb)))
    half = int(0.050 * SR)
    seam = [_spec_change(mono3, n, half), _spec_change(mono3, 2 * n, half)]
    bars = [_spec_change(mono3, n + int(round(k * song.bar_samples)), half) for k in range(1, song.bars)]
    return {
        "seconds": n / SR, "samples": n,
        "peak_db": db(float(np.max(np.abs(loop)))),
        "rms_db": db(float(np.sqrt(np.mean(loop ** 2)))),
        "clip_samples": int(np.sum(np.abs(loop) >= 1.0)),
        "seam_diff": seam_diff, "typical_step": typ,
        "rms_ratio_db": db(float(r_after)) - db(float(r_before)),
        "rms_ratio_bar_mean": float(np.mean(ratios)), "rms_ratio_bar_max": float(np.max(ratios)),
        "spec_seam": seam, "spec_bar_mean": float(np.mean(bars)),
        "spec_bar_max": float(np.max(bars)),
        **band_percent(loop),
    }


def draw_wave(path: Path, loop: np.ndarray, song: Song) -> None:
    from PIL import Image, ImageDraw
    n = loop.shape[1]
    mono = np.tile(loop.mean(axis=0), 3)
    W, H1, H2 = 1800, 300, 220
    img = Image.new("RGB", (W, H1 + H2 + 40), (20, 24, 32))
    d = ImageDraw.Draw(img)
    cols = np.array_split(np.arange(len(mono)), W)
    mid = H1 // 2
    for x, c in enumerate(cols):
        seg = mono[c]
        d.line([(x, mid - int(seg.max() * mid * 0.95)), (x, mid - int(seg.min() * mid * 0.95))],
               fill=(110, 200, 230))
    for k in (1, 2):
        x = int(W * k / 3)
        d.line([(x, 0), (x, H1)], fill=(255, 70, 70), width=2)
    d.text((6, 4), f"{song.name} x3 (red = loop seam)", fill=(220, 220, 220))
    # 이음매 확대(앞뒤 40ms)
    y0 = H1 + 40
    half = int(0.040 * SR)
    zoom = mono[n - half:n + half]
    mid2 = y0 + H2 // 2
    pts = []
    for x in range(W):
        i = int(x * len(zoom) / W)
        pts.append((x, mid2 - int(zoom[i] * (H2 // 2) * 0.95)))
    d.line(pts, fill=(130, 230, 150))
    d.line([(W // 2, y0), (W // 2, y0 + H2)], fill=(255, 70, 70), width=2)
    d.text((6, y0 - 16), "seam zoom: +-40 ms", fill=(220, 220, 220))
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path)


def export_loop(song: Song, mix: np.ndarray) -> tuple[list[Path], dict]:
    """mix: (2, n_total) 렌더. 루프 본·x3·파형 그림을 쓰고 보고 수치를 돌려준다."""
    a = (song.reps - 1) * song.N
    mix = normalize(master(mix), slice(a, a + song.N))
    loop = write_wav(EXPORT / f"{song.name}.wav", mix[:, a:a + song.N])
    write_wav(PREVIEW / f"{song.name}_x3.wav", np.tile(loop, (1, 3)))
    draw_wave(PREVIEW / f"{song.name}_wave.png", loop, song)
    rep = loop_report(loop, song)
    rep.update(bpm=song.bpm, bars=song.bars)
    return [EXPORT / f"{song.name}.wav", PREVIEW / f"{song.name}_x3.wav", PREVIEW / f"{song.name}_wave.png"], rep


def draw_sting(path: Path, snd: np.ndarray, song: Song) -> None:
    from PIL import Image, ImageDraw
    n = snd.shape[1]
    mono = snd.mean(axis=0)
    W, H1, H2 = 1800, 300, 200
    img = Image.new("RGB", (W, H1 + H2 + 50), (20, 24, 32))
    d = ImageDraw.Draw(img)
    mid = H1 // 2
    for x, c in enumerate(np.array_split(np.arange(n), W)):
        seg = mono[c]
        d.line([(x, mid - int(seg.max() * mid * 0.95)), (x, mid - int(seg.min() * mid * 0.95))],
               fill=(110, 200, 230))
    d.text((6, 4), f"{song.name} (one-shot, {n / SR:.2f}s)", fill=(220, 220, 220))
    y0 = H1 + 40
    half = int(0.040 * SR)
    for part, (seg, label) in enumerate(((mono[:half], "first 40 ms"), (mono[-half:], "last 40 ms"))):
        x0 = part * (W // 2) + 4
        wd = W // 2 - 8
        mid2 = y0 + H2 // 2
        pts = [(x0 + x, mid2 - int(seg[int(x * len(seg) / wd)] * (H2 // 2) * 0.95)) for x in range(wd)]
        d.line(pts, fill=(130, 230, 150))
        d.text((x0, y0 - 16), label, fill=(220, 220, 220))
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path)


def export_sting(song: Song, mix: np.ndarray, target_rms_db: float = -18.0) -> tuple[list[Path], dict]:
    """반복 없는 효과곡: 마스터 처리 후 앞 5ms 페이드인·끝 20ms 페이드아웃, WAV·파형 그림, 보고 수치."""
    N = song.N
    x = normalize(master(mix[:, :N]), slice(0, N), target_rms_db=target_rms_db)
    fi, fo = int(0.005 * SR), int(0.020 * SR)
    x[:, :fi] *= np.linspace(0, 1, fi)
    x[:, -fo:] *= np.linspace(1, 0, fo)
    snd = write_wav(EXPORT / f"{song.name}.wav", x)
    draw_sting(PREVIEW / f"{song.name}_wave.png", snd, song)
    tail = int(0.100 * SR)
    rep_ = {
        "kind": "sting", "seconds": N / SR, "samples": N,
        "peak_db": db(float(np.max(np.abs(snd)))), "rms_db": db(float(np.sqrt(np.mean(snd ** 2)))),
        "clip_samples": int(np.sum(np.abs(snd) >= 1.0)),
        "first_abs": float(np.max(np.abs(snd[:, 0]))), "last_abs": float(np.max(np.abs(snd[:, -1]))),
        "tail_rms_db": db(float(np.sqrt(np.mean(snd[:, -tail - fo:-fo] ** 2)))),
        **band_percent(snd),
    }
    return [EXPORT / f"{song.name}.wav", PREVIEW / f"{song.name}_wave.png"], rep_


def sum_tracks(song: Song, gains: dict[str, float] | None = None) -> np.ndarray:
    gains = gains or {}
    out = np.zeros((2, song.n_total))
    for k, t in song.tracks.items():
        out += t.buf * gains.get(k, 1.0)
    return out
