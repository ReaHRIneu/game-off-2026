"""메인 로비 본그림 (W-023) — 시안 C안 + 조력자 H2, 서브컬처 화풍 강화, 작은 움직임.

- 근거: D-106(C안·조력자 침대에 앉음), D-107(H2·화풍 강화), D-056, D-057, D-103(창 유리 투명), D-079(조명 규칙)
- 640×360. 공유 팔레트 + 인물 전용 추가 색(EXT, 아래 표). 안티앨리어싱 없음. 인물 피부·머리에는 디더를 쓰지 않는다.
- 레이어(30 FPS 기준): L0 방(창 유리 투명) / L1 조력자 시트 / L2 주인공 시트 / L3 모니터 빛 시트 /
  L4 서버 LED 시트 / L5 창틀(환경 이펙트 위).
- 시안 레시피(main_lobby_drafts.py, helper_drafts.py)는 고치지 않고 부품 함수만 가져다 쓴다.
- 인물은 흑백 실루엣 → 큰 색면 → 선·명암 순서로 그렸다(2d-human-illustration-guideline.md).
[시안] Unity 적용은 W-024, 사용자 확인 전.
"""
import json
import random
from contextlib import contextmanager

from PIL import Image, ImageDraw

import main_lobby_drafts as dr
from artkit import (EXPORT, PIX, canvas, draw_ellipse, draw_line, draw_poly, over, pbox, pfill, pset,
                    save, upscale)
from pixfont import draw_text, text_width

W, H = 640, 360
BASE = dict(PIX)  # 추가 색을 넣기 전 공유 팔레트(검사 기준)

# 인물 전용 추가 색 [제안] — H2에서 쓴 4색(A·O·o·P) + 이번에 더한 색
EXT = {
    "A": (190, 128, 72, 255),    # 머리카락 하이라이트(밤색)            [H2]
    "O": (240, 140, 50, 255),    # 주황                                  [H2]
    "o": (176, 86, 34, 255),     # 주황 어두움                           [H2]
    "P": (150, 138, 96, 255),    # 카키(카고 바지)                       [H2]
    # 이번 작업에서 더한 색
    "p": (104, 96, 64, 255),     # 카키 어두움(카고 바지 그림자)
    "N": (236, 140, 150, 255),   # 분홍(입 안·볼 홍조·입술)
}
EXT_NEW = ("p", "N")


@contextmanager
def ext_palette():
    PIX.update(EXT)
    try:
        yield
    finally:
        for k in EXT:
            PIX.pop(k, None)


def bad_colors(im):
    ok = set(BASE.values()) | set(EXT.values())
    return {c for c in im.getdata() if c not in ok}


# =====================================================================
# 좌표 기록
# =====================================================================
C_MAIN, C_SCR, C_SIDE, C_SIDE_SCR = dr.C_MAIN, dr.C_SCR, dr.C_SIDE, dr.C_SIDE_SCR
WIN_OUTER = (372, 34, 588, 170)          # 창 바깥 틀(K 테두리 포함)
WIN_GLASS = (375, 37, 585, 167)          # 유리 영역(아래 창살 제외)
MULLIONS = ((444, 38, 447, 166), (516, 38, 519, 166), (376, 96, 584, 99))   # 세로 2 + 가로 1
SILL = (366, 168, 594, 177)
LED_BOX = (588, 146, 636, 296)           # 서버 랙·공유기 LED 시트 영역
GLOW_BOX = (330, 130, 640, 310)          # 모니터 빛 시트 영역


def glass_mask():
    m = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(m)
    d.rectangle(WIN_GLASS, fill=255)
    for r in MULLIONS:
        d.rectangle(r, fill=0)
    return m


# =====================================================================
# L0 방
# =====================================================================
RACK_ROWS = list(range(188, 290, 14))
RACK_LED_COL = ["G", "G", "Y", "R", "c", "G"]


def room_layer():
    """시안 C 방 그림에서 창 안 도시·비를 뺀 판(유리는 나중에 투명 처리), 서버 LED는 꺼진 색."""
    im = canvas(W, H, PIX["k"])
    pa, pf, pb, ps = dr.pfill, dr.pfill, dr.pbox, dr.pset
    dr.dith(im, (0, 0, W - 1, 140), "a", 5)
    dr.dith(im, (0, 0, W - 1, 56), "a", 6, 1)
    pfill(im, 0, 0, W - 1, 6, "0"); pfill(im, 0, 7, W - 1, 7, "K")
    for x in range(0, W, 80):
        pfill(im, x, 8, x, 290, "K")
    pfill(im, 0, 296, W - 1, H - 1, "0")
    pfill(im, 0, 290, W - 1, 295, "4"); pfill(im, 0, 290, W - 1, 290, "2"); pfill(im, 0, 296, W - 1, 296, "K")
    for k, y in enumerate((304, 316, 330, 346)):
        pfill(im, 0, y, W - 1, y, "K")
        for x in range((k * 37) % 61, W, 61):
            pfill(im, x, y, x, y + (11 if y < 330 else 14), "K")
    dr.dith(im, (0, 297, W - 1, H - 1), "a", 5)
    # 책장
    pfill(im, 24, 126, 138, 296, "0"); pbox(im, 24, 126, 138, 296, "K"); pbox(im, 25, 127, 137, 295, "4")
    rng = random.Random(14)
    for k, sy in enumerate((130, 168, 206, 244)):
        x = 28
        while x < 130:
            bw = rng.randint(4, 8)
            bh = rng.randint(20, 32)
            col = rng.choice(["1", "4", "j", "y", "g", "r", "2", "B"])
            pfill(im, x, sy + 33 - bh, x + bw - 1, sy + 32, col)
            pbox(im, x, sy + 33 - bh, x + bw - 1, sy + 32, "K")
            x += bw + rng.randint(0, 1)
        pfill(im, 26, sy + 33, 136, sy + 36, "4"); pfill(im, 26, sy + 37, 136, sy + 37, "K")
    dr.dith(im, (25, 127, 137, 295), "a", 5)
    # 포스터
    x0, y0, x1, y1 = dr.C_POSTER
    pfill(im, x0, y0, x1, y1, "B"); pbox(im, x0, y0, x1, y1, "K")
    for x in range(x0 + 6, x1 - 4, 6):
        pfill(im, x, y0 + 4, x, y1 - 4, "b")
    pbox(im, x0 + 22, y0 + 24, x0 + 40, y1 - 4, "C"); pbox(im, x0 + 26, y0 + 14, x0 + 36, y0 + 24, "C")
    pfill(im, x0 + 31, y0 + 8, x0 + 31, y0 + 14, "C")
    for (tx, ty) in [(x0, y0), (x1 - 3, y0), (x0, y1 - 3), (x1 - 3, y1 - 3)]:
        pfill(im, tx, ty, tx + 3, ty + 3, "h")
    # 침대(W-025: 프레임·매트리스·이불·베개 모서리를 둥글게)
    rr(im, 150, 262, 334, 292, 3, "d", "K"); dr.dith(im, (152, 264, 332, 291), "K", 9)
    rr(im, 154, 292, 160, 296, 1, "K", None); rr(im, 324, 292, 330, 296, 1, "K", None)
    rr(im, 152, 240, 332, 264, 5, "j", "K")
    rr(im, 170, 232, 332, 252, 6, "B", "K")
    pfill(im, 175, 233, 326, 235, "b")
    for x in range(180, 330, 22):
        draw_line(im, [(x, 236), (x + 6, 250)], "K")
    rr(im, 154, 226, 190, 244, 8, "2", "K")                                          # 베개
    pfill(im, 162, 227, 183, 228, "3"); pfill(im, 184, 233, 189, 240, "4")
    pset(im, 158, 229, "3"); pset(im, 159, 228, "3")
    # 창 틀(유리는 K로 채운 뒤 마지막에 투명 처리)
    pfill(im, 372, 34, 588, 170, "K")
    pbox(im, 372, 34, 588, 170, "K"); pbox(im, 373, 35, 587, 169, "2"); pbox(im, 374, 36, 586, 168, "4")
    pfill(im, 444, 38, 447, 166, "2"); pfill(im, 516, 38, 519, 166, "2")
    pfill(im, 376, 96, 584, 99, "2")
    # 창살 밝은 면(왼쪽·위 광원)
    pfill(im, 444, 38, 444, 166, "3"); pfill(im, 516, 38, 516, 166, "3"); pfill(im, 376, 96, 584, 96, "3")
    pfill(im, 447, 38, 447, 166, "4"); pfill(im, 519, 38, 519, 166, "4"); pfill(im, 376, 99, 584, 99, "4")
    pfill(im, 366, 168, 594, 174, "2"); pfill(im, 366, 175, 594, 177, "4"); pbox(im, 366, 168, 594, 177, "K")
    pfill(im, 367, 169, 593, 169, "3")
    # 서버 랙·공유기(LED는 꺼진 색, 켜진 모습은 L4)
    pfill(im, 596, 182, 634, 296, "K"); pbox(im, 597, 183, 633, 295, "2"); pfill(im, 598, 184, 632, 294, "0")
    for k, ry in enumerate(RACK_ROWS):
        pfill(im, 600, ry, 630, ry + 10, "4"); pbox(im, 600, ry, 630, ry + 10, "K")
        for i in range(3):
            pfill(im, 603 + i * 4, ry + 3, 604 + i * 4, ry + 4, "1")
        for i in range(8):
            pset(im, 614 + i * 2, ry + 3, "2"); pset(im, 614 + i * 2, ry + 6, "2")
    pfill(im, 590, 166, 632, 180, "K"); pfill(im, 592, 168, 630, 178, "4"); pfill(im, 592, 168, 630, 169, "2")
    for i in range(4):
        pset(im, 600 + i * 6, 174, "1"); pset(im, 601 + i * 6, 174, "1")
    draw_line(im, [(600, 168), (598, 152), (602, 148)], "K", 2); draw_line(im, [(624, 168), (626, 150)], "K", 2)
    return im


def desk_layer():
    """시안 책상에서 상판 위쪽 두 모서리를 둥글게 깎는다(W-025)."""
    d = dr.c_desk()
    round_layer_corner(d, (356, 232, 626, 241), 3, corners=(1, 1, 0, 0))
    return d


def props_layer():
    """시안 소품에서 스탠드 갓을 둥근 갓으로 다시 그린다(W-025)."""
    pr = dr.c_props()
    px = pr.load()
    gm = dr.pmask([(383, 188), (406, 195), (402, 208), (378, 201)])
    for y in range(185, 212):
        for x in range(374, 410):
            if gm.getpixel((x, y)) > 128:
                px[x, y] = (0, 0, 0, 0)
    draw_line(pr, [(371, 228), (370, 212), (378, 200), (392, 196)], "K", 3)
    draw_line(pr, [(371, 228), (370, 212), (378, 200), (392, 196)], "2", 1)
    quad = [(384, 190), (404, 196), (400, 206), (380, 200)]
    cx = sum(p[0] for p in quad) / 4
    cy = sum(p[1] for p in quad) / 4

    def soft(inset):
        pts = []
        for i in range(4):
            a, b = quad[i], quad[(i + 1) % 4]
            for (x, y) in ((a[0] * 0.8 + b[0] * 0.2, a[1] * 0.8 + b[1] * 0.2), (a[0] * 0.2 + b[0] * 0.8, a[1] * 0.2 + b[1] * 0.8)):
                pts.append((x + (cx - x) * inset, y + (cy - y) * inset))
        return pts
    dr.fill_mask(pr, M(S(soft(0.0), 8)), "K")
    dr.fill_mask(pr, M(S(soft(0.16), 8)), "h")
    pset(pr, 386, 192, "W"); pset(pr, 387, 192, "W")
    return pr


def lit_scene(s=0.85, glass=True):
    """방 + 가구 + 소품에 빛(차가운 모니터 빛 세기 s, 스탠드, 서버 랙)을 디더로 구운 장면. 인물 제외."""
    scene = canvas(W, H, PIX["k"])
    scene.alpha_composite(room_layer())
    scene.alpha_composite(desk_layer())
    mons, smask = dr.c_monitors()
    scene.alpha_composite(mons)
    scene.alpha_composite(props_layer())
    sk = lambda x, y: smask.getpixel((x, y)) > 128
    dr.relight(scene, _scaled(dr.ell(484, 200, 250, 150, 0.8), s), dr.COOL1, dr.COOL2, skip=sk)
    dr.relight(scene, dr.ell(392, 216, 40, 34, 1.1), dr.WARM1, dr.WARM2, skip=sk)
    dr.relight(scene, dr.ell(612, 236, 20, 60, 1.0), dr.COOL1, None, skip=sk)
    dr.dith(scene, (0, 0, 200, 110), "a", 6, 2)
    if glass:
        gm = glass_mask()
        mm = dr.mask_union(dr.pmask(list(C_MAIN)), dr.pmask(list(C_SIDE)))
        px = scene.load()
        for y in range(WIN_GLASS[1], WIN_GLASS[3] + 1):
            for x in range(WIN_GLASS[0], WIN_GLASS[2] + 1):
                if gm.getpixel((x, y)) > 128 and mm.getpixel((x, y)) < 128:
                    px[x, y] = (0, 0, 0, 0)
    return scene, smask


def _scaled(f, s):
    return lambda x, y: f(x, y) * s


def backdrop():
    """미리보기용 창밖(비 오는 밤 도시). 게임에서는 환경 이펙트(D-103)가 이 자리에 나온다."""
    im = canvas(W, H, PIX["K"])
    dr.city_view(im, WIN_GLASS, seed=33, horizon=0.9)
    dr.rain(im, WIN_GLASS, 7, 28, 8)
    return im


# =====================================================================
# 칠하기 도구 (인물 공용)
# =====================================================================
from PIL import ImageChops


def M(pts):
    return dr.pmask(pts)


def inter(a, b):
    return ImageChops.darker(a, b)


def put(lay, pts, ch, edge=None, mono=False):
    """다각형을 칠하고(mono면 검정 실루엣) 바깥 1px 색 외곽선(edge)을 두른다. 마스크를 돌려준다."""
    m = pts if isinstance(pts, Image.Image) else M(pts)
    dr.fill_mask(lay, m, "K" if mono else ch)
    if edge and not mono:
        dr.outline_mask(lay, m, edge)
    return m


def shade(lay, base, pts, ch, mono=False):
    """base 마스크 안쪽 중 다각형 pts 와 겹치는 곳만 ch 로 칠한다(2단 셀 명암)."""
    if mono:
        return
    dr.fill_mask(lay, inter(base, pts if isinstance(pts, Image.Image) else M(pts)), ch)


def lines(lay, spec, mono=False):
    """spec: [(점들, 색, 굵기)]"""
    if mono:
        return
    for pts, ch, w in spec:
        draw_line(lay, pts, ch, w)


def dots(lay, pts, ch, mono=False):
    if mono:
        return
    for x, y in pts:
        pset(lay, x, y, ch)


def silhouette_outline(lay, ch="K"):
    """불투명 영역 바깥 1px(상하좌우) 외곽선."""
    px = lay.load()
    w, h = lay.size
    solid = {(x, y) for y in range(h) for x in range(w) if px[x, y][3]}
    for (x, y) in solid:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in solid:
                px[nx, ny] = PIX[ch]


def rim_right(lay, ch="c", ch2="C", skip=None):
    """모니터 쪽(오른쪽) 청록 림 라이트 1px: 오른쪽이 비어 있는 불투명 픽셀(외곽선 칠하기 전)."""
    px = lay.load()
    w, h = lay.size
    for y in range(h):
        for x in range(w - 1):
            if px[x, y][3] and not px[x + 1, y][3]:
                if skip and skip(x, y):
                    continue
                if (x + y) % 3:
                    px[x, y] = PIX[ch if (y % 4) else ch2]


def bbox_of(im):
    return im.getbbox()


# =====================================================================
# 칠하기 도구 (인물 공용)
# =====================================================================
from PIL import ImageChops


def M(pts):
    return dr.pmask(pts)


def inter(a, b):
    return ImageChops.darker(a, b)


def put(lay, pts, ch, edge=None, mono=False):
    """다각형을 칠하고(mono면 검정 실루엣) 바깥 1px 색 외곽선(edge)을 두른다. 마스크를 돌려준다."""
    m = pts if isinstance(pts, Image.Image) else M(pts)
    dr.fill_mask(lay, m, "K" if mono else ch)
    if edge and not mono:
        dr.outline_mask(lay, m, edge)
    return m


def shade(lay, base, pts, ch, mono=False):
    """base 마스크 안쪽 중 다각형 pts 와 겹치는 곳만 ch 로 칠한다(2단 셀 명암)."""
    if mono:
        return
    dr.fill_mask(lay, inter(base, pts if isinstance(pts, Image.Image) else M(pts)), ch)


def lines(lay, spec, mono=False):
    """spec: [(점들, 색, 굵기)]"""
    if mono:
        return
    for pts, ch, w in spec:
        draw_line(lay, pts, ch, w)


def dots(lay, pts, ch, mono=False):
    if mono:
        return
    for x, y in pts:
        pset(lay, x, y, ch)


def silhouette_outline(lay, ch="K"):
    """불투명 영역 바깥 1px(상하좌우) 외곽선."""
    px = lay.load()
    w, h = lay.size
    solid = {(x, y) for y in range(h) for x in range(w) if px[x, y][3]}
    for (x, y) in solid:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in solid:
                px[nx, ny] = PIX[ch]


def rim_right(lay, ch="c", ch2="C", skip=None):
    """모니터 쪽(오른쪽) 청록 림 라이트 1px: 오른쪽이 비어 있는 불투명 픽셀(외곽선 칠하기 전)."""
    px = lay.load()
    w, h = lay.size
    for y in range(h):
        for x in range(w - 1):
            if px[x, y][3] and not px[x + 1, y][3]:
                if skip and skip(x, y):
                    continue
                if (x + y) % 3:
                    px[x, y] = PIX[ch if (y % 4) else ch2]


# =====================================================================
# 곡선 도구 (W-025) — 손으로 고른 점 대신 부드러운 곡선을 그리고 래스터화해 계단이 고르게 나오게 한다
# =====================================================================
def S(pts, n=10):
    """닫힌 부드러운 곡선(Catmull-Rom). 점이 (x, y, 'c') 이면 뾰족한 모서리(꺾임)."""
    P = [(p[0], p[1]) for p in pts]
    C = [len(p) > 2 for p in pts]
    N = len(P)
    out = []
    for i in range(N):
        i0, i1, i2, i3 = (i - 1) % N, i, (i + 1) % N, (i + 2) % N
        p0 = P[i1] if C[i1] else P[i0]
        p3 = P[i2] if C[i2] else P[i3]
        p1, p2 = P[i1], P[i2]
        for k in range(n):
            t = k / n
            t2, t3 = t * t, t * t * t
            x = 0.5 * ((2 * p1[0]) + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2
                       + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3)
            y = 0.5 * ((2 * p1[1]) + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2
                       + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3)
            out.append((x, y))
    return out


def O(pts, t, n=10):
    """열린 부드러운 곡선(선 그리기용 점 목록)."""
    P = list(pts)
    out = []
    for i in range(len(P) - 1):
        p0 = P[max(i - 1, 0)]
        p1, p2 = P[i], P[i + 1]
        p3 = P[min(i + 2, len(P) - 1)]
        for k in range(n):
            u = k / n
            u2, u3 = u * u, u * u * u
            x = 0.5 * ((2 * p1[0]) + (-p0[0] + p2[0]) * u + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * u2
                       + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * u3)
            y = 0.5 * ((2 * p1[1]) + (-p0[1] + p2[1]) * u + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * u2
                       + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * u3)
            out.append((x, y))
    out.append(P[-1])
    return out


def curve(lay, pts, ch, w=1):
    """열린 곡선을 1px(또는 w px) 선으로. 안티앨리어싱 없음."""
    draw_line(lay, O(pts, 0), ch, w)


def RR(x0, y0, x1, y1, r):
    """둥근 사각형 마스크(양 끝 포함). 모서리는 반지름 r 원."""
    m = Image.new("L", (W, H), 0)
    px = m.load()
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            dx = max(x0 + r - x, 0, x - (x1 - r))
            dy = max(y0 + r - y, 0, y - (y1 - r))
            if r == 0 or (dx * dx + dy * dy) <= (r - 0.5) * (r - 0.5) + r * 0.6:
                px[x, y] = 255
    return m


def rr(im, x0, y0, x1, y1, r, fill, edge="K"):
    """둥근 사각형: 안쪽에 fill, 안쪽 1px 테두리 edge(그림 위에 덮어 그림)."""
    m = RR(x0, y0, x1, y1, r)
    dr.fill_mask(im, m, fill)
    if edge:
        mp = m.load()
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                if mp[x, y] >= 128 and any(not (0 <= x + dx < W and 0 <= y + dy < H) or mp[x + dx, y + dy] < 128
                                           for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    pset(im, x, y, edge)
    return m


def round_layer_corner(lay, box, r, edge="K", corners=(1, 1, 1, 1)):
    """투명 판 위에 그려진 사각 물체(바깥 1px 테두리 포함)의 네 모서리를 반지름 r 로 깎고 테두리를 다시 두른다."""
    x0, y0, x1, y1 = box
    px = lay.load()
    cut = set()
    for (cx, cy, sx, sy), on in zip(((x0, y0, 1, 1), (x1, y0, -1, 1), (x0, y1, 1, -1), (x1, y1, -1, -1)), corners):
        if not on:
            continue
        for j in range(r):
            for i in range(r):
                if (r - i - 0.5) ** 2 + (r - j - 0.5) ** 2 > r * r:
                    cut.add((cx + sx * i, cy + sy * j))
    for (x, y) in cut:
        px[x, y] = (0, 0, 0, 0)
    for (x, y) in cut:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if (nx, ny) not in cut and x0 <= nx <= x1 and y0 <= ny <= y1 and px[nx, ny][3]:
                px[nx, ny] = PIX[edge]


def box_pts(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def shade_box(lay, base, box, ch, mono=False):
    shade(lay, base, box_pts(*box), ch, mono)


# =====================================================================
# L1 조력자 (H2 스트리트 러너, 침대 머리 쪽에 걸터앉음) — W-025: 둥근 형태, 태블릿을 내려다보는 미소
# =====================================================================
# 비례: 얼굴(앞머리선~턱) 약 28px, 머리 정수리~턱 약 40px, 앉은키(엉덩이~정수리) 약 80px.


def helper_low(mono=False, detail=True):
    """엉덩이·다리(움직이지 않음). 무릎은 굽은 곡선, 신발 앞코는 둥글게."""
    lay = canvas(W, H)
    # 오른 다리(화면 오른쪽): 무릎에서 바깥으로 벌어져 내려와 발이 침대 프레임 아랫단에 걸림
    shin_r = put(lay, S([(222, 248), (231, 249), (238, 257), (242, 266), (244, 275), (245, 283), (234, 284), (231, 277),
                         (227, 269), (221, 262), (217, 255)]), "P", "p", mono)
    shade(lay, shin_r, [(232, 252), (238, 257), (242, 266), (245, 283), (238, 284), (237, 270), (234, 260)], "p", mono)
    shoe_r = put(lay, S([(231, 283), (238, 281), (246, 283), (251, 288), (250, 293), (244, 295), (233, 294), (230, 289)]), "W", "3", mono)
    shade(lay, shoe_r, [(230, 288), (252, 288), (252, 296), (230, 296)], "3", mono)
    sole_r = put(lay, S([(231, 290), (249, 290), (250, 294), (244, 295), (233, 295), (230, 293)]), "O", "o", mono)
    lines(lay, [(O([(239, 284), (243, 285), (246, 287)], 0), "O", 1)], mono)
    # 왼 다리(화면 왼쪽): 곧게 내려오고 발끝은 바깥(왼쪽)
    shin_l = put(lay, S([(201, 249), (213, 249), (214, 258), (213, 268), (212, 278), (211, 286), (201, 286), (200, 278),
                         (199, 268), (198, 258)]), "P", "p", mono)
    shade(lay, shin_l, [(208, 252), (213, 249), (214, 258), (213, 268), (212, 278), (211, 286), (206, 286), (207, 270), (207, 260)], "p", mono)
    shoe_l = put(lay, S([(189, 292), (190, 288), (196, 285), (203, 284), (211, 285), (213, 291), (213, 296), (192, 296), (189, 295)]), "W", "3", mono)
    shade(lay, shoe_l, [(188, 291), (214, 291), (214, 297), (188, 297)], "3", mono)
    put(lay, S([(190, 294), (192, 292), (213, 292), (213, 296), (192, 296)]), "O", "o", mono)
    lines(lay, [(O([(194, 289), (198, 287), (202, 287)], 0), "O", 1), ([(205, 287), (210, 288)], "3", 1)], mono)
    # 엉덩이·허벅지(앉은 둥근 윗면)
    put(lay, S([(199, 243), (206, 240), (218, 239), (230, 240), (237, 244), (239, 251), (232, 255), (217, 256), (203, 255),
                (196, 251)]), "P", "p", mono)
    if detail and not mono:
        pfill(lay, 205, 241, 232, 241, "2")                                                          # 허리띠 아래 가장자리
        bag = put(lay, S([(230, 250), (236, 249), (241, 252), (241, 258), (236, 261), (231, 260)]), "3", "K", mono)   # 허리 가방
        shade(lay, bag, [(237, 250), (242, 252), (242, 259), (236, 262), (238, 256)], "2", mono)
        lines(lay, [(O([(231, 254), (236, 253), (240, 254)], 0), "Y", 1)], mono)
        put(lay, S([(196, 252), (202, 251), (203, 258), (201, 262), (196, 261)]), "P", "p", mono)       # 카고 주머니
        lines(lay, [([(197, 254), (202, 253)], "p", 1), ([(199, 257), (199, 259)], "y", 1)], mono)
        # 바지 주름: 사선 3개(무릎 쪽)
        lines(lay, [([(205, 254), (208, 262)], "p", 1), ([(222, 252), (219, 260)], "p", 1), ([(235, 259), (232, 267)], "p", 1)], mono)
        dots(lay, [(204, 255), (205, 255), (221, 253), (222, 253)], "h", mono)
    return lay


def eye_down(lay, x0, y0, w, h, outer, state=0):
    """아래(태블릿)를 보는 눈. outer = -1(바깥이 왼쪽) / +1(바깥이 오른쪽). state 0=뜸, 1=반쯤, 2=감음.
    윗눈꺼풀을 반쯤 내려 홍채 윗부분을 덮고, 홍채는 아래쪽이 밝다. 두 눈은 같은 높이에서 시작한다."""
    ox = x0 if outer < 0 else x0 + w - 1            # 눈꼬리 쪽 끝 x
    ix = x0 + w - 1 if outer < 0 else x0            # 눈머리 쪽 끝 x
    if state == 2:                                   # 감은 눈: 아래로 굽은 선(웃는 눈처럼 ∪)
        for i in range(w):
            x = x0 + i
            t = i if outer > 0 else w - 1 - i        # 0=눈머리
            ydn = 2 if 1 <= t <= w - 2 else 1
            pset(lay, x, y0 + ydn, "K")
        pset(lay, ox + outer, y0 + 1, "K")
        return
    top = {0: 0, 1: 1, 3: 2}[state]
    ys = y0 + top
    bot = y0 + h - 1
    # 흰자(양 끝에만 살짝) + 홍채(폭 w-2): 위 어둡게(d) → 가운데(D) → 아래 밝게(A, h)
    iw = w - 2
    i0 = x0 + 1
    for y in range(ys + 1, bot + 1):
        k = y - ys
        n = bot - ys
        if y == bot:
            a, b = i0 + 1, i0 + iw - 2
        else:
            a, b = i0, i0 + iw - 1
        if y < bot:
            pset(lay, x0, y, "W"); pset(lay, x0 + w - 1, y, "W")
        if k <= 1:
            col = "d"
        elif k < n - 1:
            col = "D"
        elif k == n - 1:
            col = "A"
        else:
            col = "h"
        pfill(lay, a, y, b, y, col)
    if h >= 5:
        pc = i0 + (iw - 2) // 2
        pfill(lay, pc, ys + 2, pc + 1, ys + 2 + max(1, (h - 4)), "K")          # 동공(아래쪽으로 치우침)
        pset(lay, i0 + 1, ys + 2, "W")                                          # 큰 하이라이트
        if h >= 7:
            pset(lay, i0 + 2, ys + 2, "W"); pset(lay, i0 + 1, ys + 3, "W")
        pset(lay, i0 + iw - 2, bot - 1, "W")                                    # 작은 하이라이트
    # 윗속눈썹(굵음): 눈꼬리 쪽이 한 줄 더, 눈꼬리에서 1px 바깥으로 뻗음
    pfill(lay, x0, ys, x0 + w - 1, ys, "K")
    pset(lay, ox, ys + 1, "K")
    pset(lay, ox + outer, ys, "K")
    pset(lay, ox + outer, ys - 1, "K")
    # 아래 속눈썹 점 1개(눈꼬리 쪽)
    pset(lay, ox, bot + 1, "d")


def smile(lay, cx, y, half=3, drop=2):
    """닫힌 웃는 입(∪ 곡선, 이·네모 없음): 양 끝이 위, 가운데가 drop px 아래."""
    for i in range(-half, half + 1):
        pset(lay, cx + i, y + drop - round(drop * (i / half) ** 2), "K")


def helper_up(breath=0, eye=0, tap=0, mono=False, detail=True):
    """머리·몸통·팔·태블릿(숨쉬기로 함께 1px 움직이는 윗몸)."""
    lay = canvas(W, H)
    # ---- 뒷머리(머리 뒤로 늘어진 울프컷) + 왼쪽 작은 묶음 ----
    back = put(lay, S([(200, 188), (200, 178), (205, 171), (212, 167), (223, 167), (231, 171), (236, 178), (237, 190),
                       (237, 200), (239, 207, "c"), (234, 204), (231, 208, "c"), (227, 204), (222, 208, "c"), (218, 204),
                       (214, 208, "c"), (210, 204), (206, 208, "c"), (203, 204), (200, 207, "c"), (200, 197)]), "D", "d", mono)
    if detail and not mono:
        for x in (204, 210, 216, 222, 228, 234):
            lines(lay, [([(x, 194), (x + 1, 204)], "d", 1)], mono)
        shade(lay, back, [(198, 203), (240, 203), (240, 214), (198, 214)], "O", mono)      # 머리끝 주황
        lines(lay, [([(201, 202), (237, 202)], "d", 1)], mono)
    pony = put(lay, S([(203, 185), (198, 187), (194, 193), (192, 201), (193, 208), (196, 214, "c"), (199, 207), (201, 198),
                       (203, 191)]), "D", "d", mono)
    if detail and not mono:
        shade(lay, pony, [(190, 203), (203, 203), (203, 216), (190, 216)], "O", mono)
        put(lay, S([(195, 187), (201, 186), (202, 191), (196, 192)]), "O", "o", mono)      # 주황 끈
        lines(lay, [(O([(197, 194), (196, 199), (196, 205)], 0), "d", 1), ([(200, 196), (199, 203)], "A", 1)], mono)
    # ---- 목 ----
    neck = put(lay, S([(212, 201), (224, 201), (225, 207), (225, 212), (211, 212), (211, 207)]), "S", "s", mono)
    shade(lay, neck, [(217, 201), (225, 201), (225, 212), (217, 212)], "s", mono)
    shade(lay, neck, [(211, 201), (225, 201), (225, 207), (211, 207)], "s", mono)   # 턱 그림자
    # ---- 몸통: 크롭 윈드브레이커(둥근 어깨선) ----
    torso = put(lay, S([(199, 219), (203, 213), (210, 209), (218, 208), (226, 209), (233, 213), (237, 219), (237, 227),
                        (235, 235), (234, 242), (218, 244), (202, 242), (201, 235), (199, 227)]), "O", "o", mono)
    shade(lay, torso, [(227, 210), (233, 213), (237, 219), (237, 227), (235, 235), (234, 242), (224, 243), (229, 230)], "o", mono)
    if not mono:
        dr.outline_mask(lay, torso, "d", where=lambda x, y: y < 214)
    if detail and not mono:
        pfill(lay, 203, 238, 233, 241, "o"); pfill(lay, 203, 238, 233, 238, "Y")
        lines(lay, [([(215, 217), (215, 228), (216, 238)], "K", 1),                  # 지퍼
                    (O([(202, 224), (206, 228), (205, 235)], 0), "o", 1),             # 주름 1
                    (O([(223, 223), (227, 228), (226, 235)], 0), "o", 1),             # 주름 2
                    ([(209, 231), (211, 236)], "o", 1)], mono)                       # 주름 3
        lines(lay, [(O([(203, 215), (207, 212), (212, 210)], 0), "h", 1), ([(198, 223), (198, 232)], "A", 1)], mono)
        dots(lay, [(215, 216), (215, 217)], "h", mono)                                 # 지퍼 손잡이
        lines(lay, [([(235, 218), (236, 226)], "Y", 1)], mono)                         # 어깨 노랑 줄
    col = put(lay, S([(209, 207), (212, 204), (224, 204), (227, 207), (228, 213), (219, 217), (210, 214)]), "O", "o", mono)
    if detail and not mono:
        shade(lay, col, [(220, 204), (228, 207), (228, 213), (220, 217)], "o", mono)
        lines(lay, [([(210, 208), (213, 205)], "h", 1), ([(219, 209), (219, 216)], "K", 1)], mono)
    # ---- 팔(소매): 어깨에서 굽은 팔꿈치를 지나 태블릿으로 ----
    sl = put(lay, S([(199, 214), (195, 221), (193, 230), (195, 238), (201, 244), (208, 246), (210, 241), (205, 236), (203, 228),
                     (203, 220)]), "O", "o", mono)
    shade(lay, sl, [(195, 234), (199, 241), (205, 245), (210, 244), (209, 240), (204, 236)], "o", mono)
    sr = put(lay, S([(237, 214), (241, 221), (243, 230), (241, 238), (235, 244), (228, 246), (226, 241), (231, 236), (233, 228),
                     (233, 220)]), "O", "o", mono)
    shade(lay, sr, [(237, 226), (242, 232), (241, 238), (235, 244), (228, 246), (230, 240), (234, 232)], "o", mono)
    if detail and not mono:
        pfill(lay, 197, 239, 203, 241, "Y"); pfill(lay, 232, 239, 238, 241, "Y")
        lines(lay, [(O([(202, 223), (205, 232), (209, 240)], 0), "o", 1), (O([(233, 223), (231, 232), (227, 240)], 0), "o", 1)], mono)
    # ---- 태블릿(둥근 모서리) ----
    tb = put(lay, S([(207, 232), (210, 230), (225, 230), (228, 232), (229, 244), (227, 249), (209, 249), (206, 244)], 6), "4", "K", mono)
    if detail and not mono:
        lines(lay, [([(210, 231), (225, 231)], "C", 1)], mono)
        shade(lay, tb, [(222, 232), (228, 231), (229, 244), (227, 249), (219, 249)], "K", mono)
        pfill(lay, 210, 235, 211, 236, "3")
        lines(lay, [([(213, 245), (221, 245)], "2", 1)], mono)
    # ---- 손: 손가락 2~3px 묶음으로 태블릿 가장자리를 감쌈 ----
    hl = put(lay, S([(203, 242), (207, 240), (212, 242), (213, 247), (210, 251), (205, 250), (202, 246)]), "S", "s", mono)
    hr = put(lay, S([(233, 242), (229, 240), (224, 242), (223, 247), (226, 251), (231, 250), (234, 246)]), "S", "s", mono)
    if detail and not mono:
        for yy in (242, 245, 248):                       # 왼손 손가락 묶음(태블릿 앞으로 올라옴)
            lines(lay, [([(209, yy), (214, yy)], "S", 1), ([(209, yy + 1), (214, yy + 1)], "s", 1)], mono)
        for k, yy in enumerate((241, 244, 247)):         # 오른손 손가락 묶음
            up = -1 if (tap and k == 0) else 0
            lines(lay, [([(222, yy + up), (227, yy + up)], "S", 1), ([(222, yy + up + 1), (227, yy + up + 1)], "s", 1)], mono)
        ty = 232 if not tap else 234
        put(lay, S([(226, ty - 2), (231, ty - 2), (232, ty + 1), (229, ty + 3), (226, ty + 1)]), "S", "s", mono)   # 엄지
        pset(lay, 228, ty - 1, "l")
        if tap:
            lines(lay, [([(223, 232), (225, 231)], "C", 1)], mono)
    # ---- 얼굴: 갸름한 턱, 둥근 볼 ----
    face_pts = S([(204, 184), (209, 179), (216, 177), (223, 178), (229, 181), (232, 187), (232, 194), (230, 200), (226, 204),
                  (220, 207), (214, 206), (209, 203), (205, 198), (203, 191)])
    face = put(lay, face_pts, "S", "s", mono)
    put(lay, S([(201, 189), (204, 188), (204, 197), (202, 198)]), "S", "s", mono)         # 왼쪽 귀(이어피스 자리)
    if detail and not mono:
        shade(lay, face, [(227, 182), (233, 186), (233, 194), (230, 200), (226, 204), (223, 207), (226, 198), (228, 190)], "s", mono)
        shade(lay, face, [(203, 178), (233, 178), (233, 185), (203, 185)], "s", mono)       # 앞머리 그림자
        shade(lay, face, [(210, 205), (222, 207), (219, 208), (211, 207)], "s", mono)
        pfill(lay, 200, 190, 202, 195, "K"); pfill(lay, 201, 191, 201, 194, "3"); pset(lay, 201, 192, "G")   # 이어피스
        lines(lay, [(O([(200, 196), (199, 200), (198, 204)], 0), "K", 1)], mono)
        # 눈썹: 부드러운 곡선(양쪽 같은 높이), 눈: 두 눈 같은 높이·같은 크기(먼 쪽만 1px 좁게)
        curve(lay, [(206, 188), (209, 187), (213, 188)], "d")
        curve(lay, [(223, 188), (226, 187), (229, 188)], "d")
        eye_down(lay, 206, 190, 8, 6, -1, eye)
        eye_down(lay, 222, 190, 7, 6, +1, eye)
        dots(lay, [(219, 198)], "s"); dots(lay, [(218, 197)], "l")                       # 코
        smile(lay, 218, 200, 3)                                                           # 닫힌 웃는 입
        dots(lay, [(207, 197), (208, 196), (209, 197), (227, 197), (228, 196), (229, 197)], "N")   # 볼 홍조
    # ---- 앞머리·옆머리: 끝이 뾰족한 다발 4개 + 양옆 머리 ----
    front = put(lay, S([(202, 203, "c"), (200, 195), (199, 185), (202, 176), (209, 170), (218, 168), (227, 170), (233, 176),
                        (236, 185), (236, 195), (234, 203, "c"), (232, 193), (232, 187), (229, 186, "c"), (227, 181),
                        (223, 186, "c"), (220, 181), (216, 186, "c"), (213, 181), (209, 186, "c"), (206, 183), (205, 190),
                        (204, 197)], 8), "D", "d", mono)
    if detail and not mono:
        for (nx, ny, tx, ty) in ((227, 181, 229, 174), (220, 181, 220, 173), (213, 181, 211, 174)):
            curve(lay, [(nx, ny), ((nx + tx) // 2, (ny + ty) // 2 + 1), (tx, ty)], "d")
            curve(lay, [(nx - 2, ny - 1), (tx - 2, ty + 2)], "A")
        lines(lay, [([(206, 183), (207, 177)], "d", 1), ([(203, 186), (204, 195)], "d", 1), ([(201, 184), (201, 194)], "A", 1)])
        lines(lay, [([(234, 183), (234, 196)], "d", 1), ([(232, 177), (232, 187)], "A", 1)])
        shade(lay, front, [(233, 184), (238, 184), (237, 200), (234, 203)], "d")
        # 천사 링: 정수리 둥근 면을 따라 휘는 가로 하이라이트 띠
        ring = [(205, 180), (208, 176), (212, 174), (218, 173), (224, 174), (228, 176), (231, 180)]
        curve(lay, ring, "A")
        curve(lay, [(p[0], p[1] - 1) for p in ring[2:-2]], "h")
        # 바보털 1가닥
        curve(lay, [(216, 169), (215, 165), (217, 162), (221, 161)], "D")
        lines(lay, [([(217, 166), (218, 163)], "A", 1)])
        dots(lay, [(221, 161)], "d")
        # 머리끝 주황(다발 끝 2~3px)
        for tx, ty in ((229, 186), (223, 186), (216, 186), (209, 186)):
            dots(lay, [(tx, ty), (tx, ty - 1), (tx - 1, ty - 1)], "O")
        pfill(lay, 202, 199, 203, 202, "O"); pfill(lay, 233, 199, 234, 202, "O")
    return lay


def helper_frame(breath=0, eye=0, tap=0, mono=False, detail=True, rim=True):
    """한 프레임(640×360 투명 판). breath 0/1 = 윗몸이 1px 올라감."""
    lay = canvas(W, H)
    lay.alpha_composite(helper_low(mono, detail))
    up = helper_up(breath, eye, tap, mono, detail)
    lay.alpha_composite(up, (0, -breath))
    if rim and not mono:
        rim_right(lay)
    silhouette_outline(lay, "K")
    if not mono:
        px = lay.load()                      # 왼발 밑 접지 그림자(바닥에 닿은 발만, 디더 2줄 — D-079)
        for y in (299, 300):
            for x in range(190, 214):
                if (x + y) % 2 == 0 and not px[x, y][3]:
                    px[x, y] = PIX["a"]
    return lay



def rim_top(lay, ch="c", ch2="C", skip=None):
    """위쪽이 비어 있는 불투명 픽셀에 청록 림 라이트(모니터가 앞쪽 벽에 있어 위·오른쪽에서 비춤)."""
    px = lay.load()
    w, h = lay.size
    for y in range(1, h):
        for x in range(w):
            if px[x, y][3] and not px[x, y - 1][3]:
                if skip and skip(x, y):
                    continue
                if (x + y) % 3:
                    px[x, y] = PIX[ch if (x % 4) else ch2]


# =====================================================================
# L2 주인공 (책상 앞 의자) — W-025: 3/4 뒷모습 몸 + 3/4 옆얼굴(왼쪽을 향함), 담담한 집중 표정
# =====================================================================
def hacker_low(mono=False, detail=True):
    """의자 등받이·기둥·바퀴·좌석(움직이지 않음). 등받이·좌석은 둥근 모서리."""
    lay = canvas(W, H)
    put(lay, S([(452, 266), (452, 234), (456, 224), (465, 218), (478, 216), (486, 216), (499, 218), (508, 224), (512, 234),
                (512, 266)]), "K", None, mono)
    lines(lay, [(O([(458, 233), (458, 245), (459, 258)], 0), "2", 2)], mono)
    rr(lay, 479, 270, 485, 292, 2, "K", None)
    pfill(lay, 480, 270, 481, 292, "2")
    rr(lay, 462, 292, 502, 295, 2, "K", None)
    for x in (462, 480, 500):
        rr(lay, x - 1, 295, x + 2, 298, 1, "2", "K")
    rr(lay, 454, 262, 510, 272, 4, "4", "K")
    return lay


def hacker_body(mono=False, detail=True):
    """몸통·팔·후드(숨쉬기로 1px 움직임). 몸이 왼쪽으로 돌아 오른쪽 어깨가 넓게 보인다."""
    lay = canvas(W, H)
    # 팔꿈치(몸 양옆)
    for (pts, lit) in ((S([(450, 241), (454, 234), (460, 232), (463, 240), (462, 254), (458, 262), (452, 260), (449, 252)]), False),
                       (S([(506, 232), (513, 235), (518, 243), (518, 253), (514, 262), (508, 261), (506, 252)]), True)):
        am = put(lay, pts, "4", "K", mono)
        if lit and detail and not mono:
            shade(lay, am, [(512, 233), (520, 240), (519, 264), (511, 262)], "2", mono)
    bm = put(lay, S([(456, 273, "c"), (455, 249), (457, 238), (463, 230), (472, 225), (484, 223), (496, 225), (505, 230),
                     (510, 238), (512, 249), (511, 273, "c")]), "4", "K", mono)
    if detail and not mono:
        shade(lay, bm, [(488, 224), (505, 230), (510, 238), (512, 249), (511, 273), (493, 273)], "K", mono)       # 오른쪽 어두운 면
        shade(lay, bm, [(457, 236), (463, 229), (473, 224), (473, 228), (460, 240)], "2", mono)                   # 왼쪽 어깨 밝은 면
        lines(lay, [(O([(468, 240), (470, 255), (469, 268)], 0), "K", 1),
                    (O([(478, 232), (477, 250), (478, 268)], 0), "K", 1),                  # 등 가운데 솔기(몸이 왼쪽으로 돈 위치)
                    (O([(497, 238), (498, 252), (496, 266)], 0), "K", 1),
                    (O([(462, 234), (472, 229), (486, 228)], 0), "3", 1), ([(467, 242), (467, 251)], "2", 1),
                    ([(459, 247), (460, 262)], "2", 1)], mono)
    # 후드(목 뒤 접힘)
    hd = put(lay, S([(463, 226), (465, 218), (473, 213), (484, 212), (494, 214), (500, 220), (500, 229), (492, 235), (480, 236),
                     (468, 233)]), "2", "K", mono)
    if detail and not mono:
        shade(lay, hd, [(463, 226), (501, 226), (501, 237), (463, 237)], "4", mono)
        shade(lay, hd, [(490, 212), (501, 214), (501, 235), (490, 235)], "4", mono)
        lines(lay, [(O([(469, 226), (480, 222), (493, 226)], 0), "K", 1), (O([(467, 219), (472, 216)], 0), "3", 1)], mono)
    return lay


# 얼굴(왼쪽을 향한 3/4 옆얼굴): 앞머리선 y≈193, 눈 y≈199~203, 코 끝 (461,205), 입 y≈210, 턱 y≈217
def hacker_head(mono=False, detail=True, eye=0):
    """머리(단발 다발·천사 링)·헤드셋(밴드·귀 컵·마이크 붐·케이블)·옆얼굴."""
    lay = canvas(W, H)
    # 머리: 턱선 단발(둥근 뒤통수 + 끝이 갈라진 아랫선)
    hair = put(lay, S([(465, 203), (465, 194), (469, 187), (476, 183), (485, 181), (494, 183), (500, 188), (503, 196),
                       (504, 205), (506, 213), (505, 220, "c"), (500, 217), (496, 222, "c"), (491, 217), (486, 222, "c"),
                       (481, 217), (476, 220, "c"), (471, 216), (467, 214)]), "H", "J", mono)
    if detail and not mono:
        shade(lay, hair, [(466, 184), (478, 181), (490, 183), (480, 192), (468, 200)], "U", mono)
        shade(lay, hair, [(497, 186), (505, 190), (508, 220), (498, 224), (498, 204)], "J", mono)
        shade(lay, hair, [(480, 219), (508, 219), (508, 225), (480, 225)], "J", mono)
        # 정수리: 가마(487,185)에서 퍼지는 어두운 틈 3줄
        for ex, ey in ((476, 198), (488, 200), (499, 197)):
            curve(lay, [(487, 186), ((487 + ex) // 2, 192), (ex, ey)], "J")
        # 뒷머리 다발 4개(끝은 갈라짐)
        for (bx, by) in ((500, 216), (491, 218), (481, 217), (473, 214)):
            curve(lay, [(bx + (bx - 487) // 4, 203), (bx, by - 6), (bx, by)], "J")
            curve(lay, [(bx + (bx - 487) // 4 + 1, 203), (bx + 1, by - 6)], "U")
    # 옆얼굴(고개를 왼쪽으로 돌려 눈·코·입·턱선이 보임) — 앞머리와 귀 컵 아래에 깔림
    face = put(lay, S([(468, 193), (466, 197), (466, 200), (461, 205, "c"), (464, 206), (463, 208), (465, 209), (463, 212),
                       (464, 215), (467, 218), (473, 219), (480, 217), (486, 211), (488, 205), (488, 196), (480, 191)]), "S", "s", mono)
    if detail and not mono:
        shade(lay, face, [(476, 206), (489, 204), (489, 219), (474, 220)], "s", mono)              # 볼~턱 아래 그림자(1단)
        shade(lay, face, [(482, 196), (489, 196), (489, 212), (483, 212)], "s", mono)              # 귀 앞 그림자(1단)
        # 눈썹(담담한 일자에 가까운 곡선), 눈(살짝 내려 뜸, 먼 쪽 눈의 속눈썹 끝만 코 옆에 1~2px)
        curve(lay, [(469, 196), (472, 195), (475, 196)], "J")
        eye_down(lay, 470, 198, 5, 6, -1, {0: 1, 1: 3, 2: 2}[eye])
        pset(lay, 467, 198, "K"); pset(lay, 466, 197, "K")
        dots(lay, [(464, 206)], "s"); dots(lay, [(466, 203), (466, 204)], "l")                  # 코 옆 하이라이트
        dots(lay, [(464, 210), (465, 210)], "s"); dots(lay, [(466, 210)], "m")                   # 입술 끝 2~3px(피부보다 어두운 색)
        dots(lay, [(472, 207), (473, 207), (474, 207)], "s")                                      # 볼 그림자
        # 림 라이트: 얼굴 오른쪽(귀 컵 앞 볼)과 턱선에 모니터 청록 빛
        dots(lay, [(472, 219), (474, 219), (476, 218)], "c")                                       # 턱 끝 바깥 외곽 림 라이트
    # 앞머리(옆으로 쓸어 넘긴 다발, 끝이 뾰족)
    front = put(lay, S([(466, 202, "c"), (464, 193), (467, 186), (475, 182), (485, 180), (494, 183), (499, 188), (493, 190),
                        (489, 195), (485, 192, "c"), (482, 195), (478, 192, "c"), (475, 195), (472, 191, "c"), (469, 195),
                        (467, 198, "c"), (466, 196)], 8), "H", "J", mono)
    if detail and not mono:
        shade(lay, front, [(488, 186), (500, 188), (494, 196), (486, 196)], "J", mono)
        for (nx, ny, tx, ty) in ((485, 192, 487, 185), (478, 192, 478, 185), (472, 191, 469, 187)):
            curve(lay, [(nx, ny), ((nx + tx) // 2, (ny + ty) // 2 + 1), (tx, ty)], "J")
            curve(lay, [(nx + 1, ny - 1), (tx + 1, ty + 2)], "U")
        # 천사 링: 둥근 머리를 따라 휘는 가로 하이라이트 띠
        ring = [(467, 192), (470, 188), (475, 185), (482, 183), (489, 184), (495, 187)]
        curve(lay, ring, "U")
        curve(lay, [(p[0], p[1] - 1) for p in ring[1:-1]], "C")
    # 헤드셋 밴드(머리 위 호) + 낡은 테이프
    band = canvas(W, H)
    draw_ellipse(band, (464, 176, 506, 218), "K", width=5)
    draw_ellipse(band, (465, 177, 505, 217), "2", width=3)
    draw_ellipse(band, (465, 177, 505, 217), "3", width=1)
    bp = band.load()
    for y in range(H):
        for x in range(W):
            if y >= 200 or x < 468:
                bp[x, y] = (0, 0, 0, 0)
    if not mono:
        lay.alpha_composite(band)
    else:
        for y in range(H):
            for x in range(W):
                if band.getpixel((x, y))[3]:
                    lay.putpixel((x, y), PIX["K"])
    if detail and not mono:
        rr(lay, 482, 176, 490, 180, 1, "h", None); pfill(lay, 482, 176, 490, 176, "Y")           # 낡은 테이프
        dots(lay, [(484, 178), (488, 179)], "D")
        pfill(lay, 500, 188, 503, 192, "h"); pset(lay, 501, 190, "D")                              # 오른쪽 테이프 조각
    # 귀 컵(둥근 사각형, 한쪽만 보임) + 컵 가운데 면·표시등
    cup = rr(lay, 487, 197, 497, 212, 3, "2", "K")
    if detail and not mono:
        shade(lay, cup, [(487, 197), (490, 197), (490, 212), (487, 212)], "3", mono)
        shade(lay, cup, [(495, 204), (498, 204), (498, 212), (495, 212)], "4", mono)
        rr(lay, 490, 200, 494, 209, 1, "4", None); pset(lay, 492, 204, "c")
        lines(lay, [([(489, 198), (495, 198)], "W", 1)], mono)
        # 마이크 붐: 귀 컵 아래에서 턱 밑을 지나 입 쪽으로
        curve(lay, [(490, 212), (487, 218), (481, 222), (474, 223)], "3")
        rr(lay, 468, 221, 473, 225, 1, "2", "K"); pset(lay, 470, 222, "R")
        # 케이블: 귀 컵 아래에서 어깨 뒤로
        lines(lay, [(O([(495, 212), (499, 221), (497, 231)], 0), "2", 2)], mono)
    return lay


def hacker_frame(breath=0, bob=0, eye=0, mono=False, detail=True, rim=True):
    """한 프레임(640×360 투명 판). breath 0/1 = 윗몸 1px 올라감, bob = 머리가 내려간 픽셀(0/1), eye 0=뜸 1=반쯤 2=감음."""
    lay = canvas(W, H)
    lay.alpha_composite(hacker_low(mono, detail))
    lay.alpha_composite(hacker_body(mono, detail), (0, -breath))
    lay.alpha_composite(hacker_head(mono, detail, eye), (0, -breath + bob))
    if rim and not mono:
        rim_right(lay, skip=lambda x, y: y > 258)       # 의자 다리·바퀴는 림 라이트 없음
        rim_top(lay, skip=lambda x, y: y > 258)
    silhouette_outline(lay, "K")
    return lay


# =====================================================================
# 움직임(30 FPS) — 타임라인·시트
# =====================================================================
FPS = 30
LOOP_TICKS = 360          # 12초: 조력자 12초 / 주인공 2.4초×5 / 빛 1.5초×8 / LED 1.33초×9 가 모두 나누어떨어짐


def runs(states):
    """틱마다의 상태 목록 → [(상태, 유지 틱)] (같은 상태가 이어지면 합친다)."""
    out = []
    for st in states:
        if out and out[-1][0] == st:
            out[-1][1] += 1
        else:
            out.append([st, 1])
    return [(a, b) for a, b in out]


def helper_timeline():
    """숨쉬기 2초(1초씩 위/아래), 눈 깜박임 4초마다 3프레임(반쯤 2틱·감음 3틱·반쯤 2틱), 태블릿 두드리기 3초마다(5틱)."""
    states = []
    for t in range(LOOP_TICKS):
        b = 0 if (t % 60) < 30 else 1
        e = 0
        for bs in (100, 220, 340):
            d = t - bs
            if 0 <= d < 2:
                e = 1
            elif 2 <= d < 5:
                e = 2
            elif 5 <= d < 7:
                e = 1
        tp = 1 if any(0 <= t - ts < 5 for ts in (45, 135, 225, 315)) else 0
        states.append((b, e, tp))
    return runs(states)


def hacker_timeline():
    """숨쉬기 2.4초(72틱, 36틱씩 위/아래) + 머리 1px 까딱 4프레임(각 9틱) + 눈 깜박임 5초(150틱) 간격 3프레임
    (반쯤 2·감음 3·반쯤 2틱). 12초(360틱) 한 바퀴에 깜박임 2번(100틱·250틱)."""
    seq = (0, 1, 1, 0)
    states = []
    for t in range(LOOP_TICKS):
        e = 0
        for bs in (100, 250):
            d = t - bs
            if 0 <= d < 2:
                e = 1
            elif 2 <= d < 5:
                e = 2
            elif 5 <= d < 7:
                e = 1
        states.append((0 if (t % 72) < 36 else 1, seq[(t // 9) % 4], e))
    return runs(states)


GLOW_LEVELS = (0.85, 1.0, 1.15)               # 모니터 빛 세기 3단(0단 = L0에 구워진 기본)
GLOW_SEQ = ((0, 12), (1, 5), (2, 8), (1, 5), (0, 10), (1, 5))     # (단, 유지 틱) 합 45틱 = 1.5초
LED_HOLDS = (11, 7, 13, 9)                     # 합 40틱


_OCC = {}


def occluded(x, y, w, h):
    """LED 칸이 책상·모니터·소품에 가려지는가(시안 C에서 책상이 랙 아래쪽을 덮는다)."""
    if not _OCC:
        occ = canvas(W, H)
        occ.alpha_composite(desk_layer())
        occ.alpha_composite(dr.c_monitors()[0])
        occ.alpha_composite(props_layer())
        _OCC["a"] = occ.getchannel("A")
    a = _OCC["a"]
    return any(a.getpixel((x + i, y + j)) for j in range(h) for i in range(w))


def led_cells():
    """서버 랙·공유기 LED 칸(가려지지 않는 것만): (x, y, 폭, 높이, 켜진 색)."""
    cells = []
    for k, ry in enumerate(RACK_ROWS):
        for i in range(3):
            cells.append((603 + i * 4, ry + 3, 2, 2, RACK_LED_COL[(k + i) % 6]))
    for i in range(4):
        cells.append((600 + i * 6, 174, 2, 1, ["G", "G", "G", "Y"][i]))
    return [c for c in cells if not occluded(*c[:4])]


def led_frame(f):
    """f번째(0~3) LED 켜짐 무늬(L4, 640×360 투명 판). 꺼진 칸은 L0의 어두운 색이 보인다."""
    lay = canvas(W, H)
    for j, (x, y, w, h, col) in enumerate(led_cells()):
        if ((j * 3 + f * 5 + (j >> 1)) % 7) < 4:
            c = col
            if (j + f * 2) % 11 == 0:
                c = "R" if col != "R" else "Y"          # 가끔 다른 색으로 깜박
            pfill(lay, x, y, x + w - 1, y + h - 1, c)
    return lay


def crop_to(im, box):
    return im.crop(box)


def sheet_save(frames, holds, rel, box, states, status="본그림", extra=None):
    """프레임(이미 box 로 자른 것)들을 격자 시트로 저장하고 같은 이름 .json 에 유지 시간 표를 적는다."""
    w, h = frames[0].size
    cols = max(1, min(len(frames), 8, 2048 // w))
    rows = (len(frames) + cols - 1) // cols
    assert rows * h <= 2048
    sh = Image.new("RGBA", (w * cols, h * rows), (0, 0, 0, 0))
    rects = []
    for i, f in enumerate(frames):
        x, y = (i % cols) * w, (i // cols) * h
        sh.alpha_composite(f, (x, y))
        rects.append([x, y, w, h])
    meta = {"status": status, "frame_size": [w, h], "frames": len(frames), "cols": cols, "rects": rects,
            "fps": FPS, "holds": holds, "total_ticks": sum(holds), "loop": True, "pivot": "top-left",
            "origin": [box[0], box[1]], "canvas": [W, H], "states": states}
    if extra:
        meta.update(extra)
    brief = {k: meta[k] for k in ("status", "frame_size", "frames", "cols", "fps", "holds", "total_ticks", "loop", "pivot",
                                  "origin", "canvas")}
    brief["detail"] = rel + "_sheet.json"          # 프레임 좌표·상태 표는 같은 이름 .json
    p = save(sh, rel + "_sheet.png", brief)
    jp = p.with_suffix(".json")
    jp.write_text(json.dumps(meta, ensure_ascii=False, indent=2), encoding="utf-8")
    return [p, jp]


def union_box(ims, margin=2):
    bb = None
    for im in ims:
        b = im.getbbox()
        if b:
            bb = b if bb is None else (min(bb[0], b[0]), min(bb[1], b[1]), max(bb[2], b[2]), max(bb[3], b[3]))
    return (max(0, bb[0] - margin), max(0, bb[1] - margin), min(W, bb[2] + margin), min(H, bb[3] + margin))


def alpha_set(im, box=None):
    a = im.getchannel("A")
    bb = box or (0, 0, im.width, im.height)
    px = a.load()
    return {(x, y) for y in range(bb[1], bb[3]) for x in range(bb[0], bb[2]) if px[x, y]}


# =====================================================================
# 레이어 만들기
# =====================================================================
def make_layers():
    """모든 레이어를 640×360 판으로 만든다(상태별 캐시). 사전 반환."""
    L = {}
    base, smask = lit_scene(GLOW_LEVELS[0])
    L["L0"] = base
    # L5 창틀: 창 영역의 불투명 픽셀 중 모니터·공유기에 가려지는 곳은 뺀다
    cover = dr.mask_union(dr.pmask(list(C_MAIN)), dr.pmask(list(C_SIDE)), M([(590, 166), (632, 166), (632, 180), (590, 180)]))
    l5 = canvas(W, H)
    bp, lp = base.load(), l5.load()
    for y in range(WIN_OUTER[1] - 0, SILL[3] + 1):
        for x in range(SILL[0], SILL[2] + 1):
            if bp[x, y][3] and cover.getpixel((x, y)) < 128:
                lp[x, y] = bp[x, y]
    L["L5"] = l5
    # L1 조력자
    hel = {}
    for st in {s for s, _ in helper_timeline()}:
        hel[st] = helper_frame(breath=st[0], eye=st[1], tap=st[2])
    L["helper"] = hel
    hk = {}
    for st in {s for s, _ in hacker_timeline()}:
        hk[st] = hacker_frame(breath=st[0], bob=st[1], eye=st[2])
    L["hacker"] = hk
    # L3 모니터 빛: 세기 s 로 다시 구운 장면과 L0 의 차이만 모은 불투명 점(디더)
    hk_union = set()
    for f in hk.values():
        hk_union |= alpha_set(f)
    led_px = set()
    for (x, y, w, h, c) in led_cells():
        for yy in range(h):
            for xx in range(w):
                led_px.add((x + xx, y + yy))
    keep_out = hk_union | alpha_set(l5) | led_px
    sk = lambda x, y: smask.getpixel((x, y)) > 128
    glow = {}
    gb = GLOW_BOX
    for gi in (1, 2):
        sc, _ = lit_scene(GLOW_LEVELS[gi])
        ov = canvas(W, H)
        so, bo, oo = sc.load(), base.load(), ov.load()
        for y in range(gb[1], gb[3]):
            for x in range(gb[0], gb[2]):
                if so[x, y] != bo[x, y] and so[x, y][3] and bo[x, y][3] and (x, y) not in keep_out and not sk(x, y):
                    oo[x, y] = so[x, y]
        glow[gi] = ov
    glow[0] = canvas(W, H)
    L["glow"] = glow
    L["led"] = {f: led_frame(f) for f in range(4)}
    L["backdrop"] = backdrop()
    return L


def tick_states():
    """틱마다 각 레이어의 프레임 번호."""
    ht = helper_timeline()
    kt = hacker_timeline()
    h_idx, k_idx, g_idx, l_idx = [], [], [], []
    for st, hold in ht:
        h_idx += [st] * hold
    for st, hold in kt:
        k_idx += [st] * hold
    for _ in range(LOOP_TICKS // 45):
        for gi, hold in GLOW_SEQ:
            g_idx += [gi] * hold
    for _ in range(LOOP_TICKS // 40):
        for f, hold in enumerate(LED_HOLDS):
            l_idx += [f] * hold
    assert len(h_idx) == len(k_idx) == len(g_idx) == len(l_idx) == LOOP_TICKS
    return h_idx, k_idx, g_idx, l_idx


def compose(L, hs, ks, gi, li, menu=None):
    im = L["backdrop"].copy()
    im.alpha_composite(L["L0"])
    im.alpha_composite(L["helper"][hs])
    im.alpha_composite(L["hacker"][ks])
    im.alpha_composite(L["glow"][gi])
    im.alpha_composite(L["led"][li])
    im.alpha_composite(L["L5"])
    if menu is not None:
        im.alpha_composite(menu)
    return im


def tone_gif(frames, durations, path):
    """모든 프레임이 같은 팔레트를 쓰는 GIF(무보간 ×2 확대본)."""
    first = frames[0].convert("RGB").convert("P", palette=Image.ADAPTIVE, colors=200)
    outs = [first] + [f.convert("RGB").quantize(palette=first, dither=Image.Dither.NONE) for f in frames[1:]]
    outs[0].save(path, save_all=True, append_images=outs[1:], duration=durations, loop=0, disposal=1, optimize=False)


# =====================================================================
# 빌드
# =====================================================================
def build():
    out = EXPORT / "preview"
    out.mkdir(parents=True, exist_ok=True)
    made = []
    with ext_palette():
        L = make_layers()
        h_idx, k_idx, g_idx, l_idx = tick_states()
        # ---- 팔레트 검사 ----
        for name in ("L0", "L5"):
            assert not (set(L[name].getdata()) - set(BASE.values()) - {(0, 0, 0, 0)}), name
        used = set()
        for grp in ("helper", "hacker"):
            for st, im in L[grp].items():
                bad = bad_colors(im)
                assert not bad, (grp, st, bad)
                used |= {c for c in im.getdata() if c[3]}
        for grp in ("glow", "led"):
            for st, im in L[grp].items():
                assert not bad_colors(im), (grp, st)
        used_ext = sorted(k for k, v in EXT.items() if v in used)
        print(f"  [팔레트] 추가 색 사용: {used_ext} (H2 4색 A·O·o·P + 새로 더한 {list(EXT_NEW)})")
        # ---- L0 / L5 ----
        made.append(save(L["L0"], "scenes/main_lobby_L0.png", {"status": "본그림", "layer": "L0 방(창 유리 투명)", "glass": list(WIN_GLASS), "mullions": [list(r) for r in MULLIONS]}))
        made.append(save(L["L5"], "scenes/main_lobby_L5.png", {"status": "본그림", "layer": "L5 창틀·창살·창턱(환경 이펙트 위)"}))
        # ---- L1 조력자 시트 ----
        ht = helper_timeline()
        hbox = union_box(list(L["helper"].values()))
        frames = [L["helper"][st].crop(hbox) for st, _ in ht]
        holds = [h for _, h in ht]
        states = [{"breath": s[0], "eye": ["open", "half", "closed"][s[1]], "tap": s[2]} for s, _ in ht]
        made += sheet_save(frames, holds, "anim/main_lobby_helper", hbox, states,
                           extra={"note": "숨쉬기 2초(30틱씩 위/아래) · 눈 깜박임 4초마다(반쯤 2·감음 3·반쯤 2틱) · 태블릿 두드리기 3초마다(5틱). 12초 한 바퀴."})
        # ---- L2 주인공 시트 ----
        kt = hacker_timeline()
        kbox = union_box(list(L["hacker"].values()))
        frames = [L["hacker"][st].crop(kbox) for st, _ in kt]
        holds = [h for _, h in kt]
        states = [{"breath": s[0], "head_down_px": s[1], "eye": ["open", "half", "closed"][s[2]]} for s, _ in kt]
        made += sheet_save(frames, holds, "anim/main_lobby_hacker", kbox, states,
                           extra={"note": "숨쉬기 2.4초(72틱, 36틱씩) · 머리 까딱 4프레임(9틱씩) · 눈 깜박임 5초 간격(반쯤 2·감음 3·반쯤 2틱) — 12초(360틱) 한 바퀴에 깜박임 2번."})
        # ---- L3 빛 시트 ----
        frames = [L["glow"][gi].crop(GLOW_BOX) for gi, _ in GLOW_SEQ]
        holds = [h for _, h in GLOW_SEQ]
        states = [{"level": gi, "scale": GLOW_LEVELS[gi]} for gi, _ in GLOW_SEQ]
        made += sheet_save(frames, holds, "anim/main_lobby_glow", GLOW_BOX, states,
                           extra={"note": "0단=덧칠 없음(L0 기본), 1·2단=모니터 빛이 한 단계씩 더 밝아지는 디더 점. 인물·창틀·LED·모니터 화면 자리는 비움."})
        # ---- L4 LED 시트 ----
        frames = [L["led"][f].crop(LED_BOX) for f in range(4)]
        made += sheet_save(frames, list(LED_HOLDS), "anim/main_lobby_led", LED_BOX,
                           [{"pattern": f} for f in range(4)], extra={"note": "서버 랙 3×8칸 + 공유기 4칸. 꺼진 칸은 L0의 어두운 색."})
        # ---- 미리보기 ----
        menu = dr.menu_overlay(0)
        hs0, ks0, gi0, li0 = h_idx[0], k_idx[0], g_idx[0], l_idx[0]
        still = compose(L, hs0, ks0, gi0, li0)
        withmenu = compose(L, hs0, ks0, gi0, li0, menu)
        assert not bad_colors(still) and not bad_colors(withmenu)
        files = {"main_lobby_final_x3.png": upscale(still, 3), "main_lobby_final_menu_x3.png": upscale(withmenu, 3),
                 "main_lobby_helper_x6.png": upscale(still.crop((176, 156, 256, 300)), 6),
                 "main_lobby_hacker_x6.png": upscale(still.crop((444, 172, 524, 276)), 6)}
        for name, im in files.items():
            im.save(out / name)
            made.append(out / name)
        # GIF: 상태가 바뀌는 순간마다 한 장(12초 = 360틱, ×2)
        keys = [(h_idx[t], k_idx[t], g_idx[t], l_idx[t]) for t in range(LOOP_TICKS)]
        gruns = runs(keys)
        frames, durs, cache = [], [], {}
        for key, hold in gruns:
            if key not in cache:
                cache[key] = upscale(compose(L, *key), 2)
            frames.append(cache[key])
            durs.append(int(round(hold * 1000 / FPS / 10.0)) * 10)
        gif = out / "main_lobby_final.gif"
        tone_gif(frames, durs, gif)
        made.append(gif)
        print(f"  [gif] {len(frames)}장, {sum(hold for _, hold in gruns) / FPS:.1f}초")
        # ---- 좌표·프레임 요약 ----
        print(f"  [창 유리 투명 영역] x{WIN_GLASS[0]}~{WIN_GLASS[2]}, y{WIN_GLASS[1]}~{WIN_GLASS[3]} (창살 제외: {MULLIONS}), 모니터가 덮는 부분은 불투명")
        print(f"  [모니터 화면 영역] 메인 {C_SCR} / 세로 보조 {C_SIDE_SCR}")
        print(f"  [시트 영역] 조력자 {hbox} · 주인공 {kbox} · 빛 {GLOW_BOX} · LED {LED_BOX}")
    assert all(k not in PIX for k in EXT)
    return made
