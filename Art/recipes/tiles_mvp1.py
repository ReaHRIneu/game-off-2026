"""MVP-1 도트 타일·스프라이트 시안 — 32×32 px (D-035 도트, D-039 32px).

- Unity PPU 32, Point 필터 [제안]
- 현실 층: 불투명 타일 / 설계도 층: 체커 디더 + 청사진 선(현실 위에 겹침)
- 공유 팔레트 artkit.PIX 밖 색 금지, 안티앨리어싱 금지
[시안] 사용자 확정 전.
"""
from PIL import Image

from artkit import (PIX, canvas, contact_sheet, mask_draw, outline_outside, over, palette_check,
                    pbox, pfill, pset, save)

N = 32
ROT = {"right": None, "down": Image.Transpose.ROTATE_270, "left": Image.Transpose.ROTATE_180, "up": Image.Transpose.ROTATE_90}


def rot(im, d):
    return im.transpose(ROT[d]) if ROT[d] else im


def arrow_mask() -> set:
    m = {(x, y) for x in range(5, 19) for y in range(13, 19)}  # 화살대
    for x in range(17, 28):  # 화살촉: x가 커질수록 좁아짐
        half = 27 - x
        m |= {(x, y) for y in range(15 - half, 17 + half) if 4 <= y <= 27}
    return m


def arrow(direction, fill="3", line="K"):
    im = canvas(N, N)
    mask_draw(im, arrow_mask(), fill, line)
    return rot(im, direction)


# ---- 현실 층 ----
def floor():
    im = canvas(N, N, PIX["0"])
    for x, y in [(5, 7), (22, 4), (12, 21), (27, 26), (3, 18), (17, 12), (9, 28), (25, 15)]:
        pset(im, x, y, "1")
    for x, y in [(2, 2), (28, 2), (2, 28), (28, 28)]:  # 패널 리벳
        pfill(im, x, y, x + 1, y + 1, "1")
    for i in range(N):
        pset(im, N - 1, i, "K"); pset(im, i, N - 1, "K")
    return im


def wall():
    im = canvas(N, N, PIX["2"])
    pfill(im, 1, 1, 30, 3, "3")
    pfill(im, 1, 28, 30, 30, "4")
    for y in (10, 20):
        pfill(im, 1, y, 30, y, "4")
        pfill(im, 1, y + 1, 30, y + 1, "3")
    for y in range(4, 10):
        pset(im, 15, y, "4")
    for y in range(12, 20):
        pset(im, 7, y, "4"); pset(im, 23, y, "4")
    for y in range(22, 28):
        pset(im, 15, y, "4")
    pbox(im, 0, 0, N - 1, N - 1, "K")
    return im


def door(opened):
    im = wall()
    pfill(im, 6, 5, 25, 31, "K")
    if opened:
        pfill(im, 7, 6, 24, 31, "0")
        pfill(im, 7, 6, 8, 31, "D"); pfill(im, 23, 6, 24, 31, "D")
        pfill(im, 9, 6, 9, 31, "d"); pfill(im, 22, 6, 22, 31, "d")
    else:
        pfill(im, 7, 6, 24, 31, "D")
        pfill(im, 7, 6, 24, 6, "h")
        pfill(im, 15, 7, 16, 31, "d")
        for box in [(9, 9, 13, 15), (18, 9, 22, 15), (9, 20, 13, 28), (18, 20, 22, 28)]:
            pbox(im, *box, "d")
        pfill(im, 13, 18, 13, 19, "h"); pfill(im, 18, 18, 18, 19, "h")
    return im


def exit_tile():
    im = floor()
    pbox(im, 4, 4, 27, 27, "g"); pbox(im, 5, 5, 26, 26, "G")
    for top in (8, 15):  # 위쪽 꺾쇠 2개
        for i in range(7):
            for t in (0, 1):
                pset(im, 15 - i, top + i + t, "G"); pset(im, 16 + i, top + i + t, "G")
    return im


def checkpoint():
    im = floor()
    for y in range(N):
        for x in range(N):
            d = abs(x - 15.5) + abs(y - 15.5)
            if 9 <= d <= 11:
                pset(im, x, y, "Y")
            elif d <= 3:
                pset(im, x, y, "y")
    return im


def relay():
    im = floor()
    pfill(im, 15, 6, 16, 14, "K")
    mask_draw(im, {(x, y) for x in range(10, 22) for y in range(14, 28)}, "3")
    pfill(im, 11, 22, 20, 26, "2")
    pfill(im, 15, 3, 16, 4, "G"); pset(im, 14, 4, "g"); pset(im, 17, 4, "g")
    pfill(im, 15, 18, 16, 19, "G")
    return im


def arrow_tile(d):
    return over(floor(), arrow(d))


# ---- 설계도 층 ----
def bp_base():
    im = canvas(N, N)
    for y in range(N):
        for x in range(N):
            if (x + y) % 2 == 0:
                pset(im, x, y, "B")
    for i in range(N):
        for g in (8, 16, 24):
            pset(im, g, i, "b"); pset(im, i, g, "b")
    pbox(im, 0, 0, N - 1, N - 1, "C")
    return im


def bp(kind, d=None):
    im = bp_base()
    if kind == "wall":
        for i in range(4, 28):
            pset(im, i, i, "C"); pset(im, i + 1, i, "C")
            pset(im, 31 - i, i, "C"); pset(im, 30 - i, i, "C")
    elif kind in ("door_open", "door_closed"):
        pbox(im, 6, 5, 25, 31, "C")
        if kind == "door_closed":
            pfill(im, 15, 6, 16, 30, "C")
        else:
            for i in range(10):  # 열림 호(도면 기호)
                pset(im, 8 + i, 16 - i, "W")
    elif kind == "arrow":
        im = over(im, arrow(d, "W", "C"))
    return im


# ---- 스프라이트 ----
def courier(direction):
    im = canvas(N, N)
    c = {}

    def put(pts, ch):
        for p in pts:
            c[p] = ch

    put([(x, y) for y in range(16, 24) for x in range(6, 10)], "D")          # 배달 가방
    put([(x, y) for y in range(25, 30) for x in range(11, 14)], "t")         # 다리
    put([(x, y) for y in range(25, 30) for x in range(16, 19)], "t")
    put([(x, y) for y in range(15, 25) for x in range(10, 21)], "T")         # 몸통
    put([(x, y) for y in range(15, 25) for x in range(10, 12)], "t")
    put([(x, 21) for x in range(10, 21)], "t")                               # 벨트
    put([(x, y) for y in range(16, 23) for x in range(18, 22)], "T")         # 팔
    put([(x, 23) for x in range(19, 22)], "S")                               # 손
    put([(x, y) for y in range(2, 16) for x in range(8, 23) if (x - 15) ** 2 + (y - 9) ** 2 <= 42], "t")  # 후드
    put([(x, y) for y in range(6, 15) for x in range(14, 23) if (x - 17) ** 2 + (y - 10) ** 2 <= 16], "S")  # 얼굴
    put([(x, y) for y in (9, 10) for x in range(17, 22)], "C")               # 바이저
    put([(20, 9)], "W")
    put([(x, 30) for x in list(range(11, 15)) + list(range(16, 20))], "K")   # 신발
    for (x, y), ch in c.items():
        pset(im, x, y, ch)
    outline_outside(im, "K")
    return im.transpose(Image.Transpose.FLIP_LEFT_RIGHT) if direction == "left" else im


def cursor():
    im = canvas(N, N)
    for (x, y, dx, dy) in [(0, 0, 1, 1), (31, 0, -1, 1), (0, 31, 1, -1), (31, 31, -1, -1)]:
        for i in range(9):
            for t in (0, 1):
                pset(im, x + dx * i, y + dy * t, "R"); pset(im, x + dx * t, y + dy * i, "R")
        pset(im, x + dx * 2, y + dy * 2, "W")
    return im


def build():
    items = {
        "tiles/reality_floor": floor(), "tiles/reality_wall": wall(),
        "tiles/reality_door_closed": door(False), "tiles/reality_door_open": door(True),
        "tiles/reality_exit": exit_tile(), "tiles/reality_checkpoint": checkpoint(), "tiles/reality_relay": relay(),
    }
    for d in ROT:
        items[f"tiles/reality_arrow_{d}"] = arrow_tile(d)
    for k in ["floor", "wall", "door_closed", "door_open"]:
        items[f"tiles/blueprint_{k}"] = bp(k)
    for d in ROT:
        items[f"tiles/blueprint_arrow_{d}"] = bp("arrow", d)
    items["sprites/courier_right"] = courier("right")
    items["sprites/courier_left"] = courier("left")
    items["sprites/cursor"] = cursor()

    made = []
    for rel, im in items.items():
        assert not palette_check(im), rel
        layer = "blueprint" if "blueprint" in rel else ("sprite" if "sprites" in rel else "reality")
        made.append(save(im, rel + ".png", {"layer": layer, "status": "시안", "style": "pixel", "ppu": 32}))
    sheet = contact_sheet(made, 96, "preview/tiles_mvp1_sheet.png", cols=8)
    demo = [over(floor(), bp("floor")), over(door(False), bp("door_open")), over(arrow_tile("right"), bp("arrow", "down")),
            over(floor(), courier("right")), over(floor(), cursor())]
    demo_paths = [save(im, f"preview/overlay_{i}.png", {"status": "미리보기"}) for i, im in enumerate(demo)]
    made += [sheet, contact_sheet(demo_paths, 128, "preview/overlay_sheet.png", cols=5)]
    return made
