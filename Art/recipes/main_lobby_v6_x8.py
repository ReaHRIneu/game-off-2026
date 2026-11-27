"""메인 화면 v6 — PixelLab 생성물 가공(W-043·W-044, D-131, D-110·D-128, art-pipeline §4-3). 320x180(화면에서 8배=2560x1440).

lobby_scene_a.png 를 바탕으로 픽셀 편집만 한다(확대·축소·필터 없음, 알파 255).
1. 벽 판 2개의 글자 모양을 지우고 판 색 + 회로선·화살표 같은 글자 아닌 무늬로 바꾼다.
2. 책상의 인물 자리를 PixelLab 편집 크롭(lobby_hacker_crop_edit.png, 같은 크기·앉은 해커)으로 바꾼다.
3. 침대의 인물(러너)은 PixelLab 편집 크롭(lobby_runner_crop_edit.png, 48x72)을 인물 상자 안만 붙이고, 바지 초록을 러너 스프라이트의 카키로 바꾼다(W-044).
4. 책상 오른쪽의 모니터는 해커가 보는 쪽이 앞이므로 보는 사람에게는 뒷면(어두운 케이스·받침 연결부·통풍 줄)으로 바꾼다(W-045).
왼쪽 어두운 기둥(x 0~70)은 메뉴 자리라 건드리지 않는다.
5. (W-050) 움직임 덮개 그림: 장면 그림을 바꾼 곳만 작게 잘라 `scenes/main_lobby_v6_x8_<이름>.png`로 저장한다
   (바뀐 픽셀만 불투명, 나머지 투명, manifest 의 "pos" = 장면 좌표(320x180) 왼쪽 위). 본그림 main_lobby_v6_x8.png 는 바뀌지 않는다.
   blink_hacker·blink_runner 눈 감김, hands_l·hands_r 해커 손 1px 올림, mon_l1·mon_l2 모니터 뒷면 빛 어두워짐.
[시안] 사용자 확정 전.
"""
from pathlib import Path

from PIL import Image

from artkit import save

REPO = Path(__file__).resolve().parents[2]
SRC = REPO / "Art" / "Source" / "pixellab" / "2026-11-27"

PLATE = (58, 59, 107)       # 판 바탕색
SKY = (122, 197, 250)
DARK = (23, 26, 39)
# 해커 편집 크롭의 장면 위치(CX, CY)와 붙일 인물 상자(크롭 좌표 x0, y0, x1, y1)
CX, CY = 116, 52
HB = (12, 6, 58, 62)
# 러너 편집 크롭의 장면 위치(RX, RY), 붙일 인물 상자(크롭 좌표 x0, y0, x1, y1), 배경 색 흔들림 허용치
RX, RY = 80, 80
RB = (12, 4, 47, 71)
RT = 12
# 바지 초록 → 러너 스프라이트의 카키(밝음 (175,154,102), 기본 (145,124,83), 어두움 (112,86,66))
PANTS = {(104, 133, 84): (145, 124, 83), (92, 133, 93): (145, 124, 83),
         (55, 86, 62): (112, 86, 66), (57, 90, 64): (112, 86, 66)}


def is_gray(c):
    return max(c) - min(c) < 45 and sum(c) / 3 > 110


def clear_gray(im, x0, y0, x1, y1, plate):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if is_gray(im.getpixel((x, y))[:3]):
                im.putpixel((x, y), plate + (255,))


def put(im, pts, c):
    for x, y in pts:
        im.putpixel((x, y), c + (255,))


def line(a, b):
    (x0, y0), (x1, y1) = a, b
    n = max(abs(x1 - x0), abs(y1 - y0))
    return [(round(x0 + (x1 - x0) * i / n), round(y0 + (y1 - y0) * i / n)) for i in range(n + 1)] if n else [a]


def circuit(im, ox, oy, c):
    """회로선 무늬 17x15. (ox,oy)=왼쪽 위."""
    pts = []
    for a, b in [((0, 3), (6, 3)), ((6, 3), (6, 8)), ((6, 8), (12, 8)), ((12, 8), (12, 12)), ((6, 3), (10, 3)),
                 ((10, 3), (10, 0)), ((2, 8), (6, 8)), ((2, 8), (2, 13)), ((12, 12), (16, 12))]:
        pts += line(a, b)
    put(im, [(ox + x, oy + y) for x, y in pts], c)
    for nx, ny in [(0, 3), (10, 0), (2, 13), (16, 12)]:           # 끝 마디(2x2)
        put(im, [(ox + nx + dx, oy + ny + dy) for dx in (0, 1) for dy in (0, 1)], c)


def arrow(im, ox, oy, c):
    """오른쪽 화살표 13x9."""
    pts = line((0, 4), (10, 4)) + line((6, 0), (10, 4)) + line((6, 8), (10, 4)) + line((7, 0), (11, 4)) + line((7, 8), (11, 4))
    put(im, [(ox + x, oy + y) for x, y in pts], c)


BG = (41, 92, 178)           # 모니터 둘레 벽 바탕
CASE = (59, 66, 87)          # 케이스 바탕(장면 앞쪽 모니터 뒷면·책상 계열)
CASE_HI = (76, 87, 104)
CASE_SH = (41, 45, 65)
CASE_DK = (38, 40, 53)
GLOW = (75, 142, 234)


def monitor_back(im):
    """책상 오른쪽 모니터를 뒷면으로: x 179~226, y 84~107. 위쪽에 뒤로 새는 화면 빛 1~2줄, 가운데 받침 연결부와 통풍 줄 3개."""
    # 이전 윗 손잡이 자국(y 81~82)을 벽 바탕으로 지운다
    for y in (81, 82):
        for x in range(196, 211):
            if im.getpixel((x, y))[:3] != BG:
                im.putpixel((x, y), BG + (255,))
    # 케이스 면
    for y in range(84, 108):
        for x in range(179, 227):
            im.putpixel((x, y), CASE + (255,))
    # 둥근 모서리(네 귀퉁이는 벽 바탕)
    for x, y in [(179, 84), (226, 84), (179, 107), (226, 107)]:
        im.putpixel((x, y), BG + (255,))
    # 윗면 밝은 모서리, 오른쪽·아래 그늘
    put(im, [(x, 84) for x in range(180, 226)], CASE_HI)
    put(im, [(x, 107) for x in range(180, 226)], CASE_DK)
    put(im, [(226, y) for y in range(85, 107)] + [(225, y) for y in range(86, 107)], CASE_SH)
    put(im, [(x, 106) for x in range(181, 225)], CASE_SH)
    # 뒤로 새는 화면 빛: 위 모서리 바로 위 2줄
    put(im, [(x, 83) for x in range(180, 226)], SKY)
    put(im, [(x, 82) for x in range(183, 223)], SKY)
    put(im, [(x, 81) for x in range(188, 218)], GLOW)
    # 이전 앞면의 오른쪽 그늘 자국(x 227~229)을 벽 바탕으로 지운다
    for y in range(84, 108):
        for x in range(227, 231):
            if im.getpixel((x, y))[:3] in (CASE_SH, (41, 45, 65), (38, 40, 53), (35, 46, 60), (32, 40, 61)):
                im.putpixel((x, y), BG + (255,))
    # 해커 쪽(왼쪽 가장자리) 화면 빛 1px
    put(im, [(179, y) for y in range(86, 105)], GLOW)
    # 통풍 줄 3개
    for y in (88, 90, 92):
        put(im, [(x, y) for x in range(185, 196)], CASE_DK)
    # 받침 연결부(가운데 작은 사각)와 목
    put(im, [(x, y) for x in range(198, 209) for y in range(94, 102)], CASE_SH)
    put(im, [(x, y) for x in range(199, 208) for y in range(95, 101)], CASE_HI)
    put(im, [(x, y) for x in range(201, 206) for y in range(96, 100)], CASE)
    put(im, [(x, y) for x in range(201, 206) for y in range(102, 106)], CASE_DK)


SKIN_H = (248, 206, 193)     # 해커 얼굴 살색
LASH_H = (22, 25, 38)        # 해커 속눈썹
SKIN_R = (236, 187, 173)     # 러너 얼굴 살색
DARK_R = (23, 26, 39)
HAND = (247, 183, 158)
HAND_D = (242, 159, 125)
DESK_F = (22, 25, 38)


def overlay(base, edited, name):
    """바뀐 픽셀만 불투명인 작은 그림으로 저장한다. 장면 좌표 왼쪽 위를 manifest 에 남긴다."""
    pts = [(x, y) for y in range(base.height) for x in range(base.width) if base.getpixel((x, y)) != edited.getpixel((x, y))]
    assert pts, name
    x0, y0 = min(p[0] for p in pts), min(p[1] for p in pts)
    x1, y1 = max(p[0] for p in pts), max(p[1] for p in pts)
    ov = Image.new("RGBA", (x1 - x0 + 1, y1 - y0 + 1), (0, 0, 0, 0))
    for x, y in pts:
        ov.putpixel((x - x0, y - y0), edited.getpixel((x, y)))
    return save(ov, f"scenes/main_lobby_v6_x8_{name}.png", {"status": "움직임 덮개(W-050)", "pos": [x0, y0]})


def make_overlays(im):
    made = []
    # 해커 눈 감김: 눈 두 개(x149~150, x155~156) 위 줄은 살색, 아래 줄은 속눈썹 선
    e = im.copy()
    for x in (149, 150, 155, 156):
        e.putpixel((x, 74), SKIN_H + (255,))
        e.putpixel((x, 75), LASH_H + (255,))
    made.append(overlay(im, e, "blink_hacker"))
    # 러너 눈 감김(x105~106, y95~96)
    e = im.copy()
    put(e, [(105, 95), (106, 95)], SKIN_R)
    put(e, [(105, 96)], DARK_R)
    made.append(overlay(im, e, "blink_runner"))
    # 해커 손: 책상 위로 보이는 두 손(왼 x148~150, 오른 x159~162)을 번갈아 1px 올린다
    e = im.copy()
    put(e, [(148, 108), (149, 108), (150, 108)], DESK_F)
    put(e, [(149, 106), (149, 107), (150, 107)], HAND)
    put(e, [(148, 107)], HAND_D)
    made.append(overlay(im, e, "hands_l"))
    e = im.copy()
    put(e, [(x, 108) for x in range(159, 163)], DESK_F)
    put(e, [(x, 107) for x in range(159, 163)], HAND_D)
    made.append(overlay(im, e, "hands_r"))
    # 모니터 뒷면 둘레 빛: 밝음(본그림) -> 중간 -> 꺼짐
    for name, mapping in (("mon_l1", {SKY: GLOW, GLOW: BG}), ("mon_l2", {SKY: BG, GLOW: BG})):
        e = im.copy()
        for y in (81, 82, 83):
            for x in range(180, 226):
                c = im.getpixel((x, y))[:3]
                if c in mapping:
                    e.putpixel((x, y), mapping[c] + (255,))
        made.append(overlay(im, e, name))
    return made


def build():
    im = Image.open(SRC / "lobby_scene_a.png").convert("RGBA")
    assert im.size == (320, 180)
    # ---- 1. 벽 판 ----
    plate_l = PLATE
    clear_gray(im, 78, 58, 92, 74, plate_l)
    circuit(im, 78, 60, (156, 167, 182))
    plate_r = PLATE
    clear_gray(im, 238, 52, 273, 69, plate_r)
    circuit(im, 241, 53, (156, 167, 182))
    arrow(im, 260, 57, (156, 167, 182))
    # ---- 2. 책상의 인물 → 해커(W-043 검수 뒤 교체) ----
    # 전신 스프라이트는 머리가 장면 러너의 약 2배라, 장면의 (116,52)~(195,123)을 잘라 PixelLab 편집(1회)으로
    # 같은 크기·앉은 자세의 해커로 바꾼 그림을 쓴다. 인물 상자(크롭 좌표 HB) 안만 붙여 이음매를 줄인다.
    ed = Image.open(SRC / "lobby_hacker_crop_edit.png").convert("RGBA")
    assert ed.size == (80, 72)
    for y in range(HB[1], HB[3] + 1):
        for x in range(HB[0], HB[2] + 1):
            im.putpixel((CX + x, CY + y), ed.getpixel((x, y))[:3] + (255,))
    # ---- 3. 침대의 인물 → 러너(W-044) ----
    # 장면의 (80,80)~(127,151)을 잘라 PixelLab 편집(1회)으로 러너 스프라이트와 같은 머리·옷으로 바꾼 그림을 쓴다.
    # 인물 상자(크롭 좌표 RB) 안만 붙이고, 편집이 배경까지 1~3씩 흔든 색 차이(최대 채널 차이 RT 이하)는 원래 장면 색을 둔다(이음매 줄이기).
    rd = Image.open(SRC / "lobby_runner_crop_edit.png").convert("RGBA")
    assert rd.size == (48, 72)
    for y in range(RB[1], RB[3] + 1):
        for x in range(RB[0], RB[2] + 1):
            old = im.getpixel((RX + x, RY + y))[:3]
            new = rd.getpixel((x, y))[:3]
            if max(abs(a - b) for a, b in zip(old, new)) > RT:
                im.putpixel((RX + x, RY + y), new + (255,))
    # 바지의 초록 계열 → 러너 스프라이트(runner_w044)의 카키 바지 색(인물 상자 안만)
    for y in range(RB[1], RB[3] + 1):
        for x in range(RB[0], RB[2] + 1):
            c = im.getpixel((RX + x, RY + y))[:3]
            if c in PANTS:
                im.putpixel((RX + x, RY + y), PANTS[c] + (255,))
    # ---- 4. 책상 오른쪽 모니터 → 뒷면(W-045) ----
    monitor_back(im)
    assert all(a == 255 for a in im.getchannel("A").getdata())
    made = [save(im, "scenes/main_lobby_v6_x8.png", {"status": "본그림(PixelLab 가공)"})]
    made += make_overlays(im)
    out = REPO / "Art" / "Export" / "preview" / "w043_lobby_x2.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    im.resize((640, 360), Image.NEAREST).save(out)
    return made
