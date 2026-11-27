"""메인 화면 v5 — v4 방 배치 + pixelstudio 인물(W-042, D-130, D-128). 640×360 합성 한 장.

- 방(bg)·앞쪽 칸막이·모니터 뒷면(fg)은 v4 레이어를 그대로 쓴다(아래쪽 배치 유지). 위쪽 어두운 천장 띠는 자르지 않고
  그 안에 조명·배관·환기구·늘어진 선을 그려 빈 면적을 줄인다.
- 러너: `char_runner128_v3`의 관절 도구(새 러너 시트와 같은 비례)를 1.2배 목표 크기에서 직접 그려 침대에 걸터앉힌다.
- 해커: `portrait_hacker_v3`의 머리·헤드셋 도형을 0.46배 목표 크기에서 직접 그리고, 눈·몸·의자는 이 크기로 따로 찍는다
  (초상을 줄여 쓰지 않는다). 모든 색은 PIX 안.
- 레이어 합본: bg → 러너 → 해커 → fg. 출력 `scenes/main_lobby_v5.png`(v4 출력은 덮어쓰지 않는다), 비교 미리보기 포함.
[시안] 사용자 확정 전.
"""
import math

from PIL import Image

from ps_char import C, Sc, Sprite, new_layer, outline, palette_check, save, PIX
import char_runner128_v3 as rn
import portrait_hacker_v3 as hk
from artkit import EXPORT

W, H = 640, 360


# ---------------- 방 위쪽 띠(천장) ----------------
def ceiling(bg):
    s = Sprite(W, H)
    new_layer(s)
    # 천장 판 이음선과 모서리 몰딩
    for x in (215, 300, 392, 480, 560):
        s.line(x, 0, x, 30, C["2"]) if False else s.line(x, 0, x, 31, C["1"])
    s.line(145, 31, 639, 31, C["K"]); s.line(145, 32, 639, 32, C["2"])
    # 형광등(켜짐) + 아래로 번지는 빛(디더)
    s.rect(318, 8, 470, 18, C["K"]); s.rect(320, 9, 468, 17, C["3"]); s.rect(322, 11, 466, 15, C["W"])
    s.line(320, 9, 468, 9, C["C"])
    for y in range(19, 31):
        for x in range(300, 490):
            if ((x + y) % 2 == 0) and (y - 19) < 3 + 8 * (1 - abs(x - 394) / 100):
                s.px(x, y, C["2"], only=C["4"])
    # 환기구
    s.rect(520, 6, 586, 24, C["K"]); s.rect(522, 8, 584, 22, C["2"])
    for x in range(526, 582, 5):
        s.rect(x, 10, x + 2, 20, C["0"])
    s.line(522, 8, 584, 8, C["3"])
    # 배관 두 줄(벽 윗부분) + 고정구
    for y0, ch in ((44, "2"), (58, "1")):
        s.rect(145, y0, 639, y0 + 5, C["K"]); s.rect(145, y0 + 1, 639, y0 + 4, C[ch])
        s.line(145, y0 + 1, 639, y0 + 1, C["3"] if ch == "2" else C["2"])
    for x in (170, 250, 340, 430, 510, 600):
        s.rect(x, 40, x + 3, 66, C["K"]); s.rect(x + 1, 41, x + 2, 65, C["4"])
    # 늘어진 선
    for x0, sway in ((236, 6), (452, -7), (548, 5)):
        pts = [(x0 + round(sway * math.sin(y / 9.0)), y) for y in range(64, 84)]
        for (xa, ya), (xb, yb) in zip(pts, pts[1:]):
            s.line(xa, ya, xb, yb, C["K"])
    # 벽 윗부분 밝기 띠(위에서 아래로 약하게 밝아지는 디더)
    for y in range(68, 86):
        for x in range(145, 640):
            if (x + y) % 3 == 0:
                s.px(x, y, C["1"], only=C["4"])
    out = bg.copy()
    out.alpha_composite(s.composite())
    return out


# ---------------- 러너(앉음) ----------------
RUN_F = 1.2


def runner_seated():
    s = rn.figure(78, 70, 62, 56, (-22, 25), (22, 52), lean=-2, head=("smile", False), foot_fa=(0, 0),
                  sink=(208, 0), cx=171, size=(W, H), f=RUN_F)
    return s.composite()


# ---------------- 해커(모니터 뒤 상반신) ----------------
HF, HOX, HOY = 0.55, 273, 124


def _eye(r, x, y, w=5):
    for i in range(w):
        r.px(x + i, y, C["K"])
    row = ["W", "W", "v", "T", "W", "W"][:w] if w <= 6 else ["W"] * w
    for i, ch in enumerate(row):
        r.px(x + i, y + 1, C[ch])
    for i in range(1, w - 1):
        r.px(x + i, y + 2, C["c"])


class _T:
    """해커 몸 좌표(어깨 폭 0.85배, 위쪽을 아래로 눌러 머리 크기에 맞춤)를 변환해 그리는 껍데기."""

    def __init__(self, r):
        self.r = r

    @staticmethod
    def p(x, y):
        return (int(round(298 + (x - 298) * 0.85)), int(round(222 - (222 - y) * 0.88)))

    def polygon(self, pts, c, only=None):
        self.r.polygon([self.p(*q) for q in pts], c, only=only)

    def line(self, x0, y0, x1, y1, c):
        self.r.line(*self.p(x0, y0), *self.p(x1, y1), c)

    def px(self, x, y, c):
        self.r.px(*self.p(x, y), c)

    def rect(self, x0, y0, x1, y1, c):
        a, b = self.p(x0, y0), self.p(x1, y1)
        self.r.rect(a[0], a[1], b[0], b[1], c)


def hacker_body(sc):
    r = _T(sc.s)
    new_layer(sc)            # 의자 등받이
    r.polygon([(256, 222), (255, 162), (264, 146), (332, 146), (341, 162), (340, 222)], C["0"])
    r.polygon([(318, 150), (332, 146), (341, 162), (340, 222), (320, 222)], C["K"], only=C["0"])
    outline(r) if False else sc.outline(C["K"])
    new_layer(sc)            # 재킷
    r.polygon([(262, 222), (263, 164), (273, 154), (323, 154), (333, 164), (335, 222)], C["n"])
    r.polygon([(318, 154), (333, 164), (335, 222), (316, 222)], C["N"], only=C["n"])
    r.polygon([(264, 164), (273, 155), (288, 154), (284, 164), (272, 176)], C["2"], only=C["n"])   # 왼쪽 위 밝은 면
    r.polygon([(266, 164), (274, 156), (282, 156), (276, 163)], C["3"], only=C["2"])
    r.polygon([(284, 154), (312, 154), (308, 168), (298, 182), (289, 168)], C["N"])                # 깃
    r.polygon([(291, 156), (305, 156), (302, 172), (298, 178), (294, 172)], C["0"])                # 검정 티
    for yy in (157, 160, 163):                                                                      # 깃 리브(가로 줄)
        r.line(285 + (yy - 157) // 2, yy, 291, yy, C["n"]); r.line(305, yy, 311 - (yy - 157) // 2, yy, C["n"])
    r.line(298, 179, 296, 222, C["3"]); r.px(297, 190, C["c"])                                      # 지퍼
    r.line(274, 158, 270, 176, C["N"])                                                              # 어깨 솔기
    r.line(268, 190, 277, 187, C["N"]); r.line(267, 196, 278, 193, C["N"]); r.line(267, 203, 276, 200, C["N"])   # 소매 주름
    r.rect(321, 186, 329, 200, C["N"]); r.line(321, 187, 329, 187, C["2"]); r.px(325, 190, C["3"])  # 팔 주머니
    sc.outline(C["K"])
    new_layer(sc)            # 목
    r.polygon([(291, 148), (304, 148), (305, 158), (298, 162), (291, 158)], C["S"])
    r.polygon([(298, 148), (304, 148), (305, 158), (298, 161)], C["s"], only=C["S"])
    sc.outline(C["K"])


def hacker_face(sc):
    r = sc.s
    new_layer(sc)
    X, Y = sc.X, sc.Y
    ex, ey = X(39), Y(44)
    fx = X(58)
    _eye(r, ex, ey, 6)
    _eye(r, fx, ey, 6)
    for i in range(6):
        r.px(ex + i, ey - 2, C["K"]); r.px(fx + i, ey - 2, C["K"])
    r.px(X(70), Y(55), C["s"]); r.px(X(70) + 1, Y(55), C["s"])                  # 코 그림자
    r.line(X(57), Y(63), X(62), Y(63), C["m"])                                   # 입
    r.line(X(40), Y(54), X(42), Y(54), C["l"])                                   # 볼 하이라이트


def hacker_layer():
    sc = Sc(W, H, HF, HOX, HOY)
    hk.base(sc, body=hacker_body)
    hk.headset(sc)
    hacker_face(sc)
    return sc.composite()


def compose():
    bg4 = Image.open(EXPORT / "scenes" / "main_lobby_v4_bg.png").convert("RGBA")
    fg4 = Image.open(EXPORT / "scenes" / "main_lobby_v4_fg.png").convert("RGBA")
    bg = ceiling(bg4)
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for lay in (bg, runner_seated(), hacker_layer(), fg4):
        out.alpha_composite(lay)
    return out


def build():
    scene = compose()
    assert not palette_check(scene)
    assert all(a in (0, 255) for a in scene.getchannel("A").getdata())
    path = save(scene, "scenes/main_lobby_v5.png", {"status": "시안", "note": "W-042 v4 배치 + pixelstudio 인물 + 천장 띠 채움"})
    prev = EXPORT / "preview"
    scene.resize((W * 2, H * 2), Image.NEAREST).save(prev / "w042_main_lobby_v5_x2.png")
    v4 = Image.open(EXPORT / "scenes" / "main_lobby_v4.png").convert("RGBA")
    cmp_ = Image.new("RGBA", (W * 2 + 10, H), (255, 255, 255, 255))
    cmp_.alpha_composite(v4, (0, 0)); cmp_.alpha_composite(scene, (W + 10, 0))
    cmp_.save(prev / "w042_main_compare.png")
    return [path]
