"""챕터 1 구시가지 방 128px 새 그림 6종 + 레일 그림(섹터 640×384) — W-015, D-100, D-073, D-075, D-078~D-080, D-084.

방: 셋방(ot_rental)·끝 방(ot_endroom)·상점(ot_shop)·빈 점포(ot_vacant)·옥상 복도(ot_roofhall)·옥상 출구 방(ot_roofexit).
챕터 0과 같은 규칙: 광원 왼쪽 위, 명암 3단, 바깥 외곽선 K, 안티앨리어싱 없음, 공용 팔레트만, 바닥선 y 352·천장선 y 28.
구시가지 톤: 벽 보라 회색(J·H), 포인트 따뜻한 갈색(d·D), 슬래브는 방마다 H로 통일(색 변경 방과 이음새가 맞도록).
레일 그림(rooms128/common/rail)은 천장·바닥 슬래브 구역에만 그린다(방 안쪽은 덮지 않음).
"""
from artkit import PIX, canvas, draw_ellipse, draw_line, draw_poly, palette_check, pbox, pfill, pset, save
from pixfont import draw_text
from prop import ambient, box3, contact_shadow, dark_overlay, dither_fill, glow, light_cone, rivets, wood

W, H = 640, 384
FLOOR, CEIL = 352, 28

WALL, SEAM, SLAB, TILE_B = "J", "f", "H", "d"   # 구시가지 벽·벽 이음·슬래브·바닥 타일 둘째 색


# ---------- 공통 ----------

def shell(wall=WALL, seam=SEAM, floor="tile"):
    """벽 + 천장 슬래브 + 바닥 + 바닥 슬래브. 환경 음영·바깥 외곽선은 finish()에서."""
    im = canvas(W, H, PIX[wall])
    for x in range(0, W, 64):
        pfill(im, x, CEIL, x, FLOOR - 1, seam)
    pfill(im, 0, 0, W - 1, CEIL - 1, SLAB); pfill(im, 0, CEIL - 2, W - 1, CEIL - 1, "K")
    if floor == "tile":
        for x in range(0, W, 32):
            pfill(im, x, FLOOR, x + 31, FLOOR + 14, SLAB if (x // 32) % 2 else TILE_B)
    elif floor == "wood":
        wood(im, 0, FLOOR, W - 1, FLOOR + 14, "D", "d", "h", plank=7)
    else:                                                       # 콘크리트
        pfill(im, 0, FLOOR, W - 1, FLOOR + 14, TILE_B)
        dither_fill(im, 0, FLOOR + 2, W - 1, FLOOR + 14, "f", 5)
    pfill(im, 0, FLOOR, W - 1, FLOOR, "K")
    pfill(im, 0, FLOOR + 15, W - 1, H - 1, SLAB); pfill(im, 0, FLOOR + 15, W - 1, FLOOR + 16, "K")
    return im


def finish(im):
    ambient(im, CEIL, FLOOR)
    pbox(im, 0, 0, W - 1, H - 1, "K")
    return im


def hole(im, rect):
    x0, y0, w, h = rect
    for x in range(x0, x0 + w):
        for y in range(y0, y0 + h):
            im.putpixel((x, y), (0, 0, 0, 0))


def frame(im, x0, y0, x1, y1, outer="K", mid1="d", mid2="D"):
    """구멍 둘레 창틀(바깥 K 2px, 나무 4px)."""
    cols = [outer, outer, mid1, mid2, mid2, mid1]
    for t, c in enumerate(cols):
        pbox(im, x0 + t, y0 + t, x1 - t, y1 - t, c)


def shadows(pr, spans):
    for (x0, x1) in spans:
        contact_shadow(pr, x0, x1, FLOOR + 1)


def bulb(im, x, y0=CEIL, drop=40):
    """맨 전구(전선 + 전구)."""
    pfill(im, x, y0, x + 1, y0 + drop, "K")
    box3(im, x - 4, y0 + drop, x + 5, y0 + drop + 6, "3", "W", "2", bevel=2)
    draw_ellipse(im, (x - 7, y0 + drop + 6, x + 8, y0 + drop + 22), "K")
    draw_ellipse(im, (x - 5, y0 + drop + 8, x + 6, y0 + drop + 20), "Y")
    pfill(im, x - 2, y0 + drop + 10, x, y0 + drop + 13, "W")


def hazard(im, x0, y0, x1, y1, a="Y", b="K"):
    """노랑·검정 빗금 띠."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            pset(im, x, y, a if ((x + y) // 4) % 2 == 0 else b)
    pfill(im, x0, y0, x1, y0, "K"); pfill(im, x0, y1, x1, y1, "K")


def crate(im, x0, y0, x1, y1, base="D", light="h", dark="d"):
    box3(im, x0, y0, x1, y1, base, light, dark, bevel=3)
    pfill(im, x0 + 3, (y0 + y1) // 2, x1 - 3, (y0 + y1) // 2 + 1, dark)
    pfill(im, (x0 + x1) // 2 - 1, y0 + 3, (x0 + x1) // 2, y1 - 3, dark)


def big_text(im, x, y, s, ch, k=3):
    """pixfont 글자를 k배 키워 그린다(간판용, 안티앨리어싱 없음)."""
    from pixfont import text_width
    tmp = canvas(text_width(s) + 1, 8)
    draw_text(tmp, 0, 0, s, ch)
    px = tmp.load()
    for gy in range(tmp.height):
        for gx in range(tmp.width):
            if px[gx, gy][3]:
                pfill(im, x + gx * k, y + gy * k, x + gx * k + k - 1, y + gy * k + k - 1, ch)


def pipe_h(im, y, x0=0, x1=W - 1, col="3", dark="2"):
    pfill(im, x0, y, x1, y + 7, col); pfill(im, x0, y + 6, x1, y + 7, dark); pfill(im, x0, y, x1, y, "K"); pfill(im, x0, y + 8, x1, y + 8, "K")


# ---------- 셋방 ----------
RENTAL_WIN = (440, 84, 112, 104)


def rental():
    sh = shell(floor="wood")
    # 낡은 벽지: 세로 줄 + 작은 꽃무늬(마름모 점)
    for x in range(0, W, 32):
        pfill(sh, x, CEIL, x + 1, FLOOR - 1, "f")
    for y in range(CEIL + 14, FLOOR - 24, 28):
        for x in range(16 + (14 if (y // 28) % 2 else 0), W, 32):
            pset(sh, x, y, "H"); pset(sh, x - 1, y + 1, "H"); pset(sh, x + 1, y + 1, "H"); pset(sh, x, y + 2, "H")
    dither_fill(sh, 330, 50, 400, 130, "f", 3)                  # 습기 얼룩
    dither_fill(sh, 40, 240, 120, 330, "f", 3)
    pfill(sh, 0, FLOOR - 20, W - 1, FLOOR - 19, "K")             # 걸레받이
    wood(sh, 0, FLOOR - 18, W - 1, FLOOR - 1, "d", "K", "D", plank=40, vertical=True)
    finish(sh)
    x0, y0, w, h = RENTAL_WIN
    hole(sh, RENTAL_WIN)
    frame(sh, x0 - 6, y0 - 6, x0 + w + 5, y0 + h + 5)
    for bx in range(x0 + 14, x0 + w - 6, 22):                    # 방범 창살
        pfill(sh, bx, y0, bx + 3, y0 + h - 1, "K"); pfill(sh, bx + 1, y0, bx + 1, y0 + h - 1, "U")
    pfill(sh, x0, y0 + 50, x0 + w - 1, y0 + 53, "K")
    box3(sh, x0 - 10, y0 + h + 6, x0 + w + 9, y0 + h + 16, "3", "W", "2", bevel=2)    # 창턱
    pbox(sh, 0, 0, W - 1, H - 1, "K")

    pr = canvas(W, H)
    # 빨랫줄(양 끝 고리 + 늘어진 줄)
    pts = [(10, 56), (130, 82), (260, 94), (390, 90), (510, 78), (632, 54)]
    for (px, py) in [pts[0], pts[-1]]:
        box3(pr, px - 5, py - 5, px + 5, py + 5, "3", "W", "2", bevel=2)
    draw_line(pr, pts, "K", 2)

    def y_at(x):
        for (ax, ay), (bx2, by2) in zip(pts, pts[1:]):
            if ax <= x <= bx2:
                return ay + (by2 - ay) * (x - ax) // (bx2 - ax)
        return pts[-1][1]

    def peg(x):
        y = y_at(x)
        pfill(pr, x, y - 2, x + 3, y + 6, "Y"); pfill(pr, x, y - 2, x, y + 6, "W"); pfill(pr, x + 3, y - 2, x + 3, y + 6, "y")

    def shirt(x, c, lt, dk):
        y = y_at(x + 16) + 6
        draw_poly(pr, [(x - 12, y + 12), (x + 4, y), (x + 12, y + 8), (x + 20, y), (x + 36, y + 12), (x + 30, y + 22), (x + 26, y + 18)], "K")
        box3(pr, x + 3, y + 8, x + 29, y + 50, c, lt, dk, bevel=2)
        pfill(pr, x - 9, y + 12, x + 4, y + 20, c); pfill(pr, x + 28, y + 12, x + 36, y + 20, c)
        pfill(pr, x + 13, y + 6, x + 19, y + 9, "K")
        peg(x + 5); peg(x + 25)

    def towel(x, c, lt, dk, w=26, h=56):
        y = y_at(x + w // 2) + 6
        box3(pr, x, y, x + w, y + h, c, lt, dk, bevel=2)
        for ly in range(y + 12, y + h - 4, 12):
            pfill(pr, x + 4, ly, x + w - 4, ly, dk)
        peg(x + 3); peg(x + w - 6)

    def socks(x):
        y = y_at(x) + 6
        for i, c in enumerate(("W", "Y")):
            box3(pr, x + i * 14, y, x + i * 14 + 8, y + 22, c, "W", "3", bevel=2)
            pfill(pr, x + i * 14, y + 18, x + i * 14 + 12, y + 24, c); pbox(pr, x + i * 14, y + 18, x + i * 14 + 12, y + 24, "K")
        peg(x + 2); peg(x + 16)

    shirt(34, "T", "W", "t")
    towel(96, "W", "W", "3", 24, 48)
    shirt(140, "R", "W", "r")
    socks(208)
    towel(250, "C", "W", "b", 28, 60)
    shirt(306, "Y", "W", "y")
    towel(372, "b", "C", "B", 22, 50)
    shirt(560, "T", "W", "t")
    # 접이 이불 더미(왼쪽) + 베개
    box3(pr, 18, 262, 170, FLOOR - 1, "i", "m", "f")
    box3(pr, 18, 300, 170, FLOOR - 1, "H", "U", "J")
    for x in range(36, 160, 22):
        pfill(pr, x, 268, x + 8, 270, "m")
    box3(pr, 40, 238, 118, 264, "W", "W", "3", bevel=2)          # 베개
    # 펴진 이불 + 담요(바닥)
    box3(pr, 200, 326, 396, FLOOR - 1, "U", "W", "H", bevel=3)
    draw_poly(pr, [(210, 326), (260, 306), (330, 304), (388, 326)], "K")
    draw_poly(pr, [(214, 326), (262, 310), (328, 308), (384, 326)], "m")
    pfill(pr, 230, 318, 330, 320, "s")
    for x in range(220, 380, 20):
        pfill(pr, x, 334, x + 8, 336, "H")
    # 낮은 상(차부이다) + 밥솥·컵·컵라면
    box3(pr, 430, 290, 580, 304, "D", "h", "d")
    pfill(pr, 440, 304, 448, FLOOR - 1, "K"); pfill(pr, 562, 304, 570, FLOOR - 1, "K")
    box3(pr, 446, 258, 486, 290, "W", "W", "3", bevel=2); pfill(pr, 452, 266, 480, 268, "R"); pfill(pr, 464, 250, 468, 258, "K")   # 밥솥
    box3(pr, 500, 262, 518, 290, "R", "W", "r", bevel=2)                                                                         # 컵라면
    pfill(pr, 502, 258, 516, 262, "W")
    box3(pr, 530, 274, 548, 290, "C", "W", "b", bevel=2)                                                                         # 컵
    # 작은 TV(나무 받침 위, 오른쪽 끝)
    box3(pr, 590, 306, 634, FLOOR - 1, "d", "D", "K")
    box3(pr, 586, 250, 636, 308, "K", "2", "K", bevel=2)
    pfill(pr, 592, 256, 622, 300, "t"); pfill(pr, 592, 256, 622, 258, "T")
    pfill(pr, 624, 262, 632, 266, "3"); pfill(pr, 624, 272, 632, 276, "3")
    draw_line(pr, [(600, 250), (592, 226)], "K", 2); draw_line(pr, [(620, 250), (630, 226)], "K", 2)                        # 안테나
    # 달력 · 옷걸이 못
    box3(pr, 196, 150, 236, 214, "W", "W", "3", bevel=2); pfill(pr, 196, 150, 236, 164, "R")
    for gy in range(172, 210, 8):
        pfill(pr, 202, gy, 230, gy, "3")
    draw_text(pr, 206, 154, "NOV", "W")
    shadows(pr, [(18, 170), (200, 396), (430, 580), (590, 634)])
    bulb(pr, 290, CEIL, 40)
    li = {"bulb_on": glow(W, H, 290, 96, 170, 170, "Y", y_min=CEIL, y_max=FLOOR),
          "tv_on": glow(W, H, 607, 278, 70, 54, "C", y_min=CEIL, y_max=FLOOR)}
    return sh, pr, li, [RENTAL_WIN]


# ---------- 끝 방 [레일] ----------

def endroom():
    sh = shell(floor="conc")
    # 석고 벗겨진 벽 + 드러난 벽돌
    dither_fill(sh, 70, 50, 190, 170, "f", 3)
    for y in range(184, 244, 10):
        off = 0 if (y // 10) % 2 else 14
        pfill(sh, 296, y, 424, y + 8, "d")
        pfill(sh, 296, y + 9, 424, y + 9, "f")
        for x in range(296 + off, 424, 28):
            pfill(sh, x, y, x, y + 8, "f")
    pbox(sh, 296, 184, 424, 243, "K")
    dither_fill(sh, 440, 70, 560, 150, "f", 3)
    # 양 끝 철골 기둥과 이음판(떼어 낼 수 있는 칸처럼)
    for (x0, x1) in ((0, 23), (616, 639)):
        pfill(sh, x0, CEIL, x1, FLOOR - 1, "H")
        pfill(sh, x0 + 2, CEIL, x1 - 2, FLOOR - 1, "U")
        pfill(sh, x0 + 2, CEIL, x0 + 5, FLOOR - 1, "W")          # 밝은 가장자리(광원 왼쪽 위)
        pfill(sh, x1 - 5, CEIL, x1 - 2, FLOOR - 1, "H")
    pfill(sh, 24, CEIL, 25, FLOOR - 1, "K"); pfill(sh, 614, CEIL, 615, FLOOR - 1, "K")   # 칸 사이 이음 틈
    for y in (60, 140, 220, 300):
        box3(sh, 0, y, 46, y + 22, "3", "W", "2", bevel=2)
        box3(sh, 594, y, 639, y + 22, "3", "W", "2", bevel=2)
        for bx in (10, 24, 36):
            pfill(sh, bx, y + 8, bx + 3, y + 11, "K"); pfill(sh, 594 + bx, y + 8, 594 + bx + 3, y + 11, "K")
    hazard(sh, 26, 316, 33, FLOOR - 1)
    hazard(sh, 606, 316, 613, FLOOR - 1)
    finish(sh)

    pr = canvas(W, H)
    # 쌓인 짐 상자
    crate(pr, 70, 296, 150, FLOOR - 1)
    crate(pr, 154, 308, 214, FLOOR - 1, "h", "W", "D")
    crate(pr, 84, 246, 142, 298, "D", "h", "d")
    crate(pr, 92, 204, 130, 248, "h", "W", "D")
    pfill(pr, 100, 272, 124, 280, "W"); pbox(pr, 100, 272, 124, 280, "K")                       # 상자 위 라벨
    # 버려진 서랍장(기울어짐)
    box3(pr, 250, 250, 366, FLOOR - 1, "d", "D", "K")
    for yy in (264, 292, 320):
        box3(pr, 258, yy, 358, yy + 22, "D", "h", "d", bevel=2); pfill(pr, 300, yy + 8, 316, yy + 11, "K")
    pfill(pr, 250, FLOOR - 4, 258, FLOOR - 1, "K")
    # 다리 부러진 의자(기대 놓임)
    box3(pr, 390, 296, 432, 308, "D", "h", "d", bevel=2)
    pfill(pr, 392, 308, 396, FLOOR - 1, "K"); pfill(pr, 426, 308, 430, FLOOR - 14, "K")
    box3(pr, 392, 244, 398, 298, "D", "h", "d", bevel=2); box3(pr, 424, 244, 430, 298, "D", "h", "d", bevel=2)
    pfill(pr, 398, 252, 424, 258, "d")
    # 기대어 세운 매트리스 + 말린 카펫
    draw_poly(pr, [(470, FLOOR - 1), (560, FLOOR - 1), (566, 150), (498, 150)], "K")
    draw_poly(pr, [(474, FLOOR - 4), (556, FLOOR - 4), (562, 156), (502, 156)], "U")
    draw_poly(pr, [(474, FLOOR - 4), (484, FLOOR - 4), (506, 156), (502, 156)], "W")
    for y in range(180, FLOOR - 20, 36):
        pfill(pr, 490 + (FLOOR - y) // 12, y, 556 - (y - 150) // 16 + 2, y + 1, "H")
    box3(pr, 572, 234, 598, FLOOR - 1, "m", "s", "i", bevel=3)                                   # 말린 카펫
    draw_ellipse(pr, (572, 226, 598, 244), "K"); draw_ellipse(pr, (575, 229, 595, 241), "s")
    pfill(pr, 574, 280, 596, 281, "K"); pfill(pr, 574, 320, 596, 321, "K")
    # 천장에 매달린 끊긴 전선 + 전구
    shadows(pr, [(70, 214), (250, 366), (390, 432), (470, 560), (572, 598)])
    bulb(pr, 210, CEIL, 34)
    li = {"bulb_on": glow(W, H, 210, 90, 190, 170, "Y", y_min=CEIL, y_max=FLOOR)}
    return sh, pr, li, []


# ---------- 상점(24시) ----------
SHOP_WIN = (404, 100, 192, 226)


def shop():
    sh = shell("J", "f", "tile")
    pfill(sh, 0, 236, W - 1, FLOOR - 1, "H")                     # 하단 타일 벽
    for y in range(244, FLOOR - 4, 16):
        pfill(sh, 0, y, W - 1, y, "J")
    for x in range(0, W, 32):
        pfill(sh, x, 236, x, FLOOR - 1, "J")
    pfill(sh, 0, 232, W - 1, 235, "U"); pfill(sh, 0, 235, W - 1, 235, "K")
    finish(sh)
    x0, y0, w, h = SHOP_WIN
    hole(sh, SHOP_WIN)
    frame(sh, x0 - 6, y0 - 6, x0 + w + 5, y0 + h + 5, "K", "2", "3")
    pfill(sh, x0 + 94, y0, x0 + 97, y0 + h - 1, "K"); pfill(sh, x0 + 95, y0, x0 + 95, y0 + h - 1, "U")   # 유리 문설주
    pfill(sh, x0 + 20, y0 + 8, x0 + 21, y0 + 80, "W"); pfill(sh, x0 + 30, y0 + 8, x0 + 30, y0 + 40, "W")  # 유리 반사
    pfill(sh, x0 + 120, y0 + 20, x0 + 121, y0 + 100, "W")
    box3(sh, x0 - 12, y0 + h + 6, x0 + w + 11, y0 + h + 18, "3", "W", "2", bevel=2)                      # 진열 턱
    pbox(sh, 0, 0, W - 1, H - 1, "K")

    pr = canvas(W, H)
    # 냉장고(유리문 3칸, 안에 음료)
    box3(pr, 14, 132, 126, FLOOR - 1, "3", "W", "2")
    for i, fx in enumerate((20, 52, 84)):
        pfill(pr, fx, 140, fx + 28, FLOOR - 8, "K"); pfill(pr, fx + 2, 142, fx + 26, FLOOR - 10, "B")
        for ry in (150, 210, 270):
            pfill(pr, fx + 2, ry + 44, fx + 26, ry + 46, "3")
            for bx in range(fx + 5, fx + 24, 7):
                pfill(pr, bx, ry + 20, bx + 3, ry + 43, ("C", "G", "Y", "R")[(bx // 7 + i + ry // 60) % 4])
        pfill(pr, fx + 3, 144, fx + 4, 330, "W")
        pfill(pr, fx + 24, 220, fx + 25, 240, "K")
    pfill(pr, 14, 120, 126, 132, "2"); pbox(pr, 14, 120, 126, 132, "K"); draw_text(pr, 24, 123, "COLD", "C")
    # 진열 선반 3단(과자·라면)
    box3(pr, 144, 132, 268, FLOOR - 1, "2", "3", "4")
    for r, ry in enumerate((140, 196, 252)):
        pfill(pr, 148, ry + 44, 264, ry + 48, "K"); pfill(pr, 148, ry + 44, 264, ry + 45, "U")
        x = 150
        for i in range(8):
            bw = 12 + (i * 5 + r * 3) % 6
            bh = 26 + (i * 7 + r * 5) % 14
            c = ("R", "Y", "C", "G", "T", "W", "m", "h")[(i + r * 3) % 8]
            box3(pr, x, ry + 44 - bh, x + bw, ry + 43, c, "W", "K", bevel=2)
            x += bw + 2
            if x > 252:
                break
    box3(pr, 144, 300, 268, 308, "3", "W", "2", bevel=2)
    # 계산대 + 계산기
    box3(pr, 290, 276, 396, FLOOR - 1, "d", "D", "K")
    box3(pr, 284, 262, 402, 278, "D", "h", "d")
    box3(pr, 310, 226, 358, 262, "K", "2", "K", bevel=2); pfill(pr, 316, 232, 352, 252, "g"); pfill(pr, 320, 236, 340, 238, "G")   # 단말기
    box3(pr, 366, 246, 394, 262, "W", "W", "3", bevel=2)
    # 간판(24시) — 벽에 걸린 박스 간판. 빛은 L3 하나
    box3(pr, 262, 44, 418, 100, "K", "2", "K", bevel=2)
    pfill(pr, 268, 50, 412, 94, "r")
    big_text(pr, 278, 52, "24", "Y", 4)
    draw_text(pr, 340, 54, "OPEN", "W"); draw_text(pr, 340, 66, "ALL DAY", "Y")
    pfill(pr, 336, 78, 410, 79, "R"); pfill(pr, 336, 88, 410, 89, "R")
    # 유리 안쪽 진열(창 앞 낮은 선반)과 광고 전단
    box3(pr, 408, 296, 592, 312, "D", "h", "d")
    for i, bx in enumerate(range(416, 584, 22)):
        box3(pr, bx, 270 - (i % 3) * 6, bx + 14, 294, ("R", "Y", "C", "G", "T", "W", "m", "h")[i % 8], "W", "K", bevel=2)
    pfill(pr, 410, 312, 416, FLOOR - 1, "K"); pfill(pr, 584, 312, 590, FLOOR - 1, "K")
    box3(pr, 430, 124, 470, 170, "W", "W", "3", bevel=2); draw_text(pr, 436, 130, "SALE", "R")
    for ly in range(142, 166, 6):
        pfill(pr, 436, ly, 464, ly, "2")
    # 문 앞 매트
    pfill(pr, 600, FLOOR - 4, 636, FLOOR - 1, "r")
    shadows(pr, [(14, 126), (144, 268), (290, 396), (408, 592)])
    li = {"sign_on": glow(W, H, 340, 72, 160, 76, "R", y_min=CEIL, y_max=FLOOR)}
    return sh, pr, li, [SHOP_WIN]


# ---------- 빈 점포 [레일] ----------
VAC_WIN = (404, 160, 192, 166)


def vacant():
    sh = shell("J", "f", "tile")
    # 선반이 있던 자리의 얼룩(밝은 자국)과 접착 자국
    for (x, y, w, h) in [(30, 140, 130, 190), (180, 140, 130, 190)]:
        pfill(sh, x, y, x + w, y + h, "H")
        dither_fill(sh, x, y, x + w, y + h, "J", 2)
        pbox(sh, x, y, x + w, y + h, "f")
    dither_fill(sh, 330, 60, 380, 120, "f", 3)
    finish(sh)
    x0, y0, w, h = VAC_WIN
    hole(sh, VAC_WIN)
    frame(sh, x0 - 6, y0 - 6, x0 + w + 5, y0 + h + 5, "K", "2", "3")
    pfill(sh, x0 + 94, y0, x0 + 97, y0 + h - 1, "K"); pfill(sh, x0 + 95, y0, x0 + 95, y0 + h - 1, "U")
    # 반쯤 내린 셔터(위쪽): 판 + 바닥 막대 + 손잡이
    sy1 = y0 - 6
    pfill(sh, x0 - 6, 80, x0 + w + 5, sy1 + 4, "U")
    for y in range(80, sy1 + 4, 8):
        pfill(sh, x0 - 6, y, x0 + w + 5, y, "K"); pfill(sh, x0 - 6, y + 1, x0 + w + 5, y + 1, "3")
        pfill(sh, x0 - 6, y + 6, x0 + w + 5, y + 7, "H")
    for sx in range(x0 - 4, x0 + w + 4, 48):
        pfill(sh, sx, 80, sx + 1, sy1 + 4, "K")
    box3(sh, x0 - 8, sy1 + 4, x0 + w + 7, sy1 + 14, "3", "W", "2", bevel=2)
    box3(sh, x0 + 84, sy1 + 6, x0 + 108, sy1 + 12, "Y", "W", "y", bevel=2)
    pfill(sh, x0 - 6, 80, x0 + w + 5, 80, "K")
    # 셔터 흘러내린 얼룩(벽에 녹물)
    for rx in (x0 - 4, x0 + 150):
        for ry in range(sy1 + 18, sy1 + 40, 4):
            pfill(sh, rx, ry, rx + 1, ry + 2, "d")
    box3(sh, x0 - 12, y0 + h + 6, x0 + w + 11, y0 + h + 18, "3", "W", "2", bevel=2)
    pbox(sh, 0, 0, W - 1, H - 1, "K")

    pr = canvas(W, H)
    # 빈 진열대 2개(틀만)
    for sx in (30, 180):
        pfill(pr, sx, 140, sx + 5, FLOOR - 1, "K"); pfill(pr, sx + 2, 140, sx + 3, FLOOR - 1, "3")
        pfill(pr, sx + 125, 140, sx + 130, FLOOR - 1, "K"); pfill(pr, sx + 127, 140, sx + 128, FLOOR - 1, "3")
        for ry in (180, 230, 280, FLOOR - 12):
            box3(pr, sx, ry, sx + 130, ry + 7, "3", "W", "2", bevel=2)
    # 남은 물건 몇 개(쓰러진 상자·빈 병)
    crate(pr, 60, 318, 124, FLOOR - 1)
    crate(pr, 214, 322, 258, FLOOR - 1, "h", "W", "D")
    box3(pr, 126, 236, 150, 280, "W", "W", "3", bevel=2); pfill(pr, 130, 244, 146, 262, "R")
    box3(pr, 272, 332, 292, FLOOR - 1, "C", "W", "b", bevel=2)
    # 계산대(비어 있음)
    box3(pr, 328, 296, 400, FLOOR - 1, "d", "D", "K")
    box3(pr, 322, 282, 406, 298, "D", "h", "d")
    # 임대 전단(유리 안쪽 붙임) + 테이프
    box3(pr, 440, 190, 508, 262, "W", "W", "3", bevel=2)
    draw_text(pr, 452, 198, "FOR", "R"); draw_text(pr, 452, 210, "RENT", "R")
    for ly in range(228, 258, 6):
        pfill(pr, 448, ly, 500, ly, "2")
    pfill(pr, 464, 188, 484, 192, "Y"); pfill(pr, 464, 260, 484, 264, "Y")
    # 떨어지다 만 간판(한쪽 볼트만 남음)
    draw_poly(pr, [(40, 48), (250, 70), (250, 100), (40, 78)], "K")
    draw_poly(pr, [(44, 52), (246, 73), (246, 96), (44, 75)], "2")
    pfill(pr, 40, 44, 44, 50, "3")
    draw_text(pr, 66, 58, "CLOSED", "Y")
    draw_line(pr, [(250, 72), (250, 38)], "K", 2)
    # 닫힌 문턱 신문 뭉치
    box3(pr, 560, 336, 620, FLOOR - 1, "W", "W", "3", bevel=2)
    for ly in range(340, 350, 4):
        pfill(pr, 566, ly, 614, ly, "3")
    shadows(pr, [(30, 160), (180, 310), (328, 400), (560, 620)])
    li = {"tube_on": light_cone(W, H, 160, CEIL + 38, 40, 130, FLOOR, "3")}
    return sh, pr, li, [VAC_WIN]


# ---------- 옥상 복도 ----------
ROOFHALL_WIN = (24, 56, 440, 248)


def roofhall():
    sh = shell("H", "U", "conc")
    # 가건물 골함석 벽(오른쪽) + 바깥 쪽 콘크리트 난간벽
    for x in range(464, W):
        c = "U" if (x // 8) % 2 == 0 else "H"
        pfill(sh, x, CEIL, x, FLOOR - 1, c)
        if x % 8 == 0:
            pfill(sh, x, CEIL, x, FLOOR - 1, "K")
    pfill(sh, 464, CEIL, 467, FLOOR - 1, "K")
    pfill(sh, 0, 304, 463, FLOOR - 1, "H"); dither_fill(sh, 0, 306, 463, FLOOR - 1, "f", 3)
    pfill(sh, 0, 300, 463, 303, "U"); pfill(sh, 0, 303, 463, 303, "K")
    finish(sh)
    x0, y0, w, h = ROOFHALL_WIN
    hole(sh, ROOFHALL_WIN)
    for t in range(4):                                           # 구멍 윗틀(처마 보)
        pbox(sh, x0 - 6 + t, y0 - 6 + t, x0 + w + 5 - t, y0 + h + 5 - t, "K" if t < 2 else ("3" if t == 2 else "2"))
    for px in (x0 + 140, x0 + 290):                              # 철 기둥
        pfill(sh, px, y0, px + 9, y0 + h - 1, "K"); pfill(sh, px + 2, y0, px + 7, y0 + h - 1, "U"); pfill(sh, px + 2, y0, px + 3, y0 + h - 1, "W")
    pfill(sh, x0, y0 + h - 40, x0 + w - 1, y0 + h - 34, "K"); pfill(sh, x0, y0 + h - 39, x0 + w - 1, y0 + h - 37, "3")   # 난간 손잡이
    for bx in range(x0 + 8, x0 + w, 24):
        pfill(sh, bx, y0 + h - 34, bx + 2, y0 + h - 1, "K")
    pbox(sh, 0, 0, W - 1, H - 1, "K")

    pr = canvas(W, H)
    # 천장 배관 2줄
    pipe_h(pr, CEIL + 2, 0, 463, "3", "2"); pipe_h(pr, CEIL + 14, 0, 463, "d", "K")
    for x in range(60, 464, 150):
        box3(pr, x, CEIL, x + 12, CEIL + 24, "3", "W", "2", bevel=2)
    # 실외기 2대
    for ax in (30, 330):
        box3(pr, ax, 262, ax + 84, FLOOR - 1, "3", "W", "2")
        draw_ellipse(pr, (ax + 12, 270, ax + 72, 330), "K"); draw_ellipse(pr, (ax + 16, 274, ax + 68, 326), "2")
        draw_line(pr, [(ax + 42, 276), (ax + 42, 322)], "K", 2); draw_line(pr, [(ax + 20, 300), (ax + 64, 300)], "K", 2)
        pfill(pr, ax + 4, 336, ax + 80, 338, "2")
        for py in range(CEIL + 24, 262, 10):                     # 연결 배관
            pfill(pr, ax + 78, py, ax + 80, py + 6, "K")
    # TV 안테나(기둥 + 가로대)
    pfill(pr, 226, 160, 230, 300, "K"); pfill(pr, 227, 160, 228, 300, "3")
    for (yy, hw) in [(172, 34), (194, 28), (216, 22), (238, 16)]:
        pfill(pr, 228 - hw, yy, 228 + hw, yy + 2, "K"); pfill(pr, 228 - hw, yy, 228 + hw, yy, "W")
    pfill(pr, 218, 150, 238, 160, "R"); pbox(pr, 218, 150, 238, 160, "K")
    # 물탱크(가건물 쪽 선반 위): 원통 + 밴드 + 사다리
    pfill(pr, 478, 196, 484, FLOOR - 1, "K"); pfill(pr, 612, 196, 618, FLOOR - 1, "K")                  # 다리
    box3(pr, 474, 200, 622, 212, "3", "W", "2", bevel=2)
    box3(pr, 484, 98, 612, 200, "U", "W", "H")
    draw_ellipse(pr, (484, 88, 612, 112), "K"); draw_ellipse(pr, (488, 91, 608, 108), "W")
    for yy in (126, 160):
        pfill(pr, 485, yy, 611, yy + 3, "K"); pfill(pr, 485, yy, 611, yy, "3")
    rivets(pr, 490, 100, 606, 196, 14, "W")
    box3(pr, 536, 112, 560, 122, "R", "W", "r", bevel=2)                                                  # 밸브
    pfill(pr, 566, 212, 572, FLOOR - 1, "3"); pfill(pr, 566, 212, 567, FLOOR - 1, "W"); pbox(pr, 566, 212, 572, FLOOR - 1, "K")  # 배수관
    for ly in range(224, FLOOR - 8, 14):
        pfill(pr, 504, ly, 524, ly + 2, "K")
    pfill(pr, 504, 212, 506, FLOOR - 1, "K"); pfill(pr, 522, 212, 524, FLOOR - 1, "K")                     # 사다리
    # 벽 경고등(가건물 입구 쪽)
    box3(pr, 452, 170, 468, 190, "2", "3", "K", bevel=2)
    draw_ellipse(pr, (446, 150, 474, 176), "K"); draw_ellipse(pr, (449, 153, 471, 173), "R"); pfill(pr, 452, 156, 456, 160, "W")
    shadows(pr, [(30, 114), (330, 414), (478, 622)])
    lamp_x = 540
    li = {"warn_on": glow(W, H, 460, 164, 70, 60, "R", y_min=CEIL, y_max=FLOOR),
          "shed_on": light_cone(W, H, lamp_x, CEIL + 30, 20, 100, FLOOR, "h")}
    # 가건물 천장 등 기구
    box3(pr, lamp_x - 24, CEIL + 24, lamp_x + 24, CEIL + 32, "3", "W", "2", bevel=2)
    pfill(pr, lamp_x - 18, CEIL + 33, lamp_x + 18, CEIL + 35, "W")
    return sh, pr, li, [ROOFHALL_WIN]


# ---------- 옥상 출구 방 [레일] ----------
ROOFEXIT_WIN = (252, 72, 104, 84)


def roofexit():
    sh = shell("J", "f", "conc")
    for x in range(0, W, 12):                                    # 아래쪽 골함석 판
        pfill(sh, x, 236, x + 5, FLOOR - 1, "U" if (x // 12) % 2 == 0 else "H")
        pfill(sh, x + 5, 236, x + 5, FLOOR - 1, "K")
    pfill(sh, 0, 230, W - 1, 235, "3"); pfill(sh, 0, 235, W - 1, 235, "K")
    dither_fill(sh, 440, 56, 540, 110, "f", 3)
    finish(sh)
    x0, y0, w, h = ROOFEXIT_WIN
    hole(sh, ROOFEXIT_WIN)
    frame(sh, x0 - 6, y0 - 6, x0 + w + 5, y0 + h + 5, "K", "2", "3")
    pfill(sh, x0 + 50, y0, x0 + 53, y0 + h - 1, "K"); pfill(sh, x0, y0 + 40, x0 + w - 1, y0 + 43, "K")
    box3(sh, x0 - 10, y0 + h + 6, x0 + w + 9, y0 + h + 16, "3", "W", "2", bevel=2)
    pbox(sh, 0, 0, W - 1, H - 1, "K")

    pr = canvas(W, H)
    # 철제 선반 + 페인트 통·공구 상자(왼쪽)
    for y in (140, 220, FLOOR - 10):
        box3(pr, 24, y, 206, y + 8, "3", "W", "2", bevel=2)
    pfill(pr, 24, 100, 28, FLOOR - 1, "K"); pfill(pr, 202, 100, 206, FLOOR - 1, "K")
    for (bx, by, bw, bh, c) in [(34, 100, 36, 40, "D"), (74, 110, 40, 30, "w"), (120, 104, 28, 36, "h"), (152, 112, 40, 28, "D")]:
        box3(pr, bx, by, bx + bw, by + bh, c, "h", "d", bevel=2)
    for x in (36, 66, 96):
        box3(pr, x, 184, x + 24, 220, "3", "W", "2", bevel=2); pfill(pr, x + 2, 194, x + 22, 198, ("R", "Y", "C")[(x // 30) % 3])
    box3(pr, 130, 190, 190, 220, "R", "W", "r"); pfill(pr, 130, 202, 190, 204, "K"); pfill(pr, 154, 196, 166, 200, "3")
    for x in (44, 80, 116):
        box3(pr, x, 300, x + 28, FLOOR - 12, "D", "h", "d", bevel=2)
    # 양동이 + 대걸레
    box3(pr, 228, 306, 268, FLOOR - 1, "3", "W", "2", bevel=3)
    pfill(pr, 226, 302, 270, 306, "2"); pbox(pr, 226, 302, 270, 306, "K")
    draw_line(pr, [(262, 306), (292, 180)], "d", 5)
    draw_poly(pr, [(284, 168), (306, 176), (300, 200), (278, 192)], "W")
    # 가운데 쌓인 타일 포대 + 벽 전기 박스
    crate(pr, 330, 304, 402, FLOOR - 1, "3", "W", "2")
    crate(pr, 338, 258, 390, 306, "D", "h", "d")
    box3(pr, 440, 120, 492, 206, "3", "W", "2")
    pfill(pr, 446, 126, 486, 200, "2"); pfill(pr, 466, 126, 468, 200, "K")
    pfill(pr, 452, 140, 460, 146, "R"); pfill(pr, 474, 140, 482, 146, "G")
    pfill(pr, 440, 120, 492, 120, "K")
    # 환기구(벽 위) · 배관
    box3(pr, 520, 50, 600, 84, "3", "W", "2")
    for gy in range(56, 80, 6):
        pfill(pr, 526, gy, 594, gy + 2, "K")
    pipe_h(pr, CEIL + 2, 0, W - 1, "d", "K")
    # (x 520~620, 바닥~y 150 부근은 출구 오브젝트 자리: 아무것도 그리지 않음)
    shadows(pr, [(24, 206), (228, 268), (330, 402)])
    bulb(pr, 300, CEIL + 8, 30)
    li = {"bulb_on": glow(W, H, 300, 90, 190, 170, "Y", y_min=CEIL, y_max=FLOOR)}
    return sh, pr, li, [ROOFEXIT_WIN]


# ---------- 레일 ----------

def rail():
    """밀기 표시(레일): 천장 슬래브(y 0~27)·바닥 슬래브(y 353~383) 구역에만. 방 안쪽은 덮지 않는다."""
    im = canvas(W, H)

    def bar(y):
        pfill(im, 0, y, W - 1, y + 5, "K")
        pfill(im, 0, y + 1, W - 1, y + 1, "W"); pfill(im, 0, y + 2, W - 1, y + 3, "3"); pfill(im, 0, y + 4, W - 1, y + 4, "2")

    def roller(x, y0, y1):
        pfill(im, x, y0, x + 23, y1, "K")
        pfill(im, x + 2, y0 + 2, x + 21, y1 - 2, "U")
        pfill(im, x + 2, y0 + 2, x + 21, y0 + 3, "W"); pfill(im, x + 2, y0 + 2, x + 3, y1 - 2, "W")
        pfill(im, x + 2, y1 - 3, x + 21, y1 - 2, "H"); pfill(im, x + 20, y0 + 2, x + 21, y1 - 2, "H")
        for bx in (x + 6, x + 15):
            pfill(im, bx, (y0 + y1) // 2 - 1, bx + 2, (y0 + y1) // 2 + 1, "K")

    for y in (3, 15):                                            # 천장 레일 2줄
        bar(y)
    for y in (357, 372):                                         # 바닥 레일 2줄
        bar(y)
    for x in range(40, W - 24, 160):
        roller(x, 1, 23)
        roller(x, 355, 380)
    hazard(im, 240, 22, 336, 25)                                 # 경고 띠(천장·바닥 가운데)
    hazard(im, 240, 363, 336, 366)
    return im


# ---------- 빌드 ----------
ROOMS = {"ot_rental": rental, "ot_endroom": endroom, "ot_shop": shop, "ot_vacant": vacant,
         "ot_roofhall": roofhall, "ot_roofexit": roofexit}


def build():
    made = []
    for key, fn in ROOMS.items():
        sh, pr, li, wins = fn()
        for name, im in [("L1_shell", sh), ("L2_props", pr)] + [(f"L3_light_{k}", v) for k, v in li.items()]:
            assert not palette_check(im), (key, name)
            meta = {"status": "시안", "layer": name[:2], "size": [W, H], "floor_y": FLOOR}
            if name == "L1_shell":
                meta["windows"] = [list(r) for r in wins]
            made.append(save(im, f"rooms128/{key}/{name}.png", meta))
        dk = dark_overlay(W, H, CEIL, FLOOR)                    # 조명이 모두 꺼졌을 때(창 범위 제외)
        for r in wins:
            hole(dk, r)
        assert not palette_check(dk), key
        made.append(save(dk, f"rooms128/{key}/L3_dark.png", {"status": "시안", "layer": "L3", "use": "조명이 모두 꺼졌을 때"}))
        comp = canvas(W, H, PIX["e"]); comp.alpha_composite(sh); comp.alpha_composite(pr)
        for v in li.values():
            comp.alpha_composite(v)
        made.append(save(comp, f"preview/rooms128_{key}.png", {"status": "미리보기"}))
    r = rail()
    assert not palette_check(r), "rail"
    made.append(save(r, "rooms128/common/rail.png", {"status": "시안", "layer": "rail", "size": [W, H], "use": "밀기 표시(레일), L3·dark 위 L4 아래"}))
    return made
