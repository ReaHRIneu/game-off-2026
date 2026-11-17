"""메인 화면·의뢰 선택 화면 아트 시안(640×360) — D-055, Docs/Planning/05-02-screen-flow.md

- ui_mvp 의 부품 함수(창 틀·패널·배지·별 등)를 재사용한다(이름순으로 ui_mvp 다음에 실행)
- 로고 글자는 임시 표기(게임 제목 미정)
- 로고는 5×7 글꼴을 정수 배율로 키우고, 설계도 색 사본을 어긋나게 깔아 SHIFT 표현
[시안] 사용자 확정 전.
"""
from PIL import Image

from artkit import canvas, palette_check, pbox, pfill, save, upscale
from pixfont import draw_text, text_width
from ui_mvp import badge, button, frame, load, star, window_frame

TITLE_TEXT = "SHIFT//REMOTE"   # 임시 표기


def big_text(s, k, ch):
    small = canvas(text_width(s), 7)
    draw_text(small, 0, 0, s, ch)
    return upscale(small, k)


def key_hint(scr, x, y, items):
    """화면 아래 시스템 키 안내(D-051): [배지] 설명 나열."""
    for key, label in items:
        b = badge(key)
        scr.alpha_composite(b, (x, y))
        x += b.width + 3
        draw_text(scr, x, y + 2, label, "3")
        x += text_width(label) + 10


def logo():
    k = 4
    front = big_text(TITLE_TEXT, k, "W")
    shift = big_text(TITLE_TEXT, k, "C")
    shadow = big_text(TITLE_TEXT, k, "B")
    im = canvas(front.width + 8, front.height + 8)
    im.alpha_composite(shadow, (6, 6))
    im.alpha_composite(shift, (4, 0))       # 설계도 색 사본: 오른쪽으로 어긋남
    # 앞 글자 아래쪽 띠만 1칸 밀어서 글리치처럼
    top = front.crop((0, 0, front.width, front.height // 2))
    bottom = front.crop((0, front.height // 2, front.width, front.height))
    im.alpha_composite(top, (0, 0))
    im.alpha_composite(bottom, (k, front.height // 2))
    return im


def main_screen(selected=0):
    scr = load("backgrounds/city_night_composite.png")
    lg = logo()
    scr.alpha_composite(lg, ((640 - lg.width) // 2, 70))
    sub = "REMOTE REALITY HACKING"
    sw = text_width(sub)
    scr.alpha_composite(frame(sw + 16, 15, "a"), ((640 - sw) // 2 - 8, 113))   # 배경 위에서도 읽히게 받침
    draw_text(scr, (640 - sw) // 2, 117, sub, "C")
    items = ["START", "CREDITS"]
    for i, t in enumerate(items):
        bw = 96
        bx, by = (640 - bw) // 2, 186 + i * 22
        scr.alpha_composite(button(bw, i == selected), (bx, by))
        draw_text(scr, bx + (bw - text_width(t)) // 2, by + 3, t, "W")
        if i == selected:
            draw_text(scr, bx - 12, by + 3, ">", "R")
    panel = canvas(640, 22, (10, 10, 24, 255))  # 팔레트 a 와 같은 색
    scr.alpha_composite(panel, (0, 338))
    draw_text(scr, 640 - 8 - text_width("GITHUB GAME OFF 2026"), 345, "GITHUB GAME OFF 2026", "4")
    key_hint(scr, 200, 343, [("↑↓", "SELECT"), ("ENTER", "OK")])
    return scr


REQUESTS = [  # (번호, 의뢰인, 제목, 상태, 별)
    ("01", "COURIER GUILD", "LATE PACKAGE", "CLEAR", 2),
    ("02", "???", "???", "LOCKED", 0),
    ("03", "???", "???", "LOCKED", 0),
    ("04", "???", "???", "LOCKED", 0),
]


def request_card(w, req, selected):
    no, client, title, state, stars = req
    locked = state == "LOCKED"
    im = frame(w, 40, "a" if locked else "k", "C" if selected else "b")
    if selected:
        pfill(im, 2, 2, 3, 37, "R")
    draw_text(im, 8, 6, f"#{no}", "4" if locked else "C")
    draw_text(im, 34, 6, title, "4" if locked else "W")
    draw_text(im, 34, 18, client, "4" if locked else "3")
    tag_col = {"CLEAR": "G", "NEW": "Y", "LOCKED": "4"}[state]
    tw = text_width(state)
    pbox(im, w - tw - 14, 5, w - 8, 15, tag_col)
    draw_text(im, w - tw - 11, 7, state, tag_col)
    for i in range(3):
        im.alpha_composite(star(i < stars), (w - 40 + i * 11, 24))
    return im


def select_screen(sel=0):
    scr = load("backgrounds/city_night_composite.png")
    scr.alpha_composite(window_frame(624, 330), (8, 8))
    draw_text(scr, 16, 15, "SHIFT://REMOTE  -  REQUEST BOARD", "W")
    # 왼쪽 목록
    scr.alpha_composite(frame(300, 290), (14, 32))
    draw_text(scr, 22, 38, "INBOX  1 / 4", "C")
    for i, r in enumerate(REQUESTS):
        scr.alpha_composite(request_card(284, r, i == sel), (22, 50 + i * 46))
    # 오른쪽 상세
    scr.alpha_composite(frame(310, 290), (318, 32))
    no, client, title, state, stars = REQUESTS[sel]
    draw_text(scr, 328, 40, f"REQUEST #{no}", "C")
    scr.alpha_composite(big_text(title, 2, "W"), (328, 54))
    draw_text(scr, 328, 76, f"FROM: {client}", "3")
    pfill(scr, 328, 90, 618, 90, "b")
    for i, line in enumerate(["A COURIER IS STUCK IN A", "LOCKED FLOOR. GET THEM OUT", "WITHOUT LEAVING A TRACE."]):
        draw_text(scr, 328, 98 + i * 11, line, "W")
    draw_text(scr, 328, 140, "MISSIONS", "C")
    for i, m in enumerate(["REACH THE EXIT (FINAL)", "PASS POINT A", "PASS POINT B"]):
        draw_text(scr, 336, 154 + i * 12, "- " + m, "3")
    draw_text(scr, 328, 200, "BEST", "C")
    for i in range(3):
        scr.alpha_composite(star(i < stars), (360 + i * 11, 199))
    bw = 120
    scr.alpha_composite(button(bw, True), (488, 290))
    draw_text(scr, 488 + (bw - text_width("ACCEPT")) // 2, 293, "ACCEPT", "W")
    # 아래 키 안내
    key_hint(scr, 16, 343, [("↑↓", "SELECT"), ("ENTER", "ACCEPT"), ("ESC", "BACK")])
    return scr


def build():
    made = []
    for name, im in [("main", main_screen()), ("request_select", select_screen())]:
        assert not palette_check(im) or True  # 배경 패널 단색은 팔레트 밖일 수 있음 → 아래에서 검사
        bad = palette_check(im)
        assert not bad, (name, bad)
        made.append(save(im, f"preview/screen_{name}.png", {"status": "목업"}))
        made.append(save(upscale(im, 2), f"preview/screen_{name}_x2.png", {"status": "목업 2배"}))
    lg = logo()
    assert not palette_check(lg)
    made.append(save(lg, "ui/logo_temp.png", {"status": "시안", "note": "임시 표기 — 게임 제목 미정"}))
    return made
