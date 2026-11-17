"""챕터 0 주인공 건물의 나머지 방 128px 시안(섹터 640×384) — D-073, D-075, D-078.

로비·계단실·지하 창고·보일러실·엘리베이터. 주인공 방(room128_home)·복도(room128_corridor)와 같은 규칙:
광원 왼쪽 위, 명암 3단, 바깥 외곽선 2px(K), 안티앨리어싱 없음, 공용 팔레트만.
계단은 걷는 구조물이라 L1(방 껍데기)에 넣고, 셔터·엘리베이터 칸처럼 상태·위치가 바뀌는 것은 L4·L5로 따로 낸다.
"""
from artkit import PIX, canvas, draw_ellipse, draw_line, draw_poly, palette_check, pbox, pfill, pset, save
from pixfont import draw_text
from prop import ambient, box3, contact_shadow, dark_overlay, dither_fill, glow, light_cone, rivets, wood

W, H = 640, 384
FLOOR, CEIL = 352, 28


# ---------- 공통 껍데기 ----------

def base_shell(wall, seam, floor="tile", wall_dark=None):
    im = canvas(W, H, PIX[wall])
    for x in range(0, W, 64):
        pfill(im, x, CEIL, x, FLOOR - 1, seam)
    pfill(im, 0, 0, W - 1, CEIL - 1, "2"); pfill(im, 0, CEIL - 2, W - 1, CEIL - 1, "K")
    if floor == "tile":
        for x in range(0, W, 32):
            pfill(im, x, FLOOR, x + 31, FLOOR + 14, "2" if (x // 32) % 2 else "4")
    elif floor == "wood":
        wood(im, 0, FLOOR, W - 1, FLOOR + 14, "D", "d", "h", plank=7)
    else:                                                       # 콘크리트
        pfill(im, 0, FLOOR, W - 1, FLOOR + 14, "4")
        dither_fill(im, 0, FLOOR + 2, W - 1, FLOOR + 14, "0", 5)
    pfill(im, 0, FLOOR, W - 1, FLOOR, "K")
    pfill(im, 0, FLOOR + 15, W - 1, H - 1, "2"); pfill(im, 0, FLOOR + 15, W - 1, FLOOR + 16, "K")
    return im


def finish(im):
    ambient(im, CEIL, FLOOR)                                    # 환경 음영(D-079)
    pbox(im, 0, 0, W - 1, H - 1, "K")
    return im


def hole(im, rect):
    x0, y0, w, h = rect
    for x in range(x0, x0 + w):
        for y in range(y0, y0 + h):
            im.putpixel((x, y), (0, 0, 0, 0))


def shadows(pr, spans):
    for (x0, x1) in spans:
        contact_shadow(pr, x0, x1, FLOOR + 1)


WINDOWS = {"lobby": [(W - 476 - 119, 126, 119, FLOOR - 126)]}  # 유리문(바깥이 보이는 구멍) — D-080. 로비는 좌우 반전(건물 왼쪽 바깥벽에 현관)


def mirror(im):
    from PIL import ImageOps
    return ImageOps.mirror(im)


def ceiling_lamp(im, x, shade="3"):
    """천장 매달린 등(기구만, 빛은 L3)."""
    pfill(im, x - 1, CEIL, x, CEIL + 22, "K")
    draw_poly(im, [(x - 22, CEIL + 22), (x + 22, CEIL + 22), (x + 30, CEIL + 38), (x - 30, CEIL + 38)], "K")
    draw_poly(im, [(x - 20, CEIL + 24), (x + 20, CEIL + 24), (x + 26, CEIL + 36), (x - 26, CEIL + 36)], shade)
    pfill(im, x - 18, CEIL + 24, x - 6, CEIL + 26, "W")
    pfill(im, x - 6, CEIL + 38, x + 6, CEIL + 42, "Y")


def bulb(im, x, y0=CEIL):
    """맨 전구(전선 + 전구)."""
    pfill(im, x, y0, x + 1, y0 + 40, "K")
    box3(im, x - 4, y0 + 40, x + 5, y0 + 46, "3", "W", "2", bevel=2)
    draw_ellipse(im, (x - 7, y0 + 46, x + 8, y0 + 62), "K")
    draw_ellipse(im, (x - 5, y0 + 48, x + 6, y0 + 60), "Y")
    pfill(im, x - 2, y0 + 50, x, y0 + 53, "W")


# ---------- 로비 ----------

def lobby():
    sh = base_shell("1", "0", "tile")
    pfill(sh, 0, 230, W - 1, FLOOR - 1, "4")                     # 하단 타일 벽
    for y in range(238, FLOOR - 4, 16):
        pfill(sh, 0, y, W - 1, y, "0")
    for x in range(0, W, 32):
        pfill(sh, x, 230, x, FLOOR - 1, "0")
    pfill(sh, 0, 226, W - 1, 229, "3"); pfill(sh, 0, 229, W - 1, 229, "K")
    # 유리 현관문(정면 오른쪽) — 바깥 거리 불빛
    pfill(sh, 470, 120, 600, FLOOR - 1, "K")
    finish(sh)
    hole(sh, (476, 126, 119, FLOOR - 126))                       # 유리 너머 거리는 엔진이 L0·LE로(반전 전 좌표)
    pfill(sh, 534, 126, 536, FLOOR - 1, "2")
    pfill(sh, 482, 140, 483, 330, "W")                           # 유리 반사(L1에 남김)
    pfill(sh, 548, 150, 549, 300, "W")

    pr = canvas(W, H)
    # 안내 데스크
    box3(pr, 60, 250, 260, FLOOR - 1, "d", "D", "K")
    box3(pr, 52, 240, 268, 252, "D", "h", "d")
    for x in range(76, 250, 40):
        pbox(pr, x, 262, x + 28, 334, "K")
    box3(pr, 150, 206, 214, 240, "K", "2", "K", bevel=2); pfill(pr, 156, 212, 208, 234, "t")   # 낡은 모니터(꺼짐 화면)
    for yy in (218, 226):
        pfill(pr, 160, yy, 196, yy + 1, "T")
    box3(pr, 80, 226, 110, 240, "W", "W", "3", bevel=2)                                      # 방문 대장
    box3(pr, 226, 222, 240, 240, "Y", "W", "y", bevel=2)                                     # 종
    # 벽시계 · 층 안내판
    draw_ellipse(pr, (110, 70, 160, 120), "K"); draw_ellipse(pr, (114, 74, 156, 116), "W")
    draw_line(pr, [(135, 95), (135, 80)], "K", 2); draw_line(pr, [(135, 95), (146, 100)], "K", 2)
    box3(pr, 220, 70, 360, 150, "B", "b", "K")
    for i, t in enumerate(["3F  ROOMS", "2F  ROOMS", "1F  LOBBY", "B1  BOILER"]):
        draw_text(pr, 232, 82 + i * 16, t, "W" if i != 2 else "Y")
    # 대기 의자 2개
    for cx in (300, 360):
        box3(pr, cx, 290, cx + 44, 304, "r", "R", "K", bevel=2)
        box3(pr, cx + 4, 250, cx + 40, 292, "r", "R", "K", bevel=2)
        pfill(pr, cx + 4, 304, cx + 8, FLOOR - 1, "K"); pfill(pr, cx + 36, 304, cx + 40, FLOOR - 1, "K")
    # 큰 화분
    box3(pr, 420, 300, 456, FLOOR - 1, "d", "D", "K")
    for (x, y, r) in [(438, 270, 16), (424, 252, 12), (452, 248, 12), (438, 232, 10)]:
        draw_ellipse(pr, (x - r, y - r, x + r, y + r), "g"); draw_ellipse(pr, (x - r + 4, y - r + 3, x + r - 6, y + r - 6), "G")
    # 매트
    pfill(pr, 480, FLOOR - 4, 590, FLOOR - 1, "r")
    shadows(pr, [(60, 260), (300, 344), (360, 404), (420, 456)])
    for lx in (160, 400):                                        # 조명 기구는 반전 전에 그림
        ceiling_lamp(pr, lx)
    sh, pr = mirror(sh), mirror(pr)
    bx = W - 360                                                 # 반전된 층 안내판 자리에 글자를 다시 씀
    box3(pr, bx, 70, bx + 140, 150, "B", "b", "K")
    for i, t in enumerate(["3F  ROOMS", "2F  ROOMS", "1F  LOBBY", "B1  BOILER"]):
        draw_text(pr, bx + 12, 82 + i * 16, t, "W" if i != 2 else "Y")
    return sh, pr, {"ceiling_a_on": light_cone(W, H, W - 160, CEIL + 42, 26, 120, FLOOR, "h"),
                    "ceiling_b_on": light_cone(W, H, W - 400, CEIL + 42, 26, 110, FLOOR, "h"),
                    "desk_screen_on": glow(W, H, W - 182, 222, 44, 26, "T", y_min=CEIL, y_max=FLOOR)}, []


# ---------- 계단실 ----------

def stairwell():
    sh = base_shell("0", "K", "conc")
    dither_fill(sh, 0, CEIL, W - 1, 120, "1", 4)
    # 계단(왼쪽 아래 → 오른쪽 위, 위층으로) — 걷는 구조물
    step_w, step_h = 32, 20
    for i in range(11):
        x0 = 120 + i * step_w; y0 = FLOOR - (i + 1) * step_h
        box3(sh, x0, y0, x0 + step_w + 1, FLOOR - i * step_h, "3", "W", "2", bevel=2)
        pfill(sh, x0 + 2, y0 + 2, x0 + step_w - 1, y0 + 3, "Y" if i % 2 == 0 else "y")   # 미끄럼 방지 띠
    pfill(sh, 120, FLOOR - 220, 120 + 11 * step_w, FLOOR - 220 + 2, "K")
    draw_poly(sh, [(120, FLOOR), (120 + 11 * step_w, FLOOR - 11 * step_h), (120 + 11 * step_w, FLOOR)], "4")
    for i in range(11):                                          # 계단 아래 그림자 결
        pfill(sh, 120 + i * step_w + 6, FLOOR - i * step_h + 2, 120 + i * step_w + 7, FLOOR - 2, "2")
    # 층참 (위쪽 오른편)
    box3(sh, 472, FLOOR - 230, W - 1, FLOOR - 218, "3", "W", "2", bevel=2)
    finish(sh)

    pr = canvas(W, H)
    # 난간(계단 따라)
    pts = [(120 + i * step_w + 16, FLOOR - (i + 1) * step_h - 52) for i in range(11)]
    draw_line(pr, [(pts[0][0], pts[0][1]), (pts[-1][0], pts[-1][1])], "K", 6)
    draw_line(pr, [(pts[0][0], pts[0][1] - 1), (pts[-1][0], pts[-1][1] - 1)], "R", 2)
    for i, (x, y) in enumerate(pts):
        if i % 2 == 0:
            pfill(pr, x - 1, y, x + 1, y + 52, "K")
    # 비상구 표지(초록, 문 위) · 소화전
    box3(pr, 30, 120, 100, 144, "g", "G", "K", bevel=2); draw_text(pr, 40, 129, "EXIT", "W")
    box3(pr, 20, 220, 80, 300, "R", "W", "r")
    draw_ellipse(pr, (32, 236, 68, 272), "K"); draw_ellipse(pr, (36, 240, 64, 268), "r")
    draw_text(pr, 30, 282, "FIRE", "W")
    # 층 번호 · 낙서
    draw_text(pr, 540, 60, "2F", "W"); pbox(pr, 532, 52, 562, 72, "W")
    for (x, y) in [(200, 90), (206, 94), (212, 90), (218, 96)]:
        pfill(pr, x, y, x + 3, y + 3, "i")
    box3(pr, 506, 84, 534, 98, "3", "W", "2", bevel=2); pfill(pr, 510, 98, 530, 104, "W"); pbox(pr, 510, 98, 530, 104, "K")   # 벽등
    return sh, pr, {"exit_on": glow(W, H, 65, 132, 70, 50, "G", y_min=CEIL, y_max=FLOOR),
                    "wall_on": glow(W, H, 520, 100, 100, 50, "3", y_min=CEIL, y_max=FLOOR - 218)}, []


# ---------- 지하 창고 ----------

def basement():
    sh = base_shell("4", "0", "conc")
    for y in range(CEIL + 8, FLOOR, 24):                         # 블록 벽
        off = 0 if (y // 24) % 2 else 32
        pfill(sh, 0, y, W - 1, y, "0")
        for x in range(off, W, 64):
            pfill(sh, x, y, x, y + 23, "0")
    dither_fill(sh, 0, 290, W - 1, FLOOR - 1, "0", 3)            # 습기 얼룩
    pfill(sh, 0, CEIL + 6, W - 1, CEIL + 12, "2"); pfill(sh, 0, CEIL + 12, W - 1, CEIL + 12, "K")   # 들보
    finish(sh)

    pr = canvas(W, H)
    # 철제 선반 2단 + 상자
    for sx in (40, 230):
        for y in (180, 260, FLOOR - 10):
            box3(pr, sx, y, sx + 150, y + 8, "3", "W", "2", bevel=2)
        pfill(pr, sx, 160, sx + 4, FLOOR - 1, "K"); pfill(pr, sx + 146, 160, sx + 150, FLOOR - 1, "K")
        for (bx, by, bw, bh, c) in [(8, 140, 50, 40, "D"), (64, 150, 40, 30, "w"), (110, 132, 34, 48, "D"),
                                    (10, 228, 60, 32, "w"), (76, 220, 58, 40, "D")]:
            box3(pr, sx + bx, by, sx + bx + bw, by + bh, c, "h", "d", bevel=2)
            pfill(pr, sx + bx + bw // 2 - 1, by + 2, sx + bx + bw // 2 + 1, by + bh - 2, "d")      # 테이프
    # 페인트 통 · 빗자루 · 쌓인 상자
    for x in (410, 440):
        box3(pr, x, 316, x + 26, FLOOR - 1, "3", "W", "2", bevel=2); pfill(pr, x + 2, 324, x + 24, 328, "b")
    draw_line(pr, [(490, 210), (520, FLOOR - 30)], "d", 4)
    draw_poly(pr, [(510, FLOOR - 32), (532, FLOOR - 32), (540, FLOOR - 1), (504, FLOOR - 1)], "y")
    for (x, y, w, h) in [(560, 290, 70, 62), (566, 238, 56, 52)]:
        box3(pr, x, y, x + w, y + h, "D", "h", "d"); pfill(pr, x + 2, y + h // 2, x + w - 2, y + h // 2 + 1, "d")
    shadows(pr, [(40, 190), (230, 380), (410, 466), (504, 540), (560, 630)])
    return sh, pr, {"bulb_on": glow(W, H, 320, 96, 150, 150, "Y", y_min=CEIL, y_max=FLOOR)}, [("bulb", 320)]


# ---------- 보일러실 ----------

def boiler():
    sh = base_shell("0", "K", "conc")
    for x in range(0, W, 40):                                    # 철판 벽
        pfill(sh, x, CEIL, x + 1, FLOOR - 1, "1")
    rivets(sh, 0, CEIL + 4, W - 1, FLOOR - 4, 20, "2")
    pfill(sh, 0, 300, W - 1, FLOOR - 1, "y"); dither_fill(sh, 0, 300, W - 1, FLOOR - 1, "K", 2)   # 경고 줄무늬 바닥 가장자리
    for x in range(-40, W, 40):
        draw_poly(sh, [(x, FLOOR - 1), (x + 20, FLOOR - 1), (x + 52, 300), (x + 32, 300)], "Y")
    pfill(sh, 0, 300, W - 1, 301, "K")
    finish(sh)

    pr = canvas(W, H)
    # 보일러 몸통(둥근 탱크)
    box3(pr, 70, 110, 250, FLOOR - 1, "r", "R", "K")
    for y in range(140, FLOOR - 20, 40):
        pfill(pr, 72, y, 248, y + 2, "K"); rivets(pr, 76, y - 4, 246, y + 6, 14, "W")
    box3(pr, 120, 220, 200, 290, "K", "2", "K", bevel=2)                                       # 아궁이 창
    pfill(pr, 128, 228, 192, 282, "a")
    for (x, y) in [(140, 270), (156, 262), (172, 272), (164, 276)]:
        pfill(pr, x, y, x + 8, y + 8, "R"); pfill(pr, x + 2, y + 2, x + 5, y + 5, "Y")
    # 배관 다발(천장 → 보일러)
    for (x, c) in [(150, "3"), (176, "3"), (204, "w")]:
        pfill(pr, x, CEIL, x + 12, 110, c); pfill(pr, x + 10, CEIL, x + 12, 110, "2"); pbox(pr, x, CEIL, x + 12, 110, "K")
    pfill(pr, 204, 60, W - 60, 72, "3"); pfill(pr, 204, 70, W - 60, 72, "2"); pbox(pr, 204, 60, W - 60, 72, "K")
    pfill(pr, W - 72, 60, W - 60, FLOOR - 60, "3"); pbox(pr, W - 72, 60, W - 60, FLOOR - 60, "K")
    box3(pr, W - 90, 150, W - 42, 166, "R", "W", "r", bevel=2)                                  # 밸브 손잡이
    # 계기판 3개
    for i, x in enumerate((290, 340, 390)):
        draw_ellipse(pr, (x, 120, x + 40, 160), "K"); draw_ellipse(pr, (x + 3, 123, x + 37, 157), "W")
        draw_line(pr, [(x + 20, 140), (x + 10 + i * 10, 128)], "R", 2)
    # 공구함 · 경고 표지
    box3(pr, 300, 290, 400, FLOOR - 1, "R", "W", "r"); pfill(pr, 300, 312, 400, 314, "K"); pfill(pr, 340, 300, 360, 304, "3")
    draw_poly(pr, [(470, 230), (500, 180), (530, 230)], "K"); draw_poly(pr, [(476, 226), (500, 186), (524, 226)], "Y")
    pfill(pr, 499, 198, 501, 214, "K"); pfill(pr, 499, 218, 501, 221, "K")
    box3(pr, 482, 104, 518, 112, "2", "3", "K", bevel=2)                                       # 경고등 받침
    draw_ellipse(pr, (486, 82, 514, 110), "K"); draw_ellipse(pr, (489, 85, 511, 107), "R"); pfill(pr, 493, 89, 497, 93, "W")
    shadows(pr, [(70, 250), (300, 400)])
    return sh, pr, {"warn_on": glow(W, H, 500, 104, 100, 80, "R", y_min=CEIL, y_max=FLOOR),
                    "fire_on": glow(W, H, 160, 300, 110, 46, "Y", y_min=CEIL, y_max=FLOOR)}, []


# ---------- 엘리베이터 ----------

def elevator():
    sh = base_shell("1", "0", "tile")
    pfill(sh, 220, CEIL, 420, FLOOR - 1, "a")                    # 승강로(칸이 다니는 빈 공간)
    for y in range(CEIL, FLOOR, 48):
        pfill(sh, 222, y, 418, y + 1, "e")
    pfill(sh, 236, CEIL, 238, FLOOR - 1, "2"); pfill(sh, 402, CEIL, 404, FLOOR - 1, "2")   # 레일
    pfill(sh, 316, CEIL, 317, 90, "3")                            # 케이블
    pfill(sh, 324, CEIL, 325, 90, "3")
    for x0 in (204, 420):                                         # 승강로 문틀
        box3(sh, x0, CEIL, x0 + 16, FLOOR - 1, "3", "W", "2", bevel=2)
    finish(sh)

    pr = canvas(W, H)
    # 층 표시등 · 호출 버튼
    box3(pr, 270, 40, 370, 64, "K", "2", "K", bevel=2)
    for i, t in enumerate(["B1", "1", "2", "3"]):
        draw_text(pr, 280 + i * 22, 49, t, "Y" if t == "1" else "2")
    box3(pr, 440, 200, 470, 250, "3", "W", "2", bevel=2)
    draw_ellipse(pr, (446, 208, 464, 226), "K"); draw_ellipse(pr, (449, 211, 461, 223), "Y")
    draw_ellipse(pr, (446, 228, 464, 246), "K"); draw_ellipse(pr, (449, 231, 461, 243), "2")
    # 고장 안내 · 의자 · 화분 (기다리는 공간)
    box3(pr, 500, 120, 600, 180, "W", "W", "3", bevel=2)
    draw_text(pr, 512, 132, "OUT OF", "R"); draw_text(pr, 512, 146, "ORDER?", "R"); draw_text(pr, 512, 162, "-MGMT", "2")
    box3(pr, 60, 290, 140, 304, "b", "C", "B", bevel=2); pfill(pr, 66, 304, 70, FLOOR - 1, "K"); pfill(pr, 130, 304, 134, FLOOR - 1, "K")
    box3(pr, 64, 250, 136, 292, "b", "C", "B", bevel=2)
    box3(pr, 568, 316, 596, FLOOR - 1, "d", "D", "K")                                         # 화분
    for (x, y, r) in [(582, 296, 12), (572, 284, 9), (592, 282, 9)]:
        draw_ellipse(pr, (x - r, y - r, x + r, y + r), "g"); draw_ellipse(pr, (x - r + 3, y - r + 2, x + r - 4, y + r - 4), "G")
    shadows(pr, [(60, 140), (568, 596)])
    return sh, pr, {"indicator_on": glow(W, H, 320, 52, 80, 30, "Y", y_min=CEIL, y_max=FLOOR),
                    "ceiling_a_on": light_cone(W, H, 100, CEIL + 42, 24, 90, FLOOR, "h"),
                    "ceiling_b_on": light_cone(W, H, 540, CEIL + 42, 24, 90, FLOOR, "h")}, [("lamp", 100), ("lamp", 540)]


def car():
    """엘리베이터 칸(L5 움직이는 오브젝트) 176×224."""
    im = canvas(176, 224)
    box3(im, 0, 0, 175, 223, "3", "W", "2")
    pfill(im, 8, 10, 167, 215, "2"); pfill(im, 87, 10, 88, 215, "K")
    for x in (20, 100):
        box3(im, x, 30, x + 56, 120, "4", "3", "K", bevel=2)
    pfill(im, 8, 10, 167, 16, "Y")                               # 칸 안 조명 띠
    return im


def build():
    made = []
    rooms = {"lobby": lobby, "stairwell": stairwell, "basement": basement, "boiler": boiler, "elevator": elevator}
    for key, fn in rooms.items():
        sh, pr, li, fixtures = fn()
        for kind, x in fixtures:                                 # 조명 기구는 L2 소품에 그림
            if kind == "lamp":
                ceiling_lamp(pr, x)
            elif kind == "bulb":
                bulb(pr, x)
        for name, im in [("L1_shell", sh), ("L2_props", pr)] + [(f"L3_light_{k}", v) for k, v in li.items()]:
            assert not palette_check(im), (key, name)
            meta = {"status": "시안", "layer": name[:2], "size": [W, H], "floor_y": FLOOR}
            if name == "L1_shell":
                meta["windows"] = [list(r) for r in WINDOWS.get(key, [])]
            made.append(save(im, f"rooms128/{key}/{name}.png", meta))
        dk = dark_overlay(W, H, CEIL, FLOOR)                    # 조명이 모두 꺼졌을 때(창 범위 제외)
        for r in WINDOWS.get(key, []):
            hole(dk, r)
        made.append(save(dk, f"rooms128/{key}/L3_dark.png", {"status": "시안", "layer": "L3", "use": "조명이 모두 꺼졌을 때"}))
        comp = canvas(W, H, PIX["e"]); comp.alpha_composite(sh); comp.alpha_composite(pr)     # 미리보기: 유리 너머는 밤 단색
        if key == "elevator":
            c = car(); comp.alpha_composite(c, (232, FLOOR - 224))
            made.append(save(c, "rooms128/elevator/L5_car.png", {"status": "시안", "layer": "L5", "pivot": "bottom-center"}))
        for v in li.values():
            comp.alpha_composite(v)
        made.append(save(comp, f"preview/rooms128_{key}.png", {"status": "미리보기"}))
    return made
