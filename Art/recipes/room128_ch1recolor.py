"""챕터 1 구시가지 색 변경 방 5종 — W-015, D-100.

챕터 0 방(corridor·stairwell·lobby·basement)의 출력 PNG를 읽어 팔레트 문자 → 팔레트 문자 대응표로 바꿔 칠한다(새 색 추가 없음).
챕터 0 그림·레시피 출력은 건드리지 않고 새 키(ot_*)로 따로 저장한다. 원본 PNG가 먼저 만들어져 있어야 한다
(room128_ch0rooms·room128_corridor를 먼저 실행).
구시가지 톤: 챕터 0의 회청(0·1·2·3·4)을 보라 회색(f·J·H·U)과 따뜻한 갈색(d·D)으로 옮긴다.
"""
import json

from PIL import Image

from artkit import EXPORT, PIX, canvas, palette_check, pfill, pset, recolor, save
from prop import dither_fill

W, H = 640, 384

# ---------- 대응표(맨 위 상수) ----------
# 공통(복도·로비·계단실): 벽 1→J, 어두운 벽 0→f, 판넬 4→d, 슬래브·밝은 판 2→H, 금속 3→U
BASE = {"0": "f", "1": "J", "4": "d", "2": "H", "3": "U"}
# 조명(L3)은 형광등의 흰빛·회색빛을 따뜻하게
LIGHT = {"W": "h", "3": "w"}
# 창고(basement): 벽 4가 원래 벽 → 따뜻한 갈색 벽. 벽 이음 0→f
STORAGE = {"0": "f", "1": "J", "4": "d", "2": "H", "3": "U", "d": "f", "D": "w"}
# 레이저 복도(복도의 두 번째 변형): 더 차가운 보라, 형광등은 청록 톤
LASER = {"0": "f", "1": "f", "4": "J", "2": "H", "3": "U"}
LASER_LIGHT = {"W": "C", "3": "c"}

# (원본 방, 새 키, 이름 대응표, 조명 대응표, 지울 영역 [(x0,y0,x1,y1)])
JOBS = [
    ("corridor", "ot_corridor", BASE, LIGHT, []),
    ("stairwell", "ot_stairwell", BASE, LIGHT, []),
    ("lobby", "ot_lobby", BASE, LIGHT, []),
    ("basement", "ot_storage", STORAGE, LIGHT, []),
    # 레이저가 잘 보이도록 게시판·우편함·전단·표지·쓰레기통을 지운다(L2 영역 지우기)
    ("corridor", "ot_lasercorr", LASER, LASER_LIGHT, [(30, 80, 210, 200), (230, 110, 370, 210), (410, 90, 510, 130), (370, 290, 430, 351)]),
]


def src(room, name):
    return Image.open(EXPORT / f"rooms128/{room}/{name}.png").convert("RGBA")


def manifest():
    return json.loads((EXPORT / "manifest.json").read_text(encoding="utf-8"))


def clear(im, boxes):
    for (x0, y0, x1, y1) in boxes:
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                im.putpixel((x, y), (0, 0, 0, 0))


def build():
    made = []
    man = manifest()
    for room, key, cmap, lmap, erase in JOBS:
        names = sorted(p.stem for p in (EXPORT / f"rooms128/{room}").glob("L*.png"))
        for name in names:
            if name == "L3_dark":
                im = src(room, name)                              # 어둠은 색이 없는 디더(a)라 그대로 복사
            elif name.startswith("L3_light"):
                im = recolor(src(room, name), lmap)
            else:
                im = recolor(src(room, name), cmap)
                if name == "L2_props" and erase:
                    clear(im, erase)
            assert not palette_check(im), (key, name)
            meta = {"status": "시안", "layer": name[:2], "size": [W, H], "floor_y": 352, "recolor_of": room}
            if name == "L1_shell":
                meta["windows"] = man.get(f"rooms128/{room}/L1_shell.png", {}).get("windows", [])
            made.append(save(im, f"rooms128/{key}/{name}.png", meta))
        # 미리보기
        wins = man.get(f"rooms128/{room}/L1_shell.png", {}).get("windows", [])
        comp = canvas(W, H, PIX["e"])
        comp.alpha_composite(Image.open(EXPORT / f"rooms128/{key}/L1_shell.png").convert("RGBA"))
        comp.alpha_composite(Image.open(EXPORT / f"rooms128/{key}/L2_props.png").convert("RGBA"))
        for p in sorted((EXPORT / f"rooms128/{key}").glob("L3_light_*.png")):
            comp.alpha_composite(Image.open(p).convert("RGBA"))
        made.append(save(comp, f"preview/rooms128_{key}.png", {"status": "미리보기"}))
    return made
