"""인물 그림용 pixelstudio 공용 도구(W-042, D-130). 레시피 `*_v3.py`·`main_lobby_v5.py`가 가져다 쓴다.

- 공유 팔레트(artkit.PIX 47색)만 쓴다. 새 색을 만들지 않는다.
- 부품마다 레이어를 따로 두고 K(짙은 남색) 바깥 외곽선을 둘러, 겹친 부품 사이에 경계가 생기게 한다.
- 광원은 왼쪽 위(D-079): 부품의 왼쪽·위쪽은 밝게, 오른쪽·아래쪽은 어둡게.
"""
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path[:0] = [str(ROOT / "tools/art"), str(ROOT / "tools/art/vendor/pixelstudio")]
from pixelstudio import Sprite  # noqa: E402
from artkit import PIX, palette_check, save  # noqa: E402,F401
from anim import sheet  # noqa: E402,F401

C = {k: "#%02x%02x%02x" % v[:3] for k, v in PIX.items() if k != "."}   # 공유 팔레트 글자 → 색


def build():       # build_art.py 가 모든 레시피를 훑을 때 건너뛰도록
    return []


_n = [0]


def new_layer(s, name=None):
    _n[0] += 1
    s.layer(name or "p%d" % _n[0])
    return s


def outline(s, ch="K"):
    s.outline(C[ch], where="outside")
    return s


def _norm(p0, p1):
    dx, dy = p1[0] - p0[0], p1[1] - p0[1]
    L = math.hypot(dx, dy) or 1.0
    return dx / L, dy / L


def quad(p0, p1, w0, w1, off0=0.0, off1=0.0):
    """선분 p0→p1 둘레의 사각형. off = 중심선에서 옆(오른쪽/아래 방향 +)으로 민 정도(폭 비율)."""
    ux, uy = _norm(p0, p1)
    nx, ny = -uy, ux
    if nx + ny < 0:        # 법선을 오른쪽·아래(어두운 쪽)로 맞춘다
        nx, ny = -nx, -ny
    a = (p0[0] + nx * w0 * (off0 - 0.5), p0[1] + ny * w0 * (off0 - 0.5))
    b = (p0[0] + nx * w0 * (off0 + 0.5), p0[1] + ny * w0 * (off0 + 0.5))
    c = (p1[0] + nx * w1 * (off1 + 0.5), p1[1] + ny * w1 * (off1 + 0.5))
    d = (p1[0] + nx * w1 * (off1 - 0.5), p1[1] + ny * w1 * (off1 - 0.5))
    return [a, b, c, d]


def rnd(p):
    return (int(round(p[0])), int(round(p[1])))


def capsule(s, p0, p1, w0, w1, base, dark=None, light=None, round_ends=True):
    """굵기가 변하는 팔다리 조각. 어두운 줄은 오른쪽/아래, 밝은 줄은 왼쪽/위에 얹는다."""
    c = C[base]
    s.polygon([rnd(q) for q in quad(p0, p1, w0, w1)], c)
    if round_ends:
        for p, w in ((p0, w0), (p1, w1)):
            r = max(1, int(w // 2))
            s.circle(round(p[0]), round(p[1]), r, c, fill=True)
    if dark:
        q = quad(p0, p1, w0 * 0.42, w1 * 0.42, 0.78, 0.78)
        s.polygon([rnd(t) for t in q], C[dark], only=c)
    if light:
        q = quad(p0, p1, w0 * 0.22, w1 * 0.22, 0.12, 0.12)
        s.polygon([rnd(t) for t in q], C[light], only=c)
    return s


def poly(s, pts, base, dark=None, light=None):
    s.polygon([rnd(p) for p in pts], C[base])
    return s


def rot(pt, ang_deg, origin=(0, 0)):
    a = math.radians(ang_deg)
    x, y = pt[0] - origin[0], pt[1] - origin[1]
    return (origin[0] + x * math.cos(a) - y * math.sin(a), origin[1] + x * math.sin(a) + y * math.cos(a))


def grid_blit(s, x0, y0, rows, mapping=None):
    """문자 몇 줄을 (x0,y0)에 찍는다(작은 눈·입 같은 1~3픽셀 디테일 전용). '.'은 건너뜀."""
    for dy, row in enumerate(rows):
        for dx, ch in enumerate(row):
            if ch != "." and ch != " ":
                s.px(x0 + dx, y0 + dy, C[ch])
    return s


def sil(s, path, scale=8):
    s.save_silhouette(path, scale=scale)


class Sc:
    """배율 f 로 그리는 Sprite 껍데기. 같은 그리기 코드를 목표 크기에서 직접 그리게 한다(그림을 늘리거나 줄이지 않고
    도형 좌표만 배율해 그 해상도에서 새로 칠한다). 한 픽셀 점은 배율된 칸 크기의 블록이 된다(칸이 비면 생략)."""

    def __init__(self, w, h, f=1.0, ox=0, oy=0):
        self.s = Sprite(w, h)
        self.f, self.ox, self.oy = f, ox, oy

    def X(self, x):
        return int(round(x * self.f)) + self.ox

    def Y(self, y):
        return int(round(y * self.f)) + self.oy

    def layer(self, name, above=None):
        self.s.layer(name, above)
        return self

    def px(self, x, y, color, only=None):
        return self.rect(x, y, x, y, color, only=only)

    def rect(self, x0, y0, x1, y1, color, fill=True, only=None):
        x0, x1 = sorted((x0, x1)); y0, y1 = sorted((y0, y1))
        a, b = self.X(x0), self.X(x1 + 1) - 1
        c, d = self.Y(y0), self.Y(y1 + 1) - 1
        if b < a or d < c:
            return self
        self.s.rect(a, c, b, d, color, fill=fill, only=only)
        return self

    def line(self, x0, y0, x1, y1, color, only=None):
        self.s.line(self.X(x0), self.Y(y0), self.X(x1), self.Y(y1), color, only=only)
        return self

    def ellipse(self, x0, y0, x1, y1, color, fill=True, only=None):
        self.s.ellipse(self.X(x0), self.Y(y0), self.X(x1 + 1) - 1, self.Y(y1 + 1) - 1, color, fill=fill, only=only)
        return self

    def polygon(self, pts, color, fill=True, only=None):
        self.s.polygon([(self.X(x), self.Y(y)) for x, y in pts], color, fill=fill, only=only)
        return self

    def circle(self, cx, cy, r, color, fill=False, only=None):
        rr = max(1, int(round(r * self.f)))
        self.s.circle(self.X(cx), self.Y(cy), rr, color, fill=fill, only=only)
        return self

    def outline(self, color, where="outside", diagonals=False):
        self.s.outline(color, where=where, diagonals=diagonals)
        return self

    def composite(self, frame=None):
        return self.s.composite(frame)
