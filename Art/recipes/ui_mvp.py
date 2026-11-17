"""UI 아트 시안 — 가상 프로그램 창(D-008) 도트 UI 부품 + 640×360 전체 화면 목업.

- 배치 정본: Docs/Planning/05-01-ui-layout.md (D-042~D-051)
- 부품은 Unity 9-slice(Sprite Border)로 늘려 쓰도록 작게 만들고, 테두리 폭을 manifest 에 적는다
- 시스템 키 표시(D-051): 5×7 글꼴, 높이 9px 배지, 버튼 아이콘 오른쪽 아래
- 이 레시피는 이름순으로 마지막에 실행되어 다른 레시피의 PNG(배경·타일·초상)를 목업에 쓴다
[시안] 사용자 확정 전.
"""
from PIL import Image

from artkit import EXPORT, PIX, canvas, over, palette_check, pbox, pfill, pset, save, upscale
from pixfont import atlas, draw_text, text_width

# ---- 색 역할 [제안] ----
PANEL = "k"       # 패널 바탕
PANEL_IN = "j"    # 패널 안쪽 띠
LINE = "b"        # 패널 테두리(청사진 파랑)
HI = "C"          # 강조 선
TITLE = "B"       # 제목줄


# ---- 부품 ----
def frame(w, h, fill=PANEL, border=LINE, corner=HI):
    """2px 테두리 패널(9-slice 테두리 4px)."""
    im = canvas(w, h, PIX[fill])
    pbox(im, 0, 0, w - 1, h - 1, "K")
    pbox(im, 1, 1, w - 2, h - 2, border)
    for (x, y) in [(1, 1), (w - 2, 1), (1, h - 2), (w - 2, h - 2)]:
        pset(im, x, y, corner)
    return im


def window_frame(w, h):
    im = frame(w, h, "a", LINE)
    pfill(im, 2, 2, w - 3, 19, TITLE)          # 제목줄(20px: 시스템 버튼 + 키 배지가 들어가는 높이)
    pfill(im, 2, 20, w - 3, 20, HI)
    for x in range(4, w - 4, 4):               # 제목줄 스캔 점무늬
        pset(im, x, 3, "b")
    return im


def tab(w, active):
    im = canvas(w, 14, PIX[PANEL if active else "a"])
    pbox(im, 0, 0, w - 1, 13, "K")
    pbox(im, 1, 1, w - 2, 13, HI if active else LINE)
    if active:
        pfill(im, 2, 13, w - 3, 13, PIX and PANEL)  # 아래 열림(패널과 이어짐)
    return im


def badge(label):
    """시스템 키 배지(D-051): 높이 11px = 테두리 1 + 여백 1 + 글자 7 + 여백 1 + 테두리 1.
    9px 시안은 글자가 테두리에 붙어 읽히지 않아 11px로 올렸다. 어두운 바탕 + 흰 글자로 대비 확보."""
    w = text_width(label) + 6
    im = canvas(w, 11, PIX["K"])
    pbox(im, 0, 0, w - 1, 10, "3")
    draw_text(im, 3, 2, label, "W")
    return im


def icon_pause():
    im = canvas(11, 11)
    pbox(im, 0, 0, 10, 10, "K"); pfill(im, 1, 1, 9, 9, "2")
    pfill(im, 3, 3, 4, 7, "W"); pfill(im, 6, 3, 7, 7, "W")
    return im


def icon_tabswitch():
    """⇄ 모양: 위 화살표는 오른쪽, 아래 화살표는 왼쪽."""
    im = canvas(11, 11)
    pbox(im, 0, 0, 10, 10, "K"); pfill(im, 1, 1, 9, 9, "2")
    pfill(im, 2, 3, 8, 3, "W"); pset(im, 7, 2, "W"); pset(im, 7, 4, "W")
    pfill(im, 2, 7, 8, 7, "W"); pset(im, 3, 6, "W"); pset(im, 3, 8, "W")
    return im


def with_badge(icon, label):
    """아이콘 오른쪽 아래에 키 배지를 겹친다(D-051)."""
    b = badge(label)
    w = max(icon.width + b.width - 5, icon.width)
    h = icon.height + b.height - 5
    im = canvas(w, h)
    im.alpha_composite(icon, (0, 0))
    im.alpha_composite(b, (icon.width - 5, icon.height - 5))
    return im


def checkbox(done):
    im = canvas(9, 9)
    pbox(im, 0, 0, 8, 8, "K"); pbox(im, 1, 1, 7, 7, LINE); pfill(im, 2, 2, 6, 6, "a")
    if done:
        for (x, y) in [(2, 4), (3, 5), (4, 6), (5, 5), (6, 4), (7, 3), (7, 2)]:
            pset(im, x, y, "G")
    return im


STAR = ["....#....", "....#....", "...###...", "#########", ".#######.", "..#####..", "..##.##..", ".##...##.", ".#.....#."]


def star(full):
    im = canvas(9, 9)
    for y, row in enumerate(STAR):
        for x, v in enumerate(row):
            if v == "#":
                pset(im, x, y, "Y" if full else "4")
    return im


def gauge(w, ratio):
    im = canvas(w, 9)
    pbox(im, 0, 0, w - 1, 8, "K"); pbox(im, 1, 1, w - 2, 7, LINE); pfill(im, 2, 2, w - 3, 6, "a")
    fill = int((w - 4) * ratio)
    for x in range(2, 2 + fill):
        if (x - 2) % 4 != 3:                    # 칸 나뉜 게이지
            pfill(im, x, 2, x, 6, "R")
    pfill(im, 2, 2, 1 + fill, 2, "W") if fill > 2 else None
    return im


def bubble(w, h, side):
    """문자 메시지 말풍선(D-046). side: left(상대) / right(해커)."""
    im = canvas(w + 4, h)
    body_x = 4 if side == "left" else 0
    col = "2" if side == "left" else "B"
    edge = LINE if side == "left" else HI
    pfill(im, body_x, 0, body_x + w - 1, h - 1, col)
    pbox(im, body_x, 0, body_x + w - 1, h - 1, "K")
    pbox(im, body_x + 1, 1, body_x + w - 2, h - 2, edge)
    ty = h - 6                                   # 꼬리
    for i in range(4):
        x = 3 - i if side == "left" else w + i
        pfill(im, x, ty + i, x, ty + 3, "K")
        if i < 3:
            pfill(im, x + (1 if side == "left" else -1), ty + i + 1, x + (1 if side == "left" else -1), ty + 3, col)
    return im


def button(w, selected):
    im = canvas(w, 13, PIX["R" if selected else "2"])
    pbox(im, 0, 0, w - 1, 12, "K")
    pfill(im, 1, 1, w - 2, 1, "W" if selected else "3")
    pfill(im, 1, 11, w - 2, 11, "r" if selected else "4")
    return im


# ---- 640×360 목업 ----
def load(rel):
    return Image.open(EXPORT / rel).convert("RGBA")


LEVEL = ["############", "#S.>....D.E#", "###.######.#", "#..>A.D.BR^#", "############"]
TILE = {"#": "reality_wall", ".": "reality_floor", "S": "reality_floor", "D": "reality_door_closed",
        "E": "reality_exit", "A": "reality_checkpoint", "B": "reality_checkpoint", "R": "reality_relay",
        ">": "reality_arrow_right", "^": "reality_arrow_up"}


def mockup(mode):
    scr = load("backgrounds/city_night_composite.png")
    win = window_frame(624, 344)
    scr.alpha_composite(win, (8, 8))
    draw_text(scr, 16, 15, "SHIFT://REMOTE  -  REQUEST 01", "W")
    scr.alpha_composite(with_badge(icon_pause(), "ESC"), (596, 11))

    # 현실 장면 패널
    scr.alpha_composite(frame(416, 278), (14, 32))
    draw_text(scr, 22, 38, "FLOOR VIEW / CAM 03", "C")
    ox, oy = 30, 96
    for y, row in enumerate(LEVEL):
        for x, c in enumerate(row):
            scr.alpha_composite(load(f"tiles/{TILE[c]}.png"), (ox + x * 32, oy + y * 32))
    scr.alpha_composite(load("tiles/blueprint_door_open.png"), (ox + 8 * 32, oy + 32))   # Shift 중 설계도 겹침
    scr.alpha_composite(load("sprites/courier_right.png"), (ox + 6 * 32, oy + 32))
    scr.alpha_composite(load("sprites/cursor.png"), (ox + 8 * 32, oy + 32))

    # 오른쪽 위: 미션·대화 공용 패널(D-043) + 탭(D-050)
    px0, py0, pw = 436, 32, 190
    scr.alpha_composite(tab(70, mode == "mission"), (px0, py0))
    draw_text(scr, px0 + 8, py0 + 4, "MISSION", "W" if mode == "mission" else "4")
    scr.alpha_composite(tab(46, mode == "log"), (px0 + 72, py0))
    draw_text(scr, px0 + 81, py0 + 4, "LOG", "W" if mode == "log" else "4")
    scr.alpha_composite(frame(pw, 132), (px0, py0 + 13))
    scr.alpha_composite(with_badge(icon_tabswitch(), "]"), (px0 + pw - 26, py0 + 1))
    if mode == "mission":
        items = [("REACH THE EXIT", True), ("PASS POINT A", True), ("PASS POINT B", False)]
        for i, (t, done) in enumerate(items):
            y = py0 + 24 + i * 16
            scr.alpha_composite(checkbox(done), (px0 + 8, y))
            draw_text(scr, px0 + 22, y + 1, t, "W" if not done else "3")
        draw_text(scr, px0 + 8, py0 + 82, "RATING", "C")
        for i, f in enumerate([True, True, False]):
            scr.alpha_composite(star(f), (px0 + 52 + i * 12, py0 + 81))
    else:
        draw_text(scr, px0 + 8, py0 + 20, "CLIENT  21:04", "3")
        scr.alpha_composite(bubble(150, 20, "left"), (px0 + 6, py0 + 29))
        draw_text(scr, px0 + 16, py0 + 36, "PACKAGE IS LATE.", "W")
        draw_text(scr, px0 + pw - 70, py0 + 56, "ME  21:05", "C")
        scr.alpha_composite(bubble(90, 20, "right"), (px0 + pw - 100, py0 + 65))
        draw_text(scr, px0 + pw - 94, py0 + 72, "ON IT.", "W")
        draw_text(scr, px0 + 8, py0 + 92, "CLIENT  21:05", "3")
        scr.alpha_composite(bubble(150, 20, "left"), (px0 + 6, py0 + 101))
        draw_text(scr, px0 + 16, py0 + 108, "NO TRACE. PLEASE.", "W")
        scr.alpha_composite(with_badge(button(44, True), "ENTER"), (px0 + pw - 78, py0 + 124))
        draw_text(scr, px0 + pw - 72, py0 + 127, "NEXT", "W")

    # 오른쪽 아래: 캐릭터 얼굴 패널(D-042, D-049)
    scr.alpha_composite(frame(pw, 128), (px0, 182))
    scr.alpha_composite(frame(102, 102, "a"), (px0 + 6, 194))
    face = "neutral" if mode == "mission" else "anger"
    scr.alpha_composite(load(f"portraits/hacker_{face}.png"), (px0 + 9, 197))
    draw_text(scr, px0 + 114, 196, "HACKER", "C")
    draw_text(scr, px0 + 114, 206, "???", "3")

    # 아래 줄: 추적도·명령 기록
    scr.alpha_composite(frame(200, 30), (14, 314))
    draw_text(scr, 22, 320, "TRACE", "C")
    scr.alpha_composite(gauge(140, 0.62), (66, 319))
    scr.alpha_composite(frame(406, 30), (220, 314))
    draw_text(scr, 228, 325, "> LSHIFT   > RIGHT   > DOWN", "G")
    return scr


def build():
    parts = {
        "ui/window_frame": (window_frame(32, 32), {"border": [4, 22, 4, 4]}),
        "ui/panel": (frame(16, 16), {"border": [4, 4, 4, 4]}),
        "ui/tab_active": (tab(16, True), {"border": [4, 4, 4, 2]}),
        "ui/tab_inactive": (tab(16, False), {"border": [4, 4, 4, 2]}),
        "ui/bubble_left": (bubble(24, 16, "left"), {"border": [8, 4, 4, 4]}),
        "ui/bubble_right": (bubble(24, 16, "right"), {"border": [4, 4, 8, 4]}),
        "ui/button": (button(16, False), {"border": [4, 4, 4, 4]}),
        "ui/button_selected": (button(16, True), {"border": [4, 4, 4, 4]}),
        "ui/gauge_frame_fill_62": (gauge(48, 0.62), {"border": [3, 3, 3, 3]}),
        "ui/checkbox_empty": (checkbox(False), {}),
        "ui/checkbox_done": (checkbox(True), {}),
        "ui/star_full": (star(True), {}),
        "ui/star_empty": (star(False), {}),
        "ui/icon_pause": (icon_pause(), {}),
        "ui/icon_tabswitch": (icon_tabswitch(), {}),
        "ui/badge_esc": (badge("ESC"), {}),
        "ui/badge_bracket": (badge("]"), {}),
        "ui/badge_enter": (badge("ENTER"), {}),
    }
    made = []
    for rel, (im, meta) in parts.items():
        assert not palette_check(im), rel
        made.append(save(im, rel + ".png", {"status": "시안", "style": "pixel", "nine_slice_LTRB": meta.get("border")}))
    fa, fmeta = atlas("W")
    made.append(save(fa, "ui/font5x7_atlas.png", {"status": "시안", "glyph": [5, 7], "glyphs": fmeta}))
    for mode in ("mission", "log"):
        m = mockup(mode)
        assert not palette_check(m), mode
        made.append(save(m, f"preview/ui_mockup_{mode}.png", {"status": "목업"}))
        made.append(save(upscale(m, 2), f"preview/ui_mockup_{mode}_x2.png", {"status": "목업 2배"}))
    return made
