"""128px(4배) 소품·가구 그리기 도구 — D-073(L2 소품·L3 조명), D-075(4배), D-078.

규칙: 광원 왼쪽 위, 명암 3단(밝음·기본·어둠), 바깥 외곽선 2px(K), 안티앨리어싱 없음.
"""
from PIL import Image

from artkit import PIX, canvas, pbox, pfill, pset


def box3(im, x0, y0, x1, y1, base, light, dark, edge="K", bevel=4):
    """명암 3단 상자: 위·왼쪽 밝음, 아래·오른쪽 어둠, 바깥 2px 외곽선."""
    pfill(im, x0, y0, x1, y1, edge)
    pfill(im, x0 + 2, y0 + 2, x1 - 2, y1 - 2, base)
    pfill(im, x0 + 2, y0 + 2, x1 - 2, y0 + 1 + bevel, light)
    pfill(im, x0 + 2, y0 + 2, x0 + 1 + bevel // 2, y1 - 2, light)
    pfill(im, x0 + 2, y1 - 1 - bevel, x1 - 2, y1 - 2, dark)
    pfill(im, x1 - 1 - bevel // 2, y0 + 2, x1 - 2, y1 - 2, dark)


def wood(im, x0, y0, x1, y1, base="D", dark="d", light="h", plank=24, vertical=False):
    """나무판: 판 경계선 + 결 몇 줄."""
    pfill(im, x0, y0, x1, y1, base)
    if vertical:
        for x in range(x0, x1, plank):
            pfill(im, x, y0, x + 1, y1, dark)
            for y in range(y0 + 6, y1, 22):
                pfill(im, x + 6, y, x + 8, y + 9, dark)
    else:
        for y in range(y0, y1, plank):
            pfill(im, x0, y, x1, y + 1, dark)
            pfill(im, x0, y + 2, x1, y + 2, light)
            for x in range(x0 + (y // plank) * 17 % 60, x1, 60):
                pfill(im, x, y + 8, x + 14, y + 9, dark)


def dither_fill(im, x0, y0, x1, y1, ch, step=2, phase=0):
    for y in range(max(0, y0), min(im.height, y1 + 1)):
        for x in range(max(0, x0), min(im.width, x1 + 1)):
            if (x + y + phase) % step == 0:
                im.putpixel((x, y), PIX[ch])


def light_pool(w, h, cx, cy, rx, ry, ch="h"):
    """조명 빛 웅덩이(L3): 바깥으로 갈수록 성긴 디더. 투명 배경."""
    im = canvas(w, h)
    for y in range(h):
        for x in range(w):
            d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
            # 빛은 성기게(소품을 덮지 않도록): 중심 1/8, 가장자리 1/32 밀도
            if d < 0.45 and (x % 4 == 0 and y % 2 == 0) and ((x // 4 + y // 2) % 2 == 0):
                im.putpixel((x, y), PIX[ch])
            elif d < 1.0 and (x % 8 == 0 and y % 4 == 0):
                im.putpixel((x, y), PIX[ch])
    return im


def rivets(im, x0, y0, x1, y1, step=16, ch="3"):
    for x in range(x0, x1, step):
        pfill(im, x, y0, x + 1, y0 + 1, ch); pfill(im, x, y1 - 1, x + 1, y1, ch)


# ---------- 조명·음영 규칙 도구 — Docs/Art/lighting-shading.md(D-079) ----------
# 빛·그림자 점은 2×2px 단위(DOT): 화면 축소(Bilinear, D-076)에서도 1px 점처럼 사라지지 않도록.
DOT = 2


def _dot(im, x, y, ch, dot=DOT):
    if 0 <= x < im.width and 0 <= y < im.height:
        pfill(im, x, y, min(im.width - 1, x + dot - 1), min(im.height - 1, y + dot - 1), ch)


def _level_hit(gx, gy, level):
    """밀도 단계(점 격자 기준): 3=1/2, 2=1/4, 1=1/8, 0.5=1/16 칸에 점. 0은 없음."""
    if level >= 3:
        return (gx + gy) % 2 == 0
    if level == 2:
        return gx % 2 == 0 and gy % 2 == 0
    if level == 1:
        return gx % 4 == 0 and gy % 2 == 0 and (gx // 4 + gy // 2) % 2 == 0
    if level == 0.5:
        return gx % 4 == 0 and gy % 4 == 0 and (gx // 4 + gy // 4) % 2 == 0
    return False


def light_cone(w, h, x, y0, top_half, bottom_half, y1, ch="h", core=0.35, dot=DOT):
    """천장 등 빛(L3): 기구 아래(y0)에서 바닥(y1)으로 퍼지는 사다리꼴. 위로는 퍼지지 않는다.
    중심 core 비율 안은 1단계(1/8), 나머지는 0.5단계(1/16). 바닥선 띠는 2단계(빛이 바닥에 닿은 자국)."""
    im = canvas(w, h)
    for gy in range(0, (y1 - y0) // dot):
        y = y0 + gy * dot
        t = (y - y0) / max(1, y1 - y0)
        half = top_half + (bottom_half - top_half) * t
        for gx in range(0, w // dot):
            px = gx * dot
            d = abs(px - x) / max(1, half)
            if d >= 1:
                continue
            level = 1 if d < core else 0.5
            if y >= y1 - 3 * dot:
                level = 2 if d < 0.8 else 1
            if _level_hit(gx, gy, level):
                _dot(im, px, y, ch, dot)
    return im


def glow(w, h, cx, cy, rx, ry, ch="h", dot=DOT, y_min=0, y_max=None):
    """작은 광원 빛(L3): 모니터·표시등·전구 주변 타원. 중심 1단계(1/8), 가장자리 0.5단계(1/16)."""
    im = canvas(w, h)
    y_max = h if y_max is None else y_max                                  # 천장선·바닥선 밖으로 빛을 그리지 않는다
    for gy in range(y_min // dot, y_max // dot):
        for gx in range(0, w // dot):
            x, y = gx * dot, gy * dot
            d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
            level = 1 if d < 0.4 else (0.5 if d < 1 else 0)
            if _level_hit(gx, gy, level):
                _dot(im, x, y, ch, dot)
    return im


def contact_shadow(im, x0, x1, y, ch="a", dot=DOT):
    """바닥 접지 그림자(L2 소품 그림에 포함): 물건 밑면 아래 2단 — 가운데 진하게, 양 끝 디더."""
    w = x1 - x0
    pad = max(4, w // 10)
    pfill(im, x0 + pad, y, x1 - pad, y + dot - 1, ch)                     # 가운데 실선
    for gx in range((x0 - pad) // dot, (x1 + pad) // dot):                # 아래 줄·양 끝 디더
        if gx % 2 == 0:
            _dot(im, gx * dot, y + dot, ch, dot)
    for gx in range(x0 // dot, (x0 + pad) // dot):
        if gx % 2 == 0:
            _dot(im, gx * dot, y, ch, dot)
    for gx in range((x1 - pad) // dot, x1 // dot):
        if gx % 2 == 0:
            _dot(im, gx * dot, y, ch, dot)


def ambient(im, ceil_y, floor_y, ch="a", depth=40, dot=DOT):
    """방 껍데기(L1) 환경 음영: 천장 아래·바닥 위·좌우 벽 가장자리를 디더로 어둡게(빛이 덜 닿는 곳)."""
    w = im.width
    bands = [(ceil_y, ceil_y + depth // 2, 2), (ceil_y + depth // 2, ceil_y + depth, 1),
             (floor_y - depth // 2, floor_y, 1)]
    for (ya, yb, level) in bands:
        for gy in range(ya // dot, yb // dot):
            for gx in range(0, w // dot):
                if _level_hit(gx, gy, level):
                    _dot(im, gx * dot, gy * dot, ch, dot)
    for (xa, xb) in [(0, depth), (w - depth, w)]:
        for gy in range(ceil_y // dot, floor_y // dot):
            for gx in range(xa // dot, xb // dot):
                edge = min(gx * dot - xa, xb - gx * dot) if xa else xb - gx * dot
                level = 1 if (xa == 0 and gx * dot < depth // 2) or (xa > 0 and gx * dot > w - depth // 2) else 0
                if _level_hit(gx, gy, level):
                    _dot(im, gx * dot, gy * dot, ch, dot)


def dark_overlay(w, h, ceil_y, floor_y, ch="a", level=3, dot=DOT):
    """조명 꺼짐 어둠(L3 꺼짐 상태): 방 안쪽 전체에 균일 디더. 켜진 조명이 하나도 없을 때만 그린다."""
    im = canvas(w, h)
    for gy in range(ceil_y // dot, floor_y // dot):
        for gx in range(0, w // dot):
            if _level_hit(gx, gy, level):
                _dot(im, gx * dot, gy * dot, ch, dot)
    return im
