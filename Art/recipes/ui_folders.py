"""바탕화면 챕터 폴더 아이콘 시안(32×32) — 02-03 §5-3 [제안]"""
from artkit import canvas, draw_poly, outline_outside, palette_check, pbox, pfill, pset, save


def folder(state):
    """state: open(열린 챕터) / locked(잠긴 챕터)"""
    im = canvas(32, 32)
    body = "Y" if state == "open" else "3"
    dark = "y" if state == "open" else "4"
    draw_poly(im, [(3, 7), (12, 7), (15, 10), (28, 10), (28, 27), (3, 27)], dark)   # 뒤판·탭
    draw_poly(im, [(3, 12), (28, 12), (28, 27), (3, 27)], body)                     # 앞판
    pfill(im, 4, 13, 27, 13, "h" if state == "open" else "2")
    outline_outside(im, "K")
    if state == "locked":
        pbox(im, 12, 15, 18, 19, "K"); pfill(im, 10, 19, 20, 26, "2"); pbox(im, 10, 19, 20, 26, "K"); pset(im, 15, 22, "W")
    return im


def build():
    made = []
    for st in ("open", "locked"):
        im = folder(st)
        assert not palette_check(im)
        made.append(save(im, f"ui/icon_folder_{st}.png", {"status": "시안", "style": "pixel"}))
    return made
