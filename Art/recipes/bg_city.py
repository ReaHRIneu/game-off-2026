"""배경: SF 디스토피아 도시 야경 (도트, 320×180) [시안]

- 용도 [제안]: 타이틀 화면, 프로그램 창 뒤 바탕
- 레이어를 따로도 내보내 Unity에서 시차 스크롤(패럴랙스)에 쓸 수 있게 한다
- 중앙의 도시 관리 타워에 설계도 층(청사진 디더)을 몇 픽셀 어긋나게 겹쳐 SHIFT 주제를 표현
- 난수 시드 고정 → 매번 같은 그림
"""
import random

from artkit import canvas, over, palette_check, pbox, pfill, pset, save, upscale

W, H = 320, 180
SEED = 2026


def sky():
    im = canvas(W, H)
    bands = [(0, "a"), (52, "e"), (96, "f"), (136, "i")]
    for i, (y0, ch) in enumerate(bands):
        y1 = bands[i + 1][0] - 1 if i + 1 < len(bands) else H - 1
        pfill(im, 0, y0, W - 1, y1, ch)
        if i > 0:  # 띠 경계를 체커 디더로 섞음(그라데이션 대신)
            prev = bands[i - 1][1]
            for y in range(y0, y0 + 4):
                for x in range(W):
                    if (x + y) % 2 == 0 and (y - y0) < 2 or (x + y) % 4 == 0:
                        pset(im, x, y, prev)
    rnd = random.Random(SEED)
    for _ in range(40):  # 스모그 사이로 보이는 별
        pset(im, rnd.randrange(W), rnd.randrange(0, 60), "C" if rnd.random() < 0.2 else "b")
    return im


def buildings(color, hmin, hmax, wmin, wmax, seed, windows=0.0, antenna=0.0):
    im = canvas(W, H)
    rnd = random.Random(seed)
    x = -rnd.randrange(0, 10)
    while x < W:
        w = rnd.randrange(wmin, wmax)
        h = rnd.randrange(hmin, hmax)
        top = H - h
        pfill(im, max(x, 0), top, min(x + w - 1, W - 1), H - 1, color)
        if rnd.random() < antenna:
            ax = x + w // 2
            if 0 <= ax < W:
                pfill(im, ax, top - rnd.randrange(4, 12), ax, top, color)
                pset(im, ax, top - 12 if top - 12 >= 0 else 0, "R")
        if windows:
            for wy in range(top + 3, H - 2, 4):
                for wx in range(x + 2, x + w - 2, 3):
                    if 0 <= wx < W and rnd.random() < windows:
                        pset(im, wx, wy, "w" if rnd.random() < 0.7 else "c")
        x += w + rnd.randrange(0, 4)
    return im


def tower():
    """도시 관리 타워 + 어긋난 설계도 겹침."""
    im = canvas(W, H)
    x0, x1, top = 146, 173, 18
    pfill(im, x0, top, x1, H - 1, "k")
    pfill(im, x0 + 4, top - 8, x1 - 4, top, "k")
    pfill(im, 159, top - 22, 160, top - 8, "k")
    pset(im, 159, top - 23, "R"); pset(im, 160, top - 23, "R")
    for wy in range(top + 4, H - 4, 6):  # 세로 띠 조명
        pfill(im, x0 + 3, wy, x1 - 3, wy, "c")
    # 설계도 층: 타워 위쪽 일부만, 오른쪽 위로 어긋나게(글리치 띠). 아래쪽은 현실 그대로 보인다.
    dx, dy = 4, -3
    y_end = top + 64
    for y in range(top - 8 + dy, y_end):
        shift = dx + (2 if (y // 6) % 3 == 1 else 0)  # 띠마다 어긋남 폭이 달라지는 글리치
        left = (x0 + 4 if y < top + dy else x0) + shift
        right = (x1 - 4 if y < top + dy else x1) + shift
        for x in range(left, right + 1):
            if 0 <= x < W and 0 <= y < H:
                if x in (left, right):
                    pset(im, x, y, "C")
                elif (x + y) % 2 == 0:
                    pset(im, x, y, "B")
        if (y - top) % 10 == 0 and y >= top + dy:
            pfill(im, left + 1, y, right - 1, y, "b")
    pfill(im, x0 + 4 + dx, top - 8 + dy, x1 - 4 + dx, top - 8 + dy, "C")
    return im


def near():
    """가까운 지붕 실루엣 + 네온 간판."""
    im = buildings("K", 14, 40, 20, 46, SEED + 3, windows=0.0, antenna=0.3)
    rnd = random.Random(SEED + 9)
    for _ in range(6):
        sx, sy = rnd.randrange(4, W - 20), rnd.randrange(H - 30, H - 10)
        col = rnd.choice(["R", "G", "C"])
        w = rnd.randrange(8, 16)
        pbox(im, sx, sy, sx + w, sy + 4, col)
        for i in range(sx + 2, sx + w - 1, 2):
            pset(im, i, sy + 2, col)
    return im


def rain(base):
    rnd = random.Random(SEED + 5)
    for _ in range(140):
        x, y = rnd.randrange(W), rnd.randrange(H)
        for i in range(3):
            if 0 <= x - i < W and 0 <= y + i * 2 < H:
                pset(base, x - i, y + i * 2, "b")
    return base


def build():
    layers = {
        "backgrounds/city_layer0_sky": sky(),
        "backgrounds/city_layer1_far": buildings("j", 60, 115, 10, 26, SEED + 1, windows=0.04, antenna=0.4),
        "backgrounds/city_layer2_mid": buildings("k", 36, 88, 14, 32, SEED + 2, windows=0.22),
        "backgrounds/city_layer3_tower": tower(),
        "backgrounds/city_layer4_near": near(),
    }
    full = layers["backgrounds/city_layer0_sky"]
    for k in list(layers)[1:]:
        full = over(full, layers[k])
    full = rain(full)
    made = []
    for rel, im in {**layers, "backgrounds/city_night": full}.items():
        assert not palette_check(im), rel
        made.append(save(im, rel + ".png", {"status": "시안", "style": "pixel", "ppu": 16, "use": "타이틀·창 뒤 바탕 [제안]"}))
    made.append(save(upscale(full, 4), "preview/city_night_x4.png", {"status": "미리보기"}))
    return made
