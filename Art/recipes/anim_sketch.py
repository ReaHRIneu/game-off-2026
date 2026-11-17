"""128px 애니메이션 밑그림(D-075) — animation-audit §1 묶음별 대표 동작.

밑그림 = 실루엣·덩어리·동작 흐름 확인용. 세부 묘사·명암·질감은 본그림 단계에서.
- D 인물: 조력자 걷기 4 · 기다림 2 · 떨어짐 2 (128×128, 바닥 가운데 기준)
- A 상태 전환: 문 열림 2(48×240), 해치 열림 2(160×40), 레이저 켜짐 2(섹터 640×384 중 막대만)
- C 통과 표지: 단말 대기 2(80×112), 중계기 대기 2(64×120)
판정은 즉시, 그림은 따라간다(audit §3-1). 전환 2프레임, 반복 2~4프레임, 8 FPS.
"""
import math

from PIL import Image

from anim import UNIT, sheet
from artkit import PIX, canvas, draw_ellipse, draw_line, draw_poly, outline_outside, palette_check, pbox, pfill, pset, save, upscale

# 밑그림 색: 덩어리는 중간 회색 계열 + 식별 색 1~2개만
BODY, BODY_D, SKIN, ACCENT, ACCENT_D, LINE = "3", "2", "S", "Y", "y", "K"


def runner_frame(phase: str, i: int) -> Image.Image:
    """조력자 128×128. phase: walk(0~3) / wait(0~1) / fall(0~1). 오른쪽을 봄."""
    im = canvas(UNIT, UNIT)
    cx, ground = 64, 124
    bob = {"walk": [0, -4, 0, -4], "wait": [0, -2], "fall": [0, 0]}[phase][i]
    sink = [12, 30][i] if phase == "fall" else 0       # 떨어짐: 해치 아래로 빠져 내려감(캔버스 위로 나가지 않게)
    hip = ground - 44 + bob + sink
    # 다리(걷기: 앞뒤로 벌림)
    if phase == "walk":
        sw = [16, 4, -16, -4][i]
        for s, col in ((sw, BODY_D), (-sw, BODY)):
            draw_line(im, [(cx, hip), (cx + s, ground - 20), (cx + s - 2, ground)], col, 9)
    elif phase == "fall":
        draw_line(im, [(cx, hip), (cx - 16, hip + 30)], BODY_D, 9); draw_line(im, [(cx, hip), (cx + 18, hip + 26)], BODY, 9)
    else:
        for s in (-7, 7):
            draw_line(im, [(cx + s, hip), (cx + s, ground)], BODY, 9)
    # 몸통(점퍼)
    top = hip - 40
    draw_poly(im, [(cx - 18, top), (cx + 16, top), (cx + 18, hip), (cx - 20, hip)], ACCENT)
    pfill(im, cx - 20, hip - 6, cx + 18, hip, ACCENT_D)
    # 배낭
    pfill(im, cx - 34, top + 4, cx - 18, hip - 6, BODY_D)
    # 팔
    if phase == "walk":
        a = [-14, 0, 14, 0][i]
        draw_line(im, [(cx + 6, top + 6), (cx + 6 - a, top + 32)], ACCENT, 8)
    elif phase == "wait":
        draw_line(im, [(cx + 8, top + 6), (cx + 20, top + 20 - i * 6)], ACCENT, 8)   # 손 들어 신호 기다림
    else:
        draw_line(im, [(cx + 4, top + 6), (cx + 26, top - 14)], ACCENT, 8); draw_line(im, [(cx - 6, top + 6), (cx - 26, top - 12)], ACCENT, 8)
    # 머리 + 모자
    hy = top - 22
    draw_ellipse(im, (cx - 16, hy - 18, cx + 16, hy + 14), SKIN)
    pfill(im, cx - 2, hy - 2, cx + 16, hy + 8, LINE)                                # 입 가리개
    draw_poly(im, [(cx - 18, hy - 10), (cx + 14, hy - 22), (cx + 18, hy - 10)], "R")
    pfill(im, cx + 10, hy - 12, cx + 30, hy - 8, "r")                               # 챙
    pfill(im, cx + 6, hy - 4, cx + 9, hy - 1, LINE)                                 # 눈
    outline_outside(im, LINE)
    return im


def door_frame(i: int) -> Image.Image:
    """문 48×240. 0 = 반쯤 열림, 1 = 다 열림(닫힘은 기존 정지 그림)."""
    im = canvas(48, 240)
    pfill(im, 0, 0, 47, 239, LINE)
    leaf = [24, 8][i]
    pfill(im, 4, 4, 4 + leaf, 235, "D"); pfill(im, 43 - leaf, 4, 43, 235, "D")
    pfill(im, 4 + leaf + 1, 4, 42 - leaf, 235, "a")
    return im


def hatch_frame(i: int) -> Image.Image:
    """해치 160×40. 0 = 덮개 반쯤 젖힘, 1 = 다 젖힘."""
    im = canvas(160, 40)
    pfill(im, 0, 24, 159, 39, LINE); pfill(im, 8, 26, 151, 39, "a")
    if i == 0:
        draw_poly(im, [(0, 24), (60, 6), (66, 14), (6, 32)], "3")
    else:
        pfill(im, 0, 0, 10, 39, "3")
    outline_outside(im, LINE)
    return im


def laser_frame(i: int) -> Image.Image:
    """레이저 켜짐 전환(섹터 640×384 안의 막대만). 0 = 절반 켜짐(점선), 1 = 다 켜짐."""
    im = canvas(640, 384)
    for x in range(32, 608, 48):
        if i == 1:
            pfill(im, x, 32, x + 5, 360, "R")
        else:
            for y in range(32, 360, 24):
                pfill(im, x, y, x + 5, y + 11, "R")
    pfill(im, 16, 24, 623, 31, "r")
    return im


def terminal_frame(i: int) -> Image.Image:
    """단말 80×112, 화면 깜빡임 2프레임(목표 강조 — C 묶음만 반복 애니메이션)."""
    im = canvas(80, 112)
    pfill(im, 16, 48, 63, 111, BODY_D); pfill(im, 0, 0, 79, 48, LINE)
    pfill(im, 6, 6, 73, 42, "Y" if i == 0 else "h")
    for y in (14, 24, 34):
        pfill(im, 14, y, 60 - (y % 20), y + 2, "y")
    outline_outside(im, LINE)
    return im


def relay_frame(i: int) -> Image.Image:
    """중계기 64×120, 안테나 불빛 2프레임."""
    im = canvas(64, 120)
    pfill(im, 28, 0, 35, 48, LINE); pfill(im, 24, 0, 39, 8, "G" if i == 0 else "g")
    pfill(im, 8, 48, 55, 119, BODY); pfill(im, 16, 88, 47, 111, BODY_D)
    pfill(im, 28, 64, 35, 71, "G" if i == 0 else "g")
    outline_outside(im, LINE)
    return im


def build():
    made = []
    groups = {
        "anim/runner_walk": ([runner_frame("walk", i) for i in range(4)], dict(fps=8, loop=True)),
        "anim/runner_wait": ([runner_frame("wait", i) for i in range(2)], dict(fps=4, loop=True)),
        "anim/runner_fall": ([runner_frame("fall", i) for i in range(2)], dict(fps=8, loop=False)),
        "anim/door_open": ([door_frame(i) for i in range(2)], dict(fps=30, loop=False)),
        "anim/hatch_open": ([hatch_frame(i) for i in range(2)], dict(fps=30, loop=False)),
        "anim/laser_on": ([laser_frame(i) for i in range(2)], dict(fps=30, loop=False, pivot="top-left")),
        "anim/terminal_idle": ([terminal_frame(i) for i in range(2)], dict(fps=2, loop=True)),
        "anim/relay_idle": ([relay_frame(i) for i in range(2)], dict(fps=2, loop=True)),
    }
    for rel, (frames, kw) in groups.items():
        for f in frames:
            assert not palette_check(f), rel
        made += sheet(frames, rel, **kw)
    # 한눈에 보는 밑그림 판(인물 8프레임)
    board = canvas(UNIT * 8 + 9 * 8, UNIT + 16, PIX["k"])
    frames = [runner_frame("walk", i) for i in range(4)] + [runner_frame("wait", i) for i in range(2)] + [runner_frame("fall", i) for i in range(2)]
    for i, f in enumerate(frames):
        board.alpha_composite(f, (8 + i * (UNIT + 8), 8))
    made.append(save(board, "preview/anim/runner_board.png", {"status": "밑그림"}))
    return made
