"""챕터 0 상호작용 오브젝트(L4) 128px 그림 — W-012, D-035·D-073·D-075·D-079.

0-1·0-2에서 쓰는 셔터·해치·엘리베이터·레이저 차단막·중계기와 그 설계도(bp_*) 그림, 설계도 섹터 겹침(bp_sector).
크기는 예전 32px 논리 크기의 4배(엘리베이터만 높이 80→72 논리, 아래 주석). 그림 아래 끝 = 바닥선(엔진이 맞춘다).
규칙: 광원 왼쪽 위, 명암 3단, 바깥 외곽선 2px(K), 안티앨리어싱 없음, 공용 팔레트만, 빛은 2px 디더 점.
실행: python -I tools/art/build_art.py obj128_ch0
"""
from PIL import Image

from artkit import PIX, canvas, draw_ellipse, draw_line, draw_poly, palette_check, pfill, save
from pixfont import draw_text, text_width
from prop import box3, dither_fill

CEIL, FLOOR = 28, 352        # 방 그림(rooms128)의 천장선·바닥선 y


# ---------- 공통 부품 ----------

def rivet(im, x, y):
    """2×2 리벳(밝은 점)."""
    pfill(im, x, y, x + 1, y + 1, "W")


def hazard(im, x0, y0, x1, y1, period=16):
    """노랑·남색 45도 경고 줄무늬(안티앨리어싱 없음)."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if x < im.width and y < im.height:
                im.putpixel((x, y), PIX["Y" if ((x - y) // (period // 2)) % 2 == 0 else "K"])


def big_text(im, x, y, s, ch, k=2):
    """5×7 글꼴을 k배 확대해 그린다(그림 속 표지 글자)."""
    t = canvas(text_width(s), 7)
    draw_text(t, 0, 0, s, ch)
    t = t.resize((t.width * k, t.height * k), Image.NEAREST)
    im.alpha_composite(t, (x, y))


def cells(im, x0, y0, x1, y1, ch, level=2, phase=0):
    """2×2px 점 디더. level 2 = 1/2(체커), 3 = 1/3, 4 = 1/4 칸."""
    for gy in range(y0 // 2, y1 // 2 + 1):
        for gx in range(x0 // 2, x1 // 2 + 1):
            if level == 2:
                hit = (gx + gy + phase) % 2 == 0
            elif level == 3:
                hit = (gx + 2 * gy + phase) % 3 == 0
            else:
                hit = gx % 2 == 0 and gy % 2 == 0
            if hit:
                pfill(im, gx * 2, gy * 2, gx * 2 + 1, gy * 2 + 1, ch)


def _near_non_k(rp, x, y, w, h):
    """K 픽셀이 선(경계 2px 안)인지: 가까운 곳에 K가 아닌 픽셀이나 빈 곳이 있으면 선. 넓은 K 면 안쪽은 선이 아니다."""
    for dx, dy in ((2, 0), (-2, 0), (0, 2), (0, -2), (1, 0), (-1, 0), (0, 1), (0, -1)):
        nx, ny = x + dx, y + dy
        if not (0 <= nx < w and 0 <= ny < h) or rp[nx, ny] != PIX["K"]:
            return True
    return False


def to_blueprint(real):
    """현실 그림 -> 설계도 그림(사방 4px 여유 = 논리 1px). K 외곽·내부선은 하늘색(C) 2px 선, 면은 청사진 파랑 2px 디더.
    비어 있는 곳은 투명으로 둬 겹쳐도 아래 현실이 비쳐 보인다."""
    w, h = real.size
    out = canvas(w + 8, h + 8)
    rp = real.load()
    op = out.load()
    for y in range(h):
        for x in range(w):
            r = rp[x, y]
            if r[3] == 0:
                continue
            if r == PIX["K"] and _near_non_k(rp, x, y, w, h):
                op[x + 4, y + 4] = PIX["C"]
            elif ((x + 4) // 2 + (y + 4) // 2) % 3 == 0:
                op[x + 4, y + 4] = PIX["b" if ((y + 4) // 2) % 3 == 0 else "B"]
    return out


# ---------- 롤 셔터 160×280 (계단실 입구) ----------

def stair_pictogram(im, x0, y0):
    """비상계단 표지판(초록 바탕, 흰 계단 그림) 44×34. 계단 입구임을 알린다."""
    box3(im, x0, y0, x0 + 43, y0 + 33, "g", "G", "K", bevel=2)
    pfill(im, x0 + 4, y0 + 4, x0 + 39, y0 + 29, "g")
    for i in range(4):                                                   # 계단 4단(왼쪽 아래 → 오른쪽 위)
        sx = x0 + 6 + i * 8
        sy = y0 + 25 - i * 6
        pfill(im, sx, sy, sx + 9, sy + 1, "W")                           # 디딤판
        pfill(im, sx + 8, sy - 6, sx + 9, sy + 1, "W")                   # 챌판
    pfill(im, x0 + 6, y0 + 26, x0 + 37, y0 + 27, "W")                    # 바닥선


def stair_gate(p):
    """계단 입구 접이식 철창 160×280(D-139 [제안]). p: 0 = 닫힘(철창이 계단 앞을 가로막음), 1 = 열림(철창이 왼쪽 기둥에 접힘).
    문틀·상자가 없어 엘리베이터 문으로 보이지 않고, 철창 틈으로 뒤의 계단이 보인다. 그림 아래 끝 = 바닥선."""
    W, H = 160, 280
    im = canvas(W, H)
    top = 112                                                            # 철창 윗선(사람 키보다 조금 높음)
    xe = round(14 + 10 + (1 - p) * (146 - 24))                           # 철창 오른쪽 끝(접힐수록 왼쪽)
    # 바닥 레일(철창이 미끄러지는 홈) — 펼쳐진 만큼만. 열림에서는 없어 문턱·문틀로 보이지 않는다
    if p < 1.0:
        pfill(im, 12, H - 6, xe, H - 3, "2"); pfill(im, 12, H - 3, xe, H - 1, "K")
        pfill(im, 12, H - 6, xe, H - 6, "3")
    # 왼쪽 기둥 + 표지판
    box3(im, 0, 36, 13, H - 1, "3", "W", "2", bevel=2)
    pfill(im, 9, 48, 10, H - 16, "K")
    box3(im, 0, H - 14, 15, H - 1, "2", "3", "K", bevel=2)
    stair_pictogram(im, 0, 0)
    # 오른쪽 받이(잠금 고리) — 닫힘에서만. 열림·전환 중에는 없어 계단 앞이 트여 보인다
    if p <= 0.0:
        box3(im, 148, top, 159, H - 1, "3", "W", "2", bevel=2)
        box3(im, 146, H - 14, 159, H - 1, "2", "3", "K", bevel=2)
        pfill(im, 150, top + 40, 157, top + 47, "K"); pfill(im, 152, top + 42, 155, top + 45, "a")   # 걸쇠 구멍
    # 철창: x 14 ~ xe. 접힐수록 막대 간격이 좁아진다
    n = 9
    bars = [round(14 + i * (xe - 14) / (n - 1)) for i in range(n)]
    gy0, gy1 = top + 4, H - 10
    mid = (gy0 + gy1) // 2
    for a, b in zip(bars, bars[1:]):                                     # X자 연결대(마름모 2줄)
        if b - a < 3:
            continue
        for (ya, yb) in ((gy0, mid), (mid, gy1)):
            draw_line(im, [(a, ya), (b, yb)], "K", 3); draw_line(im, [(a, yb), (b, ya)], "K", 3)
            draw_line(im, [(a, ya), (b, yb)], "3", 1); draw_line(im, [(a, yb), (b, ya)], "3", 1)
    for x in bars:                                                       # 세로 막대
        pfill(im, x - 1, top, x + 2, H - 8, "K")
        pfill(im, x, top + 1, x, H - 9, "W"); pfill(im, x + 1, top + 1, x + 1, H - 9, "2")
    for y in (top, gy1):                                                 # 위·아래 가로대
        pfill(im, 14, y - 1, xe + 2, y + 3, "K"); pfill(im, 14, y, xe + 1, y + 1, "3"); pfill(im, 14, y + 2, xe + 1, y + 2, "2")
    if p <= 0.0:
        # 닫힘: 자물쇠 + 출입 금지 경고판
        box3(im, 136, mid - 6, 147, mid + 8, "2", "3", "K", bevel=2)
        draw_ellipse(im, (137, mid - 14, 146, mid - 4), "K", 2)
        pfill(im, 140, mid, 143, mid + 3, "Y")
        box3(im, 52, mid - 14, 108, mid + 14, "3", "W", "K", bevel=2)
        pfill(im, 56, mid - 10, 104, mid + 10, "K")
        hazard(im, 58, mid - 8, 102, mid + 8)
    return im


def shutter(closed):
    """계단 셔터 키(shutter_open·closed)의 그림. D-139로 문틀 셔터에서 접이식 철창(stair_gate)으로 바꿈."""
    return stair_gate(0.0 if closed else 1.0)


def shutter_door(closed):
    """예전 계단 셔터(문틀 + 셔터 상자 + 슬랫). D-139 전 그림 — 되돌림·비교용으로만 남긴다(빌드에서 쓰지 않음)."""
    W, H = 160, 280
    im = canvas(W, H)
    for x0 in (0, 146):                                                  # 양옆 레일
        box3(im, x0, 0, x0 + 13, H - 1, "3", "W", "2", bevel=2)
        pfill(im, x0 + (9 if x0 == 0 else 2), 48, x0 + (10 if x0 == 0 else 3), H - 16, "K")   # 안쪽 홈
        box3(im, x0, H - 14, x0 + 13, H - 1, "2", "3", "K", bevel=2)     # 발판
    box3(im, 0, 0, W - 1, 44, "3", "W", "2", bevel=4)                    # 셔터 상자
    for x in range(22, 140, 14):                                         # 환기 홈
        pfill(im, x, 14, x + 7, 15, "K")
        pfill(im, x, 24, x + 7, 25, "K")
    for x in (6, 150):
        rivet(im, x, 6); rivet(im, x, 36)
    pfill(im, 62, 32, 98, 40, "K"); pfill(im, 64, 34, 96, 38, "Y")      # 표지 띠
    if not closed:
        cells(im, 14, 46, 145, 53, "a", 4)                               # 상자 아래 그늘
        return im
    for i, y in enumerate(range(46, 250, 12)):                           # 금속 슬랫
        pfill(im, 14, y, 145, y + 11, "2")
        pfill(im, 14, y, 145, y + 1, "3")
        pfill(im, 14, y + 7, 145, y + 9, "4")
        pfill(im, 14, y + 10, 145, y + 11, "K")
        pfill(im, 14, y + 2, 21, y + 6, "3")                             # 왼쪽 밝음
        for x in range(30 + (i * 22) % 40, 142, 46):                     # 긁힘
            pfill(im, x, y + 4, x + 5, y + 5, "4")
    box3(im, 14, 250, 145, H - 1, "3", "W", "2", bevel=2)                # 아래 막대
    pfill(im, 16, 256, 143, 274, "K")
    hazard(im, 18, 258, 141, 272)                                        # 아래 끝 경고 띠
    box3(im, 64, 226, 96, 246, "2", "3", "K", bevel=2)                   # 잠금 손잡이
    pfill(im, 76, 232, 83, 241, "K"); pfill(im, 78, 234, 81, 239, "h")
    cells(im, 14, 46, 145, 56, "a", 4)
    return im


# ---------- 바닥 해치 160×40 ----------

def hatch(opened):
    W, H = 160, 40
    im = canvas(W, H)
    if not opened:
        box3(im, 4, 24, 155, H - 1, "3", "W", "2", bevel=2)              # 덮개
        pfill(im, 10, 29, 149, 29, "2")
        for x in range(14, 148, 10):                                     # 미끄럼 홈
            pfill(im, x, 31, x + 5, 32, "4")
        for x in (8, 150):
            rivet(im, x, 27); rivet(im, x, 34)
        box3(im, 66, 27, 92, 33, "K", "2", "K", bevel=2)                 # 손잡이 홈
        pfill(im, 72, 29, 86, 30, "h")
        for x in (14, 134):                                              # 경첩
            box3(im, x, 20, x + 11, 25, "2", "3", "K", bevel=2)
        pfill(im, 4, H - 3, 155, H - 2, "y")                             # 앞 모서리 경고선
        return im
    box3(im, 8, 0, 151, 22, "2", "3", "4", bevel=4)                      # 젖혀 세운 덮개(안쪽 면)
    pfill(im, 14, 6, 145, 7, "K"); pfill(im, 14, 15, 145, 16, "K")
    for x in (12, 144):
        rivet(im, x, 4); rivet(im, x, 17)
    pfill(im, 4, 24, 155, H - 1, "K")                                    # 구멍 테두리
    pfill(im, 8, 28, 151, H - 1, "a")                                    # 검은 구멍
    cells(im, 8, 28, 151, 33, "K", 3)
    for x0 in (44, 108):                                                 # 사다리 끝
        box3(im, x0, 8, x0 + 5, H - 1, "3", "W", "2", bevel=2)
    for y in (12, 26):
        pfill(im, 44, y, 113, y + 3, "K"); pfill(im, 46, y + 1, 111, y + 2, "3")
    pfill(im, 4, 24, 155, 25, "3"); pfill(im, 4, 24, 5, H - 1, "3")     # 구멍 테두리 밝음
    return im


# ---------- 엘리베이터 문·칸 224×288 ----------
# 높이는 예전 80 논리(320px)에서 72 논리(288px)로 줄였다: 방 그림의 층 표시등(y 40~64)이 가려지지 않게 문 위끝을 y 64에 둔다.
# 가로 224px는 방 그림 승강로 문틀(x 204~436)과 같은 자리(drawX 52 = x 208)에 맞춘다.

def elevator(running):
    W, H = 224, 288
    im = canvas(W, H)
    for x0 in (0, 212):                                                  # 문틀(방 그림 문틀과 같은 색)
        box3(im, x0, 0, x0 + 11, H - 1, "3", "W", "2", bevel=2)
    box3(im, 12, 0, 211, 35, "4", "2", "K", bevel=2)                     # 문 위 표시 띠
    pfill(im, 20, 6, 203, 29, "K")
    lamp, lamp_d = ("G", "g") if running else ("R", "r")
    for cx in (42, 182):                                                 # 양옆 표시등
        draw_ellipse(im, (cx - 11, 7, cx + 11, 29), "K")
        draw_ellipse(im, (cx - 8, 10, cx + 8, 26), lamp)
        pfill(im, cx - 5, 12, cx - 2, 15, "W")
        pfill(im, cx + 1, 20, cx + 6, 23, lamp_d)
    if running:                                                          # 작동: 위 화살표
        draw_poly(im, [(112, 8), (96, 27), (128, 27)], "G")
        pfill(im, 106, 26, 118, 28, "G")
        pfill(im, 108, 14, 111, 21, "W")
    else:                                                                # 멈춤: STOP 램프
        box3(im, 80, 8, 143, 28, "R", "W", "r", bevel=2)
        big_text(im, 89, 14, "STOP", "W", 2)
    pfill(im, 12, 36, 211, H - 1, "K")                                   # 문짝 두 장
    for x0 in (14, 114):
        box3(im, x0, 38, x0 + 95, H - 11, "3", "W", "2", bevel=2)
        box3(im, x0 + 12, 56, x0 + 83, 172, "4", "K", "2", bevel=2)      # 오목 판
        box3(im, x0 + 12, 188, x0 + 83, 252, "4", "K", "2", bevel=2)
        for y in (60, 176):
            rivet(im, x0 + 4, y); rivet(im, x0 + 88, y)
    pfill(im, 110, 38, 113, H - 11, "K")                                 # 문 틈
    if running:
        pfill(im, 111, 42, 112, H - 14, "Y")                             # 작동: 틈에 빛이 샌다
    else:
        pfill(im, 26, 116, 197, 133, "K"); hazard(im, 28, 118, 195, 131)      # 멈춤: 문 위를 가로지른 경고 테이프
    box3(im, 12, H - 11, 211, H - 1, "2", "3", "K", bevel=2)             # 문턱
    return im


# ---------- 레이저 차단막 640×384 (섹터 전체) ----------

def blocker():
    W, H = 640, 384
    im = canvas(W, H)
    xs = [20 + 40 * k for k in range(16)]
    for xc in xs:                                                        # 빔 둘레 빛(2px 디더 점)
        for gy in range((CEIL + 34) // 2, (FLOOR - 28) // 2):
            for off, ch, lv in ((-8, "R", 0), (6, "R", 0), (-12, "r", 1), (10, "r", 1)):
                hit = (gy % 2 == 0) if lv == 0 else (gy % 4 == (xc // 40) % 4)
                if hit:
                    x = xc + off
                    pfill(im, x, gy * 2, x + 1, gy * 2 + 1, ch)
    for xc in xs:                                                        # 빔(4px 실선: 붉은 가장자리 + 밝은 심)
        pfill(im, xc - 2, CEIL + 34, xc + 1, FLOOR - 28, "R")
        pfill(im, xc - 1, CEIL + 34, xc, FLOOR - 28, "W")
    for xc in xs:                                                        # 천장 발사기 · 바닥 수신기(형태 음영)
        box3(im, xc - 14, CEIL, xc + 14, CEIL + 22, "3", "W", "2", bevel=2)
        box3(im, xc - 7, CEIL + 22, xc + 7, CEIL + 31, "2", "3", "K", bevel=2)
        pfill(im, xc - 2, CEIL + 31, xc + 1, CEIL + 33, "R")
        rivet(im, xc - 10, CEIL + 5); rivet(im, xc + 8, CEIL + 5)
        box3(im, xc - 14, FLOOR - 22, xc + 14, FLOOR - 1, "3", "W", "2", bevel=2)
        box3(im, xc - 7, FLOOR - 31, xc + 7, FLOOR - 22, "2", "3", "K", bevel=2)
        pfill(im, xc - 2, FLOOR - 33, xc + 1, FLOOR - 31, "R")
        rivet(im, xc - 10, FLOOR - 17); rivet(im, xc + 8, FLOOR - 17)
    return im


# ---------- 중계기 64×140 (벽걸이: 아래쪽은 비워 둔다. 상자 아래 끝이 y 288쯤이라 복도 쓰레기통(y 296~)에 얹히지 않는다) ----------

def relay():
    W, H = 64, 140
    im = canvas(W, H)
    pfill(im, 46, 6, 49, 30, "K"); pfill(im, 47, 8, 48, 30, "3")          # 안테나
    pfill(im, 43, 0, 52, 8, "K"); pfill(im, 45, 2, 50, 6, "G"); pfill(im, 45, 2, 46, 3, "W")
    box3(im, 38, 24, 58, 31, "2", "3", "K", bevel=2)                     # 안테나 받침
    box3(im, 6, 30, 57, 68, "3", "W", "2", bevel=4)                      # 네트워크 상자
    pfill(im, 10, 36, 36, 62, "K")
    for i, x in enumerate((13, 21, 29)):                                 # 상태 LED 3개
        pfill(im, x, 39, x + 3, 42, "G" if i != 2 else "Y")
    for x in (13, 21, 29):                                               # 포트 칸
        pfill(im, x, 48, x + 5, 57, "a"); pfill(im, x + 1, 56, x + 4, 57, "Y")
    pfill(im, 42, 38, 53, 49, "K"); pfill(im, 44, 40, 51, 47, "G"); pfill(im, 44, 40, 46, 42, "W")   # 큰 초록 LED
    cells(im, 40, 36, 55, 51, "g", 4)                                    # LED 둘레 빛
    pfill(im, 42, 38, 53, 49, "K"); pfill(im, 44, 40, 51, 47, "G"); pfill(im, 44, 40, 46, 42, "W")
    rivet(im, 8, 33); rivet(im, 53, 33); rivet(im, 8, 63); rivet(im, 53, 63)
    pfill(im, 14, 68, 19, 100, "K"); pfill(im, 15, 68, 16, 100, "3")    # 벽을 타고 내려가는 배선관
    box3(im, 11, 88, 22, 94, "2", "3", "K", bevel=2)
    cells(im, 58, 36, 63, 72, "a", 4)                                    # 벽에 지는 그림자
    cells(im, 10, 70, 63, 75, "a", 4)
    return im


# ---------- 설계도 섹터 겹침 640×384 ----------

def bp_sector():
    W, H = 640, 384
    im = canvas(W, H)
    for gy in range(H // 2):                                             # 성긴 청사진 디더(1/3)
        for gx in range(W // 2):
            if (gx + 2 * gy) % 3 == 0:
                pfill(im, gx * 2, gy * 2, gx * 2 + 1, gy * 2 + 1, "b" if gy % 3 == 0 else "B")
    for x in range(0, W, 64):                                            # 격자(64px 간격)
        pfill(im, x, 0, x + 1, H - 1, "b")
    for y in range(0, H, 64):
        pfill(im, 0, y, W - 1, y + 1, "b")
    pfill(im, 0, 0, W - 1, 3, "C"); pfill(im, 0, H - 4, W - 1, H - 1, "C")   # 테두리
    pfill(im, 0, 0, 3, H - 1, "C"); pfill(im, W - 4, 0, W - 1, H - 1, "C")
    return im


# ---------- 빌드 ----------

REAL = {
    "shutter_open": lambda: shutter(False), "shutter_closed": lambda: shutter(True),
    "hatch_open": lambda: hatch(True), "hatch_closed": lambda: hatch(False),
    "elevator_running": lambda: elevator(True), "elevator_stopped": lambda: elevator(False),
}


def build():
    made = []
    pieces = {name: fn() for name, fn in REAL.items()}
    pieces["blocker_on"] = blocker()
    pieces["relay_box"] = relay()
    for name in list(REAL):
        pieces["bp_" + name] = to_blueprint(pieces[name])
    pieces["bp_sector"] = bp_sector()
    for name, im in pieces.items():
        assert not palette_check(im), name
        meta = {"status": "시안", "layer": "L4", "size": list(im.size), "px_per_logical": 4}
        made.append(save(im, f"objects128/{name}.png", meta))
    made += preview(pieces)
    return made


# ---------- 미리보기 ----------

def _label(sheet, x, y, s):
    draw_text(sheet, x, y, s, "W")


def _room(key):
    from pathlib import Path
    from artkit import EXPORT
    d = EXPORT / "rooms128" / key
    comp = canvas(640, 384, PIX["e"])
    comp.alpha_composite(Image.open(d / "L1_shell.png").convert("RGBA"))
    comp.alpha_composite(Image.open(d / "L2_props.png").convert("RGBA"))
    for p in sorted(d.glob("L3_light_*.png")):
        comp.alpha_composite(Image.open(p).convert("RGBA"))
    return comp


def preview(p):
    made = []
    # 1) 시트: 이름표와 함께, 현실·설계도 나란히(2배 확대)
    rows = [("shutter_open", "shutter_closed"), ("hatch_open", "hatch_closed"), ("elevator_running", "elevator_stopped"),
            ("relay_box", None), ("blocker_on", None)]
    pad = 24
    sheet = canvas(1500, 3800, PIX["1"])
    y = pad
    for a, b in rows:
        x = pad
        rowh = 0
        for name in (a, b):
            if not name:
                continue
            for n in (name, "bp_" + name):
                if n not in p:
                    continue
                im = p[n]
                k = 1 if im.width > 400 else 2
                big = im.resize((im.width * k, im.height * k), Image.NEAREST)
                if x + big.width + pad > sheet.width:
                    x = pad; y += rowh + 40; rowh = 0
                bg = canvas(big.width + 8, big.height + 8, PIX["0"])
                bg.alpha_composite(big, (4, 4))
                sheet.alpha_composite(bg, (x, y + 14))
                _label(sheet, x, y, n)
                x += bg.width + pad
                rowh = max(rowh, bg.height + 14)
        y += rowh + 40
    _label(sheet, pad, y, "bp_sector")
    bs = p["bp_sector"]
    sheet.alpha_composite(bs, (pad, y + 14))
    sheet = sheet.crop((0, 0, sheet.width, min(sheet.height, y + 14 + bs.height + pad)))
    made.append(save(sheet, "preview/objects128_ch0_sheet.png", {"status": "미리보기"}))

    # 2) 실제 방 위: 게임 배치(drawX, 바닥선 y 352)와 같게. 설계도는 현실 위에 겹침(x-4, 아래 +4)
    def put(room, items, bp_items=(), sector_bp=False, extra=None):
        c = _room(room)
        if extra:
            extra(c)
        for name, x in items:
            c.alpha_composite(p[name], (x, FLOOR - p[name].height))
        if sector_bp:
            c.alpha_composite(p["bp_sector"])
        for name, x in bp_items:
            c.alpha_composite(p[name], (x - 4, FLOOR + 4 - p[name].height))
        return c

    def laser(c):
        pass

    cells_ = [
        (put("stairwell", [("shutter_open", 160)]), "0-1 stairs open"),
        (put("stairwell", [("shutter_closed", 160)]), "0-1 stairs closed"),
        (put("corridor", [("hatch_closed", 216)]), "0-1 hatch closed"),
        (put("corridor", [("hatch_open", 216)]), "0-1 hatch open"),
        (put("elevator", [("elevator_running", 208)]), "0-2 elevator running"),
        (put("elevator", [("elevator_stopped", 208)]), "0-2 elevator stopped"),
        (put("corridor", [("relay_box", 368), ("blocker_on", 0)]), "0-2 laser + relay"),
        (put("corridor", [("relay_box", 368)]), "0-2 relay only (laser off)"),
        (put("stairwell", [("shutter_open", 160)], [("bp_shutter_closed", 160)], True), "bp stairs"),
        (put("corridor", [("hatch_closed", 216)], [("bp_hatch_open", 216)], True), "bp hatch"),
        (put("elevator", [("elevator_running", 208)], [("bp_elevator_stopped", 208)], True), "bp elevator"),
        (put("corridor", [("relay_box", 368), ("blocker_on", 0)], [], True), "bp sector on laser"),
    ]
    W, H = 640, 384
    gap = 16
    cols = 2
    rows_n = (len(cells_) + cols - 1) // cols
    sheet = canvas(cols * (W + gap) + gap, rows_n * (H + gap + 12) + gap, PIX["0"])
    for i, (im, label) in enumerate(cells_):
        x = gap + (i % cols) * (W + gap)
        y = gap + (i // cols) * (H + gap + 12)
        sheet.alpha_composite(im, (x, y + 12))
        _label(sheet, x, y + 2, label)
    made.append(save(sheet, "preview/objects128_ch0_onrooms.png", {"status": "미리보기"}))
    return made
