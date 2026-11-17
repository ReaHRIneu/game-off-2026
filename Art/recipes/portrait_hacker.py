"""주인공(해커) 초상 96×96 도트 시안 — 표정 7종.

- D-049 96×96, D-052 B형(맨얼굴 + 고물 헤드셋, 생활형)·서브컬처 게임풍·20대 여성·말수 적고 무뚝뚝
- D-053 표정 = 기본 감정 수: 기본(무표정) + 에크만 6감정(기쁨·슬픔·분노·두려움·놀람·혐오) [해석]
- 무뚝뚝한 성격이라 표정 변화는 크지 않게: 눈썹·눈꺼풀·입만 몇 픽셀 바꾼다
- 공유 팔레트(artkit.PIX)만 사용, 안티앨리어싱 없음
[시안] 사용자 확정 전.
"""
from artkit import (canvas, contact_sheet, draw_ellipse, draw_line, draw_poly, outline_outside,
                    palette_check, pfill, pset, save, upscale)

N = 96
EYES = {"L": 33, "R": 53}  # 눈 상자 왼쪽 x (폭 11)
EY = 39                     # 눈 상자 위 y (높이 11)
EW, EH = 11, 11


def base():
    """눈썹·눈·입을 뺀 공통 몸·머리·헤드셋."""
    im = canvas(N, N)
    # 뒷머리(턱선까지 오는 단발)
    draw_ellipse(im, (17, 9, 79, 74), "J")
    # 어깨·재킷(낡은 오버사이즈 재킷)
    draw_poly(im, [(10, 95), (16, 82), (30, 73), (66, 73), (80, 82), (86, 95)], "2")
    draw_poly(im, [(48, 73), (66, 73), (80, 82), (86, 95), (48, 95)], "4")
    draw_poly(im, [(30, 73), (40, 70), (48, 84), (56, 70), (66, 73), (58, 86), (48, 92), (38, 86)], "4")  # 후드 깃
    pfill(im, 44, 84, 52, 95, "K")  # 안쪽 셔츠
    # 목
    pfill(im, 41, 62, 55, 75, "s")
    pfill(im, 41, 62, 49, 72, "S")
    # 얼굴(서브컬처풍 갸름한 턱)
    draw_poly(im, [(28, 30), (68, 30), (68, 50), (62, 62), (48, 70), (34, 62), (28, 50)], "S")
    draw_poly(im, [(62, 34), (68, 34), (68, 50), (62, 62), (55, 67), (60, 52)], "s")  # 오른쪽 볼 그림자
    pfill(im, 30, 31, 66, 36, "s")  # 앞머리 아래 그림자
    for x, y in [(44, 52), (45, 53)]:
        pset(im, x, y, "l")  # 코 옆 하이라이트
    pset(im, 48, 54, "s"); pset(im, 48, 55, "s")  # 코
    # 옆머리(턱까지)
    draw_poly(im, [(24, 26), (33, 32), (31, 66), (26, 70), (22, 56)], "H")
    draw_poly(im, [(72, 26), (63, 32), (65, 66), (70, 70), (74, 56)], "H")
    # 앞머리(삐죽한 가닥)
    draw_poly(im, [(22, 28), (28, 14), (40, 8), (56, 8), (68, 14), (74, 28), (72, 40),
                   (66, 31), (61, 38), (55, 29), (49, 37), (44, 29), (38, 38), (32, 31), (26, 40)], "H")
    for pts in ([(36, 12), (33, 22)], [(50, 10), (49, 22)], [(60, 13), (63, 22)]):
        draw_line(im, pts, "U")  # 머리결 하이라이트
    draw_line(im, [(40, 14), (38, 20)], "J")
    # 헤드셋 밴드(머리 위) + 낡은 테이프
    draw_ellipse(im, (20, 4, 76, 76), "3", width=3)
    pfill(im, 17, 40, 79, 79, "."[0] if False else "S")  # placeholder (아래에서 다시 덮음)
    return im


def headset(im):
    """밴드 아래쪽 반을 지우는 대신, 귀 컵을 덮어 그린다."""
    # 귀 컵
    for x0 in (16, 68):
        pfill(im, x0, 38, x0 + 11, 58, "K")
        pfill(im, x0 + 1, 39, x0 + 10, 57, "3")
        pfill(im, x0 + 3, 41, x0 + 8, 55, "2")
        pfill(im, x0 + 1, 39, x0 + 10, 39, "W")
    # 테이프로 감은 부분(고물 장비)
    pfill(im, 44, 4, 52, 7, "h")
    pset(im, 46, 5, "D"); pset(im, 50, 6, "D")
    # 마이크 붐(오른쪽 컵에서 입 쪽으로)
    draw_line(im, [(70, 56), (60, 64)], "K", 2)
    pfill(im, 57, 63, 60, 66, "2")
    pset(im, 58, 64, "R")  # 작은 상태 LED
    # 케이블(왼쪽 컵에서 어깨로)
    draw_line(im, [(20, 58), (18, 70), (22, 84)], "K", 2)
    return im


def rebuild():
    """밴드가 얼굴 아래까지 그려지지 않도록 순서를 정리해 다시 그린다."""
    im = canvas(N, N)
    # 1) 밴드: 위쪽 반원만
    band = canvas(N, N)
    draw_ellipse(band, (20, 4, 76, 76), "3", width=3)
    px = band.load()
    for y in range(40, N):
        for x in range(N):
            px[x, y] = (0, 0, 0, 0)
    # 2) 몸·머리
    body = base_no_band()
    im.alpha_composite(body)
    im.alpha_composite(band)
    headset(im)
    return im


def base_no_band():
    im = canvas(N, N)
    draw_ellipse(im, (17, 9, 79, 74), "J")
    draw_poly(im, [(10, 95), (16, 82), (30, 73), (66, 73), (80, 82), (86, 95)], "2")
    draw_poly(im, [(48, 73), (66, 73), (80, 82), (86, 95), (48, 95)], "4")
    draw_poly(im, [(30, 73), (40, 70), (48, 84), (56, 70), (66, 73), (58, 86), (48, 92), (38, 86)], "4")
    pfill(im, 44, 84, 52, 95, "K")
    pfill(im, 43, 62, 53, 75, "s")
    pfill(im, 43, 62, 48, 71, "S")
    draw_poly(im, [(29, 30), (67, 30), (67, 50), (58, 62), (48, 68), (38, 62), (29, 50)], "S")
    draw_poly(im, [(62, 34), (67, 34), (67, 50), (58, 62), (54, 65), (60, 52)], "s")
    pfill(im, 30, 31, 66, 34, "s")
    pset(im, 44, 52, "l"); pset(im, 45, 53, "l")
    pset(im, 48, 55, "s"); pset(im, 49, 56, "s")
    draw_poly(im, [(24, 26), (33, 32), (31, 66), (26, 70), (22, 56)], "H")
    draw_poly(im, [(72, 26), (63, 32), (65, 66), (70, 70), (74, 56)], "H")
    draw_poly(im, [(22, 28), (28, 14), (40, 8), (56, 8), (68, 14), (74, 28), (72, 40),
                   (66, 31), (61, 38), (55, 29), (49, 37), (44, 29), (38, 38), (32, 31), (26, 40)], "H")
    for pts in ([(36, 12), (33, 22)], [(50, 10), (49, 22)], [(60, 13), (63, 22)]):
        draw_line(im, pts, "U")
    draw_line(im, [(40, 14), (38, 20)], "J")
    return im


# ---- 표정 부품 ----
def eye(im, side, mode):
    """눈 상자 11×11(서브컬처풍 큰 눈). mode: half(반쯤 감김·기본), narrow, wide, smile, down"""
    x0, y0 = EYES[side], EY
    if mode == "smile":  # 아래로 굽은 눈(웃는 눈)
        draw_line(im, [(x0, y0 + 7), (x0 + 3, y0 + 5), (x0 + 7, y0 + 5), (x0 + 10, y0 + 7)], "K", 2)
        return
    top = {"half": 3, "narrow": 5, "wide": 0, "down": 4}[mode]
    pfill(im, x0, y0 + top, x0 + EW - 1, y0 + EH - 1, "W")             # 흰자
    iris_w = 5 if mode == "wide" else 7
    ix = x0 + (EW - iris_w) // 2
    pfill(im, ix, y0 + top, ix + iris_w - 1, y0 + EH - 1, "v")          # 눈동자
    pfill(im, ix + 1, y0 + EH - 3, ix + iris_w - 2, y0 + EH - 1, "C")   # 아래쪽 반사(사이버 색)
    pfill(im, ix + 2, y0 + top + 2, ix + iris_w - 3, y0 + EH - 4, "K")  # 동공
    pfill(im, ix + 1, y0 + top + 1, ix + 2, y0 + top + 2, "W")          # 하이라이트
    out = -1 if side == "L" else EW                                     # 바깥 눈꼬리
    pfill(im, x0 - 1, y0 + top - 1, x0 + EW, y0 + top, "K")             # 윗눈꺼풀(굵게)
    pset(im, x0 + out + (-1 if side == "L" else 1), y0 + top - 2, "K")  # 속눈썹 꼬리
    if mode in ("half", "narrow", "down"):
        pfill(im, x0, y0 + top + 1, x0 + EW - 1, y0 + top + 1, "J")     # 반쯤 감긴 눈꺼풀 그림자
    pfill(im, x0 + 2, y0 + EH, x0 + EW - 3, y0 + EH, "s")               # 아래 눈꺼풀 선


def brow(im, side, mode):
    """눈썹 길이 10. mode: flat, up_inner(슬픔·두려움), down_inner(분노), raised(놀람), one_down(혐오-왼쪽만)"""
    x0 = EYES[side]
    y = EY - 4
    xs = range(x0, x0 + EW)
    for i, x in enumerate(xs):
        t = i if side == "L" else EW - 1 - i   # t: 바깥(0) → 콧쪽
        dy = 0
        if mode == "up_inner":
            dy = -(t // 4)
        elif mode == "down_inner":
            dy = t // 3
        elif mode == "raised":
            dy = -2
        pset(im, x, y + dy, "J")
        pset(im, x, y + dy + 1, "J")


def mouth(im, mode):
    cx, y = 48, 61
    if mode == "flat":
        pfill(im, cx - 2, y, cx + 2, y, "m")
    elif mode == "slight_smile":
        pfill(im, cx - 2, y, cx + 2, y, "m"); pset(im, cx - 3, y - 1, "m"); pset(im, cx + 3, y - 1, "m")
    elif mode == "down":
        pfill(im, cx - 2, y, cx + 2, y, "m"); pset(im, cx - 3, y + 1, "m"); pset(im, cx + 3, y + 1, "m")
    elif mode == "tight":
        pfill(im, cx - 3, y, cx + 3, y, "K")
    elif mode == "small_open":
        pfill(im, cx - 1, y - 1, cx + 1, y + 1, "K"); pset(im, cx, y + 1, "m")
    elif mode == "o":
        pfill(im, cx - 2, y - 2, cx + 2, y + 2, "K"); pfill(im, cx - 1, y - 1, cx + 1, y + 1, "m")
    elif mode == "sneer":
        pfill(im, cx - 2, y, cx + 3, y, "m"); pset(im, cx + 3, y - 1, "m"); pset(im, cx + 4, y - 1, "m")


EXPRESSIONS = {
    # 이름: (왼눈, 오른눈, 왼눈썹, 오른눈썹, 입)
    "neutral":  ("half", "half", "flat", "flat", "flat"),
    "joy":      ("smile", "smile", "flat", "flat", "slight_smile"),
    "sadness":  ("down", "down", "up_inner", "up_inner", "down"),
    "anger":    ("narrow", "narrow", "down_inner", "down_inner", "tight"),
    "fear":     ("wide", "wide", "up_inner", "up_inner", "small_open"),
    "surprise": ("wide", "wide", "raised", "raised", "o"),
    "disgust":  ("narrow", "half", "down_inner", "flat", "sneer"),
}


def portrait(name):
    le, re, lb, rb, mo = EXPRESSIONS[name]
    im = rebuild()
    eye(im, "L", le); eye(im, "R", re)
    brow(im, "L", lb); brow(im, "R", rb)
    mouth(im, mo)
    outline_outside(im, "K")
    return im


def build():
    made = []
    for name in EXPRESSIONS:
        im = portrait(name)
        bad = palette_check(im)
        assert not bad, (name, bad)
        made.append(save(im, f"portraits/hacker_{name}.png",
                         {"status": "시안", "style": "pixel", "size": 96, "expression": name}))
    made.append(contact_sheet(made, 192, "preview/hacker_expressions_sheet.png", cols=4))
    made.append(save(upscale(portrait("neutral"), 4), "preview/hacker_neutral_x4.png", {"status": "미리보기"}))
    return made
