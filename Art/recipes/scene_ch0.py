"""챕터 0(튜토리얼, Shift 단독) 의뢰 0-1·0-2 단면도 맵 시안 — D-062, D-063~D-066, 03-01 v0.3.0

scene_building 의 방 그리기·오브젝트 함수를 재사용한다. 배치는 [제안](레벨 기획 확정 전).
"""
from PIL import Image

import scene_building as sb
from artkit import EXPORT, PIX, canvas, over, palette_check, pfill, save, upscale
from pixfont import draw_text

LEVELS = {
    # 0-1: 1층 4섹터 — 조준 점프 + Shift로 문 열기
    "ch0_1": {"rooms": [["lobby", "corridor", "office", "corridor"]], "title": "SANDBOX BLDG A"},
    # 0-2: 2층 4섹터 — 계단, 섹터 전체형 차단막, 중계기, 별을 위한 되돌아가기
    "ch0_2": {"rooms": [["office", "corridor", "corridor", "stairs"],
                        ["lobby", "storage", "corridor", "stairs"]], "title": "SANDBOX BLDG B"},
}


def base(rooms, title):
    floors, cols = len(rooms), len(rooms[0])
    mw, mh = sb.SW * cols, sb.ROOF + sb.SH * floors + sb.GROUND
    city = Image.open(EXPORT / "backgrounds/city_night_composite.png").convert("RGBA")
    im = canvas(mw, mh, PIX["a"])
    im.alpha_composite(city.crop((0, 0, min(640, mw), mh)), (0, 0))
    if mw > 640:
        im.alpha_composite(city.crop((640 - (mw - 640), 0, 640, mh)), (640, 0))
    pfill(im, 0, sb.sy(0) - 8, mw - 1, sb.sy(0) - 1, "4"); pfill(im, 0, sb.sy(0) - 8, mw - 1, sb.sy(0) - 8, "3")
    for f, row in enumerate(rooms):
        for c, kind in enumerate(row):
            sb.room(im, kind, c, f, city)
    pfill(im, 0, sb.sy(floors), mw - 1, mh - 1, "K"); pfill(im, 0, sb.sy(floors), mw - 1, sb.sy(floors) + 1, "2")
    draw_text(im, 6, sb.sy(0) - 18, title, "3")
    return im


def exit_door(im, c, f):
    fy = sb.sy(f) + sb.SH - sb.WALL
    pfill(im, sb.sx(c) + 130, fy - 40, sb.sx(c) + 146, fy - 1, "g")
    im.alpha_composite(sb.exit_sign(), (sb.sx(c) + 124, fy - 58))


def objects_0_1(im):
    sb.place(im, sb.door(False), 1, 0, -6)          # D1 닫힘(조작 가능)
    sb.place(im, sb.terminal(), 1, 0, 80)           # A
    sb.place(im, sb.door(False), 3, 0, -6)          # D2 닫힘(조작 가능)
    sb.place(im, sb.terminal(), 2, 0, 80)           # B
    exit_door(im, 3, 0)
    sb.place(im, sb.upscale_courier(), 0, 0, 30)


def objects_0_2(im):
    sb.place(im, sb.terminal(), 1, 1, 60)           # A 창고(지나가는 길)
    sb.place(im, sb.shutter(False), 3, 1, 50)       # 1F 계단 셔터 S1(조작 가능)
    sb.place(im, sb.terminal(), 3, 1, 118)          # B: 계단 너머 끝(되돌아가야 얻음)
    sb.place(im, sb.shutter(True), 3, 0, 50)        # 2F 도착 계단
    im.alpha_composite(sb.blocker(False), (sb.sx(2), sb.sy(0)))   # 2F 차단막(섹터 전체형)
    sb.place(im, sb.relay(), 1, 0, 70)              # 중계기
    exit_door(im, 0, 0)
    sb.place(im, sb.upscale_courier(), 0, 1, 30)


def build():
    made = []
    for key, spec in LEVELS.items():
        b = base(spec["rooms"], spec["title"])
        assert not palette_check(b), key
        made.append(save(b, f"scenes/{key}_base.png", {"status": "시안", "note": "챕터 0 맵 배경(오브젝트 제외)",
                                                       "sector": [sb.SW, sb.SH], "roof": sb.ROOF, "wall": sb.WALL,
                                                       "floors": len(spec["rooms"]), "cols": len(spec["rooms"][0])}))
        full = b.copy()
        (objects_0_1 if key == "ch0_1" else objects_0_2)(full)
        assert not palette_check(full), key
        made.append(save(upscale(full, 2), f"preview/{key}_map_x2.png", {"status": "미리보기"}))
    return made
