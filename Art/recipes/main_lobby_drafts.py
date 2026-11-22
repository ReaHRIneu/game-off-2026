"""메인 로비 그림 구도 시안 3개 (W-021) — A 어깨 너머 / B 옆모습 실루엣 / C 넓은 방.

- 640×360, 공유 팔레트만, 안티앨리어싱 없음. 정지 시안(움직임·Unity 적용은 W-022).
- 같은 인물(보라 머리·헤드폰·어두운 후드)·같은 조명 규칙(모니터=차가운 청록 주광, 스탠드=따뜻한 노랑 보조,
  광원 방향 음영·림 라이트, 디더만 — Docs/Art/lighting-shading.md)을 쓴다.
- 모니터 화면은 꺼진 상태 + 약한 반사(부팅 연출이 이 영역에 겹침, D-057). 마우스는 없다(D-036).
- 왼쪽 위 메뉴 자리(로고 x20~180·y14~36, 버튼 x32~116·y50~104)는 어둡고 단순하게 비운다.
[시안] 사용자 선택 전.
"""
import math
import random

from PIL import Image, ImageDraw

from artkit import (EXPORT, PIX, canvas, draw_ellipse, draw_line, draw_poly, over, palette_check,
                    pbox, pfill, pset, save, upscale)
from pixfont import draw_text, text_width
from ui_mvp import badge, button

W, H = 640, 360
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
REV = {v: k for k, v in PIX.items()}

# 화면(꺼짐) 영역 기록 — 안마다 다름(함수 아래에서 채움)
SCREENS = {}

# ---- 조명 단계표(문자 → 한 단계 밝은 문자) ----
COOL1 = {"a": "K", "K": "0", "k": "0", "j": "1", "0": "4", "1": "4", "4": "2", "2": "3", "e": "j", "f": "j",
         "H": "U", "J": "H", "D": "d", "3": "c", "S": "l", "s": "S"}
COOL2 = {"a": "0", "K": "4", "k": "1", "j": "4", "0": "2", "1": "2", "4": "3", "2": "3", "3": "C", "H": "U",
         "J": "H", "S": "l", "s": "S", "e": "4", "f": "4", "c": "C"}
WARM1 = {"a": "K", "K": "0", "k": "0", "0": "d", "1": "d", "4": "d", "j": "d", "2": "D", "3": "h", "e": "d",
         "f": "d", "H": "U", "S": "l"}
WARM2 = {"a": "d", "K": "d", "k": "d", "0": "D", "1": "D", "4": "D", "j": "D", "2": "h", "3": "h", "d": "D",
         "D": "h", "e": "D", "f": "D", "S": "l", "H": "U"}


# =====================================================================
# 공용 도구
# =====================================================================
def relight(im, fn, r1, r2=None, skip=None, box=None):
    """디더 조명: fn(x,y)→0~1 세기. 세기가 베이어 값보다 크면 한 단계, 0.5 더 크면 두 단계 밝게."""
    px = im.load()
    x0, y0, x1, y1 = box or (0, 0, im.width - 1, im.height - 1)
    for y in range(max(0, y0), min(im.height - 1, y1) + 1):
        for x in range(max(0, x0), min(im.width - 1, x1) + 1):
            c = px[x, y]
            if c[3] == 0:
                continue
            if skip and skip(x, y):
                continue
            i = fn(x, y)
            if i <= 0:
                continue
            ch = REV.get(c)
            if ch is None:
                continue
            t = (BAYER[y & 3][x & 3] + 0.5) / 16
            n = None
            if r2 and i > t + 0.5:
                n = r2.get(ch) or r1.get(ch)
            elif i > t:
                n = r1.get(ch)
            if n:
                px[x, y] = PIX[n]


def ell(cx, cy, rx, ry, power=1.0):
    def f(x, y):
        d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
        return max(0.0, 1.0 - d) ** power
    return f


def pmask(pts, size=(W, H)):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).polygon(pts, fill=255)
    return m


def emask(box, size=(W, H)):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).ellipse(box, fill=255)
    return m


def mask_union(*ms):
    out = ms[0].copy()
    for m in ms[1:]:
        out.paste(255, (0, 0), m)
    return out


def shade_mask(im, mask, ramp, axis=(0, 1), span=None):
    """마스크 안을 축 방향 그라데이션(ramp 문자 순서, 베이어 디더)으로 채운다."""
    bb = mask.getbbox()
    if not bb:
        return
    x0, y0, x1, y1 = bb
    ax, ay = axis
    corners = [(x0 * ax + y0 * ay), (x1 * ax + y0 * ay), (x0 * ax + y1 * ay), (x1 * ax + y1 * ay)]
    lo, hi = span or (min(corners), max(corners))
    mp = mask.load()
    n = len(ramp) - 1
    for y in range(y0, y1):
        for x in range(x0, x1):
            if mp[x, y] < 128:
                continue
            t = (x * ax + y * ay - lo) / max(1, hi - lo)
            t = min(1.0, max(0.0, t)) * n
            k = int(t)
            fr = t - k
            if k < n and fr > (BAYER[y & 3][x & 3] + 0.5) / 16:
                k += 1
            pset(im, x, y, ramp[k])


def fill_mask(im, mask, ch):
    bb = mask.getbbox()
    if not bb:
        return
    mp = mask.load()
    for y in range(bb[1], bb[3]):
        for x in range(bb[0], bb[2]):
            if mp[x, y] >= 128:
                pset(im, x, y, ch)


def clip_draw(im, mask, fn):
    """fn(layer)가 그린 것을 마스크 안에만 합성."""
    lay = canvas(im.width, im.height)
    fn(lay)
    clipped = Image.new("RGBA", im.size, (0, 0, 0, 0))
    clipped.paste(lay, (0, 0), mask)
    im.alpha_composite(clipped)


def outline_mask(im, mask, ch="K", where=None):
    """마스크 바깥 1px 외곽선(where(x,y)가 거짓이면 생략)."""
    mp = mask.load()
    w, h = mask.size
    bb = mask.getbbox()
    if not bb:
        return
    for y in range(max(0, bb[1] - 1), min(h, bb[3] + 1)):
        for x in range(max(0, bb[0] - 1), min(w, bb[2] + 1)):
            if mp[x, y] >= 128:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and mp[nx, ny] >= 128:
                    if where is None or where(x, y):
                        pset(im, x, y, ch)
                    break


def rim(im, mask, ch, dirs, depth=1, cond=None, fade=None):
    """림 라이트: 마스크 안 픽셀 중 dirs 방향 바깥이 비어 있으면 ch 로 칠한다."""
    mp = mask.load()
    w, h = mask.size
    bb = mask.getbbox()
    if not bb:
        return
    for y in range(bb[1], bb[3]):
        for x in range(bb[0], bb[2]):
            if mp[x, y] < 128:
                continue
            if cond and not cond(x, y):
                continue
            for dx, dy in dirs:
                hit = False
                for k in range(1, depth + 1):
                    nx, ny = x + dx * k, y + dy * k
                    if not (0 <= nx < w and 0 <= ny < h) or mp[nx, ny] < 128:
                        hit = True
                        break
                if hit:
                    if fade and (BAYER[y & 3][x & 3] + 0.5) / 16 > fade(x, y):
                        break
                    pset(im, x, y, ch)
                    break


def dith(im, box, ch, density, phase=0):
    """베이어 디더 점(density 0~16)."""
    x0, y0, x1, y1 = box
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if BAYER[y & 3][(x + phase) & 3] < density:
                pset(im, x, y, ch)


def quad_fill(im, pts, ch):
    draw_poly(im, pts, ch)


def quad_pt(q, u, v):
    """사각형 q=(TL,TR,BR,BL) 안의 (u,v) 0~1 좌표."""
    tl, tr, br, bl = q
    top = (tl[0] + (tr[0] - tl[0]) * u, tl[1] + (tr[1] - tl[1]) * u)
    bot = (bl[0] + (br[0] - bl[0]) * u, bl[1] + (br[1] - bl[1]) * u)
    return (round(top[0] + (bot[0] - top[0]) * v), round(top[1] + (bot[1] - top[1]) * v))


def rain(im, box, seed, n, length=7, chars=("c", "3"), mask=None):
    """정지한 빗줄기 몇 줄."""
    rng = random.Random(seed)
    x0, y0, x1, y1 = box
    for _ in range(n):
        x = rng.randint(x0, x1)
        y = rng.randint(y0, y1 - length)
        ch = chars[0] if rng.random() < 0.35 else chars[1]
        for k in range(rng.randint(length - 2, length)):
            xx, yy = x - k // 3, y + k
            if mask is not None and mask.getpixel((xx, yy)) < 128 if 0 <= xx < im.width and 0 <= yy < im.height else False:
                continue
            pset(im, xx, yy, ch)


def city_view(im, box, seed=3, horizon=0.78):
    """창밖 비 오는 밤 도시(하늘 단계 디더 + 건물 3겹 + 창문 불빛)."""
    x0, y0, x1, y1 = box
    w, h = x1 - x0 + 1, y1 - y0 + 1
    hy = y0 + int(h * horizon)
    bands = ["a", "a", "e", "e", "f", "f", "i"]
    for y in range(y0, y1 + 1):
        t = (y - y0) / max(1, hy - y0)
        t = min(1.0, t) * (len(bands) - 1)
        k = int(t)
        fr = t - k
        for x in range(x0, x1 + 1):
            kk = k + 1 if k < len(bands) - 1 and fr > (BAYER[y & 3][x & 3] + 0.5) / 16 else k
            pset(im, x, y, bands[kk])
    rng = random.Random(seed)
    for layer, (col, hmin, hmax, wmin, wmax, lit) in enumerate([
        ("j", 0.30, 0.55, 14, 34, 0.04), ("k", 0.38, 0.75, 16, 40, 0.07), ("a", 0.20, 0.45, 20, 46, 0.03)]):
        x = x0 - rng.randint(0, 20)
        while x <= x1:
            bw = rng.randint(wmin, wmax)
            bh = int(h * rng.uniform(hmin, hmax))
            top = y1 - bh if layer < 2 else y1 - int(bh * 0.7)
            pfill(im, x, top, x + bw - 1, y1, col)
            if layer < 2:
                for wy in range(top + 4, y1 - 3, 5):
                    for wx in range(x + 3, x + bw - 3, 4):
                        if rng.random() < lit:
                            pset(im, wx, wy, rng.choice(["w", "w", "c"]))
                            pset(im, wx + 1, wy, "Y" if rng.random() < 0.2 else "w")
            x += bw + rng.randint(0, 3)
    # 지평선 스모그 빛(디더)
    dith(im, (x0, hy - 6, x1, hy + 2), "i", 4)


# =====================================================================
# 공용 부품: 인물 머리 결 / 헤드폰
# =====================================================================
def strands(lay, cx, ex0, ex1, ey0, ey1, bottom, ch_dark, ch_hi, step=5, lit_side=1, hi_every=3, spread=0.1):
    """머리 타원(ex0,ey0,ex1,ey1) 윗선에서 아래(bottom)로 내려가는 머리 결(거의 세로, 바깥으로 살짝 휨)."""
    rx = (ex1 - ex0) / 2
    cy = (ey0 + ey1) / 2
    ry = (ey1 - ey0) / 2
    for i, x in enumerate(range(ex0 + 3, ex1 - 2, step)):
        u = (x - cx) / rx
        if abs(u) >= 1:
            continue
        ytop = cy - ry * math.sqrt(1 - u * u)
        pts = []
        for s in range(0, 25):
            t = s / 24
            yy = ytop + (bottom - ytop) * t
            xx = x + (x - cx) * spread * (t ** 1.6) * 2 + math.sin(t * 3 + i) * 0.8
            pts.append((round(xx), round(yy)))
        hi = (i % hi_every == 0) and ((x - cx) * lit_side >= -6)
        draw_line(lay, pts, ch_hi if hi else ch_dark)


# =====================================================================
# 안 A — 어깨 너머
# =====================================================================
A_MAIN = ((236, 30), (594, 46), (594, 210), (236, 236))          # 메인 모니터 바깥 틀
A_SCR = ((243, 37), (588, 52), (588, 204), (243, 228))           # 메인 화면(꺼짐)
A_SIDE = ((184, 64), (230, 78), (230, 222), (184, 252))          # 세로 보조 모니터 틀
A_SIDE_SCR = ((188, 70), (226, 82), (226, 216), (188, 244))


def a_room():
    im = canvas(W, H, PIX["k"])
    # 벽: 위가 더 어둡다(디더) + 판 이음
    dith(im, (0, 0, W - 1, 150), "a", 5)
    dith(im, (0, 0, W - 1, 60), "a", 6, 1)
    for x in range(0, W, 80):
        pfill(im, x, 0, x, 300, "K")
        pfill(im, x + 1, 0, x + 1, 300, "0") if x else None
    # 오른쪽 창(모니터 오른쪽 끝에 일부)
    pfill(im, 596, 20, 639, 236, "K")
    city_view(im, (600, 26, 639, 226), seed=11, horizon=0.8)
    pbox(im, 596, 20, 639, 236, "K"); pbox(im, 597, 21, 639, 235, "2")
    pfill(im, 596, 236, 639, 240, "3"); pfill(im, 596, 241, 639, 243, "4")        # 창턱
    pfill(im, 618, 22, 621, 232, "2")                                             # 창살
    pfill(im, 600, 120, 639, 123, "2")
    rain(im, (600, 26, 639, 226), 5, 14, 8)
    # 책상 뒤 벽 아래 어둠(바닥 쪽)
    dith(im, (0, 250, W - 1, 300), "a", 8)
    return im


def a_screen_quad(im, q, inner, on=False, tint="a"):
    """꺼진 모니터: 틀 + 어두운 화면 + 약한 반사(사선 띠·모서리 그라데이션)."""
    quad_fill(im, list(q), "K")
    tl, tr, br, bl = q
    quad_fill(im, [(tl[0] + 1, tl[1] + 1), (tr[0] - 1, tr[1] + 1), (br[0] - 1, br[1] - 1), (bl[0] + 1, bl[1] - 1)], "4")
    quad_fill(im, list(inner), "a")
    m = pmask(list(inner))
    # 화면 안 약한 반사: 위쪽 어두운 남색 → 아래쪽 약간 밝은 k, 사선 반사 띠
    shade_mask(im, m, ["a", "a", "k", "k", "0"], axis=(0, 1))
    def refl(lay):
        for off, ch in ((60, "j"), (66, "j"), (90, "0"), (102, "j")):
            for t in range(-200, 400):
                x = inner[0][0] + off + t
                y = inner[0][1] + t * 2 // 3
                pset(lay, x, y, ch)
                pset(lay, x + 1, y, ch)
    clip_draw(im, m, refl)
    # 하단 모서리 청록 기미(대기 표시등 빛이 화면 유리에 살짝)
    return m


def a_monitors():
    im = canvas(W, H)
    # 메인 모니터 스탠드(틀 뒤)
    pfill(im, 408, 214, 426, 268, "2"); pfill(im, 408, 214, 411, 268, "3"); pfill(im, 424, 214, 426, 268, "4")
    pbox(im, 407, 214, 427, 268, "K")
    draw_poly(im, [(372, 268), (462, 268), (472, 282), (362, 282)], "2")
    draw_poly(im, [(372, 268), (462, 268), (460, 272), (374, 272)], "3")
    pbox(im, 362, 268, 472, 282, "K")
    # 세로 모니터 스탠드
    pfill(im, 202, 244, 214, 270, "2"); pbox(im, 201, 244, 215, 270, "K")
    draw_poly(im, [(180, 270), (236, 270), (244, 282), (172, 282)], "2"); pbox(im, 172, 270, 244, 282, "K")
    mm = a_screen_quad(im, A_MAIN, A_SCR)
    ms = a_screen_quad(im, A_SIDE, A_SIDE_SCR)
    # 틀 윗변 밝은 선(광원 위쪽)
    draw_line(im, [A_MAIN[0], A_MAIN[1]], "2")
    draw_line(im, [A_SIDE[0], A_SIDE[1]], "2")
    # 표시등(꺼짐 대기: 작은 청록 점)
    px, py = quad_pt(A_MAIN, 0.96, 1.0)
    pfill(im, px - 1, py - 1, px, py, "c")
    SCREENS["A"] = {"main": A_SCR, "side": A_SIDE_SCR}
    return im, mm


def a_desk():
    im = canvas(W, H)
    # 책상 상판(원근: 앞 모서리는 낮고 위 면이 보임) — 상판 면 / 앞 모서리 / 앞면
    draw_poly(im, [(20, 280), (640, 280), (640, 316), (20, 322)], "d")
    shade_mask(im, pmask([(20, 280), (640, 280), (640, 316), (20, 322)]), ["D", "d", "d", "d", "d"], axis=(0, 1))
    # 나뭇결
    for y in range(284, 316, 6):
        draw_line(im, [(24, y + 1), (640, y)], "K")
    draw_poly(im, [(20, 322), (640, 316), (640, 321), (20, 328)], "D")
    pfill(im, 20, 329, 639, 359, "d")
    dith(im, (20, 329, 639, 359), "K", 10)
    pfill(im, 20, 322, 22, 359, "K"); pfill(im, 636, 316, 639, 359, "K")
    # 서랍
    pbox(im, 520, 336, 626, 356, "K"); pbox(im, 521, 337, 625, 355, "d"); pfill(im, 566, 345, 582, 346, "h")
    # 키보드(마우스 없음)
    kb = [(446, 288), (562, 284), (578, 306), (462, 312)]
    quad_fill(im, kb, "K")
    quad_fill(im, [(448, 289), (561, 285), (576, 305), (463, 310)], "2")
    for r in range(4):
        for c in range(14):
            p0 = quad_pt(kb, 0.04 + c * 0.067, 0.12 + r * 0.22)
            p1 = quad_pt(kb, 0.04 + c * 0.067 + 0.05, 0.12 + r * 0.22 + 0.14)
            pfill(im, p0[0], p0[1], p1[0], p1[1], "3" if (r + c) % 5 else "4")
    # 에너지 음료 캔 + 컵
    for x, y, c in [(46, 248, "R"), (60, 252, "G"), (32, 253, "3")]:
        pfill(im, x, y, x + 9, y + 32, c); pfill(im, x, y, x + 9, y + 2, "W"); pfill(im, x + 7, y + 2, x + 9, y + 32, "r" if c == "R" else ("g" if c == "G" else "2"))
        pbox(im, x, y, x + 9, y + 32, "K")
    return im


def a_lamp():
    """작은 책상 스탠드(왼쪽 책상 끝). 켜짐 빛은 relight 로."""
    im = canvas(W, H)
    pfill(im, 96, 296, 132, 304, "2"); pbox(im, 96, 296, 132, 304, "K"); pfill(im, 98, 297, 130, 298, "3")
    draw_line(im, [(114, 296), (118, 262), (140, 232), (164, 222)], "K", 4)
    draw_line(im, [(114, 296), (118, 262), (140, 232), (164, 222)], "2", 2)
    draw_poly(im, [(156, 212), (190, 222), (184, 240), (150, 230)], "K")
    draw_poly(im, [(158, 215), (186, 223), (182, 236), (154, 228)], "h")      # 갓 안쪽 따뜻한 빛
    pfill(im, 162, 223, 176, 226, "W")
    return im


def a_character():
    """주인공(뒤쪽 왼편 30° 어깨 너머). 반환: (레이어, 몸 마스크)."""
    lay = canvas(W, H)
    # ---- 의자 등받이(어깨 뒤로 약간 보임) ----
    chair = [(236, 300), (252, 262), (300, 236), (448, 240), (500, 276), (512, 360), (236, 360)]
    fill_mask(lay, pmask(chair), "K")
    # ---- 어깨·후드 몸통 ----
    body = [(206, 360), (214, 312), (246, 270), (296, 250), (440, 252), (486, 282), (506, 330), (516, 360)]
    bm = pmask(body)
    shade_mask(lay, bm, ["4", "2", "4", "4", "K"], axis=(1, 0.35), span=(210, 520))
    # 윗부분을 어둡게(어깨 윗면은 광원 쪽이라 약간 밝게 → 아래로 어둡게)
    shade_mask(lay, pmask([(214, 312), (246, 270), (296, 250), (440, 252), (486, 282), (500, 312), (470, 300), (400, 292), (300, 294), (240, 318)]),
               ["2", "2", "4"], axis=(0, 1))
    # 옷 주름(2~3단 음영): 어두운 골 + 밝은 능선
    def folds(l):
        for x0, y0, x1, y1 in [(236, 322, 252, 358), (262, 300, 272, 358), (300, 318, 306, 360),
                               (344, 322, 340, 360), (392, 312, 398, 360), (440, 308, 452, 356),
                               (472, 306, 488, 350)]:
            draw_line(l, [(x0, y0), ((x0 + x1) // 2 - 3, (y0 + y1) // 2), (x1, y1)], "K", 2)
            draw_line(l, [(x0 + 4, y0 + 2), ((x0 + x1) // 2 + 1, (y0 + y1) // 2), (x1 + 4, y1)], "3")
        # 어깨 선 주름
        draw_line(l, [(250, 282), (290, 266), (340, 262)], "3")
        draw_line(l, [(380, 262), (430, 266), (472, 288)], "3")
        draw_line(l, [(228, 304), (252, 288)], "4")
    clip_draw(lay, bm, folds)
    # ---- 후드(목 뒤로 접힌 깃: 둥근 주머니) ----
    hm = emask((286, 224, 452, 302))
    shade_mask(lay, hm, ["3", "2", "4", "4", "K"], axis=(0, 1), span=(226, 302))
    def hood_folds(l):
        draw_line(l, [(300, 262), (320, 252), (344, 262)], "K")
        draw_line(l, [(362, 262), (388, 254), (420, 262)], "K")
        draw_line(l, [(316, 280), (346, 272), (372, 282)], "K")
        draw_line(l, [(340, 236), (332, 250)], "3")
        draw_line(l, [(398, 240), (412, 252)], "3")
        draw_line(l, [(304, 250), (322, 242)], "3")
    clip_draw(lay, hm, hood_folds)
    outline_mask(lay, hm, "K", where=lambda x, y: y < 262)
    # ---- 목덜미 ----
    neck = [(342, 206), (392, 206), (396, 240), (338, 240)]
    nm = pmask(neck)
    fill_mask(lay, nm, "s")
    shade_mask(lay, nm, ["s", "s", "S"], axis=(1, 0))
    pfill(lay, 342, 206, 396, 218, "K")
    # ---- 머리 ----
    head_e = emask((322, 118, 424, 224))
    zig = []
    for i, x in enumerate(range(316, 432, 8)):
        zig.append((x, 232 if i % 2 else 224))
    hair_low = pmask([(322, 190), (424, 190), (430, 226)] + zig[::-1])
    hm2 = mask_union(head_e, hair_low)
    fill_mask(lay, hm2, "H")
    shade_mask(lay, hm2, ["H", "H", "J"], axis=(0, 1), span=(120, 228))
    # 왼쪽(카메라 가까운 쪽)은 어둡게
    shade_mask(lay, hm2, ["J", "H", "H", "H"], axis=(1, 0), span=(316, 430))
    def hair_lines(l):
        strands(l, 376, 322, 424, 118, 224, 228, "J", "U", step=4, lit_side=1, hi_every=3)
        # 뒷머리 끝 층(단발 끝선 뾰족)
        for x in range(320, 430, 9):
            draw_line(l, [(x, 214), (x + 3, 232)], "J", 2)
    clip_draw(lay, hm2, hair_lines)
    # 머리 윗면 하이라이트 띠(모니터 쪽 위)
    def tophi(l):
        draw_line(l, [(352, 122), (378, 118), (404, 124)], "U", 2)
        draw_line(l, [(360, 126), (380, 123), (398, 128)], "U")
    clip_draw(lay, hm2, tophi)
    outline_mask(lay, hm2, "K")
    # 얼굴이 화면 쪽(오른쪽)으로 살짝 돌아 뺨·턱선이 머리카락 옆으로 비침
    cheek = pmask([(420, 176), (428, 180), (430, 206), (420, 214), (414, 204)])
    fill_mask(lay, cheek, "s")
    shade_mask(lay, cheek, ["s", "S", "l"], axis=(1, 0))
    outline_mask(lay, cheek, "K", where=lambda x, y: x < 420)
    # ---- 헤드폰: 밴드(머리 위 호) + 가까운 쪽 귀 컵 + 먼 쪽 컵 일부 ----
    band = canvas(W, H)
    draw_ellipse(band, (316, 110, 430, 222), "K", width=6)
    draw_ellipse(band, (318, 112, 428, 220), "2", width=3)
    draw_ellipse(band, (318, 112, 428, 220), "3", width=1)
    bp = band.load()
    for y in range(176, 240):
        for x in range(300, 450):
            bp[x, y] = (0, 0, 0, 0)
    lay.alpha_composite(band)
    # 가까운 쪽(왼) 귀 컵: 뒤에서 본 옆면(원통) — 바깥 면 + 쿠션
    cup = pmask([(304, 164), (334, 160), (338, 206), (306, 208)])
    fill_mask(lay, cup, "2")
    shade_mask(lay, cup, ["3", "2", "4"], axis=(1, 0))
    pbox(lay, 304, 160, 338, 208, "K")
    draw_line(lay, [(312, 170), (330, 168)], "3"); pfill(lay, 314, 178, 328, 196, "4")
    draw_line(lay, [(314, 178), (328, 178)], "3")
    pset(lay, 321, 187, "c")                      # 컵 바깥 표시등(청록 점)
    # 먼 쪽(오른) 컵: 머리에 가려 윗부분만 보임
    cup2 = pmask([(418, 168), (432, 170), (434, 204), (420, 202)])
    fill_mask(lay, cup2, "4"); pbox(lay, 418, 168, 434, 204, "K")
    pfill(lay, 420, 170, 421, 202, "2")
    draw_line(lay, [(318, 206), (312, 240), (318, 262)], "K", 2)                    # 케이블
    # ---- 오른팔: 어깨에서 키보드로 ----
    arm = [(448, 264), (484, 280), (528, 290), (514, 312), (470, 304), (440, 290)]
    am = pmask(arm)
    shade_mask(lay, am, ["4", "2", "4"], axis=(0, 1))
    draw_line(lay, [(458, 276), (500, 292)], "3"); draw_line(lay, [(452, 294), (490, 308)], "K", 2)
    outline_mask(lay, am, "K")
    # 손(키보드 위, 손가락 4개 + 엄지)
    hand = pmask([(516, 296), (536, 288), (552, 292), (556, 300), (540, 304), (516, 310)])
    fill_mask(lay, hand, "S")
    shade_mask(lay, hand, ["l", "S", "s"], axis=(0, 1))
    for fx in range(530, 556, 6):
        draw_line(lay, [(fx, 292), (fx + 2, 301)], "s")
    outline_mask(lay, hand, "K")
    # 왼팔 팔꿈치(몸 왼쪽 앞으로 튀어나옴)
    elbow = pmask([(206, 360), (206, 318), (226, 300), (252, 304), (262, 330), (262, 360)])
    fill_mask(lay, elbow, "4")
    shade_mask(lay, elbow, ["2", "4", "K"], axis=(1, 0.6))
    draw_line(lay, [(222, 326), (244, 332)], "K", 2); draw_line(lay, [(214, 318), (232, 306)], "3")
    outline_mask(lay, elbow, "K")
    # 몸 외곽선
    outline_mask(lay, bm, "K")
    # ---- 림 라이트(모니터가 오른쪽·앞에 있다): 오른쪽·위 가장자리 ----
    full = mask_union(bm, hm, hm2, am, hand, elbow)
    rim(lay, hm2, "c", [(1, 0), (0, -1)], 1, cond=lambda x, y: x > 346 and y < 218)
    rim(lay, hm2, "C", [(1, 0)], 1, cond=lambda x, y: x > 410 and 130 < y < 200,
        fade=lambda x, y: 0.7)
    rim(lay, bm, "c", [(0, -1), (1, 0)], 1, cond=lambda x, y: x > 330 and y < 330, fade=lambda x, y: 0.8)
    rim(lay, am, "C", [(0, -1)], 1, cond=lambda x, y: True, fade=lambda x, y: 0.6)
    rim(lay, hm, "c", [(0, -1)], 1, cond=lambda x, y: x > 350, fade=lambda x, y: 0.6)
    rim(lay, hand, "W", [(0, -1)], 1, fade=lambda x, y: 0.5)
    return lay, full


def make_a():
    scene = canvas(W, H, PIX["k"])
    room = a_room()
    mons, main_mask = a_monitors()
    desk = a_desk()
    lamp = a_lamp()
    scene.alpha_composite(room)
    scene.alpha_composite(mons)
    scene.alpha_composite(desk)
    scene.alpha_composite(lamp)
    # 모니터 쪽 차가운 빛(대기 표시등·유리 반사가 벽·책상·창틀을 청록으로): 화면 영역은 제외
    scr_mask = mask_union(pmask(list(A_SCR)), pmask(list(A_SIDE_SCR)))
    sk = lambda x, y: scr_mask.getpixel((x, y)) > 128
    relight(scene, ell(410, 130, 330, 190, 0.8), COOL1, COOL2, skip=sk)
    # 스탠드: 책상·벽·캔에 따뜻한 빛(왼쪽 아래, 메뉴 자리 밖)
    relight(scene, ell(150, 262, 110, 70, 1.1), WARM1, WARM2, skip=lambda x, y: sk(x, y) or (y < 130))
    ch_lay, ch_mask = a_character()
    scene.alpha_composite(ch_lay)
    # 스탠드 보조광이 닿는 어깨 왼쪽(작게)
    relight(scene, ell(206, 330, 40, 40, 1.0), WARM1, None, box=(180, 280, 270, 360),
            skip=lambda x, y: ch_mask.getpixel((x, y)) < 128)
    # 화면 반사를 한 번 더(캐릭터 앞에 놓이지 않게 마지막에 화면 위 일부 재그림은 하지 않음)
    return scene


# =====================================================================
# 안 B — 옆모습 실루엣
# =====================================================================
B_MAIN = ((510, 74), (634, 90), (634, 200), (510, 218))      # 모니터 바깥 틀(3/4)
B_SCR = ((516, 81), (630, 95), (630, 195), (516, 211))        # 화면(꺼짐) — 일부만 보임


def b_room():
    im = canvas(W, H, PIX["k"])
    dith(im, (0, 0, W - 1, 160), "a", 5)
    dith(im, (0, 0, W - 1, 70), "a", 6, 1)
    for x in range(0, W, 80):
        pfill(im, x, 0, x, 300, "K")
    # 왼쪽 아래 책장 실루엣(어둡게, 메뉴 자리 아래)
    pfill(im, 24, 150, 150, 306, "0"); pbox(im, 24, 150, 150, 306, "K")
    for sy in (190, 230, 270):
        pfill(im, 25, sy, 149, sy + 3, "4"); pfill(im, 25, sy + 4, 149, sy + 4, "K")
    rng = random.Random(8)
    for sy in (152, 192, 232, 272):
        x = 28
        while x < 140:
            bw = rng.randint(4, 9)
            bh = rng.randint(22, 34)
            col = rng.choice(["1", "4", "j", "y", "g", "r", "B", "2"])
            pfill(im, x, sy + 36 - bh, x + bw - 1, sy + 35, col)
            pbox(im, x, sy + 36 - bh, x + bw - 1, sy + 35, "K")
            x += bw + rng.randint(0, 1)
    # 큰 창
    pfill(im, 192, 36, 444, 262, "K")
    city_view(im, (198, 42, 438, 252), seed=21, horizon=0.84)
    dith(im, (198, 206, 438, 226), "i", 3, 1)
    pbox(im, 192, 36, 444, 262, "K"); pbox(im, 193, 37, 443, 261, "2"); pbox(im, 194, 38, 442, 260, "4")
    pfill(im, 316, 40, 320, 258, "2"); pfill(im, 316, 40, 316, 258, "3"); pfill(im, 320, 40, 320, 258, "4")
    pfill(im, 196, 136, 440, 140, "2"); pfill(im, 196, 136, 440, 136, "3")
    pfill(im, 186, 262, 452, 268, "3"); pfill(im, 186, 268, 452, 272, "4"); pbox(im, 186, 262, 452, 272, "K")  # 창턱
    rain(im, (198, 42, 438, 252), 31, 70, 9)
    dith(im, (0, 280, W - 1, 300), "a", 8)
    return im


def b_monitor():
    im = canvas(W, H)
    q = B_MAIN
    pfill(im, 562, 214, 580, 268, "2"); pfill(im, 562, 214, 564, 268, "3"); pbox(im, 561, 214, 581, 268, "K")
    draw_poly(im, [(530, 268), (614, 268), (624, 280), (520, 280)], "2")
    pfill(im, 530, 268, 614, 270, "3"); pbox(im, 520, 268, 624, 280, "K")
    quad_fill(im, list(q), "K")
    tl, tr, br, bl = q
    quad_fill(im, [(tl[0] + 1, tl[1] + 1), (tr[0] - 1, tr[1] + 1), (br[0] - 1, br[1] - 1), (bl[0] + 1, bl[1] - 1)], "4")
    quad_fill(im, list(B_SCR), "a")
    m = pmask(list(B_SCR))
    shade_mask(im, m, ["a", "a", "k", "k", "0"], axis=(0, 1))

    def refl(lay):
        for off, ch in ((30, "j"), (36, "j"), (70, "0")):
            for t in range(-100, 300):
                pset(lay, B_SCR[0][0] + off + t, B_SCR[0][1] + t * 2 // 3, ch)
                pset(lay, B_SCR[0][0] + off + t + 1, B_SCR[0][1] + t * 2 // 3, ch)
    clip_draw(im, m, refl)
    draw_line(im, [q[0], q[1]], "3")
    pfill(im, 620, 206, 622, 207, "c")                                  # 대기 표시등
    SCREENS["B"] = {"main": B_SCR}
    return im, m


def b_desk():
    im = canvas(W, H)
    pfill(im, 330, 272, 639, 278, "D"); pfill(im, 330, 272, 639, 273, "h"); pfill(im, 330, 278, 639, 281, "d")
    pbox(im, 330, 272, 639, 281, "K")
    pfill(im, 331, 282, 639, 359, "d")
    dith(im, (331, 282, 639, 359), "K", 11)
    pfill(im, 330, 282, 334, 359, "K")
    # 키보드(옆면) — 마우스 없음
    pfill(im, 482, 266, 550, 271, "K"); pfill(im, 483, 267, 549, 270, "2")
    for x in range(486, 548, 4):
        pset(im, x, 266, "3"); pset(im, x + 1, 266, "3")
    # 머그컵
    pfill(im, 400, 256, 412, 271, "3"); pfill(im, 400, 256, 412, 257, "W"); pfill(im, 409, 258, 412, 271, "2")
    pbox(im, 400, 256, 412, 271, "K"); pfill(im, 413, 260, 416, 266, "3"); pbox(im, 413, 260, 416, 266, "K")
    # 스탠드
    pfill(im, 336, 266, 360, 271, "2"); pbox(im, 336, 266, 360, 271, "K"); pfill(im, 337, 267, 359, 267, "3")
    draw_line(im, [(348, 266), (344, 236), (356, 214), (380, 204)], "K", 4)
    draw_line(im, [(348, 266), (344, 236), (356, 214), (380, 204)], "2", 2)
    draw_poly(im, [(370, 194), (402, 204), (396, 220), (364, 210)], "K")
    draw_poly(im, [(372, 197), (398, 205), (394, 217), (368, 209)], "h")
    pfill(im, 377, 206, 390, 209, "W")
    return im


def b_character():
    lay = canvas(W, H)
    fill_mask(lay, pmask([(364, 360), (366, 270), (386, 244), (404, 244), (402, 360)]), "K")   # 의자 등받이
    pfill(lay, 372, 262, 376, 346, "4")
    # 다리(책상 밑)
    th = pmask([(412, 300), (508, 308), (512, 330), (416, 328)])
    shade_mask(lay, th, ["4", "0", "K"], axis=(0, 1))
    sh = pmask([(496, 306), (516, 310), (520, 358), (500, 358)])
    shade_mask(lay, sh, ["4", "0", "K"], axis=(1, 0))
    outline_mask(lay, th, "K"); outline_mask(lay, sh, "K")
    # 몸통
    body = [(386, 360), (390, 296), (400, 262), (420, 242), (446, 234), (472, 240), (480, 264), (476, 304), (480, 330), (430, 336), (420, 360)]
    bm = pmask(body)
    shade_mask(lay, bm, ["K", "K", "0", "4", "4"], axis=(1, 0.2), span=(388, 484))

    def folds(l):
        draw_line(l, [(414, 262), (424, 290), (418, 330)], "K", 2)
        draw_line(l, [(432, 256), (440, 284), (436, 316)], "K")
        draw_line(l, [(456, 262), (462, 290), (458, 312)], "K")
        draw_line(l, [(420, 248), (446, 242), (470, 248)], "3")
        draw_line(l, [(446, 270), (454, 272)], "3")
    clip_draw(lay, bm, folds)
    outline_mask(lay, bm, "K")
    # 후드
    hd = emask((392, 218, 450, 268))
    shade_mask(lay, hd, ["2", "4", "4", "K"], axis=(0, 1), span=(220, 268))
    clip_draw(lay, hd, lambda l: (draw_line(l, [(402, 244), (420, 252), (440, 246)], "K"), draw_line(l, [(408, 232), (418, 238)], "3")))
    outline_mask(lay, hd, "K", where=lambda x, y: y < 250)
    # 목
    nk = pmask([(446, 204), (468, 206), (472, 238), (446, 238)])
    fill_mask(lay, nk, "s")
    shade_mask(lay, nk, ["K", "K", "s", "s"], axis=(0, 1), span=(204, 232))
    # 얼굴
    face = pmask([(468, 136), (484, 146), (486, 158), (492, 172), (488, 177), (489, 182), (486, 185), (487, 190),
                  (483, 198), (476, 206), (466, 212), (446, 214), (444, 170), (456, 156)])
    fill_mask(lay, face, "S")
    shade_mask(lay, face, ["s", "s", "s", "S", "S"], axis=(1, 0), span=(446, 492))
    pfill(lay, 452, 206, 482, 210, "s")
    pfill(lay, 472, 158, 482, 158, "J"); pfill(lay, 481, 157, 484, 157, "J")             # 눈썹
    pfill(lay, 474, 163, 481, 163, "K"); pfill(lay, 475, 164, 479, 165, "v"); pset(lay, 477, 164, "W")  # 눈
    pset(lay, 482, 164, "K")
    pset(lay, 491, 174, "s"); pset(lay, 490, 176, "s"); pset(lay, 489, 177, "s")
    pfill(lay, 484, 186, 487, 186, "m"); pset(lay, 483, 187, "s")
    pset(lay, 486, 192, "s"); pset(lay, 484, 195, "s")
    outline_mask(lay, face, "K", where=lambda x, y: x > 456)
    # 머리카락
    hair = pmask([(414, 168), (412, 148), (420, 134), (436, 124), (458, 120), (478, 126), (489, 140), (491, 151),
                  (484, 150), (476, 149), (468, 154), (460, 162), (454, 172), (450, 186), (448, 204),
                  (444, 220), (436, 228), (426, 224), (418, 206), (414, 190)])
    fill_mask(lay, hair, "H")
    shade_mask(lay, hair, ["H", "H", "J"], axis=(0, 1), span=(120, 228))
    shade_mask(lay, hair, ["J", "H", "H", "U"], axis=(1, 0), span=(412, 492))

    def hair_lines(l):
        for i, x in enumerate(range(414, 488, 4)):
            ytop = 122 + abs(x - 452) // 4
            draw_line(l, [(x, ytop + 4), (x - 2 + (i % 2), 160 + (x - 414) // 6), (x - 4, 214)], "J" if i % 3 else "U")
        for k in range(4):
            draw_line(l, [(456 + k * 6, 130 + k), (470 + k * 5, 140 + k * 2), (484 - k, 150 - k)], "U" if k % 2 else "J")
        for x in range(418, 446, 6):
            draw_line(l, [(x, 210), (x + 2, 226)], "J", 2)
    clip_draw(lay, hair, hair_lines)
    outline_mask(lay, hair, "K", where=lambda x, y: not (x > 454 and 152 < y < 220))
    # 헤드폰
    band = canvas(W, H)
    draw_ellipse(band, (410, 108, 490, 204), "K", width=6)
    draw_ellipse(band, (412, 110, 488, 202), "2", width=3)
    draw_ellipse(band, (412, 110, 488, 202), "3", width=1)
    bp = band.load()
    for y in range(170, 240):
        for x in range(380, 520):
            bp[x, y] = (0, 0, 0, 0)
    for y in range(100, 170):
        for x in range(380, 520):
            if x < 424 or (x < 452 and y > 160):
                bp[x, y] = (0, 0, 0, 0)
    lay.alpha_composite(band)
    cup = emask((431, 163, 465, 199))
    fill_mask(lay, cup, "2")
    shade_mask(lay, cup, ["3", "2", "4", "K"], axis=(1, 1), span=(594, 664))
    outline_mask(lay, cup, "K")
    pad = emask((438, 170, 458, 192))
    fill_mask(lay, pad, "4")
    shade_mask(lay, pad, ["2", "4", "K"], axis=(1, 1), span=(608, 650))
    pset(lay, 448, 181, "c"); pset(lay, 449, 181, "c")
    draw_line(lay, [(458, 160), (462, 176), (460, 194)], "H")          # 귀 앞 머리카락 한 가닥
    # 팔
    ua = pmask([(446, 248), (476, 252), (482, 290), (456, 312), (438, 296)])
    shade_mask(lay, ua, ["4", "2", "4", "K"], axis=(1, 0.5), span=(440, 480))
    draw_line(lay, [(454, 264), (464, 292)], "K"); draw_line(lay, [(448, 256), (470, 258)], "3")
    outline_mask(lay, ua, "K")
    fa = pmask([(452, 296), (466, 312), (518, 280), (514, 262), (492, 268), (470, 282)])
    shade_mask(lay, fa, ["2", "4", "K"], axis=(0, 1), span=(262, 312))
    draw_line(lay, [(468, 290), (506, 270)], "3")
    outline_mask(lay, fa, "K")
    hand = pmask([(508, 262), (530, 260), (544, 266), (542, 272), (516, 272), (506, 270)])
    fill_mask(lay, hand, "S")
    shade_mask(lay, hand, ["l", "S", "s"], axis=(0, 1))
    for fx in range(522, 542, 5):
        draw_line(lay, [(fx, 264), (fx + 1, 271)], "s")
    outline_mask(lay, hand, "K")
    # 림 라이트: 오른쪽(모니터)·앞면
    rim(lay, face, "C", [(1, 0), (0, -1)], 1, cond=lambda x, y: x > 470, fade=lambda x, y: 0.75)
    rim(lay, face, "W", [(1, 0)], 1, cond=lambda x, y: 168 < y < 180 and x > 486, fade=lambda x, y: 0.6)
    rim(lay, hair, "c", [(1, 0), (0, -1)], 1, cond=lambda x, y: x > 450 and y < 156, fade=lambda x, y: 0.8)
    rim(lay, cup, "c", [(1, 0)], 1, fade=lambda x, y: 0.8)
    rim(lay, hand, "C", [(1, 0), (0, -1)], 1, fade=lambda x, y: 0.8)
    rim(lay, fa, "c", [(0, -1)], 1, cond=lambda x, y: x > 480, fade=lambda x, y: 0.7)
    rim(lay, ua, "c", [(1, 0)], 1, fade=lambda x, y: 0.5)
    rim(lay, bm, "c", [(1, 0)], 1, cond=lambda x, y: y < 270, fade=lambda x, y: 0.5)
    # 따뜻한 림(스탠드가 왼쪽 뒤): 머리 뒤·후드 뒤
    rim(lay, hair, "h", [(-1, 0)], 1, cond=lambda x, y: y > 140, fade=lambda x, y: 0.5)
    rim(lay, hd, "D", [(-1, 0), (0, -1)], 1, fade=lambda x, y: 0.5)
    full = mask_union(bm, hd, face, hair, ua, fa, hand, cup, th, sh)
    return lay, full


def make_b():
    scene = canvas(W, H, PIX["k"])
    room = b_room()
    mon, mm = b_monitor()
    desk = b_desk()
    scene.alpha_composite(room)
    scene.alpha_composite(mon)
    scene.alpha_composite(desk)
    sk = lambda x, y: mm.getpixel((x, y)) > 128
    relight(scene, ell(560, 190, 150, 120, 0.9), COOL1, COOL2, skip=sk)                  # 모니터 빛
    relight(scene, ell(316, 150, 150, 130, 1.0), COOL1, None, box=(186, 36, 452, 272),   # 창 빛은 창틀에만
            skip=lambda x, y: 44 < y < 252 and 198 < x < 438)
    relight(scene, ell(372, 240, 76, 52, 1.1), WARM1, WARM2, skip=sk)                    # 스탠드(왼쪽 뒤)
    ch, cm = b_character()
    scene.alpha_composite(ch)
    return scene


# =====================================================================
# 안 C — 넓은 방
# =====================================================================
C_MAIN = ((424, 164), (538, 166), (538, 230), (424, 232))       # 메인 모니터 바깥 틀
C_SCR = ((429, 169), (533, 171), (533, 225), (429, 227))        # 화면(꺼짐)
C_SIDE = ((544, 156), (574, 158), (574, 232), (544, 232))
C_SIDE_SCR = ((548, 161), (570, 163), (570, 227), (548, 227))
C_POSTER = (200, 112, 262, 190)


def c_room():
    im = canvas(W, H, PIX["k"])
    dith(im, (0, 0, W - 1, 140), "a", 5)
    dith(im, (0, 0, W - 1, 56), "a", 6, 1)
    pfill(im, 0, 0, W - 1, 6, "0"); pfill(im, 0, 7, W - 1, 7, "K")           # 천장 모서리
    for x in range(0, W, 80):
        pfill(im, x, 8, x, 290, "K")
    # 바닥(마루): 가로 줄 + 어긋난 이음
    pfill(im, 0, 296, W - 1, H - 1, "0")
    pfill(im, 0, 290, W - 1, 295, "4"); pfill(im, 0, 290, W - 1, 290, "2"); pfill(im, 0, 296, W - 1, 296, "K")   # 걸레받이
    for k, y in enumerate((304, 316, 330, 346)):
        pfill(im, 0, y, W - 1, y, "K")
        for x in range((k * 37) % 61, W, 61):
            pfill(im, x, y, x, y + (11 if y < 330 else 14), "K")
    dith(im, (0, 297, W - 1, H - 1), "a", 5)
    # 책장(왼쪽, 메뉴 자리 아래)
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
    dith(im, (25, 127, 137, 295), "a", 5)
    # 청사진 포스터(침대 위 벽) — 지금 메인과 같은 그림, 작게
    x0, y0, x1, y1 = C_POSTER
    pfill(im, x0, y0, x1, y1, "B"); pbox(im, x0, y0, x1, y1, "K")
    for x in range(x0 + 6, x1 - 4, 6):
        pfill(im, x, y0 + 4, x, y1 - 4, "b")
    pbox(im, x0 + 22, y0 + 24, x0 + 40, y1 - 4, "C"); pbox(im, x0 + 26, y0 + 14, x0 + 36, y0 + 24, "C")
    pfill(im, x0 + 31, y0 + 8, x0 + 31, y0 + 14, "C")
    for (tx, ty) in [(x0, y0), (x1 - 3, y0), (x0, y1 - 3), (x1 - 3, y1 - 3)]:
        pfill(im, tx, ty, tx + 3, ty + 3, "h")
    # 침대 끝(발치 쪽에서 본 옆면): 프레임·매트리스·이불·베개
    pfill(im, 150, 262, 334, 292, "d"); dith(im, (151, 263, 333, 291), "K", 9); pbox(im, 150, 262, 334, 292, "K")   # 프레임
    pfill(im, 154, 292, 160, 296, "K"); pfill(im, 324, 292, 330, 296, "K")
    pfill(im, 152, 240, 332, 264, "j"); pbox(im, 152, 240, 332, 264, "K")                       # 매트리스·시트
    pfill(im, 170, 232, 332, 252, "B"); pbox(im, 170, 232, 332, 252, "K")                       # 이불
    pfill(im, 171, 233, 331, 235, "b")
    for x in range(180, 330, 22):
        draw_line(im, [(x, 236), (x + 6, 250)], "K")
    pfill(im, 154, 226, 190, 244, "2"); pbox(im, 154, 226, 190, 244, "K")                       # 베개
    pfill(im, 155, 227, 189, 229, "3"); pfill(im, 184, 230, 189, 243, "4")
    # 창
    pfill(im, 372, 34, 588, 170, "K")
    city_view(im, (378, 40, 582, 158), seed=33, horizon=0.9)
    pbox(im, 372, 34, 588, 170, "K"); pbox(im, 373, 35, 587, 169, "2"); pbox(im, 374, 36, 586, 168, "4")
    pfill(im, 444, 38, 447, 166, "2"); pfill(im, 516, 38, 519, 166, "2")
    pfill(im, 376, 96, 584, 99, "2")
    pfill(im, 366, 168, 594, 174, "2"); pfill(im, 366, 175, 594, 177, "4"); pbox(im, 366, 168, 594, 177, "K")   # 창턱
    rain(im, (378, 40, 582, 158), 7, 28, 8)
    # 서버 랙·공유기(오른쪽 끝)
    pfill(im, 596, 182, 634, 296, "K"); pbox(im, 597, 183, 633, 295, "2"); pfill(im, 598, 184, 632, 294, "0")
    for k, ry in enumerate(range(188, 290, 14)):
        pfill(im, 600, ry, 630, ry + 10, "4"); pbox(im, 600, ry, 630, ry + 10, "K")
        for i in range(3):
            pfill(im, 603 + i * 4, ry + 3, 604 + i * 4, ry + 4, ["G", "G", "Y", "R", "c", "G"][(k + i) % 6])
        for i in range(8):
            pset(im, 614 + i * 2, ry + 3, "2"); pset(im, 614 + i * 2, ry + 6, "2")
    pfill(im, 590, 166, 632, 180, "K"); pfill(im, 592, 168, 630, 178, "4"); pfill(im, 592, 168, 630, 169, "2")   # 공유기
    for i in range(4):
        pset(im, 600 + i * 6, 174, ["G", "G", "G", "Y"][i]); pset(im, 601 + i * 6, 174, ["G", "G", "G", "Y"][i])
    draw_line(im, [(600, 168), (598, 152), (602, 148)], "K", 2); draw_line(im, [(624, 168), (626, 150)], "K", 2)   # 안테나
    return im


def c_desk():
    im = canvas(W, H)
    # 책상(정면): 상판 · 앞면 · 다리 · 서랍
    pfill(im, 356, 232, 626, 238, "D"); pfill(im, 356, 232, 626, 233, "h"); pfill(im, 356, 239, 626, 241, "d")
    pbox(im, 356, 232, 626, 241, "K")
    pfill(im, 360, 242, 622, 296, "d"); dith(im, (360, 242, 622, 296), "K", 6)
    pfill(im, 356, 242, 366, 296, "K"); pfill(im, 616, 242, 626, 296, "K")
    pfill(im, 378, 246, 534, 296, "a"); dith(im, (378, 246, 534, 296), "K", 6)             # 무릎 공간(어두움)
    pbox(im, 544, 248, 608, 270, "K"); pbox(im, 545, 249, 607, 269, "d"); pfill(im, 568, 258, 584, 259, "h")
    pbox(im, 544, 272, 608, 292, "K"); pbox(im, 545, 273, 607, 291, "d"); pfill(im, 568, 282, 584, 283, "h")
    return im


def c_monitors():
    im = canvas(W, H)
    # 스탠드
    pfill(im, 474, 228, 490, 234, "2"); pbox(im, 473, 228, 491, 234, "K")
    pfill(im, 556, 228, 562, 234, "2")
    # 메인 + 세로 모니터
    for q, inner in ((C_MAIN, C_SCR), (C_SIDE, C_SIDE_SCR)):
        quad_fill(im, list(q), "K")
        tl, tr, br, bl = q
        quad_fill(im, [(tl[0] + 1, tl[1] + 1), (tr[0] - 1, tr[1] + 1), (br[0] - 1, br[1] - 1), (bl[0] + 1, bl[1] - 1)], "4")
        quad_fill(im, list(inner), "a")
        m = pmask(list(inner))
        shade_mask(im, m, ["a", "a", "k", "k", "0"], axis=(0, 1))
        def refl(lay, inner=inner):
            for off, ch in ((20, "j"), (25, "j"), (50, "0")):
                for t in range(-60, 150):
                    pset(lay, inner[0][0] + off + t, inner[0][1] + t * 2 // 3, ch)
        clip_draw(im, m, refl)
    # 키보드(마우스 없음)
    pfill(im, 438, 230, 524, 236, "K"); pfill(im, 439, 231, 523, 235, "2")
    for x in range(441, 522, 3):
        pset(im, x, 232, "3")
    pset(im, 536, 163, "c"); pset(im, 537, 163, "c")                    # 대기 표시등
    SCREENS["C"] = {"main": C_SCR, "side": C_SIDE_SCR}
    return im, mask_union(pmask(list(C_SCR)), pmask(list(C_SIDE_SCR)))


def c_props():
    im = canvas(W, H)
    # 컵라면(뚜껑 반쯤 열림)
    draw_poly(im, [(380, 216), (402, 216), (399, 232), (383, 232)], "W")
    pfill(im, 381, 221, 401, 224, "R")
    pbox(im, 380, 216, 402, 232, "K"); pfill(im, 380, 212, 402, 215, "3"); pbox(im, 380, 212, 402, 215, "K")
    # 캔
    for x, c in [(406, "R"), (418, "G")]:
        pfill(im, x, 218, x + 8, 231, c); pfill(im, x, 218, x + 8, 219, "W"); pfill(im, x + 6, 220, x + 8, 231, "r" if c == "R" else "g")
        pbox(im, x, 218, x + 8, 231, "K")
    # 작은 스탠드(책상 왼쪽 끝)
    pfill(im, 364, 228, 378, 232, "2"); pbox(im, 364, 228, 378, 232, "K")
    draw_line(im, [(371, 228), (370, 212), (378, 200), (392, 196)], "K", 3)
    draw_line(im, [(371, 228), (370, 212), (378, 200), (392, 196)], "2", 1)
    draw_poly(im, [(384, 190), (404, 196), (400, 206), (380, 200)], "K")
    draw_poly(im, [(386, 192), (401, 197), (398, 204), (383, 199)], "h")
    # 바닥 상자 더미
    pfill(im, 336, 262, 364, 296, "D"); pbox(im, 336, 262, 364, 296, "K"); pfill(im, 336, 262, 364, 264, "h")
    pfill(im, 349, 262, 351, 296, "d"); pfill(im, 340, 250, 360, 262, "D"); pbox(im, 340, 250, 360, 262, "K")
    dith(im, (337, 263, 363, 295), "K", 6)
    # 케이블: 랙 → 바닥 → 책상 뒤, 책상 뒤 → 벽
    draw_line(im, [(606, 296), (590, 312), (560, 326), (520, 322), (500, 306), (486, 296)], "K", 2)
    draw_line(im, [(612, 296), (606, 322), (570, 338), (530, 342)], "K", 2)
    draw_line(im, [(430, 234), (426, 262), (404, 290), (396, 296)], "K", 1)
    return im


def c_character():
    """한 발 물러선 시점의 주인공(작게, 뒷모습). 반환: (레이어, 마스크)."""
    lay = canvas(W, H)
    # 의자 등받이·기둥·바퀴
    ch = pmask([(452, 262), (454, 226), (462, 218), (502, 218), (510, 226), (512, 262)])
    fill_mask(lay, ch, "K")
    pfill(lay, 458, 230, 460, 258, "2")
    pfill(lay, 479, 270, 485, 292, "K"); pfill(lay, 480, 270, 481, 292, "2")
    pfill(lay, 462, 292, 502, 295, "K")
    for x in (462, 480, 500):
        pfill(lay, x - 1, 295, x + 2, 298, "2"); pfill(lay, x - 1, 298, x + 2, 298, "K")
    pfill(lay, 454, 262, 510, 272, "4"); pbox(lay, 454, 262, 510, 272, "K")               # 좌석
    # 몸통(어깨~허리)
    body = [(456, 270), (456, 240), (462, 226), (474, 221), (492, 221), (504, 226), (510, 240), (510, 270)]
    bm = pmask(body)
    shade_mask(lay, bm, ["4", "2", "4", "K"], axis=(1, 0.3), span=(456, 512))
    def folds(l):
        draw_line(l, [(466, 238), (468, 268)], "K"); draw_line(l, [(481, 236), (482, 268)], "K")
        draw_line(l, [(497, 238), (496, 266)], "K"); draw_line(l, [(462, 232), (474, 227), (490, 227)], "3")
    clip_draw(lay, bm, folds)
    outline_mask(lay, bm, "K")
    # 팔꿈치(몸 양옆으로 약간)
    for (pts, lit) in (([(452, 238), (458, 232), (462, 256), (452, 260)], False), ([(508, 232), (516, 240), (514, 260), (506, 256)], True)):
        am = pmask(pts)
        fill_mask(lay, am, "4"); outline_mask(lay, am, "K")
        if lit:
            rim(lay, am, "c", [(1, 0), (0, -1)], 1, fade=lambda x, y: 0.7)
    # 후드(목 뒤 접힘)
    hd = emask((465, 212, 499, 232))
    shade_mask(lay, hd, ["2", "4", "4", "K"], axis=(0, 1), span=(212, 232))
    draw_line(lay, [(470, 222), (482, 219), (494, 222)], "K")
    outline_mask(lay, hd, "K", where=lambda x, y: y < 222)
    # 목덜미
    pfill(lay, 478, 212, 486, 218, "s"); pfill(lay, 478, 212, 486, 213, "K")
    # 머리: 단발 + 헤드폰
    hm = mask_union(emask((467, 186, 497, 216)), pmask([(468, 202), (496, 202), (498, 220), (494, 218), (490, 221), (485, 218), (480, 221), (475, 218), (470, 221), (466, 218)]))
    fill_mask(lay, hm, "H")
    shade_mask(lay, hm, ["H", "H", "J"], axis=(0, 1), span=(186, 222))
    def hl(l):
        strands(l, 482, 467, 497, 186, 216, 221, "J", "U", step=3, lit_side=1, hi_every=3)
    clip_draw(lay, hm, hl)
    outline_mask(lay, hm, "K")
    band = canvas(W, H)
    draw_ellipse(band, (465, 183, 499, 217), "K", width=3)
    draw_ellipse(band, (466, 184, 498, 216), "2", width=1)
    bp = band.load()
    for y in range(200, 230):
        for x in range(455, 510):
            bp[x, y] = (0, 0, 0, 0)
    lay.alpha_composite(band)
    for (x0, x1, lit) in ((461, 468, False), (496, 503, True)):
        cup = pmask([(x0, 198), (x1, 198), (x1, 213), (x0, 213)])
        fill_mask(lay, cup, "2"); pbox(lay, x0, 198, x1, 213, "K")
        pfill(lay, x0 + 1, 200, x1 - 1, 201, "3")
        if lit:
            rim(lay, cup, "c", [(1, 0)], 1, fade=lambda x, y: 0.8)
    # 림 라이트(모니터가 앞=벽 쪽): 머리 위·어깨 윗선
    rim(lay, hm, "c", [(0, -1)], 1, cond=lambda x, y: y < 200, fade=lambda x, y: 0.85)
    rim(lay, hm, "C", [(0, -1)], 1, cond=lambda x, y: y < 192 and 474 < x < 492, fade=lambda x, y: 0.5)
    rim(lay, bm, "c", [(0, -1)], 1, fade=lambda x, y: 0.7)
    rim(lay, hd, "c", [(0, -1)], 1, fade=lambda x, y: 0.5)
    full = mask_union(bm, hm, hd)
    return lay, full


def make_c():
    scene = canvas(W, H, PIX["k"])
    scene.alpha_composite(c_room())
    scene.alpha_composite(c_desk())
    mons, smask = c_monitors()
    scene.alpha_composite(mons)
    scene.alpha_composite(c_props())
    sk = lambda x, y: smask.getpixel((x, y)) > 128
    # 모니터 빛 하나가 방을 비춘다(차가운 청록): 책상·벽·창틀·침대 오른쪽 일부
    relight(scene, ell(484, 200, 250, 150, 0.8), COOL1, COOL2, skip=sk)
    # 책상 스탠드(작은 따뜻한 웅덩이)
    relight(scene, ell(392, 216, 40, 34, 1.1), WARM1, WARM2, skip=sk)
    # 서버 랙 표시등이 만드는 아주 작은 빛
    relight(scene, ell(612, 236, 20, 60, 1.0), COOL1, None, skip=sk)
    # 왼쪽 가장자리 어둠(메뉴 글자 가독성)
    dith(scene, (0, 0, 200, 110), "a", 6, 2)
    chl, cm = c_character()
    scene.alpha_composite(chl)
    return scene


# =====================================================================
# 메뉴 올린 판 · 비교판 · 빌드
# =====================================================================
MENU_ITEMS = ["CONTINUE", "NEW GAME", "SETTINGS", "CREDITS"]
LOGO_BOX = (20, 14, 180, 36)
MENU_BOX = (32, 50, 116, 104)


def menu_overlay(selected=0):
    """지금 메인과 같은 구성: 로고 + 메뉴 4개(CONTINUE 선택) + 아래 키 안내줄."""
    im = canvas(W, H)
    lg = Image.open(EXPORT / "ui/logo_temp.png").convert("RGBA")
    lg = lg.resize((lg.width // 2, lg.height // 2), Image.NEAREST)
    im.alpha_composite(lg, (20, 18))
    for i, t in enumerate(MENU_ITEMS):
        bx, by = 32, 50 + i * 14
        im.alpha_composite(button(84, i == selected), (bx, by))
        draw_text(im, bx + (84 - text_width(t)) // 2, by + 3, t, "W")
        if i == selected:
            draw_text(im, bx - 10, by + 3, ">", "R")
    bar = canvas(W, 20, PIX["a"])
    im.alpha_composite(bar, (0, 340))
    x = 12
    for key, label in [("↑↓", "SELECT"), ("ENTER", "OK")]:
        b = badge(key)
        im.alpha_composite(b, (x, 344)); x += b.width + 3
        draw_text(im, x, 346, label, "3"); x += text_width(label) + 10
    return im


def zone_stats(im, box):
    """메뉴 자리 안의 가장 밝은 값·평균(가독성 점검용)."""
    x0, y0, x1, y1 = box
    vals = []
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            r, g, b, _ = im.getpixel((x, y))
            vals.append(0.299 * r + 0.587 * g + 0.114 * b)
    return max(vals), sum(vals) / len(vals)


def build():
    out = EXPORT / "preview"
    out.mkdir(parents=True, exist_ok=True)
    made = []
    menu = menu_overlay(0)
    boards = []
    for key, fn in (("A", make_a), ("B", make_b), ("C", make_c)):
        scene = fn()
        assert not palette_check(scene), key
        withmenu = over(scene, menu)
        assert not palette_check(withmenu), key + " menu"
        for name, im in ((f"main_lobby_{key}.png", scene), (f"main_lobby_{key}_x3.png", upscale(scene, 3)),
                         (f"main_lobby_{key}_menu_x3.png", upscale(withmenu, 3))):
            im.save(out / name)
            made.append(out / name)
        boards.append(withmenu)
        mx, ma = zone_stats(scene, (20, 14, 180, 104))
        print(f"  [{key}] 메뉴 자리(x20~180,y14~104) 최대 밝기 {mx:.0f}, 평균 {ma:.0f} / 화면 영역 {SCREENS[key]}")
    sheet = canvas(W, H * 3 + 8, PIX["K"])
    for i, b in enumerate(boards):
        sheet.alpha_composite(b, (0, i * (H + 4)))
    assert not palette_check(sheet)
    sheet.save(out / "main_lobby_compare.png")
    made.append(out / "main_lobby_compare.png")
    return made
