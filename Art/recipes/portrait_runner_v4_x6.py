"""러너 초상 v4 — PixelLab 생성물 가공(W-044, D-131, D-042·D-053, art-pipeline §4-3). 64x64, 7표정.

- 원본 runner_portrait_base.png(왼쪽을 봄)에서 눈썹·눈·입 주변 픽셀만 고친 뒤 좌우로 뒤집어 오른쪽을 보게 한다.
  편집 좌표는 뒤집기 전(원본) 기준이다. 얼굴 방향·머리·옷은 바꾸지 않고, 새 색은 쓰지 않는다(그림 안의 색만).
- 바닥색(모서리에서 이어진 평평한 배경)은 가장자리에서 flood fill 로 투명하게 한다. 알파 0/255.
- 확대·축소 없음. 화면에서는 키 `portraits/runner_v4_x6_*`(6배)로 96x96 논리 크기로 그린다.
[시안] 사용자 확정 전.
"""
from collections import deque
from pathlib import Path

from PIL import Image

from artkit import save

REPO = Path(__file__).resolve().parents[2]
SRC = REPO / "Art" / "Source" / "pixellab" / "2026-11-27" / "runner_portrait_base.png"
HACKER = REPO / "Art" / "Export" / "portraits" / "hacker_v4_x6_neutral.png"
EXPRS = ["neutral", "joy", "sadness", "anger", "fear", "surprise", "disgust"]

# 그림 안의 색(원본에서 읽은 값)
SK = (249, 230, 215)          # 살색
SKS = (213, 141, 136)         # 살색 그늘·입술
PINK = (230, 173, 168)        # 볼 연분홍
LASH = (28, 25, 25)           # 속눈썹·윤곽
BROW, BROW2 = (78, 46, 57), (71, 43, 53)     # 머리카락 어두운 갈색
WHITE = (253, 252, 251)
TEETH = (253, 252, 251)
MOUTH_D = (78, 46, 57)


def P(im, pts, c):
    for x, y in pts:
        im.putpixel((x, y), c + (255,))


def R(x0, x1, y):
    return [(x, y) for x in range(x0, x1 + 1)]


def edit(expr, im):
    if expr == "joy":
        # 아래 눈꺼풀을 올려 눈을 반달 쪽으로(먼 눈 x30-36, 가까운 눈 x20-23)
        P(im, R(30, 36, 29) + R(30, 36, 28), SK)
        P(im, R(31, 35, 28), SKS)
        P(im, R(20, 23, 30) + R(20, 23, 29), SK)
        P(im, R(20, 22, 29), SKS)
        # 입: 이를 보이는 큰 미소
        P(im, [(22, 33), (30, 33)], SKS)
        P(im, R(23, 29, 34), SKS)
        P(im, R(24, 28, 35), SKS)
        P(im, R(25, 27, 35), TEETH)
        P(im, R(25, 27, 36), SKS)
    elif expr == "sadness":
        P(im, [(28, 21), (29, 21), (30, 21), (31, 22), (32, 22), (33, 22), (34, 23)], BROW2)   # 먼 눈썹: 안쪽(코 쪽) 위, 바깥 아래
        P(im, [(28, 22), (29, 22)], BROW2)
        P(im, [(24, 23), (23, 24)], BROW2)
        P(im, [(25, 35), (26, 35), (27, 35)], SKS)
        P(im, [(24, 36), (28, 36)], SKS)                                      # 입꼬리 내림
        P(im, [(26, 36)], SK)
    elif expr == "anger":
        P(im, [(28, 23), (29, 23), (29, 22), (30, 22), (31, 22), (32, 21), (33, 21), (34, 21), (35, 20)], LASH)   # 먼 눈썹: 안쪽 아래
        P(im, R(31, 35, 24), LASH)                                            # 눈 위쪽을 1px 덮음
        P(im, [(21, 25), (22, 25), (23, 26)], LASH)
        P(im, R(24, 28, 35), SKS)                                             # 입 일자
        P(im, R(25, 27, 36), MOUTH_D)
    elif expr == "fear":
        P(im, R(29, 34, 21), BROW)                                            # 눈썹 올림(높이 평평하게)
        P(im, R(22, 25, 23), BROW)
        # 눈동자 작게: 홍채 가장자리를 흰자로(하이라이트 유지)
        P(im, [(32, 27), (32, 28), (32, 29), (35, 27), (35, 28), (33, 29), (34, 29), (31, 29)], WHITE)
        P(im, [(21, 29), (22, 29), (23, 29), (22, 30), (23, 30)], WHITE)
        P(im, R(25, 27, 34), SKS)
        P(im, [(24, 35), (28, 35)], SKS)
        P(im, R(25, 27, 35), MOUTH_D)
        P(im, R(25, 27, 36), SKS)                                             # 작게 벌림
    elif expr == "surprise":
        P(im, R(31, 34, 22), LASH)                                            # 먼 눈 위 1px 키움
        P(im, [(31, 23), (35, 23)], WHITE); P(im, R(32, 34, 23), (32, 66, 70))
        P(im, R(31, 35, 30), WHITE)                                           # 눈 아래 1px 키움
        P(im, R(20, 22, 24), LASH)                                            # 가까운 눈도 1px
        P(im, R(20, 22, 25), WHITE)
        P(im, [(25, 34), (26, 34), (27, 34)], SKS)
        P(im, [(24, 35), (28, 35)], SKS)
        P(im, R(25, 27, 35), MOUTH_D)
        P(im, R(25, 27, 36), SKS)                                             # 작은 o
    elif expr == "disgust":
        P(im, [(29, 22), (30, 22), (31, 23), (32, 23)], BROW)                 # 한쪽(먼 눈) 눈썹 내림
        P(im, R(31, 34, 24), LASH)                                            # 반쯤 감은 눈
        P(im, [(24, 34), (25, 34)], SKS)
        P(im, [(26, 35), (27, 35)], SKS)
        P(im, [(28, 36), (29, 36)], SKS)                                      # 입 비대칭
    return im


def cut_bg(im):
    """모서리에서 이어진 바닥색을 투명으로(머리카락·옷 안쪽 같은 색은 남김)."""
    w, h = im.size
    bg = im.getpixel((0, 0))
    seen = set()
    q = deque()
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
        made.append(save(im, f"portraits/runner_v4_x6_{e}.png", {"status": "본그림(PixelLab 가공)"}))
        faces.append(im)
    prev = REPO / "Art" / "Export" / "preview"
    prev.mkdir(parents=True, exist_ok=True)
    board = Image.new("RGBA", (64 * 8 * 7, 64 * 8), (60, 70, 90, 255))
    for i, f in enumerate(faces):
        board.alpha_composite(f.resize((512, 512), Image.NEAREST), (i * 512, 0))
    board.save(prev / "w044_runner_faces.png")
    # 얼굴 확대판(검수용, 12배): 눈·입 주변만
    crop = Image.new("RGBA", (30 * 12 * 7, 26 * 12), (60, 70, 90, 255))
    for i, f in enumerate(faces):
        c = f.crop((18, 14, 48, 40)).resize((30 * 12, 26 * 12), Image.NEAREST)
        crop.alpha_composite(c, (i * 30 * 12, 0))
    crop.save(prev / "w044_runner_faces_zoom.png")
    # 해커 neutral 과 러너 neutral 나란히(8배)
    if HACKER.exists():
        pair = Image.new("RGBA", (512 * 2 + 16, 512), (60, 70, 90, 255))
        pair.alpha_composite(Image.open(HACKER).convert("RGBA").resize((512, 512), Image.NEAREST), (0, 0))
        pair.alpha_composite(faces[0].resize((512, 512), Image.NEAREST), (528, 0))
        pair.save(prev / "w044_pair.png")
    return made
