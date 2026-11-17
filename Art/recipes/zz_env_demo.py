"""바깥 환경 이펙트 시안(D-080, Docs/Art/environment-fx.md) — 주인공 방 창으로 본 낮/밤 × 맑음·흐림·비·눈·안개 10조합.

엔진에서는 실시간으로 그리는 것을 전제로, 여기서는 같은 규칙으로 90프레임(30 FPS, 3초) 반복 미리보기를 만든다.
그리기 순서: L0 하늘(시간대·날씨 색) → L0 도시(낮이면 불 꺼진 색) → 환경 레이어(구름·비·눈·안개, 창 뒤)
→ L1 방 껍데기(창은 구멍) → 창 유리 물방울(비) → L2 소품 → 환경 밝기 어둠 → L3 창 빛·실내 조명.
"""
import math
import random

from PIL import Image

from artkit import EXPORT, PIX, canvas, palette_check, pfill, save
from prop import DOT, _dot, _level_hit, dark_overlay, glow
import room128_home as HM

W, H, FLOOR, CEIL = HM.W, HM.H, HM.FLOOR, HM.CEIL
WX, WY, WW, WH = 36, 64, 208, 144          # 창 구멍(L1)
FR = 90                                    # 3초 반복(30 FPS)

# 시간대·날씨 프리셋: 하늘 위/아래 색, 실내 어둠 단계, 창 빛(색, 단계), 입자 색
PRESET = {
    ("day", "clear"):   dict(sky=("b", "C"), amb=0,   beam=("W", 1),   p="C"),
    ("day", "cloudy"):  dict(sky=("2", "3"), amb=0.5, beam=("3", 0.5), p="W"),
    ("day", "rain"):    dict(sky=("4", "2"), amb=1,   beam=("3", 0.5), p="C"),
    ("day", "snow"):    dict(sky=("3", "U"), amb=0.5, beam=("W", 1),   p="W"),
    ("day", "fog"):     dict(sky=("3", "U"), amb=1,   beam=("W", 0.5), p="W"),
    ("night", "clear"): dict(sky=("a", "e"), amb=2,   beam=("b", 0.5), p="3"),
    ("night", "cloudy"): dict(sky=("e", "J"), amb=2,  beam=(None, 0),  p="H"),
    ("night", "rain"):  dict(sky=("e", "J"), amb=2,   beam=(None, 0),  p="3"),
    ("night", "snow"):  dict(sky=("J", "H"), amb=2,   beam=("U", 0.5), p="W"),
    ("night", "fog"):   dict(sky=("J", "H"), amb=2,   beam=(None, 0),  p="U"),
}
DAY_RAMP = ["4", "2", "3"]                 # 낮의 도시: 밝기 순서대로 다시 칠함(불 켜진 창 없음)


def city_view(time):
    bg = Image.new("RGBA", (640, 360), (0, 0, 0, 0))
    for n in ("city_layer1_far", "city_layer2_mid", "city_layer5_near"):
        bg.alpha_composite(Image.open(EXPORT / f"backgrounds/{n}.png").convert("RGBA"))
    v = bg.crop((448, 180, 448 + 52, 180 + 36)).resize((WW, WH), Image.NEAREST)   # 위 하늘·아래 건물 구역
    if time == "day":
        px = v.load()
        for y in range(WH):
            for x in range(WW):
                r, g, b, a = px[x, y]
                if a:
                    lum = (r * 3 + g * 6 + b) / 10
                    px[x, y] = PIX[DAY_RAMP[0 if lum < 50 else (1 if lum < 90 else 2)]]
    return v


def sky(top, bottom):
    im = canvas(W, H, PIX[top])
    # 가로 띠 그러데이션: 위 색 → 경계 2줄(1/2 디더) → 아래 색. 점 디더를 넓게 깔면 눈처럼 보여 띠로 나눈다
    mid = WY + WH // 3
    pfill(im, 0, mid + 2 * DOT, W - 1, H - 1, bottom)
    for gy in range(mid // DOT, mid // DOT + 2):
        for gx in range(0, W // DOT):
            if _level_hit(gx, gy, 3):
                _dot(im, gx * DOT, gy * DOT, bottom)
    return im


def rng(seed):
    return random.Random(seed)


def clouds(im, f, ch, dark):
    r = rng(1)
    span = WW + 90                                      # 3px/프레임 × 90 = 270 → 한 바퀴
    for i in range(6):
        cx0, cy, rw, rh = r.randrange(0, span), WY + r.randrange(4, 60), r.randrange(30, 54), r.randrange(8, 14)
        cx = WX - 45 + (cx0 + 3 * f) % span
        for gy in range((cy - rh) // DOT, (cy + rh) // DOT):
            for gx in range((cx - rw) // DOT, (cx + rw) // DOT):
                d = ((gx * DOT - cx) / rw) ** 2 + ((gy * DOT - cy) / rh) ** 2
                if d < 0.55:
                    _dot(im, gx * DOT, gy * DOT, ch if gy * DOT < cy else dark)
                elif d < 1 and _level_hit(gx, gy, 2):
                    _dot(im, gx * DOT, gy * DOT, dark)


def rain(im, f, ch, n=70):
    r = rng(2)
    span = 180                                          # 24px/프레임 × 90 = 12바퀴
    for i in range(n):
        x, y0 = WX + r.randrange(0, WW + 20), r.randrange(0, span)
        y = WY - 20 + (y0 + 24 * f) % span
        for k in range(0, 16, 2):                       # 2px 굵기, 길이 16, 바람에 살짝 기울어짐
            _dot(im, x - k // 4, y + k, ch)


def snow(im, f, ch, n=60):
    r = rng(3)
    span = 180
    for i in range(n):
        x0, y0 = WX + r.randrange(0, WW), r.randrange(0, span)
        spd, ph, big = r.choice((2, 3)), r.random(), r.random() < 0.35
        y = WY - 10 + (y0 + spd * f) % span
        x = x0 + round(6 * math.sin(2 * math.pi * (f / FR * 2 + ph)))
        _dot(im, x, y, ch)
        if big:
            _dot(im, x + 2, y, ch); _dot(im, x, y + 2, ch); _dot(im, x + 2, y + 2, ch)


def fog(im, f, ch, ch2):
    for gy in range(WY // DOT, (WY + WH) // DOT):
        y = gy * DOT
        band = 0.5 + 0.5 * math.sin(2 * math.pi * (y - WY) / 72)
        for gx in range(0, W // DOT):
            x = gx * DOT
            wave = 0.5 + 0.5 * math.sin(2 * math.pi * ((x + 2 * f) / 180))      # 2px/프레임 × 90 = 180 한 바퀴
            v = band * 0.6 + wave * 0.4 + (y - WY) / WH * 0.5
            level = 3 if v > 0.95 else (2 if v > 0.7 else (1 if v > 0.45 else 0))
            if _level_hit(gx, gy, level):
                _dot(im, x, y, ch if level < 3 else ch2)


def glass_drips(im, f, ch):
    r = rng(4)
    for i in range(8):
        x, y0, ln = WX + 6 + r.randrange(0, WW - 12), r.randrange(0, 150), r.randrange(6, 14)
        y = WY + (y0 + 5 * f) % 150 - 4                  # 5px/프레임 × 90 = 450 = 3바퀴
        for k in range(0, ln, 2):
            if WY <= y + k < WY + WH:
                _dot(im, x, y + k, ch)


def window_beam(ch, level):
    """창으로 들어오는 빛(L3): 창 아래부터 바닥까지 오른쪽 아래로 기운 띠."""
    im = canvas(W, H)
    if not ch:
        return im
    top = WY + WH + 20
    for gy in range(top // DOT, FLOOR // DOT):
        y = gy * DOT
        off = int((y - top) * 0.7)
        for gx in range((WX + off) // DOT, (WX + WW + off) // DOT):
            lv = level if y < FLOOR - 6 else min(2, level * 2)
            if _level_hit(gx, gy, lv):
                _dot(im, gx * DOT, y, ch)
    return im


def shell_with_hole():
    sh = HM.shell()
    pfill(sh, WX, WY, WX + WW - 1, WY + WH - 1, ".")     # 창 구멍(투명)
    for x in range(WX, WX + WW):
        for y in range(WY, WY + WH):
            sh.putpixel((x, y), (0, 0, 0, 0))
    bars = canvas(W, H)
    pfill(bars, WX + 102, WY, WX + 105, WY + WH - 1, "2"); pfill(bars, WX, WY + 70, WX + WW - 1, WY + 73, "2")
    return sh, bars


def frames(time, weather):
    P = PRESET[(time, weather)]
    sh, bars = shell_with_hole()
    pr = HM.props()
    base = sky(*P["sky"]); base.alpha_composite(city_view(time), (WX, WY))
    amb = dark_overlay(W, H, CEIL, FLOOR, "a", P["amb"]) if P["amb"] else canvas(W, H)
    for x in range(WX, WX + WW):                          # 실내 어둠은 창밖(유리 너머)에 그리지 않는다
        for y in range(WY, WY + WH):
            amb.putpixel((x, y), (0, 0, 0, 0))
    lights = [window_beam(*P["beam"]), glow(W, H, 417, 214, 130, 70, "b", y_min=CEIL, y_max=FLOOR)]
    if time == "night":
        lights.append(glow(W, H, 470, 240, 70, 40, "h", y_min=CEIL, y_max=FLOOR))   # 밤에는 스탠드 켬
    out = []
    for f in range(FR):
        env = canvas(W, H)
        if weather in ("cloudy", "rain"):
            clouds(env, f, P["sky"][1], P["sky"][0])
        if weather == "rain":
            rain(env, f, P["p"])
        if weather == "snow":
            snow(env, f, P["p"])
        if weather == "fog":
            fog(env, f, P["p"], "W" if time == "day" else "U")
        im = base.copy(); im.alpha_composite(env); im.alpha_composite(sh); im.alpha_composite(bars)
        if weather == "rain":
            drips = canvas(W, H); glass_drips(drips, f, "C" if time == "day" else "3"); im.alpha_composite(drips)
        im.alpha_composite(pr); im.alpha_composite(amb)
        for l in lights:
            im.alpha_composite(l)
        out.append(im)
    return out


def build():
    made = []
    board = Image.new("RGBA", (5 * 328, 2 * 300), (40, 40, 40, 255))
    for row, time in enumerate(("day", "night")):
        for col, weather in enumerate(("clear", "cloudy", "rain", "snow", "fog")):
            fs = frames(time, weather)
            assert not palette_check(fs[0]), (time, weather)
            path = EXPORT / f"preview/env/env_{time}_{weather}.gif"
            path.parent.mkdir(parents=True, exist_ok=True)
            pal = [f.convert("RGB").quantize(64, method=Image.Quantize.FASTOCTREE) for f in fs]
            pal[0].save(path, save_all=True, append_images=pal[1:], duration=33, loop=0, optimize=False)
            made.append(path)
            board.alpha_composite(fs[20].crop((0, 40, 320, 340)), (col * 328, row * 300))
    made.append(save(board, "preview/env_board.png", {"status": "환경 이펙트 10조합(20번째 프레임) — 위 낮, 아래 밤 / 맑음·흐림·비·눈·안개"}))
    return made
