"""챕터 2 공장 색 변경 방 5종 — W-039, D-068, 03-01 §3 (room128_ch1recolor 방식).

챕터 0 방(corridor·stairwell)·챕터 1 옥상 복도(ot_roofhall)·이 챕터 출하장(fc_exit)의 출력 PNG를 읽어
팔레트 문자 → 팔레트 문자 대응표로 바꿔 칠한다(새 색 추가 없음). 원본 그림은 건드리지 않고 새 키로 따로 저장한다.
먼저 실행할 것: room128_ch0rooms·room128_corridor·room128_ch1rooms(원본 방) → room128_ch2rooms(fc_exit).
공장 톤 [제안]: 벽 청록 회청(v·t), 강재(4·2·3), 노랑 경고선(Y·K).
 - fc_corridor(복도)·fc_stair(계단실)·fc_watch(감시 통로)·fc_roofpipe(옥상 배관): 색만 바꿈(+ 노랑·검정 경고 띠 추가)
 - fc_control(관제실): fc_exit의 색 바꾸기 + 출하 설비를 지우고 관제 콘솔·모니터 벽을 추가(03-01 §3-3의 3F 출구 방)
"""
import json

from PIL import Image

from artkit import EXPORT, PIX, canvas, palette_check, pbox, pfill, pset, recolor, save
from pixfont import draw_text
from prop import box3, contact_shadow, dither_fill, glow
from room128_ch1rooms import big_text, hazard

W, H = 640, 384
FLOOR, CEIL = 352, 28

# ---------- 대응표(맨 위 상수) ----------
# 복도(corridor): 벽 1→v, 패널 2→t, 나머지 강재(0·3·4)는 그대로
CORR = {"1": "v", "2": "t"}
# 계단실(stairwell): 벽 0→v, 어두운 판 4→t는 너무 밝아져 4 유지, 밝은 판 2→t
STAIR = {"0": "v", "2": "t"}
# 감시 통로: 복도보다 어둡게(감시 구역 느낌): 벽 1→4, 패널 2→v
WATCH = {"1": "4", "2": "v"}
# 옥상 배관(옥상 복도 원본): 보라 회색 → 강재·청록. 갈색 d·D는 녹슨 배관으로 둠
ROOF = {"H": "4", "U": "2", "f": "0", "J": "v"}
# 관제실(fc_exit 원본): 청록 벽 → 어두운 강재 + 밝은 패널, 노랑 경고 → 빨강 경고
CTRL = {"v": "4", "t": "2", "Y": "R", "y": "r"}
# 조명(L3): 챕터 0 형광등의 흰빛을 차가운 하늘빛으로
LIGHT = {"W": "C"}

# (원본 방, 새 키, 이름 대응표, 조명 대응표, 지울 영역 [(x0,y0,x1,y1)], 경고 띠 추가 여부)
JOBS = [
    ("corridor", "fc_corridor", CORR, LIGHT, [], True),
    ("stairwell", "fc_stair", STAIR, LIGHT, [], True),
    # 감시 통로: 카메라(L4)가 보이도록 게시판·전단·표지를 지운다
    ("corridor", "fc_watch", WATCH, LIGHT, [(30, 80, 210, 200), (230, 110, 370, 210), (410, 90, 510, 130), (370, 290, 430, 351)], True),
    ("ot_roofhall", "fc_roofpipe", ROOF, {"W": "C", "3": "c"}, [], False),
    ("fc_exit", "fc_control", CTRL, {"h": "C", "G": "C"}, [(0, 100, 495, 356)], False),
]


def src(room, name):
    return Image.open(EXPORT / f"rooms128/{room}/{name}.png").convert("RGBA")


def clear(im, boxes):
    for (x0, y0, x1, y1) in boxes:
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                im.putpixel((x, y), (0, 0, 0, 0))


def control_shell_patch(im):
    """fc_control L1: 출하 'OUT' 표지를 지우고 관제 표지로 바꾼다."""
    pfill(im, 336, 66, 476, 116, "4")
    box3(im, 340, 70, 470, 112, "K", "2", "K", bevel=2)
    pfill(im, 346, 76, 464, 106, "g")
    draw_text(im, 362, 86, "CONTROL", "W")
    pfill(im, 346, 76, 464, 78, "G")


def control_props(im):
    """fc_control L2: 관제 콘솔 + 모니터 벽(왼쪽·가운데), 오른쪽 출구 자리는 비움."""
    # 모니터 벽 2열 × 4개
    box3(im, 20, 120, 400, 232, "K", "2", "K", bevel=2)
    for r in range(2):
        for c in range(4):
            x0 = 30 + c * 92
            y0 = 128 + r * 50
            pfill(im, x0, y0, x0 + 84, y0 + 42, "K")
            pfill(im, x0 + 2, y0 + 2, x0 + 82, y0 + 40, "g" if (r + c) % 2 == 0 else "B")
            pfill(im, x0 + 2, y0 + 2, x0 + 82, y0 + 4, "G" if (r + c) % 2 == 0 else "b")
            for k in range(4):                                    # 화면 속 줄(데이터)
                pfill(im, x0 + 8, y0 + 12 + k * 7, x0 + 20 + ((k + c) % 3) * 16, y0 + 13 + k * 7, "W")
    # 콘솔 책상
    box3(im, 30, 284, 380, 300, "3", "W", "2", bevel=2)
    box3(im, 40, 300, 370, FLOOR - 1, "2", "3", "4", bevel=3)
    for x in range(60, 360, 60):
        pfill(im, x, 310, x + 40, 312, "K")
    for i, c in enumerate(("R", "Y", "G", "C", "G", "Y")):        # 스위치 줄
        pfill(im, 60 + i * 24, 288, 70 + i * 24, 294, c)
    box3(im, 120, 250, 190, 284, "K", "2", "K", bevel=2)         # 책상 위 단말
    pfill(im, 124, 254, 186, 280, "g"); pfill(im, 124, 254, 186, 256, "G")
    # 의자
    box3(im, 236, 296, 280, 312, "4", "2", "0", bevel=2)
    pfill(im, 256, 312, 260, FLOOR - 1, "K")
    contact_shadow(im, 30, 380, FLOOR + 1)


def add_hazard(im):
    """벽 아래 노랑·검정 경고 띠(공장 표시). L1 바닥선 바로 위."""
    hazard(im, 0, FLOOR - 8, W - 1, FLOOR - 1)


def build():
    made = []
    man = json.loads((EXPORT / "manifest.json").read_text(encoding="utf-8"))
    for room, key, cmap, lmap, erase, hz in JOBS:
        names = sorted(p.stem for p in (EXPORT / f"rooms128/{room}").glob("L*.png"))
        for name in names:
            if name == "L3_dark":
                im = src(room, name)
            elif name.startswith("L3_light"):
                im = recolor(src(room, name), lmap)
            else:
                im = recolor(src(room, name), cmap)
                if name == "L2_props" and erase:
                    clear(im, erase)
                    if key == "fc_control":
                        control_props(im)
                if name == "L1_shell":
                    if hz:
                        add_hazard(im)
                    if key == "fc_control":
                        control_shell_patch(im)
            assert not palette_check(im), (key, name)
            meta = {"status": "시안", "layer": name[:2], "size": [W, H], "floor_y": FLOOR, "recolor_of": room}
            if name == "L1_shell":
                meta["windows"] = man.get(f"rooms128/{room}/L1_shell.png", {}).get("windows", [])
            made.append(save(im, f"rooms128/{key}/{name}.png", meta))
        comp = canvas(W, H, PIX["e"])
        comp.alpha_composite(Image.open(EXPORT / f"rooms128/{key}/L1_shell.png").convert("RGBA"))
        comp.alpha_composite(Image.open(EXPORT / f"rooms128/{key}/L2_props.png").convert("RGBA"))
        for p in sorted((EXPORT / f"rooms128/{key}").glob("L3_light_*.png")):
            comp.alpha_composite(Image.open(p).convert("RGBA"))
        made.append(save(comp, f"preview/rooms128_{key}.png", {"status": "미리보기"}))
    return made
