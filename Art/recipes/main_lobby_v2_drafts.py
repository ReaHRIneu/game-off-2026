"""메인 화면 새 구도 시안 3개 (W-026, D-109) — A 정면 대칭 / B 대각선 3/4 / C 낮은 가까운 시점.

- 640x360, 공유 팔레트만, 안티앨리어싱 없음. 정지 시안(manifest.json 을 바꾸지 않고 preview 에만 저장).
- 카메라는 컴퓨터 뒤쪽: 앞에 모니터 뒷면, 그 너머에 주인공(관객 쪽을 봄), 주인공 뒤 침대에 조력자.
- 인물은 LD 약 7.5등신 러프(실루엣 + 큰 색면). 외형 디자인은 다음 단계(W-027) — 색 계열만 구분
  (주인공 차가운 색, 조력자 따뜻한 색).
- 창 유리 좌표는 GLASS 에 기록(본그림에서 투명 처리). 메뉴 자리(x20~180, y14~104)는 어둡게 비운다.
[시안] 사용자 선택 전.
"""
import math
import random

from PIL import Image, ImageChops, ImageDraw

import main_lobby_drafts as dr
from artkit import EXPORT, PIX, canvas, draw_ellipse, draw_line, draw_poly, over, palette_check, pbox, pfill, pset, upscale

W, H = 640, 360
pmask, emask, mask_union, shade_mask = dr.pmask, dr.emask, dr.mask_union, dr.shade_mask
fill_mask, clip_draw, outline_mask, rim = dr.fill_mask, dr.clip_draw, dr.outline_mask, dr.rim
relight, dith, ell, city_view, rain, quad_pt = dr.relight, dr.dith, dr.ell, dr.city_view, dr.rain, dr.quad_pt
COOL1, COOL2, WARM1, WARM2 = dr.COOL1, dr.COOL2, dr.WARM1, dr.WARM2

MENU_ZONE = (20, 14, 180, 104)
INFO = {}   # 안별 측정·좌표 기록


def msub(a, b):
    return ImageChops.subtract(a, b)


def rect_mask(x0, y0, x1, y1):
    return pmask([(x0, y0), (x1, y0), (x1, y1), (x0, y1)])


def alpha_mask(lay):
    return lay.getchannel("A").point(lambda v: 255 if v > 0 else 0)


def area(m):
    return sum(1 for v in m.getdata() if v >= 128)


def bbox_h(m):
    bb = m.getbbox()
    return (bb[3] - bb[1]) if bb else 0


# =====================================================================
# 인물 러프 (LD 약 7.5등신, 정면/3/4)
# =====================================================================
PLAYER = dict(hair=("H", "J", "U"), top=("t", "0", "T"), pants=("4", "K"), skin=("S", "s", "l"), long=False)
HELPER = dict(hair=("y", "d", "h"), top=("D", "d", "D"), pants=("2", "4"), skin=("S", "s", "l"), long=True)


def person(cx, ty, h, pal, yaw=0.0, sw_k=0.95, arms="desk", hand_y=None, legs=True, chair=False):
    """앉은 인물. ty=머리 꼭대기 y, h=머리 높이(px). 앉은 키(머리 꼭대기~좌석면)=3.9h(7.5등신).

    반환: body 레이어(책상 앞에 놓기 전), arms 레이어(책상 위로 올라오는 팔·손), info.
    """
    body = canvas(W, H)
    armlay = canvas(W, H)
    seat = ty + 3.9 * h
    sh = ty + 1.15 * h
    tx = cx + yaw * 0.10 * h
    sw = sw_k * h
    hair_b, hair_d, hair_l = pal["hair"]
    top_b, top_d, top_l = pal["top"]
    sk, sk_d, sk_l = pal["skin"]

    # --- 의자 등받이 ---
    if chair:
        cm = pmask([(tx - sw * 1.0, sh - 0.2 * h), (tx + sw * 1.0, sh - 0.2 * h), (tx + sw * 1.05, seat + 0.5 * h),
                    (tx - sw * 1.05, seat + 0.5 * h)])
        fill_mask(body, cm, "K")
        shade_mask(body, cm, ["4", "K", "K"], axis=(1, 0), span=(tx - sw, tx + sw))
        outline_mask(body, cm, "K")

    # --- 긴 머리(뒤) ---
    hx = cx + yaw * 0.12 * h
    hw = 0.40 * h
    if pal["long"]:
        lm = pmask([(hx - hw - 0.1 * h, ty + 0.3 * h), (hx + hw + 0.1 * h, ty + 0.3 * h), (hx + hw + 0.3 * h, sh + 1.0 * h),
                    (hx - hw - 0.3 * h, sh + 1.0 * h)])
        fill_mask(body, lm, hair_d)
        outline_mask(body, lm, "K")

    # --- 다리(좌석 아래) ---
    leg_masks = []
    if legs == "cross":      # 침대 위 책상다리: 허리 앞에 넓게 포개진 다리
        lg = emask((round(tx - 1.15 * h), round(seat - 0.3 * h), round(tx + 1.15 * h), round(seat + 0.75 * h)))
        fill_mask(body, lg, pal["pants"][0])
        shade_mask(body, lg, [pal["pants"][0], pal["pants"][0], pal["pants"][1]], axis=(0, 1), span=(seat - 0.3 * h, seat + 0.75 * h))
        draw_line(body, [(round(tx - 0.8 * h), round(seat + 0.1 * h)), (round(tx + 0.2 * h), round(seat + 0.4 * h)),
                         (round(tx + 0.9 * h), round(seat + 0.2 * h))], pal["pants"][1])
        outline_mask(body, lg, "K")
        for sgn in (-1, 1):
            ft = emask((round(tx + sgn * 0.95 * h - 0.3 * h), round(seat + 0.35 * h), round(tx + sgn * 0.95 * h + 0.3 * h), round(seat + 0.8 * h)))
            fill_mask(body, ft, "K")
            leg_masks.append(ft)
        leg_masks.append(lg)
    elif legs:
        hipw = sw * 0.84
        for sgn in (-1, 1):
            tcx = tx + sgn * 0.42 * h
            th = pmask([(tcx - 0.34 * h, seat - 0.15 * h), (tcx + 0.34 * h, seat - 0.15 * h),
                        (tcx + 0.36 * h, seat + 0.75 * h), (tcx - 0.36 * h, seat + 0.75 * h)])
            fill_mask(body, th, pal["pants"][0])
            shade_mask(body, th, [pal["pants"][0], pal["pants"][1]], axis=(0, 1))
            scx = tx + sgn * 0.5 * h
            sm = pmask([(scx - 0.24 * h, seat + 0.7 * h), (scx + 0.24 * h, seat + 0.7 * h),
                        (scx + 0.22 * h, seat + 2.65 * h), (scx - 0.22 * h, seat + 2.65 * h)])
            fill_mask(body, sm, pal["pants"][0])
            shade_mask(body, sm, [pal["pants"][0], pal["pants"][1], pal["pants"][1]], axis=(0, 1))
            ft = pmask([(scx - 0.3 * h, seat + 2.6 * h), (scx + 0.3 * h, seat + 2.6 * h),
                        (scx + 0.4 * h, seat + 3.0 * h), (scx - 0.4 * h, seat + 3.0 * h)])
            fill_mask(body, ft, "K")
            for mk in (th, sm, ft):
                outline_mask(body, mk, "K")
                leg_masks.append(mk)

    # --- 몸통 ---
    torso = [(tx - sw * 0.5, sh - 0.05 * h), (tx + sw * 0.5, sh - 0.05 * h), (tx + sw, sh + 0.25 * h),
             (tx + sw * 0.92, sh + 1.0 * h), (tx + sw * 0.74, ty + 2.9 * h), (tx + sw * 0.84, seat),
             (tx - sw * 0.84, seat), (tx - sw * 0.74, ty + 2.9 * h), (tx - sw * 0.92, sh + 1.0 * h), (tx - sw, sh + 0.25 * h)]
    bm = pmask(torso)
    fill_mask(body, bm, top_b)
    shade_mask(body, bm, [top_l, top_b, top_b], axis=(0, 1), span=(sh - 2, ty + 2.2 * h))
    # 옆면 그림자(가운데가 밝다)
    shade_mask(body, bm, [top_d, top_b, top_b, top_b, top_d], axis=(1, 0), span=(tx - sw, tx + sw))
    outline_mask(body, bm, "K")

    # --- 목 ---
    nm = pmask([(tx - 0.15 * h, ty + 0.8 * h), (tx + 0.15 * h, ty + 0.8 * h), (tx + 0.17 * h, sh + 0.05 * h), (tx - 0.17 * h, sh + 0.05 * h)])
    fill_mask(body, nm, sk_d)
    outline_mask(body, nm, "K", where=lambda x, y: y > ty + h)

    # --- 머리 ---
    head = emask((round(hx - hw), round(ty), round(hx + hw), round(ty + h - 1)))
    fx = hx + yaw * 0.08 * h
    face_box = rect_mask(round(fx - 0.30 * h), round(ty + 0.40 * h), round(fx + 0.30 * h), round(ty + 1.2 * h))
    hair_big = emask((round(hx - hw - 1), round(ty - 1), round(hx + hw + 1), round(ty + 0.95 * h)))
    hair = msub(hair_big, face_box)
    face = msub(head, hair)
    fill_mask(body, head, sk)
    shade_mask(body, face, [sk, sk, sk, sk_d], axis=(0, 1), span=(ty + 0.4 * h, ty + h))
    fill_mask(body, hair, hair_b)
    shade_mask(body, hair, [hair_l, hair_b, hair_b, hair_d], axis=(0, 1), span=(ty, ty + 0.95 * h))
    # 앞머리 선(이마 위 한 줄) + 눈
    ey = round(ty + 0.58 * h)
    for sgn in (-1, 1):
        ex = round(fx + sgn * 0.17 * h)
        if h >= 20:
            pfill(body, ex, ey, ex + 1, ey + 1, "K")
            pfill(body, ex - 1, ey - 3, ex + 1, ey - 3, hair_d)    # 눈썹
        else:
            pset(body, ex, ey, "K")
            if h >= 13:
                pset(body, ex, ey - 1, hair_d)          # 눈썹 1px
    # 화면 빛(앞에서 오는 청록)이 닿는 아래쪽 뺨·턱 — 디더 점
    dith(body, (round(fx - 0.28 * h), round(ty + 0.76 * h), round(fx + 0.28 * h), round(ty + 0.96 * h)), "c", 1)
    outline_mask(body, mask_union(head, hair_big if False else head), "K")
    outline_mask(body, hair, "K", where=lambda x, y: y < ty + 0.7 * h)
    head_full = mask_union(head, hair)

    # --- 팔 ---
    arm_masks = []
    tgt = armlay if arms == "desk" else body
    for sgn in (-1, 1):
        sx, sy = tx + sgn * sw * 0.93, sh + 0.3 * h
        ex_, ey_ = tx + sgn * sw * 1.02, sh + 1.65 * h
        if arms == "desk":
            hxp, hyp = tx + sgn * 0.45 * h, hand_y
        else:   # 무릎 위
            hxp, hyp = tx + sgn * 0.55 * h, seat - 0.05 * h
        ua = pmask([(sx - 0.2 * h, sy), (sx + 0.2 * h, sy), (ex_ + 0.19 * h, ey_), (ex_ - 0.19 * h, ey_)])
        fa = pmask([(ex_ - 0.17 * h, ey_), (ex_ + 0.17 * h, ey_), (hxp + 0.16 * h, hyp), (hxp - 0.16 * h, hyp)])
        hd = emask((round(hxp - 0.24 * h), round(hyp - 0.14 * h), round(hxp + 0.24 * h), round(hyp + 0.16 * h)))
        fill_mask(tgt, ua, top_b)
        shade_mask(tgt, ua, [top_b, top_d], axis=(1, 0), span=(tx - sw * 1.2, tx + sw * 1.2) if sgn < 0 else (tx + sw * 1.2, tx - sw * 1.2))
        fill_mask(tgt, fa, top_b)
        fill_mask(tgt, hd, sk)
        for mk in (ua, fa, hd):
            outline_mask(tgt, mk, "K")
        arm_masks += [ua, fa, hd]

    full = mask_union(bm, nm, head_full, *leg_masks, *arm_masks)
    info = dict(head=head_full, face=face, torso=bm, full=full, seat=round(seat), ty=ty, h=h,
                head_h=bbox_h(head_full), floor=round(seat + 3.0 * h), hand_y=hand_y)
    return body, armlay, info


# =====================================================================
# 방 소품
# =====================================================================
def bookshelf(im, x0, y0, x1, y1, seed, rows=4):
    pfill(im, x0, y0, x1, y1, "0")
    pbox(im, x0, y0, x1, y1, "K")
    rng = random.Random(seed)
    rh = (y1 - y0 - 2) // rows
    for r in range(rows):
        sy = y0 + 1 + r * rh
        x = x0 + 3
        while x < x1 - 8:
            bw = rng.randint(3, 6)
            bh = rng.randint(max(6, rh - 12), rh - 4)
            col = rng.choice(["1", "4", "j", "y", "g", "r", "2", "B"])
            pfill(im, x, sy + rh - 3 - bh, x + bw - 1, sy + rh - 4, col)
            pbox(im, x, sy + rh - 3 - bh, x + bw - 1, sy + rh - 4, "K")
            x += bw + rng.randint(0, 1)
        pfill(im, x0 + 1, sy + rh - 3, x1 - 1, sy + rh - 2, "4")
        pfill(im, x0 + 1, sy + rh - 1, x1 - 1, sy + rh - 1, "K")


def server_rack(im, x0, y0, x1, y1, seed=1):
    pfill(im, x0, y0, x1, y1, "K")
    pbox(im, x0 + 1, y0 + 1, x1 - 1, y1 - 1, "2")
    pfill(im, x0 + 2, y0 + 2, x1 - 2, y1 - 2, "0")
    leds = ["G", "G", "Y", "R", "c", "G"]
    k = 0
    for ry in range(y0 + 4, y1 - 9, 11):
        pfill(im, x0 + 4, ry, x1 - 4, ry + 8, "4")
        pbox(im, x0 + 4, ry, x1 - 4, ry + 8, "K")
        for i in range(3):
            c = leds[(k + i + seed) % 6]
            pset(im, x0 + 7 + i * 4, ry + 3, c)
            pset(im, x0 + 8 + i * 4, ry + 3, c)
        for i in range(4):
            pset(im, x1 - 8 - i * 2, ry + 2, "2")
            pset(im, x1 - 8 - i * 2, ry + 5, "2")
        k += 1


def poster(im, x0, y0, x1, y1):
    pfill(im, x0, y0, x1, y1, "B")
    pbox(im, x0, y0, x1, y1, "K")
    for x in range(x0 + 5, x1 - 3, 6):
        pfill(im, x, y0 + 3, x, y1 - 3, "b")
    cx = (x0 + x1) // 2
    pbox(im, cx - 7, y0 + (y1 - y0) // 2, cx + 7, y1 - 5, "C")
    pbox(im, cx - 4, y0 + 8, cx + 4, y0 + (y1 - y0) // 2, "C")
    for (tx, ty) in [(x0, y0), (x1 - 2, y0), (x0, y1 - 2), (x1 - 2, y1 - 2)]:
        pfill(im, tx, ty, tx + 2, ty + 2, "h")


def window_panes(im, frame, panes, seed, sill=None, quad=None):
    """frame=(x0,y0,x1,y1) 바깥 틀, panes=[(x0,y0,x1,y1)...] 유리."""
    x0, y0, x1, y1 = frame
    pfill(im, x0, y0, x1, y1, "K")
    pbox(im, x0 + 1, y0 + 1, x1 - 1, y1 - 1, "2")
    pfill(im, x0 + 2, y0 + 2, x1 - 2, y1 - 2, "4")
    for i, p in enumerate(panes):
        pfill(im, *p, "K")
        city_view(im, p, seed=seed + i * 5, horizon=0.8)
        rain(im, p, seed + i, 9, 7)
        pbox(im, p[0] - 1, p[1] - 1, p[2] + 1, p[3] + 1, "K")
    if sill:
        sx0, sy0, sx1, sy1 = sill
        pfill(im, sx0, sy0, sx1, sy1, "3")
        pfill(im, sx0, sy0 + 2, sx1, sy1, "4")
        pbox(im, sx0, sy0, sx1, sy1, "K")


def bedside_lamp(im, x, y_floor):
    """작은 협탁 + 스탠드(따뜻한 불빛). x=가운데."""
    pfill(im, x - 11, y_floor - 24, x + 11, y_floor, "d")
    pbox(im, x - 11, y_floor - 24, x + 11, y_floor, "K")
    pfill(im, x - 11, y_floor - 24, x + 11, y_floor - 22, "D")
    pfill(im, x - 1, y_floor - 36, x + 1, y_floor - 24, "K")
    draw_poly(im, [(x - 9, y_floor - 52), (x + 9, y_floor - 52), (x + 12, y_floor - 36), (x - 12, y_floor - 36)], "K")
    draw_poly(im, [(x - 7, y_floor - 50), (x + 7, y_floor - 50), (x + 10, y_floor - 38), (x - 10, y_floor - 38)], "h")
    pfill(im, x - 3, y_floor - 46, x + 3, y_floor - 42, "W")


def bed(im, x0, x1, floor_y, slope, side_h, top_d, head_h=34, cov=(0.0, 0.62)):
    """침대(긴 옆면이 카메라 쪽). 오른쪽 끝에 머리판. 반환: 좌석면 y 함수."""
    def yl(x):
        return floor_y + slope * (x - x0)
    side = [(x0, yl(x0) - side_h), (x1, yl(x1) - side_h), (x1, yl(x1)), (x0, yl(x0))]
    top = [(x0, yl(x0) - side_h), (x1, yl(x1) - side_h), (x1, yl(x1) - side_h - top_d), (x0, yl(x0) - side_h - top_d)]
    # 머리판
    hb = [(x1 - 8, yl(x1 - 8) - side_h - top_d - head_h), (x1, yl(x1) - side_h - top_d - head_h), (x1, yl(x1)), (x1 - 8, yl(x1 - 8))]
    fill_mask(im, pmask(hb), "d")
    outline_mask(im, pmask(hb), "K")
    fill_mask(im, pmask(side), "d")
    dith(im, (int(x0), int(yl(x0) - side_h), int(x1), int(yl(x1))), "K", 4)
    sm = pmask(side)
    shade_mask(im, pmask([(x0, yl(x0) - side_h), (x1, yl(x1) - side_h), (x1, yl(x1) - side_h + 6), (x0, yl(x0) - side_h + 6)]), ["j"], axis=(0, 1))
    outline_mask(im, sm, "K")
    fill_mask(im, pmask(top), "j")
    outline_mask(im, pmask(top), "K", where=lambda x, y: True)
    # 이불(왼쪽~가운데) + 늘어진 단
    bxs = x0 + (x1 - x0) * cov[0] + 2
    bx1 = x0 + (x1 - x0) * cov[1]
    cover = [(bxs, yl(x0) - side_h - 1), (bx1, yl(bx1) - side_h - 1), (bx1, yl(bx1) - side_h - top_d + 1), (bxs, yl(bxs) - side_h - top_d + 1)]
    fill_mask(im, pmask(cover), "B")
    drape = [(bxs, yl(bxs) - side_h), (bx1, yl(bx1) - side_h), (bx1, yl(bx1) - side_h + 9), (bxs, yl(bxs) - side_h + 9)]
    fill_mask(im, pmask(drape), "B")
    draw_line(im, [(bxs, yl(bxs) - side_h), (bx1, yl(bx1) - side_h)], "b")
    outline_mask(im, pmask(drape), "K", where=lambda x, y: True)
    # 베개
    px0 = x1 - 38
    pil = [(px0, yl(px0) - side_h - 1), (x1 - 10, yl(x1 - 10) - side_h - 1), (x1 - 10, yl(x1 - 10) - side_h - top_d - 3), (px0 + 3, yl(px0) - side_h - top_d - 3)]
    fill_mask(im, pmask(pil), "3")
    outline_mask(im, pmask(pil), "K")
    return lambda x: yl(x) - side_h - top_d * 0.5      # 좌석(앉는) 높이 선


# =====================================================================
# 쌓기 도우미 · 조명
# =====================================================================
class Stack:
    def __init__(self):
        self.im = canvas(W, H, PIX["K"])
        self.layers = []

    def add(self, name, lay):
        self.im.alpha_composite(lay)
        self.layers.append((name, lay))

    def later_alpha(self, name):
        """name 층보다 위에 올라간 모든 층의 불투명 영역 합."""
        out = Image.new("L", (W, H), 0)
        seen = False
        for n, lay in self.layers:
            if seen:
                out.paste(255, (0, 0), alpha_mask(lay))
            if n == name:
                seen = True
        return out


def inter(a, b):
    return ImageChops.multiply(a, b)


def occlusion(st, name, mask):
    """mask(전체 모양) 중 name 층 뒤쪽에 쌓인 층이 가린 비율."""
    later = st.later_alpha(name)
    tot = area(mask)
    return (area(inter(mask, later)) / tot) if tot else 0.0


def cool_front(lay, cx, cy, rx, ry):
    relight(lay, ell(cx, cy, rx, ry, 0.7), COOL1, COOL2)


def zone_report(scene, key):
    mx, ma = dr.zone_stats(scene, MENU_ZONE)
    INFO[key]["menu_zone"] = (round(mx), round(ma))


def measure_person(st, key, tag, layer_name, info, glass=None):
    occ = occlusion(st, layer_name, info["full"])
    INFO[key][tag] = dict(head_h=info["head_h"], seat_px=info["seat"] - info["ty"], ty=info["ty"], seat_y=info["seat"],
                          eq=round((info["seat"] - info["ty"]) / info["head_h"], 2),
                          occl=round(occ * 100, 1), bbox=info["full"].getbbox())
    zb = MENU_ZONE
    fb = info["full"].getbbox()
    INFO[key][tag]["in_menu_zone"] = bool(fb and not (fb[2] < zb[0] or fb[0] > zb[2] or fb[3] < zb[1] or fb[1] > zb[3]))


def check_face_clear(st, key, layer_after, info):
    """주인공 얼굴(머리 전체)이 그 뒤에 쌓인 층에 가려지는 픽셀 수."""
    later = st.later_alpha(layer_after)
    INFO[key]["face_covered_px"] = area(inter(info["head"], later))


def monitor_back(lay, q, side, neck=True):
    """모니터 뒷면(카메라 쪽). side: 안쪽 모서리가 오는 방향."""
    tl, tr, br, bl = q
    # 목(스탠드) + 받침
    cxm = (tl[0] + tr[0]) // 2
    by = max(bl[1], br[1])
    if neck:
        pfill(lay, cxm - 8, by - 18, cxm + 8, by + 8, "4")
        pfill(lay, cxm - 8, by - 18, cxm - 6, by + 8, "2")
        pbox(lay, cxm - 8, by - 18, cxm + 8, by + 8, "K")
        draw_poly(lay, [(cxm - 30, by + 8), (cxm + 30, by + 8), (cxm + 40, by + 20), (cxm - 40, by + 20)], "2")
        draw_poly(lay, [(cxm - 30, by + 8), (cxm + 30, by + 8), (cxm + 28, by + 11), (cxm - 28, by + 11)], "3")
        draw_line(lay, [(cxm - 40, by + 20), (cxm + 40, by + 20)], "K")
        draw_line(lay, [(cxm - 30, by + 8), (cxm - 40, by + 20)], "K")
        draw_line(lay, [(cxm + 30, by + 8), (cxm + 40, by + 20)], "K")
    draw_poly(lay, list(q), "K")
    inner = [(tl[0] + 2, tl[1] + 2), (tr[0] - 2, tr[1] + 2), (br[0] - 2, br[1] - 2), (bl[0] + 2, bl[1] - 2)]
    draw_poly(lay, inner, "4")
    m = pmask(inner)
    shade_mask(lay, m, ["2", "4", "4", "0"], axis=(0, 1))
    # 환기구 줄, 로고, 단자
    for k in range(5):
        a = quad_pt(q, 0.12, 0.62 + k * 0.06)
        b = quad_pt(q, 0.40, 0.62 + k * 0.06)
        draw_line(lay, [a, b], "K")
    lg = quad_pt(q, 0.5, 0.38)
    pfill(lay, lg[0] - 3, lg[1] - 2, lg[0] + 3, lg[1] + 2, "3")
    pbox(lay, lg[0] - 3, lg[1] - 2, lg[0] + 3, lg[1] + 2, "K")
    pt = quad_pt(q, 0.5, 0.9)
    for i in range(3):
        pfill(lay, pt[0] - 12 + i * 9, pt[1] - 1, pt[0] - 8 + i * 9, pt[1] + 1, "K")
    led = quad_pt(q, 0.93, 0.93)
    pfill(lay, led[0], led[1], led[0] + 1, led[1], "G")
    return m


def lamp_only(im, x, y):
    """벽 선반 위 작은 스탠드(따뜻한 불빛). y=받침 바닥."""
    pfill(im, x - 8, y - 3, x + 8, y, "2")
    pbox(im, x - 8, y - 3, x + 8, y, "K")
    pfill(im, x - 1, y - 18, x + 1, y - 3, "K")
    draw_poly(im, [(x - 9, y - 34), (x + 9, y - 34), (x + 12, y - 18), (x - 12, y - 18)], "K")
    draw_poly(im, [(x - 7, y - 32), (x + 7, y - 32), (x + 10, y - 20), (x - 10, y - 20)], "h")
    pfill(im, x - 3, y - 28, x + 3, y - 24, "W")


def curtains(im, x0, y0, x1, y1, side):
    """창 옆 커튼(어두운 보라 주름)."""
    pfill(im, x0, y0, x1, y1, "f")
    for x in range(x0 + 2, x1, 4):
        pfill(im, x, y0, x, y1, "e")
    pbox(im, x0, y0, x1, y1, "K")


def bed_mirrored(im, x0, x1, floor_y, side_h, top_d, head_h):
    """머리판이 왼쪽에 오도록 좌우를 뒤집은 침대(기울기 0). 좌석 높이 함수 반환."""
    tmp = canvas(W, H)
    seat = bed(tmp, x0, x1, floor_y, 0.0, side_h, top_d, head_h)
    bb = tmp.getbbox()
    part = tmp.crop(bb).transpose(Image.FLIP_LEFT_RIGHT)
    im.alpha_composite(part, (bb[0], bb[1]))
    return seat


def floor_visible(st, floor_mask):
    later = st.later_alpha("room")
    vis = ImageChops.subtract(floor_mask, later)
    return round(area(vis) / (W * H) * 100, 1)


def finish(st, key, pi, hi, floor_mask):
    measure_person(st, key, "player", "player", pi)
    measure_person(st, key, "helper", "helper", hi)
    check_face_clear(st, key, "player", pi)
    zone_report(st.im, key)
    INFO[key]["floor_pct"] = floor_visible(st, floor_mask)
    return st.im


# =====================================================================
# 안 A — 정면 대칭 (모니터 2대 사이 주인공, 침대·조력자는 뒤 오른쪽)
# =====================================================================
A_GLASS = [(220, 70, 316, 91), (324, 70, 420, 91), (220, 95, 316, 114), (324, 95, 420, 114)]
A_MON_L = ((132, 188), (262, 200), (262, 262), (132, 268))      # 모니터 뒷면(왼쪽) TL,TR,BR,BL
A_MON_R = ((378, 200), (508, 188), (508, 268), (378, 262))


def scene_a():
    key = "A"
    INFO[key] = {"glass": A_GLASS, "monitors": {"L": A_MON_L, "R": A_MON_R}}
    st = Stack()
    WT, WB = 52, 238
    r = canvas(W, H, PIX["a"])
    pfill(r, 0, WT, W - 1, WB, "0")
    dith(r, (0, WT, W - 1, WT + 30), "a", 7)
    dith(r, (0, WT + 31, W - 1, WT + 70), "a", 3, 1)
    for x in range(0, W, 96):
        pfill(r, x, WT, x, WB, "K")
    pfill(r, 0, WT - 1, W - 1, WT - 1, "K")
    for x0 in (210, 320, 430):
        draw_line(r, [(320, WT - 1), (x0, 0)], "0")
    pfill(r, 0, WB + 1, W - 1, H - 1, "0")
    for xb in range(-300, 1000, 70):
        xt = 320 + (xb - 320) * (WB + 1 - 150) / (360 - 150)
        draw_line(r, [(round(xt), WB + 1), (xb, 360)], "K")
    for y in (250, 268, 292, 322, 350):
        pfill(r, 0, y, W - 1, y, "K")
    dith(r, (0, WB + 1, W - 1, 359), "1", 2, 1)
    pfill(r, 0, WB - 4, W - 1, WB, "4")
    pfill(r, 0, WB - 4, W - 1, WB - 4, "2")
    floor_mask = rect_mask(0, WB + 1, W - 1, H - 1)
    # 벽 생활감: 책장, 서버 랙, 창(커튼·창틀·창살·창턱), 포스터, 선반 스탠드
    bookshelf(r, 24, 112, 116, WB - 4, seed=4, rows=4)
    server_rack(r, 126, 118, 166, WB - 4, seed=1)
    curtains(r, 196, 58, 213, 128, "L")
    curtains(r, 427, 58, 444, 128, "R")
    window_panes(r, (214, 64, 426, 120), A_GLASS, seed=11, sill=(208, 120, 432, 126))
    poster(r, 476, 100, 526, 146)
    pfill(r, 440, 150, 470, 153, "d")
    pbox(r, 440, 150, 470, 153, "K")
    lamp_only(r, 455, 150)
    seat_y = bed(r, 440, 636, 246, 0.0, 28, 18, head_h=44, cov=(0.25, 0.80))
    glass_u = mask_union(*[rect_mask(*g) for g in A_GLASS])
    sg = lambda x, y: glass_u.getpixel((x, y)) > 128
    relight(r, ell(470, 150, 90, 70, 1.2), WARM1, WARM2, skip=sg)
    relight(r, ell(320, 100, 130, 50, 0.6), COOL1, None, skip=sg)
    st.add("room", r)

    # 조력자: 침대 위(주인공 오른쪽 뒤), 책상다리로 앉음
    h2 = 15
    sy = round(seat_y(575)) + 3
    hb, ha, hi = person(575, sy - round(3.9 * h2), h2, HELPER, yaw=-0.3, sw_k=0.82, arms="lap", legs="cross")
    relight(hb, ell(470, 170, 160, 90, 0.8), WARM1, None)
    st.add("helper", hb)

    # 주인공: 모니터 바로 뒤, 얼굴 정면
    h1 = 24
    pb, pa, pi = person(320, 122, h1, PLAYER, yaw=0.0, sw_k=0.95, arms="desk", hand_y=207, legs=False, chair=True)
    cool_front(pb, 320, 165, 60, 70)
    st.add("player", pb)

    # 책상: 모니터와 주인공 사이는 얇은 띠뿐, 앞은 어두운 앞판
    d = canvas(W, H)
    top = [(150, 196), (490, 196), (550, 262), (90, 262)]
    draw_poly(d, top, "d")
    shade_mask(d, pmask(top), ["d", "d", "D"], axis=(0, 1))
    dith(d, (90, 196, 550, 262), "K", 8)
    draw_line(d, [(150, 196), (490, 196)], "K")
    draw_poly(d, [(90, 262), (550, 262), (550, 268), (90, 268)], "D")
    pfill(d, 90, 269, 550, 359, "0")
    pfill(d, 200, 272, 340, 359, "a")
    pbox(d, 400, 276, 530, 310, "K")
    pbox(d, 401, 277, 529, 309, "d")
    pfill(d, 452, 291, 478, 293, "h")
    pbox(d, 400, 314, 530, 350, "K")
    pbox(d, 401, 315, 529, 349, "d")
    pfill(d, 452, 330, 478, 332, "h")
    pfill(d, 90, 269, 98, 359, "K")
    pfill(d, 542, 269, 550, 359, "K")
    kb = [(286, 200), (354, 200), (362, 214), (278, 214)]
    draw_poly(d, kb, "K")
    draw_poly(d, [(288, 201), (352, 201), (359, 212), (281, 212)], "2")
    for rr in range(2):
        draw_line(d, [(290 - rr * 3, 204 + rr * 4), (350 + rr * 3, 204 + rr * 4)], "3")
    cool_front(d, 320, 215, 190, 60)
    st.add("desk", d)
    st.add("hands", pa)

    m = canvas(W, H)
    monitor_back(m, A_MON_L, "L", neck=False)
    monitor_back(m, A_MON_R, "R", neck=False)
    pfill(m, 176, 268, 218, 272, "K")
    pfill(m, 422, 268, 464, 272, "K")
    draw_line(m, [(190, 268), (186, 290), (172, 312), (176, 359)], "K", 2)
    draw_line(m, [(450, 268), (454, 290), (468, 312), (464, 359)], "K", 2)
    rim(m, pmask(list(A_MON_L)), "c", [(1, 0), (0, -1)], 1, fade=lambda x, y: 0.6)
    rim(m, pmask(list(A_MON_R)), "c", [(-1, 0), (0, -1)], 1, fade=lambda x, y: 0.6)
    st.add("monitors", m)
    cool_front(m, 320, 230, 330, 60)
    return finish(st, key, pi, hi, floor_mask)


# =====================================================================
# 안 B — 대각선 3/4 (카메라는 책상 오른쪽 앞, 방은 왼쪽 안쪽으로 깊어진다)
# =====================================================================
VPX, VPY = -900.0, 165.0
K1, K2 = (238 - VPY) / (380 - VPX), (VPY - 28) / (380 - VPX)


def b_base(x):
    return VPY + K1 * (x - VPX)


def b_top(x):
    return VPY - K2 * (x - VPX)


def b_wall(x, t):
    return b_base(x) + (b_top(x) - b_base(x)) * t


B_MON = ((280, 182), (700, 214), (700, 372), (280, 276))   # 모니터 뒷면(오른쪽·아래 화면 밖으로 잘림) TL,TR,BR,BL


def wall_quad(x0, x1, t0, t1):
    return [(x0, b_wall(x0, t1)), (x1, b_wall(x1, t1)), (x1, b_wall(x1, t0)), (x0, b_wall(x0, t0))]


def scene_b():
    key = "B"
    st = Stack()
    r = canvas(W, H, PIX["a"])
    draw_poly(r, [(0, b_top(0)), (W, b_top(W)), (W, b_base(W)), (0, b_base(0))], "0")
    dith(r, (0, 30, W - 1, 130), "a", 5)
    for x in range(40, W, 100):
        draw_line(r, [(x, b_top(x)), (x, b_base(x))], "K")
    draw_line(r, [(0, b_top(0)), (W, b_top(W))], "K")
    for xx in (230, 400, 560):
        draw_line(r, [(xx, b_top(xx)), (xx + 30, 0)], "0")
    floor_poly = [(0, b_base(0)), (W, b_base(W)), (W, H), (0, H)]
    draw_poly(r, floor_poly, "0")
    floor_mask = pmask(floor_poly)
    for k in range(1, 9):
        draw_line(r, [(0, round(b_base(0) + k * 20 + k * k)), (W, round(b_base(W) + k * 28 + k * k * 2))], "K")
    dith(r, (0, 200, W - 1, 359), "1", 2, 1)
    draw_line(r, [(0, b_base(0)), (W, b_base(W))], "2")
    draw_line(r, [(0, b_base(0) - 3), (W, b_base(W) - 3)], "4")
    # 창: 커튼 + 창틀 + 2x2 유리 + 창턱
    for (cx0, cx1) in ((176, 189), (301, 314)):
        q = wall_quad(cx0, cx1, 0.34, 0.94)
        draw_poly(r, q, "f")
        outline_mask(r, pmask(q), "K")
    draw_poly(r, wall_quad(190, 300, 0.40, 0.90), "K")
    draw_poly(r, wall_quad(192, 298, 0.41, 0.89), "4")
    gl = []
    for (x0, x1) in ((195, 242), (248, 295)):
        for (t0, t1) in ((0.44, 0.64), (0.67, 0.86)):
            gl.append(wall_quad(x0, x1, t0, t1))
    glass_masks = []
    for quad in gl:
        gm = pmask(quad)
        glass_masks.append(gm)
        bb = gm.getbbox()
        clip_draw(r, gm, lambda lay, bb=bb: (city_view(lay, (bb[0], bb[1], bb[2] - 1, bb[3] - 1), seed=21, horizon=0.8),
                                             rain(lay, (bb[0], bb[1], bb[2] - 1, bb[3] - 1), 3, 4, 6)))
        outline_mask(r, gm, "K")
    sill = [(186, b_wall(186, 0.40) + 1), (304, b_wall(304, 0.40) + 1), (304, b_wall(304, 0.40) + 6), (186, b_wall(186, 0.40) + 6)]
    draw_poly(r, sill, "3")
    outline_mask(r, pmask(sill), "K")
    glass_union = mask_union(*glass_masks)
    INFO[key] = {"glass": [m.getbbox() for m in glass_masks], "monitors": {"main": B_MON}}
    # 포스터(침대 위), 서버 랙, 오른쪽 책장
    pq = wall_quad(42, 112, 0.38, 0.72)
    draw_poly(r, pq, "B")
    outline_mask(r, pmask(pq), "K")
    for x in range(48, 110, 7):
        draw_line(r, [(x, b_wall(x, 0.69)), (x, b_wall(x, 0.41))], "b")
    server_rack(r, 318, round(b_wall(318, 0.74)), 350, round(b_base(318)) - 3, seed=2)
    bookshelf(r, 452, 96, 612, 236, seed=7, rows=5)
    # 침대(왼쪽 안쪽, 크게) + 협탁 스탠드
    seat_y = bed(r, 20, 250, round(b_base(135)) + 10, K1, 26, 16, head_h=36)
    bedside_lamp(r, 268, round(b_base(268)) + 10)
    sg = lambda x, y: glass_union.getpixel((x, y)) > 128
    relight(r, ell(250, 180, 90, 70, 1.2), WARM1, WARM2, skip=sg)
    relight(r, ell(300, 120, 140, 60, 0.6), COOL1, None, skip=sg)
    st.add("room", r)

    h2 = 15
    sy = round(seat_y(140)) + 3
    hb, ha, hi = person(140, sy - round(3.9 * h2), h2, HELPER, yaw=0.5, sw_k=0.82, arms="lap", legs="cross")
    relight(hb, ell(250, 190, 150, 90, 0.8), WARM1, None)
    st.add("helper", hb)

    h1 = 24
    pb, pa, pi = person(382, 118, h1, PLAYER, yaw=-0.7, sw_k=0.95, arms="desk", hand_y=214, legs=False, chair=True)
    cool_front(pb, 382, 165, 62, 70)
    st.add("player", pb)

    d = canvas(W, H)
    top = [(200, 172), (640, 214), (640, 366), (170, 262)]
    draw_poly(d, top, "d")
    shade_mask(d, pmask(top), ["d", "d", "D"], axis=(0.3, 1))
    dith(d, (170, 172, 640, 366), "K", 8)
    draw_line(d, [(200, 172), (640, 214)], "K")
    draw_poly(d, [(170, 262), (640, 366), (640, 372), (170, 268)], "D")
    draw_poly(d, [(170, 268), (640, 372), (640, 359), (170, 359)], "0")
    # 책상 위 소품(모니터 왼쪽 빈 곳): 머그, 공유기, 책 더미
    pfill(d, 214, 190, 226, 204, "3")
    pfill(d, 214, 190, 226, 191, "W")
    pbox(d, 214, 190, 226, 204, "K")
    pbox(d, 227, 194, 231, 200, "K")
    pfill(d, 238, 196, 262, 206, "K")
    pfill(d, 240, 198, 260, 204, "4")
    pset(d, 244, 201, "G")
    pset(d, 248, 201, "G")
    pset(d, 252, 201, "Y")
    for k, c in enumerate(("r", "B", "y")):
        y0 = 224 - k * 5
        pfill(d, 214 + k, y0, 250 - k * 2, y0 + 6, c)
        pbox(d, 214 + k, y0, 250 - k * 2, y0 + 6, "K")
    cool_front(d, 380, 215, 200, 80)
    st.add("desk", d)
    st.add("hands", pa)

    m = canvas(W, H)
    tl, tr, br, bl = B_MON
    draw_poly(m, list(B_MON), "K")
    inner = [(tl[0] + 3, tl[1] + 3), (tr[0] - 3, tr[1] + 2), (br[0] - 3, br[1] - 3), (bl[0] + 3, bl[1] - 3)]
    draw_poly(m, inner, "4")
    shade_mask(m, pmask(inner), ["2", "4", "4", "0"], axis=(1, 0.4))
    for k in range(7):
        draw_line(m, [quad_pt(B_MON, 0.08, 0.58 + k * 0.05), quad_pt(B_MON, 0.30, 0.58 + k * 0.05)], "K")
    lg = quad_pt(B_MON, 0.5, 0.36)
    pfill(m, lg[0] - 4, lg[1] - 3, lg[0] + 4, lg[1] + 3, "3")
    pbox(m, lg[0] - 4, lg[1] - 3, lg[0] + 4, lg[1] + 3, "K")
    pt = quad_pt(B_MON, 0.2, 0.9)
    for i in range(3):
        pfill(m, pt[0] + i * 12, pt[1], pt[0] + 6 + i * 12, pt[1] + 3, "K")
    draw_line(m, [(310, 262), (300, 290), (286, 316), (296, 345), (280, 359)], "K", 2)
    draw_line(m, [(340, 270), (330, 300), (318, 330), (328, 359)], "K", 2)
    pfill(m, 286, 196, 287, 197, "G")
    rim(m, pmask(list(B_MON)), "c", [(-1, 0), (0, -1)], 1, fade=lambda x, y: 0.7)
    cool_front(m, 480, 270, 220, 120)
    st.add("monitors", m)
    return finish(st, key, pi, hi, floor_mask)


# =====================================================================
# 안 C — 낮은 가까운 시점 (모니터 뒷면·케이블이 아래를 채우고 주인공 상반신이 크다)
# =====================================================================
C_GLASS = [(458, 78, 520, 108), (526, 78, 588, 108), (458, 113, 520, 144), (526, 113, 588, 144)]
C_MON_L = ((4, 196), (296, 188), (296, 380), (4, 380))
C_MON_R = ((338, 188), (636, 196), (636, 380), (338, 380))


def scene_c():
    key = "C"
    INFO[key] = {"glass": C_GLASS, "monitors": {"L": C_MON_L, "R": C_MON_R}}
    st = Stack()
    WT, WB = 44, 186
    r = canvas(W, H, PIX["a"])
    pfill(r, 0, WT, W - 1, WB, "0")
    dith(r, (0, WT, W - 1, WT + 30), "a", 7)
    dith(r, (0, WT + 31, W - 1, WT + 70), "a", 3, 1)
    for x in range(0, W, 110):
        pfill(r, x, WT, x, WB, "K")
    pfill(r, 0, WT - 1, W - 1, WT - 1, "K")
    for x0 in (220, 330, 440):
        draw_line(r, [(330, WT - 1), (x0, 0)], "0")
    pfill(r, 0, WB + 1, W - 1, H - 1, "0")
    pfill(r, 0, WB - 4, W - 1, WB, "4")
    pfill(r, 0, WB - 4, W - 1, WB - 4, "2")
    for xb in range(-400, 1100, 80):
        xt = 330 + (xb - 330) * (WB + 1 - 150) / (360 - 150)
        draw_line(r, [(round(xt), WB + 1), (xb, 360)], "K")
    floor_mask = rect_mask(0, WB + 1, W - 1, H - 1)
    # 창(오른쪽 뒤) + 커튼
    curtains(r, 440, 62, 457, 156, "L")
    curtains(r, 590, 62, 607, 156, "R")
    window_panes(r, (452, 72, 596, 150), C_GLASS, seed=33, sill=(446, 150, 602, 156))
    server_rack(r, 612, 110, 636, WB - 4, seed=3)
    # 침대(왼쪽 뒤, 머리판 왼쪽) + 포스터 + 선반
    poster(r, 86, 108, 134, 146)
    seat_y = bed_mirrored(r, 60, 300, 192, 24, 14, 40)
    pfill(r, 372, 150, 428, 153, "d")
    pbox(r, 372, 150, 428, 153, "K")
    lamp_only(r, 400, 150)
    glass_u = mask_union(*[rect_mask(*g) for g in C_GLASS])
    sg = lambda x, y: glass_u.getpixel((x, y)) > 128
    relight(r, ell(400, 130, 55, 45, 1.3), WARM1, WARM2, skip=sg)
    st.add("room", r)

    h2 = 16
    sy = round(seat_y(200)) + 3
    hb, ha, hi = person(200, sy - round(3.9 * h2), h2, HELPER, yaw=0.4, sw_k=0.82, arms="lap", legs="cross")
    relight(hb, ell(400, 150, 220, 90, 0.8), WARM1, None)
    st.add("helper", hb)

    h1 = 26
    pb, pa, pi = person(330, 94, h1, PLAYER, yaw=0.5, sw_k=0.98, arms="desk", hand_y=181, legs=False, chair=True)
    cool_front(pb, 330, 140, 64, 80)
    st.add("player", pb)

    d = canvas(W, H)
    top = [(200, 168), (460, 168), (640, 240), (0, 240)]
    draw_poly(d, top, "d")
    shade_mask(d, pmask(top), ["d", "d", "D"], axis=(0, 1))
    dith(d, (0, 168, 639, 240), "K", 8)
    draw_line(d, [(200, 168), (460, 168)], "K")
    pfill(d, 296, 240, 338, 359, "a")
    kb = [(288, 172), (372, 172), (378, 184), (282, 184)]
    draw_poly(d, kb, "K")
    draw_poly(d, [(290, 173), (370, 173), (375, 182), (285, 182)], "2")
    for rr in range(2):
        draw_line(d, [(294, 175 + rr * 4), (366, 175 + rr * 4)], "3")
    cool_front(d, 330, 200, 240, 50)
    st.add("desk", d)
    st.add("hands", pa)

    m = canvas(W, H)
    for q in (C_MON_L, C_MON_R):
        draw_poly(m, list(q), "K")
        tl, tr, br, bl = q
        inner = [(tl[0] + 3, tl[1] + 3), (tr[0] - 3, tr[1] + 3), (br[0] - 3, br[1] - 3), (bl[0] + 3, bl[1] - 3)]
        draw_poly(m, inner, "4")
        shade_mask(m, pmask(inner), ["2", "4", "4", "0"], axis=(0, 1))
    for q, ox in ((C_MON_L, 0), (C_MON_R, 1)):
        for k in range(6):
            draw_line(m, [quad_pt(q, 0.10 if not ox else 0.62, 0.15 + k * 0.03), quad_pt(q, 0.36 if not ox else 0.88, 0.15 + k * 0.03)], "K")
        lg = quad_pt(q, 0.5, 0.1)
        pfill(m, lg[0] - 5, lg[1] - 3, lg[0] + 5, lg[1] + 3, "3")
        pbox(m, lg[0] - 5, lg[1] - 3, lg[0] + 5, lg[1] + 3, "K")
    for x0 in (60, 112, 164, 420, 472, 524):
        pfill(m, x0, 280, x0 + 14, 286, "K")
        pfill(m, x0 + 2, 282, x0 + 12, 284, "0")
    for pts in ([(64, 286), (70, 310), (96, 330), (92, 360)], [(116, 286), (130, 314), (160, 336), (170, 360)],
                [(168, 286), (200, 310), (230, 330), (262, 360)], [(428, 286), (420, 310), (392, 330), (396, 360)],
                [(480, 286), (470, 316), (440, 338), (430, 360)], [(530, 286), (520, 310), (560, 332), (596, 360)],
                [(296, 230), (302, 270), (316, 310), (310, 360)], [(338, 230), (332, 270), (326, 300), (336, 360)]):
        draw_line(m, pts, "K", 3)
        draw_line(m, [(x + 1, y) for x, y in pts], "4", 1)
    pfill(m, 20, 206, 21, 207, "G")
    pfill(m, 616, 206, 617, 207, "G")
    rim(m, mask_union(pmask(list(C_MON_L)), pmask(list(C_MON_R))), "c", [(0, -1)], 1, fade=lambda x, y: 0.6)
    st.add("monitors", m)
    cool_front(m, 330, 220, 330, 50)
    return finish(st, key, pi, hi, floor_mask)


# =====================================================================
# 빌드
# =====================================================================
def build():
    out = EXPORT / "preview"
    out.mkdir(parents=True, exist_ok=True)
    menu = dr.menu_overlay(0)
    made, boards = [], []
    for key, fn in (("A", scene_a), ("B", scene_b), ("C", scene_c)):
        scene = fn()
        assert not palette_check(scene), key
        withmenu = over(scene, menu)
        assert not palette_check(withmenu), key + " menu"
        for name, im in ((f"main_lobby_v2_{key}.png", scene), (f"main_lobby_v2_{key}_x3.png", upscale(scene, 3)),
                         (f"main_lobby_v2_{key}_menu_x3.png", upscale(withmenu, 3))):
            im.save(out / name)
            made.append(out / name)
        boards.append(withmenu)
        pl, hp = INFO[key]["player"], INFO[key]["helper"]
        print(f"  [{key}] 주인공 머리 {pl['head_h']}px 앉은키 {pl['seat_px']}px({pl['eq']}머리) 가림 {pl['occl']}% bbox {pl['bbox']}")
        print(f"       조력자 머리 {hp['head_h']}px 앉은키 {hp['seat_px']}px({hp['eq']}머리) 가림 {hp['occl']}% bbox {hp['bbox']}")
        print(f"       얼굴 가린 픽셀 {INFO[key]['face_covered_px']}, 바닥 {INFO[key]['floor_pct']}%, 메뉴자리 최대/평균 밝기 {INFO[key]['menu_zone']}, "
              f"인물 메뉴자리 침범 {pl['in_menu_zone']}/{hp['in_menu_zone']}")
        print(f"       창 {INFO[key]['glass']} 모니터 {INFO[key]['monitors']}")
    sheet = canvas(W, H * 3 + 8, PIX["K"])
    for i, b in enumerate(boards):
        sheet.alpha_composite(b, (0, i * (H + 4)))
    assert not palette_check(sheet)
    sheet.save(out / "main_lobby_v2_compare.png")
    made.append(out / "main_lobby_v2_compare.png")
    return made
