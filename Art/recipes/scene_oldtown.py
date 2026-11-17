"""챕터 1(구시가지 여러 빌딩, D-067) 맵·이동형 오브젝트 시안 — 02-02 §0-3(위치 어긋남 제안), 03-01 §2

- 맵 = 건물 A(왼쪽, 2층 + 옥상) · 골목 틈 · 건물 B(오른쪽, 3층)
- 낡은 벽돌 외벽, 옛 상가·셋방·세탁소 실내, 골목 틈은 바닥 없이 도시가 보임
- 이동형 오브젝트: 건물 사이 철판 다리(단계 0~3, 1단계 32px), 옥상 간판 발판
[시안] 배치는 03-01 §2 제안
"""
from PIL import Image

import scene_building as sb
import scene_home as sh
from artkit import EXPORT, PIX, canvas, draw_line, over, palette_check, pbox, pfill, pset, save, upscale
from pixfont import draw_text

# 층 0 = 옥상층(3F), 1 = 2F, 2 = 1F. None = 하늘(건물 없음), "gap" = 골목 틈
ROWS = [
    ["roof", "roof", "gap", "flat", "flat"],
    ["flat", "stairs", "gap", "corridor", "stairs"],
    ["shop", "stairs", "alley", "lobby", "stairs"],
]
BRICK, BRICK_D = "d", "D"


def brick_wall(im, x0, y0, x1, y1):
    """외벽 단면: 어두운 벽돌색 띠 + 테두리(사다리처럼 보이지 않게 단색)."""
    pfill(im, x0, y0, x1, y1, BRICK)
    pbox(im, x0, y0, x1, y1, "K")


def room(im, kind, c, f, city):
    x0, y0 = sb.sx(c), sb.sy(f)
    x1, y1 = x0 + sb.SW - 1, y0 + sb.SH - 1
    fy = y1 - sb.WALL
    if kind in ("gap", "alley"):
        cx = (x0 + 120) % 480 if (x0 + 120) % 480 < 260 or (x0 + 120) % 480 > 360 else 380   # 관리 타워(청사진) 구역을 피해 자름
        im.alpha_composite(city.crop((cx, y0, cx + sb.SW, y0 + sb.SH)), (x0, y0))
        if kind == "alley":                                   # 1층 골목: 철망(통행 불가)
            pfill(im, x0, fy + 1, x1, y1, "4")
            for x in range(x0 + 4, x1, 8):
                draw_line(im, [(x, fy - 40), (x + 8, fy)], "2"); draw_line(im, [(x + 8, fy - 40), (x, fy)], "2")
            pfill(im, x0, fy - 42, x1, fy - 41, "3")
        return
    if kind == "roof":                                        # 옥상: 하늘 + 난간 + 물탱크(관리 타워 구역을 피해 자름)
        im.alpha_composite(city.crop((420, y0, 420 + sb.SW, y0 + sb.SH)), (x0, y0))
        pfill(im, x0, fy + 1, x1, y1, "2"); pfill(im, x0, fy + 1, x1, fy + 1, "3")
        for x in range(x0, x1, 10):
            pfill(im, x, fy - 12, x + 1, fy, "3")
        pfill(im, x0, fy - 12, x1, fy - 11, "3")
        if c == 0:
            pfill(im, x0 + 30, fy - 40, x0 + 70, fy, "4"); pbox(im, x0 + 30, fy - 40, x0 + 70, fy, "K")
        return
    if kind == "shop":
        pfill(im, x0, y0 + sb.WALL, x1, y1 - sb.WALL, "1")
        pfill(im, x0 + 10, fy - 30, x0 + 80, fy, "4"); pfill(im, x0 + 10, fy - 32, x0 + 80, fy - 30, "3")   # 진열대
        for i in range(5):
            pfill(im, x0 + 14 + i * 13, fy - 28, x0 + 22 + i * 13, fy - 20, "RGYCh"[i])
        pfill(im, x0 + 100, y0 + 20, x0 + 150, y0 + 34, "R"); draw_text(im, x0 + 104, y0 + 24, "24H", "W")
        pfill(im, x0, y1 - sb.WALL + 1, x1, y1, "2"); pbox(im, x0, y0, x1, y1, "K")
        return
    if kind == "flat":                                        # 셋방: 낡은 벽지, 창, 옷걸이
        pfill(im, x0, y0 + sb.WALL, x1, y1 - sb.WALL, "4")
        for x in range(x0 + 6, x1, 12):
            pfill(im, x, y0 + sb.WALL, x, y1 - sb.WALL, "2")
        sb.window(im, x0 + 20, y0 + 18, 44, 30, city, 120 + c * 30)
        draw_line(im, [(x0 + 90, y0 + 20), (x0 + 150, y0 + 20)], "3")
        for i, col in enumerate("RYG"):
            pfill(im, x0 + 96 + i * 18, y0 + 21, x0 + 106 + i * 18, y0 + 40, col)
        pfill(im, x0, y1 - sb.WALL + 1, x1, y1, "2"); pbox(im, x0, y0, x1, y1, "K")
        return
    sb.room(im, kind, c, f, city)


STEP = 40          # 1단계 이동 거리(px). 4단계면 틈(160px)을 다 덮는다


def bridge(step):
    """철판 다리 그림(160×8). 단계 0 = 건물 A 안에 접혀 있음, 단계 4 = 틈을 다 덮음."""
    im = canvas(160, 8)
    pfill(im, 0, 0, 159, 5, "3"); pfill(im, 0, 6, 159, 7, "4"); pbox(im, 0, 0, 159, 7, "K")
    for x in range(6, 154, 12):
        pset(im, x, 3, "2")
    return im


def sign_plank():
    """옥상 간판(발판으로 쓰는 낡은 간판)."""
    im = canvas(96, 14)
    pfill(im, 0, 0, 95, 13, "R"); pbox(im, 0, 0, 95, 13, "K"); draw_text(im, 6, 4, "HOTEL ROSE", "W")
    return im


def base():
    floors, cols = len(ROWS), len(ROWS[0])
    mw, mh = sb.SW * cols, sb.ROOF + sb.SH * floors + sb.GROUND
    city = Image.open(EXPORT / "backgrounds/city_night_composite.png").convert("RGBA")
    im = canvas(mw, mh, PIX["a"])
    im.alpha_composite(city.crop((0, 0, 640, mh)), (0, 0))
    im.alpha_composite(city.crop((480, 0, 640, mh)), (640, 0))
    for f, row in enumerate(ROWS):
        for c, kind in enumerate(row):
            room(im, kind, c, f, city)
    # 외벽(벽돌): 건물 A 오른쪽 끝, 건물 B 왼쪽 끝 바깥면
    brick_wall(im, sb.sx(2) - 6, sb.sy(1), sb.sx(2) - 1, sb.sy(3) - 1)
    brick_wall(im, sb.sx(3), sb.sy(0), sb.sx(3) + 5, sb.sy(3) - 1)
    pfill(im, 0, sb.sy(floors), mw - 1, mh - 1, "K"); pfill(im, 0, sb.sy(floors), mw - 1, sb.sy(floors) + 1, "2")
    return im


def preview(step=4):
    im = base()
    fy2 = sb.sy(1) + sb.SH - sb.WALL                          # 2F 바닥선
    bx = sb.sx(2) - 160 + step * STEP                         # 단계 0 = 건물 A 안에 접혀 있음
    im.alpha_composite(bridge(step), (bx, fy2 - 2))
    im.alpha_composite(sign_plank(), (sb.sx(2) + 32, sb.sy(0) + sb.SH - sb.WALL - 2))
    sb.place(im, sb.terminal(), 4, 0, 90)
    pfill(im, sb.sx(3) + 130, sb.sy(2) + sb.SH - sb.WALL - 40, sb.sx(3) + 146, sb.sy(2) + sb.SH - sb.WALL - 1, "g")
    im.alpha_composite(sb.exit_sign(), (sb.sx(3) + 124, sb.sy(2) + sb.SH - sb.WALL - 58))
    sb.place(im, sh.runner("right"), 0, 2, 20)
    return im


def build():
    made = []
    b = base()
    assert not palette_check(b)
    made.append(save(b, "scenes/ch1_oldtown1_base.png", {"status": "시안", "note": "챕터 1 구시가지 맵 배경(오브젝트 제외)",
                                                         "sector": [sb.SW, sb.SH], "roof": sb.ROOF, "wall": sb.WALL,
                                                         "floors": len(ROWS), "cols": len(ROWS[0])}))
    for st in (0, 2, 4):
        made.append(save(upscale(preview(st), 2), f"preview/ch1_oldtown1_step{st}_x2.png", {"status": "미리보기"}))
    made.append(save(bridge(0), "objects/bridge_plate.png", {"status": "시안", "note": "이동형, 1단계 40px, 0~4단계"}))
    made.append(save(sb.bp_outline(bridge(0)), "objects/bp_bridge_plate.png", {"status": "시안", "layer": "blueprint"}))
    made.append(save(sign_plank(), "objects/sign_plank.png", {"status": "시안"}))
    return made
