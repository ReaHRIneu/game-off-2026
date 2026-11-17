"""주인공 방 128px 규격 기준 시안(섹터 640×384) — D-073 레이어, D-075, D-078

L1 방 껍데기(벽지·천장·바닥·창), L2 소품(침대·책상·모니터·의자·선반·포스터·냉장고·화분·옷더미·케이블),
L3 조명(모니터 빛·스탠드 빛 — 켜짐 그림만). 출입문(실패 지점)은 L4 오브젝트로 따로.
"""
from PIL import Image

from artkit import EXPORT, PIX, canvas, draw_line, draw_poly, palette_check, pbox, pfill, pset, save, upscale
from prop import box3, dither_fill, light_pool, rivets, wood

W, H = 640, 384
FLOOR = 352          # 바닥선(걷는 높이)
CEIL = 28


def shell():
    im = canvas(W, H, PIX["k"])
    # 벽지: 세로 줄무늬 + 작은 마름모 디더
    for x in range(0, W, 32):
        pfill(im, x, CEIL, x + 1, FLOOR, "j")
    for y in range(CEIL + 12, FLOOR - 20, 24):
        for x in range(16, W, 32):
            pset(im, x, y, "j"); pset(im, x - 1, y + 1, "j"); pset(im, x + 1, y + 1, "j"); pset(im, x, y + 2, "j")
    # 얼룩(낡은 벽)
    dither_fill(im, 420, 60, 470, 110, "a", 3)
    # 천장 + 몰딩
    pfill(im, 0, 0, W - 1, CEIL - 1, "2"); pfill(im, 0, CEIL - 6, W - 1, CEIL - 1, "4"); pfill(im, 0, CEIL - 2, W - 1, CEIL - 1, "K")
    # 걸레받이
    wood(im, 0, FLOOR - 18, W - 1, FLOOR - 1, "d", "K", "D", plank=40, vertical=True)
    pfill(im, 0, FLOOR - 19, W - 1, FLOOR - 18, "K")
    # 바닥(마루) + 슬래브
    wood(im, 0, FLOOR, W - 1, FLOOR + 14, "D", "d", "h", plank=7)
    pfill(im, 0, FLOOR + 15, W - 1, H - 1, "2"); pfill(im, 0, FLOOR + 15, W - 1, FLOOR + 16, "K")
    # 창: 창밖 도시(배경 합본 4배 확대 자르기)
    city = Image.open(EXPORT / "backgrounds/city_night_composite.png").convert("RGBA")
    view = city.crop((470, 200, 470 + 52, 200 + 36)).resize((208, 144), Image.NEAREST)   # 불 켜진 창이 많은 구역
    wx, wy = 36, 64
    im.alpha_composite(view, (wx, wy))
    box_frame(im, wx - 8, wy - 8, wx + 208 + 7, wy + 144 + 7)
    pfill(im, wx + 102, wy, wx + 105, wy + 143, "2"); pfill(im, wx, wy + 70, wx + 207, wy + 73, "2")     # 창살
    box3(im, wx - 14, wy + 150, wx + 221, wy + 162, "3", "W", "2")                                        # 창턱
    pbox(im, 0, 0, W - 1, H - 1, "K")
    return im


def box_frame(im, x0, y0, x1, y1):
    for t in range(8):
        col = "K" if t < 2 or t > 5 else ("3" if t < 4 else "2")
        pbox(im, x0 + t, y0 + t, x1 - t, y1 - t, col)


def props():
    im = canvas(W, H)
    # 커튼(창 양옆, 천 주름)
    for cx0 in (16, 238):
        pfill(im, cx0, 44, cx0 + 22, 236, "K"); pfill(im, cx0 + 2, 46, cx0 + 20, 234, "B")
        for x in range(cx0 + 5, cx0 + 20, 6):
            pfill(im, x, 46, x + 1, 234, "b")
    pfill(im, 8, 38, 268, 43, "4"); pbox(im, 8, 38, 268, 43, "K")                                           # 커튼봉
    # 화분(창턱)
    box3(im, 190, 210, 214, 226, "d", "D", "K")
    for (x, y) in [(196, 196), (204, 190), (210, 200), (200, 204)]:
        pfill(im, x - 5, y - 5, x + 5, y + 5, "g"); pfill(im, x - 3, y - 3, x + 2, y + 2, "G")
    # 침대
    box3(im, 20, 294, 250, 332, "d", "D", "K")                                                              # 틀
    box3(im, 26, 278, 244, 300, "b", "C", "B")                                                              # 이불
    box3(im, 30, 266, 88, 286, "W", "W", "3")                                                               # 베개
    for x in range(100, 240, 18):
        pfill(im, x, 282, x + 6, 284, "B")                                                                  # 이불 주름
    pfill(im, 26, 332, 36, FLOOR - 1, "K"); pfill(im, 234, 332, 244, FLOOR - 1, "K")                         # 다리
    # 선반 + 책
    box3(im, 300, 92, 520, 104, "D", "h", "d")
    x = 308
    for i, (wbk, hbk, col) in enumerate([(12, 40, "R"), (10, 34, "Y"), (14, 44, "C"), (8, 30, "G"), (12, 38, "3"), (10, 42, "r"), (16, 36, "B")]):
        box3(im, x, 92 - hbk, x + wbk, 92, col, "W" if col in "YCG" else "h", "K", bevel=2)
        x += wbk + 2
    box3(im, 440, 66, 470, 92, "3", "W", "2", bevel=2)                                                      # 작은 상자
    # 포스터(청사진)
    pfill(im, 540, 60, 616, 160, "K"); pfill(im, 542, 62, 614, 158, "B")
    for xx in range(548, 612, 10):
        pfill(im, xx, 66, xx, 154, "b")
    pbox(im, 566, 86, 592, 150, "C"); pbox(im, 572, 72, 586, 86, "C")
    # 책상
    box3(im, 290, 262, 528, 276, "D", "h", "d")                                                             # 상판
    box3(im, 296, 276, 340, FLOOR - 1, "d", "D", "K")                                                       # 서랍장
    for yy in (290, 312, 334):
        pfill(im, 312, yy, 324, yy + 2, "h")
    pfill(im, 476, 276, 484, FLOOR - 1, "K")                                                                # 다리
    # 모니터 2대(켜짐)
    for mx in (330, 420):
        box3(im, mx, 170, mx + 84, 236, "K", "2", "K", bevel=2)
        pfill(im, mx + 6, 176, mx + 78, 230, "B")
        for li, yy in enumerate(range(182, 226, 7)):
            pfill(im, mx + 10, yy, mx + 10 + (17 * (li + 2)) % 60, yy + 2, "C" if li % 3 else "G")
        pfill(im, mx + 36, 236, mx + 48, 256, "2"); box3(im, mx + 24, 254, mx + 60, 262, "2", "3", "K", bevel=2)
    # 키보드·머그·캔
    box3(im, 360, 254, 470, 262, "2", "3", "K", bevel=2)
    for kx in range(366, 466, 8):
        pfill(im, kx, 256, kx + 4, 257, "3")
    box3(im, 482, 240, 500, 262, "W", "W", "3", bevel=2); pfill(im, 500, 246, 506, 254, "K")                 # 머그
    box3(im, 300, 236, 314, 262, "R", "W", "r", bevel=2); box3(im, 316, 240, 328, 262, "G", "W", "g", bevel=2)  # 캔
    # 스탠드
    draw_line(im, [(512, 262), (506, 196), (470, 176)], "K", 4)
    draw_poly(im, [(456, 166), (486, 166), (494, 182), (448, 182)], "2"); pfill(im, 498, 256, 526, 262, "2")
    # 게이밍 의자(뒤돌아 놓임)
    box3(im, 400, 220, 470, 312, "4", "2", "K")                                                             # 등받이
    pfill(im, 408, 232, 462, 238, "R")                                                                      # 줄무늬
    box3(im, 392, 306, 478, 320, "4", "2", "K"); pfill(im, 431, 320, 439, 338, "K")
    pfill(im, 404, 338, 466, 342, "K")
    for wx in (404, 432, 462):
        pfill(im, wx - 4, 342, wx + 4, FLOOR - 1, "2")
    # 미니 냉장고
    box3(im, 486, 286, 540, FLOOR - 1, "3", "W", "2"); pfill(im, 492, 310, 534, 311, "2"); pfill(im, 528, 292, 532, 306, "K")
    # 케이블(책상 → 바닥)
    draw_line(im, [(420, 276), (424, 320), (380, 346), (300, 350)], "K", 3)
    # 옷더미
    for (x0, y0, x1, y1, c) in [(256, 336, 296, 350, "Y"), (262, 328, 290, 340, "T"), (270, 322, 286, 332, "r")]:
        box3(im, x0, y0, x1, y1, c, "W", "K", bevel=2)
    return im


def lights():
    """L3 조명(켜짐 그림만): 모니터 빛, 스탠드 빛."""
    mon = canvas(W, H)
    for mx in (330, 420):
        mon.alpha_composite(light_pool(W, H, mx + 42, 250, 120, 70, "b"))
    lamp = light_pool(W, H, 470, 262, 90, 40, "h")
    return {"monitors_on": mon, "desk_lamp_on": lamp}


def door(open_=False):
    """출입문(실패 지점 표시가 붙는 방문) — L4 오브젝트, 64×208."""
    im = canvas(64, 208)
    box3(im, 0, 0, 63, 207, "D", "h", "d")
    pbox(im, 10, 16, 53, 90, "d"); pbox(im, 10, 110, 53, 190, "d")
    pfill(im, 50, 104, 56, 112, "Y")
    return im


def build():
    made = []
    sh, pr, li = shell(), props(), lights()
    for name, im in [("L1_shell", sh), ("L2_props", pr)] + [(f"L3_light_{k}", v) for k, v in li.items()]:
        assert not palette_check(im), name
        made.append(save(im, f"rooms128/home/{name}.png", {"status": "시안", "layer": name[:2], "size": [W, H], "floor_y": FLOOR}))
    d = door()
    made.append(save(d, "rooms128/home/L4_door.png", {"status": "시안", "layer": "L4", "pivot": "bottom-center"}))
    comp = sh.copy()
    comp.alpha_composite(pr)
    for v in li.values():
        comp.alpha_composite(v)
    comp.alpha_composite(d, (W - 72, FLOOR - 208))
    made.append(save(comp, "preview/rooms128_home.png", {"status": "미리보기 — 조명 켜짐"}))
    dark = sh.copy(); dark.alpha_composite(pr); dark.alpha_composite(d, (W - 72, FLOOR - 208))
    made.append(save(dark, "preview/rooms128_home_lights_off.png", {"status": "미리보기 — 조명 꺼짐"}))
    return made
