"""방 변형 그림 4종 — W-046: 한 스테이지에 같은 방 그림이 3개 이상 나오지 않게 L2 소품만 다르게 그린 변형 방.

corridor_b·corridor_c(바탕 corridor), ot_corridor_b(바탕 ot_corridor), ot_stairwell_b(바탕 ot_stairwell).
L1 껍데기·L3 조명(·어둠)은 바탕 방 그림을 그대로 복사하고(벽·바닥·계단 모양·조명 기구 위치·windows 동일), L2 소품만 새로 그린다.
구시가지(ot_*) 변형은 챕터 0 팔레트로 그린 뒤 room128_ch1recolor와 같은 대응표(BASE)로 칠한다(새 색 없음).
바탕 방 그림이 먼저 만들어져 있어야 한다(room128_corridor·room128_ch0rooms·room128_ch1recolor 먼저).
게임 판정(계단·문·해치·오브젝트 자리)은 바꾸지 않는다: 소품은 벽 쪽에 두고 바닥 가운데는 비운다.
"""
import json
import shutil

from PIL import Image

from artkit import EXPORT, PIX, canvas, draw_ellipse, draw_line, draw_poly, palette_check, pbox, pfill, pset, recolor, save
from pixfont import draw_text
from prop import box3, contact_shadow, dither_fill

W, H = 640, 384
FLOOR, CEIL = 352, 28

# 구시가지 색 대응표(room128_ch1recolor.BASE와 같은 값)
OT_BASE = {"0": "f", "1": "J", "4": "d", "2": "H", "3": "U"}


def cl(im, x0, y0, x1, y1):
    """영역 지우기(투명)."""
    for y in range(max(0, y0), min(H, y1 + 1)):
        for x in range(max(0, x0), min(W, x1 + 1)):
            im.putpixel((x, y), (0, 0, 0, 0))


def fixtures(im):
    """천장 형광등 기구(바탕 corridor와 같은 자리 — L3 조명 위치가 같다)."""
    for lx in (140, 460):
        box3(im, lx - 50, CEIL + 26, lx + 50, CEIL + 34, "3", "W", "2", bevel=2)
        pfill(im, lx - 44, CEIL + 35, lx + 44, CEIL + 37, "W" if lx == 140 else "3")


def door(im, x, num, base="D", light="h", dark="d"):
    box3(im, x - 4, 100, x + 84, FLOOR - 1, "d", "D", "K")
    box3(im, x, 106, x + 80, FLOOR - 1, base, light, dark, bevel=2)
    pbox(im, x + 10, 150, x + 70, 215, dark); pbox(im, x + 10, 235, x + 70, 330, dark)
    pfill(im, x + 66, 226, x + 72, 244, "h"); pbox(im, x + 66, 226, x + 72, 244, "K")
    draw_ellipse(im, (x + 36, 142, x + 44, 150), "K"); pset(im, x + 38, 144, "W")
    box3(im, x + 22, 114, x + 58, 134, "3", "W", "2", bevel=2)
    draw_text(im, x + 31, 120, num, "K")


# ---------- corridor_b: 호수 붙은 현관문·신발장·화분·벽 시계 ----------

def props_corridor_b():
    im = canvas(W, H)
    fixtures(im)
    door(im, 200, "201"); door(im, 330, "202")
    for dx in (200, 330):                                                  # 문 앞 매트
        pfill(im, dx + 6, FLOOR - 7, dx + 74, FLOOR - 2, "r"); pfill(im, dx + 6, FLOOR - 7, dx + 74, FLOOR - 6, "R")
        pbox(im, dx + 6, FLOOR - 7, dx + 74, FLOOR - 2, "K")
    # 신발장(왼쪽 벽) + 위 신발
    box3(im, 30, 262, 150, FLOOR - 1, "D", "h", "d", bevel=2)
    pfill(im, 90, 266, 91, FLOOR - 4, "K")
    for sy in (290, 318):
        pfill(im, 38, sy, 82, sy + 3, "K"); pfill(im, 98, sy, 142, sy + 3, "K")
    pfill(im, 62, 270, 76, 276, "h"); pfill(im, 104, 270, 118, 276, "h")
    draw_poly(im, [(46, 262), (80, 262), (80, 246), (66, 240), (46, 246)], "t"); pbox(im, 46, 240, 80, 262, "K")
    pfill(im, 46, 256, 80, 262, "K")
    draw_poly(im, [(100, 262), (134, 262), (134, 250), (118, 242), (100, 244)], "R"); pfill(im, 100, 256, 134, 262, "K")
    # 벽 시계
    draw_ellipse(im, (450, 128, 494, 172), "K"); draw_ellipse(im, (454, 132, 490, 168), "W")
    for (x, y) in [(472, 135), (472, 164), (457, 150), (487, 150)]:
        pfill(im, x - 1, y - 1, x + 1, y + 1, "K")
    draw_line(im, [(472, 150), (472, 139)], "K", 3); draw_line(im, [(472, 150), (482, 154)], "K", 3)
    pfill(im, 470, 148, 474, 152, "R")
    # 화분(오른쪽)
    for pts, c in [([(566, 306), (546, 262), (558, 250), (570, 300)], "g"), ([(570, 306), (580, 236), (594, 230), (584, 300)], "G"),
                   ([(574, 306), (608, 266), (618, 276), (582, 308)], "g"), ([(568, 306), (540, 290), (534, 300), (562, 310)], "G")]:
        draw_poly(im, pts, c)
    draw_line(im, [(570, 306), (580, 240)], "K", 2)
    box3(im, 546, 306, 602, FLOOR - 1, "O", "A", "o", bevel=4)
    pfill(im, 542, 302, 606, 312, "o"); pbox(im, 542, 302, 606, 312, "K"); pfill(im, 544, 304, 604, 305, "O")
    for (x0, x1) in [(30, 150), (546, 602)]:
        contact_shadow(im, x0, x1, FLOOR + 1)
    return im


# ---------- corridor_c: 우편함 묶음·자판기·자전거·층 안내판 ----------

def props_corridor_c():
    im = canvas(W, H)
    fixtures(im)
    # 우편함 묶음(2줄 x 7칸)
    box3(im, 22, 96, 236, 214, "3", "W", "2")
    for r in range(2):
        for c in range(7):
            x, y = 28 + c * 29, 102 + r * 55
            box3(im, x, y, x + 25, y + 49, "d" if (r * 7 + c) % 4 == 1 else "2", "3", "K", bevel=2)
            pfill(im, x + 5, y + 6, x + 20, y + 13, "W"); pfill(im, x + 11, y + 32, x + 14, y + 36, "K")
    # 층 안내판(가운데 벽)
    box3(im, 282, 96, 402, 206, "4", "3", "K", bevel=2)
    pfill(im, 290, 104, 394, 126, "K"); draw_text(im, 298, 111, "FLOOR", "Y")
    for i, (t, c) in enumerate([("3F", "C"), ("2F", "G"), ("1F", "R"), ("B1", "Y")]):
        y = 134 + i * 18
        draw_text(im, 298, y, t, "W"); pfill(im, 326, y + 1, 326 + 54 - i * 10, y + 5, c)
    # 자판기(오른쪽)
    box3(im, 524, 186, 618, FLOOR - 1, "b", "C", "B")
    pfill(im, 534, 214, 608, 296, "K")
    for r in range(3):
        for c in range(5):
            x, y = 538 + c * 14, 220 + r * 26
            pfill(im, x, y, x + 9, y + 19, ["R", "Y", "G", "W", "C"][(r + c) % 5]); pfill(im, x, y + 17, x + 9, y + 19, "K")
    pbox(im, 534, 214, 608, 296, "K")
    pfill(im, 534, 194, 608, 208, "K"); draw_text(im, 548, 197, "DRINK", "W")
    pfill(im, 538, 304, 580, 326, "K"); pfill(im, 592, 304, 608, 312, "R"); pbox(im, 592, 304, 608, 312, "K")
    # 자전거(왼쪽 아래, 벽에 기대 둠)
    for cx in (66, 146):
        draw_ellipse(im, (cx - 36, FLOOR - 74, cx + 36, FLOOR - 2), "K", 5)
        draw_ellipse(im, (cx - 31, FLOOR - 69, cx + 31, FLOOR - 7), "2", 2)
        pfill(im, cx - 3, FLOOR - 41, cx + 3, FLOOR - 35, "3")
    draw_line(im, [(66, FLOOR - 38), (96, FLOOR - 70), (138, FLOOR - 70), (146, FLOOR - 38)], "K", 8)
    draw_line(im, [(66, FLOOR - 38), (96, FLOOR - 70), (138, FLOOR - 70), (146, FLOOR - 38), (110, FLOOR - 38), (96, FLOOR - 70)], "R", 4)
    draw_line(im, [(138, FLOOR - 70), (142, FLOOR - 96), (158, FLOOR - 102)], "K", 6)
    pfill(im, 86, FLOOR - 82, 112, FLOOR - 76, "K")                     # 안장
    contact_shadow(im, 30, 182, FLOOR + 1); contact_shadow(im, 524, 618, FLOOR + 1)
    return im


# ---------- ot_corridor_b: 낡은 전단 벽·늘어진 전선·녹슨 배관·우편 구멍 문·쓰레기봉투 ----------

def sag(im, x0, y0, x1, y1, depth, ch="K", w=3):
    pts = []
    n = 24
    for i in range(n + 1):
        t = i / n
        pts.append((x0 + (x1 - x0) * t, y0 + (y1 - y0) * t + depth * 4 * t * (1 - t)))
    draw_line(im, pts, ch, w)


def props_ot_corridor_b():
    im = canvas(W, H)
    fixtures(im)
    # 낡은 전단 벽: 겹쳐 붙은 종이, 찢긴 모서리
    for (x, y, w, h, c) in [(30, 84, 44, 62, "W"), (66, 100, 52, 40, "h"), (112, 82, 40, 70, "3"), (146, 104, 56, 44, "W"),
                            (40, 150, 60, 36, "y"), (104, 148, 36, 52, "W"), (170, 150, 48, 46, "C")]:
        box3(im, x, y, x + w, y + h, c, "W" if c != "W" else "3", "3", bevel=2)
        for ly in range(y + 7, y + h - 4, 6):
            pfill(im, x + 5, ly, x + w - 8, ly, "2")
        pfill(im, x + w - 8, y, x + w, y + 7, "1")                         # 찢겨 나간 귀퉁이
    dither_fill(im, 36, 90, 214, 200, "0", 5)                              # 얼룩
    # 늘어진 전선과 접속함
    box3(im, 250, 104, 284, 130, "3", "W", "2", bevel=2)
    box3(im, 530, 112, 562, 136, "3", "W", "2", bevel=2)
    sag(im, 284, 116, 530, 124, 56, "K", 6); sag(im, 284, 116, 530, 124, 56, "3", 3); sag(im, 284, 120, 530, 128, 40, "R", 2)
    sag(im, 562, 120, 636, 150, 34, "K", 6); sag(im, 562, 120, 636, 150, 34, "3", 3)
    draw_line(im, [(266, 130), (264, 168), (270, 182)], "K", 6); draw_line(im, [(266, 130), (264, 168), (270, 182)], "3", 2)
    # 녹슨 배관(가로)
    pfill(im, 214, 214, W - 1, 232, "o"); pfill(im, 214, 214, W - 1, 217, "O"); pfill(im, 214, 228, W - 1, 232, "r")
    pfill(im, 214, 213, W - 1, 213, "K"); pfill(im, 214, 233, W - 1, 233, "K")
    for x in range(250, W, 120):
        box3(im, x, 208, x + 14, 238, "3", "W", "2", bevel=2)
    dither_fill(im, 214, 218, W - 1, 230, "d", 4)
    for x in (300, 470):                                                   # 녹물 줄
        pfill(im, x, 233, x + 3, 290, "d"); pfill(im, x + 1, 233, x + 1, 300, "o")
    # 우편 구멍 달린 문(가운데)
    box3(im, 326, 108, 414, FLOOR - 1, "d", "D", "K")
    box3(im, 330, 114, 410, FLOOR - 1, "d", "D", "K", bevel=2)
    pfill(im, 346, 246, 392, 252, "K"); pfill(im, 348, 240, 390, 245, "3")           # 우편 구멍과 덮개
    pfill(im, 396, 286, 402, 302, "h"); pbox(im, 396, 286, 402, 302, "K")
    box3(im, 346, 124, 380, 142, "4", "3", "K", bevel=2); draw_text(im, 359, 129, "7", "W")
    for (x, y) in [(350, 300), (372, 318), (394, 306)]:
        pfill(im, x, y, x + 5, y + 2, "r")                                # 녹 얼룩
    # 쓰레기봉투(오른쪽 바닥)
    for (cx, w, h, c) in [(492, 46, 52, "K"), (548, 54, 62, "4"), (598, 40, 44, "K")]:
        draw_ellipse(im, (cx - w // 2, FLOOR - h, cx + w // 2, FLOOR - 2), c)
        draw_poly(im, [(cx - 6, FLOOR - h + 3), (cx, FLOOR - h - 12), (cx + 6, FLOOR - h + 3)], c)
        draw_ellipse(im, (cx - w // 2, FLOOR - h, cx + w // 2, FLOOR - 2), "3", 2)
        pfill(im, cx - w // 4, FLOOR - h + 12, cx - w // 4 + 5, FLOOR - h + 24, "3")
        pfill(im, cx - 2, FLOOR - h - 6, cx + 2, FLOOR - h - 3, "R")
    contact_shadow(im, 466, 620, FLOOR + 1)
    return im


# ---------- ot_stairwell_b: 다른 층 번호·계단 밑 자전거·상자·다른 색 포스터 ----------

def props_ot_stairwell_b():
    import room128_ch0rooms as rc
    _, pr, _, _ = rc.stairwell()                                           # 난간·비상구 표지·벽등은 바탕 그대로
    cl(pr, 14, 214, 92, 320)                                               # 소화전 + FIRE 글자
    cl(pr, 528, 48, 568, 76)                                               # 층 번호
    cl(pr, 192, 84, 226, 102)                                              # 낙서
    # 층 번호(3F)
    pfill(pr, 532, 50, 568, 74, "Y"); pbox(pr, 532, 50, 568, 74, "K"); pfill(pr, 533, 71, 567, 73, "y")
    draw_text(pr, 543, 58, "3F", "K")
    # 다른 색 포스터 2장
    box3(pr, 22, 206, 98, 318, "T", "S", "t", bevel=2)
    pfill(pr, 30, 214, 90, 250, "K"); pfill(pr, 34, 218, 86, 246, "C")
    draw_ellipse(pr, (50, 222, 70, 242), "Y"); pfill(pr, 38, 262, 82, 266, "W"); pfill(pr, 38, 274, 74, 278, "W"); pfill(pr, 38, 286, 62, 290, "W")
    box3(pr, 108, 232, 134, 282, "R", "W", "r", bevel=2); pfill(pr, 114, 240, 128, 262, "W")
    for (x, y) in [(200, 88), (212, 94), (206, 100)]:                      # 보라색 낙서 선
        pfill(pr, x, y, x + 12, y + 2, "i")
    # 계단 밑 상자(계단 끝 너머) + 자전거(계단 아래)
    for (x, y, w, h) in [(496, FLOOR - 44, 56, 44), (560, FLOOR - 62, 60, 62), (508, FLOOR - 86, 44, 42)]:
        box3(pr, x, y, x + w, y + h, "D", "h", "d", bevel=2)
        pfill(pr, x + 2, y + h // 2, x + w - 2, y + h // 2 + 1, "d"); pfill(pr, x + w // 2 - 4, y + 2, x + w // 2 + 4, y + h - 2, "y")
    for cx in (330, 410):
        draw_ellipse(pr, (cx - 30, FLOOR - 62, cx + 30, FLOOR - 2), "K", 5)
        draw_ellipse(pr, (cx - 25, FLOOR - 57, cx + 25, FLOOR - 7), "2", 2)
    draw_line(pr, [(330, FLOOR - 32), (354, FLOOR - 58), (392, FLOOR - 58), (410, FLOOR - 32), (368, FLOOR - 32), (354, FLOOR - 58)], "G", 4)
    draw_line(pr, [(392, FLOOR - 58), (396, FLOOR - 80), (410, FLOOR - 84)], "K", 6)
    pfill(pr, 342, FLOOR - 70, 364, FLOOR - 65, "K")
    contact_shadow(pr, 296, 446, FLOOR + 1); contact_shadow(pr, 496, 620, FLOOR + 1)
    return pr


JOBS = [
    # (새 키, 바탕 방, 소품 함수, 구시가지 색 여부)
    ("corridor_b", "corridor", props_corridor_b, False),
    ("corridor_c", "corridor", props_corridor_c, False),
    ("ot_corridor_b", "ot_corridor", props_ot_corridor_b, True),
    ("ot_stairwell_b", "ot_stairwell", props_ot_stairwell_b, True),
]


def build():
    made = []
    man = json.loads((EXPORT / "manifest.json").read_text(encoding="utf-8"))
    for key, base, fn, ot in JOBS:
        pr = fn()
        if ot:
            pr = recolor(pr, OT_BASE)
        assert not palette_check(pr), key
        out = EXPORT / f"rooms128/{key}"
        out.mkdir(parents=True, exist_ok=True)
        for p in sorted((EXPORT / f"rooms128/{base}").glob("L*.png")):
            if p.stem == "L2_props":
                continue
            shutil.copyfile(p, out / p.name)                              # L1 껍데기·L3 조명·어둠은 바탕 그대로
            meta = dict(man.get(f"rooms128/{base}/{p.name}", {}))
            meta["variant_of"] = base
            made.append(save(Image.open(out / p.name).convert("RGBA"), f"rooms128/{key}/{p.name}", meta))
        meta = {"status": "시안", "layer": "L2", "size": [W, H], "floor_y": FLOOR, "variant_of": base}
        made.append(save(pr, f"rooms128/{key}/L2_props.png", meta))

    def comp(k):
        c = Image.open(EXPORT / f"rooms128/{k}/L1_shell.png").convert("RGBA")
        c.alpha_composite(Image.open(EXPORT / f"rooms128/{k}/L2_props.png").convert("RGBA"))
        for p in sorted((EXPORT / f"rooms128/{k}").glob("L3_light_*.png")):
            c.alpha_composite(Image.open(p).convert("RGBA"))
        return c.resize((W // 2, H // 2), Image.NEAREST)

    rows = [("corridor", "corridor_b", "corridor_c"), ("ot_corridor", "ot_corridor_b", None), ("ot_stairwell", "ot_stairwell_b", None)]
    pv = canvas(W // 2 * 3 + 8, H // 2 * 3 + 8, PIX["a"])
    for r, row in enumerate(rows):
        for c, k in enumerate(row):
            if k:
                pv.alpha_composite(comp(k), (c * (W // 2 + 4), r * (H // 2 + 4)))
    made.append(save(pv, "preview/w046_variants.png", {"status": "미리보기"}))
    return made
