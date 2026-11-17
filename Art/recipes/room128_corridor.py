"""복도 128px 시안(섹터 640×384) — D-073, D-075, D-078. 주인공 방(room128_home) 기준을 따른다.

L1 방 껍데기(낡은 페인트 벽·하단 판넬·천장 배관·리놀륨 바닥), L2 소품(소화기·게시판·우편함·배관·비상구 표지·전단·쓰레기통),
L3 조명(형광등 2개 — 하나는 오래됨)
"""
from artkit import PIX, canvas, draw_line, palette_check, pbox, pfill, pset, save
from pixfont import draw_text
from prop import box3, dither_fill, light_pool, rivets, wood

W, H = 640, 384
FLOOR, CEIL = 352, 28


def shell():
    im = canvas(W, H, PIX["0"])
    # 위쪽 페인트 벽 + 아래쪽 판넬(두 톤 벽)
    pfill(im, 0, CEIL, W - 1, 210, "1")
    for x in range(0, W, 64):                                   # 벽 이음
        pfill(im, x, CEIL, x, 210, "0")
    dither_fill(im, 120, 120, 220, 200, "0", 3)                 # 벗겨진 페인트
    dither_fill(im, 470, 60, 530, 110, "0", 3)
    pfill(im, 0, 210, W - 1, 214, "4"); pfill(im, 0, 215, W - 1, 216, "K")   # 몰딩
    for x in range(0, W, 80):                                   # 하단 판넬
        box3(im, x + 4, 222, x + 76, FLOOR - 22, "2", "3", "4", bevel=2)
    wood(im, 0, FLOOR - 18, W - 1, FLOOR - 1, "4", "K", "2", plank=80, vertical=True)
    # 천장 + 배관
    pfill(im, 0, 0, W - 1, CEIL - 1, "2"); pfill(im, 0, CEIL - 2, W - 1, CEIL - 1, "K")
    for (y, col, dark) in [(CEIL + 4, "3", "2"), (CEIL + 16, "d", "K")]:
        pfill(im, 0, y, W - 1, y + 7, col); pfill(im, 0, y + 6, W - 1, y + 7, dark); pfill(im, 0, y, W - 1, y, "K")
        for x in range(40, W, 160):
            box3(im, x, y - 3, x + 12, y + 10, "3", "W", "2", bevel=2)   # 배관 이음쇠
    # 바닥(리놀륨 체크) + 슬래브
    for x in range(0, W, 32):
        pfill(im, x, FLOOR, x + 31, FLOOR + 14, "2" if (x // 32) % 2 else "4")
    pfill(im, 0, FLOOR, W - 1, FLOOR, "K")
    pfill(im, 0, FLOOR + 15, W - 1, H - 1, "2"); pfill(im, 0, FLOOR + 15, W - 1, FLOOR + 16, "K")
    pbox(im, 0, 0, W - 1, H - 1, "K")
    return im


def props():
    im = canvas(W, H)
    # 형광등 기구(천장)
    for lx in (140, 460):
        box3(im, lx - 50, CEIL + 26, lx + 50, CEIL + 34, "3", "W", "2", bevel=2)
        pfill(im, lx - 44, CEIL + 35, lx + 44, CEIL + 37, "W" if lx == 140 else "3")
    # 게시판 + 전단
    box3(im, 40, 90, 200, 190, "d", "D", "K")
    pfill(im, 46, 96, 194, 184, "D")
    for (x, y, w, h, c) in [(54, 102, 40, 52, "W"), (100, 106, 46, 36, "Y"), (152, 100, 36, 56, "W"), (70, 158, 60, 22, "C")]:
        box3(im, x, y, x + w, y + h, c, "W", "3", bevel=2)
        for ly in range(y + 8, y + h - 4, 6):
            pfill(im, x + 5, ly, x + w - 6, ly, "2")
        pfill(im, x + w // 2 - 1, y + 1, x + w // 2 + 1, y + 3, "R")             # 압정
    # 우편함(작은 칸들)
    box3(im, 240, 120, 360, 200, "3", "W", "2")
    for r in range(3):
        for c in range(4):
            x, y = 246 + c * 28, 126 + r * 24
            box3(im, x, y, x + 24, y + 20, "2", "3", "K", bevel=2)
            pfill(im, x + 6, y + 8, x + 18, y + 9, "K")
    # 비상구 표지
    box3(im, 420, 100, 500, 124, "g", "G", "K", bevel=2)
    draw_text(im, 430, 109, "EXIT", "W")
    # 소화기
    box3(im, 540, 270, 566, FLOOR - 4, "R", "W", "r", bevel=2)
    pfill(im, 546, 258, 560, 270, "K"); draw_line(im, [(560, 262), (574, 278), (572, 300)], "K", 3)
    pfill(im, 532, 236, 576, 252, "W"); pbox(im, 532, 236, 576, 252, "K"); pfill(im, 538, 242, 570, 246, "R")   # 표지
    # 벽 배관(세로)
    pfill(im, 600, CEIL, 612, FLOOR - 1, "3"); pfill(im, 610, CEIL, 612, FLOOR - 1, "2"); pbox(im, 600, CEIL, 612, FLOOR - 1, "K")
    for y in range(80, FLOOR, 90):
        box3(im, 596, y, 616, y + 10, "3", "W", "2", bevel=2)
    # 쓰레기통
    box3(im, 380, 300, 420, FLOOR - 1, "4", "2", "K")
    pfill(im, 376, 296, 424, 302, "2"); pbox(im, 376, 296, 424, 302, "K")
    for x in range(388, 414, 8):
        pfill(im, x, 306, x + 1, FLOOR - 6, "K")
    return im


def lights():
    good = light_pool(W, H, 140, 120, 150, 140, "W")
    old = light_pool(W, H, 460, 120, 110, 110, "3")        # 오래된 형광등(어둡게)
    return {"fluorescent_a_on": good, "fluorescent_b_on": old}


def build():
    made = []
    sh, pr, li = shell(), props(), lights()
    for name, im in [("L1_shell", sh), ("L2_props", pr)] + [(f"L3_light_{k}", v) for k, v in li.items()]:
        assert not palette_check(im), name
        made.append(save(im, f"rooms128/corridor/{name}.png", {"status": "시안", "layer": name[:2], "size": [W, H], "floor_y": FLOOR}))
    comp = sh.copy(); comp.alpha_composite(pr)
    for v in li.values():
        comp.alpha_composite(v)
    made.append(save(comp, "preview/rooms128_corridor.png", {"status": "미리보기"}))
    return made
