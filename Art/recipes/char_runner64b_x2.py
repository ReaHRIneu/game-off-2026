"""러너 64px 시트 v2 — PixelLab 생성물 가공(W-044, D-131, art-pipeline §4-3).

- 원본: Art/Source/pixellab/2026-11-27/runner_w044 (88x88, 오른쪽을 봄). 자르기·뒤집기·픽셀 지우기만 한다(확대·축소·필터 없음).
- 자르기: 애니메이션마다 한 자리. 0번 프레임 불투명 영역의 맨 아랫줄이 y=62, 가로 중심이 x=31(몸의 오르내림을 살림).
- 떨어짐: 원본 8장을 모두 쓴다(10틱). 구멍 타원(몸 외곽선과 같은 검은색)과 몸에서 떨어진 파편(흰 점 등)은 지운다(투명).
  몸 외곽선은 남긴다 — 구멍(몸에 맞닿지 않은 검은 픽셀)을 지운 뒤 8방향 연결 요소 중 가장 큰 덩어리(몸)만 남긴다.
- 색은 그림 자체 팔레트, 알파 0/255.
[시안] 사용자 확정 전.
"""
from collections import deque
from pathlib import Path

from PIL import Image

from anim import sheet

REPO = Path(__file__).resolve().parents[2]
SRC = REPO / "Art" / "Source" / "pixellab" / "2026-11-27" / "runner_w044" / "Idle" / "animations"
SIZE = 64
BASE_Y, CENTER_X = 62, 31
STATUS = "본그림(PixelLab 가공)"
HOLE = (0, 0, 9)
COLORS = {}
CLIPPED = {}     # 보고용: 잘린 불투명 픽셀 수
# 떨어짐 원본의 흰 파편(충격 고리·반짝임): 프레임 번호 -> 지울 상자(x0, y0, x1, y1, 원본 좌표). 몸과 맞닿아 있어 연결 요소로는 못 가른다.
FRAG_BOXES = {
    4: [(24, 46, 30, 52)],
    5: [(22, 45, 30, 53), (57, 22, 61, 26), (62, 20, 66, 26)],
    6: [(56, 14, 64, 25), (63, 55, 70, 63), (27, 50, 38, 61)],
    7: [(56, 13, 64, 24), (64, 61, 70, 71), (21, 60, 27, 69)],
}
ERASED = {}      # 보고용: 지운 픽셀 수(구멍, 파편)


def _load(phase, n):
    return [Image.open(SRC / phase / "east" / f"frame_{i:03d}.png").convert("RGBA") for i in range(n)]


def _origin(f0):
    """0번 프레임으로 자르기 왼쪽 위(원본 좌표)를 정한다."""
    x0, y0, x1, y1 = f0.getchannel("A").getbbox()
    return round((x0 + x1 - 1) / 2) - CENTER_X, (y1 - 1) - BASE_Y


def _crop(f, ox, oy):
    big = Image.new("RGBA", (f.width + 128, f.height + 128), (0, 0, 0, 0))
    big.alpha_composite(f, (64, 64))
    return big.crop((64 + ox, 64 + oy, 64 + ox + SIZE, 64 + oy + SIZE))


def _lost(frames_src, ox, oy, name):
    for i, f in enumerate(frames_src):
        total = sum(1 for a in f.getchannel("A").getdata() if a)
        kept = sum(1 for a in _crop(f, ox, oy).getchannel("A").getdata() if a)
        if kept != total:
            CLIPPED[f"{name}[{i}]"] = total - kept


def _clean(f, name, hole_rows=None, boxes=()):
    """구멍 타원과 파편을 지운다. 몸 외곽선과 구멍이 같은 검은색((0,0,9))이라 색으로는 구분되지 않는다.
    - hole_rows(y 시작)가 있으면 그 줄부터 몸 색 픽셀과 맞닿지 않은 검은 픽셀(구멍)을 지운다 → 몸 바깥 1px 외곽선만 남는다.
    - 그 뒤 8방향 연결 요소 중 가장 큰 덩어리(몸)만 남긴다(떨어진 흰 파편·바닥 그림자 조각 제거)."""
    f = f.copy()
    w, h = f.size
    hole = 0
    for x0, y0, x1, y1 in boxes:
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                f.putpixel((x, y), (0, 0, 0, 0))

    def dark(p):
        return p[3] and p[:3] == HOLE

    if hole_rows is not None:
        body = {(x, y) for y in range(h) for x in range(w) if f.getpixel((x, y))[3] and not dark(f.getpixel((x, y)))}
        for y in range(hole_rows, h):
            for x in range(w):
                if dark(f.getpixel((x, y))) and not any((x + dx, y + dy) in body for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                    f.putpixel((x, y), (0, 0, 0, 0))
                    hole += 1
    seen = set()
    comps = []
    for y in range(h):
        for x in range(w):
            if (x, y) in seen or not f.getpixel((x, y))[3]:
                continue
            comp, q = [], deque([(x, y)])
            seen.add((x, y))
            while q:
                cx, cy = q.popleft()
                comp.append((cx, cy))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = cx + dx, cy + dy
                        if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in seen and f.getpixel((nx, ny))[3]:
                            seen.add((nx, ny))
                            q.append((nx, ny))
            comps.append(comp)
    comps.sort(key=len, reverse=True)
    frag = 0
    for comp in comps[1:]:
        for x, y in comp:
            f.putpixel((x, y), (0, 0, 0, 0))
        frag += len(comp)
    ERASED[name] = (hole, frag, len(comps) - 1)
    return f


def _check(frames, name):
    for f in frames:
        assert f.size == (SIZE, SIZE), name
        assert all(a in (0, 255) for a in f.getchannel("A").getdata()), name
    colors = set()
    for f in frames:
        colors |= {p[:3] for p in f.getdata() if p[3]}
    COLORS[name] = len(colors)      # 그림 팔레트 크기 보고(프레임당 수십 색)


def build():
    made = []
    spec = {"walk": ([2] * 8, True), "wait": ([3] * 8, True), "fall": ([1, 1, 1, 1, 1, 1, 2, 2], False)}
    for phase, (holds, loop) in spec.items():
        src = _load(phase, 8)
        ox, oy = _origin(src[0])
        if phase == "fall":
            src = [src[0]] + [_clean(f, f"fall[{i}]", 66 if i >= 6 else None, FRAG_BOXES.get(i, ())) for i, f in enumerate(src) if i]
        _lost(src, ox, oy, phase)
        frames = [_crop(f, ox, oy) for f in src]
        _check(frames, phase)
        made += sheet(frames, f"anim/runner64b_x2_{phase}", fps=30, loop=loop, status=STATUS, holds=holds)
        made += sheet([f.transpose(Image.Transpose.FLIP_LEFT_RIGHT) for f in frames], f"anim/runner64b_x2_{phase}_left",
                      fps=30, loop=loop, status=STATUS, holds=holds)
        # 미리보기 판(8배는 너무 커서 프레임 8장을 한 줄에 놓고 4배, 아래에 8배 확대 2줄이 아니라 한 줄 8배)
        board = Image.new("RGBA", (SIZE * len(frames), SIZE), (60, 70, 90, 255))
        for i, f in enumerate(frames):
            board.alpha_composite(f, (i * SIZE, 0))
        prev = REPO / "Art" / "Export" / "preview"
        prev.mkdir(parents=True, exist_ok=True)
        board.resize((board.width * 3, board.height * 3), Image.NEAREST).save(prev / f"w044_runner_{phase}.png")
        if phase == "fall":
            big = Image.new("RGBA", (SIZE * 4 * 4, SIZE * 4 * 2), (60, 70, 90, 255))
            for i, f in enumerate(frames):
                big.alpha_composite(f.resize((SIZE * 4, SIZE * 4), Image.NEAREST), ((i % 4) * SIZE * 4, (i // 4) * SIZE * 4))
            big.save(prev / "w044_runner_fall_x4.png")
    print("색 수:", COLORS)
    print("잘린 불투명 픽셀:", CLIPPED or "없음")
    print("지운 픽셀(구멍, 파편, 파편 덩어리 수):", ERASED)
    return made
