"""메인 화면 v6 — PixelLab 생성물 가공(W-043, D-131, D-110·D-128, art-pipeline §4-3). 320x180(화면에서 8배=2560x1440).

lobby_scene_a.png 를 바탕으로 픽셀 편집만 한다(확대·축소·필터 없음, 알파 255).
1. 벽 판 2개의 글자 모양을 지우고 판 색 + 회로선·화살표 같은 글자 아닌 무늬로 바꾼다.
2. 책상의 인물 자리를 PixelLab 편집 크롭(lobby_hacker_crop_edit.png, 같은 크기·앉은 해커)으로 바꾼다.
3. 침대의 인물(러너)은 머리를 갈색 단발 + 작은 꽁지로, 헤드셋을 지운다(러너 스프라이트의 머리색).
왼쪽 어두운 기둥(x 0~70)은 메뉴 자리라 건드리지 않는다.
[시안] 사용자 확정 전.
"""
from pathlib import Path

from PIL import Image

from artkit import save

REPO = Path(__file__).resolve().parents[2]
SRC = REPO / "Art" / "Source" / "pixellab" / "2026-11-27"

PLATE = (58, 59, 107)       # 판 바탕색
SKY = (122, 197, 250)
DARK = (23, 26, 39)
# 해커 편집 크롭의 장면 위치(CX, CY)와 붙일 인물 상자(크롭 좌표 x0, y0, x1, y1)
CX, CY = 116, 52
HB = (12, 6, 58, 62)
# 러너 머리색(러너 스프라이트에서)
BR_L, BR_M, BR_D = (122, 63, 37), (105, 51, 35), (71, 26, 47)


def is_gray(c):
    return max(c) - min(c) < 45 and sum(c) / 3 > 110


def clear_gray(im, x0, y0, x1, y1, plate):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if is_gray(im.getpixel((x, y))[:3]):
                im.putpixel((x, y), plate + (255,))


def put(im, pts, c):
    for x, y in pts:
        im.putpixel((x, y), c + (255,))


def line(a, b):
    (x0, y0), (x1, y1) = a, b
    n = max(abs(x1 - x0), abs(y1 - y0))
    return [(round(x0 + (x1 - x0) * i / n), round(y0 + (y1 - y0) * i / n)) for i in range(n + 1)] if n else [a]


def circuit(im, ox, oy, c):
    """회로선 무늬 17x15. (ox,oy)=왼쪽 위."""
    pts = []
    for a, b in [((0, 3), (6, 3)), ((6, 3), (6, 8)), ((6, 8), (12, 8)), ((12, 8), (12, 12)), ((6, 3), (10, 3)),
                 ((10, 3), (10, 0)), ((2, 8), (6, 8)), ((2, 8), (2, 13)), ((12, 12), (16, 12))]:
        pts += line(a, b)
    put(im, [(ox + x, oy + y) for x, y in pts], c)
    for nx, ny in [(0, 3), (10, 0), (2, 13), (16, 12)]:           # 끝 마디(2x2)
        put(im, [(ox + nx + dx, oy + ny + dy) for dx in (0, 1) for dy in (0, 1)], c)


def arrow(im, ox, oy, c):
    """오른쪽 화살표 13x9."""
    pts = line((0, 4), (10, 4)) + line((6, 0), (10, 4)) + line((6, 8), (10, 4)) + line((7, 0), (11, 4)) + line((7, 8), (11, 4))
    put(im, [(ox + x, oy + y) for x, y in pts], c)


def build():
    im = Image.open(SRC / "lobby_scene_a.png").convert("RGBA")
    assert im.size == (320, 180)
    # ---- 1. 벽 판 ----
    plate_l = PLATE
    clear_gray(im, 78, 58, 92, 74, plate_l)
    circuit(im, 78, 60, (156, 167, 182))
    plate_r = PLATE
    clear_gray(im, 238, 52, 273, 69, plate_r)
    circuit(im, 241, 53, (156, 167, 182))
    arrow(im, 260, 57, (156, 167, 182))
    # ---- 2. 책상의 인물 → 해커(W-043 검수 뒤 교체) ----
    # 전신 스프라이트는 머리가 장면 러너의 약 2배라, 장면의 (116,52)~(195,123)을 잘라 PixelLab 편집(1회)으로
    # 같은 크기·앉은 자세의 해커로 바꾼 그림을 쓴다. 인물 상자(크롭 좌표 HB) 안만 붙여 이음매를 줄인다.
    ed = Image.open(SRC / "lobby_hacker_crop_edit.png").convert("RGBA")
    assert ed.size == (80, 72)
    for y in range(HB[1], HB[3] + 1):
        for x in range(HB[0], HB[2] + 1):
            im.putpixel((CX + x, CY + y), ed.getpixel((x, y))[:3] + (255,))
    # ---- 3. 러너 머리 ----
    for y in range(89, 100):
        for x in range(96, 106):
            c = im.getpixel((x, y))[:3]
            if c == (85, 75, 154):
                put(im, [(x, y)], BR_L)
            elif c == (58, 59, 107):
                put(im, [(x, y)], BR_M)
            elif c in {(23, 26, 39), (26, 30, 53), (30, 32, 46)} and x <= 104:
                put(im, [(x, y)], BR_D)
            elif c in {(221, 241, 248), (160, 177, 190), (136, 148, 162)} and x <= 103 and y >= 93:
                put(im, [(x, y)], BR_M)       # 헤드셋 지움
    put(im, [(95, 95), (95, 96), (96, 97), (95, 97), (96, 98)], BR_M)    # 작은 꽁지
    assert all(a == 255 for a in im.getchannel("A").getdata())
    made = [save(im, "scenes/main_lobby_v6_x8.png", {"status": "본그림(PixelLab 가공)"})]
    out = REPO / "Art" / "Export" / "preview" / "w043_lobby_x2.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    im.resize((640, 360), Image.NEAREST).save(out)
    return made
