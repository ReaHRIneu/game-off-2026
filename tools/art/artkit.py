"""코드 기반 아트 제작 도구 (Pillow).

이 게임의 그림(타일·UI·아이콘)은 AI 이미지 생성이 아니라 코드로 그린다.
레시피(Art/recipes/*.py)가 이 모듈의 함수로 그림을 만들고 PNG로 저장한다.
저장소 기준 상대 경로만 사용한다(공개 저장소 — 로컬 절대 경로 금지).
"""
from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

REPO = Path(__file__).resolve().parents[2]
EXPORT = REPO / "Art" / "Export"

# 팔레트 [제안] — Docs/Art/art-pipeline.md 와 같은 값. 바꿀 때는 두 곳을 함께 고친다.
PALETTE = {
    "reality_bg": (24, 28, 36, 255),      # 현실 층 바닥(어두운 회청)
    "reality_wall": (58, 64, 78, 255),    # 현실 층 벽
    "reality_edge": (96, 104, 122, 255),  # 현실 층 윤곽
    "door": (170, 120, 60, 255),          # 문
    "bp_bg": (14, 52, 110, 255),          # 설계도 층 바탕(청사진)
    "bp_line": (150, 210, 255, 255),      # 설계도 선
    "bp_grid": (60, 120, 190, 255),       # 설계도 격자
    "accent": (255, 80, 110, 255),        # 경고·추적도
    "ok": (90, 230, 160, 255),            # 완료·통과
}


def canvas(w: int, h: int, color=(0, 0, 0, 0)) -> Image.Image:
    """투명(기본) RGBA 캔버스."""
    return Image.new("RGBA", (w, h), color)


def grid(img: Image.Image, step: int, color, width: int = 1) -> None:
    """청사진 격자선."""
    d = ImageDraw.Draw(img)
    w, h = img.size
    for x in range(0, w, step):
        d.line([(x, 0), (x, h - 1)], fill=color, width=width)
    for y in range(0, h, step):
        d.line([(0, y), (w - 1, y)], fill=color, width=width)


def rect(img: Image.Image, box, outline=None, fill=None, width: int = 1) -> None:
    ImageDraw.Draw(img).rectangle(box, outline=outline, fill=fill, width=width)


def line(img: Image.Image, pts, color, width: int = 1) -> None:
    ImageDraw.Draw(img).line(pts, fill=color, width=width)


def arrow(img: Image.Image, cx: int, cy: int, size: int, direction: str, color, width: int = 3) -> None:
    """방향 칸 화살표. direction: up/down/left/right."""
    s = size // 2
    pts = {
        "right": [(cx - s, cy), (cx + s, cy)],
        "left": [(cx + s, cy), (cx - s, cy)],
        "up": [(cx, cy + s), (cx, cy - s)],
        "down": [(cx, cy - s), (cx, cy + s)],
    }[direction]
    d = ImageDraw.Draw(img)
    d.line(pts, fill=color, width=width)
    (x0, y0), (x1, y1) = pts
    hx, hy = x1, y1
    k = s // 2
    head = {
        "right": [(hx, hy), (hx - k, hy - k), (hx - k, hy + k)],
        "left": [(hx, hy), (hx + k, hy - k), (hx + k, hy + k)],
        "up": [(hx, hy), (hx - k, hy + k), (hx + k, hy + k)],
        "down": [(hx, hy), (hx - k, hy - k), (hx + k, hy - k)],
    }[direction]
    d.polygon(head, fill=color)


def glow(img: Image.Image, radius: int = 2) -> Image.Image:
    """선 주변에 은은한 빛 번짐(원본 위에 흐린 사본을 깐다)."""
    blur = img.filter(ImageFilter.GaussianBlur(radius))
    out = canvas(*img.size)
    out.alpha_composite(blur)
    out.alpha_composite(img)
    return out


def save(img: Image.Image, rel: str, meta: dict | None = None) -> Path:
    """Art/Export/<rel> 로 저장하고 manifest 에 기록."""
    path = EXPORT / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path)
    manifest = EXPORT / "manifest.json"
    data = json.loads(manifest.read_text(encoding="utf-8")) if manifest.exists() else {}
    data[rel] = {"size": list(img.size), **(meta or {})}
    manifest.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    return path


def contact_sheet(paths: list[Path], cell: int, out_rel: str, cols: int = 8, bg=(40, 40, 40, 255)) -> Path:
    """검수용 미리보기 시트(체커 대신 회색 바탕)."""
    rows = (len(paths) + cols - 1) // cols
    sheet = canvas(cols * (cell + 8) + 8, rows * (cell + 8) + 8, bg)
    for i, p in enumerate(paths):
        im = Image.open(p).convert("RGBA").resize((cell, cell), Image.NEAREST)
        x = 8 + (i % cols) * (cell + 8)
        y = 8 + (i // cols) * (cell + 8)
        sheet.alpha_composite(im, (x, y))
    path = EXPORT / out_rel
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(path)
    return path


# ---------------------------------------------------------------------------
# 도트(픽셀아트) 도구 — D-035. 안티앨리어싱·블러 없이 픽셀 단위로만 그린다.
# ---------------------------------------------------------------------------

# 도트 공유 팔레트 [제안] — 문자 하나 = 색 하나. '.' 은 투명.
PIX = {
    ".": (0, 0, 0, 0),
    "K": (18, 20, 38, 255),     # 외곽선(짙은 남색)
    "0": (30, 34, 48, 255),     # 현실 바닥 어두움
    "1": (42, 48, 64, 255),     # 현실 바닥 밝음
    "2": (68, 76, 98, 255),     # 벽 중간
    "3": (104, 114, 140, 255),  # 벽 밝음·금속
    "4": (50, 56, 74, 255),     # 벽 어두움
    "D": (150, 96, 52, 255),    # 문
    "d": (100, 62, 34, 255),    # 문 어두움
    "h": (232, 200, 110, 255),  # 손잡이·하이라이트
    "G": (84, 224, 152, 255),   # 완료·통과(민트)
    "g": (36, 128, 92, 255),    # 민트 어두움
    "Y": (240, 210, 90, 255),   # 체크 지점
    "y": (150, 120, 40, 255),   # 체크 지점 어두움
    "R": (240, 72, 102, 255),   # 경고·추적도·조준
    "r": (140, 36, 60, 255),    # 경고 어두움
    "B": (18, 54, 118, 255),    # 청사진 바탕
    "b": (40, 96, 176, 255),    # 청사진 격자
    "C": (140, 210, 255, 255),  # 청사진 선
    "W": (236, 246, 255, 255),  # 흰 하이라이트
    "T": (40, 150, 160, 255),   # 인물 옷(청록)
    "t": (24, 92, 104, 255),    # 인물 옷 어두움
    "S": (226, 176, 140, 255),  # 피부
    # 배경(도시 야경) 전용 [제안]
    "a": (10, 10, 24, 255),     # 하늘 위
    "e": (22, 16, 44, 255),     # 하늘 중간
    "f": (40, 22, 64, 255),     # 하늘 아래
    "i": (88, 34, 82, 255),     # 지평선 스모그 빛
    "j": (30, 26, 56, 255),     # 먼 건물
    "k": (20, 20, 40, 255),     # 중간 건물
    "w": (160, 124, 64, 255),   # 창문 불빛(따뜻)
    "c": (56, 150, 170, 255),   # 창문 불빛(차가움)
}


def pmap(rows: list[str], palette: dict = PIX) -> Image.Image:
    """문자 격자 → RGBA 이미지. 모든 행 길이가 같아야 한다."""
    h, w = len(rows), len(rows[0])
    assert all(len(r) == w for r in rows), "행 길이가 다릅니다"
    im = canvas(w, h)
    px = im.load()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            px[x, y] = palette[ch]
    return im


def pset(im: Image.Image, x: int, y: int, ch: str) -> None:
    im.putpixel((x, y), PIX[ch])


def pfill(im: Image.Image, x0: int, y0: int, x1: int, y1: int, ch: str) -> None:
    """양 끝 포함 사각 채우기."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            im.putpixel((x, y), PIX[ch])


def pbox(im: Image.Image, x0: int, y0: int, x1: int, y1: int, ch: str) -> None:
    """양 끝 포함 1px 테두리."""
    for x in range(x0, x1 + 1):
        im.putpixel((x, y0), PIX[ch]); im.putpixel((x, y1), PIX[ch])
    for y in range(y0, y1 + 1):
        im.putpixel((x0, y), PIX[ch]); im.putpixel((x1, y), PIX[ch])


def recolor(im: Image.Image, mapping: dict[str, str]) -> Image.Image:
    """팔레트 문자 기준 색 교체(예: {'3': 'C'})."""
    src = {PIX[k]: PIX[v] for k, v in mapping.items()}
    out = im.copy()
    px = out.load()
    w, h = out.size
    for y in range(h):
        for x in range(w):
            if px[x, y] in src:
                px[x, y] = src[px[x, y]]
    return out


def over(base: Image.Image, top: Image.Image) -> Image.Image:
    out = base.copy()
    out.alpha_composite(top)
    return out


def upscale(im: Image.Image, k: int) -> Image.Image:
    """정수 배율 Nearest 확대(미리보기용)."""
    return im.resize((im.width * k, im.height * k), Image.NEAREST)


def palette_check(im: Image.Image) -> set:
    """공유 팔레트 밖 색이 있으면 반환(도트 규칙 검사)."""
    allowed = set(PIX.values())
    return {c for c in im.getdata() if c not in allowed}
