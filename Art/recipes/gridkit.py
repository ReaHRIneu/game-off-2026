"""문자 격자 로더: 한 글자 = 한 픽셀(팔레트 문자). 줄 앞 `N:`은 왼쪽으로 N칸 비우는 위치 이동, `|`와 공백은 읽기 쉽게 넣은 눈금(무시).
`#`로 시작하는 줄은 주석. 오른쪽 끝은 투명으로 채운다. 도형 함수·자동 외곽선·자동 명암은 쓰지 않는다."""
from pathlib import Path
from artkit import PIX, canvas

GRIDS = Path(__file__).resolve().parent / "grids"


def load_rows(name):
    rows = []
    for ln in (GRIDS / name).read_text(encoding="utf-8").splitlines():
        if not ln.strip() or ln.lstrip().startswith("#"):
            continue
        off = 0
        if ":" in ln.split("|")[0] and ln.split(":")[0].strip().isdigit():
            off = int(ln.split(":")[0]); ln = ln.split(":", 1)[1]
        body = ln.replace("|", "").replace(" ", "").rstrip()
        rows.append("." * off + body)
    return rows


def grid_image(rows, width=None):
    w = width or max(len(r) for r in rows)
    im = canvas(w, len(rows))
    px = im.load()
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if x < w and ch != ".":
                px[x, y] = PIX[ch]
    return im


def paste(dst, grid, ox, oy, flip=False):
    """완성된 격자를 위치만 옮겨 붙인다(투명 칸은 건너뜀)."""
    g = grid.transpose(0) if flip else grid
    dst.alpha_composite(g, (ox, oy))
