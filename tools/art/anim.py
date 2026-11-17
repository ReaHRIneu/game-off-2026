"""애니메이션 프레임 도구 — 128px 규격(D-075), animation-audit §3-3 규격.

- 프레임 목록 → 가로 시트 PNG + 미리보기 GIF(정수 배율, 무보간) + manifest(프레임 좌표·FPS·loop·기준점)
- 이름 규칙: <종류>_<동작>  (프레임은 시트 안 칸 번호로 구분)
- 기준점(pivot): bottom-center(바닥 오브젝트·인물) / top-left(섹터 전체·벽걸이)
"""
from __future__ import annotations

from pathlib import Path

from PIL import Image

from artkit import EXPORT, save

UNIT = 128          # 기본 단위(px) — D-075. 이전 32px의 4배
SCALE = 4           # 32px 시절 좌표를 옮길 때 곱하는 값
FPS = 30            # 기본 재생 속도 — D-077(30프레임 기준)
COLS = 8            # 시트 한 줄 최대 칸 수
MAX_SIDE = 2048     # Web(WebGL) 기본 최대 텍스처 크기


def sheet(frames: list[Image.Image], rel: str, fps: int = FPS, loop: bool = True,
          pivot: str = "bottom-center", status: str = "밑그림", holds: list[int] | None = None) -> list[Path]:
    """같은 크기 프레임들을 가로로 이어 붙여 저장하고, GIF 미리보기도 만든다.

    holds: 프레임마다 화면에 머무는 틱 수(1틱 = 1/fps초). 없으면 모두 1. 천천히 깜빡이는 동작을
    30 FPS 기준 안에서 프레임을 복제하지 않고 표현한다."""
    holds = holds or [1] * len(frames)
    assert len(holds) == len(frames)
    w, h = frames[0].size
    assert all(f.size == (w, h) for f in frames), "프레임 크기가 다릅니다"
    # 격자 시트: 한 줄 최대 COLS칸(Web 최대 텍스처 2048px 안에 들도록), 왼쪽 위부터 채움
    cols = max(1, min(len(frames), COLS, MAX_SIDE // w))
    rows = (len(frames) + cols - 1) // cols
    assert rows * h <= MAX_SIDE, "시트 높이가 최대 크기를 넘습니다"
    sh = Image.new("RGBA", (w * cols, h * rows), (0, 0, 0, 0))
    rects = []
    for i, f in enumerate(frames):
        x, y = (i % cols) * w, (i // cols) * h
        sh.alpha_composite(f, (x, y))
        rects.append([x, y, w, h])
    meta = {"status": status, "frame_size": [w, h], "frames": len(frames), "cols": cols,
            "rects": rects,
            "fps": fps, "holds": holds, "loop": loop, "pivot": pivot, "unit": UNIT}
    out = [save(sh, rel + "_sheet.png", meta)]
    # 미리보기 GIF(바탕을 채워 투명 깨짐 방지, 2배 무보간)
    gif_frames = []
    for f in frames:
        bg = Image.new("RGBA", (w, h), (30, 34, 48, 255))
        bg.alpha_composite(f)
        gif_frames.append(bg.resize((w * 2, h * 2), Image.NEAREST).convert("P", palette=Image.ADAPTIVE))
    gif = EXPORT / "preview" / "anim" / (Path(rel).name + ".gif")
    gif.parent.mkdir(parents=True, exist_ok=True)
    gif_frames[0].save(gif, save_all=True, append_images=gif_frames[1:], duration=[int(1000 * h / fps) for h in holds],
                       loop=0 if loop else 1, disposal=2)
    out.append(gif)
    return out
