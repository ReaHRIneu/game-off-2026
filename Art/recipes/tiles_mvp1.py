"""MVP-1 도트 타일·스프라이트 시안 (D-035 도트 스타일).

- 논리 크기 16×16 px, Unity PPU 16, Point 필터 [제안]
- 현실 층: 불투명 타일 / 설계도 층: 체커 디더(반투명 효과를 픽셀로 표현) + 청사진 선
- 공유 팔레트 artkit.PIX 밖 색 금지, 안티앨리어싱 금지
[시안] 사용자 확정 전.
"""
from PIL import Image

from artkit import PIX, canvas, contact_sheet, over, palette_check, pbox, pfill, pmap, pset, recolor, save, upscale

N = 16
DIRS = {"right": None, "down": Image.Transpose.ROTATE_270, "left": Image.Transpose.ROTATE_180, "up": Image.Transpose.ROTATE_90}

ARROW_RIGHT = [
    "................",
    "................",
    "................",
    "........KK......",
    "........K3K.....",
    "........K33K....",
    "..KKKKKKK333K...",
    "..K3333333333K..",
    "..K3333333333K..",
    "..KKKKKKK333K...",
    "........K33K....",
    "........K3K.....",
    "........KK......",
    "................",
    "................",
    "................",
]

COURIER_RIGHT = [
    "................",
    ".....KKKK.......",
    "....KttttK......",
    "...KtTTTTtK.....",
    "...KtTSSCCK.....",
    "...KtSSSCCK.....",
    "....KtSSSK......",
    "....KTTTTK......",
    "...KTTTTTTK.....",
    "...KTtTTtTK.....",
    "...KSKTTKSK.....",
    "....KTTTTK......",
    "....KtKKtK......",
    "....KtK.KtK.....",
    "....KKK.KKK.....",
    "................",
]


def arrow(direction: str) -> Image.Image:
    im = pmap(ARROW_RIGHT)
    t = DIRS[direction]
    return im.transpose(t) if t else im


# ---- 현실 층 ----
def floor() -> Image.Image:
    im = canvas(N, N, PIX["0"])
    for x, y in [(3, 4), (11, 2), (6, 11), (13, 13), (1, 9)]:
        pset(im, x, y, "1")
    for i in range(N):  # 타일 이음새(오른쪽·아래 1px)
        pset(im, N - 1, i, "K"); pset(im, i, N - 1, "K")
    return im


def wall() -> Image.Image:
    im = canvas(N, N, PIX["2"])
    pfill(im, 1, 1, 14, 2, "3")
    pfill(im, 1, 13, 14, 14, "4")
    for x in range(1, 15):
        pset(im, x, 7, "4")
    for y in range(3, 7):
        pset(im, 7, y, "4")
    for y in range(8, 13):
        pset(im, 3, y, "4"); pset(im, 11, y, "4")
    pbox(im, 0, 0, N - 1, N - 1, "K")
    return im


def door(opened: bool) -> Image.Image:
    im = wall()
    pfill(im, 3, 3, 12, 15, "K")
    if opened:
        pfill(im, 4, 4, 11, 15, "0")
        pfill(im, 4, 4, 4, 15, "D"); pfill(im, 11, 4, 11, 15, "D")  # 옆으로 밀린 문짝
    else:
        pfill(im, 4, 4, 11, 15, "D")
        pfill(im, 7, 4, 8, 15, "d")
        pset(im, 6, 9, "h"); pset(im, 9, 9, "h")
        pfill(im, 4, 4, 11, 4, "h")
    return im


def exit_tile() -> Image.Image:
    im = floor()
    pbox(im, 2, 2, 13, 13, "g"); pbox(im, 3, 3, 12, 12, "G")
    for i in range(4):  # 위쪽 꺾쇠 2개(출구 방향 표시)
        for top in (5, 8):
            pset(im, 7 - i, top + i, "G"); pset(im, 8 + i, top + i, "G")
    return im


def checkpoint() -> Image.Image:
    im = floor()
    for i in range(5):  # 마름모
        for x, y in [(7 - i, 3 + i), (8 + i, 3 + i), (7 - i, 12 - i), (8 + i, 12 - i)]:
            pset(im, x, y, "Y")
    pfill(im, 7, 7, 8, 8, "y")
    return im


def relay() -> Image.Image:
    im = floor()
    pfill(im, 5, 7, 10, 13, "K"); pfill(im, 6, 8, 9, 12, "3"); pfill(im, 6, 11, 9, 12, "2")
    pfill(im, 7, 3, 7, 7, "K")
    pset(im, 7, 2, "G"); pset(im, 6, 2, "g"); pset(im, 8, 2, "g")
    pset(im, 7, 9, "G")
    return im


def arrow_tile(direction: str) -> Image.Image:
    return over(floor(), arrow(direction))


# ---- 설계도 층 (현실 위에 겹침) ----
def bp_base() -> Image.Image:
    im = canvas(N, N)
    for y in range(N):
        for x in range(N):
            if (x + y) % 2 == 0:
                pset(im, x, y, "B")  # 체커 디더 = 반투명 효과
    for i in range(N):
        pset(im, 8, i, "b"); pset(im, i, 8, "b")  # 청사진 격자
    pbox(im, 0, 0, N - 1, N - 1, "C")
    return im


def bp(kind: str, direction: str | None = None) -> Image.Image:
    im = bp_base()
    if kind == "wall":
        for i in range(2, 14):
            pset(im, i, i, "C"); pset(im, 15 - i, i, "C")
    elif kind in ("door_open", "door_closed"):
        pbox(im, 3, 3, 12, 15, "C")
        if kind == "door_closed":
            pfill(im, 7, 4, 8, 14, "C")
        else:
            for i in range(5):  # 열림 호(도면 기호)
                pset(im, 4 + i, 8 - i, "W")
    elif kind == "arrow":
        im = over(im, recolor(arrow(direction), {"3": "W", "K": "C"}))
    return im


# ---- 스프라이트 ----
def courier(direction: str) -> Image.Image:
    im = pmap(COURIER_RIGHT)
    return im.transpose(Image.Transpose.FLIP_LEFT_RIGHT) if direction == "left" else im


def cursor() -> Image.Image:
    im = canvas(N, N)
    for (x, y, dx, dy) in [(0, 0, 1, 1), (15, 0, -1, 1), (0, 15, 1, -1), (15, 15, -1, -1)]:
        for i in range(5):
            pset(im, x + dx * i, y, "R"); pset(im, x, y + dy * i, "R")
        pset(im, x + dx, y + dy, "W")
    return im


def build():
    items = {
        "tiles/reality_floor": floor(), "tiles/reality_wall": wall(),
        "tiles/reality_door_closed": door(False), "tiles/reality_door_open": door(True),
        "tiles/reality_exit": exit_tile(), "tiles/reality_checkpoint": checkpoint(), "tiles/reality_relay": relay(),
    }
    for d in DIRS:
        items[f"tiles/reality_arrow_{d}"] = arrow_tile(d)
    for k in ["floor", "wall", "door_closed", "door_open"]:
        items[f"tiles/blueprint_{k}"] = bp(k)
    for d in DIRS:
        items[f"tiles/blueprint_arrow_{d}"] = bp("arrow", d)
    items["sprites/courier_right"] = courier("right")
    items["sprites/courier_left"] = courier("left")
    items["sprites/cursor"] = cursor()

    made = []
    for rel, im in items.items():
        bad = palette_check(im)
        assert not bad, f"{rel}: 팔레트 밖 색 {bad}"
        layer = "blueprint" if "blueprint" in rel else ("sprite" if "sprites" in rel else "reality")
        made.append(save(im, rel + ".png", {"layer": layer, "status": "시안", "style": "pixel", "ppu": 16}))

    # 미리보기: 개별 타일 시트 + 현실 위에 설계도를 겹친 예시
    sheet = contact_sheet(made, 64, "preview/tiles_mvp1_sheet.png", cols=8)
    demo = [over(floor(), bp("floor")), over(door(False), bp("door_open")), over(arrow_tile("right"), bp("arrow", "down")),
            over(floor(), courier("right")), over(floor(), cursor())]
    demo_paths = [save(im, f"preview/overlay_{i}.png", {"status": "미리보기"}) for i, im in enumerate(demo)]
    made.append(sheet)
    made.append(contact_sheet(demo_paths, 96, "preview/overlay_sheet.png", cols=5))
    return made
