"""메인(방 일러스트)·모니터 로딩·컴퓨터 바탕화면 아트 시안 — D-056~D-059, 05-02-screen-flow.md v0.2.0

- 640×360, 도트, 공유 팔레트, 이펙트·파티클 없음(D-040 정신 유지 — 빛 번짐은 디더로만)
- 메인: 주인공 뒤쪽 비스듬한 시점 [제안]. 마우스는 책상에 없다(D-036)
- 레이어로 내보내 Unity 에서 모니터 꺼짐/켜짐·로딩 단계를 겹쳐 바꾼다
[시안] 사용자 확정 전.
"""
from PIL import Image

from artkit import (EXPORT, PIX, canvas, draw_ellipse, draw_line, draw_poly, outline_outside, over,
                    palette_check, pbox, pfill, pset, save, upscale)
from pixfont import draw_text, text_width
from ui_mvp import badge, button, frame, gauge, star, window_frame

W, H = 640, 360
SCR = (246, 116, 394, 206)   # 메인 모니터 화면 영역(포함)


def dither(im, box, ch, step=2, phase=0):
    x0, y0, x1, y1 = box
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if (x + y + phase) % step == 0:
                pset(im, x, y, ch)


# ---------------- 방 ----------------
def room_wall():
    im = canvas(W, H, PIX["k"])
    for x in range(0, W, 80):
        pfill(im, x, 0, x, 299, "a")
    pfill(im, 0, 300, W - 1, H - 1, "a")                 # 바닥
    for y in range(306, H, 10):
        pfill(im, 0, y, W - 1, y, "k")
    # 창문(오른쪽) — 창밖 도시는 배경 합본에서 잘라 쓴다
    city = Image.open(EXPORT / "backgrounds/city_night_composite.png").convert("RGBA")
    view = city.crop((230, 60, 410, 230))
    pfill(im, 426, 26, 614, 204, "2")
    im.alpha_composite(view, (430, 30))
    pfill(im, 518, 30, 521, 200, "2"); pfill(im, 430, 113, 610, 116, "2")   # 창살
    pbox(im, 426, 26, 614, 204, "K")
    pfill(im, 420, 204, 620, 209, "3"); pfill(im, 420, 209, 620, 210, "4")  # 창턱
    # 청사진 포스터(왼쪽 벽)
    pfill(im, 40, 116, 122, 206, "B"); pbox(im, 40, 116, 122, 206, "K")
    for x in range(48, 116, 8):
        pfill(im, x, 122, x, 200, "b")
    pbox(im, 70, 136, 92, 200, "C"); pbox(im, 74, 126, 88, 136, "C"); pfill(im, 81, 120, 81, 126, "C")
    for (x, y) in [(40, 116), (118, 116), (40, 202), (118, 202)]:
        pfill(im, x, y, x + 4, y + 4, "h")                # 테이프
    return im


def desk():
    im = canvas(W, H)
    pfill(im, 110, 228, 570, 233, "3"); pfill(im, 110, 234, 570, 238, "2")   # 상판
    pfill(im, 110, 239, 570, 300, "4")
    pfill(im, 120, 239, 126, 330, "K"); pfill(im, 554, 239, 560, 330, "K")   # 다리
    pbox(im, 110, 228, 570, 300, "K")
    # 서랍
    pbox(im, 470, 246, 560, 270, "K"); pfill(im, 510, 257, 520, 258, "3")
    # 키보드(마우스 없음)
    pfill(im, 268, 222, 372, 229, "K"); pfill(im, 270, 223, 370, 227, "2")
    for x in range(272, 368, 4):
        pfill(im, x, 224, x + 1, 224, "3"); pfill(im, x + 2, 226, x + 3, 226, "3")
    # 에너지 음료 캔
    for x, c in [(170, "R"), (182, "G"), (160, "2")]:
        pfill(im, x, 210, x + 8, 227, c); pfill(im, x, 210, x + 8, 211, "3"); pbox(im, x, 210, x + 8, 227, "K")
    # 스탠드 조명(꺼짐)
    pfill(im, 128, 222, 150, 227, "2"); draw_line(im, [(140, 222), (148, 180), (166, 168)], "K", 2)
    draw_poly(im, [(160, 162), (178, 162), (182, 174), (156, 174)], "2")
    # 케이블
    draw_line(im, [(320, 238), (322, 270), (300, 300), (300, 330)], "K", 2)
    draw_line(im, [(440, 238), (444, 280), (470, 320)], "K", 2)
    return im


def monitors(on):
    im = canvas(W, H)
    # 메인 모니터
    pfill(im, 240, 110, 400, 212, "K"); pbox(im, 241, 111, 399, 211, "2")
    pfill(im, 312, 213, 328, 222, "2"); pfill(im, 296, 222, 344, 225, "2"); pbox(im, 296, 222, 344, 225, "K")
    x0, y0, x1, y1 = SCR
    if on:
        pfill(im, x0, y0, x1, y1, "B")
        dither(im, (x0, y0, x1, y1), "b", 4)
    else:
        pfill(im, x0, y0, x1, y1, "a")
        for i in range(12):                                   # 꺼진 화면 반사 사선
            pset(im, x0 + 10 + i, y0 + 6 + i, "j"); pset(im, x0 + 14 + i, y0 + 6 + i, "j")
    pset(im, 392, 208, "G" if on else "r")                    # 전원 표시등
    # 보조 모니터(세로)
    pfill(im, 408, 126, 466, 212, "K"); pbox(im, 409, 127, 465, 211, "2")
    pfill(im, 431, 213, 443, 222, "2"); pfill(im, 422, 222, 452, 225, "2")
    if on:
        pfill(im, 412, 130, 462, 208, "a")
        for i, y in enumerate(range(134, 204, 6)):            # 로그 글줄(작은 막대)
            pfill(im, 414, y, 414 + (17 * (i + 3)) % 44, y + 1, "G" if i % 3 else "C")
    else:
        pfill(im, 412, 130, 462, 208, "a")
    return im


def hacker_back(lit):
    """주인공 뒷모습(의자에 앉음). lit=True 면 모니터 빛 테두리."""
    im = canvas(W, H)
    # 의자 등받이
    draw_poly(im, [(262, 196), (346, 196), (354, 300), (254, 300)], "K")
    draw_poly(im, [(266, 200), (342, 200), (348, 296), (260, 296)], "4")
    pfill(im, 300, 300, 308, 322, "K")                        # 기둥
    pfill(im, 270, 322, 340, 326, "K")                        # 다리
    for x in (270, 300, 336):
        pfill(im, x - 2, 326, x + 2, 330, "2")                # 바퀴
    # 어깨·재킷(등받이 위로 보임)
    draw_poly(im, [(256, 214), (270, 188), (338, 188), (352, 214), (346, 222), (262, 222)], "2")
    draw_poly(im, [(284, 184), (324, 184), (330, 196), (278, 196)], "4")   # 후드 깃
    # 왼팔(키보드로 뻗음)
    draw_poly(im, [(256, 206), (264, 204), (270, 226), (258, 228)], "2")
    pfill(im, 262, 224, 270, 228, "S")
    # 머리(뒷머리 단발)
    draw_ellipse(im, (280, 138, 330, 192), "H")
    draw_poly(im, [(280, 166), (330, 166), (334, 190), (276, 190)], "H")
    for x in range(282, 330, 6):                              # 머리결
        draw_line(im, [(x, 150), (x + 1, 188)], "J")
    draw_line(im, [(296, 142), (292, 156)], "U"); draw_line(im, [(312, 142), (316, 156)], "U")
    # 헤드셋: 밴드는 머리 위쪽 반원만 + 양쪽 귀 컵
    band = canvas(W, H)
    draw_ellipse(band, (276, 134, 334, 200), "3", width=3)
    bp = band.load()
    for y in range(168, 201):
        for x in range(270, 340):
            bp[x, y] = (0, 0, 0, 0)
    im.alpha_composite(band)
    pfill(im, 272, 160, 282, 182, "K"); pfill(im, 273, 161, 281, 181, "3"); pfill(im, 275, 164, 279, 178, "2")
    pfill(im, 328, 160, 338, 182, "K"); pfill(im, 329, 161, 337, 181, "3"); pfill(im, 331, 164, 335, 178, "2")
    pfill(im, 300, 132, 308, 135, "h")                        # 테이프
    draw_line(im, [(276, 182), (270, 210)], "K", 2)           # 케이블
    if lit:  # 모니터 빛이 닿는 머리 윗부분·어깨 테두리
        for x in range(286, 326):
            pset(im, x, 139 if 292 < x < 318 else 141, "C")
        for x in range(272, 338, 2):
            pset(im, x, 189, "b")
    return im


def light_spill():
    """모니터가 켜졌을 때 벽·책상에 비치는 빛(디더)."""
    im = canvas(W, H)
    cx, cy = 320, 160
    for y in range(60, 240):
        for x in range(170, 424):          # 창문(x≥426)에는 비치지 않게
            d = ((x - cx) / 150) ** 2 + ((y - cy) / 95) ** 2
            if d < 0.55 and (x + y) % 2 == 0:
                pset(im, x, y, "j")
            elif d < 1.0 and (x + y) % 4 == 0:
                pset(im, x, y, "j")
    dither(im, (226, 228, 414, 233), "b", 3)   # 책상 상판에 닿는 빛
    return im


def screen_content(stage):
    """메인 모니터 화면 안 부팅 로딩. stage 0~3."""
    im = canvas(W, H)
    x0, y0, x1, y1 = SCR
    lines = ["SHIFT OS 0.1", "LINK ......... OK", "MOUNT /CITY ... OK", "TRACE GUARD ... OK"]
    for i, t in enumerate(lines[: stage + 1]):
        draw_text(im, x0 + 6, y0 + 6 + i * 10, t, "C" if i == 0 else "G")
    g = gauge(x1 - x0 - 12, [0.15, 0.45, 0.8, 1.0][stage])
    im.alpha_composite(g, (x0 + 6, y1 - 16))
    return im


def menu_overlay(selected=0):
    im = canvas(W, H)
    lg = Image.open(EXPORT / "ui/logo.png").convert("RGBA")
    lg = lg.resize((lg.width // 2, lg.height // 2), Image.NEAREST)   # 정수 배율 축소(4배 → 2배 글자)
    im.alpha_composite(lg, (20, 18))
    for i, t in enumerate(["START", "CREDITS"]):
        bx, by = 32, 64 + i * 20
        im.alpha_composite(button(84, i == selected), (bx, by))
        draw_text(im, bx + (84 - text_width(t)) // 2, by + 3, t, "W")
        if i == selected:
            draw_text(im, bx - 10, by + 3, ">", "R")
    bar = canvas(W, 20, PIX["a"])
    im.alpha_composite(bar, (0, 340))
    x = 12
    for key, label in [("↑↓", "SELECT"), ("ENTER", "OK")]:
        b = badge(key); im.alpha_composite(b, (x, 344)); x += b.width + 3
        draw_text(im, x, 346, label, "3"); x += text_width(label) + 10
    return im


# ---------------- 바탕화면 ----------------
def wallpaper():
    im = canvas(W, H, PIX["B"])
    for x in range(0, W, 16):
        pfill(im, x, 0, x, H - 1, "b")
    for y in range(0, H, 16):
        pfill(im, 0, y, W - 1, y, "b")
    # 가운데 큰 타워 도면(선)
    pbox(im, 280, 60, 360, 330, "C"); pbox(im, 296, 30, 344, 60, "C"); pfill(im, 319, 8, 320, 30, "C")
    for y in range(80, 330, 20):
        pfill(im, 284, y, 356, y, "b")
    draw_text(im, 370, 300, "CITY ADMIN TOWER - REV.03", "b")
    return im


ICON_LABEL = {"clear": "G", "new": "Y", "locked": "4"}


def file_icon(state):
    im = canvas(32, 32)
    draw_poly(im, [(6, 2), (20, 2), (26, 8), (26, 29), (6, 29)], "W" if state != "locked" else "3")
    draw_poly(im, [(20, 2), (26, 8), (20, 8)], "3")
    for y in (12, 16, 20):
        pfill(im, 10, y, 22, y, "b" if state != "locked" else "4")
    outline_outside(im, "K")
    if state == "locked":  # 자물쇠
        pbox(im, 13, 15, 19, 19, "K"); pfill(im, 11, 19, 21, 27, "Y"); pbox(im, 11, 19, 21, 27, "K"); pset(im, 16, 23, "K")
    elif state == "clear":
        for (x, y) in [(18, 24), (19, 25), (20, 26), (21, 25), (22, 24), (23, 23), (24, 22)]:
            pset(im, x, y, "G"); pset(im, x, y + 1, "G")
    elif state == "new":
        pfill(im, 22, 22, 27, 27, "Y"); pbox(im, 22, 22, 27, 27, "K")
    return im


REQS = [("REQ_01", "clear", 2), ("REQ_02", "locked", 0), ("REQ_03", "locked", 0), ("REQ_04", "locked", 0)]


def desktop(selected=0, panel=False, connecting=None):
    im = wallpaper()
    for i, (name, state, stars) in enumerate(REQS):
        x, y = 24, 20 + i * 72
        if i == selected:
            pfill(im, x - 6, y - 4, x + 50, y + 58, "a"); pbox(im, x - 6, y - 4, x + 50, y + 58, "C")
        im.alpha_composite(file_icon(state), (x + 6, y))
        draw_text(im, x + 22 - text_width(name) // 2, y + 36, name, "W" if state != "locked" else "3")
        for s in range(3):
            im.alpha_composite(star(s < stars), (x + 6 + s * 11, y + 46))
    # 작업 표시줄
    pfill(im, 0, 340, W - 1, H - 1, "a"); pfill(im, 0, 340, W - 1, 340, "C")
    pfill(im, 4, 343, 52, 356, "B"); pbox(im, 4, 343, 52, 356, "C"); draw_text(im, 13, 347, "SHIFT", "W")
    x = 62
    hints = [("↑↓", "SELECT"), ("ENTER", "CONNECT" if panel else "OPEN"), ("ESC", "CLOSE" if panel else "LOG OFF")]
    for key, label in hints:
        b = badge(key); im.alpha_composite(b, (x, 344)); x += b.width + 3
        draw_text(im, x, 346, label, "3"); x += text_width(label) + 10
    draw_text(im, W - 8 - text_width("21:04"), 347, "21:04", "W")
    if panel:
        im.alpha_composite(detail_panel(), (352, 16))
    if connecting is not None:
        im.alpha_composite(connect_box(connecting), (180, 140))
    return im


def detail_panel():
    pw, ph = 280, 318
    p = window_frame(pw, ph)
    draw_text(p, 8, 7, "REQ_01.DAT", "W")
    t = canvas(text_width("LATE PACKAGE"), 7); draw_text(t, 0, 0, "LATE PACKAGE", "W")
    p.alpha_composite(upscale(t, 2), (10, 30))
    draw_text(p, 10, 50, "FROM: COURIER GUILD", "3")
    pfill(p, 10, 62, pw - 11, 62, "b")
    for i, line in enumerate(["A COURIER IS STUCK IN A", "LOCKED FLOOR. GET THEM", "OUT WITHOUT A TRACE."]):
        draw_text(p, 10, 70 + i * 11, line, "W")
    draw_text(p, 10, 112, "MISSIONS", "C")
    for i, m in enumerate(["REACH THE EXIT (FINAL)", "PASS POINT A", "PASS POINT B"]):
        draw_text(p, 16, 126 + i * 12, "- " + m, "3")
    draw_text(p, 10, 172, "BEST", "C")
    for s in range(3):
        p.alpha_composite(star(s < 2), (42 + s * 11, 171))
    bw = 110
    btn = button(bw, True)
    bx, by = pw - bw - 34, ph - 36   # 배지가 창 밖으로 잘리지 않게
    p.alpha_composite(btn, (bx, by))
    draw_text(p, bx + (bw - text_width("CONNECT")) // 2, by + 3, "CONNECT", "W")
    b = badge("ENTER"); p.alpha_composite(b, (bx + bw - 5, by + 13 - 5))
    return p


def connect_box(ratio):
    p = window_frame(280, 70)
    draw_text(p, 8, 7, "CONNECTING", "W")
    draw_text(p, 12, 30, "CONNECTING TO CAM 03...", "G")
    p.alpha_composite(gauge(256, ratio), (12, 46))
    return p


def build():
    wall, dk = room_wall(), desk()
    made = []
    layers = {
        "scenes/room_layer0_wall": wall,
        "scenes/room_layer1_desk": dk,
        "scenes/room_layer2_monitor_off": monitors(False),
        "scenes/room_layer2_monitor_on": monitors(True),
        "scenes/room_layer3_light_on": light_spill(),
        "scenes/room_layer4_hacker": hacker_back(False),
        "scenes/room_layer4_hacker_lit": hacker_back(True),
    }
    for k in range(4):
        layers[f"scenes/room_screen_boot_{k}"] = screen_content(k)
    for rel, im in layers.items():
        assert not palette_check(im), rel
        made.append(save(im, rel + ".png", {"status": "시안", "style": "pixel", "role": "메인 방 레이어"}))

    def comp(*ims):
        out = ims[0]
        for i in ims[1:]:
            out = over(out, i)
        return out

    previews = {
        "main_off": comp(wall, dk, monitors(False), hacker_back(False), menu_overlay(0)),
        "main_boot": comp(wall, light_spill(), dk, monitors(True), screen_content(2), hacker_back(True)),
        "desktop": desktop(0, False),
        "desktop_panel": desktop(0, True),
        "desktop_connecting": desktop(0, True, 0.6),
    }
    for name, im in previews.items():
        assert not palette_check(im), name
        made.append(save(im, f"preview/scene_{name}.png", {"status": "목업"}))
        made.append(save(upscale(im, 2), f"preview/scene_{name}_x2.png", {"status": "목업 2배"}))
    made.append(save(wallpaper(), "scenes/desktop_wallpaper.png", {"status": "시안"}))
    for st in ("clear", "new", "locked"):
        made.append(save(file_icon(st), f"ui/icon_file_{st}.png", {"status": "시안"}))
    made.append(save(detail_panel(), "ui/desktop_detail_panel.png", {"status": "시안", "note": "글자 포함 목업용"}))
    return made
