"""배경: SF 디스토피아 도시 야경 — 여러 레이어 구성 (도트, 640×360) [시안]

- D-039 32px 규격에 맞춰 기준 해상도 640×360(32px 타일 20×11.25칸) [제안]
- D-040 비·별 같은 별도 이펙트·파티클은 넣지 않는다
- D-041 배경은 여러 레이어로 구성한다. 각 레이어는 따로 내보내고 Unity에서 겹친다.
  합친 그림(city_night_composite)은 확인용이다.
- 레이어 순서(뒤 → 앞): 0 하늘, 1 먼 건물, 2 중간 건물, 3 관리 타워(현실), 4 관리 타워 설계도 층, 5 가까운 지붕
- 레이어 4는 설계도 층을 어긋나게 겹친 그림이다(SHIFT 주제). 따로 켜고 끌 수 있다.
- 난수 시드 고정 → 매번 같은 그림
"""
import random

from artkit import canvas, over, palette_check, pbox, pfill, pset, save, upscale

W, H = 640, 360
SEED = 2026


def sky():
    im = canvas(W, H)
    bands = [(0, "a"), (104, "e"), (192, "f"), (272, "i")]
    for i, (y0, ch) in enumerate(bands):
        y1 = bands[i + 1][0] - 1 if i + 1 < len(bands) else H - 1
        pfill(im, 0, y0, W - 1, y1, ch)
        if i > 0:  # 띠 경계를 디더로 섞음(그라데이션 대신)
            prev = bands[i - 1][1]
            for y in range(y0, y0 + 6):
                step = 2 if y - y0 < 3 else 4
                for x in range(W):
                    if (x + y) % step == 0:
                        pset(im, x, y, prev)
    return im


def buildings(color, hmin, hmax, wmin, wmax, seed, windows=0.0, antenna=0.0, roof="", gap=6):
    im = canvas(W, H)
    rnd = random.Random(seed)
    x = -rnd.randrange(0, 20)
    while x < W:
        w = rnd.randrange(wmin, wmax)
        h = rnd.randrange(hmin, hmax)
        top = H - h
        x0, x1 = max(x, 0), min(x + w - 1, W - 1)
        pfill(im, x0, top, x1, H - 1, color)
        if roof and rnd.random() < 0.5:  # 지붕 단차
            pfill(im, max(x + 3, 0), top - 4, min(x + w - 4, W - 1), top - 1, color)
            pfill(im, max(x + 3, 0), top - 4, min(x + w - 4, W - 1), top - 4, roof)
        if rnd.random() < antenna:
            ax = x + w // 2
            if 0 <= ax < W - 1:
                ah = rnd.randrange(8, 24)
                pfill(im, ax, max(top - ah, 0), ax + 1, top, color)
                pfill(im, ax, max(top - ah - 2, 0), ax + 1, max(top - ah - 1, 0), "R")
        if windows:
            for wy in range(top + 6, H - 4, 6):
                for wx in range(x + 4, x + w - 5, 5):
                    if 0 <= wx < W - 2 and rnd.random() < windows:
                        pfill(im, wx, wy, wx + 2, wy + 1, "w" if rnd.random() < 0.7 else "c")
        x += w + rnd.randrange(0, gap)
    return im


TX0, TX1, TOP = 292, 347, 36  # 관리 타워 위치


def tower_real():
    im = canvas(W, H)
    pfill(im, TX0, TOP, TX1, H - 1, "k")
    pfill(im, TX0 + 8, TOP - 16, TX1 - 8, TOP - 1, "k")
    pfill(im, 318, TOP - 44, 321, TOP - 17, "k")
    pfill(im, 318, TOP - 47, 321, TOP - 45, "R")
    for wy in range(TOP + 8, H - 8, 12):  # 가로 띠 조명
        pfill(im, TX0 + 6, wy, TX1 - 6, wy + 1, "c")
    pfill(im, TX0, TOP, TX0 + 1, H - 1, "j")  # 왼쪽 가장자리 밝음
    return im


def tower_blueprint():
    """같은 타워의 설계도 층을 오른쪽 위로 어긋나게. 위쪽 일부에만 겹친다."""
    im = canvas(W, H)
    dx, dy = 8, -6
    y_end = TOP + 128
    for y in range(TOP - 16 + dy, y_end):
        shift = dx + (4 if (y // 12) % 3 == 1 else 0)  # 띠마다 어긋남 폭이 다름
        narrow = y < TOP + dy
        left = (TX0 + 8 if narrow else TX0) + shift
        right = (TX1 - 8 if narrow else TX1) + shift
        for x in range(left, right + 1):
            if not (0 <= x < W and 0 <= y < H):
                continue
            if x in (left, left + 1, right - 1, right):
                pset(im, x, y, "C")
            elif (x + y) % 2 == 0:
                pset(im, x, y, "B")
        if (y - TOP) % 20 == 0 and not narrow:
            pfill(im, left + 2, y, right - 2, y, "b")
    pfill(im, TX0 + 8 + dx, TOP - 16 + dy, TX1 - 8 + dx, TOP - 15 + dy, "C")
    return im


def near():
    im = buildings("K", 28, 80, 40, 92, SEED + 3, antenna=0.3, roof="2", gap=4)
    rnd = random.Random(SEED + 9)
    for _ in range(7):  # 네온 간판(정지 그림)
        sx, sy = rnd.randrange(8, W - 40), rnd.randrange(H - 60, H - 20)
        col = rnd.choice(["R", "G", "C"])
        w = rnd.randrange(16, 32)
        pbox(im, sx, sy, sx + w, sy + 8, col)
        pbox(im, sx + 1, sy + 1, sx + w - 1, sy + 7, col)
        for i in range(sx + 4, sx + w - 3, 4):
            pfill(im, i, sy + 4, i + 1, sy + 4, col)
    return im


def build():
    layers = {
        "backgrounds/city_layer0_sky": sky(),
        "backgrounds/city_layer1_far": buildings("j", 120, 230, 20, 52, SEED + 1, windows=0.03, antenna=0.4, roof="e"),
        "backgrounds/city_layer2_mid": buildings("k", 72, 176, 28, 64, SEED + 2, windows=0.22, roof="j"),
        "backgrounds/city_layer3_tower": tower_real(),
        "backgrounds/city_layer4_tower_blueprint": tower_blueprint(),
        "backgrounds/city_layer5_near": near(),
    }
    composite = None
    made = []
    for rel, im in layers.items():
        assert not palette_check(im), rel
        made.append(save(im, rel + ".png", {"status": "시안", "style": "pixel", "ppu": 32, "role": "배경 레이어"}))
        composite = im if composite is None else over(composite, im)
    made.append(save(composite, "backgrounds/city_night_composite.png", {"status": "확인용 합본"}))
    made.append(save(upscale(composite, 2), "preview/city_night_x2.png", {"status": "미리보기"}))
    return made
