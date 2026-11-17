"""조력자(침입자) 128px 본그림 1차 — D-070, D-075. 밑그림(anim_sketch) 동작을 바탕으로 명암·디테일 추가.

- 외곽선 2px(K) [제안], 명암 3단(밝음·기본·어둠), 광원 왼쪽 위
- 걷기 4 · 기다림 2 · 떨어짐 2, 오른쪽 방향(왼쪽은 좌우 반전)
"""
import math

from PIL import Image

from anim import UNIT, sheet
from artkit import PIX, canvas, draw_ellipse, draw_line, draw_poly, palette_check, pfill, pset, save

# 역할별 색(공유 팔레트 문자)
JK, JK_D, JK_H = "Y", "y", "h"          # 점퍼
PANT, PANT_D = "2", "4"                 # 바지
SHOE = "K"
BAG, BAG_D = "3", "2"                   # 배낭
SKIN, SKIN_D = "S", "s"
CAP, CAP_D = "R", "r"
HAIR = "J"
MASK = "K"


def outline2(im):
    """불투명 영역 바깥 2px 외곽선."""
    w, h = im.size
    for _ in range(2):
        px = im.load()
        solid = {(x, y) for y in range(h) for x in range(w) if px[x, y][3]}
        for (x, y) in list(solid):
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in solid:
                    px[nx, ny] = PIX["K"]


def leg(im, hip, knee, foot, front):
    col, dark = (PANT, PANT_D) if front else (PANT_D, "K")
    draw_line(im, [hip, knee], col, 11); draw_line(im, [knee, foot], col, 10)
    draw_line(im, [(hip[0] - 3, hip[1]), (knee[0] - 3, knee[1])], dark, 3)      # 그림자 쪽
    fx, fy = foot
    draw_poly(im, [(fx - 7, fy - 7), (fx + 11, fy - 7), (fx + 14, fy), (fx - 7, fy)], SHOE)
    pfill(im, fx - 6, fy - 2, fx + 13, fy, "3")                                  # 밑창


N_FRAMES = {"walk": 16, "wait": 24, "fall": 10}   # 30 FPS 기준(D-077): 걷기 한 주기 0.53초, 기다림 0.8초, 떨어짐 0.33초


def frame(phase, i):
    """i번째 프레임. 동작은 0~1 사이 진행값 t로 계산해 프레임 수와 상관없이 매끄럽게 만든다."""
    im = canvas(UNIT, UNIT)
    cx, ground = 62, 124
    t = i / N_FRAMES[phase]
    wave = math.sin(2 * math.pi * t)
    if phase == "walk":
        bob = round(-4 * abs(math.sin(2 * math.pi * t)))
    elif phase == "wait":
        bob = round(-2 * (0.5 - 0.5 * math.cos(2 * math.pi * t)))
    else:
        bob = 0
    sink = round(30 * (i / (N_FRAMES["fall"] - 1)) ** 1.5) if phase == "fall" else 0
    hip_y = ground - 40 + bob + sink
    # 다리
    if phase == "walk":
        sw = round(18 * math.cos(2 * math.pi * t))
        leg(im, (cx - 2, hip_y), (cx - sw // 2 - 2, hip_y + 22), (cx - sw, ground), front=False)
        leg(im, (cx + 2, hip_y), (cx + sw // 2 + 2, hip_y + 22), (cx + sw, ground), front=True)
    elif phase == "fall":
        leg(im, (cx - 4, hip_y), (cx - 18, hip_y + 16), (cx - 24, hip_y + 34), front=False)
        leg(im, (cx + 4, hip_y), (cx + 20, hip_y + 14), (cx + 22, hip_y + 32), front=True)
    else:
        leg(im, (cx - 7, hip_y), (cx - 7, hip_y + 22), (cx - 8, ground), front=False)
        leg(im, (cx + 7, hip_y), (cx + 7, hip_y + 22), (cx + 8, ground), front=True)
    top = hip_y - 34
    # 배낭(몸 뒤)
    pfill(im, cx - 36, top + 4, cx - 18, hip_y - 4, BAG); pfill(im, cx - 36, top + 4, cx - 30, hip_y - 4, BAG_D)
    pfill(im, cx - 34, top + 14, cx - 20, top + 20, BAG_D)                         # 주머니
    # 몸통(점퍼) — 명암 3단
    draw_poly(im, [(cx - 19, top), (cx + 17, top), (cx + 20, hip_y + 2), (cx - 21, hip_y + 2)], JK)
    draw_poly(im, [(cx - 19, top), (cx - 9, top), (cx - 11, hip_y + 2), (cx - 21, hip_y + 2)], JK_D)     # 뒤쪽 그림자
    draw_line(im, [(cx + 10, top + 4), (cx + 13, hip_y - 4)], JK_H, 3)                                  # 빛 받는 면
    pfill(im, cx - 21, hip_y - 4, cx + 20, hip_y + 2, JK_D)                                              # 밑단
    draw_line(im, [(cx + 2, top + 2), (cx + 3, hip_y)], JK_D, 2)                                         # 지퍼
    draw_line(im, [(cx - 12, top), (cx - 16, hip_y - 6)], BAG_D, 3)                                      # 배낭 끈
    # 팔
    if phase == "walk":
        a = round(-16 * math.cos(2 * math.pi * t))
        draw_line(im, [(cx + 6, top + 6), (cx + 6 - a // 2, top + 22), (cx + 6 - a, top + 34)], JK, 9)
        draw_ellipse(im, (cx + 2 - a, top + 32, cx + 10 - a, top + 40), SKIN)
    elif phase == "wait":
        ry = top + 22 - round(8 * (0.5 - 0.5 * math.cos(2 * math.pi * t)))
        draw_line(im, [(cx + 8, top + 6), (cx + 20, top + 16), (cx + 24, ry)], JK, 9)                    # 손 들어 신호 기다림
        draw_ellipse(im, (cx + 20, ry - 6, cx + 30, ry + 4), SKIN)
    else:
        for s in (1, -1):
            draw_line(im, [(cx + 6 * s, top + 6), (cx + 24 * s, top - 14)], JK, 9)
            draw_ellipse(im, (cx + 20 * s - 5, top - 22, cx + 20 * s + 5, top - 12), SKIN)
    # 목·머리
    pfill(im, cx - 4, top - 6, cx + 6, top + 2, SKIN_D)
    hy = top - 20
    draw_ellipse(im, (cx - 16, hy - 16, cx + 18, hy + 16), SKIN)
    draw_ellipse(im, (cx - 16, hy - 10, cx - 2, hy + 14), SKIN_D)                                        # 뒤통수 그림자
    draw_poly(im, [(cx - 18, hy - 4), (cx - 6, hy - 6), (cx - 10, hy + 10), (cx - 18, hy + 6)], HAIR)  # 뒷머리
    pfill(im, cx - 1, hy - 1, cx + 19, hy + 11, MASK)                                                    # 입 가리개
    for x in range(cx + 2, cx + 18, 4):
        pset(im, x, hy + 4, "2")                                                                         # 천 결
    pfill(im, cx + 7, hy - 6, cx + 11, hy - 3, "K"); pset(im, cx + 8, hy - 6, "W")                      # 눈
    draw_line(im, [(cx + 4, hy - 9), (cx + 12, hy - 10)], HAIR, 2)                                       # 눈썹
    # 모자
    draw_poly(im, [(cx - 19, hy - 8), (cx - 14, hy - 22), (cx + 10, hy - 24), (cx + 19, hy - 10)], CAP)
    draw_poly(im, [(cx - 19, hy - 8), (cx - 14, hy - 22), (cx - 6, hy - 23), (cx - 9, hy - 8)], CAP_D)
    pfill(im, cx + 12, hy - 12, cx + 34, hy - 8, CAP_D)                                                  # 챙
    pfill(im, cx - 2, hy - 19, cx + 5, hy - 13, "W"); pset(im, cx + 1, hy - 16, CAP)                    # 모자 로고
    outline2(im)
    return im


def build():
    made = []
    for phase, fps, loop in (("walk", 30, True), ("wait", 30, True), ("fall", 30, False)):
        n = N_FRAMES[phase]
        frames = [frame(phase, i) for i in range(n)]
        for f in frames:
            assert not palette_check(f), phase
        made += sheet(frames, f"anim/runner128_{phase}", fps=fps, loop=loop, status="본그림 1차")
        made += sheet([f.transpose(Image.Transpose.FLIP_LEFT_RIGHT) for f in frames], f"anim/runner128_{phase}_left",
                      fps=fps, loop=loop, status="본그림 1차")
    board = canvas(UNIT * 8 + 72, UNIT + 16, PIX["k"])
    fr = [frame("walk", i) for i in (0, 4, 8, 12)] + [frame("wait", i) for i in (0, 12)] + [frame("fall", i) for i in (3, 9)]
    for i, f in enumerate(fr):
        board.alpha_composite(f, (8 + i * (UNIT + 8), 8))
    made.append(save(board, "preview/anim/runner128_board.png", {"status": "본그림 1차"}))
    return made
