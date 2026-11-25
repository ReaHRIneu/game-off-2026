"""메인 로비 v4 (W-040 갈래 C) — 사용자 원화 키아트의 배치를 따라 코드로 다시 그린 메인 화면.

- 640×360(v3 B2와 같은 규격, 화면 4배 = 2560×1440), 공유 팔레트(PIX)만, 안티앨리어싱 없음.
- 원화 파일은 열지 않는다. 원화를 눈으로 보고 정한 배치·비율·색을 「원화 좌표(1024 기준) → 화면」 함수 T()로 옮겨 도형으로 그린다.
- 구도(원화 배치): 오른쪽 아래 큰 모니터 뒷면·책상, 오른쪽에 헤드셋 쓴 해커, 왼쪽 벽 침대에 걸터앉아 웃는 러너,
  가운데 뒤 창문·커튼, 벽 케이블·선반·오래된 컴퓨터, 왼쪽은 어두운 칸막이(메뉴 글자 자리, D-105).
- 레이어: bg(방) → runner → hacker → fg(칸막이·모니터 뒷면·책상). 합본 main_lobby_v4.png.
- 출력: Art/Export/scenes/main_lobby_v4*.png (기존 파일은 덮어쓰지 않음). manifest는 바꾸지 않는다.
[시안] 사용자 확정 전.
"""
import math

from PIL import Image, ImageDraw

from artkit import EXPORT, PIX, canvas, draw_ellipse, draw_poly, outline_outside, palette_check, pfill, pset, upscale
import main_lobby_drafts as dr
import gridkit

W, H = 640, 360
S = 0.30          # 원화 1px → 화면 0.30px (R4: 바닥선 y≈255, 인물 발 y≈297)
X0, Y0 = 84, -160  # 원화 (0,-160)이 화면 (84,0)
BAY = dr.BAYER


# =====================================================================
# 좌표·그리기 도구
# =====================================================================
def T(ox, oy):
    return (X0 + S * ox, S * (oy - Y0))


def TI(ox, oy):
    x, y = T(ox, oy)
    return (int(round(x)), int(round(y)))


def poly(L, pts, ch):
    draw_poly(L, [TI(*p) for p in pts], ch)


def polyo(L, pts, ch, ol="K"):
    q = [TI(*p) for p in pts]
    draw_poly(L, q, ch)
    ImageDraw.Draw(L).line(q + [q[0]], fill=PIX[ol], width=1)


def line(L, pts, ch, w=1):
    ImageDraw.Draw(L).line([TI(*p) for p in pts], fill=PIX[ch], width=w)


def ell(L, cx, cy, rx, ry, ch):
    x0, y0 = T(cx - rx, cy - ry)
    x1, y1 = T(cx + rx, cy + ry)
    draw_ellipse(L, (int(round(x0)), int(round(y0)), max(int(round(x0)), int(round(x1)) - 1), max(int(round(y0)), int(round(y1)) - 1)), ch)


def rect(L, ox0, oy0, ox1, oy1, ch):
    x0, y0 = TI(ox0, oy0)
    x1, y1 = TI(ox1, oy1)
    pfill(L, x0, y0, x1, y1, ch)


def rect_o(L, ox0, oy0, ox1, oy1, ch, ol="K"):
    x0, y0 = TI(ox0, oy0)
    x1, y1 = TI(ox1, oy1)
    pfill(L, x0, y0, x1, y1, ch)
    ImageDraw.Draw(L).rectangle((x0, y0, x1, y1), outline=PIX[ol])


def spolyo(L, pts, ch, ol="K"):
    """화면 좌표 다각형(배경 전용)."""
    q = [(int(round(x)), int(round(y))) for x, y in pts]
    draw_poly(L, q, ch)
    ImageDraw.Draw(L).line(q + [q[0]], fill=PIX[ol], width=1)


def spoly(L, pts, ch):
    draw_poly(L, [(int(round(x)), int(round(y))) for x, y in pts], ch)


def sline(L, pts, ch, w=1):
    ImageDraw.Draw(L).line([(int(round(x)), int(round(y))) for x, y in pts], fill=PIX[ch], width=w)


def scable(L, pts, ch, w=1):
    sline(L, [(int(x), int(y)) for x, y in curve(pts)], ch, w)


def curve(pts, n=24):
    """Catmull-Rom 곡선 샘플(원화 좌표)."""
    P = [pts[0]] + list(pts) + [pts[-1]]
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for k in range(n):
            t = k / n
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2
                                    + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in range(2)))
    out.append(pts[-1])
    return out


def cable(L, pts, ch, w=1):
    line(L, curve(pts), ch, w)


def bay(x, y):
    return (BAY[y & 3][x & 3] + 0.5) / 16


def dither_rect(L, ox0, oy0, ox1, oy1, top, bot, a=0.0, b=1.0):
    """세로 방향 디더 그라데이션(top → bot 문자)."""
    x0, y0 = TI(ox0, oy0)
    x1, y1 = TI(ox1, oy1)
    for y in range(y0, y1 + 1):
        t = (y - y0) / max(1, y1 - y0)
        t = min(1, max(0, (t - a) / max(1e-6, b - a)))
        for x in range(x0, x1 + 1):
            pset(L, x, y, bot if t > bay(x, y) else top)


def mask_of(L):
    m = Image.new("L", L.size, 0)
    px, mp = L.load(), m.load()
    for y in range(L.height):
        for x in range(L.width):
            if px[x, y][3]:
                mp[x, y] = 255
    return m


def clip_to(L, mask):
    out = Image.new("RGBA", L.size, (0, 0, 0, 0))
    out.paste(L, (0, 0), mask)
    return out


LIT = dict(dr.COOL1)
LIT.update({"B": "b", "b": "C", "n": "2", "N": "n", "p": "D", "E": "p", "O": "A", "o": "O", "x": "h", "z": "x",
            "l": "W", "T": "c", "t": "T", "l ": "W"})
WL = {"B": "b", "K": "0", "0": "4", "1": "4", "4": "2", "2": "3", "b": "C", "d": "D", "D": "h", "3": "C"}


# =====================================================================
# 배경(방)
# =====================================================================
def room_bg():
    L = canvas(W, H)
    pfill(L, 0, 0, W - 1, H - 1, "B")
    # 천장 그림자 띠(위쪽)
    dither_rect(L, -100, 120, 2300, 175, "0", "B", 0.0, 1.0)
    pfill(L, 0, 0, W - 1, TI(0, 121)[1], "0")
    line(L, [(190, 121), (2300, 121)], "K")
    line(L, [(190, 150), (2300, 150)], "4")   # 몰딩 선

    # --- 창문 ---
    polyo(L, [(512, 150), (762, 150), (762, 452), (512, 452)], "3")
    for (a, b) in ((528, 622), (634, 752)):
        dither_rect(L, a, 176, b, 432, "C", "b", 0.0, 1.0)
        ImageDraw.Draw(L).rectangle((*TI(a, 176), *TI(b, 432)), outline=PIX["K"])
    # 창밖 건물 실루엣 + 창 불빛
    import random
    rr = random.Random(7)
    for (x0_, x1_, top_, ch_) in ((532, 566, 330, "j"), (568, 612, 290, "k"), (534, 546, 380, "k"),
                                  (638, 672, 270, "k"), (674, 716, 340, "j"), (718, 750, 300, "k"), (730, 750, 360, "j")):
        rect(L, x0_, top_, x1_, 430, ch_)
        for yy in range(top_ + 12, 420, 18):
            for xx in range(x0_ + 8, x1_ - 6, 14):
                if rr.random() < 0.4:
                    rect(L, xx, yy, xx + 5, yy + 6, "c")
    line(L, [(628, 176), (628, 432)], "3", 2)
    # 창턱
    polyo(L, [(500, 432), (775, 432), (782, 452), (494, 452)], "2")
    line(L, [(500, 433), (775, 433)], "3")

    # --- 커튼 ---
    rect_o(L, 428, 152, 520, 462, "b")
    for x in (445, 462, 478, 496, 508):
        line(L, [(x, 156), (x - 2, 458)], "B")
    for x in (452, 486):
        line(L, [(x, 158), (x, 300)], "C")
    pfill(L, *TI(428, 456), *TI(520, 462), "B")
    rect_o(L, 757, 152, 842, 340, "b")
    for x in (772, 790, 806, 824):
        line(L, [(x, 156), (x - 1, 336)], "B")
    line(L, [(414, 148), (524, 148)], "K", 2)
    line(L, [(752, 148), (852, 148)], "K", 2)
    ell(L, 414, 148, 6, 6, "3"); ell(L, 852, 148, 6, 6, "3")

    # --- 벽 소품: 포스터·액자 ---
    rect_o(L, 226, 232, 282, 382, "4")
    rect_o(L, 288, 252, 366, 380, "2")
    ImageDraw.Draw(L).rectangle((*TI(296, 262), *TI(358, 372)), outline=PIX["4"])
    rect_o(L, 374, 188, 420, 238, "3")
    ell(L, 397, 212, 11, 11, "K"); ell(L, 397, 212, 8, 8, "3")
    line(L, [(397, 204), (397, 220)], "K"); line(L, [(389, 212), (405, 212)], "K")

    # --- 왼쪽 위 선반 + 오래된 컴퓨터 ---
    polyo(L, [(200, 120), (322, 120), (322, 170), (200, 150)], "K")
    polyo(L, [(240, 120), (322, 120), (322, 215), (240, 170)], "K")
    rect_o(L, 272, 134, 316, 205, "0")
    line(L, [(280, 146), (310, 146)], "K"); line(L, [(280, 156), (310, 156)], "K")
    polyo(L, [(200, 150), (322, 214), (322, 228), (200, 168)], "0")
    line(L, [(250, 214), (250, 240)], "K", 2); line(L, [(310, 226), (310, 252)], "K", 2)
    # --- 케이블(벽) ---
    for pts in ([(335, 126), (338, 165), (330, 205), (338, 240)],
                [(402, 126), (404, 175), (385, 215), (340, 245), (300, 245)],
                [(668, 126), (668, 170), (690, 205), (730, 230)],
                [(704, 126), (706, 190), (708, 250), (702, 300)],
                [(335, 245), (300, 262), (275, 270)]):
        cable(L, pts, "K", 1)
    ell(L, 336, 124, 8, 5, "2"); ell(L, 403, 124, 8, 5, "2"); ell(L, 669, 124, 8, 5, "2"); ell(L, 705, 124, 8, 5, "2")

    # --- 오른쪽 벽: 벽에 붙은 작은 선반 3단(원화처럼) ---
    for sy_ in (96, 118, 140):
        pfill(L, 470, sy_, 560, sy_ + 3, "3")
        pfill(L, 470, sy_ + 4, 560, sy_ + 4, "K")
    for (bx, by, bw, bh, bc) in ((476, 80, 14, 15, "4"), (494, 84, 18, 11, "2"), (520, 82, 20, 13, "4"),
                                 (478, 102, 22, 15, "2"), (506, 104, 16, 13, "4"), (530, 100, 22, 17, "2"),
                                 (476, 124, 16, 15, "4"), (498, 126, 28, 13, "2")):
        pfill(L, bx, by, bx + bw - 1, by + bh - 1, bc)
        pfill(L, bx, by, bx + bw - 1, by, "K"); pfill(L, bx, by, bx, by + bh - 1, "K")
    pfill(L, 468, 94, 469, 146, "K")
    # --- 바닥·바닥판 ---
    FY = 690
    floor = canvas(W, H)
    poly(floor, [(190, FY), (2300, FY), (2300, 1500), (190, 1500)], "b")
    vp = (690, 470)
    for xb in range(-400, 1700, 130):
        line(floor, [vp, (xb, 1500)], "B")
    for k in range(0, 8):
        yy = FY + 20 + k * k * 9
        line(floor, [(190, yy), (2300, yy)], "B")
    # 창 쪽 바닥은 더 밝게(디더)
    L.alpha_composite(clip_to(floor, mask_of(floor)))
    line(L, [(190, FY), (2300, FY)], "K")
    line(L, [(190, FY - 6), (2300, FY - 6)], "B")

    # --- 협탁 ---
    polyo(L, [(488, 548), (632, 548), (636, 566), (488, 566)], "3")
    rect_o(L, 494, 566, 626, 600, "2")
    rect(L, 540, 580, 580, 586, "3")
    rect_o(L, 494, 600, 626, 690, "4")
    rect_o(L, 506, 614, 610, 668, "0")               # 아래 칸의 상자
    line(L, [(506, 628), (610, 628)], "4")
    # 컵·액자
    rect_o(L, 506, 508, 532, 548, "3"); rect(L, 506, 505, 532, 510, "W")
    polyo(L, [(534, 478), (566, 478), (566, 548), (534, 548)], "2")
    rect(L, 540, 486, 560, 540, "b")

    # --- 침대(화면 좌표: 매트리스 윗면 y≈250이 러너 엉덩이 높이, 다리는 바닥 y≈297) ---
    pfill(L, 148, 190, 262, 214, "1")
    pfill(L, 148, 190, 262, 190, "K")
    pfill(L, 148, 176, 156, 250, "0"); pfill(L, 148, 176, 148, 250, "K")
    pfill(L, 256, 176, 264, 297, "0"); pfill(L, 264, 176, 264, 297, "K")
    pfill(L, 156, 214, 256, 250, "b")                       # 매트리스 윗면
    pfill(L, 156, 214, 256, 214, "K")
    pfill(L, 206, 218, 240, 230, "C"); pfill(L, 206, 218, 240, 218, "W"); pfill(L, 206, 231, 240, 231, "b")   # 베개
    pfill(L, 156, 250, 256, 250, "K")
    pfill(L, 142, 251, 256, 254, "C")                       # 매트리스 옆면(밝은 시트)
    pfill(L, 142, 255, 256, 266, "b")
    for x0_, x1_ in ((150, 168), (188, 204), (226, 244)):
        pfill(L, x0_, 259, x1_, 259, "B")
    pfill(L, 142, 267, 256, 267, "K")
    pfill(L, 142, 268, 256, 284, "B")                       # 이불 늘어진 부분
    for x0_ in (160, 200, 236):
        pfill(L, x0_, 270, x0_, 282, "K")
    pfill(L, 142, 285, 256, 297, "0")                       # 침대 프레임·다리
    pfill(L, 142, 285, 256, 285, "K")
    # --- 바닥 그림자 ---
    dither_rect(L, 380, 840, 640, 940, "b", "B", 0.0, 1.0)
    # 창 빛이 닿는 곳(차갑게)
    dr.relight(L, (lambda f: (lambda x, y: 0.5 * f(x, y)))(dr.ell(*T(640, 330), 170, 170)), WL, box=(120, 0, W - 1, H - 1))
    return L


# =====================================================================
# 해커
# =====================================================================
def eye(L, cx, cy, w, h, iris, dark, lid_frac, sclera="W", skin="l"):
    """w×h 눈. 위쪽 lid_frac 만큼 눈꺼풀(피부+진한 선)로 가린다."""
    ex0, ey0 = int(round(cx - w / 2)), int(round(cy - h / 2))
    ex1, ey1 = ex0 + w - 1, ey0 + h - 1
    draw_ellipse(L, (ex0, ey0, ex1, ey1), sclera)
    iw = max(3, int(w * 0.62))
    ix0 = int(round(cx - iw / 2))
    draw_ellipse(L, (ix0, ey0, ix0 + iw - 1, ey1), iris)
    pfill(L, ix0 + 1, ey0, ix0 + iw - 2, ey0 + max(1, int(h * 0.35)), dark)
    pfill(L, int(round(cx)) - 1, int(round(cy)), int(round(cx)), int(round(cy)) + 1, "K")
    pset(L, ix0 + iw - 2, ey0 + int(h * 0.55), "W")
    ly = ey0 + int(h * lid_frac)
    for y in range(ey0 - 1, ly):
        pfill(L, ex0 - 1, y, ex1 + 1, y, skin)
    pfill(L, ex0 - 1, ly, ex1 + 1, ly, "K")
    pfill(L, ex0 - 1, ly - 1, ex1 + 1, ly - 1, "K") if w >= 12 else None
    pset(L, ex0 - 2, ly + 1, "K"); pset(L, ex1 + 2, ly, "K")
    pfill(L, ex0, ey1 + 1, ex1, ey1 + 1, "S")


HACKER_AT = (247, 131)
RUNNER_AT = (153, 183)


def _paste(L, name, x, y):
    L.alpha_composite(gridkit.grid_image(gridkit.load_rows(name)), (x, y))


def hacker():
    """해커(의자 등받이 포함): 도형 함수 없이 손으로 찍은 문자 격자(grids/c_hacker*.txt)만 위치를 옮겨 붙인다."""
    L = canvas(W, H)
    x, y = HACKER_AT
    _paste(L, "c_hacker.txt", x, y)
    _paste(L, "c_hacker_cable.txt", x + 70, y + 47)
    _paste(L, "c_hacker_cupL.txt", x + 22, y + 22)
    _paste(L, "c_hacker_cupR.txt", x + 68, y + 22)
    _paste(L, "c_hacker_boom.txt", x + 54, y + 41)
    return L


def runner():
    """러너: 도형 함수 없이 손으로 찍은 문자 격자(grids/c_runner*.txt)만 위치를 옮겨 붙인다."""
    L = canvas(W, H)
    x, y = RUNNER_AT
    _paste(L, "c_runner.txt", x, y)
    _paste(L, "c_runner_bag.txt", x + 43, y + 56)
    return L


# =====================================================================
# 전경(칸막이·모니터 뒷면·책상)
# =====================================================================
def foreground():
    L = canvas(W, H)
    # --- 왼쪽 어두운 칸막이 ---
    px0, py0 = T(-200, 120)
    pfill(L, 0, 0, TI(195, 0)[0], H - 1, "K")
    pfill(L, TI(168, 0)[0], 0, TI(196, 0)[0], H - 1, "0")
    pfill(L, TI(196, 0)[0], 0, TI(196, 0)[0], H - 1, "1")
    # 칸막이에 걸린 케이블(메뉴 글자 아래·옆쪽에만)
    for pts in ([(40, 560), (110, 600), (160, 660), (196, 700)],
                [(-60, 700), (30, 690), (120, 730), (170, 790)],
                [(120, 330), (118, 420), (138, 470)],
                [(150, 120), (156, 260), (146, 360), (150, 420)]):
        cable(L, pts, "0", 2)
    rect(L, 142, 468, 154, 500, "0")                    # 늘어진 케이블 끝 단자
    # 칸막이 아래 PC 본체
    polyo(L, [(-20, 790), (150, 800), (160, 1100), (-20, 1100)], "0")
    polyo(L, [(-20, 790), (30, 780), (170, 796), (150, 800)], "1")
    for y in (860, 910, 960):
        rect_o(L, 56, y, 140, y + 36, "K", "1")
    ell(L, 100, 1040, 12, 12, "1")
    # 바닥 케이블
    for pts in ([(160, 1010), (240, 1000), (330, 1010), (420, 1040)], [(200, 960), (260, 940), (340, 970)]):
        cable(L, pts, "K", 2)

    # --- 책상 + 모니터 뒷면(R4: 오른쪽 아래 전경에서 가장 크게, 해커 가슴 아래를 가림) ---
    spolyo(L, [(236, 300), (276, 296), (700, 306), (700, 362), (246, 362)], "1")             # 책상 앞면·윗면
    sline(L, [(236, 301), (276, 297), (700, 307)], "2")
    spolyo(L, [(236, 300), (246, 362), (222, 362), (216, 312)], "0")                          # 책상 왼쪽 옆면
    spolyo(L, [(274, 221), (540, 215), (540, 330), (266, 332)], "0")                          # 모니터 뒷면 껍데기
    spoly(L, [(274, 221), (540, 215), (540, 226), (276, 232)], "4")                          # 윗변 빛
    sline(L, [(274, 223), (266, 332)], "2")
    sline(L, [(274, 223), (318, 262)], "K"); sline(L, [(266, 332), (310, 308)], "K")
    spolyo(L, [(318, 262), (540, 254), (540, 304), (310, 308)], "K")                          # 안쪽 돌출부
    sline(L, [(318, 262), (310, 308)], "4"); sline(L, [(318, 262), (540, 254)], "4")
    spolyo(L, [(350, 268), (410, 266), (409, 296), (351, 298)], "0")                          # 스탠드 마운트
    for (a_, b_) in ((356, 273), (404, 272), (357, 292), (404, 291)):
        pfill(L, a_, b_, a_ + 1, b_ + 1, "1")
    spolyo(L, [(366, 296), (400, 295), (402, 338), (364, 340)], "0")                          # 스탠드 목
    sline(L, [(368, 299), (366, 336)], "4")
    # 키보드
    spolyo(L, [(300, 336), (500, 338), (500, 348), (310, 346)], "0")
    for x in range(312, 496, 12):
        sline(L, [(x, 340), (x + 6, 340)], "1")
    # 케이블
    for pts in ([(300, 320), (290, 336), (270, 350), (240, 356)], [(420, 300), (430, 330), (450, 352), (480, 360)],
                [(250, 340), (230, 352), (200, 358)]):
        scable(L, pts, "K", 2)
    # 모니터 아래 어두운 그림자
    return L


def right_extra():
    """책상 위에 놓인 켜진 모니터·본체(해커 뒤쪽 오른쪽), 책상 뒤쪽 면."""
    L = canvas(W, H)
    pfill(L, 330, 221, 640, 232, "1"); pfill(L, 330, 221, 640, 221, "2")                    # 책상 뒤쪽 윗면
    pfill(L, 330, 232, 640, 233, "K")
    spolyo(L, [(334, 186), (350, 186), (350, 221), (334, 221)], "4")                          # 본체
    pfill(L, 337, 192, 347, 195, "0"); pfill(L, 337, 199, 347, 202, "0")
    spolyo(L, [(356, 168), (452, 164), (454, 214), (358, 218)], "3")                          # 켜진 모니터
    spolyo(L, [(361, 173), (448, 170), (449, 209), (362, 213)], "C")
    pfill(L, 362, 190, 448, 209, "W")
    pfill(L, 362, 186, 448, 186, "C")
    pfill(L, 395, 218, 410, 221, "3")                                                          # 받침
    return L


# =====================================================================
# 조립·검사·미리보기
# =====================================================================
def compose():
    bg = room_bg()
    bg.alpha_composite(right_extra())
    # 모니터 켜진 빛이 방에 번짐(차갑게, 약하게)
    soft = lambda f, k: (lambda x, y: k * f(x, y))
    dr.relight(bg, soft(dr.ell(*T(1100, 450), 150, 110), 0.6), WL, box=(300, 0, W - 1, H - 1))
    run = runner()
    hk = hacker()
    # 모니터 빛이 턱·목·가슴에 닿음(약하게)
    fg = foreground()
    out = canvas(W, H)
    for lay in (bg, run, hk, fg):
        out.alpha_composite(lay)
    return {"bg": bg, "runner": run, "hacker": hk, "fg": fg}, out


MENU_BOX = (20, 14, 120, 106)


def zone_check(im):
    mx, ma = dr.zone_stats(im, MENU_BOX)
    return mx, ma


def build():
    scenes = EXPORT / "scenes"
    prev = EXPORT / "preview"
    scenes.mkdir(parents=True, exist_ok=True)
    prev.mkdir(parents=True, exist_ok=True)
    layers, scene = compose()
    made = []
    for name, im in layers.items():
        assert not palette_check(im), name
        im.save(scenes / f"main_lobby_v4_{name}.png")
        made.append(scenes / f"main_lobby_v4_{name}.png")
    assert not palette_check(scene)
    scene.save(scenes / "main_lobby_v4.png")
    made.append(scenes / "main_lobby_v4.png")
    withmenu = dr.over(scene, dr.menu_overlay(0))
    assert not palette_check(withmenu)
    x1 = scene
    x3 = upscale(scene, 3)
    half = scene.resize((W // 2, H // 2), Image.NEAREST)
    # 메뉴 글자 자리 표시(빨간 테두리)
    marked = upscale(scene, 3).convert("RGBA")
    d = ImageDraw.Draw(marked)
    mb = MENU_BOX
    d.rectangle((mb[0] * 3, mb[1] * 3, mb[2] * 3, mb[3] * 3), outline=(255, 40, 40, 255), width=2)
    for name, im in (("main_lobby_v4_x1.png", x1), ("main_lobby_v4_x3.png", x3), ("main_lobby_v4_half.png", half),
                     ("main_lobby_v4_menu_x3.png", upscale(withmenu, 3)), ("main_lobby_v4_menuzone_x3.png", marked)):
        im.save(prev / name)
        made.append(prev / name)
    mx, ma = zone_check(scene)
    print(f"  [v4] {W}x{H}(x4=2560x1440), 배율 S={S}, 원화(0,120)→화면({X0},0), 메뉴 자리 {MENU_BOX} 최대 밝기 {mx:.0f}/평균 {ma:.0f}")
    return made
