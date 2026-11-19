"""챕터 1 의뢰 1-1·1-2의 임시 방 배치(layout.json) — W-014.

구시가지 128px 방 그림은 W-015에서 만든다. 그때까지는 챕터 0의 방(rooms128/)을 그대로 재사용해 배치만 새로 짠다.
챕터 0 배치(scenes128/ch0_home1·ch0_home2)를 읽어 방 정의·타일·크기를 가져오고, 섹터 목록만 03-01 §2-1·§2-2 맵 표대로 다시 쓴다.
방이 없는 칸(빈 칸)은 섹터 목록에 넣지 않는다(엔진이 어두운 바깥 배경 단색으로 그린다).
"""
import json

from artkit import EXPORT

# 방 이름은 rooms128/ 아래 폴더 이름. None = 빈 칸
MAPS = {
    # 1-1 OLD TOWN A·B: 2F(층 0) · 1F(층 1) x 5열
    "ch1_old1_tmp": [
        ["home", "corridor", None, "corridor", "stairwell"],      # 2F: A 셋방 · A 끝 방[레일] · (빈 칸) · B 복도 · B 계단실(단말)
        ["lobby", "stairwell", None, "lobby", "basement"],         # 1F: A 상점(시작) · A 계단실 · (빈 칸) · B 로비(출구) · B 창고
    ],
    # 1-2 OLD TOWN C: 3F(층 0) · 2F(층 1) · 1F(층 2) x 5열
    "ch1_old2_tmp": [
        [None, "corridor", None, "corridor", "stairwell"],         # 3F: (빈 칸) · 옥상 출구 방[레일] · (빈 칸) · 옥상 복도 · 계단실
        [None, None, "corridor", "corridor", "stairwell"],         # 2F: (빈 칸) · (빈 칸, 무너진 바닥) · 복도 · 레이저 복도 · 계단실
        ["lobby", "corridor", None, "basement", None],             # 1F: 상점(시작) · 빈 점포[레일] · (빈 칸, 골목) · 창고(단말) · (빈 칸)
    ],
}


def build():
    src = EXPORT / "scenes128" / "ch0_home1" / "layout.json"
    alt = EXPORT / "scenes128" / "ch0_home2" / "layout.json"
    base = json.loads(src.read_text(encoding="utf-8"))
    rooms = dict(json.loads(alt.read_text(encoding="utf-8"))["rooms"])
    rooms.update(base["rooms"])
    made = []
    for key, rows in MAPS.items():
        used = {}
        sectors = []
        for f, row in enumerate(rows):
            for c, rk in enumerate(row):
                if rk is None:
                    continue
                used[rk] = rooms[rk]
                lights = sorted(rooms[rk]["L3"].keys())
                sectors.append({"floor": f, "col": c, "room": rk, "lights": {name: "on" for name in lights}})
        layout = {
            "map": key, "status": "임시(W-014, 챕터 0 방 재사용)", "decisions": base["decisions"],
            "px_per_logical": base["px_per_logical"], "sector_px": base["sector_px"],
            "floor_y_px": base["floor_y_px"], "ceil_y_px": base["ceil_y_px"],
            "roof_px": base["roof_px"], "ground_px": base["ground_px"],
            "floors": len(rows), "cols": len(rows[0]),
            "tiles": base["tiles"], "rooms": used, "sectors": sectors,
            "light_states": base["light_states"],
        }
        path = EXPORT / "scenes128" / key / "layout.json"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(layout, ensure_ascii=False, indent=1), encoding="utf-8")
        made.append(path)
    return made
