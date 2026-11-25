"""챕터 2 무인 공장지대 방 128px 새 그림 6종(섹터 640×384) — W-039, D-068·D-123·D-126, 03-01 §3.

방: 하역장(fc_dock)·프레스 라인(fc_press)·레이저 라인(fc_laser)·창고 단말(fc_stock)·출하장(fc_exit)·화물 승강 칸(fc_lift).
챕터 0·1과 같은 규칙: 광원 왼쪽 위, 명암 3단, 바깥 외곽선 K, 안티앨리어싱 없음, 공용 팔레트만, 바닥선 y 352·천장선 y 28.
공장 톤 [제안]: 벽 청록 회청(v·t — 챕터 0 남색·챕터 1 보라와 구분), 슬래브·강재(4·2·3), 바닥 콘크리트(1·0),
노랑·검정 경고 줄무늬(Y·K), 지게차·경고등만 노랑·빨강 강조.
빈 자리 규칙: 프레스 장치(W-035 임시 코드 그림)와 레이저(obj128_ch0 blocker, 천장·바닥 발사기 포함)는 엔진이 얹으므로
프레스 방 가운데(x 160~480)와 레이저 방 빔 구역(천장선 아래 34px·바닥선 위 33px, 전체 폭)에는 소품을 두지 않는다.
fc_control(관제실)은 room128_ch2zcolor가 fc_exit를 색 바꾸기로 만든다. 먼저 이 레시피를 실행한다.
레일 그림은 rooms128/common/rail(챕터 1)을 재사용한다(레일 방: fc_lift).
"""
from artkit import PIX, canvas, draw_ellipse, draw_line, draw_poly, palette_check, pbox, pfill, pset, save
from pixfont import draw_text
from prop import ambient, box3, contact_shadow, dark_overlay, dither_fill, glow, light_cone, rivets
from room128_ch1rooms import big_text, crate, hazard, hole, pipe_h

W, H = 640, 384
FLOOR, CEIL = 352, 28

WALL, WALL_HI, SLAB, STEEL, FLOOR_A, FLOOR_B = "v", "t", "4", "3", "1", "0"


# ---------- 공통 ----------

def shell(wall=WALL, bay_hazard=True):
    """벽 + 천장 슬래브 + 콘크리트 바닥 + 바닥 슬래브 + 벽 아래 경고 띠."""
    im = canvas(W, H, PIX[wall])
    for x in range(0, W, 64):                                    # 벽 패널 이음(어두운 줄 + 밝은 모서리)
        pfill(im, x, CEIL, x, FLOOR - 1, "K")
        pfill(im, x + 1, CEIL, x + 1, FLOOR - 1, WALL_HI)
    pfill(im, 0, 0, W - 1, CEIL - 1, SLAB); pfill(im, 0, CEIL - 2, W - 1, CEIL - 1, "K")
    pfill(im, 0, 6, W - 1, 7, "2")                               # 천장 슬래브 음영선
    pfill(im, 0, FLOOR, W - 1, FLOOR + 14, FLOOR_A)              # 콘크리트 바닥
    dither_fill(im, 0, FLOOR + 3, W - 1, FLOOR + 14, FLOOR_B, 5)
    for x in range(0, W, 96):
        pfill(im, x, FLOOR, x, FLOOR + 14, FLOOR_B)              # 타설 이음
    pfill(im, 0, FLOOR, W - 1, FLOOR, "K")
    pfill(im, 0, FLOOR + 15, W - 1, H - 1, SLAB); pfill(im, 0, FLOOR + 15, W - 1, FLOOR + 16, "K")
    if bay_hazard:
        hazard(im, 0, FLOOR - 8, W - 1, FLOOR - 1)               # 벽 아래 노랑·검정 줄무늬
    return im


def finish(im):
    ambient(im, CEIL, FLOOR)
    pbox(im, 0, 0, W - 1, H - 1, "K")
    return im


def shadows(pr, spans):
    for (x0, x1) in spans:
        contact_shadow(pr, x0, x1, FLOOR + 1)


def recess(im, x0, y0, x1, y1, fill="4", edge_hz=True):
    """벽에 들어간 홈(문틀·패널 자리): 어두운 바닥 + 위·왼쪽 안쪽 그림자 + 경고 테두리."""
    pfill(im, x0, y0, x1, y1, fill)
    pfill(im, x0, y0, x1, y0 + 3, "0"); pfill(im, x0, y0, x0 + 3, y1, "0")
    pbox(im, x0 - 1, y0 - 1, x1 + 1, y1 + 1, "K")
    if edge_hz:
        hazard(im, x0 - 10, y0 - 10, x1 + 10, y0 - 3)
        hazard(im, x0 - 10, y0 - 3, x0 - 3, y1)
        hazard(im, x1 + 3, y0 - 3, x1 + 10, y1)


def lamp_hang(pr, x, drop=36, w=46):
    """천장 매달린 공장 갓등(원뿔 갓 + 전선). 빛은 L3에서."""
    pfill(pr, x, CEIL, x + 1, CEIL + drop, "K")
    draw_poly(pr, [(x - w // 2 - 2, CEIL + drop + 18), (x - 8, CEIL + drop - 2), (x + 9, CEIL + drop - 2), (x + w // 2 + 3, CEIL + drop + 18)], "K")
    draw_poly(pr, [(x - w // 2 + 1, CEIL + drop + 16), (x - 6, CEIL + drop), (x + 7, CEIL + drop), (x + w // 2, CEIL + drop + 16)], "3")
    draw_poly(pr, [(x - w // 2 + 1, CEIL + drop + 16), (x - 6, CEIL + drop), (x - 3, CEIL + drop), (x - w // 2 + 8, CEIL + drop + 16)], "W")
    pfill(pr, x - w // 2 + 4, CEIL + drop + 13, x + w // 2 - 4, CEIL + drop + 17, "2")
    pfill(pr, x - w // 2 + 8, CEIL + drop + 18, x + w // 2 - 8, CEIL + drop + 20, "h")      # 전구 면


def lamp_light(x, drop=36, rx=130, ry=170, ch="h"):
    return glow(W, H, x, CEIL + drop + 70, rx, ry, ch, y_min=CEIL, y_max=FLOOR)


def beacon(pr, cx, cy, ch="R", dk="r"):
    """경고 회전등(벽·기계 위)."""
    box3(pr, cx - 12, cy + 14, cx + 12, cy + 24, "2", "3", "K", bevel=2)
    draw_ellipse(pr, (cx - 14, cy - 12, cx + 14, cy + 16), "K")
    draw_ellipse(pr, (cx - 11, cy - 9, cx + 11, cy + 14), ch)
    pfill(pr, cx - 8, cy - 5, cx - 5, cy, "W"); pfill(pr, cx + 3, cy + 6, cx + 9, cy + 12, dk)


def pallet(pr, x0, x1, y1=FLOOR - 1):
    """나무 팔레트(바닥에 놓임, 높이 16)."""
    for bx in (x0, (x0 + x1) // 2 - 8, x1 - 16):
        box3(pr, bx, y1 - 12, bx + 16, y1, "D", "h", "d", bevel=2)
    box3(pr, x0, y1 - 20, x1, y1 - 11, "D", "h", "d", bevel=2)
    for x in range(x0 + 20, x1 - 10, 28):
        pfill(pr, x, y1 - 18, x + 1, y1 - 13, "d")


def cartons(pr, x0, y1, cols, rows, cw=44, ch=34, hues=("D", "h")):
    """상자 더미(골판지)."""
    for r in range(rows):
        for c in range(cols):
            bx = x0 + c * (cw + 2) + (6 if r % 2 else 0)
            by = y1 - (r + 1) * (ch + 1)
            box3(pr, bx, by, bx + cw, by + ch, hues[0], hues[1], "d", bevel=3)
            pfill(pr, bx + cw // 2 - 4, by + 2, bx + cw // 2 + 3, by + 9, "h")               # 테이프
            pfill(pr, bx + 6, by + ch - 12, bx + 18, by + ch - 8, "W")                        # 라벨


def steel_shelf(pr, x0, x1, top, rows, box_pal):
    """강재 선반: 기둥 + 가로대 + 상자들."""
    for xx in (x0, x1 - 5):
        pfill(pr, xx, top, xx + 4, FLOOR - 1, "K"); pfill(pr, xx + 1, top, xx + 2, FLOOR - 1, "3")
    gap = (FLOOR - 8 - top) // rows
    for r in range(rows):
        by = top + (r + 1) * gap
        box3(pr, x0, by, x1, by + 6, "3", "W", "2", bevel=2)
        x = x0 + 8
        i = 0
        while x < x1 - 30:
            bw = 22 + (i * 7 + r * 5) % 16
            bh = 20 + (i * 5 + r * 3) % 14
            if x + bw > x1 - 8:
                break
            c = box_pal[(i + r) % len(box_pal)]
            box3(pr, x, by - bh, x + bw, by - 1, c, "h" if c == "D" else "W", "d" if c == "D" else "2", bevel=2)
            if i % 2 == 0:
                pfill(pr, x + 4, by - bh + 4, x + bw - 5, by - bh + 5, "K")
            x += bw + 3
            i += 1


# ---------- 하역장 fc_dock ----------
DOCK_DOOR = (70, 78, 270, 273)       # x, y, w, h(바닥선 y 352까지)


def dock():
    sh = shell()
    x0, y0, w, h = DOCK_DOOR
    # 셔터 문: 홈 + 판 + 아래 막대 + 경고 테두리
    recess(sh, x0, y0, x0 + w - 1, FLOOR - 9, "3", True)
    for y in range(y0, FLOOR - 9, 9):
        pfill(sh, x0, y, x0 + w - 1, y, "K"); pfill(sh, x0, y + 1, x0 + w - 1, y + 1, "W")
        pfill(sh, x0, y + 7, x0 + w - 1, y + 8, "2")
    for sx in (x0 + 44, x0 + 156):                               # 가이드 레일 이음
        pfill(sh, sx, y0, sx + 1, FLOOR - 9, "K")
    pfill(sh, x0, FLOOR - 24, x0 + w - 1, FLOOR - 10, "2"); pfill(sh, x0, FLOOR - 24, x0 + w - 1, FLOOR - 24, "K")   # 바닥 막대
    pfill(sh, x0 + 90, FLOOR - 22, x0 + 110, FLOOR - 16, "K")                                                           # 손잡이 홈
    box3(sh, x0 + w // 2 - 40, y0 - 40, x0 + w // 2 + 40, y0 - 24, "K", "2", "K", bevel=2)                           # 문 위 표지판
    pfill(sh, x0 + w // 2 - 36, y0 - 36, x0 + w // 2 + 36, y0 - 28, "Y")
    draw_text(sh, x0 + w // 2 - 24, y0 - 36, "DOCK 1", "K")
    # 오른쪽 벽: 번호판 + 안전 수칙 패널
    box3(sh, 380, 70, 480, 120, "t", "W", "v", bevel=2)
    pfill(sh, 388, 80, 472, 82, "Y"); draw_text(sh, 392, 90, "KEEP CLEAR", "W"); draw_text(sh, 392, 102, "OF FORKS", "Y")
    finish(sh)
    # finish()가 음영·외곽선을 덮으므로 구멍은 없음(셔터 닫힘)
    pr = canvas(W, H)
    # 갓등 2개
    lamp_hang(pr, 40, 30); lamp_hang(pr, 330, 30)
    # 천장 레일(호이스트 레일) + 체인
    pfill(pr, 0, CEIL, W - 1, CEIL + 5, "K"); pfill(pr, 0, CEIL + 1, W - 1, CEIL + 2, "3")
    # 왼쪽: 도크 레벨러(바닥 경사판)와 완충 고무
    box3(pr, 30, FLOOR - 14, 66, FLOOR - 1, "Y", "h", "y", bevel=2)
    for ky in (150, 230):
        box3(pr, 12, ky, 26, ky + 60, "K", "2", "K", bevel=2)
    # 오른쪽: 팔레트 + 상자 더미
    pallet(pr, 300, 400)
    cartons(pr, 304, FLOOR - 20, 2, 3, 44, 32)
    pallet(pr, 410, 500)
    cartons(pr, 414, FLOOR - 20, 2, 2, 40, 30, ("h", "W"))
    # 지게차(오른쪽 끝): 마스트 왼쪽, 포크는 팔레트 쪽
    mx = 520
    box3(pr, mx, 140, mx + 14, FLOOR - 6, "3", "W", "2", bevel=2)
    box3(pr, mx + 16, 140, mx + 28, FLOOR - 6, "2", "3", "K", bevel=2)
    pfill(pr, mx - 40, FLOOR - 15, mx, FLOOR - 10, "K"); pfill(pr, mx - 38, FLOOR - 14, mx - 2, FLOOR - 13, "3")             # 포크
    box3(pr, mx + 28, 250, 628, FLOOR - 24, "Y", "h", "y", bevel=4)
    pfill(pr, mx + 28, 296, 628, 300, "K")
    box3(pr, mx + 30, 252, mx + 64, 280, "2", "3", "K", bevel=2)                                                             # 엔진 덮개 홈
    pfill(pr, mx + 76, 196, mx + 80, 252, "K"); pfill(pr, 606, 196, 610, 252, "K")                                           # 운전석 지붕 기둥
    box3(pr, mx + 62, 188, 618, 200, "2", "3", "K", bevel=2)
    box3(pr, mx + 84, 232, 590, 252, "K", "2", "K", bevel=2)                                                                 # 좌석
    pfill(pr, mx + 100, 214, mx + 106, 232, "K")                                                                              # 핸들대
    draw_ellipse(pr, (mx + 26, FLOOR - 50, mx + 76, FLOOR - 1), "K"); draw_ellipse(pr, (mx + 33, FLOOR - 43, mx + 69, FLOOR - 8), "4")
    draw_ellipse(pr, (mx + 42, FLOOR - 34, mx + 60, FLOOR - 16), "3")
    draw_ellipse(pr, (558, FLOOR - 40, 618, FLOOR - 1), "K"); draw_ellipse(pr, (565, FLOOR - 33, 611, FLOOR - 8), "4")
    draw_ellipse(pr, (579, FLOOR - 27, 597, FLOOR - 14), "3")
    beacon(pr, 612, 170)
    pfill(pr, 611, 184, 614, 190, "K")
    shadows(pr, [(300, 400), (410, 500), (480, 628)])
    li = {"lamp_a_on": lamp_light(40), "lamp_b_on": lamp_light(330),
          "beacon_on": glow(W, H, 612, 160, 36, 36, "R", y_min=CEIL, y_max=FLOOR)}
    return sh, pr, li, []


# ---------- 프레스 라인 fc_press ----------
PRESS_FREE = (160, CEIL, 480, FLOOR)     # 장치(W-035 임시 그림)를 얹을 빈 자리


def press():
    sh = shell()
    # 가운데 작업 홈(프레스가 서는 자리): 어두운 패널 + 위쪽 경고 띠 + 바닥 표시
    recess(sh, 180, 64, 460, FLOOR - 9, "4", True)
    for x in range(184, 460, 28):
        pfill(sh, x, 72, x, FLOOR - 10, "0")
    # 양쪽 벽: 기둥 + 철판 패널
    for (xa, xb) in ((0, 40), (600, 639)):
        pfill(sh, xa, CEIL, xb, FLOOR - 9, "2"); pfill(sh, xa + 2, CEIL, xb - 2, FLOOR - 9, "4")
        for y in range(CEIL + 12, FLOOR - 20, 52):
            box3(sh, xa + 4, y, xb - 4, y + 30, "2", "3", "0", bevel=2)
    # 바닥 경고 사각(프레스 아래 작업 구역)
    for (xa, xb) in ((150, 160), (480, 490)):
        hazard(sh, xa, FLOOR - 1, xb, FLOOR + 12)
    finish(sh)
    pr = canvas(W, H)
    # 왼쪽: 제어반 + 비상정지
    box3(pr, 44, 168, 128, FLOOR - 1, "3", "W", "2")
    pfill(pr, 54, 180, 118, 226, "K"); pfill(pr, 56, 182, 116, 224, "g"); pfill(pr, 56, 182, 116, 186, "G")
    draw_text(pr, 60, 192, "PRESS A", "W"); draw_text(pr, 60, 206, "READY", "G")
    for i, c in enumerate(("R", "Y", "G")):
        box3(pr, 58 + i * 18, 236, 70 + i * 18, 248, c, "W", "K", bevel=2)
    draw_ellipse(pr, (74, 262, 100, 288), "K"); draw_ellipse(pr, (77, 265, 97, 285), "R"); pfill(pr, 82, 268, 86, 272, "W")  # 비상정지
    pfill(pr, 54, 300, 118, 302, "K")
    # 왼쪽 천장: 호이스트 레일 + 유압 배관(가운데 위는 비움)
    box3(pr, 0, CEIL, 150, CEIL + 12, "3", "W", "2", bevel=2)
    pipe_h(pr, CEIL + 16, 0, 140, "3", "2"); pipe_h(pr, CEIL + 30, 0, 150, "t", "K")
    box3(pr, 490, CEIL, W - 1, CEIL + 12, "3", "W", "2", bevel=2)
    pipe_h(pr, CEIL + 16, 500, W - 1, "3", "2"); pipe_h(pr, CEIL + 30, 490, W - 1, "t", "K")
    for x in (24, 560):
        box3(pr, x, CEIL + 12, x + 12, CEIL + 36, "3", "W", "2", bevel=2)
    # 오른쪽: 금형 선반 + 강판 재료 더미 + 부품 상자
    steel_shelf(pr, 500, 626, 140, 2, ("3", "2", "3", "W"))
    for (bx, by) in ((506, 232), (524, 232), (542, 232)):                                                                      # 금형 블록
        box3(pr, bx, by, bx + 14, by + 20, "2", "3", "K", bevel=2)
    box3(pr, 510, 300, 620, FLOOR - 1, "t", "C", "v", bevel=4)                                                                 # 부품 보관통
    for i in range(3):
        pfill(pr, 518 + i * 34, 268, 548 + i * 34, 278, "3"); pfill(pr, 518 + i * 34, 268, 548 + i * 34, 269, "W")        # 찍힌 판
    pfill(pr, 510, 322, 620, 324, "K")
    beacon(pr, 500, 100)
    pfill(pr, 499, 114, 502, 140, "K")
    beacon(pr, 140, 100)
    pfill(pr, 139, 114, 142, 140, "K")
    shadows(pr, [(44, 128), (500, 626)])
    li = {"strip_a_on": glow(W, H, 80, 60, 90, 50, "C", y_min=CEIL, y_max=FLOOR),
          "strip_b_on": glow(W, H, 560, 60, 90, 50, "C", y_min=CEIL, y_max=FLOOR),
          "beacon_on": glow(W, H, 140, 96, 36, 36, "R", y_min=CEIL, y_max=FLOOR)}
    return sh, pr, li, []


# ---------- 레이저 라인 fc_laser ----------

def laser():
    sh = shell(wall="v")
    # 벽 가운데를 한 단 어둡게(빔이 잘 보이도록): 어두운 패널 + 세로 홈
    pfill(sh, 24, 66, 615, 318, "4")
    for x in range(24, 615, 40):
        pfill(sh, x, 66, x + 1, 318, "0"); pfill(sh, x + 2, 66, x + 2, 318, "2")
    pbox(sh, 24, 66, 615, 318, "K")
    # 양 끝 경고 기둥(세로 줄무늬)
    for (xa, xb) in ((0, 21), (618, 639)):
        pfill(sh, xa, CEIL, xb, FLOOR - 9, "2")
    for xa in (6, 624):
        for y in range(CEIL + 4, FLOOR - 12, 8):
            pfill(sh, xa, y, xa + 8, y + 3, "Y")
    finish(sh)
    pr = canvas(W, H)
    # 케이블 트레이(발사기 아래 y 70~)와 벽걸이 접속함: 빔 구역(천장선 +34·바닥선 -33) 밖에만
    box3(pr, 24, 66, 615, 76, "3", "W", "2", bevel=2)
    for x in range(40, 600, 56):
        pfill(pr, x, 66, x + 1, 76, "K")
    # 왼쪽 접속함 + 상태등(바닥선 위 33px 아래는 바닥 수신기 자리라 y 280 위로)
    box3(pr, 30, 100, 76, 160, "3", "W", "2")
    pfill(pr, 36, 108, 70, 130, "K"); pfill(pr, 38, 110, 68, 128, "g"); draw_text(pr, 40, 116, "LASER", "G")
    for i, c in enumerate(("R", "Y", "G")):
        pfill(pr, 38 + i * 11, 140, 44 + i * 11, 146, c)
    # 오른쪽 경고 표지(삼각 + 번개)
    box3(pr, 556, 96, 614, 170, "Y", "h", "y", bevel=2)
    draw_poly(pr, [(585, 106), (608, 154), (562, 154)], "K")
    draw_poly(pr, [(585, 114), (602, 150), (568, 150)], "Y")
    pfill(pr, 583, 124, 587, 140, "K"); pfill(pr, 583, 144, 587, 148, "K")
    draw_text(pr, 566, 158, "LASER", "K")
    # 바닥 가운데 먼지 얼룩 없음(수신기 구역), 중계기 자리는 비워 둔다
    # 천장 형광등 2개(L3로 켜짐)
    for x in (200, 440):
        box3(pr, x - 36, CEIL + 36, x + 36, CEIL + 44, "3", "W", "2", bevel=2)
        pfill(pr, x - 30, CEIL + 45, x + 30, CEIL + 47, "W")
    li = {"fluor_a_on": glow(W, H, 200, 100, 100, 70, "C", y_min=CEIL, y_max=FLOOR),
          "fluor_b_on": glow(W, H, 440, 100, 100, 70, "C", y_min=CEIL, y_max=FLOOR)}
    return sh, pr, li, []


# ---------- 창고·단말 fc_stock ----------

def stock():
    sh = shell()
    # 벽 위쪽 번호 줄과 구역 표시
    pfill(sh, 0, 62, W - 1, 65, "K"); pfill(sh, 0, 63, W - 1, 63, "3")
    for i, x in enumerate(range(48, W, 128)):
        draw_text(sh, x, 70, f"A{i + 1}", "Y")
    recess(sh, 500, 120, 628, FLOOR - 9, "4", False)             # 단말 오브젝트가 서는 벽 홈
    finish(sh)
    pr = canvas(W, H)
    # 왼쪽 큰 선반(3단)
    steel_shelf(pr, 16, 250, 100, 3, ("D", "h", "3", "D"))
    # 가운데 낮은 선반(2단)과 팔레트
    steel_shelf(pr, 266, 380, 200, 2, ("3", "D", "h"))
    # 도시 단속반 표식 상자 [생성]: 바닥에 놓인 상자, 빨강·흰 사선 + 방패 표식 + 라벨
    pallet(pr, 392, 478)
    bx0, by0, bx1, by1 = 396, 252, 474, FLOOR - 22
    box3(pr, bx0, by0, bx1, by1, "3", "W", "2", bevel=3)
    for sx in range(bx0 + 6, bx1 - 6, 14):                        # 빨강 띠(대각선처럼 보이는 블록)
        pfill(pr, sx, by0 + 6, sx + 6, by0 + 12, "R")
    draw_poly(pr, [(436, 270), (454, 274), (454, 290), (436, 302), (418, 290), (418, 274)], "K")
    draw_poly(pr, [(436, 273), (451, 277), (451, 289), (436, 299), (421, 289), (421, 277)], "R")
    pfill(pr, 434, 280, 438, 290, "W"); pfill(pr, 430, 284, 442, 286, "W")
    draw_text(pr, 404, 306, "CITY", "K"); draw_text(pr, 430, 306, "GUARD", "R")
    pfill(pr, 400, 322, 470, 324, "K")
    # 작은 일반 상자 몇 개
    cartons(pr, 404, 252, 1, 1, 60, 30)
    box3(pr, 484, 306, 506, FLOOR - 1, "D", "h", "d", bevel=3)
    # 오른쪽 벽: 단말 옆 클립보드와 전표
    box3(pr, 540, 66, 580, 108, "W", "W", "3", bevel=2)
    for ly in range(76, 104, 6):
        pfill(pr, 546, ly, 574, ly, "2")
    pfill(pr, 552, 62, 568, 68, "3")
    # 천장 형광등
    for x in (140, 400):
        box3(pr, x - 40, CEIL + 4, x + 40, CEIL + 12, "3", "W", "2", bevel=2)
        pfill(pr, x - 34, CEIL + 13, x + 34, CEIL + 15, "W")
    shadows(pr, [(16, 250), (266, 380), (392, 478)])
    li = {"fluor_a_on": glow(W, H, 140, 100, 120, 80, "C", y_min=CEIL, y_max=FLOOR),
          "fluor_b_on": glow(W, H, 400, 100, 120, 80, "C", y_min=CEIL, y_max=FLOOR)}
    return sh, pr, li, []


# ---------- 출하장 fc_exit ----------

def exit_room():
    sh = shell()
    # 오른쪽 출구 홈(출구 오브젝트가 서는 자리) + 위 표지
    recess(sh, 496, 110, 630, FLOOR - 9, "0", True)
    box3(sh, 340, 70, 470, 112, "K", "2", "K", bevel=2)
    pfill(sh, 346, 76, 464, 106, "g")
    big_text(sh, 354, 78, "OUT", "G", 3)
    for k in range(3):
        draw_poly(sh, [(418 + k * 14, 80), (428 + k * 14, 91), (418 + k * 14, 102), (414 + k * 14, 102), (424 + k * 14, 91), (414 + k * 14, 80)], "G")
    finish(sh)
    pr = canvas(W, H)
    # 컨베이어(바닥 위 컨베이어 벨트, 오른쪽으로 흐름)
    y0, y1 = 294, 322
    box3(pr, 14, y0, 470, y1, "2", "3", "K", bevel=3)
    pfill(pr, 18, y0 + 4, 466, y0 + 14, "4")
    for x in range(20, 466, 18):
        pfill(pr, x, y0 + 4, x + 1, y0 + 14, "0")
    for x in (24, 160, 296, 432):                                 # 다리
        box3(pr, x, y1, x + 12, FLOOR - 1, "3", "W", "2", bevel=2)
    for x in range(60, 450, 130):                                 # 롤러 받침 보강
        pfill(pr, x, y1 + 6, x + 100, y1 + 8, "K")
    # 벨트 위 상자
    for (x, w_, h_, hue) in ((40, 54, 38, "D"), (190, 48, 30, "h"), (318, 58, 42, "D")):
        box3(pr, x, y0 - h_, x + w_, y0 - 1, hue, "h" if hue == "D" else "W", "d", bevel=3)
        pfill(pr, x + w_ // 2 - 3, y0 - h_ + 2, x + w_ // 2 + 3, y0 - h_ + 10, "h")
        pfill(pr, x + 6, y0 - 12, x + 20, y0 - 8, "W")
    # 상자 쌓음대 + 라벨 프린터(왼쪽 위)
    box3(pr, 16, 130, 120, 180, "3", "W", "2")
    pfill(pr, 24, 138, 112, 160, "K"); pfill(pr, 26, 140, 110, 158, "g"); draw_text(pr, 30, 146, "SHIP 07", "G")
    pfill(pr, 40, 170, 100, 174, "K")
    # 컨베이어 끝 출하 램프(오른쪽 홈 앞은 비워 둠 x 480~630)
    box3(pr, 466, y0 + 4, 486, y1 - 2, "3", "W", "2", bevel=2)
    # 천장: 컨베이어 위 갓등 2개
    lamp_hang(pr, 110, 26); lamp_hang(pr, 240, 26)
    shadows(pr, [(14, 470)])
    li = {"lamp_a_on": lamp_light(110, 26), "lamp_b_on": lamp_light(240, 26),
          "sign_on": glow(W, H, 405, 90, 80, 40, "G", y_min=CEIL, y_max=FLOOR)}
    return sh, pr, li, []


# ---------- 화물 승강 칸 fc_lift [레일] ----------

def lift():
    sh = shell(wall="2", bay_hazard=False)
    # 철망 벽(칸 안쪽 격자) + 양 끝 가이드 기둥
    for x in range(24, W - 24, 16):
        pfill(sh, x, CEIL, x, FLOOR - 1, "4")
    for y in range(CEIL + 4, FLOOR, 16):
        pfill(sh, 24, y, W - 25, y, "4")
    for x in range(24, W - 24, 16):
        for y in range(CEIL + 4, FLOOR, 16):
            pset(sh, x, y, "3")                                   # 격자 매듭(밝은 점)
    for (xa, xb) in ((0, 23), (616, 639)):
        pfill(sh, xa, CEIL, xb, FLOOR - 1, "4"); pfill(sh, xa + 2, CEIL, xb - 2, FLOOR - 1, "t")
        pfill(sh, xa + 2, CEIL, xa + 5, FLOOR - 1, "C"); pfill(sh, xb - 5, CEIL, xb - 2, FLOOR - 1, "v")
    for y in (60, 140, 220, 300):                                 # 가이드 롤러 이음판
        for xa in (0, 594):
            box3(sh, xa, y, xa + 45, y + 22, "3", "W", "2", bevel=2)
            for bx in (10, 24, 36):
                pfill(sh, xa + bx, y + 8, xa + bx + 3, y + 11, "K")
    # 바닥 격판(다이아몬드 무늬)
    for x in range(26, W - 26, 12):
        pfill(sh, x, FLOOR + 3, x + 5, FLOOR + 5, "3")
    hazard(sh, 26, 338, 613, 343)
    # 가운데 위: 하중 표시판
    box3(sh, 380, 70, 516, 112, "Y", "h", "y", bevel=2)
    draw_text(sh, 394, 78, "CARGO LIFT", "K"); draw_text(sh, 394, 92, "MAX 5T", "K")
    finish(sh)
    pr = canvas(W, H)
    # 위 체인 걸이 + 후크(가운데 천장)
    for k in range(5):
        pfill(pr, 316, CEIL + k * 8, 323, CEIL + k * 8 + 5, "K"); pfill(pr, 318, CEIL + k * 8 + 1, 321, CEIL + k * 8 + 4, "3")
    box3(pr, 300, CEIL + 40, 340, CEIL + 54, "3", "W", "2", bevel=2)
    draw_line(pr, [(320, CEIL + 54), (320, CEIL + 66), (312, CEIL + 76)], "K", 4)
    # 한쪽 구석 짐: 묶음 강재와 팔레트
    pallet(pr, 60, 190)
    for k in range(4):
        box3(pr, 66, FLOOR - 40 - k * 12, 184, FLOOR - 29 - k * 12, "3", "W", "2", bevel=2)
    pallet(pr, 450, 560)
    crate(pr, 456, 286, 556, FLOOR - 21, "D", "h", "d")
    # 조작 패널(벽 오른쪽)
    box3(pr, 584, 140, 612, 220, "2", "3", "K", bevel=2)
    pfill(pr, 590, 148, 606, 154, "R")
    for i, ch in enumerate(("up", "dn")):
        box3(pr, 590, 164 + i * 22, 606, 180 + i * 22, "Y", "h", "y", bevel=2)
        if ch == "up":
            draw_poly(pr, [(598, 167), (603, 175), (593, 175)], "K")
        else:
            draw_poly(pr, [(598, 195), (603, 187), (593, 187)], "K")
    shadows(pr, [(60, 190), (450, 560)])
    li = {"cage_a_on": glow(W, H, 200, 100, 120, 70, "h", y_min=CEIL, y_max=FLOOR),
          "cage_b_on": glow(W, H, 440, 100, 120, 70, "h", y_min=CEIL, y_max=FLOOR)}
    # 천장 등 기구
    for x in (200, 440):
        box3(pr, x - 30, CEIL + 4, x + 30, CEIL + 12, "3", "W", "2", bevel=2)
        pfill(pr, x - 24, CEIL + 13, x + 24, CEIL + 15, "W")
    return sh, pr, li, []


# ---------- 빌드 ----------
ROOMS = {"fc_dock": dock, "fc_press": press, "fc_laser": laser, "fc_stock": stock,
         "fc_exit": exit_room, "fc_lift": lift}


def build():
    made = []
    for key, fn in ROOMS.items():
        sh, pr, li, wins = fn()
        for name, im in [("L1_shell", sh), ("L2_props", pr)] + [(f"L3_light_{k}", v) for k, v in li.items()]:
            assert not palette_check(im), (key, name)
            meta = {"status": "시안", "layer": name[:2], "size": [W, H], "floor_y": FLOOR}
            if name == "L1_shell":
                meta["windows"] = [list(r) for r in wins]
            made.append(save(im, f"rooms128/{key}/{name}.png", meta))
        dk = dark_overlay(W, H, CEIL, FLOOR)
        assert not palette_check(dk), key
        made.append(save(dk, f"rooms128/{key}/L3_dark.png", {"status": "시안", "layer": "L3", "use": "조명이 모두 꺼졌을 때"}))
        comp = canvas(W, H, PIX["e"]); comp.alpha_composite(sh); comp.alpha_composite(pr)
        for v in li.values():
            comp.alpha_composite(v)
        made.append(save(comp, f"preview/rooms128_{key}.png", {"status": "미리보기"}))
    return made
