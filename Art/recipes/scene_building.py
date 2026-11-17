"""측면 단면도 테스트 맵 아트 시안 — D-063~D-066, 02-02 §0-2

- 맵 = 3층 × 5섹터(섹터 160×96) + 지붕·지면. 화면보다 크다(D-063: 일부만 보임)
- 방마다 다른 실내(로비·복도·창고·계단실·사무실·서버실·엘리베이터), 창 너머 도시 야경
- 오브젝트(문·계단 셔터·엘리베이터·차단막·출구·단말·중계기)는 현실/설계도 두 상태를 따로 그린다
- 이펙트·파티클 없음(D-040), 공유 팔레트, 안티앨리어싱 없음
[시안] 테스트 맵 배치는 임시(레벨 기획 전)
"""
from PIL import Image

from artkit import (EXPORT, PIX, canvas, draw_line, draw_poly, outline_outside, over, palette_check,
                    pbox, pfill, pset, save, upscale)
from pixfont import draw_text, text_width
from ui_mvp import badge, frame, gauge, window_frame

SW, SH = 160, 96           # 섹터 크기
COLS, FLOORS = 5, 3
ROOF, GROUND = 24, 32
MW, MH = SW * COLS, ROOF + SH * FLOORS + GROUND   # 800 × 344
WALL = 6                    # 바닥·천장 두께

# 섹터 종류(층은 위에서부터 0) — 임시 테스트 맵
ROOMS = [
    ["office", "office", "server", "corridor", "elevator"],
    ["office", "corridor", "corridor", "stairs", "elevator"],
    ["lobby", "corridor", "storage", "storage", "stairs"],
]


def sx(c): return c * SW
def sy(f): return ROOF + f * SH


def dither(im, box, ch, step=2):
    x0, y0, x1, y1 = box
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if (x + y) % step == 0:
                pset(im, x, y, ch)


def window(im, x, y, w, h, city, ox):
    view = city.crop((ox, 120, ox + w, 120 + h))
    im.alpha_composite(view, (x, y))
    pbox(im, x - 1, y - 1, x + w, y + h, "2")
    pfill(im, x + w // 2, y, x + w // 2, y + h - 1, "2")


def room(im, kind, c, f, city):
    x0, y0 = sx(c), sy(f)
    x1, y1 = x0 + SW - 1, y0 + SH - 1
    wall = {"lobby": "1", "corridor": "0", "storage": "4", "office": "1", "server": "a",
            "stairs": "0", "elevator": "k"}[kind]
    pfill(im, x0, y0 + WALL, x1, y1 - WALL, wall)
    pfill(im, x0, y1 - WALL + 1, x1, y1, "2")             # 바닥 슬래브
    pfill(im, x0, y1 - WALL + 1, x1, y1 - WALL + 1, "3")
    fy = y1 - WALL                                        # 바닥 높이(걷는 선)
    if kind == "lobby":
        window(im, x0 + 14, y0 + 18, 48, 40, city, 40 + c * 50)
        pfill(im, x0 + 90, fy - 22, x0 + 140, fy, "2"); pfill(im, x0 + 90, fy - 22, x0 + 140, fy - 20, "3")  # 안내 데스크
        pfill(im, x0 + 70, fy - 30, x0 + 74, fy, "g"); pfill(im, x0 + 66, fy - 38, x0 + 78, fy - 28, "G")   # 화분
    elif kind == "corridor":
        for lx in (x0 + 40, x0 + 120):                    # 천장 조명
            pfill(im, lx - 8, y0 + WALL, lx + 8, y0 + WALL + 2, "3")
        pfill(im, x0 + 70, y0 + 30, x0 + 90, y0 + 44, "B"); pbox(im, x0 + 70, y0 + 30, x0 + 90, y0 + 44, "C")  # 안내판
    elif kind == "storage":
        for i, (bx, h) in enumerate([(10, 34), (46, 50), (84, 26), (120, 42)]):
            pfill(im, x0 + bx, fy - h, x0 + bx + 30, fy, "D" if i % 2 else "d")
            pbox(im, x0 + bx, fy - h, x0 + bx + 30, fy, "K")
            pfill(im, x0 + bx + 4, fy - h + 4, x0 + bx + 26, fy - h + 5, "h")
    elif kind == "office":
        window(im, x0 + 20, y0 + 16, 120, 34, city, 200 + c * 40)
        for dx in (20, 90):                               # 책상 + 모니터
            pfill(im, x0 + dx, fy - 16, x0 + dx + 44, fy - 14, "3"); pfill(im, x0 + dx + 2, fy - 13, x0 + dx + 4, fy, "4")
            pfill(im, x0 + dx + 14, fy - 30, x0 + dx + 30, fy - 18, "K"); pfill(im, x0 + dx + 16, fy - 28, x0 + dx + 28, fy - 20, "B")
    elif kind == "server":
        for bx in range(10, 150, 28):                     # 서버 랙
            pfill(im, x0 + bx, y0 + 16, x0 + bx + 20, fy, "k"); pbox(im, x0 + bx, y0 + 16, x0 + bx + 20, fy, "K")
            for ly in range(y0 + 22, fy - 4, 6):
                pset(im, x0 + bx + 4, ly, "G" if (bx + ly) % 3 else "R")
    elif kind == "stairs":
        for i in range(12):                               # 계단 사선
            pfill(im, x0 + 40 + i * 7, fy - i * 6 - 6, x0 + 46 + i * 7, fy - i * 6, "3")
        draw_line(im, [(x0 + 40, fy - 20), (x0 + 124, fy - 90)], "2", 2)  # 난간
    elif kind == "elevator":
        pfill(im, x0 + 50, y0 + WALL, x0 + 110, y1 - WALL, "a")          # 승강로
        pfill(im, x0 + 78, y0 + WALL, x0 + 82, y1 - WALL, "2")           # 케이블
    pbox(im, x0, y0, x1, y1, "K")


def building():
    city = Image.open(EXPORT / "backgrounds/city_night_composite.png").convert("RGBA")
    im = city.resize((MW, MH), Image.NEAREST) if False else canvas(MW, MH, PIX["a"])
    # 바깥 하늘·도시(맵 뒤) — 배경 합본을 위쪽에 깔기
    im.alpha_composite(city.crop((0, 0, 640, 360)).crop((0, 0, 640, MH)), (0, 0))
    im.alpha_composite(city.crop((480, 0, 640, MH)), (640, 0))
    pfill(im, 0, sy(0) - 8, MW - 1, sy(0) - 1, "4")       # 지붕 슬래브
    pfill(im, 0, sy(0) - 8, MW - 1, sy(0) - 8, "3")
    for f, row in enumerate(ROOMS):
        for c, kind in enumerate(row):
            room(im, kind, c, f, city)
    pfill(im, 0, sy(FLOORS), MW - 1, MH - 1, "K")         # 지면
    pfill(im, 0, sy(FLOORS), MW - 1, sy(FLOORS) + 1, "2")
    # 큰 방(창고 2섹터, 사무실 2섹터) 가상 경계선 — 점선
    for (c, f) in [(3, 2), (1, 0)]:
        for y in range(sy(f) + WALL, sy(f) + SH - WALL, 4):
            pfill(im, sx(c), y, sx(c), y + 1, "C")
    for (c, f) in [(3, 2), (1, 0)]:   # 실제 벽 대신 점선만 보이도록 벽선 지우기
        for y in range(sy(f) + WALL, sy(f) + SH - WALL):
            if (y - sy(f) - WALL) % 4 >= 2:
                pset(im, sx(c), y, "4" if f == 2 else "1")
    # 건물 이름은 게임 장면 머리글에 표시(W-006)
    return im


# ---------------- 오브젝트(현실/설계도) ----------------
def door(open_):
    im = canvas(12, 60)
    pfill(im, 0, 0, 11, 59, "K")
    if open_:
        pfill(im, 1, 1, 3, 58, "D"); pfill(im, 8, 1, 10, 58, "D")
    else:
        pfill(im, 1, 1, 10, 58, "D"); pfill(im, 1, 1, 10, 2, "h"); pset(im, 8, 30, "h"); pset(im, 8, 31, "h")
    return im


def shutter(open_):
    im = canvas(40, 70)
    pbox(im, 0, 0, 39, 69, "K"); pfill(im, 1, 1, 38, 4, "3")
    if not open_:
        for y in range(5, 69, 4):
            pfill(im, 1, y, 38, y + 2, "2"); pfill(im, 1, y + 3, 38, y + 3, "4")
        pfill(im, 14, 60, 26, 63, "Y")
    return im


def elevator_car(running):
    im = canvas(56, 80)
    pfill(im, 0, 0, 55, 79, "3"); pbox(im, 0, 0, 55, 79, "K")
    pfill(im, 4, 6, 51, 76, "2"); pfill(im, 27, 6, 28, 76, "K")
    pfill(im, 22, 1, 33, 4, "a")
    draw_text(im, 24, 0, "^" if running else "-", "G" if running else "R") if False else pfill(im, 25, 2, 30, 3, "G" if running else "R")
    return im


def blocker(open_):
    """섹터 전체형 — 바닥 차단막(섹터 전체를 막는 레이저 펜스)."""
    im = canvas(SW, SH)
    if not open_:
        for x in range(8, SW - 8, 12):
            pfill(im, x, WALL + 2, x + 1, SH - WALL - 1, "R")
        pfill(im, 4, WALL, SW - 5, WALL + 1, "r")
    return im


def exit_sign():
    im = canvas(28, 14, PIX["g"])
    pbox(im, 0, 0, 27, 13, "K"); draw_text(im, 3, 3, "EXIT", "W")
    return im


def terminal():
    im = canvas(20, 28)
    pfill(im, 4, 12, 15, 27, "2"); pfill(im, 0, 0, 19, 12, "K"); pfill(im, 2, 2, 17, 10, "Y")
    pfill(im, 4, 4, 15, 4, "y"); pfill(im, 4, 7, 12, 7, "y")
    outline_outside(im, "K")
    return im


def relay():
    im = canvas(16, 30)
    pfill(im, 7, 0, 8, 12, "K"); pfill(im, 6, 0, 9, 1, "G")
    pfill(im, 2, 12, 13, 29, "3"); pfill(im, 4, 22, 11, 27, "2"); pfill(im, 7, 16, 8, 17, "G")
    outline_outside(im, "K")
    return im


def bp_sector():
    """섹터가 설계도 상태일 때 겹치는 청사진(섹터 전체)."""
    im = canvas(SW, SH)
    dither(im, (1, 1, SW - 2, SH - 2), "B", 2)
    for x in range(0, SW, 16):
        pfill(im, x, 1, x, SH - 2, "b")
    for y in range(0, SH, 16):
        pfill(im, 1, y, SW - 2, y, "b")
    pbox(im, 0, 0, SW - 1, SH - 1, "C")
    return im


def bp_outline(obj):
    """오브젝트의 설계도 상태 그림: 실루엣을 청사진 선으로."""
    out = canvas(obj.width + 2, obj.height + 2)
    px = obj.load()
    for y in range(obj.height):
        for x in range(obj.width):
            if px[x, y][3]:
                pset(out, x + 1, y + 1, "B" if (x + y) % 2 else "b")
    outline_outside(out, "C")
    return out


def sector_cursor():
    im = canvas(SW, SH)
    for (x, y, dx, dy) in [(0, 0, 1, 1), (SW - 1, 0, -1, 1), (0, SH - 1, 1, -1), (SW - 1, SH - 1, -1, -1)]:
        for i in range(14):
            for t in (0, 1):
                pset(im, x + dx * i, y + dy * t, "R"); pset(im, x + dx * t, y + dy * i, "R")
    return im


# 오브젝트 배치(맵 좌표): (이름, 그림 함수, x, y_바닥기준, 층)
def place(im, sprite, c, f, x_in, from_floor=True):
    fy = sy(f) + SH - WALL
    im.alpha_composite(sprite, (sx(c) + x_in, fy - sprite.height if from_floor else sy(f) + WALL))


def map_with_objects(blueprint_sector=None):
    im = building()
    place(im, door(False), 1, 2, -6)                      # 1F 로비|복도 문
    place(im, door(True), 2, 1, -6)                       # 2F 복도 문
    place(im, shutter(False), 4, 2, 60)                   # 1F 계단 셔터
    place(im, shutter(True), 3, 1, 60)                    # 2F 계단 셔터
    place(im, elevator_car(False), 4, 0, 52)              # 엘리베이터
    place(im, terminal(), 2, 2, 120)                      # A 단말(창고)
    place(im, terminal(), 2, 0, 20)                       # B 단말(서버실)
    place(im, relay(), 1, 1, 70)                          # 중계기
    pfill(im, sx(0) + 14, sy(1) + SH - WALL - 40, sx(0) + 30, sy(1) + SH - WALL - 1, "g")   # 출구 문
    im.alpha_composite(exit_sign(), (sx(0) + 8, sy(1) + SH - WALL - 58))                    # 출구 표지(문 위)
    im.alpha_composite(blocker(False), (sx(3), sy(0)))    # 3F 복도 차단막(섹터 전체형)
    place(im, upscale_courier(), 0, 2, 40)
    if blueprint_sector:
        c, f = blueprint_sector
        im.alpha_composite(bp_sector(), (sx(c), sy(f)))
        im.alpha_composite(bp_outline(shutter(True)), (sx(c) + 59, sy(f) + SH - WALL - 71))
    return im


def upscale_courier():
    return Image.open(EXPORT / "sprites/courier_right.png").convert("RGBA")


def game_mockup():
    """W-003 게임 화면 틀 안에 단면도 장면을 카메라로 잘라 넣은 목업."""
    scr = Image.open(EXPORT / "preview/ui_mockup_mission.png").convert("RGBA")
    m = map_with_objects(blueprint_sector=(4, 2))
    m.alpha_composite(sector_cursor(), (sx(4), sy(2)))
    # 장면 패널 안쪽(16,48)~(428,308) = 412×260 에 맵 일부(카메라)를 넣는다
    cam_x, cam_y = MW - 412, MH - 260
    view = m.crop((cam_x, cam_y, cam_x + 412, cam_y + 260))
    pfill(scr, 16, 48, 427, 307, "a")
    scr.alpha_composite(view, (16, 48))
    pbox(scr, 15, 47, 428, 308, "b")
    # 화면 밖 의뢰 대상 방향 표시(왼쪽 가장자리)
    for i in range(5):
        pfill(scr, 20 + i, 270 - i, 20 + i, 270 + i, "R")
    draw_text(scr, 30, 267, "TARGET", "R")
    return scr


def build():
    made = []
    full = map_with_objects()
    made.append(save(full, "scenes/building_test_map.png", {"status": "시안", "note": "측면 단면도 테스트 맵(임시 배치)"}))
    made.append(save(building(), "scenes/building_test_base.png",
                     {"status": "시안", "note": "오브젝트를 뺀 건물 배경 — 구현에서 오브젝트를 따로 그림",
                      "sector": [SW, SH], "roof": ROOF, "floors": FLOORS, "cols": COLS, "wall": WALL}))
    made.append(save(upscale(full, 2), "preview/building_test_map_x2.png", {"status": "미리보기"}))
    objs = {"door_closed": door(False), "door_open": door(True), "shutter_closed": shutter(False),
            "shutter_open": shutter(True), "elevator_stopped": elevator_car(False), "elevator_running": elevator_car(True),
            "blocker_on": blocker(False), "exit_sign": exit_sign(), "terminal": terminal(), "relay_box": relay(),
            "bp_sector": bp_sector(), "sector_cursor": sector_cursor()}
    for k, im in objs.items():
        assert not palette_check(im), k
        made.append(save(im, f"objects/{k}.png", {"status": "시안", "style": "pixel"}))
    for k in ("door_open", "shutter_open", "elevator_running"):
        made.append(save(bp_outline(objs[k]), f"objects/bp_{k}.png", {"status": "시안", "layer": "blueprint"}))
    gm = game_mockup()
    assert not palette_check(gm)
    made.append(save(gm, "preview/building_game_mockup.png", {"status": "목업"}))
    made.append(save(upscale(gm, 2), "preview/building_game_mockup_x2.png", {"status": "목업 2배"}))
    return made
