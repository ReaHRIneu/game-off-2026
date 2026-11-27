"""게임 아이콘(D-135) — 제목 SHIFT//REMOTE. 해커 초상(portraits/hacker_v4_x6_neutral, PixelLab 가공)을 크기 그대로
설계도 격자 바탕에 얹고, 오른쪽 위에 제목의 `//` 표식을 둔다. 64px 원본을 정수 배율로 키워 512·256·128을 만들고,
작은 크기(32·16)는 얼굴만 잘라 따로 만든다(축소하지 않음).
portrait_hacker_v4_x6 레시피가 먼저 실행돼 있어야 한다.
"""
from PIL import Image, ImageDraw

from artkit import EXPORT, save

BG, GRID, CY = (18, 32, 72, 255), (34, 70, 140, 255), (140, 210, 255, 255)


def grid_bg(n, step):
    im = Image.new("RGBA", (n, n), BG)
    d = ImageDraw.Draw(im)
    for i in range(0, n, step):
        d.line([(i, 0), (i, n - 1)], fill=GRID)
        d.line([(0, i), (n - 1, i)], fill=GRID)
    return im


def slashes(im, x0, y0, h):
    """`//` 표식: 두 줄, 폭 2px, 위에서 아래로 왼쪽으로 기움."""
    for k in range(2):
        for t in range(h):
            for w in range(2):
                im.putpixel((x0 + k * 5 + w - t // 2, y0 + t), CY)


def build():
    face = Image.open(EXPORT / "portraits/hacker_v4_x6_neutral.png").convert("RGBA")
    assert face.size == (64, 64)
    bb = face.getbbox()
    base = grid_bg(64, 8)
    base.alpha_composite(face, ((64 - (bb[2] - bb[0])) // 2 - bb[0], 64 - bb[3]))
    slashes(base, 52, 4, 9)
    ImageDraw.Draw(base).rectangle([0, 0, 63, 63], outline=CY)
    made = [save(base, "ui/icon_app_64.png", {"status": "본그림", "note": "게임 아이콘 원본(D-135)"})]
    for s in (2, 4, 8):
        made.append(save(base.resize((64 * s, 64 * s), Image.NEAREST), f"ui/icon_app_{64 * s}.png", {"status": "본그림", "note": f"아이콘 {s}배"}))
    # 작은 크기: 얼굴 부분만 잘라 그대로 쓴다(눈·헤드셋이 남는 영역)
    for n, (cx, cy) in ((32, (38, 31)), (16, (38, 29))):
        small = grid_bg(n, 8 if n == 32 else 16)
        crop = face.crop((cx - n // 2, cy - n // 2, cx + n // 2, cy + n // 2))
        small.alpha_composite(crop)
        ImageDraw.Draw(small).rectangle([0, 0, n - 1, n - 1], outline=CY)
        made.append(save(small, f"ui/icon_app_{n}.png", {"status": "본그림", "note": "작은 아이콘(얼굴 부분)"}))
    for im in [Image.open(p) for p in made]:
        a = im.convert("RGBA").getchannel("A")
        assert set(a.getdata()) <= {0, 255}
    return made
