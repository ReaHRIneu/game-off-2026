"""W-033 장치 그림 보강(D-123 ①) — 1-4 셔터 전용 그림, 문·셔터·해치·레이저 전환 중간 프레임 2장.

- 롤 셔터(복도용) 닫힘·열림 + 설계도판: 챕터 0 계단 셔터(obj128_ch0 shutter)와 같은 계열(레일·상자·슬랫·경고 띠). 복도 천장선~바닥선을 채운다(160×324).
- 전환 중간 프레임(vfx-direction §2-2): 닫힘→열림의 1/3·2/3 지점. 설계도판(bp_*_t1·t2)은 Shift를 누르는 순간 겹침 그림 자리에 0.07초(2틱) 재생한다.
  레이저 차단막(현실 그림, 설계도판 없음)은 실선→점선 2단계(blocker_t1 = 빔 3/4, blocker_t2 = 빔 1/4).
규칙: 광원 왼쪽 위, 명암 3단, 바깥 외곽선 2px(K), 안티앨리어싱 없음, 공용 팔레트만, 2px 디더 점. 4배 규격(D-075).
실행: python -I tools/art/build_art.py obj128_w033   (rooms128·obj128_ch0 그림이 먼저 있어야 미리보기가 만들어진다)
"""
from PIL import Image

from artkit import EXPORT, PIX, canvas, palette_check, pfill, save
from obj128_ch0 import CEIL, FLOOR, cells, hazard, rivet, shutter as stair_shutter, to_blueprint
from pixfont import draw_text
from prop import box3

ROLL_H = 324                  # 롤 셔터 높이: 천장선(28)~바닥선(352)


# ---------- 롤 셔터(진행도 p: 0 = 닫힘, 1 = 열림) ----------

def roll_shutter(H, p, label="Y"):
    """obj128_ch0.shutter()를 높이·진행도 매개변수로 일반화한 것. H=280·p=0이면 원본 닫힘 그림과 같다(build()가 확인)."""
    W = 160
    im = canvas(W, H)
    for x0 in (0, 146):                                                  # 양옆 레일
        box3(im, x0, 0, x0 + 13, H - 1, "3", "W", "2", bevel=2)
        pfill(im, x0 + (9 if x0 == 0 else 2), 48, x0 + (10 if x0 == 0 else 3), H - 16, "K")
        box3(im, x0, H - 14, x0 + 13, H - 1, "2", "3", "K", bevel=2)
    box3(im, 0, 0, W - 1, 44, "3", "W", "2", bevel=4)                    # 셔터 상자
    for x in range(22, 140, 14):
        pfill(im, x, 14, x + 7, 15, "K")
        pfill(im, x, 24, x + 7, 25, "K")
    for x in (6, 150):
        rivet(im, x, 6); rivet(im, x, 36)
    pfill(im, 62, 32, 98, 40, "K"); pfill(im, 64, 34, 96, 38, label)     # 표지 띠(상태 램프 색)
    if p >= 1.0:
        cells(im, 14, 46, 145, 53, "a", 4)                               # 열림: 상자 아래 그늘만
        return im
    y_end = H - round((H - 46) * p / 4) * 4                              # 커튼 아래 끝(4px 단위)
    bar_top = y_end - 30
    slats = canvas(W, H)
    for i, y in enumerate(range(46, 250 + (H - 280), 12)):               # 금속 슬랫(원본과 같은 12px 주기)
        pfill(slats, 14, y, 145, y + 11, "2")
        pfill(slats, 14, y, 145, y + 1, "3")
        pfill(slats, 14, y + 7, 145, y + 9, "4")
        pfill(slats, 14, y + 10, 145, y + 11, "K")
        pfill(slats, 14, y + 2, 21, y + 6, "3")
        for x in range(30 + (i * 22) % 40, 142, 46):
            pfill(slats, x, y + 4, x + 5, y + 5, "4")
    if bar_top > 46:
        im.alpha_composite(slats.crop((0, 46, W, bar_top)), (0, 46))
    box3(im, 14, bar_top, 145, y_end - 1, "3", "W", "2", bevel=2)        # 아래 막대
    pfill(im, 16, bar_top + 6, 143, bar_top + 24, "K")
    hazard(im, 18, bar_top + 8, 141, bar_top + 22)                       # 아래 끝 경고 띠
    if bar_top - 24 >= 48:
        box3(im, 64, bar_top - 24, 96, bar_top - 4, "2", "3", "K", bevel=2)   # 잠금 손잡이
        pfill(im, 76, bar_top - 18, 83, bar_top - 9, "K"); pfill(im, 78, bar_top - 16, 81, bar_top - 11, "h")
    cells(im, 14, 46, 145, 56, "a", 4)
    return im


# ---------- 바닥 해치 전환 중간(q: 열린 정도 1/3·2/3) ----------

def hatch_mid(q):
    """덮개가 뒤 경첩을 축으로 들리는 중. 1/3 = 얇게 비스듬히 들려 앞쪽 구멍이 드러남, 2/3 = 거의 세워져 구멍이 다 보임."""
    W, H = 160, 40
    im = canvas(W, H)
    pfill(im, 4, 24, 155, H - 1, "K")                                    # 구멍 테두리
    pfill(im, 8, 28, 151, H - 1, "a")                                    # 검은 구멍
    cells(im, 8, 28, 151, 33, "K", 3)
    pfill(im, 4, 24, 155, 25, "3"); pfill(im, 4, 24, 5, H - 1, "3")
    if q > 0.5:
        for x0 in (44, 108):                                             # 사다리 끝(거의 열렸을 때만 보임)
            box3(im, x0, 8, x0 + 5, H - 1, "3", "W", "2", bevel=2)
        for y in (12, 26):
            pfill(im, 44, y, 113, y + 3, "K"); pfill(im, 46, y + 1, 111, y + 2, "3")
    for x in (14, 134):                                                  # 경첩(뒤쪽)
        box3(im, x, 20, x + 11, 25, "2", "3", "K", bevel=2)
    if q <= 0.5:
        box3(im, 5, 21, 154, 28, "3", "W", "2", bevel=2)                 # 앞이 살짝 들린 덮개(얇게 비스듬히)
        for x in range(14, 148, 10):
            pfill(im, x, 25, x + 5, 25, "4")
        for x in (8, 150):
            rivet(im, x, 23)
    else:
        box3(im, 8, 9, 151, 23, "2", "3", "4", bevel=4)                  # 거의 세운 덮개(안쪽 면)
        pfill(im, 14, 14, 145, 14, "K"); pfill(im, 14, 18, 145, 18, "K")
        for x in (12, 144):
            rivet(im, x, 11)
    return im


# ---------- 레이저 차단막 전환 중간(빔이 끊어지는 정도) ----------

def blocker_mid(on_len, period):
    """obj128_ch0.blocker()의 빔을 점선으로 바꾼 그림. 발사기·수신기는 그대로, 빔과 둘레 빛은 on_len 만큼만 칠한다(period 마다 반복)."""
    W, H = 640, 384
    im = canvas(W, H)
    y0, y1 = CEIL + 34, FLOOR - 28
    xs = [20 + 40 * k for k in range(16)]

    def lit(xc, y):                                                      # 빔마다 위상을 달리해 한 줄로 끊어지지 않게
        return (y - y0 + (xc // 40) * 12) % period < on_len

    for xc in xs:                                                        # 빔 둘레 빛(2px 디더 점): 켜진 마디 근처만
        for gy in range((CEIL + 34) // 2, (FLOOR - 28) // 2):
            if not lit(xc, gy * 2):
                continue
            for off, ch, lv in ((-8, "R", 0), (6, "R", 0), (-12, "r", 1), (10, "r", 1)):
                hit = (gy % 2 == 0) if lv == 0 else (gy % 4 == (xc // 40) % 4)
                if hit:
                    x = xc + off
                    pfill(im, x, gy * 2, x + 1, gy * 2 + 1, ch)
    for xc in xs:                                                        # 빔(4px): 켜진 마디만
        for y in range(y0, y1 + 1):
            if lit(xc, y):
                pfill(im, xc - 2, y, xc + 1, y, "R")
                pfill(im, xc - 1, y, xc, y, "W")
    for xc in xs:                                                        # 천장 발사기 · 바닥 수신기(원본과 같음)
        box3(im, xc - 14, CEIL, xc + 14, CEIL + 22, "3", "W", "2", bevel=2)
        box3(im, xc - 7, CEIL + 22, xc + 7, CEIL + 31, "2", "3", "K", bevel=2)
        pfill(im, xc - 2, CEIL + 31, xc + 1, CEIL + 33, "R")
        rivet(im, xc - 10, CEIL + 5); rivet(im, xc + 8, CEIL + 5)
        box3(im, xc - 14, FLOOR - 22, xc + 14, FLOOR - 1, "3", "W", "2", bevel=2)
        box3(im, xc - 7, FLOOR - 31, xc + 7, FLOOR - 22, "2", "3", "K", bevel=2)
        pfill(im, xc - 2, FLOOR - 33, xc + 1, FLOOR - 31, "R")
        rivet(im, xc - 10, FLOOR - 17); rivet(im, xc + 8, FLOOR - 17)
    return im


# ---------- 빌드 ----------

def _same(a, b):
    return a.size == b.size and list(a.getdata()) == list(b.getdata())


def build():
    # 일반화한 롤 셔터가 원본 계단 셔터(닫힘·열림)와 같은 그림을 만드는지 확인한다(같은 계열 보장)
    assert _same(roll_shutter(280, 0.0), stair_shutter(True)), "roll_shutter(280, 0)이 계단 셔터 닫힘과 다름"
    assert _same(roll_shutter(280, 1.0), stair_shutter(False)), "roll_shutter(280, 1)이 계단 셔터 열림과 다름"

    pieces = {}
    pieces["rollshutter_closed"] = roll_shutter(ROLL_H, 0.0, "R")        # 닫힘 = 빨간 램프
    pieces["rollshutter_open"] = roll_shutter(ROLL_H, 1.0, "G")          # 열림 = 민트 램프
    pieces["bp_rollshutter_closed"] = to_blueprint(pieces["rollshutter_closed"])
    pieces["bp_rollshutter_open"] = to_blueprint(pieces["rollshutter_open"])
    # 전환 중간 프레임: t1 = 1/3 열림, t2 = 2/3 열림(설계도판). 닫힘→열림 순서. 열림→닫힘이면 엔진이 t2, t1 순으로 재생한다
    pieces["bp_shutter_t1"] = to_blueprint(roll_shutter(280, 1 / 3))
    pieces["bp_shutter_t2"] = to_blueprint(roll_shutter(280, 2 / 3))
    pieces["bp_rollshutter_t1"] = to_blueprint(roll_shutter(ROLL_H, 1 / 3))
    pieces["bp_rollshutter_t2"] = to_blueprint(roll_shutter(ROLL_H, 2 / 3))
    pieces["bp_hatch_t1"] = to_blueprint(hatch_mid(1 / 3))
    pieces["bp_hatch_t2"] = to_blueprint(hatch_mid(2 / 3))
    # 레이저: t1 = 빔 3/4(실선에 가까움), t2 = 빔 1/4(점선). 켜짐→꺼짐 순서. 꺼짐→켜짐이면 t2, t1 순
    pieces["blocker_t1"] = blocker_mid(24, 32)
    pieces["blocker_t2"] = blocker_mid(8, 32)

    made = []
    for name, im in pieces.items():
        assert not palette_check(im), name
        layer = "L3.5" if name.startswith("blocker") else "L4"
        meta = {"status": "시안", "layer": layer, "size": list(im.size), "px_per_logical": 4}
        made.append(save(im, f"objects128/{name}.png", meta))
    made += preview(pieces)
    return made


# ---------- 미리보기 ----------

def _room(key):
    d = EXPORT / "rooms128" / key
    comp = canvas(640, 384, PIX["e"])
    comp.alpha_composite(Image.open(d / "L1_shell.png").convert("RGBA"))
    comp.alpha_composite(Image.open(d / "L2_props.png").convert("RGBA"))
    for p in sorted(d.glob("L3_light_*.png")):
        comp.alpha_composite(Image.open(p).convert("RGBA"))
    return comp


def preview(p):
    real_closed, real_open = p["rollshutter_closed"], p["rollshutter_open"]
    stair_c, stair_o = stair_shutter(True), stair_shutter(False)
    hatch_c = Image.open(EXPORT / "objects128/hatch_closed.png").convert("RGBA")
    hatch_o = Image.open(EXPORT / "objects128/hatch_open.png").convert("RGBA")
    blocker = Image.open(EXPORT / "objects128/blocker_on.png").convert("RGBA")
    bpsec = Image.open(EXPORT / "objects128/bp_sector.png").convert("RGBA")
    cells_ = []

    def put(room, items, bp=None, sector=False):
        c = _room(room)
        for im, x in items:
            c.alpha_composite(im, (x, FLOOR - im.height))
        if sector:
            c.alpha_composite(bpsec)
        for im, x in bp or []:
            c.alpha_composite(im, (x - 4, FLOOR + 4 - im.height))
        return c

    # 1-4 3F 복도: 셔터 x = drawX 80 논리 = 320px(막는 선 x 100 논리가 셔터 가운데)
    rx = 320
    cells_.append((put("ot_corridor", [(real_closed, rx)]), "1-4 3F rollshutter closed"))
    cells_.append((put("ot_corridor", [(real_open, rx)]), "1-4 3F rollshutter open"))
    cells_.append((put("ot_corridor", [(real_closed, rx)], [(p["bp_rollshutter_t1"], rx)], True), "press: t1 (bp over closed)"))
    cells_.append((put("ot_corridor", [(real_closed, rx)], [(p["bp_rollshutter_t2"], rx)], True), "press: t2"))
    cells_.append((put("ot_corridor", [(real_closed, rx)], [(p["bp_rollshutter_open"], rx)], True), "hold: bp open"))
    cells_.append((put("ot_corridor", [(real_open, rx)], [(p["bp_rollshutter_closed"], rx)], True), "hold: bp closed (reality open)"))
    cells_.append((put("ot_stairwell", [(stair_c, 240)], [(p["bp_shutter_t1"], 240)], True), "stairs shutter t1"))
    cells_.append((put("ot_stairwell", [(stair_c, 240)], [(p["bp_shutter_t2"], 240)], True), "stairs shutter t2"))
    cells_.append((put("corridor", [(hatch_c, 216)], [(p["bp_hatch_t1"], 216)], True), "hatch t1"))
    cells_.append((put("corridor", [(hatch_c, 216)], [(p["bp_hatch_t2"], 216)], True), "hatch t2"))
    cells_.append((put("corridor", [(blocker, 0)]), "laser on"))
    cells_.append((put("corridor", [(p["blocker_t1"], 0)]), "laser t1 (3/4)"))
    cells_.append((put("corridor", [(p["blocker_t2"], 0)]), "laser t2 (1/4)"))
    cells_.append((put("corridor", []), "laser off"))

    W, H, gap, cols = 640, 384, 16, 2
    rows = (len(cells_) + cols - 1) // cols
    sheet = canvas(cols * (W + gap) + gap, rows * (H + gap + 12) + gap, PIX["0"])
    for i, (im, label) in enumerate(cells_):
        x = gap + (i % cols) * (W + gap)
        y = gap + (i // cols) * (H + gap + 12)
        sheet.alpha_composite(im, (x, y + 12))
        draw_text(sheet, x, y + 2, label, "W")
    return [save(sheet, "preview/objects128_w033_onrooms.png", {"status": "미리보기"})]
