"""러너 128px 본그림 v2 — 도트 격자판(W-040 R2). 몸 부품은 Art/recipes/grids/runner_*.txt 의 손으로 찍은 문자 격자.

- 한 글자 = 한 픽셀. 도형 함수·자동 외곽선·자동 명암을 쓰지 않는다. 레시피는 격자를 위치에 붙이고 뒤쪽 다리 색만 바꾼다.
- 프레임 수(걷기 16·기다림 24·떨어짐 10), 128px, 발 기준선, 시트 배치, 타이밍은 char_runner128.py 와 같다.
- 키 포즈: 걷기 8, 기다림 4, 떨어짐 4 — 같은 키 안에서는 위아래 이동만 한다.
"""
import math
from pathlib import Path

from PIL import Image

from anim import UNIT, sheet
from artkit import PIX, canvas, palette_check, pmap, save
from char_runner128 import N_FRAMES

GRID = Path(__file__).parent / "grids"
FAR = str.maketrans({"x": "z", "z": "E", "W": "3", "3": "2", "Y": "y", "2": "K"})   # 뒤쪽 다리(그림자 쪽) 색 바꿈


def load(name, far=False):
    rows = [l.rstrip("\n") for l in open(GRID / f"runner_{name}.txt", encoding="utf-8") if not l.startswith("#")]
    w = max(map(len, rows))
    rows = [r.ljust(w, ".") for r in rows]
    if far:
        rows = [r.translate(FAR) for r in rows]
    return pmap(rows)


G = {n: load(n) for n in ("head", "torso", "leg_pass", "leg_fwd", "leg_back", "arm_hang", "arm_fwd", "arm_back", "arm_wait", "arm_up")}
GF = {n: load(n, far=True) for n in ("leg_pass", "leg_fwd", "leg_back")}
LEG_ANCHOR = {"leg_pass": 10, "leg_fwd": 10, "leg_back": 32}
ARM_ANCHOR = {"arm_hang": 5, "arm_fwd": 5, "arm_back": 15, "arm_wait": 5}


def put(im, g, x, y):
    im.paste(g, (x, y), g)


def walk_key(k):
    sw = 18 * math.cos(2 * math.pi * k / 8)
    leg = "leg_fwd" if sw > 10 else ("leg_back" if sw < -10 else "leg_pass")
    back = {"leg_fwd": "leg_back", "leg_back": "leg_fwd", "leg_pass": "leg_pass"}[leg]
    arm = "arm_fwd" if sw > 6 else ("arm_back" if sw < -6 else "arm_hang")
    return leg, back, arm


def frame(phase, i):
    im = canvas(UNIT, UNIT)
    cx, ground = 62, 124
    t = i / N_FRAMES[phase]
    if phase == "walk":
        bob = round(-4 * abs(math.sin(2 * math.pi * t)))
        nl, fl, arm = walk_key(i * 8 // N_FRAMES["walk"])
        fx, bx = cx + 2, cx - 2
    elif phase == "wait":
        bob = round(-2 * (0.5 - 0.5 * math.cos(2 * math.pi * t)))
        nl = fl = "leg_pass"; arm = "arm_wait"
        fx, bx = cx + 7, cx - 7
    else:
        bob = 0
        k = min(3, i * 4 // N_FRAMES["fall"])
        nl, fl = ("leg_pass", "leg_pass") if k == 0 else ("leg_fwd", "leg_back")
        arm = "arm_up"
        fx, bx = cx + 4, cx - 4
    sink = round(30 * (i / (N_FRAMES["fall"] - 1)) ** 1.5) if phase == "fall" else 0
    hip = ground - 40 + bob + sink
    top = hip - 34
    # 뒤쪽 다리 → 앞쪽 다리 → 몸통 → (떨어짐: 먼 쪽 팔) → 머리 → 가까운 팔
    put(im, GF[fl], bx - LEG_ANCHOR[fl], hip + 1)
    put(im, G[nl], fx - LEG_ANCHOR[nl], hip + 1)
    if phase == "fall":
        far = G["arm_up"].transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        put(im, far, cx - 6 - far.width + 5, hip - 27 - (far.height - 1))
    put(im, G["head"], cx - 22, hip - 83)
    put(im, G["torso"], cx - 22, hip - 35)
    if phase == "fall":
        put(im, G["arm_up"], cx + 2 - 5, hip - 27 - (G["arm_up"].height - 1))
    else:
        dy = -round(3 * (0.5 - 0.5 * math.cos(2 * math.pi * t))) if phase == "wait" else 0
        sx = cx + 2
        put(im, G[arm], sx - ARM_ANCHOR[arm], hip - 29 + dy)
    return im


def build():
    made = []
    for phase, fps, loop in (("walk", 30, True), ("wait", 30, True), ("fall", 30, False)):
        frames = [frame(phase, i) for i in range(N_FRAMES[phase])]
        for f in frames:
            assert not palette_check(f), phase
        made += sheet(frames, f"anim/runner128_v2_{phase}", fps=fps, loop=loop, status="본그림 v2(도트 격자)")
        made += sheet([f.transpose(Image.Transpose.FLIP_LEFT_RIGHT) for f in frames], f"anim/runner128_v2_{phase}_left",
                      fps=fps, loop=loop, status="본그림 v2(도트 격자)")
    board = canvas(UNIT * 8 + 72, UNIT + 16, PIX["k"])
    fr = [frame("walk", i) for i in (0, 4, 8, 12)] + [frame("wait", i) for i in (0, 12)] + [frame("fall", i) for i in (3, 9)]
    for i, f in enumerate(fr):
        board.alpha_composite(f, (8 + i * (UNIT + 8), 8))
    made.append(save(board, "preview/anim/runner128_v2_board.png", {"status": "본그림 v2(도트 격자)"}))
    return made
