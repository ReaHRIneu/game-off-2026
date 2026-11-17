"""새 챕터 0(주인공 건물 침입자 저지, D-069) 맵·오브젝트·인물 시안 — 03-01 v0.4.1 §1-C-1·§1-C-2

- 침입자 = 이후 조력자(D-070). 32×32 좌·우. 주인공(청록 후드 해커와 다른 실루엣): 모자, 입 가리개, 노란 점퍼, 배낭
- 맵: 주인공 건물(낡은 주택 건물) + 지하 함정(B1)
- scene_building 의 방 그리기·오브젝트 함수를 재사용
[시안] 배치는 03-01 제안
"""
from PIL import Image

import scene_building as sb
from artkit import EXPORT, PIX, canvas, draw_line, outline_outside, over, palette_check, pbox, pfill, pset, save, upscale

LEVELS = {
    "ch0_home1": [["home", "corridor", "corridor", "stairs"],
                  ["lobby", "corridor", "corridor", "stairs"],
                  [None, None, "basement", None]],
    "ch0_home2": [["home", "corridor", "corridor", "corridor"],
                  ["corridor", "elevator", "corridor", "corridor"],
                  [None, None, None, "boiler"]],
}


def runner(direction="right"):
    """침입자/조력자: 모자, 입 가리개, 노란 점퍼, 배낭."""
    im = canvas(32, 32)
    c = {}

    def put(pts, ch):
        for p in pts:
            c[p] = ch
    put([(x, y) for y in range(15, 24) for x in range(6, 11)], "4")            # 배낭
    put([(x, y) for y in range(16, 22) for x in range(7, 10)], "2")
    put([(x, y) for y in range(25, 30) for x in range(12, 15)], "2")           # 다리(작업 바지)
    put([(x, y) for y in range(25, 30) for x in range(17, 20)], "2")
    put([(x, y) for y in range(15, 25) for x in range(11, 21)], "Y")           # 점퍼
    put([(x, y) for y in range(15, 25) for x in range(11, 13)], "y")
    put([(x, 20) for x in range(11, 21)], "y")                                 # 지퍼선
    put([(x, y) for y in range(16, 22) for x in range(19, 23)], "Y")           # 팔
    put([(x, 22) for x in range(20, 23)], "S")                                 # 손
    put([(x, y) for y in range(6, 15) for x in range(12, 21) if (x - 16) ** 2 + (y - 10) ** 2 <= 20], "S")  # 얼굴
    put([(x, y) for y in range(4, 9) for x in range(11, 21) if (x - 16) ** 2 + (y - 8) ** 2 <= 22], "K")    # 머리카락
    put([(x, y) for y in range(3, 7) for x in range(12, 21)], "R")             # 모자
    put([(x, 6) for x in range(18, 24)], "r")                                  # 모자 챙(앞)
    put([(x, y) for y in range(11, 14) for x in range(15, 21)], "K")           # 입 가리개
    put([(19, 9)], "W"); put([(18, 9)], "K")                                   # 눈
    put([(x, 30) for x in list(range(11, 15)) + list(range(17, 21))], "K")     # 신발
    for (x, y), ch in c.items():
        pset(im, x, y, ch)
    outline_outside(im, "K")
    return im.transpose(Image.Transpose.FLIP_LEFT_RIGHT) if direction == "left" else im


def hatch(open_):
    """바닥 해치: 바닥선에 놓이는 납작한 철판. 열리면 아래로 구멍."""
    im = canvas(40, 10)
    if open_:
        pfill(im, 0, 4, 39, 9, "K"); pfill(im, 2, 5, 37, 9, "a")
        pfill(im, 0, 0, 3, 9, "3")                                             # 세워진 덮개
    else:
        pfill(im, 0, 6, 39, 9, "3"); pbox(im, 0, 6, 39, 9, "K")
        for x in range(4, 36, 6):
            pset(im, x, 7, "4")
        pfill(im, 18, 7, 21, 8, "Y")
    return im


def home_room(im, c, f, city):
    x0, y0 = sb.sx(c), sb.sy(f)
    x1, y1 = x0 + sb.SW - 1, y0 + sb.SH - 1
    fy = y1 - sb.WALL
    pfill(im, x0, y0 + sb.WALL, x1, y1 - sb.WALL, "k")
    pfill(im, x0, y1 - sb.WALL + 1, x1, y1, "2"); pfill(im, x0, y1 - sb.WALL + 1, x1, y1 - sb.WALL + 1, "3")
    sb.window(im, x0 + 12, y0 + 16, 40, 30, city, 250)
    pfill(im, x0 + 70, fy - 16, x0 + 130, fy - 14, "3")                       # 책상
    pfill(im, x0 + 74, fy - 13, x0 + 76, fy, "4"); pfill(im, x0 + 124, fy - 13, x0 + 126, fy, "4")
    for mx in (78, 102):                                                     # 모니터 2대(켜짐)
        pfill(im, x0 + mx, fy - 34, x0 + mx + 20, fy - 18, "K"); pfill(im, x0 + mx + 2, fy - 32, x0 + mx + 18, fy - 20, "B")
        pfill(im, x0 + mx + 4, fy - 30, x0 + mx + 14, fy - 30, "C")
    pfill(im, x0 + 12, fy - 10, x0 + 52, fy, "4"); pfill(im, x0 + 12, fy - 12, x0 + 52, fy - 10, "2")   # 침대
    pfill(im, x0 + 140, fy - 46, x0 + 152, fy, "D"); pbox(im, x0 + 140, fy - 46, x0 + 152, fy, "K")      # 방문
    pfill(im, x0 + 136, fy - 58, x0 + 156, fy - 50, "R"); pbox(im, x0 + 136, fy - 58, x0 + 156, fy - 50, "K")  # 경고 표시
    pbox(im, x0, y0, x1, y1, "K")


def basement_room(im, c, f, kind):
    x0, y0 = sb.sx(c), sb.sy(f)
    x1, y1 = x0 + sb.SW - 1, y0 + sb.SH - 1
    fy = y1 - sb.WALL
    pfill(im, x0, y0 + sb.WALL, x1, y1 - sb.WALL, "a")
    pfill(im, x0, y1 - sb.WALL + 1, x1, y1, "2")
    if kind == "basement":
        for i, (bx, h) in enumerate([(14, 30), (52, 44), (100, 24)]):
            pfill(im, x0 + bx, fy - h, x0 + bx + 28, fy, "d"); pbox(im, x0 + bx, fy - h, x0 + bx + 28, fy, "K")
    else:  # boiler
        pfill(im, x0 + 20, fy - 50, x0 + 70, fy, "4"); pbox(im, x0 + 20, fy - 50, x0 + 70, fy, "K")
        pfill(im, x0 + 34, fy - 30, x0 + 56, fy - 22, "R")
        draw_line(im, [(x0 + 70, fy - 40), (x0 + 150, fy - 40)], "3", 3)
        draw_line(im, [(x0 + 100, y0 + sb.WALL), (x0 + 100, fy - 40)], "3", 3)
    pfill(im, x0 + 60, y0 + sb.WALL, x0 + 100, y0 + sb.WALL + 2, "Y")          # 위쪽 해치 구멍 표시
    pbox(im, x0, y0, x1, y1, "K")


def base(rows):
    floors, cols = len(rows), len(rows[0])
    mw, mh = sb.SW * cols, sb.ROOF + sb.SH * floors + sb.GROUND
    city = Image.open(EXPORT / "backgrounds/city_night_composite.png").convert("RGBA")
    im = canvas(mw, mh, PIX["a"])
    im.alpha_composite(city.crop((0, 0, min(640, mw), mh)), (0, 0))
    pfill(im, 0, sb.sy(0) - 8, mw - 1, sb.sy(0) - 1, "4"); pfill(im, 0, sb.sy(0) - 8, mw - 1, sb.sy(0) - 8, "3")
    ground_top = sb.sy(floors - 1)                                           # 지하 층 위
    pfill(im, 0, ground_top, mw - 1, mh - 1, "K")                            # 흙(지하 전체)
    for y in range(ground_top + 4, mh, 6):
        for x in range((y // 6) % 2 * 4, mw, 9):
            pset(im, x, y, "k")
    pfill(im, 0, ground_top, mw - 1, ground_top + 1, "2")
    for f, row in enumerate(rows):
        for c, kind in enumerate(row):
            if kind is None:
                continue
            if kind == "home":
                home_room(im, c, f, city)
            elif kind in ("basement", "boiler"):
                basement_room(im, c, f, kind)
            else:
                sb.room(im, kind, c, f, city)
    return im


def preview(key, im):
    full = im.copy()
    if key == "ch0_home1":
        sb.place(full, sb.shutter(True), 3, 1, 40)                          # 계단 열림(현실)
        full.alpha_composite(hatch(False), (sb.sx(2) + 60, sb.sy(1) + sb.SH - sb.WALL - 10))
        sb.place(full, sb.terminal(), 0, 1, 70)
        sb.place(full, runner("right"), 1, 1, 14)
    else:
        sb.place(full, sb.elevator_car(True), 1, 1, 52)
        full.alpha_composite(sb.blocker(False), (sb.sx(2), sb.sy(1)))       # 레이저 켜짐(현실)
        sb.place(full, sb.relay(), 2, 1, 92)
        full.alpha_composite(hatch(False), (sb.sx(3) + 60, sb.sy(1) + sb.SH - sb.WALL - 10))
        sb.place(full, runner("right"), 0, 1, 14)
    return full


def build():
    made = []
    for key, rows in LEVELS.items():
        b = base(rows)
        assert not palette_check(b), key
        made.append(save(b, f"scenes/{key}_base.png", {"status": "시안", "note": "새 챕터 0 맵 배경(오브젝트 제외)",
                                                       "sector": [sb.SW, sb.SH], "roof": sb.ROOF, "wall": sb.WALL,
                                                       "floors": len(rows), "cols": len(rows[0])}))
        made.append(save(upscale(preview(key, b), 2), f"preview/{key}_map_x2.png", {"status": "미리보기"}))
    for d in ("right", "left"):
        made.append(save(runner(d), f"sprites/runner_{d}.png", {"status": "시안", "note": "침입자 = 조력자(D-070)"}))
    for st, im in (("closed", hatch(False)), ("open", hatch(True))):
        assert not palette_check(im)
        made.append(save(im, f"objects/hatch_{st}.png", {"status": "시안"}))
    made.append(save(sb.bp_outline(hatch(True)), "objects/bp_hatch_open.png", {"status": "시안", "layer": "blueprint"}))
    sheet = canvas(200, 70, PIX["k"])
    sheet.alpha_composite(upscale(runner("right"), 2), (4, 4)); sheet.alpha_composite(upscale(runner("left"), 2), (72, 4))
    made.append(save(upscale(sheet, 2), "preview/runner_x4.png", {"status": "미리보기"}))
    return made
