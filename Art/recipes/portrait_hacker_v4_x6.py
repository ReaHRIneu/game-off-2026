"""해커 초상 v4 — PixelLab 생성물 가공(W-043, D-131, D-042·D-053, art-pipeline §4-3). 64x64, 7표정.

- 원본 hacker_portrait_base.png(왼쪽을 봄)에서 눈썹·눈·입 주변 픽셀만 고친 뒤 좌우로 뒤집어 오른쪽을 보게 한다.
  편집 좌표는 뒤집기 전(원본) 기준이다. 얼굴 방향·머리·헤드셋·옷은 바꾸지 않고, 새 색은 쓰지 않는다(그림 안의 색만).
- 바닥색(모서리에서 이어진 평평한 배경)은 가장자리에서 flood fill 로 투명하게 한다. 알파 0/255.
- 확대·축소 없음. 화면에서는 키 `portraits/hacker_v4_x6_*`(6배)로 96x96 논리 크기로 그린다.
[시안] 사용자 확정 전.
"""
from collections import deque
from pathlib import Path

from PIL import Image

from artkit import save

REPO = Path(__file__).resolve().parents[2]
SRC = REPO / "Art" / "Source" / "pixellab" / "2026-11-27" / "hacker_portrait_base.png"
EXPRS = ["neutral", "joy", "sadness", "anger", "fear", "surprise", "disgust"]

# 그림 안의 색(원본에서 읽은 값)
SK, SKS, SKD = (248, 228, 215), (206, 160, 152), (186, 141, 137)    # 살색 밝음·중간·그늘
LASH = (11, 8, 10)
BROW, BROW2 = (46, 30, 87), (74, 54, 123)                           # 머리카락 어두운 보라
WHITE = (251, 252, 250)
MOUTH_P, MOUTH, MOUTH_D = (229, 103, 148), (182, 91, 104), (98, 35, 54)


def P(im, pts, c):
    for x, y in pts:
        im.putpixel((x, y), c + (255,))


def R(x0, x1, y):
    return [(x, y) for x in range(x0, x1 + 1)]


def edit(expr, im):
    if expr == "joy":
        P(im, R(24, 31, 30) + R(24, 31, 29), SK)                    # 아래 눈꺼풀 올림 -> 반달
        P(im, R(26, 29, 29), SKS)                                   # 올라온 눈꺼풀 선
        P(im, [(21, 37), (22, 37)], MOUTH); P(im, [(23, 37), (24, 37)], MOUTH)
        P(im, [(24, 36)], MOUTH); P(im, [(21, 36)], MOUTH_P)        # 입꼬리 올림
        P(im, [(21, 37)], SK)
    elif expr == "sadness":
        P(im, [(24, 22), (25, 23), (26, 23), (27, 24)], BROW)       # 눈썹: 안쪽(코 쪽) 위, 바깥 아래
        P(im, [(24, 23), (26, 24)], BROW2)
        P(im, [(21, 37)], SK)
        P(im, [(22, 37), (23, 37)], MOUTH)
        P(im, [(21, 38), (24, 38)], MOUTH)                          # 입꼬리 내림
        P(im, [(24, 37)], SK)
    elif expr == "anger":
        P(im, [(24, 24), (25, 24), (26, 23), (27, 23)], BROW)       # 눈썹: 안쪽 아래
        P(im, R(25, 29, 26), LASH)                                  # 눈 위쪽을 1px 덮음
        P(im, R(21, 24, 37), MOUTH)                                 # 입 일자
        P(im, R(22, 23, 38), MOUTH_D)
    elif expr == "fear":
        P(im, [(24, 23), (25, 23), (26, 23)], BROW)                 # 눈썹 올림(높이 평평하게)
        P(im, [(24, 22), (25, 22), (26, 22), (27, 22)], BROW2)
        # 눈동자 작게: 홍채 가장자리를 흰자로
        P(im, [(26, 27), (26, 28), (26, 29), (29, 28), (29, 29), (28, 30), (27, 30), (26, 30)], WHITE)
        P(im, [(21, 37)], SK)
        P(im, R(22, 24, 37), MOUTH_D); P(im, R(22, 23, 38), MOUTH)  # 작게 벌림
    elif expr == "surprise":
        P(im, R(25, 28, 24), LASH)                                  # 눈 위 1px 키움
        P(im, R(26, 28, 25), (6, 55, 83)); P(im, [(25, 25), (29, 25)], (195, 191, 223))   # 눈동자가 위로 늘어남
        P(im, R(26, 29, 31), WHITE)                                 # 눈 아래 1px 키움
        P(im, [(21, 37)], SK)
        P(im, [(22, 36), (23, 36), (24, 36), (22, 37), (24, 37), (22, 38), (23, 38), (24, 38)], MOUTH)
        P(im, [(23, 37)], MOUTH_D)                                  # 작은 o
    elif expr == "disgust":
        P(im, [(25, 24), (26, 24), (27, 24)], BROW)                 # 한쪽 눈썹 내림
        P(im, [(24, 24)], BROW2)
        P(im, [(21, 37)], SK)
        P(im, [(22, 37), (23, 37)], MOUTH); P(im, [(24, 36)], MOUTH); P(im, [(21, 38)], MOUTH)   # 입 비대칭
    return im


def cut_bg(im):
    """모서리에서 이어진 바닥색을 투명으로(머리카락·재킷 안쪽 같은 색은 남김)."""
    w, h = im.size
    bg = im.getpixel((0, 0))
    seen = set()
    q = deque((x, y) for x in range(w) for y in (0, h - 1)) if False else deque()
    for x in range(w):
        q += [(x, 0), (x, h - 1)]
    for y in range(h):
        q += [(0, y), (w - 1, y)]
    while q:
        x, y = q.popleft()
        if (x, y) in seen or not (0 <= x < w and 0 <= y < h):
            continue
        seen.add((x, y))
        if im.getpixel((x, y)) != bg:
            continue
        im.putpixel((x, y), (0, 0, 0, 0))
        q += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    return im


def build():
    made = []
    base = Image.open(SRC).convert("RGBA")
    faces = []
    for e in EXPRS:
        im = edit(e, base.copy())
        im = cut_bg(im.transpose(Image.Transpose.FLIP_LEFT_RIGHT))
        assert im.size == (64, 64)
        assert all(a in (0, 255) for a in im.getchannel("A").getdata())
        assert len({p[:3] for p in im.getdata() if p[3]}) <= 48
        made.append(save(im, f"portraits/hacker_v4_x6_{e}.png", {"status": "본그림(PixelLab 가공)"}))
        faces.append(im)
    board = Image.new("RGBA", (64 * 8 * 7, 64 * 8), (60, 70, 90, 255))
    for i, f in enumerate(faces):
        board.alpha_composite(f.resize((512, 512), Image.NEAREST), (i * 512, 0))
    out = REPO / "Art" / "Export" / "preview" / "w043_hacker_faces.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    board.save(out)
    # 얼굴 확대판(검수용, 12배): 눈·입 주변만
    crop = Image.new("RGBA", (26 * 12 * 7, 28 * 12), (60, 70, 90, 255))
    for i, f in enumerate(faces):
        c = f.crop((24, 14, 50, 42)).resize((26 * 12, 28 * 12), Image.NEAREST)
        crop.alpha_composite(c, (i * 26 * 12, 0))
    crop.save(REPO / "Art" / "Export" / "preview" / "w043_hacker_faces_zoom.png")
    return made
