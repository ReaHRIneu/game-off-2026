"""MVP-1 칸 타일 시안 — 현실 층(불투명) + 설계도 층(반투명 겹침용).

[시안] 스타일은 Docs/Art/art-pipeline.md 의 제안값이며 사용자 확정 전이다.
"""
from artkit import PALETTE as P, EXPORT, arrow, canvas, contact_sheet, glow, grid, line, rect, save

T = 64  # 타일 크기(px) [제안], PPU 64 [제안]
DIRS = ["up", "down", "left", "right"]


def reality(kind: str, direction: str | None = None):
    im = canvas(T, T, P["reality_bg"])
    rect(im, (0, 0, T - 1, T - 1), outline=(32, 36, 46, 255))
    if kind == "wall":
        rect(im, (2, 2, T - 3, T - 3), fill=P["reality_wall"], outline=P["reality_edge"], width=2)
    elif kind in ("door_closed", "door_open"):
        rect(im, (2, 2, T - 3, T - 3), fill=P["reality_wall"], outline=P["reality_edge"], width=2)
        if kind == "door_closed":
            rect(im, (12, 10, T - 13, T - 11), fill=P["door"])
            line(im, [(T // 2, 10), (T // 2, T - 11)], (110, 76, 36, 255), 2)
        else:
            rect(im, (12, 10, T - 13, T - 11), fill=P["reality_bg"], outline=P["door"], width=2)
    elif kind == "arrow":
        arrow(im, T // 2, T // 2, 30, direction, P["reality_edge"], 4)
    elif kind == "exit":
        rect(im, (10, 10, T - 11, T - 11), outline=P["ok"], width=3)
        arrow(im, T // 2, T // 2, 22, "up", P["ok"], 3)
    elif kind == "checkpoint":
        rect(im, (20, 20, T - 21, T - 21), outline=(230, 220, 120, 255), width=3)
    elif kind == "relay":
        rect(im, (24, 14, T - 25, T - 15), fill=P["reality_edge"])
        line(im, [(T // 2, 14), (T // 2, 6)], P["ok"], 2)
    return im


def blueprint(kind: str, direction: str | None = None):
    """설계도 층: 청사진 바탕 + 격자 + 밝은 선. 겹쳐 쓰도록 반투명."""
    im = canvas(T, T, P["bp_bg"][:3] + (170,))
    grid(im, 16, P["bp_grid"][:3] + (140,))
    lc = P["bp_line"]
    rect(im, (1, 1, T - 2, T - 2), outline=lc, width=2)
    if kind == "wall":
        line(im, [(4, 4), (T - 5, T - 5)], lc, 2)
        line(im, [(T - 5, 4), (4, T - 5)], lc, 2)
    elif kind == "door_open":
        rect(im, (12, 10, T - 13, T - 11), outline=lc, width=2)
        line(im, [(12, 10), (T - 13, 18)], lc, 1)  # 열린 문 도면 표기
    elif kind == "door_closed":
        rect(im, (12, 10, T - 13, T - 11), outline=lc, width=2)
        line(im, [(T // 2, 10), (T // 2, T - 11)], lc, 2)
    elif kind == "arrow":
        arrow(im, T // 2, T // 2, 30, direction, lc, 3)
    return glow(im, 1)


def build():
    made = []
    for k in ["floor", "wall", "door_closed", "door_open", "exit", "checkpoint", "relay"]:
        made.append(save(reality(k), f"tiles/reality_{k}.png", {"layer": "reality", "status": "시안"}))
    for d in DIRS:
        made.append(save(reality("arrow", d), f"tiles/reality_arrow_{d}.png", {"layer": "reality", "status": "시안"}))
    for k in ["floor", "wall", "door_closed", "door_open"]:
        made.append(save(blueprint(k), f"tiles/blueprint_{k}.png", {"layer": "blueprint", "status": "시안"}))
    for d in DIRS:
        made.append(save(blueprint("arrow", d), f"tiles/blueprint_arrow_{d}.png", {"layer": "blueprint", "status": "시안"}))
    made.append(contact_sheet(made, 64, "preview/tiles_mvp1_sheet.png", cols=8))
    return made
