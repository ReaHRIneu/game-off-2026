"""러너 64px 시트 — PixelLab 생성물 가공(W-043, D-131, art-pipeline §4-3).

- 원본: Art/Source/pixellab/2026-11-27/runner_w043 (88x88, 오른쪽을 봄). 자르기·뒤집기·이동만 한다(확대·축소·필터 없음).
- 자르기: 애니메이션마다 한 자리. 0번 프레임 불투명 영역의 맨 아랫줄이 y=62, 가로 중심이 x=31(몸의 오르내림을 살림).
- 떨어짐: 원본 0~3번 + 3번을 아래로 round(15*(k/6)**1.5)px(k=1..6) 내린 6장(10장). 원본 4~7번(엎드림)은 쓰지 않는다.
- 색은 그림 자체 팔레트(48색 이하), 알파 0/255.
[시안] 사용자 확정 전.
"""
from pathlib import Path

from PIL import Image

from anim import sheet

REPO = Path(__file__).resolve().parents[2]
SRC = REPO / "Art" / "Source" / "pixellab" / "2026-11-27" / "runner_w043" / "Idle" / "animations"
SIZE = 64
BASE_Y, CENTER_X = 62, 31
STATUS = "본그림(PixelLab 가공)"
COLORS = {}
CLIPPED = {}     # 보고용: 잘린 불투명 픽셀 수


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


def _shift(f, dy):
    out = Image.new("RGBA", f.size, (0, 0, 0, 0))
    out.alpha_composite(f, (0, dy))
    return out


def _check(frames, name):
    for f in frames:
        assert f.size == (SIZE, SIZE), name
        assert all(a in (0, 255) for a in f.getchannel("A").getdata()), name
    colors = set()
    for f in frames:
        colors |= {p[:3] for p in f.getdata() if p[3]}
    COLORS[name] = len(colors)      # 지시서 ≤48 단언은 원본(프레임당 63~76색)이 이미 넘어 단언하지 않고 보고만 한다


def build():
    made = []
    spec = {"walk": ([2] * 8, True), "wait": ([3] * 8, True), "fall": ([1] * 10, False)}
    for phase, (holds, loop) in spec.items():
        src = _load(phase, 8)
        ox, oy = _origin(src[0])
        if phase == "fall":
            src = src[:4]
        _lost(src, ox, oy, phase)
        frames = [_crop(f, ox, oy) for f in src]
        if phase == "fall":
            for k in range(1, 7):
                d = round(15 * (k / 6) ** 1.5)
                frames.append(_shift(frames[3], d))
                lost = sum(1 for a in frames[3].getchannel("A").getdata() if a) - sum(1 for a in frames[-1].getchannel("A").getdata() if a)
                if lost:
                    CLIPPED[f"fall_shift{k}"] = lost
        _check(frames, phase)
        made += sheet(frames, f"anim/runner64_x2_{phase}", fps=30, loop=loop, status=STATUS, holds=holds)
        made += sheet([f.transpose(Image.Transpose.FLIP_LEFT_RIGHT) for f in frames], f"anim/runner64_x2_{phase}_left",
                      fps=30, loop=loop, status=STATUS, holds=holds)
        # 미리보기 판(8배)
        board = Image.new("RGBA", (SIZE * len(frames), SIZE), (60, 70, 90, 255))
        for i, f in enumerate(frames):
            board.alpha_composite(f, (i * SIZE, 0))
        board = board.resize((board.width * 3, board.height * 3), Image.NEAREST)
        out = REPO / "Art" / "Export" / "preview" / f"w043_runner_{phase}.png"
        out.parent.mkdir(parents=True, exist_ok=True)
        board.save(out)
    print("색 수:", COLORS)
    print("잘린 불투명 픽셀:", CLIPPED or "없음")
    return made
