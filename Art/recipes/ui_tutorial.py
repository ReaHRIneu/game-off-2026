"""W-033 조작 튜토리얼 UI 그림(D-123 ②): 키 배지 6종(SHIFT·←↑→↓·ENTER), 안내 말풍선 틀(9-slice) + 꼬리 4방향.
키 배지는 ui/badge_enter·badge_esc와 같은 높이(11)·5x7 글꼴, 시안 테두리. 네온 테두리는 코드(GameRenderer)가 그린다.
실행: python -I tools/art/build_art.py ui_tutorial
"""
from artkit import PIX, canvas, palette_check, pbox, pfill, pset, save
from pixfont import draw_text, text_width


def _arrow(im, x, y, d):
    """5x7 칸 안에 화살표(←→는 글꼴에 없어 직접 그림, ↑↓는 PixelCanvas와 같은 모양)."""
    if d in "←→":
        pfill(im, x, y + 3, x + 4, y + 3, "W")
        a = x + 3 if d == "→" else x + 1
        pset(im, a, y + 2, "W"); pset(im, a, y + 4, "W")
        b = x + 2
        pset(im, b, y + 1, "W"); pset(im, b, y + 5, "W")
    else:
        pfill(im, x + 2, y + 1, x + 2, y + 5, "W")
        n = y + 2 if d == "↑" else y + 4
        pset(im, x + 1, n, "W"); pset(im, x + 3, n, "W")
        pset(im, x, y + 3, "W"); pset(im, x + 4, y + 3, "W")


def key(label):
    w = (text_width(label) if len(label) > 1 else 5) + 6
    im = canvas(w, 11, PIX["K"])
    pbox(im, 0, 0, w - 1, 10, "C")
    if len(label) > 1:
        draw_text(im, 3, 2, label, "W")
    else:
        _arrow(im, 3, 2, label)
    return im


def bubble():
    """16x16 9-slice(테두리 4): 바깥 K, 안쪽 시안 C, 바탕 a."""
    im = canvas(16, 16, PIX["a"])
    pbox(im, 0, 0, 15, 15, "K"); pbox(im, 1, 1, 14, 14, "C")
    pbox(im, 2, 2, 13, 13, "K")
    return im


def tail(d):
    """꼬리(9x6 또는 6x9). 말풍선 변에 붙는 쪽 줄은 바탕색으로 비워 테두리와 이어진다."""
    if d in ("up", "down"):
        im = canvas(9, 6)
        for i in range(5):
            x0, x1 = 4 - i, 4 + i
            y = i if d == "up" else 5 - i
            pfill(im, x0, y, x1, y, "a")
            pset(im, x0, y, "C"); pset(im, x1, y, "C")
        for x in range(9):
            pass
        y = 5 if d == "up" else 0
        pfill(im, 0, y, 8, y, "a")
        pset(im, 0, y, "C"); pset(im, 8, y, "C")
        return im
    im = canvas(6, 9)
    for i in range(5):
        y0, y1 = 4 - i, 4 + i
        x = i if d == "left" else 5 - i
        pfill(im, x, y0, x, y1, "a")
        pset(im, x, y0, "C"); pset(im, x, y1, "C")
    x = 5 if d == "left" else 0
    pfill(im, x, 0, x, 8, "a")
    pset(im, x, 0, "C"); pset(im, x, 8, "C")
    return im


def build():
    pieces = {f"ui/tut_key_{n}": key(l) for n, l in
              (("shift", "SHIFT"), ("left", "←"), ("up", "↑"), ("right", "→"), ("down", "↓"), ("enter", "ENTER"))}
    pieces["ui/tut_bubble"] = bubble()
    for d in ("up", "down", "left", "right"):
        pieces[f"ui/tut_tail_{d}"] = tail(d)
    made = []
    for rel, im in pieces.items():
        assert not palette_check(im), rel
        meta = {"status": "시안", "style": "pixel"}
        if rel == "ui/tut_bubble":
            meta["nine_slice_LTRB"] = [4, 4, 4, 4]
        made.append(save(im, rel + ".png", meta))
    return made
