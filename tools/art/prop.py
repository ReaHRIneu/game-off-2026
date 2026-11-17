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
