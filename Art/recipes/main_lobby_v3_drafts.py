"""메인 로비 구도 시안 v3 (W-027) — 1점 원근을 먼저 정하고 모든 물체를 그 함수로 계산한다.

- 640×360, 공유 팔레트(PIX)만, 안티앨리어싱 없음. 정지 시안(manifest는 바꾸지 않음, preview 폴더에만 저장).
- 카메라: 모니터 뒷면 바로 뒤. 수평선 y=150, 소실점 1개. 세계 좌표(cm) → 화면 좌표는 Cam.p() 한 곳에서만 계산한다.
  x = vx + f·X/Z,  y = hy + f·(Ch − Y)/Z   (X 오른쪽, Y 위, Z 카메라에서 멀어지는 방향)
- 방 상자·책상·침대·의자·인물 크기는 모두 Cam.p()/Cam.s()로 만든다(손으로 꼭짓점을 찍지 않음).
- 후퇴선(깊이 방향 모서리)은 REG에 기록해 확인판에서 소실점으로 모이는지 보여 주고, 코드로도 검사한다.
- 인물은 LD(7.5등신, 머리 높이 22cm) 러프. 외형은 후속 작업.
[시안] 사용자 선택 전.
"""
import math
import random

from PIL import Image, ImageDraw

from artkit import EXPORT, PIX, canvas, draw_ellipse, draw_poly, outline_outside, palette_check, pfill, pset, upscale
import main_lobby_drafts as dr

W, H = 640, 360
K = PIX["K"]


# =====================================================================
# 원근 함수
# =====================================================================
class Cam:
    def __init__(self, vx, hy=150, f=240.0, ch=130.0):
        self.vx, self.hy, self.f, self.ch = vx, hy, f, ch

    def p(self, X, Y, Z):
        return (self.vx + self.f * X / Z, self.hy + self.f * (self.ch - Y) / Z)

    def s(self, Z):
        """Z 깊이에서 1cm가 화면에서 차지하는 px."""
        return self.f / Z

    def ip(self, X, Y, Z):
        x, y = self.p(X, Y, Z)
        return (int(round(x)), int(round(y)))

    def floor_y(self, Z):
        return self.hy + self.f * self.ch / Z


mons = []       # 모니터 뒷면 사각형(화면 좌표)
REG = []        # 후퇴선(가까운 점, 먼 점) 기록
FOOT = []       # 바닥 사각형 기록(Y=0, X0,X1,Z0,Z1)
LABEL = {}


def reg(cam, a, b, name=""):
    REG.append((cam.p(*a), cam.p(*b), name))


def poly3(im, cam, pts3, ch, outline=None):
    pts = [cam.ip(*p) for p in pts3]
    draw_poly(im, pts, ch)
    if outline:
        ImageDraw.Draw(im).line(pts + [pts[0]], fill=PIX[outline], width=1)


def box(im, cam, X0, X1, Y0, Y1, Z0, Z1, front, top=None, side=None, ol="K", name=""):
    """직육면체. 카메라가 보는 면(앞·위·소실점 쪽 옆면)만 그린다. 후퇴선을 기록한다."""
    top = top or front
    side = side or front
    if Y1 < cam.ch:
        poly3(im, cam, [(X0, Y1, Z0), (X1, Y1, Z0), (X1, Y1, Z1), (X0, Y1, Z1)], top, ol)
    if X0 > 0:
        poly3(im, cam, [(X0, Y0, Z0), (X0, Y1, Z0), (X0, Y1, Z1), (X0, Y0, Z1)], side, ol)
    if X1 < 0:
        poly3(im, cam, [(X1, Y0, Z0), (X1, Y1, Z0), (X1, Y1, Z1), (X1, Y0, Z1)], side, ol)
    poly3(im, cam, [(X0, Y0, Z0), (X1, Y0, Z0), (X1, Y1, Z0), (X0, Y1, Z0)], front, ol)
    for X in (X0, X1):
        for Y in (Y0, Y1):
            reg(cam, (X, Y, Z0), (X, Y, Z1), name)
    if Y0 == 0:
        FOOT.append((X0, X1, Z0, Z1, name))


# =====================================================================
# 방(뒷벽·옆벽·바닥·천장) — 상자 크기는 Cam.p()에서 나온다
# =====================================================================
ZB = 390.0      # 뒷벽 깊이 (바닥 뒷선 y=230 → f·Ch/(230−150))
HC = 260.0      # 천장 높이 (뒷벽 윗변 y=70)
ZN = 40.0       # 그리는 가장 가까운 깊이(화면 밖까지 충분히 나가도록)


def room(im, cam, xl, xr):
    pf = lambda X, Y, Z: cam.ip(X, Y, Z)
    # 바닥 전체 → 천장 → 옆벽 → 뒷벽 순으로 덮는다.
    pfill(im, 0, 0, W - 1, H - 1, "0")
    poly3(im, cam, [(xl, 0, ZB), (xr, 0, ZB), (xr, 0, ZN), (xl, 0, ZN)], "0")
    # 바닥 널(세로선 = 소실점으로 모임) — 50cm 폭, 한 줄 걸러 밝게
    n0 = int(math.floor(xl / 50.0))
    for k in range(n0, int(xr / 50.0) + 1):
        a, b = max(xl, k * 50), min(xr, k * 50 + 50)
        if b > a and k % 2 == 0:
            poly3(im, cam, [(a, 0, ZB), (b, 0, ZB), (b, 0, ZN), (a, 0, ZN)], "1")
    for k in range(n0, int(xr / 50.0) + 2):
        X = k * 50
        if xl <= X <= xr:
            reg(cam, (X, 0, ZN), (X, 0, ZB), "floor-seam")
            ImageDraw.Draw(im).line([cam.ip(X, 0, ZN), cam.ip(X, 0, ZB)], fill=K, width=1)
    for Z in (90, 140, 200, 270):
        ImageDraw.Draw(im).line([cam.ip(xl, 0, Z), cam.ip(xr, 0, Z)], fill=PIX["K"], width=1)
    # 천장
    poly3(im, cam, [(xl, HC, ZB), (xr, HC, ZB), (xr, HC, ZN), (xl, HC, ZN)], "k")
    # 옆벽(어둡게) + 걸레받이·허리 몰딩(후퇴선)
    for X, sgn in ((xl, -1), (xr, 1)):
        poly3(im, cam, [(X, 0, ZB), (X, HC, ZB), (X, HC, ZN), (X, 0, ZN)], "0")
        for Y, ch in ((9, "K"), (86, "1"), (89, "K"), (HC - 6, "K")):
            ImageDraw.Draw(im).line([cam.ip(X, Y, ZN), cam.ip(X, Y, ZB)], fill=PIX[ch], width=1)
            reg(cam, (X, Y, ZN), (X, Y, ZB), "wall-line")
        for Z in range(100, int(ZB), 70):
            ImageDraw.Draw(im).line([cam.ip(X, 0, Z), cam.ip(X, HC, Z)], fill=PIX["K"], width=1)
    # 뒷벽(화면과 평행한 직사각형)
    poly3(im, cam, [(xl, 0, ZB), (xr, 0, ZB), (xr, HC, ZB), (xl, HC, ZB)], "4")
    poly3(im, cam, [(xl, 0, ZB), (xr, 0, ZB), (xr, 86, ZB), (xl, 86, ZB)], "1")      # 아래 판넬
    ImageDraw.Draw(im).line([cam.ip(xl, 86, ZB), cam.ip(xr, 86, ZB)], fill=PIX["2"], width=1)
    ImageDraw.Draw(im).line([cam.ip(xl, 89, ZB), cam.ip(xr, 89, ZB)], fill=K, width=1)
    ImageDraw.Draw(im).line([cam.ip(xl, 9, ZB), cam.ip(xr, 9, ZB)], fill=K, width=1)
    # 방 상자 모서리(후퇴선) 기록
    for X in (xl, xr):
        for Y in (0, HC):
            reg(cam, (X, Y, ZN), (X, Y, ZB), "room-edge")
    # 천장 조명 띠(소실점으로 모이는 선)
    for X in (-60, 60) if xl < -100 and xr > 100 else (xl * 0.3 + xr * 0.7,):
        ImageDraw.Draw(im).line([cam.ip(X, HC - 1, 120), cam.ip(X, HC - 1, ZB - 20)], fill=PIX["2"], width=1)
        reg(cam, (X, HC - 1, 120), (X, HC - 1, ZB - 20), "ceiling-strip")


def back_rect(cam, xl, xr):
    return (cam.ip(xl, HC, ZB), cam.ip(xr, 0, ZB))


# =====================================================================
# 창 / 소품
# =====================================================================
def window(im, cam, X0, X1, Y0, Y1, seed):
    """뒷벽의 창. 유리 영역(본그림에서 투명)을 반환한다."""
    x0, y0 = cam.ip(X0, Y1, ZB)
    x1, y1 = cam.ip(X1, Y0, ZB)
    # 틀 두께 4cm
    fr = 4
    fx0, fy0 = cam.ip(X0 - fr, Y1 + fr, ZB)
    fx1, fy1 = cam.ip(X1 + fr, Y0 - fr, ZB)
    pfill(im, fx0, fy0, fx1, fy1, "3")
    ImageDraw.Draw(im).rectangle((fx0, fy0, fx1, fy1), outline=K)
    glass = (x0, y0, x1, y1)
    dr.city_view(im, glass, seed=seed, horizon=0.9)
    # 창살(세로 1·가로 1)
    mx = (x0 + x1) // 2
    my = (y0 + y1) // 2
    pfill(im, mx - 1, y0, mx, y1, "3")
    pfill(im, x0, my - 1, x1, my, "3")
    ImageDraw.Draw(im).rectangle(glass, outline=K)
    # 창턱(벽에서 튀어나온 상자, 위에서 보임)
    sill = (fx0 - 3, fy1, fx1 + 3, fy1 + 4)
    pfill(im, *sill, "2")
    pfill(im, sill[0], sill[3], sill[2], sill[3], "K")
    pfill(im, sill[0], sill[1], sill[2], sill[1], "3")
    return glass


def shelf(im, cam, X0, X1, Y, seed):
    rng = random.Random(seed)
    # 선반 판: 뒷벽에 붙은 얇은 상자(위가 보임)
    box(im, cam, X0, X1, Y, Y + 3, ZB - 22, ZB, "d", top="D", ol="K", name="shelf")
    X = X0 + 3
    while X < X1 - 6:
        w = rng.randint(3, 6)
        h = rng.randint(14, 24)
        col = rng.choice(["R", "b", "Y", "g", "3", "C"])
        x0, yb = cam.ip(X, Y + 3, ZB - 8)
        x1, yt = cam.ip(X + w, Y + 3 + h, ZB - 8)
        pfill(im, x0, yt, max(x0, x1 - 1), yb, col)
        pfill(im, x0, yt, x0, yb, "K")
        X += w + 1


def poster(im, cam, X0, X1, Y0, Y1):
    x0, y0 = cam.ip(X0, Y1, ZB)
    x1, y1 = cam.ip(X1, Y0, ZB)
    pfill(im, x0, y0, x1, y1, "B")
    ImageDraw.Draw(im).rectangle((x0, y0, x1, y1), outline=K)
    pfill(im, x0 + 1, y0 + 1, x1 - 1, y0 + 1, "C")
    cx = (x0 + x1) // 2
    for i in range(3):
        pfill(im, cx - 3 + i, y0 + 4 + i * 3, cx + 3 - i, y0 + 4 + i * 3, "b")
    ImageDraw.Draw(im).line([(x0 + 3, y1 - 4), (x1 - 3, y1 - 4)], fill=PIX["C"], width=1)


# =====================================================================
# 침대 (벽에 붙인 상자: 옆면 두께·매트리스 두께·베개·이불)
# =====================================================================
SEAT_Y = 46.0   # 매트리스 윗면 높이


def bed(im, cam, X0, X1, Z0, Z1, head="back", pillow_at=None):
    # 프레임(다리 포함) → 매트리스 → 베개·이불
    box(im, cam, X0, X1, 0, 24, Z0, Z1, "d", top="d", side="d", name="bed-frame")
    box(im, cam, X0 - 1, X1 + 1, 24, 30, Z0 - 1, Z1, "D", top="D", side="D", name="bed-base")
    box(im, cam, X0, X1, 30, SEAT_Y, Z0, Z1, "2", top="b", side="2", name="bed-mattress")
    # 이불: 매트리스 앞쪽 2/3를 덮는 얇은 판
    zc = Z0 + (Z1 - Z0) * 0.62
    box(im, cam, X0 - 0.5, X1 + 0.5, 30, SEAT_Y + 3, Z0 - 0.5, zc, "B", top="b", side="B", name="blanket")
    # 베개(머리 쪽)
    if pillow_at:
        px0, px1, pz0, pz1 = pillow_at
        box(im, cam, px0, px1, SEAT_Y, SEAT_Y + 7, pz0, pz1, "3", top="W", side="3", name="pillow")
    # 머리판(뒷벽에 붙은 판)
    box(im, cam, X0 - 1, X1 + 1, 24, 78, ZB - 5, ZB, "d", top="D", name="headboard")


# =====================================================================
# 인물(LD 러프) — 관절을 세계 좌표(cm)로 두고 Cam.p()로 투영
# =====================================================================
HEAD_H, HEAD_W = 22.0, 15.0


def limb(L, cam, a, b, wa, wb, ch):
    xa, ya = cam.p(*a)
    xb, yb = cam.p(*b)
    ra, rb = wa * cam.s(a[2]) / 2, wb * cam.s(b[2]) / 2
    dx, dy = xb - xa, yb - ya
    n = math.hypot(dx, dy)
    if n > 0.5:
        nx, ny = -dy / n, dx / n
        pts = [(xa + nx * ra, ya + ny * ra), (xb + nx * rb, yb + ny * rb),
               (xb - nx * rb, yb - ny * rb), (xa - nx * ra, ya - ny * ra)]
        draw_poly(L, [(int(round(x)), int(round(y))) for x, y in pts], ch)
    for (x, y, r) in ((xa, ya, ra), (xb, yb, rb)):
        draw_ellipse(L, (int(round(x - r)), int(round(y - r)), int(round(x + r)), int(round(y + r))), ch)


def head(L, cam, cx, y_chin, Z, hair, hair_dk, hair_hi, phones=False):
    s = cam.s(Z)
    x0, ytop = cam.p(cx - HEAD_W / 2, y_chin + HEAD_H, Z)
    x1, ychin = cam.p(cx + HEAD_W / 2, y_chin, Z)
    # 목
    limb(L, cam, (cx, y_chin + 4, Z), (cx, y_chin - 6, Z), 6.5, 7.5, "s")
    draw_ellipse(L, (int(round(x0)), int(round(ytop)), int(round(x1)), int(round(ychin))), "l")
    h = ychin - ytop
    # 머리카락: 윗부분 + 앞머리 + 옆머리
    draw_ellipse(L, (int(round(x0 - 1)), int(round(ytop - 1)), int(round(x1 + 1)), int(round(ytop + h * 0.62))), hair)
    pfill(L, int(round(x0 - 1)), int(round(ytop + h * 0.30)), int(round(x0 + 1)), int(round(ytop + h * 0.78)), hair)
    pfill(L, int(round(x1 - 1)), int(round(ytop + h * 0.30)), int(round(x1 + 1)), int(round(ytop + h * 0.78)), hair)
    fy = int(round(ytop + h * 0.34))
    xs = int(round(x0 + 1)); xe = int(round(x1 - 1))
    for i, x in enumerate(range(xs, xe + 1)):
        dy = 0 if (i // 2) % 2 == 0 else 1
        pfill(L, x, fy, x, fy + dy, hair)
    pfill(L, xs, int(round(ytop + 1)), xe, int(round(ytop + 1)), hair_hi)
    for x in range(xs + 1, xe, 3):
        pfill(L, x, fy + 1, x, fy + 1, hair_dk)
    # 눈·입
    ey = int(round(ytop + h * 0.58))
    ex = int(round(3.4 * s))
    cxs = int(round((x0 + x1) / 2))
    eh = 1 if s < 1.1 else 2
    for sx in (-1, 1):
        pfill(L, cxs + sx * ex - (1 if sx < 0 else 0), ey, cxs + sx * ex, ey + eh - 1, "K")
    if s >= 1.1:
        pset(L, cxs, int(round(ytop + h * 0.80)), "m")
    if phones:
        pfill(L, int(round(x0 - 2)), int(round(ytop + h * 0.38)), int(round(x0 + 1)), int(round(ytop + h * 0.72)), "3")
        pfill(L, int(round(x1 - 1)), int(round(ytop + h * 0.38)), int(round(x1 + 2)), int(round(ytop + h * 0.72)), "3")
        ImageDraw.Draw(L).arc((int(round(x0 - 1)), int(round(ytop - 3)), int(round(x1 + 1)), int(round(ytop + h * 0.70))), 180, 360, fill=K, width=1)


def torso(L, cam, cx, zs, zh, y_sh, y_hip, cloth, cloth_dk):
    """어깨(폭 40cm, Z=zs)·엉덩이(폭 34cm, Z=zh) 사다리꼴 + 가운데 접힘선."""
    pts = [(cx - 20, y_sh, zs), (cx + 20, y_sh, zs), (cx + 17, y_hip, zh), (cx - 17, y_hip, zh)]
    poly3(L, cam, pts, cloth)
    a = cam.ip(cx + 6, y_sh - 4, zs)
    b = cam.ip(cx + 8, y_hip + 2, zh)
    ImageDraw.Draw(L).line([a, b], fill=PIX[cloth_dk], width=1)
    poly3(L, cam, [(cx + 8, y_sh, zs), (cx + 20, y_sh, zs), (cx + 17, y_hip, zh), (cx + 9, y_hip, zh)], cloth_dk)


def protagonist(im, cam, px, zc):
    """의자에 앉아 정면(관객 쪽)을 보는 주인공. 모니터 뒤라서 머리·어깨·가슴 위만 보인다."""
    L = canvas(W, H)
    zs, zh = zc - 8, zc
    # 의자 등받이(어깨 뒤)
    box(L, cam, px - 24, px + 24, 60, 119, zc + 8, zc + 12, "1", top="3", side="1", name="chair-back")
    box(L, cam, px - 3, px + 3, 0, 60, zc + 8, zc + 12, "0", top="0", name="chair-post")
    outline_outside(L)
    im.alpha_composite(L)
    L = canvas(W, H)
    # 팔 → 몸통 → 머리 (손은 키보드 쪽)
    for sx in (-1, 1):
        limb(L, cam, (px + sx * 21, 96, zs), (px + sx * 25, 72, zs - 8), 9, 8, "t")
        limb(L, cam, (px + sx * 25, 72, zs - 8), (px + sx * 12, 80, zs - 30), 7.5, 6.5, "t")
        draw_ellipse(L, tuple(int(round(v)) for v in (
            cam.p(px + sx * 12 - 3, 83, zs - 30)[0], cam.p(px + sx * 12, 83, zs - 30)[1],
            cam.p(px + sx * 12 + 3, 78, zs - 30)[0], cam.p(px + sx * 12, 78, zs - 30)[1])), "l")
    torso(L, cam, px, zs, zh, 98, 48, "T", "t")
    head(L, cam, px, 104, zs - 2, "H", "J", "U", phones=True)
    # 후드 카라
    c0 = cam.ip(px - 9, 100, zs - 3)
    c1 = cam.ip(px + 9, 94, zs - 3)
    draw_ellipse(L, (c0[0], c0[1], c1[0], c1[1]), "t")
    outline_outside(L)
    im.alpha_composite(L)
    return {"head_top_y": cam.p(px, 104 + HEAD_H, zs - 2)[1], "head_chin_y": cam.p(px, 104, zs - 2)[1],
            "head_px": HEAD_H * cam.s(zs - 2), "chair_floor_y": cam.floor_y(zc + 10)}


def helper(im, cam, hx, hz, mode):
    """침대 가장자리에 걸터앉은 조력자. 엉덩이 = 매트리스 윗면(SEAT_Y), 발 = 바닥(Y=0).
    mode 'front': 다리가 카메라 쪽(−Z)으로, 'side': 다리가 오른쪽(+X)으로 내려옴."""
    L = canvas(W, H)
    hip_y = SEAT_Y + 3
    zs = hz + 2
    if mode == "front":
        knees = [(hx + sx * 11, 54, hz - 36) for sx in (-1, 1)]
        feet = [(hx + sx * 11, 4, hz - 40) for sx in (-1, 1)]
        toe = [(hx + sx * 11, 4, hz - 49) for sx in (-1, 1)]
        order = (-1, 1)
    else:
        knees = [(hx + 36, 54, hz + sz) for sz in (-6, 6)]
        feet = [(hx + 40, 4, hz + sz) for sz in (-6, 6)]
        toe = [(hx + 49, 4, hz + sz) for sz in (-6, 6)]
        order = (0, 1)
    # 몸통 → 다리 → 팔 → 머리
    torso(L, cam, hx, zs, hz, 98, hip_y, "D", "d")
    for i in range(2):
        thigh_w = 14
        limb(L, cam, (hx + (-1 if i == 0 else 1) * 8, hip_y, hz), knees[i], thigh_w, 11, "1")
        limb(L, cam, knees[i], feet[i], 10.5, 8, "1")
        limb(L, cam, feet[i], toe[i], 8, 8, "2")
        gx, gy = cam.p(*toe[i])
        gy = cam.p(toe[i][0], 0, toe[i][2])[1]
        r = int(round(4.5 * cam.s(toe[i][2])))
        pfill(L, int(round(gx)) - r, int(round(gy)) - 1, int(round(gx)) + r, int(round(gy)), "W")
        pfill(L, int(round(gx)) - r + 1, int(round(gy)) - 3, int(round(gx)) + r - 1, int(round(gy)) - 2, "3")
    for sx in (-1, 1):
        elb = (hx + sx * 24, 74, hz - 4)
        hand = (hx + sx * 18, 58, hz - 20) if mode == "front" else (hx + sx * 16, 55, hz - 14)
        limb(L, cam, (hx + sx * 20, 96, zs), elb, 9, 8, "D")
        limb(L, cam, elb, hand, 7.5, 6.5, "D")
        hx_, hy_ = cam.p(*hand)
        r = 3.2 * cam.s(hand[2])
        draw_ellipse(L, (int(round(hx_ - r)), int(round(hy_ - r)), int(round(hx_ + r)), int(round(hy_ + r))), "l")
    head(L, cam, hx, 104, zs, "Y", "y", "h")
    outline_outside(L)
    im.alpha_composite(L)
    foot_y = cam.p(*feet[0])[1]
    ground_y = cam.floor_y(feet[0][2])
    return {"hip_y": cam.p(hx, hip_y, hz)[1], "seat_top_y": cam.p(hx, SEAT_Y, hz)[1],
            "foot_y": cam.p(*toe[0])[1], "ground_y": cam.floor_y(toe[0][2]),
            "head_top_y": cam.p(hx, 104 + HEAD_H, zs)[1], "head_px": HEAD_H * cam.s(zs), "floor_Z": toe[0][2]}


# =====================================================================
# 책상·모니터·키보드
# =====================================================================
DESK_Y, DESK_T = 76.0, 3.0
DESK_Z0, DESK_Z1 = 52.0, 150.0      # 책상은 모니터 바로 뒤에서 주인공 쪽으로 이어진다
MON_Z = 60.0                        # 모니터 뒷면 깊이(카메라 바로 앞)
MON_T = 4.0
MON_Y0, MON_Y1 = 78.0, 114.0


def monitor(im, cam, xc, w, led=True, name="monitor"):
    """카메라 바로 앞의 모니터 뒷면: 직사각형 실루엣. 스탠드 목·케이블이 보인다."""
    X0, X1 = xc - w / 2, xc + w / 2
    Z0 = MON_Z
    box(im, cam, xc - 12, xc + 12, DESK_Y, DESK_Y + 1.5, Z0 - 6, Z0 + 8, "3", top="3", name=name + "-foot")
    box(im, cam, X0, X1, MON_Y0, MON_Y1, Z0, Z0 + MON_T, "1", top="3", side="0", name=name + "-panel")
    box(im, cam, xc - 2.5, xc + 2.5, DESK_Y + 1.5, MON_Y0 + 14, Z0 - 3, Z0, "0", top="3", name=name + "-neck")
    x0, y0 = cam.ip(X0 + 4, MON_Y1 - 4, Z0)
    x1, y1 = cam.ip(X1 - 4, MON_Y0 + 4, Z0)
    for y in range(y0 + 3, y0 + 3 + 12 * 4, 4):
        pfill(im, x0, y, x0 + max(4, (x1 - x0) // 3), y, "4")
    lx, ly = cam.ip(xc, MON_Y1 - 8, Z0)
    pfill(im, lx - 3, ly - 1, lx + 3, ly + 1, "3")
    if led:
        ex, ey = cam.ip(X1 - 5, MON_Y0 + 3, Z0)
        pfill(im, ex - 1, ey - 1, ex, ey, "G")
    # 케이블: 목에서 아래로(화면 밖까지)
    a = cam.ip(xc, MON_Y0 + 8, Z0 - 4)
    pts = [a, (a[0] + 3, a[1] + 14), (a[0] - 2, a[1] + 30), (a[0] + 4, a[1] + 50)]
    ImageDraw.Draw(im).line(pts, fill=K, width=2)
    return (cam.ip(X0, MON_Y1, Z0), cam.ip(X1, MON_Y0, Z0))


def keyboard(im, cam, xc, w=40.0):
    z0 = 128.0
    box(im, cam, xc - w / 2, xc + w / 2, DESK_Y, DESK_Y + 1.5, z0, z0 + 12, "2", top="3", name="keyboard")
    for i in range(4):
        x0, y = cam.ip(xc - w / 2 + 2, DESK_Y + 1.5, z0 + 1.5 + i * 2.6)
        x1, _ = cam.ip(xc + w / 2 - 2, DESK_Y + 1.5, z0 + 1.5 + i * 2.6)
        ImageDraw.Draw(im).line([(x0, y), (x1, y)], fill=K, width=1)


def desk(im, cam, X0, X1):
    """상판(모니터 뒤로 주인공까지 이어지는 띠). 카메라 쪽 면은 서랍 없는 뒷판."""
    box(im, cam, X0, X1, DESK_Y - DESK_T, DESK_Y, DESK_Z0, DESK_Z1, "d", top="2", name="desk-top")
    poly3(im, cam, [(X0, DESK_Y - DESK_T, DESK_Z0), (X1, DESK_Y - DESK_T, DESK_Z0), (X1, 0, DESK_Z0), (X0, 0, DESK_Z0)], "0", "K")


# =====================================================================
# 시안 두 개
# =====================================================================
def make_a():
    REG.clear(); FOOT.clear()
    cam = Cam(320)
    mons.clear()
    xl, xr = -309, 309
    im = canvas(W, H)
    room(im, cam, xl, xr)
    glass = window(im, cam, -185, -25, 108, 198, seed=11)
    poster(im, cam, 100, 140, 120, 175)
    shelf(im, cam, -300, -245, 150, seed=5)
    # 침대: 뒷벽 오른쪽, 긴 변이 벽과 평행 (X 100~275, Z 258~348)
    bed(im, cam, 105, 303, 300, ZB - 5, pillow_at=(255, 295, 340, 380))
    # 창 빛(차가운 디더) — 인물 그리기 전에 방에만
    dr.relight(im, dr.ell(cam.ip(-125, 150, ZB)[0], cam.ip(-125, 150, ZB)[1] + 30, 110, 80), dr.COOL1,
               box=(0, 0, W - 1, H - 1))
    hinfo = helper(im, cam, 262, 318, "front")
    pinfo = protagonist(im, cam, 0, 170)
    desk(im, cam, -55, 55)
    keyboard(im, cam, 0)
    mons.append(monitor(im, cam, -27.5, 45, name="monitor-L"))
    mons.append(monitor(im, cam, 27.5, 45, name="monitor-R"))
    return cam, im, glass, hinfo, pinfo, (xl, xr)


def make_b():
    REG.clear(); FOOT.clear()
    cam = Cam(400)
    mons.clear()
    xl, xr = -426, 190
    im = canvas(W, H)
    room(im, cam, xl, xr)
    glass = window(im, cam, -10, 120, 108, 198, seed=23)
    shelf(im, cam, -350, -265, 150, seed=9)
    # 침대: 왼쪽 옆벽을 따라 깊이 방향으로 길게 (X −380~−290, Z 190~348)
    bed(im, cam, -424, -336, 230, ZB - 5, pillow_at=(-416, -346, 340, 380))
    dr.relight(im, dr.ell(cam.ip(55, 150, ZB)[0], cam.ip(55, 150, ZB)[1] + 30, 110, 80), dr.COOL1)
    hinfo = helper(im, cam, -354, 290, "side")
    pinfo = protagonist(im, cam, 78, 172)
    desk(im, cam, -32, 110)
    keyboard(im, cam, 78)
    mons.append(monitor(im, cam, 8, 80, led=True))
    return cam, im, glass, hinfo, pinfo, (xl, xr)


# =====================================================================
# 확인판·메뉴·검사
# =====================================================================
def line_dist(pt, a, b):
    (x, y), (x0, y0), (x1, y1) = pt, a, b
    d = math.hypot(x1 - x0, y1 - y0)
    if d < 1e-6:
        return 0.0
    return abs((x1 - x0) * (y0 - y) - (x0 - x) * (y1 - y0)) / d


def check_converge(cam):
    worst = 0.0
    for a, b, name in REG:
        worst = max(worst, line_dist((cam.vx, cam.hy), a, b))
    return worst


def guide_image(cam, im, extra_marks, xr_pair):
    k = 2
    g = upscale(im, k).convert("RGBA")
    d = ImageDraw.Draw(g)
    RED, CY, DCY, YEL = (255, 40, 40, 255), (0, 255, 255, 255), (0, 150, 170, 255), (255, 255, 0, 255)

    def sp(p):
        return (p[0] * k + k // 2, p[1] * k + k // 2)

    # 소실점으로 이어지는 연장선(점선)
    vp = (cam.vx, cam.hy)
    for a, b, name in REG:
        if name in ("floor-seam",):
            continue
        if name == "room-edge" or name.startswith(("monitor", "keyboard", "chair", "pillow", "blanket", "desk-top")):
            continue
        far = b
        n = max(2, int(math.hypot(vp[0] - far[0], vp[1] - far[1]) / 3))
        for i in range(0, n, 2):
            t0, t1 = i / n, min(1, (i + 1) / n)
            d.line([sp((far[0] + (vp[0] - far[0]) * t0, far[1] + (vp[1] - far[1]) * t0)),
                    sp((far[0] + (vp[0] - far[0]) * t1, far[1] + (vp[1] - far[1]) * t1))], fill=DCY, width=1)
    # 방 상자 모서리선(소실점에서 시작해 화면 밖으로)
    for a, b, name in REG:
        if name in ("room-edge", "wall-line", "ceiling-strip"):
            d.line([sp(a), sp(b)], fill=CY, width=1)
            ex = (b[0] + (b[0] - vp[0]) * -0.0, b[1])
    # 물체 후퇴선(청록)
    for a, b, name in REG:
        if name not in ("room-edge", "wall-line", "ceiling-strip", "floor-seam"):
            d.line([sp(a), sp(b)], fill=CY, width=1)
    # 뒷벽 사각형
    (bx0, by0), (bx1, by1) = extra_marks["back"]
    d.rectangle([sp((bx0, by0)), sp((bx1, by1))], outline=CY, width=1)
    # 바닥 사각형(발자국)
    for X0, X1, Z0, Z1, name in FOOT:
        q = [cam.p(X0, 0, Z0), cam.p(X1, 0, Z0), cam.p(X1, 0, Z1), cam.p(X0, 0, Z1)]
        d.line([sp(p) for p in q] + [sp(q[0])], fill=CY, width=1)
    # 인물 접지·엉덩이(노랑)
    for (y, x0, x1) in extra_marks["ymarks"]:
        d.line([(x0 * k, y * k), (x1 * k, y * k)], fill=YEL, width=1)
    # 수평선·소실점(빨강)
    d.line([(0, cam.hy * k + 1), (W * k, cam.hy * k + 1)], fill=RED, width=1)
    cx, cy = sp(vp)
    d.ellipse((cx - 4, cy - 4, cx + 4, cy + 4), fill=RED)
    return g


def build():
    out = EXPORT / "preview"
    out.mkdir(parents=True, exist_ok=True)
    made = []
    menu = dr.menu_overlay(0)
    boards = []
    for key, fn in (("A", make_a), ("B", make_b)):
        cam, scene, glass, hinfo, pinfo, (xl, xr) = fn()
        worst = check_converge(cam)
        assert worst < 1e-6, (key, worst)
        assert not palette_check(scene), key
        withmenu = dr.over(scene, menu)
        assert not palette_check(withmenu), key + " menu"
        back = back_rect(cam, xl, xr)
        marks = {"back": back, "ymarks": [
            (round(hinfo["seat_top_y"]), int(cam.p(xl, 0, 1)[0]), W),
            (round(hinfo["ground_y"]), 0, W),
            (round(pinfo["chair_floor_y"]), 0, W)]}
        guide = guide_image(cam, scene, marks, (xl, xr))
        for name, im in ((f"main_lobby_v3_{key}.png", scene), (f"main_lobby_v3_{key}_x3.png", upscale(scene, 3)),
                         (f"main_lobby_v3_{key}_menu_x3.png", upscale(withmenu, 3)),
                         (f"main_lobby_v3_{key}_guide.png", guide)):
            im.save(out / name)
            made.append(out / name)
        boards.append(withmenu)
        mx, ma = dr.zone_stats(scene, (20, 14, 180, 104))
        band_a = cam.p(0, DESK_Y, DESK_Z0)[1] - cam.p(0, DESK_Y, DESK_Z1)[1]
        s_h = cam.s(hinfo["floor_Z"])
        mt = min(m[0][1] for m in mons)
        print(f"      모니터 뒷면 {mons}, 윗변 y={mt} / 주인공 턱 y={pinfo['head_chin_y']:.1f} (얼굴 가림 {'0' if mt > pinfo['head_chin_y'] else '있음'})")
        print(f"      책상 상판 far edge y={cam.p(0, DESK_Y, DESK_Z1)[1]:.1f}, 책상 near edge y={cam.p(0, DESK_Y, DESK_Z0)[1]:.1f}")
        s_p = cam.s(170 - 10)
        print(f"  [{key}] 수평선 y={cam.hy}, 소실점 x={cam.vx}, 뒷벽 사각형 {back[0]}~{back[1]}, 소실점 수렴 최대 오차 {worst:.2e}px")
        print(f"      유리 좌표 {glass}, 메뉴 자리 최대 밝기 {mx:.0f}/평균 {ma:.0f}, 책상 상판 띠 두께 {band_a:.1f}px")
        print(f"      주인공 의자 바닥 접지 y={pinfo['chair_floor_y']:.1f}(Z=180), 머리 {pinfo['head_top_y']:.1f}~{pinfo['head_chin_y']:.1f} (높이 {pinfo['head_px']:.1f}px)")
        print(f"      조력자 엉덩이(매트리스 윗면) y={hinfo['seat_top_y']:.1f}, 발바닥 접지 y={hinfo['ground_y']:.1f}(Z={hinfo['floor_Z']}), "
              f"머리 {hinfo['head_top_y']:.1f}~ (높이 {hinfo['head_px']:.1f}px)")
        print(f"      깊이 배율(조력자/주인공) = (접지y-수평선) 비 = {(hinfo['ground_y']-cam.hy)/(pinfo['chair_floor_y']-cam.hy):.3f}, "
              f"머리 높이 비 = {hinfo['head_px']/pinfo['head_px']:.3f}")
    sheet = canvas(W, H * 2 + 4, PIX["K"])
    for i, b in enumerate(boards):
        sheet.alpha_composite(b, (0, i * (H + 4)))
    assert not palette_check(sheet)
    sheet.save(out / "main_lobby_v3_compare.png")
    made.append(out / "main_lobby_v3_compare.png")
    return made
