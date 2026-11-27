"""러너 128px 본그림 v3 — pixelstudio 레시피(W-042, D-130). 약 6등신, 팔은 몸 옆에서 흔든다.

- v2(문자 격자)는 남겨 두고 이 레시피는 `anim/runner128_v3_*` 로 저장한다. 프레임 수·128px·시트 배치·holds·pivot 은 v2와 같다.
- 관절 각도(허벅지·무릎·어깨·팔꿈치)를 프레임마다 계산해 팔다리를 그리고, 가장 낮은 발바닥이 항상 기준선(y=124)에 닿게 몸 높이를 맞춘다.
- 원화(Art/External/2026-11-25)는 눈으로 보고 식별점(갈색 단발+꽁지·주황 크롭 재킷+노랑 줄·카키 카고·회색 운동화·허리 가방)만 참고했다. 레시피는 열지 않는다.
- 광원 왼쪽 위(D-079). 색은 artkit.PIX 47색만.
[시안] 사용자 확정 전.
"""
import math

from PIL import Image

from ps_char import Sc, C, Sprite, capsule, grid_blit, new_layer, outline, palette_check, rnd, rot, sheet, save, PIX
from char_runner128 import N_FRAMES

UNIT = 128
GROUND = 123
STATUS = "본그림 v3(pixelstudio)"

# ---- 몸 치수(약 6등신: 머리 19px) ----
THIGH, SHIN = 27, 26
UPPER, FORE = 16, 14
TORSO = 27            # 엉덩이 → 어깨


def draw_head(mood="smile", blink=False, f=1.0):
    """머리(오른쪽 3/4). 30×26 투명 이미지. 얼굴 높이 19px(턱~정수리), 위로 삐친 머리 3px."""
    s = Sprite(30, 26) if f == 1.0 else Sc(int(30 * f) + 2, int(26 * f) + 2, f)
    # 뒷머리(단발) — 갈색 덩어리
    new_layer(s)
    s.ellipse(2, 3, 16, 17, C["p"])
    s.polygon([(2, 9), (2, 18), (4, 20), (8, 20), (9, 12)], C["p"])
    s.rect(2, 11, 4, 19, C["E"], only=C["p"])
    s.rect(4, 17, 8, 20, C["E"], only=C["p"])
    for x, y in ((3, 20), (4, 20), (6, 20), (7, 20), (3, 19), (8, 19)):
        s.px(x, y, C["O"])
    # 꽁지(뒤쪽 작은 묶음)
    s.polygon([(0, 14), (3, 13), (3, 18), (1, 19)], C["p"])
    s.px(0, 17, C["O"]); s.px(1, 18, C["O"]); s.px(1, 19, C["O"]); s.px(0, 15, C["E"])
    outline(s)
    # 얼굴
    new_layer(s)
    s.polygon([(6, 7), (17, 7), (19, 10), (19, 14), (18, 18), (15, 21), (10, 21), (7, 18), (6, 12)], C["S"])
    s.px(20, 14, C["S"]); s.px(20, 15, C["S"])                        # 코끝
    s.rect(7, 10, 19, 10, C["s"], only=C["S"])                         # 앞머리 아래 그림자
    s.rect(11, 20, 15, 21, C["s"], only=C["S"])                        # 턱 아래 그림자
    for x, y in ((18, 18), (17, 19), (16, 20), (18, 17)):
        s.px(x, y, C["s"])
    s.rect(10, 15, 12, 16, C["l"], only=C["S"])                        # 볼 하이라이트
    outline(s)
    # 귀 이어폰(옆머리 사이로 살짝)
    new_layer(s)
    s.rect(7, 13, 8, 15, C["S"]); s.px(8, 14, C["c"]); s.px(8, 15, C["T"]); s.px(7, 14, C["K"])
    # 앞머리(윗머리 + 큰 덩어리 3개로 끝나는 앞머리)
    new_layer(s)
    s.ellipse(2, 3, 18, 11, C["p"])
    s.polygon([(3, 8), (3, 13), (5, 17), (9, 17), (9, 9)], C["p"])
    s.polygon([(3, 7), (19, 7), (19, 9), (17, 9), (16, 11), (14, 9), (12, 11), (10, 9), (9, 12), (6, 12)], C["p"])
    s.polygon([(4, 6), (7, 4), (11, 4), (10, 7), (6, 8)], C["D"], only=C["p"])     # 정수리 하이라이트
    s.rect(3, 12, 5, 17, C["E"], only=C["p"])
    s.polygon([(10, 9), (12, 11), (14, 9), (16, 11), (17, 9), (19, 9), (19, 8), (10, 8)], C["E"], only=C["p"])
    for x, y in ((16, 11), (12, 11), (9, 12), (9, 13), (18, 9), (19, 9), (5, 17), (7, 17)):
        s.px(x, y, C["O"])
    # 삐친 머리
    s.polygon([(9, 3), (10, 0), (13, 0), (12, 3)], C["p"])
    s.px(10, 0, C["D"]); s.px(11, 0, C["D"])
    outline(s)
    # 머리핀(노랑)
    new_layer(s)
    s.px(12, 6, C["Y"]); s.px(13, 6, C["Y"]); s.px(13, 7, C["y"])
    # 눈·입
    new_layer(s)
    ex, ey = 13, 12
    if blink:
        s.line(ex, ey + 2, ex + 2, ey + 2, C["K"]); s.line(17, ey + 2, 18, ey + 2, C["K"])
    else:
        grid_blit(s, ex, ey, ["KKK", "TWT", "TTc", "cc."])
        grid_blit(s, 17, ey, ["KK", "TT", "Tc"])
    if mood == "smile":
        grid_blit(s, 14, 18, ["KWWWK", ".KKK."])
    elif mood == "open":
        grid_blit(s, 15, 17, ["KKK", "KmK", "KKK"])
    return s.composite()


HEAD = {}


def head_img(key, f=1.0):
    if (key, f) not in HEAD:
        HEAD[(key, f)] = draw_head(*key, f=f)
    return HEAD[(key, f)]
    if key not in HEAD:
        HEAD[key] = draw_head(*key)
    return HEAD[key]


def limb_leg(s, hip, a, k, foot_ang, near=True):
    """다리 하나. a=허벅지 각도(아래=0, 앞=+), k=무릎 굽힘(뒤로 접힘). 반환: 발목 위치·발바닥 최저 y."""
    th = math.radians(a)
    sh = math.radians(a - k)
    knee = (hip[0] + THIGH * math.sin(th), hip[1] + THIGH * math.cos(th))
    ank = (knee[0] + SHIN * math.sin(sh), knee[1] + SHIN * math.cos(sh))
    return knee, ank


def draw_leg(s, hip, knee, ank, foot_ang, near):
    base, dark, light = ("x", "z", None) if near else ("z", "E", None)
    new_layer(s)
    capsule(s, hip, knee, 12, 10, base, dark=dark)
    capsule(s, knee, ank, 10, 8, base, dark=dark)
    # 카고 바지 주머니(허벅지 옆)
    if near:
        mid = ((hip[0] + knee[0]) / 2, (hip[1] + knee[1]) / 2 - 1)
        s.rect(round(mid[0]) - 2, round(mid[1]) - 2, round(mid[0]) + 2, round(mid[1]) + 3, C["z"])
        s.line(round(mid[0]) - 2, round(mid[1]) - 1, round(mid[0]) + 2, round(mid[1]) - 1, C["x"])
    # 밑단(조거 커프스)
    cuff0 = (ank[0] + (knee[0] - ank[0]) * 0.18, ank[1] + (knee[1] - ank[1]) * 0.18)
    capsule(s, cuff0, ank, 8, 7, "z", dark="E" if near else "K", round_ends=False)
    # 운동화
    sh = []
    for p in [(-3, -2), (3, -2), (6, 1), (10, 3), (10, 6), (-4, 6), (-4, 2)]:
        q = rot(p, foot_ang)
        sh.append((ank[0] + q[0] + 1, ank[1] + q[1] + 1))
    s.polygon([rnd(p) for p in sh], C["3"] if near else C["2"])
    sole = [rot(p, foot_ang) for p in [(-4, 5), (10, 5), (10, 6), (-4, 6)]]
    s.polygon([rnd((ank[0] + q[0] + 1, ank[1] + q[1] + 1)) for q in sole], C["W"] if near else C["3"])
    t = rot((7, 2), foot_ang)
    s.px(round(ank[0] + t[0] + 1), round(ank[1] + t[1] + 1), C["W"] if near else C["3"])
    outline(s)
    return s


def arm_pts(sh, a1, flex):
    """어깨 → 팔꿈치 → 손목. a1=위팔 각도(아래=0, 앞=+), flex=팔꿈치 접힘(앞으로)."""
    t1 = math.radians(a1)
    t2 = math.radians(a1 + flex)
    el = (sh[0] + UPPER * math.sin(t1), sh[1] + UPPER * math.cos(t1))
    wr = (el[0] + FORE * math.sin(t2), el[1] + FORE * math.cos(t2))
    return el, wr, a1 + flex


def draw_arm(s, sh, el, wr, wang, near):
    new_layer(s)
    b, d = ("O", "o") if near else ("o", "K")
    capsule(s, sh, el, 7, 6, b, dark=d, light="A" if near else None)
    capsule(s, el, wr, 6, 5, b, dark=d)
    # 노랑 줄(소매 바깥쪽 선)
    if near:
        for t in (0.15, 0.4, 0.65, 0.9):
            px = sh[0] + (el[0] - sh[0]) * t
            py = sh[1] + (el[1] - sh[1]) * t
            s.px(round(px) - 1, round(py), C["Y"])
        for t in (0.1, 0.35, 0.55):
            px = el[0] + (wr[0] - el[0]) * t
            py = el[1] + (wr[1] - el[1]) * t
            s.px(round(px) - 1, round(py), C["Y"])
    # 손(소매 끝 밴드 + 손)
    ux, uy = wr[0] - el[0], wr[1] - el[1]
    L = math.hypot(ux, uy) or 1
    ux, uy = ux / L, uy / L
    cuff = (wr[0] - ux * 2, wr[1] - uy * 2)
    capsule(s, cuff, wr, 6, 6, "o" if near else "K", round_ends=False)
    hand = (wr[0] + ux * 4, wr[1] + uy * 4)
    capsule(s, wr, hand, 5, 4, "S" if near else "s", dark="s" if near else None)
    outline(s)
    return s


def draw_torso(s, hip, lean=0.0, near=True):
    """크롭 재킷 + 허리 + 허리 가방. hip=엉덩이 중심. 어깨는 hip에서 위로 TORSO."""
    sh = (hip[0] + lean, hip[1] - TORSO)
    new_layer(s)
    # 바지 허리~골반
    s.polygon([rnd((hip[0] - 7, hip[1] - 6)), rnd((hip[0] + 7, hip[1] - 6)), rnd((hip[0] + 8, hip[1] + 5)),
               rnd((hip[0] - 8, hip[1] + 5))], C["x"])
    s.rect(round(hip[0] - 8), round(hip[1] + 2), round(hip[0] + 8), round(hip[1] + 5), C["z"], only=C["x"])
    # 배(드러난 허리)
    s.rect(round(hip[0] - 6), round(hip[1] - 9), round(hip[0] + 7), round(hip[1] - 6), C["S"])
    # 재킷
    j = [(sh[0] - 8, sh[1] + 1), (sh[0] - 4, sh[1] - 2), (sh[0] + 5, sh[1] - 2), (sh[0] + 10, sh[1] + 3),
         (hip[0] + 11, hip[1] - 9), (hip[0] - 9, hip[1] - 9)]
    s.polygon([rnd(p) for p in j], C["O"])
    s.polygon([rnd((sh[0] + 3, sh[1] + 1)), rnd((sh[0] + 8, sh[1] + 2)), rnd((hip[0] + 9, hip[1] - 9)),
               rnd((hip[0] + 5, hip[1] - 9))], C["o"], only=C["O"])     # 오른쪽(어두운 면)
    s.polygon([rnd((sh[0] - 7, sh[1] + 1)), rnd((sh[0] - 4, sh[1] - 2)), rnd((sh[0] - 1, sh[1] - 2)),
               rnd((sh[0] - 3, sh[1] + 4)), rnd((sh[0] - 6, sh[1] + 6))], C["A"], only=C["O"])   # 밝은 면
    # 목 세운 깃
    s.rect(round(sh[0]) - 3, round(sh[1]) - 4, round(sh[0]) + 3, round(sh[1]) - 1, C["O"])
    s.rect(round(sh[0]) - 3, round(sh[1]) - 4, round(sh[0]) - 1, round(sh[1]) - 3, C["A"])
    s.rect(round(sh[0]) + 2, round(sh[1]) - 2, round(sh[0]) + 3, round(sh[1]) - 1, C["o"])
    # 지퍼 줄 + 앞 노랑 줄
    zx = round(sh[0] + 5 + (hip[0] - sh[0] + 4) * 0.0)
    s.line(zx, round(sh[1]) + 1, round(hip[0] + 7), round(hip[1]) - 10, C["3"])
    s.line(round(sh[0]) - 5, round(sh[1]) + 3, round(sh[0]) - 5, round(hip[1]) - 11, C["Y"])
    # 재킷 밑단 밴드
    s.rect(round(hip[0] - 8), round(hip[1]) - 11, round(hip[0] + 9), round(hip[1]) - 9, C["o"])
    # 벨트 + 허리 가방(앞쪽)
    s.rect(round(hip[0] - 8), round(hip[1]) - 5, round(hip[0] + 8), round(hip[1]) - 4, C["0"])
    s.rect(round(hip[0] + 3), round(hip[1]) - 6, round(hip[0] + 9), round(hip[1]) - 1, C["1"])
    s.rect(round(hip[0] + 3), round(hip[1]) - 6, round(hip[0] + 9), round(hip[1]) - 6, C["3"])
    s.px(round(hip[0] + 7), round(hip[1]) - 4, C["3"])
    outline(s)
    return sh


def walk_pose(p):
    """걷기 한 주기(p=0..2π). 반환 (가까운 다리, 먼 다리) 각도와 팔."""
    def leg(q):
        a = 20 * math.cos(q)
        sw = max(0.0, -math.sin(q))              # 앞으로 내딛는 구간(스윙)
        k = 6 + 44 * sw
        return a, k
    n = leg(p)
    f = leg(p + math.pi)
    return n, f


def figure(an, kn, af, kf, arm_n, arm_f, lean=1.5, head=("smile", False), head_dy=0, rise=0, dx=0,
           sink=None, foot_fa=None, cx=58, arms_back=False, size=(UNIT, UNIT), f=1.0):
    """한 프레임. arm_* = (위팔 각도, 팔꿈치 접힘). sink=None 이면 가장 낮은 발바닥이 GROUND 에 닿게 높이를 맞춘다."""
    def foot(a, k, fa=None):
        _, ank = limb_leg(None, (0, 0), a, k, 0)
        shin = a - k
        if fa is None:
            fa = max(-16, min(22, -shin * 0.7))
        low = ank[1] + 1 + max(rot(q, fa)[1] for q in [(-4, 6), (10, 6)])
        return fa, low
    fan, lown = foot(an, kn, foot_fa[0] if foot_fa else None)
    faf, lowf = foot(af, kf, foot_fa[1] if foot_fa else None)
    base_y = GROUND - max(lown, lowf)
    hip_y = base_y if sink is None else sink[0] + sink[1]
    hip = (cx + dx, round(hip_y))
    s = Sprite(*size) if f == 1.0 else Sc(size[0], size[1], f)
    kf_ = limb_leg(s, hip, af, kf, faf)
    kn_ = limb_leg(s, hip, an, kn, fan)
    thip = (hip[0], hip[1] - rise)
    shd = (thip[0] + lean, thip[1] - TORSO + 2)
    sf = (shd[0] + 1, shd[1] + 1)
    el, wr, wa = arm_pts(sf, *arm_f)
    draw_arm(s, sf, el, wr, wa, False)
    draw_leg(s, (hip[0] - 1, hip[1]), kf_[0], kf_[1], faf, False)
    sh = draw_torso(s, thip, lean=lean)
    draw_leg(s, (hip[0] + 1, hip[1]), kn_[0], kn_[1], fan, True)
    if arms_back:          # 팔을 머리 뒤에 깐다(떨어짐: 팔을 들어도 얼굴이 가려지지 않게)
        el, wr, wa = arm_pts(shd, *arm_n)
        draw_arm(s, shd, el, wr, wa, True)
    new_layer(s, "head")
    _paste(s, head_img(head, f), round(sh[0] * f) - round(11 * f) if f != 1.0 else round(sh[0]) - 11, round(sh[1] * f) - round(22 * f) + head_dy if f != 1.0 else round(sh[1]) - 22 + head_dy, raw=f != 1.0)
    if not arms_back:
        el, wr, wa = arm_pts(shd, *arm_n)
        draw_arm(s, shd, el, wr, wa, True)
    return s


def walk_arms(p):
    sw = 24 * math.cos(p)
    return (-sw, 8 + 16 * max(0, -math.cos(p))), (sw, 8 + 16 * max(0, math.cos(p)))


def frame_walk(i):
    p = 2 * math.pi * i / N_FRAMES["walk"]
    (an, kn), (af, kf) = walk_pose(p)
    arm_n, arm_f = walk_arms(p)
    hd = 1 if (math.cos(2 * p) < -0.5) else 0       # 발이 닿을 때 머리가 살짝 내려감
    return figure(an, kn, af, kf, arm_n, arm_f, head_dy=hd)


def frame_wait(i):
    t = i / N_FRAMES["wait"]
    br = 0.5 - 0.5 * math.cos(2 * math.pi * t)       # 숨쉬기 0..1
    sway = math.sin(2 * math.pi * t)
    blink = i in (15, 16)
    return figure(5, 5, -5, 6, (-4 + 3 * sway, 6 + 2 * br), (4 - 3 * sway, 8),
                  head=("smile", blink), head_dy=0 if br < 0.5 else -1, rise=round(br * 1.4), dx=round(sway * 0.7), lean=1)


def frame_fall(i):
    n = N_FRAMES["fall"]
    sink = round(30 * (i / (n - 1)) ** 1.5)
    ph = i * 1.25
    an, kn = 16 * math.sin(ph) + 6, 28 + 12 * math.sin(ph + 1)
    af, kf = -16 * math.sin(ph) - 4, 24 + 12 * math.cos(ph)
    arm_n = (205 + 8 * math.sin(ph), 12)
    arm_f = (150 + 8 * math.cos(ph), -10)
    stand = _stand_hip()
    return figure(an, kn, af, kf, arm_n, arm_f, lean=-1, head=("open", False), sink=(stand, sink), foot_fa=(24, 24), arms_back=True,
                  head_dy=-1 if i % 2 else 0)


_STAND = []


def _stand_hip():
    if not _STAND:
        # 서 있는 자세에서 엉덩이 y 를 구한다(figure 와 같은 계산)
        def low(a, k):
            _, ank = limb_leg(None, (0, 0), a, k, 0)
            fa = max(-16, min(22, -(a - k) * 0.7))
            return ank[1] + 1 + max(rot(q, fa)[1] for q in [(-4, 6), (10, 6)])
        _STAND.append(round(GROUND - max(low(5, 5), low(-5, 5))))
    return _STAND[0]


def _paste(s, im, x, y, raw=False):
    if raw:      # 배율 그림: 이미 목표 해상도로 그린 머리를 1:1 로 얹는다
        px = im.load()
        for yy in range(im.height):
            for xx in range(im.width):
                c = px[xx, yy]
                if c[3]:
                    s.s.px(x + xx, y + yy, "#%02x%02x%02x" % c[:3])
        return
    px = im.load()
    for yy in range(im.height):
        for xx in range(im.width):
            c = px[xx, yy]
            if c[3]:
                s.px(x + xx, y + yy, "#%02x%02x%02x" % c[:3])


def build():
    made = []
    spec = (("walk", frame_walk), ("wait", frame_wait), ("fall", frame_fall))
    sheets = {}
    for phase, fn in spec:
        frames = [fn(i).composite() for i in range(N_FRAMES[phase])]
        for f in frames:
            assert not palette_check(f), phase
            assert all(a in (0, 255) for a in f.getchannel("A").getdata()), phase
        sheets[phase] = frames
        made += sheet(frames, f"anim/runner128_v3_{phase}", fps=30, loop=(phase != "fall"), status=STATUS)
        made += sheet([f.transpose(Image.Transpose.FLIP_LEFT_RIGHT) for f in frames], f"anim/runner128_v3_{phase}_left",
                      fps=30, loop=(phase != "fall"), status=STATUS)
    pick = [sheets["walk"][i] for i in (0, 2, 4, 6, 8, 10, 12, 14)] + [sheets["wait"][i] for i in (0, 12, 15)] +            [sheets["fall"][i] for i in (0, 4, 9)]
    board = Image.new("RGBA", (UNIT * 8, UNIT * 2), (60, 70, 90, 255))
    for k, f in enumerate(pick):
        board.alpha_composite(f, ((k % 8) * UNIT, (k // 8) * UNIT))
    board.resize((board.width * 2, board.height * 2), Image.NEAREST).save("Art/Export/preview/w042_runner_board.png")
    return made
