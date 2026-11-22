"""조력자 외형 시안 3개 (W-022) — H1 배달원 계승 / H2 스트리트 러너 / H3 대비 짝.

- D-106(메인 C안·조력자 침대에 앉음·20대 여성·주인공과 비슷한 디자인, 범위는 디자인 시안), D-052, D-070
- 같은 화풍·비례·외곽선 규칙(초상 1px, 게임 속 2px), 따뜻한 색 + 크게 뜬 눈 + 웃는 입, 귀에 통신 이어피스
- 공유 팔레트 + 안마다 4색 이내 추가(아래 EXT). 안티앨리어싱 없음. 정지 시안, Unity 적용 없음.
- 기존 manifest.json 은 건드리지 않는다(artkit.save 대신 Image.save 로 preview/ 에만 저장).
[시안] 사용자 선택 전.
"""
from contextlib import contextmanager

from PIL import Image

from anim import UNIT
from artkit import (EXPORT, PIX, canvas, draw_ellipse, draw_line, draw_poly, outline_outside, pbox,
                    pfill, pset, upscale)
from char_runner128 import frame as runner_frame, outline2
from pixfont import draw_text, text_width
import portrait_hacker as hk

N = 96
BASE = dict(PIX)  # 추가 색을 넣기 전 공유 팔레트(검사 기준)

# 추가 색 [제안] — 안마다 4색 이내
EXT = {
    "A": (190, 128, 72, 255),    # 머리카락 하이라이트(밤색)
    "O": (240, 140, 50, 255),    # 주황
    "o": (176, 86, 34, 255),     # 주황 어두움
    "P": (150, 138, 96, 255),    # 카키(카고 바지)
    "L": (150, 240, 200, 255),   # 연한 민트(재킷 하이라이트)
}
EXT_USE = {"H1": "A", "H2": "AOoP", "H3": "AOoL"}
NAMES = {"H1": "H1 DELIVERY", "H2": "H2 STREET", "H3": "H3 PAIR"}


@contextmanager
def ext_palette():
    PIX.update(EXT)
    try:
        yield
    finally:
        for k in EXT:
            PIX.pop(k, None)


def allowed(d):
    ok = set(BASE.values())
    for k in EXT_USE[d]:
        ok.add(EXT[k])
    return ok


def bad_colors(im, d):
    return {c for c in im.getdata() if c not in allowed(d)}


def used_colors(im):
    return {c for c in im.getdata() if c[3] > 0}


def mirror_x(x):
    return N - 1 - x


# =====================================================================
# 초상 96×96 — 주인공 초상과 같은 틀(눈 상자 위치·얼굴 모양·외곽선 1px)
# =====================================================================
def face_base(im):
    """목·얼굴·귀·코(공통). 주인공 얼굴과 같은 모양."""
    pfill(im, 43, 62, 53, 75, "s"); pfill(im, 43, 62, 48, 71, "S")
    pfill(im, 26, 43, 29, 53, "S"); pfill(im, 67, 43, 70, 53, "s")          # 귀
    draw_poly(im, [(29, 30), (67, 30), (67, 50), (58, 62), (48, 68), (38, 62), (29, 50)], "S")
    draw_poly(im, [(62, 34), (67, 34), (67, 50), (58, 62), (54, 65), (60, 52)], "s")
    pfill(im, 30, 31, 66, 33, "s")
    pset(im, 44, 52, "l"); pset(im, 45, 53, "l")
    pset(im, 48, 55, "s"); pset(im, 49, 56, "s")


def happy_eye(im, side):
    """크게 뜬 눈(웃는 인상): 눈동자 7폭, 하이라이트 2개, 아래쪽 따뜻한 반사."""
    x0, y0 = hk.EYES[side], hk.EY
    top = 1
    pfill(im, x0, y0 + top, x0 + 10, y0 + 10, "W")
    ix = x0 + 2
    pfill(im, ix, y0 + top, ix + 6, y0 + 10, "d")
    pfill(im, ix + 1, y0 + 5, ix + 5, y0 + 10, "D")
    pfill(im, ix + 1, y0 + 9, ix + 5, y0 + 10, "h")
    pfill(im, ix + 2, y0 + top + 2, ix + 4, y0 + 7, "K")
    pfill(im, ix + 1, y0 + top + 1, ix + 2, y0 + top + 2, "W")
    pset(im, ix + 5, y0 + 8, "W")
    pfill(im, x0 - 1, y0 + top - 1, x0 + 11, y0 + top, "K")
    pset(im, x0 - 2 if side == "L" else x0 + 12, y0 + top - 2, "K")
    pfill(im, x0 + 2, y0 + 11, x0 + 8, y0 + 11, "s")


def happy_brow(im, side, col="d"):
    x0 = hk.EYES[side]
    y = hk.EY - 4
    for i in range(hk.EW):
        t = i if side == "L" else hk.EW - 1 - i
        dy = -1 if 3 <= t <= 7 else 0
        pset(im, x0 + i, y + dy, col); pset(im, x0 + i, y + dy + 1, col)


def happy_mouth(im):
    cx, y = 48, 59
    pfill(im, cx - 4, y, cx + 4, y, "K")
    pset(im, cx - 5, y - 1, "K"); pset(im, cx + 5, y - 1, "K")
    pfill(im, cx - 4, y + 1, cx + 4, y + 1, "K"); pfill(im, cx - 3, y + 1, cx + 3, y + 1, "W")
    pfill(im, cx - 3, y + 2, cx + 3, y + 2, "K"); pfill(im, cx - 2, y + 2, cx + 2, y + 2, "m")
    pfill(im, cx - 2, y + 3, cx + 2, y + 3, "K")
    for x in (36, 37, 58, 59):
        pset(im, x, 54, "m")                                                # 볼 홍조


def earpiece(im, flip=False):
    """귀에 작은 통신 이어피스 + 가는 선."""
    def X(x):
        return mirror_x(x) if flip else x
    for x in range(23, 28):
        for y in range(44, 50):
            pset(im, X(x), y, "K")
    for x in range(24, 27):
        for y in range(45, 49):
            pset(im, X(x), y, "3")
    pset(im, X(25), 46, "G"); pset(im, X(25), 47, "g")
    draw_line(im, [(X(24), 50), (X(22), 60), (X(24), 70)], "K", 1)


# ---- H1 ------------------------------------------------------------
def p_body_h1(im):
    draw_poly(im, [(10, 95), (16, 82), (30, 73), (66, 73), (80, 82), (86, 95)], "Y")
    draw_poly(im, [(48, 73), (66, 73), (80, 82), (86, 95), (48, 95)], "y")
    draw_line(im, [(17, 85), (30, 76)], "h", 2)
    pbox(im, 19, 86, 31, 94, "y"); pfill(im, 19, 86, 31, 88, "y")
    draw_poly(im, [(30, 73), (40, 70), (48, 84), (56, 70), (66, 73), (58, 86), (48, 92), (38, 86)], "y")
    pfill(im, 44, 84, 52, 95, "3")
    draw_line(im, [(48, 88), (48, 95)], "K")
    draw_line(im, [(66, 74), (36, 95)], "2", 5)                           # 가방 끈
    draw_line(im, [(66, 74), (36, 95)], "3", 1)
    pfill(im, 52, 82, 56, 86, "h"); pbox(im, 52, 82, 56, 86, "K")          # 버클


def p_mid_h1(im):
    draw_poly(im, [(66, 38), (72, 42), (77, 56), (79, 72), (77, 88), (71, 94), (70, 80), (69, 62), (66, 50)], "D")
    draw_poly(im, [(72, 46), (76, 58), (77, 74), (74, 90), (71, 94), (70, 80), (74, 62)], "d")
    draw_line(im, [(71, 46), (73, 62), (74, 78)], "A")
    pfill(im, 66, 38, 70, 42, "R")


def p_front_h1(im):
    draw_poly(im, [(28, 30), (31, 30), (31, 44), (28, 44)], "D")                       # 옆머리 끝
    draw_poly(im, [(65, 30), (68, 30), (68, 44), (65, 44)], "D")
    draw_poly(im, [(29, 30), (67, 30), (67, 34), (61, 38), (56, 34), (50, 39), (45, 34), (39, 38), (34, 34), (30, 38)], "D")
    draw_poly(im, [(25, 34), (26, 20), (34, 10), (48, 6), (62, 10), (70, 20), (71, 34), (60, 30), (36, 30)], "R")
    draw_poly(im, [(58, 9), (62, 10), (70, 20), (71, 34), (63, 31), (64, 20)], "r")
    pfill(im, 44, 14, 51, 19, "W"); pfill(im, 47, 15, 48, 18, "R")
    draw_line(im, [(32, 14), (28, 24)], "W")
    draw_poly(im, [(24, 28), (72, 28), (71, 33), (62, 35), (48, 36), (34, 35), (25, 33)], "r")
    pfill(im, 30, 28, 66, 28, "R")
    for x, y in ((36, 36), (44, 36), (52, 35)):
        pset(im, x, y, "A")


# ---- H2 ------------------------------------------------------------
def p_back_h2(im):
    draw_poly(im, [(21, 34), (26, 14), (40, 6), (56, 6), (70, 14), (75, 34), (76, 50), (80, 56), (66, 56), (30, 56), (20, 60), (14, 58), (18, 50)], "D")
    draw_poly(im, [(21, 52), (30, 56), (20, 61), (13, 58)], "O")                       # 바깥으로 뻗친 끝(주황)
    draw_poly(im, [(76, 46), (82, 56), (70, 55), (74, 50)], "O")


def p_body_h2(im):
    draw_poly(im, [(10, 95), (16, 82), (30, 73), (66, 73), (80, 82), (86, 95)], "O")
    draw_poly(im, [(48, 73), (66, 73), (80, 82), (86, 95), (48, 95)], "o")
    draw_poly(im, [(18, 84), (30, 74), (66, 74), (78, 84), (75, 88), (62, 80), (34, 80), (21, 88)], "Y")
    draw_poly(im, [(34, 72), (40, 64), (56, 64), (62, 72), (63, 84), (48, 90), (33, 84)], "O")
    draw_poly(im, [(48, 64), (56, 64), (62, 72), (63, 84), (48, 90)], "o")
    pfill(im, 43, 66, 53, 74, "s")                                                # 목(높은 칼라 안쪽은 얼굴 단계에서 덮음)
    draw_line(im, [(48, 76), (48, 95)], "K")
    pset(im, 48, 78, "h"); pset(im, 48, 79, "h")


def p_front_h2(im):
    draw_poly(im, [(23, 28), (32, 32), (31, 50), (26, 54), (21, 50)], "D")
    draw_poly(im, [(73, 28), (64, 32), (65, 46), (70, 50), (75, 46)], "D")
    draw_poly(im, [(21, 50), (26, 54), (31, 50), (30, 57), (24, 60), (18, 54)], "O")
    draw_poly(im, [(75, 46), (70, 50), (65, 46), (66, 52), (72, 54), (78, 50)], "O")
    draw_poly(im, [(22, 30), (27, 14), (40, 8), (56, 8), (69, 14), (74, 30), (72, 38), (66, 35), (60, 40), (54, 34),
                   (48, 39), (41, 34), (35, 40), (29, 35), (24, 40)], "D")
    for pts in ([(34, 12), (30, 24)], [(48, 10), (47, 24)], [(60, 12), (64, 24)]):
        draw_line(im, pts, "A")
    for x, y in ((24, 40), (35, 40), (48, 39), (60, 40), (72, 38)):
        pset(im, x, y, "O"); pset(im, x, y - 1, "O")


# ---- H3 ------------------------------------------------------------
def p_back_h3(im):
    draw_poly(im, [(21, 34), (27, 14), (40, 6), (56, 6), (70, 14), (75, 34), (74, 60), (66, 66), (30, 66), (22, 60)], "D")


def p_body_h3(im):
    draw_poly(im, [(4, 95), (10, 82), (28, 73), (68, 73), (86, 82), (92, 95)], "G")
    draw_poly(im, [(48, 73), (68, 73), (86, 82), (92, 95), (48, 95)], "g")
    draw_line(im, [(12, 86), (28, 75)], "L", 2)
    draw_poly(im, [(30, 73), (38, 69), (48, 82), (58, 69), (66, 73), (58, 88), (48, 92), (38, 88)], "g")
    draw_poly(im, [(38, 73), (44, 72), (48, 82), (52, 72), (58, 73), (54, 86), (48, 90), (42, 86)], "W")
    draw_line(im, [(44, 84), (43, 95)], "W"); draw_line(im, [(52, 84), (53, 95)], "W")        # 후드 끈
    draw_line(im, [(48, 90), (48, 95)], "K")


def p_mid_h3(im):
    for k in range(7):                                                              # 한쪽으로 땋은 머리
        cy = 54 + k * 6
        cx = 28 - k // 2
        w = 5 - k // 3
        draw_ellipse(im, (cx - w, cy - 3, cx + w, cy + 3), "D")
        draw_line(im, [(cx - w + 1, cy + 2), (cx + w - 1, cy - 2)], "d")
    pfill(im, 22, 92, 26, 95, "Y")


def p_front_h3(im):
    draw_poly(im, [(26, 30), (31, 30), (31, 46), (28, 46)], "D")
    draw_poly(im, [(65, 30), (70, 30), (68, 46), (65, 46)], "D")
    # 고글(이마 위) — 끈 + 렌즈
    pfill(im, 25, 26, 71, 29, "y"); pfill(im, 25, 25, 71, 25, "Y")
    for x0 in (31, 51):
        draw_ellipse(im, (x0, 20, x0 + 13, 33), "K")
        draw_ellipse(im, (x0 + 2, 22, x0 + 11, 31), "C")
        pfill(im, x0 + 3, 23, x0 + 5, 24, "W"); pset(im, x0 + 8, 29, "W")
    pfill(im, 44, 25, 51, 29, "K")
    # 거꾸로 쓴 캡(앞면: 무늬 없는 앞 패널)
    draw_poly(im, [(24, 22), (26, 13), (36, 6), (60, 6), (70, 13), (72, 22), (66, 20), (30, 20)], "O")
    draw_poly(im, [(60, 6), (70, 13), (72, 22), (66, 20), (62, 14)], "o")
    pfill(im, 46, 3, 50, 5, "o")
    draw_line(im, [(48, 6), (47, 19)], "o")
    draw_line(im, [(37, 8), (34, 19)], "o")
    draw_line(im, [(59, 8), (62, 19)], "o")
    pfill(im, 30, 20, 66, 20, "o")
    for x, y in ((31, 34), (34, 35), (61, 35), (64, 34)):
        pset(im, x, y, "D")


PORTRAIT_PARTS = {
    "H1": (None, p_body_h1, p_mid_h1, p_front_h1),
    "H2": (p_back_h2, p_body_h2, None, p_front_h2),
    "H3": (p_back_h3, p_body_h3, p_mid_h3, p_front_h3),
}


def portrait(d):
    back, body, mid, front = PORTRAIT_PARTS[d]
    im = canvas(N, N)
    if back:
        back(im)
    body(im)
    if mid:
        mid(im)
    face_base(im)
    front(im)
    happy_eye(im, "L"); happy_eye(im, "R")
    happy_brow(im, "L"); happy_brow(im, "R")
    happy_mouth(im)
    earpiece(im, flip=(d == "H3"))
    outline_outside(im, "K")
    return im


# =====================================================================
# 게임 속 크기 서 있는 전신 128×128 — runner 기다림 프레임과 같은 바닥선(124)·몸 배치, 오른쪽을 봄
# =====================================================================
CX, GROUND = 62, 124
HIP = GROUND - 40
TOP = HIP - 34
HY = TOP - 20

STAND_COL = {
    # 바지, 바지 어둠, 신발, 밑창
    "H1": ("2", "4", "K", "3"),
    "H2": ("P", "y", "W", "O"),
    "H3": ("2", "4", "K", "W"),
}


def s_leg(im, d, hip, foot, front):
    pa, pd, shoe, sole = STAND_COL[d]
    col, dark = (pa, pd) if front else (pd, "K")
    knee = (foot[0], hip[1] + 22)
    draw_line(im, [hip, knee], col, 10); draw_line(im, [knee, foot], col, 9)
    draw_line(im, [(hip[0] - 3, hip[1]), (knee[0] - 3, knee[1])], dark, 3)
    fx, fy = foot
    draw_poly(im, [(fx - 7, fy - 7), (fx + 11, fy - 7), (fx + 14, fy), (fx - 7, fy)], shoe)
    pfill(im, fx - 6, fy - 2, fx + 13, fy, sole)
    if d == "H2" and front:
        pfill(im, fx + 4, fy - 6, fx + 10, fy - 4, "O")


def s_torso(im, d):
    top, hip = TOP, HIP
    if d == "H1":
        draw_poly(im, [(CX - 17, top), (CX + 15, top), (CX + 17, hip + 2), (CX - 18, hip + 2)], "Y")
        draw_poly(im, [(CX - 17, top), (CX - 8, top), (CX - 10, hip + 2), (CX - 18, hip + 2)], "y")
        draw_line(im, [(CX + 8, top + 4), (CX + 11, hip - 4)], "h", 3)
        pfill(im, CX - 18, hip - 4, CX + 17, hip + 2, "y")
        draw_line(im, [(CX + 2, top + 2), (CX + 3, hip)], "y", 2)
        draw_line(im, [(CX + 10, top), (CX - 14, hip - 6)], "2", 4)                       # 가방 끈
        draw_line(im, [(CX + 10, top), (CX - 14, hip - 6)], "3", 1)
        pfill(im, CX - 4, top + 14, CX + 3, top + 17, "h")
    elif d == "H2":
        draw_poly(im, [(CX - 17, top), (CX + 15, top), (CX + 16, hip - 8), (CX - 17, hip - 8)], "O")
        draw_poly(im, [(CX - 17, top), (CX - 8, top), (CX - 10, hip - 8), (CX - 17, hip - 8)], "o")
        draw_line(im, [(CX + 9, top + 3), (CX + 11, hip - 10)], "Y", 2)                   # 노랑 줄
        pfill(im, CX - 17, hip - 10, CX + 16, hip - 8, "o")
        draw_line(im, [(CX + 2, top + 2), (CX + 3, hip - 9)], "o", 2)
        pfill(im, CX - 17, hip - 7, CX + 16, hip + 4, "P")                                 # 카고 바지 허리
        pfill(im, CX - 17, hip - 7, CX - 9, hip + 4, "y")
        pfill(im, CX - 17, hip - 3, CX + 16, hip - 2, "2")                                 # 허리 가방 끈
        pfill(im, CX + 3, hip - 4, CX + 17, hip + 6, "3"); pbox(im, CX + 3, hip - 4, CX + 17, hip + 6, "K")
        pfill(im, CX + 5, hip - 2, CX + 15, hip - 2, "Y")
    else:
        draw_poly(im, [(CX - 22, top + 2), (CX + 19, top + 2), (CX + 21, hip + 8), (CX - 23, hip + 8)], "G")
        draw_poly(im, [(CX - 22, top + 2), (CX - 11, top + 2), (CX - 13, hip + 8), (CX - 23, hip + 8)], "g")
        draw_line(im, [(CX + 11, top + 6), (CX + 14, hip + 2)], "L", 3)
        pfill(im, CX - 23, hip + 3, CX + 21, hip + 8, "g")
        draw_line(im, [(CX + 3, top + 4), (CX + 4, hip + 6)], "g", 2)
        draw_poly(im, [(CX - 8, top - 4), (CX + 6, top - 4), (CX + 10, top + 4), (CX - 12, top + 4)], "g")  # 후드 깃
        pfill(im, CX - 2, top - 2, CX + 4, top + 1, "W")


def s_arm(im, d):
    top = TOP
    ry = top + 22
    col = {"H1": "Y", "H2": "O", "H3": "G"}[d]
    w = 10 if d == "H3" else 8
    draw_line(im, [(CX + 8, top + 6), (CX + 20, top + 16), (CX + 24, ry)], col, w)
    if d == "H2":
        draw_line(im, [(CX + 8, top + 6), (CX + 20, top + 16)], "Y", 2)
    draw_ellipse(im, (CX + 20, ry - 6, CX + 30, ry + 4), "S")
    cuff = {"H1": "y", "H2": "Y", "H3": "g"}[d]
    pfill(im, CX + 19, ry - 8, CX + 28, ry - 6, cuff)


def s_head(im, d):
    cx, hy = CX, HY
    pfill(im, cx - 4, TOP - 6, cx + 6, TOP + 2, "s")                                       # 목
    # 뒤쪽 머리(얼굴 뒤)
    if d == "H1":                                                                          # 포니테일은 모자 뒤 구멍으로
        draw_poly(im, [(cx - 17, hy - 12), (cx - 24, hy - 6), (cx - 32, hy + 6), (cx - 36, hy + 24), (cx - 31, hy + 32),
                       (cx - 28, hy + 16), (cx - 22, hy + 4), (cx - 16, hy)], "D")
        draw_line(im, [(cx - 27, hy + 4), (cx - 31, hy + 24)], "A")
        draw_line(im, [(cx - 32, hy + 12), (cx - 33, hy + 28)], "d", 2)
        pfill(im, cx - 22, hy - 10, cx - 18, hy - 6, "Y")
        draw_poly(im, [(cx - 18, hy - 4), (cx - 8, hy - 6), (cx - 10, hy + 8), (cx - 18, hy + 6)], "D")
    elif d == "H2":
        draw_poly(im, [(cx - 21, hy + 14), (cx - 21, hy - 8), (cx - 14, hy - 21), (cx - 2, hy - 26), (cx + 10, hy - 23),
                       (cx + 18, hy - 12), (cx - 4, hy - 4), (cx - 4, hy + 14)], "D")
        pfill(im, cx - 21, hy + 9, cx - 4, hy + 14, "O")
    else:
        draw_poly(im, [(cx - 19, hy - 8), (cx - 16, hy - 20), (cx + 10, hy - 22), (cx + 18, hy - 10),
                       (cx + 2, hy - 4), (cx - 6, hy + 8), (cx - 18, hy + 8)], "D")
        for k in range(7):                                                                 # 등 뒤로 늘어진 땋은 머리
            cyy = hy + 10 + k * 6
            cxx = cx - 18 - k // 2
            w = 5 - k // 3
            draw_ellipse(im, (cxx - w, cyy - 3, cxx + w, cyy + 3), "D")
            draw_line(im, [(cxx - w + 1, cyy + 2), (cxx + w - 1, cyy - 2)], "d")
        pfill(im, cx - 22, hy + 52, cx - 17, hy + 55, "Y")
    # 얼굴
    draw_ellipse(im, (cx - 16, hy - 16, cx + 18, hy + 16), "S")
    draw_ellipse(im, (cx - 16, hy - 10, cx - 2, hy + 14), "s")
    pset(im, cx + 19, hy + 3, "S"); pset(im, cx + 18, hy + 4, "s")                         # 코끝
    # 귀 + 이어피스
    pfill(im, cx - 4, hy - 1, cx + 1, hy + 5, "S")
    pfill(im, cx - 6, hy - 2, cx - 3, hy + 6, "K"); pfill(im, cx - 5, hy - 1, cx - 4, hy + 5, "3")
    pset(im, cx - 5, hy, "G")
    draw_line(im, [(cx - 5, hy + 6), (cx - 6, hy + 14)], "K")
    # 눈(크게 뜸, 오른쪽을 봄)
    ex, ey = cx + 6, hy - 4
    pfill(im, ex, ey, ex + 7, ey + 8, "W")
    pfill(im, ex + 2, ey, ex + 7, ey + 8, "d")
    pfill(im, ex + 2, ey + 4, ex + 7, ey + 8, "D"); pfill(im, ex + 3, ey + 7, ex + 6, ey + 8, "h")
    pfill(im, ex + 3, ey + 2, ex + 5, ey + 6, "K")
    pfill(im, ex + 2, ey + 1, ex + 3, ey + 2, "W"); pset(im, ex + 6, ey + 6, "W")
    pfill(im, ex - 1, ey - 1, ex + 8, ey, "K"); pset(im, ex - 2, ey - 2, "K")
    pfill(im, ex + 1, ey + 9, ex + 6, ey + 9, "s")
    draw_line(im, [(ex - 1, ey - 5), (ex + 4, ey - 6), (ex + 9, ey - 4)], "d", 1)         # 눈썹
    # 입(작게 열린 웃음) + 홍조
    pfill(im, cx + 9, hy + 9, cx + 16, hy + 9, "K"); pset(im, cx + 17, hy + 8, "K")
    pfill(im, cx + 10, hy + 10, cx + 15, hy + 10, "W"); pfill(im, cx + 11, hy + 11, cx + 14, hy + 11, "K")
    pfill(im, cx + 7, hy + 6, cx + 9, hy + 6, "m")
    # 앞머리·모자
    if d == "H1":
        draw_poly(im, [(cx + 2, hy - 9), (cx + 18, hy - 9), (cx + 17, hy - 3), (cx + 13, hy - 6), (cx + 9, hy - 2),
                       (cx + 6, hy - 6), (cx + 3, hy - 3)], "D")
        draw_poly(im, [(cx - 19, hy - 8), (cx - 14, hy - 22), (cx + 10, hy - 24), (cx + 19, hy - 10)], "R")
        draw_poly(im, [(cx - 19, hy - 8), (cx - 14, hy - 22), (cx - 6, hy - 23), (cx - 9, hy - 8)], "r")
        pfill(im, cx + 12, hy - 12, cx + 34, hy - 8, "r")
        pfill(im, cx - 2, hy - 19, cx + 5, hy - 13, "W"); pset(im, cx + 1, hy - 16, "R")
    elif d == "H2":
        draw_poly(im, [(cx - 20, hy - 6), (cx - 14, hy - 20), (cx - 2, hy - 25), (cx + 10, hy - 22), (cx + 18, hy - 12),
                       (cx + 19, hy - 7), (cx + 13, hy - 10), (cx + 8, hy - 6), (cx + 4, hy - 11), (cx - 1, hy - 7),
                       (cx - 6, hy - 3), (cx - 9, hy + 4), (cx - 20, hy + 4)], "D")
        draw_line(im, [(cx - 12, hy - 18), (cx, hy - 22), (cx + 10, hy - 18)], "A")
        for x, y in ((cx + 19, hy - 8), (cx + 13, hy - 10), (cx + 8, hy - 6), (cx + 4, hy - 11)):
            pset(im, x, y, "O"); pset(im, x - 1, y, "O")
    else:
        draw_poly(im, [(cx - 19, hy - 8), (cx - 14, hy - 22), (cx + 10, hy - 24), (cx + 19, hy - 10), (cx + 16, hy - 8),
                       (cx + 2, hy - 10), (cx - 12, hy - 8)], "O")
        draw_poly(im, [(cx - 19, hy - 8), (cx - 14, hy - 22), (cx - 6, hy - 23), (cx - 9, hy - 8)], "o")
        pfill(im, cx - 36, hy - 12, cx - 14, hy - 8, "o")                                   # 챙이 뒤(왼쪽)로
        pfill(im, cx - 36, hy - 12, cx - 14, hy - 12, "O")
        draw_poly(im, [(cx + 3, hy - 9), (cx + 17, hy - 9), (cx + 17, hy - 4), (cx + 12, hy - 7), (cx + 8, hy - 4),
                       (cx + 5, hy - 6)], "D")                                              # 이마 앞머리
        draw_line(im, [(cx + 3, hy - 9), (cx - 16, hy - 9)], "y", 2)                        # 고글 끈
        draw_ellipse(im, (cx + 4, hy - 16, cx + 17, hy - 6), "K")
        draw_ellipse(im, (cx + 6, hy - 14, cx + 15, hy - 8), "C")
        pfill(im, cx + 7, hy - 13, cx + 9, hy - 12, "W")


def stand(d):
    im = canvas(UNIT, UNIT)
    s_leg(im, d, (CX - 7, HIP), (CX - 8, GROUND), False)
    s_leg(im, d, (CX + 7, HIP), (CX + 8, GROUND), True)
    s_torso(im, d)
    s_arm(im, d)
    s_head(im, d)
    outline2(im)
    return im


# =====================================================================
# 메인 화면 크기 침대에 앉은 자세 (시안 C 합성)
# =====================================================================
W, H = 640, 360
LCX = 212          # 앉은 자리 가운데(침대 머리 쪽 베개 옆)
SEAT = 254         # 엉덩이 높이(매트리스 앞 가장자리)
FLOOR = 295


LEG_SEAT = SEAT - 12   # 다리는 매트리스 윗면 높이에서 시작(몸통·머리는 SEAT 기준으로 그린 뒤 12px 올림)


def l_leg(im, d, near):
    """걸터앉은 다리: 허벅지는 오른쪽 앞으로 뻗고(매트리스 가장자리), 정강이는 아래로 늘어뜨림."""
    pa, pd, shoe, sole = STAND_COL[d]
    st = LEG_SEAT
    cx = LCX
    if near:
        col, t0, k0, k1 = pa, st - 2, cx + 19, cx + 31
        pfill(im, cx - 14, t0, cx + 31, t0 + 11, col)
        pfill(im, cx - 14, t0, cx + 31, t0 + 1, "3" if d == "H1" else col)
        pfill(im, cx - 14, t0 + 9, cx + 31, t0 + 11, pd)
        pbox(im, cx - 14, t0, cx + 31, t0 + 11, "K")
        sy1 = FLOOR - 8
        pfill(im, k0, t0 + 11, k1, sy1, col); pfill(im, k0, t0 + 11, k0 + 3, sy1, pd)
        pbox(im, k0 - 1, t0 + 11, k1 + 1, sy1, "K")
        fx0, fx1, fy = k0 - 2, k1 + 6, FLOOR
        if d == "H2":
            pfill(im, cx + 6, t0 + 2, cx + 15, t0 + 7, "y"); pbox(im, cx + 6, t0 + 2, cx + 15, t0 + 7, "K")
    else:
        col, t0, k0, k1 = pd, st - 5, cx + 27, cx + 39
        pfill(im, cx - 4, t0, cx + 39, t0 + 10, col)
        pbox(im, cx - 4, t0, cx + 39, t0 + 10, "K")
        sy1 = FLOOR - 10
        pfill(im, k0, t0 + 10, k1, sy1, col)
        pbox(im, k0 - 1, t0 + 10, k1 + 1, sy1, "K")
        fx0, fx1, fy = k0 - 2, k1 + 6, FLOOR - 3
    pfill(im, fx0, fy - 8, fx1, fy, shoe)
    pfill(im, fx0, fy - 2, fx1 + 1, fy, sole)
    pbox(im, fx0, fy - 8, fx1, fy, "K")
    if d == "H2" and near:
        pfill(im, fx0 + 4, fy - 6, fx0 + 9, fy - 4, "O")


def l_torso(im, d):
    cx = LCX
    if d == "H1":
        draw_poly(im, [(cx - 21, 219), (cx - 12, 214), (cx + 12, 214), (cx + 21, 219), (cx + 19, SEAT + 1), (cx - 19, SEAT + 1)], "Y")
        draw_poly(im, [(cx - 21, 219), (cx - 12, 214), (cx - 8, 214), (cx - 10, SEAT + 1), (cx - 19, SEAT + 1)], "y")
        draw_line(im, [(cx + 15, 218), (cx + 14, SEAT - 2)], "h", 2)
        pfill(im, cx - 19, SEAT - 3, cx + 19, SEAT + 1, "y")
        draw_line(im, [(cx + 1, 220), (cx + 1, SEAT - 3)], "y")
        draw_line(im, [(cx + 10, 215), (cx - 12, SEAT - 4)], "2", 3)
        draw_line(im, [(cx + 10, 215), (cx - 12, SEAT - 4)], "3", 1)
        draw_poly(im, [(cx - 9, 214), (cx - 4, 214), (cx, 222), (cx + 4, 214), (cx + 9, 214), (cx + 4, 226), (cx - 4, 226)], "y")
    elif d == "H2":
        draw_poly(im, [(cx - 20, 219), (cx - 12, 214), (cx + 12, 214), (cx + 20, 219), (cx + 18, SEAT - 6), (cx - 18, SEAT - 6)], "O")
        draw_poly(im, [(cx - 20, 219), (cx - 12, 214), (cx - 8, 214), (cx - 10, SEAT - 6), (cx - 18, SEAT - 6)], "o")
        draw_line(im, [(cx + 14, 218), (cx + 14, SEAT - 8)], "Y", 2)
        pfill(im, cx - 18, SEAT - 8, cx + 18, SEAT - 6, "o")
        pfill(im, cx - 18, SEAT - 5, cx + 18, SEAT + 1, "P")                               # 카고 허리
        pfill(im, cx - 18, SEAT - 5, cx - 10, SEAT + 1, "y")
        pfill(im, cx - 18, SEAT - 4, cx + 18, SEAT - 3, "2")                               # 허리 가방 끈
        pfill(im, cx + 6, SEAT - 6, cx + 17, SEAT + 2, "3"); pbox(im, cx + 6, SEAT - 6, cx + 17, SEAT + 2, "K")
        draw_poly(im, [(cx - 8, 214), (cx + 8, 214), (cx + 9, 224), (cx, 227), (cx - 9, 224)], "o")   # 높은 칼라
        draw_line(im, [(cx, 224), (cx, SEAT - 8)], "K")
    else:
        draw_poly(im, [(cx - 25, 220), (cx - 14, 214), (cx + 14, 214), (cx + 25, 220), (cx + 23, SEAT + 5), (cx - 23, SEAT + 5)], "G")
        draw_poly(im, [(cx - 25, 220), (cx - 14, 214), (cx - 9, 214), (cx - 11, SEAT + 5), (cx - 23, SEAT + 5)], "g")
        draw_line(im, [(cx + 18, 218), (cx + 17, SEAT + 2)], "L", 2)
        pfill(im, cx - 23, SEAT + 1, cx + 23, SEAT + 5, "g")
        draw_line(im, [(cx + 1, 222), (cx + 1, SEAT)], "g")
        draw_poly(im, [(cx - 10, 213), (cx + 10, 213), (cx + 12, 220), (cx - 12, 220)], "g")
        pfill(im, cx - 4, 216, cx + 4, 224, "W")


def l_tablet_arms(im, d):
    cx = LCX
    sl = {"H1": "Y", "H2": "O", "H3": "G"}[d]
    sd = {"H1": "y", "H2": "o", "H3": "g"}[d]
    w = 8 if d == "H3" else 6
    for s in (-1, 1):
        draw_line(im, [(cx + s * 20, 222), (cx + s * 22, 236), (cx + s * 13, 242)], sl if s > 0 else sd, w)
    pfill(im, cx - 12, 230, cx + 12, 247, "K"); pfill(im, cx - 11, 231, cx + 11, 246, "4")        # 태블릿 뒷면
    pfill(im, cx - 11, 231, cx + 11, 232, "2"); pset(im, cx - 8, 234, "K"); pset(im, cx - 8, 235, "3")
    pfill(im, cx - 3, 238, cx + 3, 240, "3")
    for s in (-1, 1):
        draw_ellipse(im, (cx + s * 13 - 4, 237, cx + s * 13 + 4, 245), "S")
    pfill(im, cx - 11, 229, cx + 11, 229, "C")                                                     # 화면 빛 새어 나옴


def l_head(im, d):
    cx = LCX
    # 뒷머리
    if d == "H1":
        draw_poly(im, [(cx - 12, 186), (cx - 18, 194), (cx - 23, 208), (cx - 23, 226), (cx - 19, 232), (cx - 17, 216), (cx - 13, 200)], "D")
        draw_line(im, [(cx - 18, 196), (cx - 20, 214)], "A")
        pfill(im, cx - 16, 188, cx - 12, 191, "Y")
        pfill(im, cx - 14, 186, cx + 14, 206, "D")
    elif d == "H2":
        draw_poly(im, [(cx - 17, 184), (cx - 12, 175), (cx, 171), (cx + 12, 175), (cx + 17, 184), (cx + 17, 208),
                       (cx + 13, 213), (cx - 13, 213), (cx - 17, 208)], "D")
        pfill(im, cx - 17, 207, cx + 17, 213, "O")
    else:
        draw_poly(im, [(cx - 16, 186), (cx - 12, 175), (cx, 171), (cx + 12, 175), (cx + 16, 186), (cx + 15, 208), (cx - 15, 208)], "D")
    # 목
    pfill(im, cx - 4, 208, cx + 4, 216, "s")
    # 얼굴
    draw_poly(im, [(cx - 11, 189), (cx + 11, 189), (cx + 11, 201), (cx + 7, 209), (cx, 212), (cx - 7, 209), (cx - 11, 201)], "S")
    draw_poly(im, [(cx + 8, 190), (cx + 11, 190), (cx + 11, 201), (cx + 7, 209), (cx + 9, 200)], "s")
    pfill(im, cx - 13, 195, cx - 12, 200, "S"); pfill(im, cx + 12, 195, cx + 13, 200, "s")        # 귀
    # 눈(아래를 보며 웃음: 눈동자가 아래쪽)
    for s in (-1, 1):
        ex = cx + s * 6 - 2
        pfill(im, ex, 196, ex + 4, 200, "W")
        pfill(im, ex + 1, 196, ex + 3, 200, "d"); pfill(im, ex + 1, 198, ex + 3, 200, "K")
        pset(im, ex + 1, 197, "W")
        pfill(im, ex - 1, 195, ex + 5, 195, "K")
        pfill(im, ex, 192 + 0, ex + 4, 192, "d")                                                    # 눈썹
    # 입(열린 웃음) + 홍조 + 코
    pfill(im, cx - 3, 204, cx + 3, 204, "K"); pset(im, cx - 4, 203, "K"); pset(im, cx + 4, 203, "K")
    pfill(im, cx - 2, 205, cx + 2, 205, "W"); pfill(im, cx - 1, 206, cx + 1, 206, "K")
    pfill(im, cx - 9, 202, cx - 8, 202, "m"); pfill(im, cx + 8, 202, cx + 9, 202, "m")
    pset(im, cx, 201, "s")
    # 앞머리·모자
    if d == "H1":
        pfill(im, cx - 12, 190, cx - 10, 198, "D"); pfill(im, cx + 10, 190, cx + 12, 198, "D")
        draw_poly(im, [(cx - 11, 189), (cx + 11, 189), (cx + 10, 194), (cx + 6, 192), (cx + 2, 195), (cx - 2, 192), (cx - 6, 195), (cx - 10, 192)], "D")
        draw_poly(im, [(cx - 15, 190), (cx - 13, 178), (cx - 5, 172), (cx + 5, 172), (cx + 13, 178), (cx + 15, 190)], "R")
        draw_poly(im, [(cx + 7, 173), (cx + 13, 178), (cx + 15, 190), (cx + 10, 189)], "r")
        pfill(im, cx - 3, 178, cx + 3, 181, "W")
        draw_poly(im, [(cx - 16, 186), (cx + 16, 186), (cx + 14, 191), (cx + 6, 193), (cx - 6, 193), (cx - 14, 191)], "r")
        pfill(im, cx - 15, 186, cx + 15, 186, "R")
    elif d == "H2":
        draw_poly(im, [(cx - 16, 184), (cx - 11, 176), (cx, 172), (cx + 11, 176), (cx + 16, 184), (cx + 11, 192), (cx + 7, 189),
                       (cx + 3, 193), (cx - 1, 189), (cx - 5, 193), (cx - 9, 189), (cx - 13, 193)], "D")
        for x in (cx - 13, cx - 5, cx + 3, cx + 11):
            pset(im, x, 193 if x != cx + 11 else 192, "O")
        draw_line(im, [(cx - 9, 176), (cx, 173), (cx + 9, 176)], "A")
        draw_poly(im, [(cx - 15, 188), (cx - 11, 190), (cx - 11, 206), (cx - 14, 210), (cx - 17, 204)], "D")
        draw_poly(im, [(cx + 15, 188), (cx + 11, 190), (cx + 11, 206), (cx + 14, 210), (cx + 17, 204)], "D")
        pfill(im, cx - 16, 207, cx - 12, 210, "O"); pfill(im, cx + 12, 207, cx + 16, 210, "O")
    else:
        pfill(im, cx - 12, 192, cx - 10, 200, "D"); pfill(im, cx + 10, 192, cx + 12, 200, "D")
        draw_poly(im, [(cx - 16, 188), (cx - 13, 177), (cx - 5, 171), (cx + 5, 171), (cx + 13, 177), (cx + 16, 188), (cx + 11, 186), (cx - 11, 186)], "O")
        draw_poly(im, [(cx + 6, 172), (cx + 13, 177), (cx + 16, 188), (cx + 11, 186)], "o")
        pfill(im, cx - 1, 168, cx + 1, 170, "o"); pfill(im, cx, 172, cx, 185, "o")
        pfill(im, cx - 11, 186, cx + 11, 186, "o")
        pfill(im, cx - 15, 187, cx + 15, 189, "y")                                                   # 고글 끈
        for s in (-1, 1):
            draw_ellipse(im, (cx + s * 7 - 5, 184, cx + s * 7 + 5, 194), "K")
            draw_ellipse(im, (cx + s * 7 - 3, 186, cx + s * 7 + 3, 192), "C")
            pset(im, cx + s * 7 - 2, 187, "W")
    # 이어피스(왼쪽 귀)
    pfill(im, cx - 15, 195, cx - 13, 200, "K"); pset(im, cx - 14, 197, "G")
    pfill(im, cx - 14, 196, cx - 14, 199, "3"); pset(im, cx - 14, 197, "G")
    draw_line(im, [(cx - 15, 201), (cx - 16, 212)], "K")


def l_braid(im, d):
    if d != "H3":
        return
    for k in range(7):
        cyy = 214 + k * 5
        cxx = LCX - 22 - k // 3
        w = 4 - k // 3
        draw_ellipse(im, (cxx - w, cyy - 3, cxx + w, cyy + 3), "D")
        draw_line(im, [(cxx - w + 1, cyy + 2), (cxx + w - 1, cyy - 2)], "d")
    pfill(im, LCX - 25, 246, LCX - 21, 249, "Y")


def lobby_figure(d):
    lay = canvas(W, H)
    l_leg(lay, d, near=False)
    body = canvas(W, H)
    l_torso(body, d)
    l_head(body, d)
    l_tablet_arms(body, d)
    l_braid(body, d)
    lay.alpha_composite(body.crop((0, 12, W, H)), (0, 0))                                 # 몸통·머리를 12px 올려 매트리스 위에 앉힘
    outline_outside(lay, "K")
    l_leg(lay, d, near=True)
    outline_outside(lay, "K")
    # 오른쪽(모니터 쪽) 림 라이트: 바깥선 안쪽 한 줄을 청록으로, 한 줄 걸러
    px = lay.load()
    K = PIX["K"]
    for y in range(H):
        for x in range(1, W - 1):
            if px[x, y] == K and px[x - 1, y][3] and px[x - 1, y] != K and px[x + 1, y][3] == 0 and y % 2 == 0:
                px[x - 1, y] = PIX["c"]
    return lay


def shadow(scene):
    """발밑 접촉 그림자(디더)."""
    for y in range(FLOOR + 1, FLOOR + 5):
        for x in range(LCX - 22, LCX + 24):
            if (x + y) % 2 == 0:
                scene.putpixel((x, y), PIX["a"])


def lobby(d, base):
    scene = base.copy()
    shadow(scene)
    scene.alpha_composite(lobby_figure(d))
    return scene


# =====================================================================
# 비교판 · 빌드
# =====================================================================
def label(im, x, y, text, ch="W"):
    draw_text(im, x, y, text, ch)


def silhouette(im):
    return {(x, y) for y in range(im.height) for x in range(im.width) if im.getpixel((x, y))[3] > 0}


def overlap(a, b):
    sa, sb = silhouette(a), silhouette(b)
    return len(sa & sb) / max(1, len(sa | sb))


def build():
    out = EXPORT / "preview"
    out.mkdir(parents=True, exist_ok=True)
    made = []
    base = Image.open(out / "main_lobby_C.png").convert("RGBA")
    base_all = {k: v for k, v in PIX.items()}
    with ext_palette():
        hk_portrait = hk.portrait("neutral")
        runner = runner_frame("wait", 0)
        res = {}
        for d in ("H1", "H2", "H3"):
            p, s, lb = portrait(d), stand(d), lobby(d, base)
            for tag, im in (("portrait", p), ("stand", s), ("lobby", lb)):
                bad = bad_colors(im, d)
                assert not bad, (d, tag, bad)
            res[d] = (p, s, lb)
            print(f"  [{d}] 사용 색 수 초상 {len(used_colors(p))} / 전신 {len(used_colors(s))} / 침대 합성 {len(used_colors(lb))}"
                  f" (추가 색 {len(EXT_USE[d])}: {EXT_USE[d]})")
            hd = lambda im: im.crop((0, 0, 96, 72))
            print(f"       실루엣 겹침(IoU) 초상 전체 {overlap(p, hk_portrait):.2f} · 머리부분(y<72) {overlap(hd(p), hd(hk_portrait)):.2f}"
                  f" / 전신 vs runner {overlap(s, runner):.2f} · 머리부분(y<50) {overlap(s.crop((0,0,128,50)), runner.crop((0,0,128,50))):.2f}")
            files = {
                f"helper_{d}_portrait.png": p, f"helper_{d}_portrait_x4.png": upscale(p, 4),
                f"helper_{d}_stand.png": s, f"helper_{d}_stand_x4.png": upscale(s, 4),
                f"helper_{d}_lobby.png": lb, f"helper_{d}_lobby_x3.png": upscale(lb, 3),
            }
            # 주인공 초상 / 지금 runner와 나란히 놓은 비교판(×3)
            pv = canvas(96 * 3 * 2 + 36, 96 * 3 + 28, PIX["k"])
            pv.alpha_composite(upscale(hk_portrait, 3), (12, 20)); pv.alpha_composite(upscale(p, 3), (96 * 3 + 24, 20))
            label(pv, 12, 6, "PROTAGONIST"); label(pv, 96 * 3 + 24, 6, NAMES[d])
            sv = canvas(128 * 3 * 2 + 36, 128 * 3 + 28, PIX["k"])
            sv.alpha_composite(upscale(runner, 3), (12, 20)); sv.alpha_composite(upscale(s, 3), (128 * 3 + 24, 20))
            label(sv, 12, 6, "RUNNER NOW"); label(sv, 128 * 3 + 24, 6, NAMES[d])
            files[f"helper_{d}_portrait_vs_hacker.png"] = pv
            files[f"helper_{d}_stand_vs_runner.png"] = sv
            for name, im in files.items():
                assert not (set(c for c in im.getdata() if c[3] > 0) - allowed(d) - {PIX["k"]}), name
                im.save(out / name)
                made.append(out / name)
        # 종합 비교판: 행 = 안, 열 = 초상 / 전신 / 침대 합성, 맨 위 줄 = 주인공 초상 · 지금 runner · 시안 C 원본
        crop = (150, 150, 350, 305)
        k = 2
        cw, ch_ = (crop[2] - crop[0]) * k, (crop[3] - crop[1]) * k
        colw = (96 * k, 128 * k, cw)
        rowh = max(128 * k, ch_) + 16
        total_w = 8 + sum(colw) + 8 * 3 + 8
        sheet = canvas(total_w, 4 * (rowh + 12) + 8, PIX["K"])
        xs = [8, 8 + colw[0] + 8, 8 + colw[0] + colw[1] + 16]
        hk_crop = base.crop((420, 150, 620, 305))

        def put(im, col, row, name):
            y = 8 + row * (rowh + 12)
            sheet.alpha_composite(canvas(colw[col], rowh, PIX["k"]), (xs[col], y))
            sheet.alpha_composite(upscale(im, k), (xs[col], y + 10))
            label(sheet, xs[col] + 3, y + 2, name)
        put(hk_portrait, 0, 0, "PROTAGONIST")
        put(runner, 1, 0, "RUNNER NOW")
        put(hk_crop, 2, 0, "LOBBY C")
        for r, d in enumerate(("H1", "H2", "H3"), start=1):
            p, s, lb = res[d]
            put(p, 0, r, NAMES[d]); put(s, 1, r, NAMES[d]); put(lb.crop(crop), 2, r, NAMES[d])
        sheet.save(out / "helper_compare.png")
        made.append(out / "helper_compare.png")
        for d in res:
            assert not (set(c for c in sheet.getdata() if c[3] > 0) - set(BASE.values()) - set(EXT.values()))
    assert all(k not in PIX for k in EXT)
    return made
