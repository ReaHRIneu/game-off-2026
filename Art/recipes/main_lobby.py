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
    # 침대
    pfill(im, 150, 262, 334, 292, "d"); dr.dith(im, (151, 263, 333, 291), "K", 9); pbox(im, 150, 262, 334, 292, "K")
    pfill(im, 154, 292, 160, 296, "K"); pfill(im, 324, 292, 330, 296, "K")
    pfill(im, 152, 240, 332, 264, "j"); pbox(im, 152, 240, 332, 264, "K")
    pfill(im, 170, 232, 332, 252, "B"); pbox(im, 170, 232, 332, 252, "K")
    pfill(im, 171, 233, 331, 235, "b")
    for x in range(180, 330, 22):
        draw_line(im, [(x, 236), (x + 6, 250)], "K")
    pfill(im, 154, 226, 190, 244, "2"); pbox(im, 154, 226, 190, 244, "K")
    pfill(im, 155, 227, 189, 229, "3"); pfill(im, 184, 230, 189, 243, "4")
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


def lit_scene(s=0.85, glass=True):
    """방 + 가구 + 소품에 빛(차가운 모니터 빛 세기 s, 스탠드, 서버 랙)을 디더로 구운 장면. 인물 제외."""
    scene = canvas(W, H, PIX["k"])
    scene.alpha_composite(room_layer())
    scene.alpha_composite(dr.c_desk())
    mons, smask = dr.c_monitors()
    scene.alpha_composite(mons)
    scene.alpha_composite(dr.c_props())
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
# L1 조력자 (H2 스트리트 러너, 침대 머리 쪽에 걸터앉음)
# =====================================================================
# 비례: 얼굴(앞머리선~턱) 28px, 머리 정수리~턱 36px, 앉은키(엉덩이~정수리) 약 80px.


def helper_low(mono=False, detail=True):
    """엉덩이·다리(움직이지 않음)."""
    lay = canvas(W, H)
    # 오른 다리(화면 오른쪽): 무릎에서 바깥으로 벌어져 내려와 발이 침대 프레임 아랫단에 걸림
    shin_r = put(lay, [(217, 262), (230, 262), (234, 271), (238, 280), (233, 285), (227, 279), (219, 271)], "P", "p", mono)
    shade(lay, shin_r, [(226, 262), (230, 262), (234, 271), (238, 280), (233, 285), (229, 276)], "p", mono)
    shoe_r = put(lay, [(231, 281), (239, 279), (247, 283), (248, 290), (240, 292), (231, 290)], "W", "3", mono)
    shade(lay, shoe_r, [(231, 286), (248, 286), (248, 292), (231, 292)], "3", mono)
    put(lay, [(231, 289), (248, 288), (248, 292), (231, 292)], "O", "o", mono)
    lines(lay, [([(241, 283), (246, 286)], "O", 1)], mono)
    # 왼 다리(화면 왼쪽): 곧게 내려오고 발끝은 바깥(왼쪽)
    shin_l = put(lay, [(200, 262), (212, 262), (211, 274), (210, 285), (203, 285), (202, 274)], "P", "p", mono)
    shade(lay, shin_l, [(208, 262), (212, 262), (211, 274), (210, 285), (207, 285), (208, 272)], "p", mono)
    shoe_l = put(lay, [(190, 291), (197, 287), (203, 284), (211, 285), (212, 294), (212, 296), (190, 296)], "W", "3", mono)
    shade(lay, shoe_l, [(190, 292), (212, 292), (212, 296), (190, 296)], "3", mono)
    put(lay, [(190, 293), (212, 293), (212, 296), (190, 296)], "O", "o", mono)
    lines(lay, [([(194, 288), (199, 286)], "O", 1), ([(204, 286), (210, 287)], "3", 1)], mono)
    # 엉덩이·허벅지
    put(lay, [(200, 240), (234, 240), (236, 250), (197, 250)], "P", "p", mono)
    th_l = put(lay, [(197, 246), (214, 248), (214, 257), (212, 264), (201, 265), (196, 258)], "P", "p", mono)
    th_r = put(lay, [(214, 248), (236, 246), (239, 258), (232, 265), (216, 264), (214, 257)], "P", "p", mono)
    shade(lay, th_l, [(208, 248), (214, 248), (214, 257), (212, 264), (206, 264), (209, 256)], "p", mono)
    shade(lay, th_r, [(232, 247), (237, 246), (240, 258), (232, 265), (228, 264), (234, 256)], "p", mono)
    if detail and not mono:
        pfill(lay, 201, 240, 234, 241, "2"); pfill(lay, 201, 240, 234, 240, "3")                    # 허리띠
        bag = put(lay, [(229, 251), (239, 250), (241, 259), (231, 261)], "3", "K", mono)           # 허리 가방(오른쪽 엉덩이 옆)
        shade(lay, bag, [(236, 250), (241, 259), (231, 261), (234, 256)], "2", mono)
        lines(lay, [([(230, 254), (240, 253)], "Y", 1), ([(236, 241), (237, 250)], "o", 2)], mono)
        put(lay, [(195, 251), (202, 251), (202, 260), (196, 260)], "P", "p", mono)       # 카고 주머니
        lines(lay, [([(195, 253), (202, 253)], "p", 1), ([(199, 256), (199, 258)], "y", 1)], mono)
        lines(lay, [([(205, 252), (204, 262)], "p", 1), ([(222, 252), (221, 262)], "p", 1)], mono)   # 무릎 쪽 주름
        dots(lay, [(202, 262), (203, 262), (219, 261), (220, 261)], "h", mono)
    return lay


def eye_shape(lay, x0, y0, w, outer, state, mono=False, h=8):
    """눈 하나. outer = -1(바깥이 왼쪽) / +1(바깥이 오른쪽). state 0=뜸, 1=반쯤, 2=감음.
    높이 8(윗속눈썹 줄 + 눈동자·흰자 7줄) + 아래 속눈썹 점. 윗속눈썹은 눈꼬리 쪽이 2줄로 굵다."""
    if mono:
        return
    xs = list(range(x0, x0 + w))
    ox = x0 if outer < 0 else x0 + w - 1          # 눈꼬리 쪽 끝 x
    ix = x0 + w - 1 if outer < 0 else x0          # 눈머리(코 쪽) 끝 x
    if state == 2:                                  # 감은 눈: 아래로 굽은 선 + 꼬리
        for i, x in enumerate(xs):
            t = (i if outer > 0 else w - 1 - i)     # 0=안쪽
            dy = 0 if (1 <= t <= w - 3) else -1
            pset(lay, x, y0 + 5 + dy, "K")
        pset(lay, ox + outer, y0 + 4, "K")
        pset(lay, ox + 2 * outer, y0 + 3, "K")
        return
    top = 0 if state == 0 else 2                    # 반쯤 감기면 눈꺼풀이 2줄 더 내려옴
    ys = y0 + top                                   # 윗속눈썹 줄
    # 흰자(아래가 둥근 모양)
    for y in range(ys + 1, y0 + h):
        a, b = x0, x0 + w - 1
        if y == y0 + h - 1:
            a, b = x0 + 2, x0 + w - 3
        elif y == y0 + h - 2:
            a, b = x0 + 1, x0 + w - 2
        pfill(lay, a, y, b, y, "W")
    # 홍채: 폭 w-3, 코 쪽으로 약간 치우침. 위 어둡게(d) → 가운데(D) → 아래 밝게(A, h)
    iw = w - 3
    i0 = x0 + 1 if outer > 0 else x0 + 2
    n = y0 + h - (ys + 1)
    for k, y in enumerate(range(ys + 1, y0 + h)):
        if y == y0 + h - 1:
            col = "h"
        elif k < max(2, n * 2 // 7 + 1):
            col = "d"
        elif k < max(3, n * 4 // 7 + 1):
            col = "D"
        else:
            col = "A"
        a, b = i0, i0 + iw - 1
        if y == y0 + h - 1:
            a, b = i0 + 1, i0 + iw - 2
        pfill(lay, a, y, b, y, col)
    # 동공(2폭)
    py0 = ys + 3
    px0 = i0 + (iw - 2) // 2
    pfill(lay, px0, py0, px0 + 1, min(py0 + 2, y0 + h - 2), "K")
    # 하이라이트 2점: 큰 점(2×2, 왼쪽 위) + 작은 점(1px, 오른쪽 아래)
    pfill(lay, i0, ys + 2, i0 + 1, ys + 3, "W")
    pset(lay, i0 + iw - 2, y0 + h - 2, "W")
    # 윗속눈썹: 한 줄 + 눈꼬리 쪽 2줄, 눈꼬리에서 1px 바깥으로 뻗음
    pfill(lay, x0, ys, x0 + w - 1, ys, "K")
    for k in range(3):
        pset(lay, ox - outer * k, ys + 1, "K")
    pset(lay, ix, ys + 1, "K")
    pset(lay, ox + outer, ys, "K")
    pset(lay, ox + 2 * outer, ys - 1, "K")
    # 눈꺼풀 아래 그림자(흰자 윗줄)
    for x in xs[1:-1]:
        if lay.getpixel((x, ys + 1)) == PIX["W"]:
            pset(lay, x, ys + 1, "l")
    # 아래 속눈썹 점 1~2개(눈꼬리 쪽)
    pset(lay, ox, y0 + h, "d")
    if w > 8:
        pset(lay, ox - outer * 2, y0 + h, "s")


def helper_up(breath=0, eye=0, tap=0, mono=False, detail=True):
    """머리·몸통·팔·태블릿(숨쉬기로 함께 1px 움직이는 윗몸)."""
    lay = canvas(W, H)
    # ---- 뒷머리(머리 뒤로 늘어진 울프컷) + 왼쪽 작은 묶음 ----
    back = put(lay, [(201, 188), (200, 177), (205, 170), (212, 166), (223, 166), (231, 170), (236, 178), (237, 191),
                     (236, 200), (237, 205), (233, 207), (231, 203), (227, 207), (223, 204), (219, 208), (215, 204),
                     (211, 207), (207, 203), (204, 206), (201, 203), (200, 197)], "D", "d", mono)
    if detail and not mono:
        for x in (203, 209, 215, 221, 227, 233):
            lines(lay, [([(x, 192), (x + 1, 203)], "d", 1)], mono)
        shade(lay, back, [(198, 203), (240, 203), (240, 214), (198, 214)], "O", mono)      # 머리끝 주황
        lines(lay, [([(200, 202), (237, 202)], "d", 1)], mono)
    pony = put(lay, [(201, 185), (197, 187), (193, 193), (191, 202), (193, 209), (197, 213), (198, 205), (200, 197)], "D", "d", mono)
    if detail and not mono:
        shade(lay, pony, [(190, 203), (202, 203), (202, 215), (190, 215)], "O", mono)
        put(lay, [(194, 188), (200, 187), (200, 191), (194, 192)], "O", "o", mono)         # 주황 끈
        lines(lay, [([(197, 193), (195, 204)], "d", 1), ([(199, 196), (198, 203)], "A", 1)], mono)
    # ---- 목 ----
    neck = put(lay, [(212, 201), (223, 201), (224, 212), (211, 212)], "S", "s", mono)
    shade(lay, neck, [(216, 201), (224, 201), (224, 212), (216, 212)], "s", mono)
    shade(lay, neck, [(211, 201), (224, 201), (224, 207), (211, 207)], "s", mono)   # 턱 그림자
    # ---- 몸통: 크롭 윈드브레이커 ----
    torso = put(lay, [(199, 216), (204, 212), (210, 208), (224, 208), (230, 212), (235, 216), (236, 224), (233, 236),
                      (232, 241), (203, 241), (201, 236), (198, 224)], "O", "o", mono)
    shade(lay, torso, [(226, 210), (230, 212), (235, 216), (236, 224), (233, 236), (232, 241), (222, 241), (227, 228)], "o", mono)
    if not mono:
        dr.outline_mask(lay, torso, "d", where=lambda x, y: y < 214)
    if detail and not mono:
        pfill(lay, 202, 237, 232, 240, "o"); pfill(lay, 202, 237, 232, 237, "Y")
        lines(lay, [([(214, 216), (214, 227), (215, 237)], "K", 1),                  # 지퍼
                    ([(201, 222), (206, 226), (205, 233)], "o", 1),                   # 주름 1
                    ([(221, 222), (225, 227), (224, 234)], "o", 1),                   # 주름 2
                    ([(208, 230), (211, 235)], "o", 1)], mono)                       # 주름 3
        lines(lay, [([(202, 214), (208, 211)], "h", 1), ([(197, 222), (197, 232)], "A", 1)], mono)
        dots(lay, [(214, 215), (214, 216)], "h", mono)                                 # 지퍼 손잡이
        lines(lay, [([(233, 216), (234, 224)], "Y", 1)], mono)                         # 어깨 노랑 줄
    col = put(lay, [(209, 206), (212, 204), (224, 204), (227, 206), (228, 214), (219, 217), (209, 214)], "O", "o", mono)
    if detail and not mono:
        shade(lay, col, [(219, 204), (227, 206), (228, 214), (219, 217)], "o", mono)
        lines(lay, [([(210, 207), (213, 205)], "h", 1), ([(219, 208), (219, 216)], "K", 1)], mono)
    # ---- 팔(소매) + 태블릿 + 손 ----
    sl = put(lay, [(199, 216), (195, 224), (192, 233), (196, 239), (205, 244), (209, 240), (203, 234), (202, 225)], "O", "o", mono)
    shade(lay, sl, [(196, 232), (199, 238), (205, 244), (209, 240), (203, 235)], "o", mono)
    sr = put(lay, [(235, 216), (238, 224), (241, 233), (237, 239), (228, 244), (224, 240), (231, 234), (232, 225)], "O", "o", mono)
    shade(lay, sr, [(237, 226), (241, 233), (237, 239), (228, 244), (231, 238), (234, 230)], "o", mono)
    if detail and not mono:
        pfill(lay, 199, 238, 205, 240, "Y"); pfill(lay, 226, 238, 232, 240, "Y")
    if detail and not mono:
        lines(lay, [([(203, 225), (206, 234), (209, 240)], "o", 2), ([(231, 225), (228, 234), (225, 240)], "o", 2)], mono)
    tb = put(lay, [(207, 231), (228, 231), (229, 247), (206, 247)], "4", "K", mono)
    if detail and not mono:
        pfill(lay, 208, 232, 227, 233, "2"); lines(lay, [([(207, 231), (228, 231)], "C", 1)], mono)
        shade(lay, tb, [(222, 232), (228, 231), (229, 247), (219, 247)], "K", mono)
        pfill(lay, 209, 235, 210, 236, "3")
        lines(lay, [([(212, 242), (221, 242)], "2", 1)], mono)
    hl = put(lay, [(203, 240), (208, 239), (211, 245), (207, 249), (202, 246)], "S", "s", mono)
    hr = put(lay, [(226, 240), (231, 238), (234, 244), (230, 249), (225, 246)], "S", "s", mono)
    if detail and not mono:
        for yy in (241, 243, 245):
            lines(lay, [([(209, yy), (213, yy)], "S", 1), ([(209, yy + 1), (213, yy + 1)], "s", 1)], mono)
            pset(lay, 214, yy, "s")
        for yy in (240, 242, 244):
            up = -1 if (tap and yy == 240) else 0
            lines(lay, [([(223, yy + up), (227, yy + up)], "S", 1), ([(223, yy + up + 1), (227, yy + up + 1)], "s", 1)], mono)
            pset(lay, 222, yy + up, "s")
        ty = 230 if not tap else 232
        put(lay, [(224, ty - 2), (229, ty - 2), (230, ty + 1), (225, ty + 2)], "S", "s", mono)
        pset(lay, 226, ty - 1, "l")
        if tap:
            lines(lay, [([(222, 230), (224, 229)], "C", 1)], mono)
    # ---- 얼굴 ----
    face_pts = [(204, 181), (208, 177), (214, 176), (222, 176), (228, 179), (231, 185), (231, 192), (229, 198),
                (225, 202), (219, 205), (213, 203), (208, 199), (204, 194), (203, 187)]
    face = put(lay, face_pts, "S", "s", mono)
    put(lay, [(201, 189), (203, 188), (203, 197), (201, 196)], "S", "s", mono)         # 왼쪽 귀(이어피스 자리)
    if detail and not mono:
        shade(lay, face, [(226, 178), (232, 184), (232, 192), (229, 198), (225, 202), (222, 204), (225, 196), (227, 188)], "s", mono)
        shade(lay, face, [(203, 178), (232, 178), (232, 184), (203, 184)], "s", mono)       # 앞머리 그림자
        shade(lay, face, [(209, 203), (222, 205), (219, 206), (210, 205)], "s", mono)
        pfill(lay, 200, 190, 202, 195, "K"); pfill(lay, 201, 191, 201, 194, "3"); pset(lay, 201, 192, "G")   # 이어피스
        lines(lay, [([(200, 196), (198, 202)], "K", 1)], mono)
        lines(lay, [([(206, 185), (209, 184), (213, 185)], "d", 1), ([(223, 184), (226, 183), (229, 185)], "d", 1)], mono)
        eye_shape(lay, 206, 187, 9, -1, eye)
        eye_shape(lay, 223, 187, 7, +1, eye)
        dots(lay, [(219, 197)], "s"); dots(lay, [(218, 196)], "l")                       # 코
        lines(lay, [([(215, 199), (216, 200), (217, 201), (221, 201), (222, 200), (223, 199)], "K", 1)])   # 웃는 선
        pfill(lay, 217, 202, 221, 202, "K"); pfill(lay, 218, 202, 220, 202, "N")          # 작게 열린 입
        dots(lay, [(207, 198), (208, 197), (209, 198), (226, 197), (227, 196), (228, 197)], "N")   # 볼 홍조
    # ---- 앞머리·옆머리: 끝이 뾰족한 다발 4개 + 양옆 머리, 다발 사이 어두운 틈 ----
    front = put(lay, [(200, 192), (199, 180), (204, 172), (212, 168), (221, 168), (229, 171), (234, 177), (236, 187),
                      (235, 197), (232, 201), (231, 194), (230, 188), (228, 183), (225, 179), (222, 184), (221, 185),
                      (218, 179), (216, 182), (214, 183), (211, 179), (209, 183), (208, 184), (206, 188), (205, 193),
                      (204, 198), (202, 201)], "D", "d", mono)
    if detail and not mono:
        # 다발 사이 틈(노치에서 정수리 쪽으로 휘어 올라가는 어두운 선) + 왼쪽 가장자리 밝은 선
        for (nx, ny, tx, ty) in ((225, 179, 227, 172), (218, 179, 218, 171), (211, 179, 209, 172)):
            lines(lay, [([(nx, ny), ((nx + tx) // 2, (ny + ty) // 2 + 1), (tx, ty)], "d", 1)])
            lines(lay, [([(nx - 2, ny - 1), (tx - 2, ty + 2)], "A", 1)])
        lines(lay, [([(205, 182), (206, 176)], "d", 1), ([(203, 184), (204, 194)], "d", 1), ([(201, 182), (201, 192)], "A", 1)])
        lines(lay, [([(233, 180), (234, 196)], "d", 1), ([(231, 176), (231, 186)], "A", 1)])
        # 오른쪽 옆머리 안쪽 그림자(2단째)
        shade(lay, front, [(232, 182), (237, 182), (236, 198), (233, 200)], "d")
        # 천사 링: 정수리 둥근 면을 따라 휘는 가로 하이라이트 띠
        ring = [(205, 178), (207, 175), (210, 173), (214, 172), (219, 172), (223, 173), (227, 175), (230, 178)]
        lines(lay, [(ring, "A", 1), ([(p[0], p[1] - 1) for p in ring[2:-2]], "h", 1)])
        # 바보털 1가닥
        lines(lay, [([(216, 168), (215, 164), (217, 161), (220, 160)], "D", 1), ([(217, 165), (218, 162)], "A", 1)])
        dots(lay, [(220, 160)], "d")
        # 머리끝 주황(다발 끝 2~3px)
        for tx, ty in ((228, 183), (221, 185), (214, 183), (208, 184)):
            dots(lay, [(tx, ty), (tx, ty - 1), (tx - 1, ty - 1)], "O")
        pfill(lay, 202, 198, 203, 201, "O"); pfill(lay, 231, 197, 232, 200, "O")
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
# L2 주인공 (책상 앞 의자, 뒷모습 — 고개를 왼쪽으로 조금 돌려 옆얼굴 윤곽이 살짝 보임)
# =====================================================================
def hacker_low(mono=False, detail=True):
    """의자 등받이·기둥·바퀴·좌석(움직이지 않음)."""
    lay = canvas(W, H)
    put(lay, [(452, 262), (454, 226), (462, 218), (502, 218), (510, 226), (512, 262)], "K", None, mono)
    lines(lay, [([(459, 231), (459, 258)], "2", 2)], mono)
    pfill(lay, 479, 270, 485, 292, "K"); pfill(lay, 480, 270, 481, 292, "2")
    pfill(lay, 462, 292, 502, 295, "K")
    for x in (462, 480, 500):
        pfill(lay, x - 1, 295, x + 2, 298, "2"); pfill(lay, x - 1, 298, x + 2, 298, "K")
    seat = put(lay, [(454, 262), (510, 262), (510, 272), (454, 272)], "4", "K", mono)
    return lay


def hacker_body(mono=False, detail=True):
    """몸통·팔·후드(숨쉬기로 1px 움직임)."""
    lay = canvas(W, H)
    # 팔꿈치(몸 양옆)
    for (pts, lit) in (([(451, 237), (458, 231), (462, 257), (452, 261)], False), ([(508, 231), (517, 239), (515, 261), (506, 257)], True)):
        am = put(lay, pts, "4", "K", mono)
        if lit and detail and not mono:
            shade(lay, am, [(512, 233), (518, 240), (516, 262), (510, 258)], "2", mono)
    bm = put(lay, [(456, 272), (455, 240), (461, 226), (473, 221), (492, 221), (505, 226), (511, 240), (511, 272)], "4", "K", mono)
    if detail and not mono:
        shade(lay, bm, [(485, 222), (505, 226), (511, 240), (511, 272), (490, 272)], "K", mono)       # 오른쪽 어두운 면
        shade(lay, bm, [(457, 232), (462, 227), (473, 222), (473, 226), (460, 236)], "2", mono)       # 왼쪽 어깨 밝은 면
        lines(lay, [([(466, 238), (468, 268)], "K", 1), ([(481, 236), (482, 268)], "K", 1), ([(497, 238), (496, 266)], "K", 1),
                    ([(462, 231), (474, 226), (490, 226)], "3", 1), ([(467, 240), (467, 250)], "2", 1), ([(459, 246), (460, 262)], "2", 1)], mono)
    # 후드(목 뒤 접힘)
    hd = put(lay, dr.emask((464, 211, 500, 233)), "2", "K", mono)
    if detail and not mono:
        shade(lay, hd, [(464, 224), (500, 224), (500, 234), (464, 234)], "4", mono)
        shade(lay, hd, [(488, 211), (500, 214), (500, 233), (488, 233)], "4", mono)
        lines(lay, [([(470, 224), (482, 220), (494, 224)], "K", 1), ([(468, 217), (476, 214)], "3", 1)], mono)
    # 목덜미
    put(lay, [(478, 213), (487, 213), (488, 220), (477, 220)], "s", "K", mono)
    if detail and not mono:
        pfill(lay, 478, 214, 480, 219, "S")
    return lay


def hacker_head(mono=False, detail=True):
    """머리(단발 다발·천사 링)·헤드셋(밴드·테이프·귀 컵·마이크 붐·케이블)·옆얼굴 윤곽."""
    lay = canvas(W, H)
    # 옆얼굴(고개를 왼쪽으로 돌려 머리 왼쪽 밖으로 볼이 조금 보임) — 머리카락·귀 컵 아래에 깔림
    fc = put(lay, [(461, 201), (464, 199), (465, 212), (463, 216), (460, 212), (460, 205)], "S", "s", mono)
    if detail and not mono:
        shade(lay, fc, [(463, 203), (466, 203), (466, 214), (462, 215)], "s", mono)
        dots(lay, [(461, 206), (461, 208)], "l", mono)
        pset(lay, 459, 203, "K"); pset(lay, 458, 202, "K")            # 속눈썹 끝 1~2px
    # 머리: 턱선 단발(뒤에서 본 둥근 덩어리 + 끝이 들쭉날쭉한 아랫선)
    hair = put(lay, [(468, 200), (466, 194), (468, 188), (474, 184), (482, 182), (490, 184), (496, 188), (499, 194),
                     (500, 201), (503, 208), (505, 215), (501, 219), (497, 216), (493, 221), (488, 217), (484, 222),
                     (479, 218), (475, 221), (470, 217), (466, 219), (462, 215), (464, 208), (467, 203)], "H", "J", mono)
    if detail and not mono:
        # 큰 색면: 위·왼쪽은 밝은 면(U), 오른쪽·목덜미는 그늘(J)
        shade(lay, hair, [(466, 186), (476, 183), (486, 183), (478, 192), (470, 200)], "U", mono)
        shade(lay, hair, [(494, 188), (500, 190), (506, 216), (496, 222), (496, 204)], "J", mono)
        shade(lay, hair, [(462, 219), (506, 219), (506, 224), (462, 224)], "J", mono)
        # 정수리: 가마(483,187)에서 천사 링 쪽으로 퍼지는 어두운 틈 3줄
        for ex, ey in ((471, 197), (483, 197), (495, 197)):
            lines(lay, [([(483, 188), ((483 + ex) // 2, 192), (ex, ey)], "J", 1)])
        lines(lay, [([(478, 188), (474, 192), (470, 196)], "U", 1), ([(484, 190), (480, 193), (478, 196)], "U", 1)])
        # 다발 5개(링 아래): 경계마다 어두운 틈(J) + 오른쪽 밝은 선(U), 끝은 뾰족하게 갈라짐
        tips = [(468, 216), (474, 221), (480, 219), (486, 222), (492, 218), (499, 219)]
        for k, (bx, by) in enumerate(tips):
            pts = []
            for y in range(198, by + 1):
                t = (y - 198) / max(1, by - 198)
                pts.append((round(bx + (bx - 483) / 5 * t * t), y))
            lines(lay, [(pts, "J", 1)])
            if k < 5:
                lines(lay, [([(x + 1, y) for x, y in pts[:-3]], "U", 1)])
        # 천사 링: 둥근 뒤통수를 따라 휘는 가로 하이라이트 띠 1줄(위에 얹음)
        ring = [(468, 201), (471, 198), (476, 196), (482, 195), (488, 196), (493, 198), (497, 201)]
        lines(lay, [(ring, "U", 1), ([(p[0], p[1] + 1) for p in ring[1:-1]], "U", 1)])
        lines(lay, [([(476, 196), (482, 195), (488, 196)], "C", 1)])
    # 헤드셋 밴드(머리 위 호) + 낡은 테이프
    band = canvas(W, H)
    draw_ellipse(band, (463, 178, 503, 216), "K", width=5)
    draw_ellipse(band, (464, 179, 502, 215), "2", width=3)
    draw_ellipse(band, (464, 179, 502, 215), "3", width=1)
    bp = band.load()
    for y in range(197, 224):
        for x in range(455, 512):
            bp[x, y] = (0, 0, 0, 0)
    if not mono:
        lay.alpha_composite(band)
    else:
        for y in range(H):
            for x in range(W):
                if band.getpixel((x, y))[3]:
                    lay.putpixel((x, y), PIX["K"])
    if detail and not mono:
        pfill(lay, 478, 178, 486, 182, "h"); pfill(lay, 478, 178, 486, 178, "Y")                  # 낡은 테이프
        dots(lay, [(480, 180), (484, 181)], "D")
        pfill(lay, 500, 188, 502, 192, "h"); pset(lay, 501, 190, "D")                              # 오른쪽 테이프 조각
        lines(lay, [([(468, 190), (466, 195)], "3", 1)])
    # 귀 컵(왼쪽은 약간 앞으로 돌아와 크게 보임)
    cl = put(lay, [(464, 198), (473, 198), (474, 213), (465, 214)], "2", "K", mono)
    cr = put(lay, [(494, 198), (502, 198), (503, 212), (495, 212)], "2", "K", mono)
    if detail and not mono:
        shade(lay, cl, [(464, 198), (467, 198), (468, 214), (465, 214)], "3", mono)
        shade(lay, cl, [(471, 204), (474, 204), (474, 213), (471, 213)], "4", mono)
        shade(lay, cr, [(494, 198), (497, 198), (497, 212), (495, 212)], "3", mono)
        shade(lay, cr, [(500, 204), (503, 204), (503, 212), (500, 212)], "4", mono)
        pbox(lay, 467, 202, 471, 210, "4"); pset(lay, 469, 206, "c")                               # 컵 가운데 면 + 표시등
        pbox(lay, 497, 201, 500, 209, "4")
        lines(lay, [([(465, 199), (472, 199)], "W", 1), ([(495, 199), (501, 199)], "3", 1)], mono)
        # 마이크 붐: 왼쪽 컵 아래에서 볼 쪽으로(어두운 배경 위라 3색 선)
        lines(lay, [([(467, 214), (463, 219), (459, 222)], "3", 1)], mono)
        pfill(lay, 456, 221, 459, 224, "2"); pbox(lay, 456, 221, 459, 224, "K"); pset(lay, 457, 222, "R")
        # 케이블: 오른쪽 컵 아래에서 어깨 뒤로
        lines(lay, [([(499, 213), (502, 222), (500, 232)], "2", 2)], mono)
    return lay


def hacker_frame(breath=0, bob=0, mono=False, detail=True, rim=True):
    """한 프레임(640×360 투명 판). breath 0/1 = 윗몸 1px 올라감, bob = 머리가 내려간 픽셀(0/1)."""
    lay = canvas(W, H)
    lay.alpha_composite(hacker_low(mono, detail))
    lay.alpha_composite(hacker_body(mono, detail), (0, -breath))
    lay.alpha_composite(hacker_head(mono, detail), (0, -breath + bob))
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
    """숨쉬기 2.4초(72틱, 1.2초씩 위/아래) + 음악에 맞춰 머리 1px 까딱 4프레임(각 9틱, 0.6초 주기)."""
    seq = (0, 1, 1, 0)
    return runs([((0 if t < 36 else 1), seq[(t // 9) % 4]) for t in range(72)])


GLOW_LEVELS = (0.85, 1.0, 1.15)               # 모니터 빛 세기 3단(0단 = L0에 구워진 기본)
GLOW_SEQ = ((0, 12), (1, 5), (2, 8), (1, 5), (0, 10), (1, 5))     # (단, 유지 틱) 합 45틱 = 1.5초
LED_HOLDS = (11, 7, 13, 9)                     # 합 40틱


_OCC = {}


def occluded(x, y, w, h):
    """LED 칸이 책상·모니터·소품에 가려지는가(시안 C에서 책상이 랙 아래쪽을 덮는다)."""
    if not _OCC:
        occ = canvas(W, H)
        occ.alpha_composite(dr.c_desk())
        occ.alpha_composite(dr.c_monitors()[0])
        occ.alpha_composite(dr.c_props())
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
        hk[st] = hacker_frame(breath=st[0], bob=st[1])
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
    for _ in range(LOOP_TICKS // 72):
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
        states = [{"breath": s[0], "head_down_px": s[1]} for s, _ in kt]
        made += sheet_save(frames, holds, "anim/main_lobby_hacker", kbox, states,
                           extra={"note": "숨쉬기 2.4초(72틱, 36틱씩) · 머리 까딱 4프레임(9틱씩) — 한 바퀴 72틱."})
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
