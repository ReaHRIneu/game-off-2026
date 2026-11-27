"""챕터 0 맵 2개를 128px 방으로 조립하는 배치 정보(layout.json)와 공용 타일 — D-073, D-075, D-078~D-080.

맵 전체를 그림 한 장으로 만들지 않는다: 4열 × 640px = 2560px로 Web 최대 텍스처(2048)를 넘고,
같은 방(복도 등)을 섹터마다 따로 그리면 메모리가 커진다. 엔진은 섹터마다 방 그림을 그린다.
좌표: 방 그림 1px = 논리 1/4px(D-075). 섹터 = 논리 160×96 = 그림 640×384.
"""
import json

from artkit import EXPORT, PIX, canvas, palette_check, pfill, save
from prop import DOT, _dot, _level_hit
from scene_home import LEVELS

# W-031(D-119): 0-2는 4줄(3F·2F·1F·B1). scene_home.LEVELS는 한 장 그림 폴백용이라 그대로 두고, 방 배치만 여기서 덮어쓴다
LAYOUT_ROWS = dict(LEVELS)
# W-046(D-134): 한 스테이지에 같은 방 그림 3개 이상 금지 — 복도는 변형(corridor_b·corridor_c: L2 소품만 다름)을 섞는다. 게임 판정은 그대로
LAYOUT_ROWS["ch0_home1"] = [
    ["home", "corridor", "corridor_b", "stairs"],            # 2F: 주인공 방 · 복도 · 복도 변형 b(현관문) · 계단실
    ["lobby", "corridor_c", "corridor", "stairs"],           # 1F: 로비 · 복도 변형 c(우편함·자판기) · 복도(바닥 해치) · 계단실
    [None, None, "basement", None],
]
LAYOUT_ROWS["ch0_home2"] = [
    ["home", "corridor", "corridor_b", "corridor_c"],        # 3F: 주인공 방 · 복도(엘리베이터 도착) 바탕 · b · c
    ["corridor_b", "elevator", "corridor", "stairs"],        # 2F: 복도 b(시작) · 엘리베이터 · 레이저 복도(바탕) · 계단실(계단 2F->1F)
    ["lobby", "elevator", "corridor_c", "stairs"],           # 1F: 로비 · 엘리베이터(-> 3F) · 복도 c(바닥 해치) · 계단실(도착)
    [None, None, "boiler", None],                            # B1: 보일러실(함정)
]

W, H = 640, 384
ROOF_PX, GROUND_PX = 24 * 4, 32 * 4
ROOM_OF = {"home": "home", "corridor": "corridor", "stairs": "stairwell", "lobby": "lobby",
           "basement": "basement", "boiler": "boiler", "elevator": "elevator",
           "corridor_b": "corridor_b", "corridor_c": "corridor_c"}


def roof():
    """지붕 띠(섹터 1칸 폭). 위는 투명(하늘은 L0), 아래 32px 슬래브 + 난간."""
    im = canvas(W, ROOF_PX)
    pfill(im, 0, ROOF_PX - 32, W - 1, ROOF_PX - 1, "4"); pfill(im, 0, ROOF_PX - 32, W - 1, ROOF_PX - 29, "3")
    pfill(im, 0, ROOF_PX - 2, W - 1, ROOF_PX - 1, "K"); pfill(im, 0, ROOF_PX - 33, W - 1, ROOF_PX - 33, "K")
    for x in range(8, W, 48):                                    # 난간 기둥 + 손잡이
        pfill(im, x, ROOF_PX - 60, x + 3, ROOF_PX - 33, "2")
    pfill(im, 0, ROOF_PX - 62, W - 1, ROOF_PX - 59, "3"); pfill(im, 0, ROOF_PX - 63, W - 1, ROOF_PX - 63, "K")
    pfill(im, 120, ROOF_PX - 80, 168, ROOF_PX - 33, "2"); pfill(im, 120, ROOF_PX - 80, 168, ROOF_PX - 78, "3")   # 환기구
    return im


def earth():
    """흙(지하에 방이 없는 섹터·땅). 섹터 크기, 2px 점 디더."""
    im = canvas(W, H, PIX["K"])
    for gy in range(0, H // DOT):
        for gx in range(0, W // DOT):
            if _level_hit(gx + gy // 3, gy, 0.5):
                _dot(im, gx * DOT, gy * DOT, "k")
    for (x, y) in [(60, 80), (300, 200), (520, 120), (180, 300), (440, 330)]:     # 돌
        pfill(im, x, y, x + 9, y + 5, "1"); pfill(im, x + 2, y, x + 7, y + 1, "2")
    return im


def manifest_meta(key):
    m = json.loads((EXPORT / "manifest.json").read_text(encoding="utf-8"))
    return m


def build():
    made = []
    for name, im in [("roof", roof()), ("earth", earth())]:
        assert not palette_check(im), name
        made.append(save(im, f"rooms128/common/{name}.png", {"status": "시안", "layer": "L1", "tile": True}))
    man = json.loads((EXPORT / "manifest.json").read_text(encoding="utf-8"))
    entries = man.get("files", man)

    def meta_of(rel):
        for k, v in (entries.items() if isinstance(entries, dict) else []):
            if k.replace("\\", "/").endswith(rel):
                return v
        return {}

    for key, rows in LAYOUT_ROWS.items():
        rooms, sectors = {}, []
        for f, row in enumerate(rows):
            for c, kind in enumerate(row):
                if kind is None:
                    sectors.append({"floor": f, "col": c, "room": None, "tile": "rooms128/common/earth"})
                    continue
                rk = ROOM_OF[kind]
                lights = sorted(p.stem[len("L3_light_"):] for p in (EXPORT / f"rooms128/{rk}").glob("L3_light_*.png"))
                if rk not in rooms:
                    rooms[rk] = {
                        "L1": f"rooms128/{rk}/L1_shell", "L2": f"rooms128/{rk}/L2_props",
                        "L3": {l: f"rooms128/{rk}/L3_light_{l}" for l in lights},
                        "dark": f"rooms128/{rk}/L3_dark",
                        "windows": meta_of(f"rooms128/{rk}/L1_shell.png").get("windows", []),
                    }
                sectors.append({"floor": f, "col": c, "room": rk, "lights": {l: "on" for l in lights}})
        layout = {
            "map": key, "status": "시안", "decisions": ["D-073", "D-075", "D-078", "D-079", "D-080"],
            "px_per_logical": 4, "sector_px": [W, H], "floor_y_px": 352, "ceil_y_px": 28,
            "roof_px": ROOF_PX, "ground_px": GROUND_PX,
            "floors": len(rows), "cols": len(rows[0]),
            "tiles": {"roof": "rooms128/common/roof", "earth": "rooms128/common/earth"},
            "rooms": rooms, "sectors": sectors,
            "objects": [{"kind": "door", "floor": 0, "col": 0, "x_px": W - 72, "sprite": "rooms128/home/L4_door",
                         "note": "주인공 방 문 = 실패 지점 그림(L4). 실패 지점 x는 문 왼쪽 끝(논리 142)"}],
            "light_states": "on | off | auto(D-080 시간대: 밤에 켬). 섹터의 빛이 모두 off면 dark 그림",
        }
        path = EXPORT / f"scenes128/{key}/layout.json"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(layout, ensure_ascii=False, indent=1), encoding="utf-8")
        made.append(path)
    return made
