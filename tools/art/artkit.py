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
