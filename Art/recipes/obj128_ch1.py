"""챕터 1 상호작용 오브젝트(L4) 128px 그림 — W-016, D-097·D-098.

감시 카메라(켜짐·꺼짐)와 감시 빛(켜진 감시 구역 섹터에 깔리는 빨간 줄무늬·스캔라인).
규칙: 광원 왼쪽 위, 명암 3단, 바깥 외곽선 2px(K), 안티앨리어싱 없음, 공용 팔레트(PIX)만, 빛은 2px 디더 점.
카메라 그림은 천장선(방 그림 y 28px)에 위 끝이 닿는다(엔진이 맞춘다). 감시 빛은 천장선 y 28 ~ 바닥선 y 352 사이만 칠한다.
실행: python -I tools/art/build_art.py obj128_ch1
"""
from PIL import Image

from artkit import PIX, canvas, draw_ellipse, palette_check, pfill, save
from obj128_ch0 import cells, rivet
from prop import box3

CEIL, FLOOR = 28, 352        # 방 그림(rooms128)의 천장선·바닥선 y


# ---------- 감시 카메라 96×72 ----------

def camera(on):
    """천장 브래킷에 달린 감시 카메라. 렌즈는 왼쪽(조력자가 오는 쪽)을 본다. 켜짐 = 빨간 LED + 렌즈 반사, 꺼짐 = 어두운 렌즈·LED."""
    W, H = 96, 72
    im = canvas(W, H)
    box3(im, 22, 0, 73, 9, "3", "W", "2", bevel=2)                       # 천장 브래킷 판
    rivet(im, 27, 3); rivet(im, 66, 3)
    box3(im, 39, 9, 56, 25, "2", "3", "K", bevel=2)                      # 회전 관절
    pfill(im, 42, 14, 53, 15, "K"); pfill(im, 42, 19, 53, 20, "K")       # 관절 홈
    box3(im, 14, 18, 84, 28, "4", "2", "K", bevel=2)                     # 차양(위 덮개)
    box3(im, 14, 24, 84, 58, "2", "3", "4", bevel=4)                     # 본체
    box3(im, 2, 22, 20, 60, "4", "2", "K", bevel=2)                      # 렌즈 통(왼쪽, 본체보다 살짝 큼)
    # 렌즈 유리
    pfill(im, 5, 29, 12, 53, "K")
    if on:
        pfill(im, 6, 30, 11, 52, "a")
        pfill(im, 7, 32, 8, 37, "C"); pfill(im, 7, 38, 8, 39, "W")      # 반사 빛
        pfill(im, 9, 44, 10, 49, "R")                                    # 렌즈 속 붉은 점(녹화 중)
    else:
        pfill(im, 6, 30, 11, 52, "0")
        pfill(im, 7, 32, 8, 35, "2")                                     # 꺼진 렌즈: 희미한 반사만
    # 본체 홈(환기구)
    for x in range(30, 62, 8):
        pfill(im, x, 46, x + 3, 47, "K"); pfill(im, x, 51, x + 3, 52, "K")
    rivet(im, 26, 28); rivet(im, 78, 28)
    # LED(본체 오른쪽 위)
    pfill(im, 66, 30, 77, 41, "K")
    if on:
        pfill(im, 68, 32, 75, 39, "R"); pfill(im, 68, 32, 70, 34, "W"); pfill(im, 73, 37, 75, 39, "r")
        cells(im, 60, 24, 95, 46, "r", 4)                                # LED 둘레 붉은 빛
        pfill(im, 66, 30, 77, 41, "K"); pfill(im, 68, 32, 75, 39, "R"); pfill(im, 68, 32, 70, 34, "W"); pfill(im, 73, 37, 75, 39, "r")
    else:
        pfill(im, 68, 32, 75, 39, "r"); pfill(im, 68, 32, 69, 33, "2")
    cells(im, 16, 60, 84, 66, "a", 4)                                    # 본체 아래 그늘(디더)
    return im


# ---------- 감시 빛 640×384 (섹터 전체) ----------

def surveil_zone():
    """켜진 감시 구역에 깔리는 빨간 줄무늬·스캔라인. 방·레일 그림이 비치도록 2px 점을 성기게 찍는다(투명 배경)."""
    W, H = 640, 384
    im = canvas(W, H)
    for gy in range(CEIL // 2, FLOOR // 2):                              # 45도 줄무늬(폭 40px, 간격 120px): 띠 안은 1/3 점
        for gx in range(W // 2):
            x, y = gx * 2, gy * 2
            if ((x + y) // 40) % 3 == 0 and (gx + 2 * gy) % 4 == 0:
                pfill(im, x, y, x + 1, y + 1, "r")
            elif gx % 4 == 0 and gy % 4 == 0 and (gx // 4 + gy // 4) % 2 == 0:   # 띠 밖은 1/32 점(옅은 안개)
                pfill(im, x, y, x + 1, y + 1, "r")
    for y in range(CEIL + 20, FLOOR - 10, 64):                           # 스캔라인: 8px 줄·24px 비움, 줄마다 16px씩 어긋남
        for x in range(((y // 64) % 2) * 16, W, 32):
            pfill(im, x, y, x + 7, y + 1, "R")
    for x in range(0, W, 32):                                            # 천장선 아래·바닥선 위 경계 줄
        pfill(im, x, CEIL, x + 7, CEIL + 1, "R")
        pfill(im, x + 16, FLOOR - 2, x + 23, FLOOR - 1, "R")
    return im


# ---------- 빌드 ----------

def build():
    made = []
    pieces = {"camera_on": camera(True), "camera_off": camera(False), "surveil_zone": surveil_zone()}
    for name, im in pieces.items():
        assert not palette_check(im), name
        meta = {"status": "시안", "layer": "L4" if name != "surveil_zone" else "L3.5", "size": list(im.size), "px_per_logical": 4}
        made.append(save(im, f"objects128/{name}.png", meta))
    made += preview(pieces)
    return made


def _room(key):
    from artkit import EXPORT
    d = EXPORT / "rooms128" / key
    comp = canvas(640, 384, PIX["e"])
    comp.alpha_composite(Image.open(d / "L1_shell.png").convert("RGBA"))
    comp.alpha_composite(Image.open(d / "L2_props.png").convert("RGBA"))
    for p in sorted(d.glob("L3_light_*.png")):
        comp.alpha_composite(Image.open(p).convert("RGBA"))
    return comp


def preview(p):
    """계단실 위의 모습(켜짐 + 감시 빛 / 꺼짐)을 같은 배치(그림 x 120 논리 = 480px, 위 끝 y 28)로 보여 준다."""
    made = []
    cells_ = []
    for label, on in (("camera on and zone", True), ("camera off", False)):
        c = _room("ot_stairwell")
        if on:
            c.alpha_composite(p["surveil_zone"])
        c.alpha_composite(p["camera_on" if on else "camera_off"], (480, CEIL))
        cells_.append((c, label))
    gap = 16
    sheet = canvas(2 * 640 + 3 * gap, 384 + 2 * gap + 12, PIX["0"])
    from pixfont import draw_text
    for i, (im, label) in enumerate(cells_):
        x = gap + i * (640 + gap)
        sheet.alpha_composite(im, (x, gap + 12))
        draw_text(sheet, x, gap + 2, label, "W")
    made.append(save(sheet, "preview/objects128_ch1_onrooms.png", {"status": "미리보기"}))
    return made
