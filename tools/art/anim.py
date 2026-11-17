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
FPS = 8             # 기본 재생 속도


def sheet(frames: list[Image.Image], rel: str, fps: int = FPS, loop: bool = True,
          pivot: str = "bottom-center", status: str = "밑그림") -> list[Path]:
    """같은 크기 프레임들을 가로로 이어 붙여 저장하고, GIF 미리보기도 만든다."""
    w, h = frames[0].size
    assert all(f.size == (w, h) for f in frames), "프레임 크기가 다릅니다"
    sh = Image.new("RGBA", (w * len(frames), h), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        sh.alpha_composite(f, (i * w, 0))
    meta = {"status": status, "frame_size": [w, h], "frames": len(frames),
            "rects": [[i * w, 0, w, h] for i in range(len(frames))],
            "fps": fps, "loop": loop, "pivot": pivot, "unit": UNIT}
    out = [save(sh, rel + "_sheet.png", meta)]
    # 미리보기 GIF(바탕을 채워 투명 깨짐 방지, 2배 무보간)
    gif_frames = []
    for f in frames:
        bg = Image.new("RGBA", (w, h), (30, 34, 48, 255))
        bg.alpha_composite(f)
        gif_frames.append(bg.resize((w * 2, h * 2), Image.NEAREST).convert("P", palette=Image.ADAPTIVE))
    gif = EXPORT / "preview" / "anim" / (Path(rel).name + ".gif")
    gif.parent.mkdir(parents=True, exist_ok=True)
    gif_frames[0].save(gif, save_all=True, append_images=gif_frames[1:], duration=int(1000 / fps),
                       loop=0 if loop else 1, disposal=2)
    out.append(gif)
    return out
